# Grounded - Verified Source of Truth

This document is the correction set for the repo's architecture diagrams and ADRs.
It reflects the observed implementation, not the earlier AI-generated assumptions.

## What the app is

Grounded is a React + ASP.NET Core analytics app backed by PostgreSQL.
The user types a natural-language analytics question in the UI, the backend turns that question into a structured `QueryPlan`, converts that plan to SQL, executes the SQL against Postgres, and then synthesizes a grounded answer from the returned rows.

The app also exposes:
- a direct plan execution endpoint for deterministic tests and programmatic callers
- an eval endpoint for benchmark runs
- a conversation-state path so follow-up questions can reuse prior plans

## Verified runtime path

### 1. UI to backend

Observed UI entry point:
- `grounded-ui/src/App.tsx`
- `grounded-ui/src/hooks/useQuery.ts`
- `grounded-ui/src/lib/api.ts`

The UI flow is:
1. `App` calls `useAnalyticsQuery()`.
2. `useAnalyticsQuery()` wraps `postQuery(req)`.
3. `postQuery()` sends `POST /analytics/query`.
4. `App` passes `{ question, conversationId }` into that request.

The answer and internals panes render the returned payload:
- `grounded-ui/src/components/AnswerPanel.tsx`
- `grounded-ui/src/components/InternalsPanel.tsx`

### 2. Backend controller

Observed controller:
- `Grounded.Api/Controllers/AnalyticsController.cs`

Request path:
- `POST /analytics/query`

Controller behavior:
1. Validates `question` length and optional `conversationId`.
2. Calls `AnalyticsQueryPlanService.ExecuteFromQuestionAsync(...)`.
3. Returns `200` on success, `422` on handled failure, `400` for invalid input, `500` for unexpected exceptions.

There is a second primary endpoint:
- `POST /analytics/query-plan`

That endpoint bypasses the planner and executes a caller-supplied `QueryPlan`.

There is also:
- `POST /analytics/eval`

That endpoint runs the benchmark suite.

### 3. Question handling and planner selection

Observed orchestrator:
- `Grounded.Api/Services/AnalyticsQueryPlanService.cs`

The orchestration sequence is:
1. Capture `startedAt` and a new `traceId`.
2. Load prior conversation state using `ConversationStateService.GetAsync(...)`.
3. Resolve whether the incoming question is a follow-up using `ConversationStateService.Resolve(...)`.
4. If it is a supported follow-up, reuse the resolved `QueryPlan`.
5. Otherwise call `_plannerGateway.PlanFromQuestionAsync(userQuestion, priorState, cancellationToken)`.

Important point:
- the service depends on `ILlmPlannerGateway`
- tests use `DeterministicLlmPlannerGateway`
- the live path should be drawn as the production planner gateway, not the deterministic test gateway

### 4. Planner prompt and structured output

Observed planner prompt source:
- `prompts/planner/v2.md`

Observed prompt renderer:
- `Grounded.Api/Services/PlannerPromptRenderer.cs`

Observed context builder:
- `Grounded.Api/Services/PlannerContextBuilder.cs`

Observed parser/repair stack:
- `Grounded.Api/Services/PlannerResponseParser.cs`
- `Grounded.Api/Services/PlannerResponseRepairService.cs`

What this means:
1. The planner prompt is rendered from the versioned prompt store.
2. `PlannerContextBuilder` injects supported question types, metrics, dimensions, filters, schema fragments, and examples.
3. Prior conversation state is appended when present.
4. The prompt is sent as structured JSON output.
5. The response is parsed into a `QueryPlan`.
6. Alias normalization and recovery logic are applied if needed.

This is not a raw free-form SQL generation step.
The planner produces a constrained `QueryPlan`, then the backend compiles SQL from that plan.

### 5. Plan validation and SQL compilation

Observed validators and compiler:
- `Grounded.Api/Services/QueryPlanValidator.cs`
- `Grounded.Api/Services/TimeRangeResolver.cs`
- `Grounded.Api/Services/QueryPlanCompiler.cs`
- `Grounded.Api/Services/SqlSafetyGuard.cs`

Observed SQL execution service:
- `Grounded.Api/Services/AnalyticsQueryExecutor.cs`

Verified order:
1. Validate the `QueryPlan`.
2. Resolve relative time presets into UTC boundaries.
3. Compile the plan to parameterized SQL.
4. Safety-check the compiled SQL.
5. Execute the SQL read-only against Postgres.

Important execution details:
- parameterized SQL only
- `SET statement_timeout = 15000`
- transaction marked read-only
- row cap enforced by the compiler and guarded again by execution

### 6. Answer synthesis

Observed prompt source:
- `prompts/answer-synthesizer/v1.md`

Observed synthesizer:
- `Grounded.Api/Services/AnswerSynthesizer.cs`

Observed output validator:
- `Grounded.Api/Services/AnswerOutputValidator.cs`

Observed model gateway:
- `Grounded.Api/Services/OpenAiCompatibleAnswerGateway.cs`
- `Grounded.Api/Services/LlmGateway.cs`

Verified synthesis input:
- `userQuestion`
- `queryPlan`
- `rows`
- `columns`
- `executionMetadata`
- `promptVersion`

Verified synthesis output:
- `summary`
- `keyPoints`
- `tableIncluded`

The answer output is validated to ensure:
- summary is present
- key points are present and capped
- the answer references at least one visible value from the result rows when rows exist

### 7. Trace persistence and response payload

Observed trace persistence:
- `Grounded.Api/Services/TraceRepository.cs`

The backend persists:
- `trace_id`
- `request_id`
- timestamps
- final status
- failure category
- `query_plan_json`
- `validation_errors_json`
- `compiled_sql`
- `row_count`
- `planner_attempt_json`
- `synthesis_attempt_json`

The response payload returned to the UI includes:
- `status`
- `rows`
- `metadata`
- `answer`
- `trace`

### 8. UI display surface

Observed UI trace tabs:
- `trace`
- `plan`
- `sql`
- `eval`

The UI shows:
- request id
- trace id
- planner status
- synthesis status
- final status
- failure category
- duration
- row count
- planner and synthesizer token metrics
- raw `queryPlan` JSON
- compiled SQL

The answer panel shows:
- summary
- key points
- row count
- duration
- LLM latency
- result table

## Diagram constraints

When redrawing diagrams, do not:
- show the UI calling OpenAI directly
- show the SQL executor calling OpenAI
- show the planner emitting raw SQL
- show a vector database or RAG layer
- show the answer synthesizer before SQL execution
- show the trace repository as part of the request path instead of the side-effect path

When redrawing diagrams, do:
- show the UI as a thin client over `/analytics/query`
- show the controller as the entrypoint into the backend pipeline
- show the planner prompt as a distinct LLM call
- show SQL compilation and safety validation as backend-only steps
- show answer synthesis as a second LLM call after rows are available
- show trace persistence as a side effect after the execution result is known
