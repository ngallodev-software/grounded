#!/usr/bin/env bash
# Verify the Cloudflare edge-only ingress path.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "No .env found. Copy .env.example to .env and set required values."
  exit 1
fi

PUBLIC_URL="${PUBLIC_URL:-}"
API_LOCAL_URL="${API_LOCAL_URL:-http://127.0.0.1:5252/analytics/query}"
CHECK_LOCAL="${CHECK_LOCAL:-1}"
START_STACK="${START_STACK:-1}"

if [ "$CHECK_LOCAL" = "0" ] && [ -z "$PUBLIC_URL" ]; then
  echo "PUBLIC_URL is required when CHECK_LOCAL=0."
  exit 1
fi

if [ "$START_STACK" = "1" ]; then
  echo "Starting services …"
  docker compose up -d
fi

if [ "$CHECK_LOCAL" = "1" ]; then
  echo "Waiting for Cloudflare tunnel …"
  for _ in $(seq 1 30); do
    if docker compose logs --tail=500 cloudflared 2>/dev/null | grep -q 'Registered tunnel connection'; then
      echo "Cloudflare tunnel is connected."
      break
    fi
    sleep 2
  done

  if ! docker compose logs --tail=500 cloudflared 2>/dev/null | grep -q 'Registered tunnel connection'; then
    echo "Cloudflare tunnel did not report an active connection."
    exit 1
  fi

  echo "Checking local UI …"
  ui_body="$(docker compose exec -T ui wget --no-check-certificate -qO- https://127.0.0.1/)"
  if ! printf '%s' "$ui_body" | grep -q '<div id="root">'; then
    echo "Local UI check failed: app root element not found."
    exit 1
  fi

  echo "Checking local API …"
  api_response="$(curl -fsS -X POST "$API_LOCAL_URL" \
    -H 'Content-Type: application/json' \
    -d '{"question":"Total revenue last month"}')"
  if ! printf '%s' "$api_response" | grep -q '"status":"success"'; then
    echo "Local API check failed: expected status=success."
    exit 1
  fi

  echo "Checking local TLS listener on 443 …"
  tls_body="$(docker compose exec -T ui wget --no-check-certificate -qO- https://127.0.0.1/)"
  if ! printf '%s' "$tls_body" | grep -q '<div id="root">'; then
    echo "Local TLS check failed: app root element not found."
    exit 1
  fi
fi

if [ -n "$PUBLIC_URL" ]; then
  echo "Checking public HTTPS URL …"
  case "$PUBLIC_URL" in
    https://*)
      ;;
    *)
      echo "Public URL check failed: PUBLIC_URL must start with https://"
      exit 1
      ;;
  esac

  public_body="$(curl -fsS "$PUBLIC_URL")"
  if ! printf '%s' "$public_body" | grep -q '<div id="root">'; then
    echo "Public URL check failed: app root element not found."
    exit 1
  fi
fi

echo "HTTPS ingress checks passed."
