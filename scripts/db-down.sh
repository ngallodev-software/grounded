#!/usr/bin/env bash
# Stop Postgres (keeps the volume).
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose down
echo "Postgres stopped."
