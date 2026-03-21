> Historical implementation-planning artifact.
>
> This prompt pack assumes a partially implemented `Grounded` codebase. The current repo already contains most of the planner, replay, trace, and conversation-state components it references.
>
> Use `docs/grounded-current-status.md` as the canonical reference for current state.

# Grounded — Execution Prompt Pack

## How to Use This Pack

Use these prompts in order. Each prompt is designed to complete one bounded slice of the execution plan without causing requirement drift.

Global rule for every prompt in this pack:

- Preserve the core contract: `LLM -> structured QueryPlan -> validator -> SQL compiler -> safety guard -> Postgres execution`
- The model must **not** generate executable SQL
- Do not introduce vector search, multi-agent workflows, generalized chat memory, UI expansion, or cloud deployment work unless explicitly requested
- Prefer incremental edits over rewrites
- Do not change public behavior outside the scoped task
- If a blocker is found, complete all non-blocked work in scope and then report the blocker clearly
- Keep code production-shaped, testable, and minimal
- Preserve deterministic replay-friendly testing
- Prefer existing patterns and naming already present in the repo over inventing new frameworks

---

# Prompt 0 — Master Executor Prompt

Use this when you want the agent to execute the whole plan phase by phase.

```text
You are implementing the `Grounded` execution plan in an existing .NET codebase.

Project goal:
Upgrade `Grounded` from a deterministic architecture demo into a credible, production-shaped LLM analytics backend.

Core contract:
`LLM -> structured QueryPlan -> validator -> SQL compiler -> safety guard -> Postgres execution`

Non-negotiable rules:
- The model must not generate executable SQL
- Preserve deterministic validation and SQL safety boundaries
- Do not add vector search, multi-agent orchestration, broad chat memory, generalized BI support, UI work, or cloud deployment work
- Do not do speculative cleanup or renaming unless directly required for the scoped task
- Prefer small, reviewable commits
- Preserve or improve testability
- Keep CI-compatible deterministic test paths via replay fixtures or fake providers
- Use the SQL registry / allow-lists as the source of truth where possible
- Do not duplicate schema definitions by hand unless unavoidable
- If you discover a design conflict, choose the option that preserves safety, observability, and benchmarkability

Execution rules:
1. Work in the planned order:
   - Phase 1: real planner integration
   - Phase 2: shared invocation + traces + failure taxonomy
   - Phase 3: benchmark expansion + replay mode
   - Phase 4: real answer synthesis
   - Phase 5: compact conversation state
2. Before editing, inspect the current code and list the exact files likely to change
3. Implement only the current phase unless a tiny prerequisite is required
4. Add or update tests before declaring the phase complete
5. At the end, return:
   - summary of what changed
   - files changed
   - tests added/updated
   - unresolved risks
   - whether the phase exit criteria are met

Delegation guidance:
- Use GPT-5.4 mini for bounded, file-scoped, low-ambiguity tasks:
  - DTO/model creation
  - parser implementation
  - test fixture expansion
  - replay fixture plumbing
  - repository boilerplate
  - API error-code mapping
  - docs/examples
- Keep GPT-5.4 level for:
  - architecture-affecting changes
  - prompt design
  - orchestration boundaries
  - failure taxonomy design
  - persistence schema design
  - grounding and safety decisions
  - planner context design
  - tradeoff decisions

Start with Phase 1 only.
First output:
- proposed file change list
- implementation order
- key risks
Then begin implementation.
```

---

# Prompt 1 — Phase 1: Real Planner Integration

Use this for the highest-value change.

```text
Implement Phase 1 of the `Grounded` execution plan: real planner integration.

Objective:
Replace the fake planner with a real provider-backed planner while preserving deterministic validation and execution.

Required deliverables:
- `PlannerContextBuilder`
- planner prompt file + checksum/versioning
- provider-backed planner gateway using an OpenAI-compatible endpoint
- strict JSON planner response parser
- one repair attempt for malformed JSON
- planner metadata in traces/responses
- integration tests for planner success/failure paths

Hard guardrails:
- The planner outputs a structured `QueryPlan`, never SQL
- Do not weaken validator, compiler, or SQL safety guard behavior
- Do not add synthesis changes yet
- Do not add conversation memory
- Do not over-abstract provider support; support one clean OpenAI-compatible path first
- Keep prompt inputs bounded and derived from existing schema/registry definitions where possible
- Do not hand-copy large schema definitions into prompt builders if they can be derived from existing source-of-truth code
- Repair is exactly one attempt, not an open-ended loop
- Persist or surface enough planner metadata for later debugging

Expected new/changed areas:
- planner prompt file
- prompt loading support for planner
- planner models
- planner context builder
- provider-backed planner gateway
- planner response parser
- planner response repair service
- orchestration service wiring
- planner integration tests

What to inspect before editing:
- current planner gateway path
- current prompt loading system
- QueryPlan model and validator
- SQL registry / allow-list source of truth
- existing trace/response models
- current tests around analytics execution

Required tests:
- valid aggregate question -> valid plan
- valid grouped or ranking question -> valid plan
- malformed JSON -> repair succeeds
- malformed JSON -> repair fails
- unsupported question -> deterministic failure category
- planner timeout -> deterministic failure category

At the end return:
- files changed
- planner call flow
- trace fields added
- tests added/updated
- known limitations still remaining in Phase 1
```

