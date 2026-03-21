> Historical implementation-planning artifact.
>
> This step-by-step plan predates verification of the current `Grounded` codebase. It should not be treated as the active source of truth for implementation status.
>
> Use `docs/grounded-current-status.md` as the canonical reference.

# Grounded — Overall Execution Step by Step

## Goal

Upgrade `Grounded` from a well-architected deterministic demo into a credible, production-shaped LLM analytics backend.

Core contract remains unchanged:

`LLM -> structured QueryPlan -> validator -> SQL compiler -> safety guard -> Postgres execution`

The model must not generate executable SQL.

---

## Execution Order

1. Real planner integration
2. Shared invocation + trace persistence + failure taxonomy
3. Benchmark expansion + replay mode
4. Real answer synthesis
5. Compact conversation state
6. Production hardening pass

---

# Phase 1 — Real Planner Integration

## Outcome
The system performs real NL -> QueryPlan planning with a provider-backed model.

## Step 1. Add planner prompt file
Create:
- `prompts/planner/v1.md`

Include:
- allowed question types
- allowed metrics
- allowed dimensions
- allowed filters/operators
- allowed time presets/grains
- strict JSON-only output instruction
- 3-5 few-shot examples

## Step 2. Extend prompt loading for planner prompts
Update prompt-loading so planner prompts use:
- versioning
- checksums
- consistent retrieval behavior

## Step 3. Add planner models
Create:
- `LlmIntegrationDemo.Api/Models/PlannerModels.cs`

Include:
- bounded planner input payload
- raw planner response
- parsed planner result
- planner trace metadata

## Step 4. Build deterministic planner context
Create:
- `LlmIntegrationDemo.Api/Services/PlannerContextBuilder.cs`

It should derive from the SQL registry where possible:
- supported metrics
- supported dimensions
- supported filters
- supported time options
- compact schema summary

Do not hand-maintain duplicate schema definitions if avoidable.

## Step 5. Implement provider-backed planner gateway
Create:
- `LlmIntegrationDemo.Api/Services/OpenAiCompatiblePlannerGateway.cs`

Use env-configured:
- base URL
- API key
- model
- timeout
- retries

Keep the abstraction light.

## Step 6. Implement strict planner parser
Create:
- `LlmIntegrationDemo.Api/Services/PlannerResponseParser.cs`

Requirements:
- planner returns one JSON object
- parse into `QueryPlan`
- capture parse failure details cleanly

## Step 7. Implement one-shot repair path
Create:
- `LlmIntegrationDemo.Api/Services/PlannerResponseRepairService.cs`

Behavior:
- exactly one repair attempt
- persist original output
- persist repaired output
- mark whether repair succeeded

## Step 8. Wire planner into orchestration service
Update:
- `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs`

Replace deterministic planner path with:
- real planner call
- parse
- optional repair
- validation
- existing compile/execute pipeline

## Step 9. Surface planner trace metadata
Add to in-memory response trace:
- prompt key/version/checksum
- model
- provider
- latency
- tokens in/out
- parse status
- repair attempted
- failure category

## Step 10. Add Phase 1 tests
Add integration coverage for:
- valid aggregate question
- valid grouped question
- valid ranking question
- malformed JSON with repair success
- malformed JSON with repair failure
- unsupported question
- planner timeout

## Phase 1 exit criteria
- different questions produce different valid plans
- planner metadata is visible
- execution path still works
- failures are categorized deterministically

---

# Phase 2 — Shared Invocation, Traces, and Failure Taxonomy

## Outcome
Every model interaction is observable, persisted, and categorized.

## Step 11. Add shared model invocation layer
Create internal abstractions:
- `IModelInvoker`
- `ModelRequest`
- `ModelResponse`
- `ModelUsage`
- `ModelFailure`

Use this under planner and synthesizer.

## Step 12. Define failure taxonomy
Use:
- `transport_failure`
- `timeout`
- `provider_error`
- `json_parse_failure`
- `planner_validation_failure`
- `unsupported_request`
- `sql_safety_failure`
- `execution_failure`
- `synthesis_failure`

## Step 13. Add trace models and repositories
Create:
- `TraceModels.cs`
- `TraceRepository.cs`
- `EvalRepository.cs`

## Step 14. Add persistence schema
Persist at minimum:
- request ID
- trace ID
- prompt key/version/checksum
- provider/model
- timestamps
- latency
- tokens
- raw response
- parsed `QueryPlan`
- validation errors
- compiled SQL
- row count
- final status
- failure category

## Step 15. Add correlation IDs and stable API error codes
Map error codes directly to failure taxonomy where appropriate.

## Step 16. Return trace summaries in API responses
Return:
- trace ID
- planner status
- synthesis status
- failure category

Do not dump raw provider output by default.

## Step 17. Add Phase 2 tests
Verify:
- failed parse persists trace
- timeout persists categorized failure
- eval run persists correctly
- synthesis failure surfaces in response and trace

## Phase 2 exit criteria
- every model call is traceable
- failures are categorized
- API responses expose usable execution status

---

# Phase 3 — Benchmark Expansion and Replay Mode

## Outcome
Prompt/model iteration becomes measurable and reproducible.

