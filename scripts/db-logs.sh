#!/usr/bin/env bash
# Tail Postgres logs.
set -euo pipefail
cd "$(dirname "$0")/.."
docker compose logs -f postgres
