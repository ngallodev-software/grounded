# Diagram Instructions

Use this document as the build spec for the corrected diagrams.

## 1. System Context Diagram

Draw these actors and systems:
- User
- Grounded UI
- Grounded.Api
- PostgreSQL
- OpenAI-compatible LLM endpoint

Draw these relationships:
- User interacts with the UI
- UI sends analytics questions to Grounded.Api
- Grounded.Api reads and writes PostgreSQL
- Grounded.Api sends planner and synthesis prompts to the LLM endpoint

Do not draw:
- direct UI to LLM communication
- direct SQL execution from the UI
- a separate vector store

## 2. Component Architecture Diagram

Show the following major groups:
- Browser / React UI
- API controller layer
- analytics orchestration layer
- planner prompt + parser stack
- SQL compiler / validator / executor stack
- answer synthesis stack
- trace and conversation persistence
- eval runner

Important components to include:
- `App`
- `useAnalyticsQuery`
- `AnalyticsController`
- `AnalyticsQueryPlanService`
- `ConversationStateService`
- `PlannerPromptRenderer`
- `PlannerContextBuilder`
- `PlannerResponseParser`
- `PlannerResponseRepairService`
- `QueryPlanValidator`
- `TimeRangeResolver`
- `QueryPlanCompiler`
- `SqlSafetyGuard`
- `AnalyticsQueryExecutor`
- `AnswerSynthesizer`
- `AnswerOutputValidator`
- `TraceRepository`
- `ModelInvokerResolver`
- `OpenAiCompatibleModelInvoker`
- `DeterministicModelInvoker`
- `ReplayModelInvoker`

Show the planner and answer LLM calls as separate stages.
Do not collapse them into one generic LLM box.

## 3. Request Pipeline Sequence Diagram

Use this exact order for the primary query path:
1. User submits question in the UI.
2. UI calls `POST /analytics/query`.
3. Controller validates request shape.
4. Orchestrator loads conversation state.
5. Orchestrator resolves supported follow-up or calls planner gateway.
6. Planner prompt renderer loads `prompts/planner/v2.md`.
7. Planner context builder injects schema and examples.
8. Planner gateway sends the prompt to the OpenAI-compatible endpoint.
9. Planner response is parsed, normalized, and optionally repaired.
10. Plan is validated.
11. Time range is resolved.
12. SQL is compiled.
13. SQL is safety-checked.
14. SQL executes against Postgres in read-only mode.
15. Result rows are passed to answer synthesis.
16. Answer synthesizer loads `prompts/answer-synthesizer/v1.md`.
17. Answer synthesis request is sent to the OpenAI-compatible endpoint.
18. Answer JSON is validated.
19. Trace and attempts are persisted.
20. Conversation state is updated.
21. Response returns to the UI.
22. UI renders answer, trace, SQL, and plan.

Required branches:
- invalid request input
- unsupported follow-up
- planner failure
- validation failure
- SQL safety failure
- execution failure
- synthesis failure with partial success

## 4. Trace And Data Contracts Diagram

Show these payloads:
- `QueryRequest`
- `QuerySuccessResponse`
- `QueryErrorResponse`
- `QueryTrace`
- `LlmStageTrace`
- `AnswerDto`
- `PlannerTrace`
- `SynthesizerTrace`
- `PersistedTraceRecord`

Important fields to show in the trace box:
- `requestId`
- `traceId`
- `plannerStatus`
- `synthesisStatus`
- `finalStatus`
- `failureCategory`
- `durationMs`
- `llmLatencyMs`
- `rowCount`
- `compiledSql`
- `queryPlan`
- `planner`
- `synthesizer`
- `startedAt`
- `completedAt`

Important fields to show in the answer box:
- `summary`
- `keyPoints`
- `tableIncluded`

Important fields to show in the metadata box:
- `rowCount`
- `durationMs`
- `llmLatencyMs`

## 5. What Not To Draw

Avoid these mistakes:
- planner emits raw SQL directly
- answer synthesis happens before execution
- trace repository is part of the core control path
- UI reads directly from the database
- eval flow is mixed into the main request path
- the deterministic test gateways are shown as the production path

## 6. Suggested Labels

Use these labels for precision:
- "Planner prompt: `prompts/planner/v2.md`"
- "Answer prompt: `prompts/answer-synthesizer/v1.md`"
- "Structured `QueryPlan` JSON"
- "Parameterised SQL"
- "Read-only Postgres execution"
- "Grounded answer JSON"
- "Trace persistence"
