# Grounded UI (spec)

This UI is intentionally small. It exists to make the pipeline easy to demo and inspect: ask a question, see the answer, then flip through the internals.

## What the UI does

- Single page app with a stable per-session `conversationId` (UUID) so follow-ups work without user setup.
- On submit, `POST /analytics/query` with `{ question, conversationId }`.
- Left pane:
  - answer summary + key points
  - raw result rows in a table
  - failure rendering for validation / execution errors
- Right pane (tabs):
  - Trace: request/trace IDs, stage status, timings, token counts and cost estimate when available
  - Plan: the `QueryPlan` returned by the planner
  - SQL: the compiled, parameterized SQL from the compiler (the model never generates this)
  - Eval: a baked-in snapshot of the benchmark results (no API call; see `eval/artifacts/`)

## States

- Empty: suggested questions + schema cheat sheet (toggleable)
- Loading: disables input and shows skeletons
- Success: answer + rows + internals populated
- Error: shows failure category and structured error list
- Locked: optional auth gate (see below)

## Auth gate

The UI has a feature-flagged lock screen controlled by `VITE_AUTH_ENABLED`.

This is not real authentication. It exists for demos where the UI is protected by an external system (Cloudflare Access, Basic Auth, etc.) and you want the UI to reflect "restricted" mode.

## Non-goals

- No query builder
- No multi-tab navigation
- No live benchmark runner from the UI (the eval tab is a snapshot)