---

# Prompt 2 — GPT-5.4 Mini Subagent: Planner Models and DTOs

Use this for a narrow subtask.

```text
Implement only the planner-related models/DTOs for `Grounded`.

Scope:
Create or update only the types needed for Phase 1 planner integration.

Include models for:
- bounded planner input payload
- raw planner response
- parsed planner result
- planner trace metadata
- failure category enum or constants if needed by this slice

Guardrails:
- Do not edit orchestration logic
- Do not edit provider transport code
- Do not change existing public API contracts unless necessary
- Match existing repo naming/style patterns
- Keep models minimal and strongly typed
- Include fields for prompt key/checksum, model, timestamps, latency, token usage, parse success, repair attempted, repair success, and failure category if that fits existing design

Return:
- files created/updated
- why each field exists
- any assumptions that need confirmation by the parent task
```

---

# Prompt 3 — GPT-5.4 Mini Subagent: Planner Context Builder

```text
Implement only `PlannerContextBuilder` for `Grounded`.

Objective:
Build a bounded planner context package for the planner prompt.

Requirements:
- derive supported metrics, dimensions, filters, operators, and time options from existing source-of-truth code where possible
- include a compact schema summary
- include only what is needed for accurate planning
- keep the payload small and deterministic
- do not include execution results
- do not include raw full chat history
- do not hand-maintain large duplicated schema definitions

Guardrails:
- no provider code
- no parser code
- no orchestration changes
- no tests beyond builder-specific tests unless required
- prefer pure functions or easily testable service methods

Return:
- files changed
- resulting planner context structure
- anything that could drift and how you minimized it
```

---

# Prompt 4 — GPT-5.4 Mini Subagent: Planner Parser and Repair

```text
Implement only the planner response parsing and one-shot repair path for `Grounded`.

Scope:
- `PlannerResponseParser`
- `PlannerResponseRepairService`
- related tests

Requirements:
- planner output must parse into exactly one `QueryPlan` JSON object
- return structured parser failure metadata
- allow exactly one repair attempt for malformed but recoverable JSON
- persist or expose both original and repaired outputs in metadata if the surrounding design supports it
- do not weaken validation; parser success is not the same as QueryPlan validity

Guardrails:
- do not modify provider gateway behavior beyond what is necessary to support repair input/output
- do not introduce multiple repair loops
- do not change SQL compilation/execution logic
- keep parsing behavior explicit and testable

Required tests:
- valid JSON parses
- malformed JSON repair succeeds
- malformed JSON repair fails cleanly
- extra unexpected structure is rejected if required by the current contract

Return:
- files changed
- parsing rules
- repair rules
- edge cases intentionally rejected
```

---

# Prompt 5 — Phase 2: Shared Invocation, Traces, and Failure Taxonomy

```text
Implement Phase 2 of the `Grounded` plan: shared model invocation, trace persistence, and failure taxonomy.

Objective:
Make every planner and synthesizer outcome observable, categorized, and persistable.

Required deliverables:
- shared model invocation abstraction used under planner and synthesizer
- explicit failure taxonomy
- trace persistence
- eval persistence
- stable error-code mapping
- trace summaries in API responses

Guardrails:
- do not change the planner contract from Phase 1
- do not add conversation state
- do not broaden the public API beyond what is needed for trace visibility
- do not dump raw provider responses to normal API clients by default
- keep categories explicit and stable
- preserve deterministic tests

Failure taxonomy:
- `transport_failure`
- `timeout`
- `provider_error`
- `json_parse_failure`
- `planner_validation_failure`
- `unsupported_request`
- `sql_safety_failure`
- `execution_failure`
- `synthesis_failure`

Implementation rules:
- planner and synthesizer should share transport/invocation plumbing where practical
- persist prompt key/version/checksum, provider/model, timestamps, latency, tokens, raw response, parsed output, validation errors, compiled SQL, row count, final status, and failure category where applicable
- add correlation IDs
- map API-visible error semantics to stable codes

Required tests:
- failed parse persists categorized trace
- timeout persists categorized trace
- eval run persistence works
- synthesis failure surfaces in response and trace

At the end return:
- schema/storage changes
- failure taxonomy implementation details
- response contract changes
- remaining risks
```

