# Phase 5 Spec — Conversation State, Planner Hardening, and Operational Controls

## 1. Purpose

Phase 5 hardens the Phase 3–4 pipeline in three areas:

1. **Narrow conversation state** — add bounded follow-up support backed by compact per-conversation state in Postgres.
2. **Planner alignment** — tighten the planner prompt, remove `simple_follow_up` from the advertised surface, add canonical `__unsupported__` sentinel examples, and align `PlannerContextBuilder` with `QueryPlanValidator` exactly.
3. **Operational controls** — add trace persistence, eval persistence, startup schema migration, and structured logging. Document server-side rate limiting, token caps, audit logging, and kill-switch requirements.

---

## 2. Scope

### In scope
- `ConversationStateService` + `NpgsqlConversationStateRepository`
- `POST /analytics/query` — NL endpoint with optional `conversationId`
- Compact 5-field conversation state stored per `conversationId`
- Three deterministic follow-up patterns (last quarter, same by category, electronics only)
- `ITraceRepository` / `NpgsqlTraceRepository` — per-request execution trace persistence
- `IEvalRepository` / `NpgsqlEvalRepository` — eval run persistence
- `SchemaInitializer` (`IHostedService`) — startup DDL migration
- `OpenAiCompatiblePlannerGateway` and `OpenAiCompatibleAnswerGateway` replacing deterministic stubs as production defaults
- `ModelInvoker` abstraction (`IModelInvoker`, `ModelInvokerResolver`, `DeterministicModelInvoker`, `ReplayModelInvoker`, `OpenAiCompatibleModelInvoker`)
- `PlannerContextBuilder` / `PlannerPromptRenderer` / `PlannerResponseParser` / `PlannerResponseRepairService` as discrete services
- `FailureCategories` constants + structured `FailureCategory` on all responses and traces
- `EvalRunSummary` with per-run aggregate metrics
- Structured logging on all controller 500-catch paths
- `conversationId` max-length (128) validation at API boundary

### Out of scope
- Frontend / UI
- Server-side rate limiting implementation *(required before production; documented in `docs/grounded-execution-prompt-pack.md`)*
- Token/request cap enforcement *(documented, not implemented)*
- Audit logging middleware *(documented, not implemented)*
- Trace ID propagation headers *(documented, not implemented)*
- Live-model kill switch *(documented, not implemented)*

---

## 3. Conversation State Design

### Stored fields (exactly 5)
| Field | Type | Notes |
|---|---|---|
| `questionType` | string | Last resolved question type |
| `metric` | string | Last executed metric |
| `dimension` | string? | Last dimension (nullable) |
| `filters` | `FilterSpec[]` | Last filter set |
| `timeRange` | `TimeRangeSpec` | Last time range |

### Intentionally not stored
- `limit` — ranking limits are not inferred for follow-ups
- `timeGrain` — time-series grains are not inferred for follow-ups
- Raw question text
- Prior result rows
- Any LLM-generated prose

### Follow-up resolution
`ConversationStateService.Resolve()` is called before the planner. If the question matches a follow-up prefix (`what about`, `how about`, `same thing`, `same question`, `same query`, `now `), the service attempts deterministic resolution:

1. No prior state → reject with `unsupported_follow_up`
2. `questionType` not in `{aggregate, grouped_breakdown}` → reject (unstored fields required)
3. Pattern match against three supported regexes → produce derived `QueryPlan`
4. No pattern match → reject with `unsupported_follow_up`

Supported patterns:
- `same thing/question/query [, but] by category` → `grouped_breakdown` on `product_category`
- `what/how about last quarter` → same plan with `last_quarter` time range
- `what/how about just electronics` / `now only for electronics` → add/replace `product_category eq Electronics` filter

---

## 4. API Contract

### `POST /analytics/query`
```json
// Request
{
  "question": "What was total revenue last month?",
  "conversationId": "optional-client-generated-id-max-128-chars"
}

// Success (200)
{
  "status": "success",
  "rows": [...],
  "metadata": { ... },
  "answer": { "summary": "...", "keyPoints": [...], "tableIncluded": true },
  "trace": { "requestId": "...", "traceId": "...", "plannerStatus": "completed", ... }
}

// Semantic failure (422)
{
  "status": "error",
  "failureCategory": "unsupported_request",
  "errors": [{ "code": "unsupported_follow_up", "message": "..." }],
  "trace": { ... }
}
```

