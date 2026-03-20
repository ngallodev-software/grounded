#!/usr/bin/env bash
# Tear down Postgres and delete the volume (destructive).
set -euo pipefail
cd "$(dirname "$0")/.."

echo "WARNING: This will delete all Postgres data."
read -r -p "Type 'yes' to continue: " confirm
if [ "$confirm" != "yes" ]; then
  echo "Aborted."
  exit 0
fi

docker compose down -v
echo "Postgres container and volume removed."
