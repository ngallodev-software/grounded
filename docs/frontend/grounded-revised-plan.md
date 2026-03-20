# Grounded — Revised Plan

## Goal

Turn `Grounded` from a strong deterministic architecture demo into a credible, production-shaped LLM backend system for natural-language analytics.

Core contract remains unchanged:

`LLM -> structured QueryPlan -> validator -> SQL compiler -> safety guard -> Postgres execution`

The model does **not** generate executable SQL.

---

## Why This Project Matters

`Grounded` is the closest match to the target role’s core problem: natural language to structured data query to grounded answer. It already demonstrates strong .NET backend design, SQL safety, orchestration, and evaluation. The main credibility gap is that the planner boundary is still deterministic rather than provider-backed.

---

## Revised Priorities

1. Make the planner real first.
2. Make planner behavior observable and measurable.
3. Expand evaluation before adding more product surface.
4. Make synthesis real only after the planner and eval loop are solid.
5. Add compact conversation state last.

---

## Critical Design Principles

### 1. Structured planning over raw SQL generation
- The model outputs `QueryPlan`, not SQL.
- Application code owns validation, compilation, and execution.

### 2. Deterministic safety boundaries
- Whitelist validation
- SQL fragment registry as source of truth
- Read-only execution
- Row caps and timeouts

### 3. Reproducible evaluation
- Real provider in production path
- Replay fixtures in CI
- Prompt/model changes compared against benchmarks

### 4. Production-shaped observability
- Trace every model call
- Persist failures with explicit categories
- Expose useful status in API responses

---

## Revised Phase Plan

## Phase 1 — Real Planner Integration

### Objective
Replace the fake planner with a real provider-backed planner while preserving deterministic validation and execution.

### Deliverables
- `PlannerContextBuilder`
- planner prompt file + checksum/versioning
- real provider-backed planner gateway
- strict JSON parser
- one repair attempt
- planner metadata in traces/responses

### New or Updated Components
- `LlmIntegrationDemo.Api/Services/PlannerContextBuilder.cs`
- `LlmIntegrationDemo.Api/Services/PlannerPromptRenderer.cs`
- `LlmIntegrationDemo.Api/Services/PlannerResponseParser.cs`
- `LlmIntegrationDemo.Api/Services/PlannerResponseRepairService.cs`
- `LlmIntegrationDemo.Api/Services/OpenAiCompatiblePlannerGateway.cs`
- `LlmIntegrationDemo.Api/Models/PlannerModels.cs`
- `prompts/planner/v1.md`

### Acceptance Criteria
- Different questions produce meaningfully different valid plans.
- Invalid planner outputs fail deterministically.
- One malformed JSON response can be repaired once.
- Planner metadata appears in traces and eval results.

---

## Phase 2 — Shared Invocation, Traces, and Failure Taxonomy

### Objective
Make model behavior observable, debuggable, and measurable.

### Deliverables
- shared model invocation layer
- persisted traces
- explicit failure taxonomy
- request correlation IDs
- trace summaries in API responses

### Suggested Internal Abstractions
- `IModelInvoker`
- `ModelRequest`
- `ModelResponse`
- `ModelUsage`
- `ModelFailure`

### Failure Taxonomy
- `transport_failure`
- `timeout`
- `provider_error`
- `json_parse_failure`
- `planner_validation_failure`
- `unsupported_request`
- `sql_safety_failure`
- `execution_failure`
- `synthesis_failure`

### Acceptance Criteria
- Every planner and synthesizer call creates a persisted trace.
- Failures show categorized cause, not generic exceptions.
- Eval output summarizes failures by category.

---

## Phase 3 — Benchmark Expansion and Replay Mode

### Objective
Make prompt/model iteration benchmark-driven instead of anecdotal.

### Deliverables
- expanded benchmark corpus
- richer benchmark schema
- replay fixtures
- upgraded scorecards

### Minimum Benchmark Coverage
- aggregate questions
- grouped breakdown questions
- ranking questions
- time-series questions
- unsupported or ambiguous questions
- adversarial/schema-escape questions
- follow-ups that should currently be rejected

### Acceptance Criteria
- Benchmarks run against real provider or replay mode.
- CI runs fully in replay mode.
- Prompt changes can be compared against meaningful metrics.

---

## Phase 4 — Real Answer Synthesis

### Objective
Replace deterministic synthesis with a real provider-backed grounded answer step.

### Deliverables
- provider-backed answer gateway
- stricter grounding validator
- synthesis trace persistence
- visible fallback behavior

### Acceptance Criteria
- Real synthesis responses are persisted and measurable.
- Grounding validator rejects unsupported summaries.
- Fallback behavior is visible in traces and evals.

---

## Phase 5 — Compact Conversation State

### Objective
Add narrow follow-up support without becoming a general chat-memory system.

### Deliverables
- deterministic conversation state persistence
- narrow follow-up handling
- bounded planner context for follow-ups

### Store Only
- prior metric
- prior dimension
- prior filters
- prior time range
- prior question type

### Acceptance Criteria
- Narrow follow-up support works.
- State remains structured and explainable.
- Planner context stays bounded.

---

## Cross-Cutting Additions

### Observability and SLOs
- success rate
- p95 latency
- cost per request
- dashboards for planner validity, execution success, failure categories
- alerts for spikes in parse failures, SQL safety failures, or timeouts

### Cost Controls
- token budgets
- hard request caps
- optional adaptive model selection
- planner-output caching for identical inputs

### Security and Data Governance
- prompt injection defenses
- no free-form SQL
- PII redaction in traces
- audit logs for executed queries
- role-based access for query/eval endpoints if needed

### API Contracts
- stable error codes mapped to failure taxonomy
- idempotent request IDs
- pagination / result limits
- clear `/query`, `/trace`, and `/eval` semantics

### Resilience
- retries with backoff
- provider timeouts
- circuit breaker at model boundary
- graceful degradation when synthesis fails

### Schema Evolution
- versioned schema snapshot for planner context
- `QueryPlan` backward compatibility
- drift tests to ensure `SqlFragmentRegistry` and prompt context stay aligned

### Prompt Discipline
- prompt registry
- versioning + checksums
- golden tests
- prompt/model comparison support

### Evaluation Rigor
- dataset versioning
- replay-based deterministic tests
- regression deltas over time
- latency/cost metrics in eval outputs

### Lightweight Deployment Readiness
- env-based configuration
- health endpoints
- structured logging with correlation IDs

---

## Recommended Milestones

### Milestone A
- real planner integration
- parser
- repair path
- planner traces in memory/API response

### Milestone B
- shared invocation layer
- trace persistence
- failure taxonomy
- trace summaries in API responses

### Milestone C
- benchmark expansion
- replay fixtures
- deterministic CI path
- richer scoring outputs

### Milestone D
- real answer synthesizer
- grounding validation
- synthesis trace persistence

### Milestone E
- compact conversation state
- narrow follow-up support

---

## What Not To Do Yet
- multi-agent orchestration
- vector search
- generalized BI support
- broad conversational memory
- cloud deployment work
- UI expansion
- automatic prompt tuning
- renaming churn during planner work

---

## Immediate Recommendation

The next move is still the same:

**Make the planner real, measurable, and testable before touching anything else.**