`POST /analytics/query-plan` is unchanged — still accepts a full `QueryPlan` directly.

---

## 5. Planner Prompt Hardening

- `simple_follow_up` removed from `SupportedQuestionTypes` in `PlannerContextBuilder`
- Canonical `__unsupported__` sentinel examples added to both `PlannerContextBuilder` and `prompts/planner/v1.md`
- Prompt rules enforce `usePriorState = false`, `version = "1.0"`, no SQL generation
- `time_series` sort note clarifies sort controls display order, not chronological output
- Validator and prompt are aligned: every rule in the prompt has a corresponding validator check

---

## 6. Services

| Service | Type | Purpose |
|---|---|---|
| `ConversationStateService` | Scoped | Follow-up detection + resolution |
| `NpgsqlConversationStateRepository` | Scoped | Postgres persistence for conversation state |
| `NpgsqlTraceRepository` | Scoped | Persist `PersistedTraceRecord` per request |
| `NpgsqlEvalRepository` | Scoped | Persist `PersistedEvalRun` per eval run |
| `SchemaInitializer` | Hosted singleton | Startup DDL for all three tables |
| `PlannerContextBuilder` | Singleton | Builds `PlannerContext` from `SqlFragmentRegistry` |
| `PlannerPromptRenderer` | Singleton | Assembles prompt + context + question |
| `PlannerResponseParser` | Singleton | Parses raw JSON → `QueryPlan` |
| `PlannerResponseRepairService` | Singleton | One-pass repair (fence strip + JSON extract) |
| `ModelInvokerResolver` | Singleton | Resolves `IModelInvoker` by name |
| `OpenAiCompatibleModelInvoker` | Singleton (HttpClient) | Calls OpenAI-compatible API |
| `ReplayModelInvoker` | Singleton | Lazy fixture-based replay for tests |
| `DeterministicModelInvoker` | Singleton | Deterministic local engine for tests |
| `OpenAiCompatiblePlannerGateway` | Singleton | Production `ILlmPlannerGateway` |
| `OpenAiCompatibleAnswerGateway` | Singleton | Production `ILlmGateway` |

---

## 7. Environment Variables

| Variable | Purpose |
|---|---|
| `GROUNDED_PLANNER_BASE_URL` | Base URL for planner HTTP client (default: `https://api.openai.com/v1/`) |
| `GROUNDED_PLANNER_API_KEY` | API key for planner gateway |
| `GROUNDED_PLANNER_MODEL` | Model name for planner |
| `GROUNDED_PLANNER_TIMEOUT_SECONDS` | HTTP client timeout (default: 30) |
| `GROUNDED_SYNTHESIS_API_KEY` | API key for synthesis gateway |
| `GROUNDED_SYNTHESIS_MODEL` | Model name for synthesis |
| `GROUNDED_REPLAY_MODE` | Set to `"true"` to use `ReplayModelInvoker` |

---

## 8. Test Coverage

| Test file | Coverage |
|---|---|
| `AnalyticsPhase5ConversationTests.cs` | Supported follow-up mutation, unsupported follow-up rejection, bounded prior-state prompt packaging |
| `AnalyticsPhase1PlannerTests.cs` | PlannerTrace shape, repair success/failure, unsupported question type, timeout failure category, sentinel mapping, `PlannerContextBuilder` exclusions, prompt canonical wording |

---

## 9. Acceptance Criteria

- `POST /analytics/query` accepts optional `conversationId` and routes follow-ups through `ConversationStateService` before the planner
- Supported follow-up patterns produce deterministic `QueryPlan` mutations without invoking the planner
- Unsupported follow-ups are rejected with stable error code `unsupported_follow_up` and `failureCategory: unsupported_request`
- `ranking` and `time_series` follow-ups are rejected (unstored fields required)
- Planner prompt does not advertise `simple_follow_up`; validator rejects it if model emits it anyway
- All execution traces written to `llm_traces` table
- All eval runs written to `eval_runs` table
- Schema created at startup via `SchemaInitializer`, not per-request
- `conversationId` max 128 chars enforced at API boundary
- Controller 500-catch paths log structured errors
- 43/43 tests pass
