# Grounded Frontend Stack & Architecture

## Stack
- React + TypeScript
- Vite
- Tailwind + shadcn/ui primitives (Radix)
- TanStack Query for the single `/analytics/query` mutation

## Architecture

User -> UI -> API -> Postgres

LLM calls happen behind the API boundary:

User -> UI -> API -> (planner LLM) -> QueryPlan -> compiler -> Postgres -> (synth LLM) -> answer

## Auth Flow

- Optional gate in the UI via `VITE_AUTH_ENABLED`
- For real deployments, put auth in front of the UI (Cloudflare Access, SSO, etc.)

## Routing / base path

- Dev mode (`npm run dev`): served at `/`
- Docker build: `VITE_BASE_PATH=/grounded/` so it can be hosted behind a reverse proxy without owning the root path

## API connectivity

- Dev server proxies `/analytics/*` to `VITE_API_BASE_URL` (defaults to `http://localhost:5252`)
- Docker UI image uses nginx to proxy `/analytics/*` to the `api` container