---

# Prompt 6 — GPT-5.4 Mini Subagent: Trace Repository and Persistence Models

```text
Implement only the trace/eval persistence slice for `Grounded`.

Scope:
- trace models
- eval models if needed
- repository classes
- migration/schema artifacts if applicable
- repository tests if present in repo style

Guardrails:
- do not redesign orchestration
- do not redesign failure taxonomy beyond using already-defined categories
- do not add UI/reporting work
- match existing persistence patterns
- keep schema practical and auditable

Persist at minimum:
- request/trace IDs
- prompt key/version/checksum
- provider/model
- timestamps
- latency
- token usage
- raw response or snapshot
- parsed plan/output
- validation errors
- compiled SQL
- row count
- final status
- failure category

Return:
- files changed
- tables/entities created
- indexes or lookup choices
- any migration caveats
```

---

# Prompt 7 — Phase 3: Benchmark Expansion and Replay Mode

```text
Implement Phase 3 of `Grounded`: benchmark expansion and replay mode.

Objective:
Make prompt/model iteration benchmark-driven and deterministic in CI.

Required deliverables:
- expanded benchmark schema
- materially larger benchmark corpus
- replay planner gateway
- replay answer gateway
- sanitized replay fixtures
- richer eval scoring outputs

Guardrails:
- do not add new product features
- do not change the planner contract
- do not rely on live provider calls in CI
- benchmark categories must include both happy-path and failure-path coverage
- keep replay fixtures deterministic and reviewable

Benchmark target:
At least 30 cases across:
- aggregate
- grouped
- ranking
- time-series
- unsupported
- ambiguous
- adversarial/schema-escape
- rejected follow-up

Scoring output should include:
- planner validity rate
- execution success rate
- grounding rate
- failure counts by category
- average latency
- average token usage
- cost estimate if available

Required tests:
- replay mode works without network access
- loader supports expanded schema
- regression comparison works with the larger corpus

At the end return:
- benchmark coverage summary
- replay mechanism summary
- scoring/reporting changes
- known blind spots still remaining
```

---

# Prompt 8 — GPT-5.4 Mini Subagent: Benchmark Corpus Authoring

```text
Create or expand the benchmark dataset for `Grounded`.

Scope:
Benchmark data only, plus minimal schema/loader updates if required.

Requirements:
- create at least 30 benchmark cases
- include categories: aggregate, grouped, ranking, time-series, unsupported, ambiguous, adversarial, rejected follow-up
- each case should include:
  - caseId
  - category
  - question
  - expected outcome type
  - optional expected failure category
  - optional expected plan assertions
  - tags if useful
- keep questions realistic for the supported domain
- unsupported/adversarial cases should be plausible and useful, not silly

Guardrails:
- do not modify planner/execution code unless loader changes are strictly required
- do not invent unsupported schema elements as if they are valid
- design cases to catch regressions, not to flatter the system
- keep fixture format deterministic and reviewable

Return:
- benchmark counts by category
- files changed
- 5 highest-value cases for catching regressions
```

---

# Prompt 9 — Phase 4: Real Answer Synthesis

```text
Implement Phase 4 of `Grounded`: real answer synthesis.

Objective:
Replace deterministic answer synthesis with a real provider-backed grounded answer step.

Required deliverables:
- provider-backed answer gateway
- stricter grounding validation
- synthesis trace persistence
- visible fallback behavior on synthesis failure

Guardrails:
- do not change planner behavior
- continue passing only bounded synthesis inputs:
  - user question
  - QueryPlan
  - rows
  - columns
  - execution metadata
  - prompt/version metadata as appropriate
- do not allow the answer step to invent unsupported dimensions or numeric claims
- fallback behavior must be visible and categorized as `synthesis_failure`
- preserve replay-based deterministic tests

Required tests:
- real/replay synthesis path returns structured answer
- unsupported answer content is rejected
- synthesis failure returns fallback plus visible trace status

At the end return:
- files changed
- grounding rules added
- fallback behavior details
- remaining gaps in synthesis robustness
```

---

# Prompt 10 — GPT-5.4 Mini Subagent: Answer Grounding Validator

```text
Implement only the grounding-validation slice for the answer synthesizer in `Grounded`.

Scope:
- create or update `AnswerGroundingValidator`
- add focused tests

Validation goals:
- summary must be non-empty
- reject unsupported dimensions or entities not present in the result context
- reject unsupported numeric claims where feasible
- distinguish grounding failure from transport/provider failure

Guardrails:
- do not modify planner or SQL logic
- do not broaden the answer schema unnecessarily
- keep checks explicit, deterministic, and testable
- avoid brittle exact-string logic when a structural check is possible

Return:
- files changed
- grounding checks implemented
- known limitations of the validator
```

