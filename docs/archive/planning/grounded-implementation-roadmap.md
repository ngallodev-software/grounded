> Historical implementation-planning artifact.
>
> This roadmap was drafted before validating the current `Grounded` repo. Most of the major architecture items it proposes are already present in `Grounded.Api` and `Grounded.Tests`.
>
> Use `docs/grounded-current-status.md` as the canonical reference for what is implemented and what still needs work.

# Grounded Implementation Roadmap
# Status: Reference Only (Not Execution Source)

This document captures the original architectural intent.

Execution is now driven by:
- grounded-overall-execution-step-by-step.md
- grounded-execution-prompt-pack.md
## Goal

Move `Grounded` from a deterministic architecture demo to a credible, evaluation-driven LLM application with:
- real planner model integration
- persistent traces
- meaningful benchmark coverage
- reproducible local testing
- a path toward compact conversation state

The highest-leverage change is to make the planner real first. The planner is where product value and interview credibility sit.

---

## Design Priorities

1. Keep the current contract: model outputs `QueryPlan`, code owns SQL.
2. Preserve deterministic validation and SQL safety as non-negotiable boundaries.
3. Add real model integration without losing reproducibility in tests.
4. Make eval artifacts first-class so prompt/model changes can be measured.
5. Delay conversation memory until the planner and trace stack are real.

---

## Target End State

### Runtime flow

1. User submits question.
2. `PlannerContextBuilder` assembles bounded planner context.
3. `PlannerPromptStore` loads the versioned planner prompt.
4. `PlannerGateway` calls a real provider.
5. Planner output is parsed into `QueryPlan`.
6. Planner validation and optional repair path run.
7. Existing execution pipeline compiles SQL, applies safety checks, and executes.
8. Optional answer synthesizer runs against real data.
9. Full trace is persisted.
10. Eval runner reuses the same production path and records scores, latency, tokens, and failures.

### Key architectural properties

- Planner and synthesizer use shared transport infrastructure.
- Every model call is traceable by prompt version, model, latency, tokens, and raw output.
- CI remains deterministic via replay fixtures or fake providers.
- Benchmarks cover both happy paths and failure classes.

---

## Phase 1: Real Planner Integration

### Why first

The current planner returns the same fixed plan for every question. That means the repo does not yet prove natural-language understanding or real prompt behavior.

### Deliverables

- `PlannerContextBuilder`
- real planner prompt file and versioning
- provider-backed `ILlmPlannerGateway`
- structured planner response parser
- one repair attempt for malformed JSON
- planner trace metadata on every request

### Suggested code changes

- Add `PlannerContextBuilder` service.
- Add `PlannerPromptStore` or extend `PromptStore` for planner prompt lookup.
- Replace `DeterministicLlmPlannerGateway` with:
  - `OpenAiCompatiblePlannerGateway`
  - optional `ReplayPlannerGateway` for tests
- Add parser/validator classes:
  - `PlannerResponseParser`
  - `PlannerResponseRepairService`
- Extend models with planner trace data:
  - prompt key
  - prompt checksum
  - model
  - latency
  - tokens in/out
  - raw response
  - parse status
  - repair attempted

### Prompt inputs

The planner prompt should get only:
- user question
- supported question types
- supported metrics
- supported dimensions
- supported filters/operators
- allowed time presets/time grains
- a compact schema summary
- 3-5 fixed few-shot examples

Do not include:
- full raw schema dumps
- SQL examples beyond what is necessary for instruction
- full chat history
- execution results

### Acceptance criteria

- Different user questions produce meaningfully different valid plans.
- Invalid planner outputs fail deterministically.
- One malformed JSON response can be repaired once.
- Planner metadata is exposed in traces and eval results.

### Risks

- Overly large context package causing prompt drift.
- Planner prompt leaking unsupported fields or combinations.
- Repair path masking poor prompt quality if overused.

---

## Phase 2: Shared Model Invocation + Trace Persistence

### Why second

Once the planner is real, the next bottleneck is observability. Without persistent traces, you cannot debug failures or compare prompt/model changes honestly.

### Deliverables

- shared model invocation abstraction
- Postgres-backed trace persistence
- explicit failure taxonomy
- request correlation IDs

### Suggested architecture

Introduce a shared internal layer, for example:
- `IModelInvoker`
- `ModelRequest`
- `ModelResponse`
- `ModelUsage`
- `ModelFailure`

Use it under both planner and synthesizer gateways.

### Trace schema

Add app tables such as:
- `llm_traces`
- `planner_attempts`
- `execution_traces`
- `eval_runs`
- `eval_case_results`

Each trace should capture:
- request ID
- conversation ID if present
- prompt key/version/checksum
- model name
- provider
- started/ended timestamps
- latency ms
- tokens in/out
- raw request payload hash or snapshot
- raw response text
- parsed structured output
- validation errors
- compiled SQL
- row count
- final status