## Step 18. Expand benchmark schema
Each case should support:
- `caseId`
- category
- question
- expected outcome type
- expected failure category
- expected plan assertions
- tags

## Step 19. Expand benchmark dataset
Build at least 30 cases across:
- aggregate
- grouped
- ranking
- time-series
- unsupported
- ambiguous
- adversarial
- rejected follow-up

## Step 20. Add replay gateways
Create:
- `ReplayPlannerGateway.cs`
- `ReplayAnswerGateway.cs`

## Step 21. Add replay fixtures
Create fixture directories:
- `eval/fixtures/planner/`
- `eval/fixtures/synthesizer/`

Use sanitized captured provider outputs.

## Step 22. Upgrade eval scoring
Report:
- planner validity rate
- execution success rate
- grounding rate
- failure counts by category
- average latency
- average tokens
- cost estimate

## Step 23. Add deterministic eval tests
Ensure:
- no network required in CI
- replay outputs are stable
- regression comparison works with larger corpus

## Phase 3 exit criteria
- CI runs in replay mode
- prompt changes can be benchmarked
- failures are visible by category and frequency

---

# Phase 4 — Real Answer Synthesis

## Outcome
The second model boundary becomes real and measurable.

## Step 24. Implement provider-backed answer gateway
Create:
- `OpenAiCompatibleAnswerGateway.cs`

Reuse shared model invocation layer.

## Step 25. Strengthen answer grounding validation
Create or enhance:
- `AnswerGroundingValidator.cs`

Check:
- summary is non-empty
- claims are row-supported
- unsupported dimensions are rejected
- unsupported numeric claims are rejected where feasible

## Step 26. Preserve fallback behavior
If synthesis fails:
- return fallback answer
- mark trace status clearly
- classify as `synthesis_failure`

## Step 27. Persist synthesis traces
Planner and synthesizer should have equivalent trace quality.

## Step 28. Add Phase 4 tests
Verify:
- real/replay synthesis path returns structured answer
- unsupported summary content is rejected
- synthesis failure returns fallback plus visible trace state

## Phase 4 exit criteria
- synthesizer is real in production mode
- grounding failures are visible
- tests remain deterministic in replay mode

---

# Phase 5 — Compact Conversation State

## Outcome
A narrow, deterministic follow-up capability exists without broad chat memory.

## Step 29. Add conversation state models and service
Create:
- `ConversationModels.cs`
- `ConversationStateService.cs`

## Step 30. Persist structured prior-turn state
Store only:
- previous metric
- previous dimension
- previous filters
- previous time range
- previous question type

## Step 31. Support narrow follow-up patterns
Examples:
- “same thing by category”
- “what about last quarter”
- “now only for electronics”

## Step 32. Keep context deterministic and bounded
Do not:
- replay full chat history
- use LLM-generated summaries yet

## Step 33. Add Phase 5 tests
Verify:
- supported follow-up modifies prior state correctly
- unsupported follow-up rejects deterministically
- planner context remains bounded

## Phase 5 exit criteria
- narrow follow-ups work
- state is explainable
- memory remains structured

---

# Phase 6 — Production Hardening Pass

## Outcome
The project feels operated, not just implemented.

## Step 34. Add observability and SLOs
Track:
- success rate
- p95 latency
- cost per request
- planner validity rate
- execution success rate
- failure counts by category

## Step 35. Add cost controls
Implement:
- token budgets
- token/request caps
- hard caps
- server-side rate limiting
- request size limits
- feature flag to disable live model calls quickly
- optional planner-output caching for identical inputs
- optional model tiering later if needed

## Step 36. Add security and governance protections
Implement:
- prompt injection defenses
- PII redaction in traces
- audit logging for who asked what
- endpoint authorization if needed
- API auth between frontend and backend if origins are split

## Step 37. Add schema-drift protections
Implement:
- versioned planner schema snapshot
- drift tests between prompt context and `SqlFragmentRegistry`
- backward compatibility checks for `QueryPlan`

## Step 38. Improve API and developer experience
Add:
- stable error code documentation
- curl examples
- endpoint semantics for `/query`, `/trace/{id}`, `/eval/{runId}`
- health endpoint

## Step 39. Add structured logging
Include:
- trace IDs / correlation IDs
- request IDs
- provider/model metadata
- categorized failures

## Phase 6 exit criteria
- system has measurable operational behavior
- failures are diagnosable
- API surface is understandable to a reviewer

---

# Recommended Immediate Sprint

Do only these next:

1. Add `prompts/planner/v1.md`
2. Extend prompt loading for planner
3. Implement `PlannerContextBuilder`
4. Implement `OpenAiCompatiblePlannerGateway`
5. Implement `PlannerResponseParser`
6. Implement `PlannerResponseRepairService`
7. Extend trace models for planner metadata
8. Add planner integration tests
9. Replace deterministic planner in `AnalyticsQueryPlanService`

That is the shortest path from architecture demo to credible LLM backend system.

---

# Non-Goals During Early Execution

Do not split focus across these yet:
- answer synthesis
- memory/follow-ups
- UI work
- cloud deployment
- vector search
- multi-agent orchestration
- broad BI expansion
- rename churn

---

# Final Rule

Make the planner real, measurable, and testable before touching anything else.
