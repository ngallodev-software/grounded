#!/usr/bin/env bash
# Run the API with environment variables loaded from .env
set -euo pipefail

cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "No .env found. Copy .env.example to .env and fill in values."
  exit 1
fi

# Export all non-comment, non-empty lines from .env
set -o allexport
# shellcheck source=/dev/null
source .env
set +o allexport

cd Grounded.Api
exec dotnet run "$@"
