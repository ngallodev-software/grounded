# Grounded UI build notes

This is a lightweight reference for how the UI is intended to evolve. It's written as a checklist on purpose so changes stay small and reviewable.

## When changing the UI

- Keep it single-page.
- Treat `/analytics/query` as the only "real" interaction.
- Don’t add free-form SQL input or anything that bypasses the `QueryPlan` pipeline.
- Keep internals visible and honest (Trace / Plan / SQL / Eval).

## Adding a new view of data

- Prefer a new tab in the internals pane over a new page/route.
- Ensure the tab works on a narrow right pane (no horizontal scrolling).
- If the view depends on API changes, update `openapi.yaml` and the UI client types together.

## Env vars (UI)

- `VITE_API_BASE_URL`: dev proxy target (defaults to `http://localhost:5252`)
- `VITE_BASE_PATH`: build-time base path (Docker uses `/grounded/`)
- `VITE_AUTH_ENABLED`: enable the lock screen
