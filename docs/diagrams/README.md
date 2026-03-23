# Grounded - Architecture Artifacts

This folder now contains the verified source-of-truth notes and Mermaid drafts that should be used to redraw diagrams.

The prior diagram set in this folder was misleading and has been archived in:
`docs/archive/diagrams/2026-03-23-legacy-diagrams.md`

Use these files instead:

| File | Purpose |
|---|---|
| [00-source-of-truth.md](./00-source-of-truth.md) | Verified runtime flow, data flow, trace fields, and component boundaries |
| [01-diagram-instructions.md](./01-diagram-instructions.md) | Exact instructions for rebuilding the system context, component, and sequence diagrams correctly |
| [02-system-context.mmd](./02-system-context.mmd) | Mermaid draft of the top-level actors and system boundaries |
| [03-component-architecture.mmd](./03-component-architecture.mmd) | Mermaid draft of the backend and UI component layout |
| [04-request-pipeline.mmd](./04-request-pipeline.mmd) | Mermaid draft of the verified UI to planner to SQL to answer flow |
| [05-trace-contracts.mmd](./05-trace-contracts.mmd) | Mermaid draft of the API response, trace, and persistence contracts |