---

# Prompt 11 — Phase 5: Compact Conversation State

```text
Implement Phase 5 of `Grounded`: compact conversation state.

Objective:
Add narrow follow-up support without turning the system into a general chat-memory product.

Required deliverables:
- deterministic conversation state persistence
- support for a narrow follow-up slice
- bounded planner context for follow-ups

Store only:
- previous metric
- previous dimension
- previous filters
- previous time range
- previous question type

Guardrails:
- do not replay full raw chat history
- do not use LLM-generated conversation summaries
- keep follow-up support intentionally narrow
- unsupported follow-ups must reject deterministically
- do not introduce broad conversational memory

Required tests:
- supported follow-up modifies prior state correctly
- unsupported follow-up rejects deterministically
- planner context remains bounded in follow-up mode

At the end return:
- files changed
- supported follow-up patterns
- explicit non-supported follow-up patterns
- risks of future expansion
```

---

# Prompt 12 — GPT-5.4 Mini Subagent: API Error Codes, Examples, and Docs

```text
Implement only the API error-code/docs/examples slice for `Grounded`.

Scope:
- stable API error codes mapped to the implemented failure taxonomy
- minimal docs/examples for `/query`, `/trace/{id}`, `/eval/{runId}`
- curl examples
- small contract examples
- health endpoint docs if present

Guardrails:
- do not redesign runtime behavior
- do not invent endpoints that do not exist
- keep docs concise, concrete, and copy-pasteable
- use real failure categories and real request/response shapes

Return:
- files changed
- documented error codes
- example requests/responses added
```

---

# Prompt 13 — Production Hardening Pass

```text
Implement a focused production-hardening pass for `Grounded` after Phases 1-5 are complete.

Objective:
Make the system feel operated, not merely implemented.

Scope:
- observability and SLO instrumentation
- cost controls and request budgets
- structured logging with correlation IDs
- server-side rate limiting and request size limits
- API auth if frontend and backend are split across origins
- health endpoints
- prompt-injection and governance protections already implied by the architecture
- schema-drift protections between planner context and SQL registry

Guardrails:
- do not add major new product capabilities
- do not add cloud-specific deployment complexity unless already present
- do not over-engineer dashboards if the project currently supports only logs/metrics export points
- prefer practical instrumentation over broad platform work

Minimum outcomes:
- measurable success rate, p95 latency, and cost/request
- token/request caps or budget enforcement
- server-side rate limiting on externally exposed routes
- explicit request body size limits for planner/execution endpoints
- planner-output caching for identical inputs if straightforward
- PII redaction in traces if sensitive fields can appear
- audit logging for who asked what, including request identity when available
- trace IDs / correlation IDs propagated through request logs and traces
- a feature flag or kill switch to disable live model calls quickly without redeploying
- API authentication between frontend and backend if they are split across origins
- drift tests between planner context and SQL registry

At the end return:
- hardening changes made
- metrics added
- limits/controls added
- remaining production-readiness gaps
```

---

# Prompt 14 — Final Review / Exit Criteria Prompt

Use this once all implementation work is done.

```text
Review the current `Grounded` codebase against the execution plan.

Your task:
- verify which phase exit criteria are met
- identify any missing or partially implemented items
- identify any requirement drift
- identify places where the implementation weakened the original contract or safety posture
- identify test gaps
- identify the top 5 improvements still worth making for interview credibility

Evaluate against these phases:
1. real planner integration
2. shared invocation + traces + failure taxonomy
3. benchmark expansion + replay mode
4. real answer synthesis
5. compact conversation state
6. production hardening

Guardrails:
- be strict
- do not assume intent counts as implementation
- distinguish direct evidence from inference
- do not recommend new scope unless it materially improves credibility for the target role

Required output:
- phase-by-phase pass/partial/fail
- missing items by severity
- safety/regression risks
- top 5 highest-ROI follow-up tasks
- interview-readiness assessment
```

---

# Recommended Delegation Map

## Keep with GPT-5.4
- master phase execution
- planner prompt design
- planner context design
- failure taxonomy and API semantics
- orchestration changes
- persistence schema tradeoffs
- grounding policy decisions
- final review

## Safe to delegate to GPT-5.4 mini
- models/DTOs
- parser/repair implementation
- repository boilerplate
- benchmark authoring
- replay gateway plumbing
- error-code docs/examples
- focused validators
- test case expansion
- small file-scoped refactors

---

# Final Guardrail

Do not let the work drift away from the single most important credibility upgrade:

**Make the planner real, measurable, and benchmarked before doing anything else.**
