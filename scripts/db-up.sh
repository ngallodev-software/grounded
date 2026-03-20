#!/usr/bin/env bash
# Start Postgres and wait for it to be healthy.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "No .env found. Copy .env.example to .env and set POSTGRES_PASSWORD."
  exit 1
fi

echo "Starting Postgres …"
docker compose up -d postgres

echo "Waiting for healthcheck …"
for i in $(seq 1 30); do
  status=$(docker inspect --format='{{.State.Health.Status}}' grounded-postgres 2>/dev/null || echo "missing")
  if [ "$status" = "healthy" ]; then
    echo "Postgres is healthy."
    break
  fi
  sleep 2
done

# shellcheck source=/dev/null
source .env
echo ""
echo "Connection string:"
echo "  Host=127.0.0.1;Port=5432;Database=grounded;Username=grounded;Password=${POSTGRES_PASSWORD}"