### Failure taxonomy

Use explicit categories:
- `transport_failure`
- `timeout`
- `provider_error`
- `json_parse_failure`
- `planner_validation_failure`
- `unsupported_request`
- `sql_safety_failure`
- `execution_failure`
- `synthesis_failure`

### Acceptance criteria

- Every planner and synthesizer call creates a persisted trace.
- Failures show categorized cause, not just generic exceptions.
- Eval output can summarize failures by category.

---

## Phase 3: Benchmark Expansion + Replay Fixtures

### Why third

A real planner without a serious benchmark suite still leaves the project mostly anecdotal.

### Deliverables

- expanded benchmark corpus
- benchmark categories and tags
- replay fixtures for deterministic CI
- scorecards that include correctness, latency, tokens, and failure class

### Benchmark matrix

Add at least:
- 8 aggregate questions
- 8 grouped breakdown questions
- 8 ranking questions
- 8 time-series questions
- 6 unsupported/ambiguous/adversarial questions
- 6 follow-up questions that should currently be rejected

Each case should capture:
- `caseId`
- category
- question
- expected outcome type
- optional expected plan assertions
- optional expected failure category

### Scoring improvements

Keep the current structural scoring, then add:
- planner validity rate
- execution success rate
- grounding rate
- average latency
- average tokens
- cost estimate
- failure counts by category

### Replay fixtures

Create a test path where:
- real provider responses can be captured once
- sanitized outputs are written to fixture files
- test runs replay those fixtures without network calls

This preserves deterministic CI while keeping the production code real.

### Acceptance criteria

- Benchmarks can run against either real provider or replay mode.
- CI runs entirely in replay mode.
- Prompt changes can be compared against a materially sized benchmark set.

---

## Phase 4: Real Answer Synthesis

### Why fourth

The planner is more valuable than the synthesizer. Once the planner and eval stack are real, the synthesizer can become a genuine second-stage model boundary.

### Deliverables

- provider-backed answer synthesizer gateway
- stricter grounding validation
- answer trace persistence

### Recommended guardrails

- continue passing only question, `QueryPlan`, rows, columns, and metadata
- reject answers that reference values not present in rows
- track synthesis failures separately from execution failures

### Acceptance criteria

- Real model responses are persisted and measurable.
- Grounding validator catches unsupported summaries.
- Fallback behavior remains visible in traces and eval output.

---

## Phase 5: Compact Conversation State

### Why last

Conversation memory only matters after the single-turn planner is real and measurable. Otherwise you are stacking complexity on top of a fake foundation.

### Deliverables

- conversation state table(s)
- deterministic state compressor
- support for one follow-up slice of functionality

### Minimal version

Start with deterministic state only:
- active filters
- prior metric
- prior dimension
- prior time range
- prior question type

Do not start with LLM-generated summaries.

### Acceptance criteria

- `simple_follow_up` can be supported for a narrow set of cases.
- State is loaded from persisted structured data, not full raw history replay.
- Planner context remains bounded.

---

## Post-Stage-5 Operational Hardening Additions

These controls were added after Stage 5. They belong to the later hardening/operability work, not to the earlier planner, synthesis, or compact-memory phases.

### Runtime protections

- server-side rate limiting
- request size limits
- token/request caps
- feature flag to disable live model calls quickly

### Identity and auditability

- API auth between frontend and backend if origins are split
- audit logging for who asked what
- trace IDs / correlation IDs propagated through logs and traces

---

## Recommended Order Of Files/Modules

1. `LlmIntegrationDemo.Api/Services/LlmGateway.cs`
2. `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs`
3. new planner-specific services under `LlmIntegrationDemo.Api/Services/`
4. trace/eval models under `LlmIntegrationDemo.Api/Models/`
5. persistence services + migrations/schema work
6. prompt files under `prompts/`
7. benchmark dataset under `eval/`
8. integration tests under `LlmIntegrationDemo.Tests/`

---

## Immediate Next Sprint

If you want the shortest path to making `Grounded` more valuable, do this next:

1. Replace `DeterministicLlmPlannerGateway` with a real provider-backed planner.
2. Add `PlannerContextBuilder` and planner prompt loading.
3. Persist planner traces with raw output, parse result, tokens, and latency.
4. Expand the benchmark file to at least 15-20 planner cases.
5. Add replay fixtures so tests stay deterministic.

That sequence materially changes the project from "well-designed demo" to "credible LLM backend system."

---

## Non-Goals For Now

- multi-agent orchestration
- vector search
- automatic prompt tuning
- cloud deployment
- UI work
- generalized BI support
- broad conversational memory

Those would add surface area without increasing the strongest signal of the project.
