# ADR-008: Verified Grounded Runtime Architecture and Data Flow

## PURPOSE
Document the verified runtime architecture of Grounded so future diagramming and review work uses the actual code path instead of the earlier AI-generated assumptions.

This ADR captures the end-to-end flow for a natural-language analytics question: UI request, planner prompt, structured QueryPlan generation, SQL compilation and execution, answer synthesis, trace persistence, and UI rendering.

## STACK
- Frontend: React, Vite, TypeScript, component-based UI.
- Backend: ASP.NET Core Web API in `Grounded.Api`.
- Database: PostgreSQL accessed through Npgsql.
- Prompts: versioned markdown prompts loaded from the `prompts/` tree.
- LLM access: OpenAI-compatible chat-completions transport behind model-invoker abstractions.
- Testing/eval support: deterministic and replay invokers for non-network runs.

## ARCHITECTURE
The primary production path is:
1. The UI `App` calls `useAnalyticsQuery()`, which posts `{ question, conversationId }` to `POST /analytics/query`.
2. `AnalyticsController.ExecuteQuestion()` validates the request and delegates to `AnalyticsQueryPlanService.ExecuteFromQuestionAsync(...)`.
3. `AnalyticsQueryPlanService` loads prior conversation state, resolves supported follow-ups, and otherwise asks the planner gateway for a `QueryPlan`.
4. The planner prompt is rendered from `prompts/planner/v2.md` with `PlannerContextBuilder`-supplied schema, examples, and supported surface.
5. Planner output is parsed into a structured `QueryPlan`, then normalized / repaired as needed.
6. `QueryPlanValidator` rejects unsupported or malformed plans.
7. `TimeRangeResolver` resolves relative presets into concrete UTC ranges.
8. `QueryPlanCompiler` compiles the plan into parameterized SQL using `SqlFragmentRegistry`.
9. `SqlSafetyGuard` blocks unsafe SQL shapes.
10. `AnalyticsQueryExecutor` executes the SQL read-only against PostgreSQL with a statement timeout and returns rows plus execution metadata.
11. `AnswerSynthesizer` loads `prompts/answer-synthesizer/v1.md`, builds an `AnswerSynthesizerRequest` from the user question, plan, rows, columns, and execution metadata, sends the synthesis prompt to the LLM gateway, and validates the returned answer.
12. `AnalyticsQueryPlanService` assembles `QueryExecutionTrace`, persists a `PersistedTraceRecord` through `TraceRepository`, saves conversation state, and returns `ExecuteQueryPlanResponse` to the UI.
13. The UI renders the answer panel, trace tab, plan JSON, compiled SQL, and evaluation panel from the response payload.

Secondary endpoints are part of the architecture but not the main query path:
- `POST /analytics/query-plan` bypasses the planner and executes a caller-supplied `QueryPlan`.
- `POST /analytics/eval` runs the benchmark/eval pipeline.

Important response fields exposed to the UI are:
- `answer.summary`, `answer.keyPoints`, `answer.tableIncluded`
- `metadata.rowCount`, `metadata.durationMs`, `metadata.llmLatencyMs`
- `trace.requestId`, `trace.traceId`, `trace.plannerStatus`, `trace.synthesisStatus`, `trace.finalStatus`, `trace.failureCategory`, `trace.compiledSql`, `trace.queryPlan`, `trace.planner`, `trace.synthesizer`, `trace.startedAt`, `trace.completedAt`

The planner stage is a structured-output LLM call, not raw SQL generation. The answer stage is a second LLM call that only runs after SQL execution returns rows.

## PATTERNS
- Versioned prompts are stored as files and loaded by key/version.
- The planner and synthesizer are separate LLM stages with separate prompts and traces.
- `QueryPlan` is the contract between planner and SQL compiler.
- SQL is always compiled server-side and parameterized.
- SQL execution is guarded and read-only.
- Traces are persisted as first-class side effects.
- Conversation state is used only to support bounded follow-ups.
- Deterministic and replay model invokers exist for tests and evals; they are not the production user-query path.

## TRADEOFFS
- Using two LLM calls increases latency and token cost, but it keeps SQL generation safe and makes the answer stage grounded in actual result rows.
- A strict `QueryPlan` contract reduces flexibility, but it makes the planner testable and the compiler deterministic.
- The system stores rich trace data, which increases persistence overhead, but it makes debugging and diagramming much more reliable.
- A direct `/analytics/query-plan` endpoint exists for deterministic callers, but it is a separate path and should not be confused with the user-facing planner flow.

## PHILOSOPHY
- Draw and document the code that exists, not the architecture that was assumed.
- Treat the UI as a thin client over the analytics API.
- Never imply that the LLM generates raw SQL directly.
- Keep planner, executor, and answer synthesis as distinct responsibilities.
- Make trace data observable and diagram-ready by design.
- Prefer source-of-truth notes over regenerated diagrams when the implementation is subtle or easy to misread.
