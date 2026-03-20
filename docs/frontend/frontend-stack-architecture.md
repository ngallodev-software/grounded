# Grounded Frontend Stack & Architecture

## Stack
- React
- TypeScript
- Vite
- Tailwind
- shadcn/ui
- TanStack Query

## Architecture

User
 ↓
Cloudflare Access
 ↓
Frontend (React)
 ↓
Backend (.NET API)
 ↓
Postgres
 ↓
OpenAI API

## Auth Flow
Feature flag → session check → allow or block UI
