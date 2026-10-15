> Historical implementation-planning artifact.
>
> This document predates verification of the current `Grounded` codebase. Most of the major items described here are already implemented under `Grounded.Api` and `Grounded.Tests`.
>
> Use `docs/grounded-current-status.md` as the canonical reference for current state.

# Grounded Execution Plan
##for original reference only, do not use.  deprecated


# Purpose

This document translates the roadmap into concrete implementation work for `Grounded`.

It is optimized for execution:
- what to build first
- which files to add or change
- what each phase should prove
- what tests to add before moving on

The plan assumes one core principle remains unchanged:

`LLM -> structured QueryPlan -> validator -> SQL compiler -> safety guard -> Postgres execution`

The model should not generate executable SQL.

---

## Recommended Delivery Order

1. Real planner integration
2. Planner traces + failure taxonomy
3. Benchmark expansion + replay mode
4. Real answer synthesis integration
5. Compact conversation state

This order keeps the highest-value risk on the critical path and avoids adding memory or UI complexity before the planner is real.

---

## Phase 1: Real Planner Integration

## Objective

Replace the fake planner path with a real provider-backed planner while preserving deterministic validation and execution.

## Current gap

Today `DeterministicLlmPlannerGateway` returns the same plan for every question. That means the system does not yet demonstrate real NL-to-plan behavior.

## Files To Change

### Existing files
- `LlmIntegrationDemo.Api/Program.cs`
- `LlmIntegrationDemo.Api/Services/LlmGateway.cs`
- `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs`
- `LlmIntegrationDemo.Api/Models/Contracts.cs`
- `LlmIntegrationDemo.Api/Models/Answering.cs`
- `LlmIntegrationDemo.Api/Models/EvalModels.cs`

### New files
- `LlmIntegrationDemo.Api/Services/PlannerContextBuilder.cs`
- `LlmIntegrationDemo.Api/Services/PlannerPromptRenderer.cs`
- `LlmIntegrationDemo.Api/Services/PlannerResponseParser.cs`
- `LlmIntegrationDemo.Api/Services/PlannerResponseRepairService.cs`
- `LlmIntegrationDemo.Api/Services/OpenAiCompatiblePlannerGateway.cs`
- `LlmIntegrationDemo.Api/Models/PlannerModels.cs`
- `prompts/planner/v1.md`

## Implementation Tasks

### Task 1. Add planner request/response models

Add models for:
- bounded planner input payload
- raw planner response
- parsed planner result
- planner trace metadata

Fields to include:
- `PromptKey`
- `PromptChecksum`
- `Model`
- `RequestedAt`
- `RespondedAt`
- `LatencyMs`
- `TokensIn`
- `TokensOut`
- `RawContent`
- `ParseSucceeded`
- `RepairAttempted`
- `RepairSucceeded`
- `FailureCategory`

### Task 2. Build deterministic planner context

Implement `PlannerContextBuilder` to assemble:
- supported question types
- supported metrics
- supported dimensions
- supported filters/operators
- supported time presets/time grains
- compact schema summary
- fixed examples

This builder should derive allowed fields from `SqlFragmentRegistry` where possible to avoid drift.

### Task 3. Add planner prompt loading

Add `prompts/planner/v1.md` and load it through the same checksum/version mechanism already used for synthesis prompts.

### Task 4. Add real provider-backed planner gateway

Implement a real gateway using an OpenAI-compatible chat/completions endpoint.

Keep the design provider-agnostic enough that a second provider can be added later, but do not over-abstract early.

Minimum configuration:
- base URL
- API key
- model ID
- timeout seconds
- max retries

### Task 5. Parse strict JSON planner output

Planner must return a single `QueryPlan` JSON object.

Parser behavior:
- parse raw JSON
- map to `QueryPlan`
- reject extra unsupported structure if necessary
- return structured parser failure metadata

### Task 6. Add one repair attempt

If the planner returns malformed-but-recoverable JSON:
- make exactly one repair attempt
- use a dedicated repair instruction
- persist both original and repaired outputs in trace metadata

### Task 7. Wire planner into service response trace

Extend `AnalyticsQueryPlanService.ExecuteFromQuestionAsync` so planner metadata is captured in the final trace returned to clients and eval runs.

## Tests Required

Add integration coverage for:
- valid aggregate question -> valid plan
- valid ranking question -> valid plan
- malformed JSON -> one repair attempt succeeds
- malformed JSON -> repair still fails
- unsupported question -> returns deterministic planner validation failure
- planner timeout -> deterministic error category

## Phase Exit Criteria

- Real planner call path exists.
- Different questions produce different plans.
- Planner metadata is visible in traces.
- Existing execution path remains intact.

---

## Phase 2: Planner Traces And Failure Taxonomy

## Objective

Make every planner and synthesizer outcome observable and analyzable.

## Files To Change

### Existing files
- `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs`
- `LlmIntegrationDemo.Api/Services/EvalRunner.cs`
- `LlmIntegrationDemo.Api/Services/RegressionComparer.cs`
- `LlmIntegrationDemo.Api/Models/EvalModels.cs`
- `LlmIntegrationDemo.Api/Models/Contracts.cs`

### New files
- `LlmIntegrationDemo.Api/Services/TraceRepository.cs`
- `LlmIntegrationDemo.Api/Services/EvalRepository.cs`
- `LlmIntegrationDemo.Api/Models/TraceModels.cs`
- `database/` or `sql/` migration artifacts if you add schema scripts

## Implementation Tasks

### Task 1. Define failure taxonomy

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

### Task 2. Add trace persistence

Persist at minimum:
- request ID
- prompt key/version/checksum
- model
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

### Task 3. Persist eval runs

Store:
- run metadata
- benchmark case results
- prompt/model versions
- score
- latency/token aggregates
- regression deltas

### Task 4. Surface trace summaries in API responses

Do not dump full raw model output to normal clients by default, but do include:
- planner status
- synthesis status
- failure category
- trace ID

## Tests Required

- failed planner parse persists trace
- timeout persists categorized failure
- eval run persists run + case results
- synthesis failure surfaces in response and trace

## Phase Exit Criteria

- Every model call is traceable.
- Every failed request has a categorized failure.
- Eval output can summarize failures by category.

---

## Phase 3: Benchmark Expansion And Replay Mode

## Objective

Make the evaluation harness strong enough to support real prompt/model iteration.

## Files To Change

### Existing files
- `eval/benchmark_cases.jsonl`
- `LlmIntegrationDemo.Api/Services/BenchmarkLoader.cs`
- `LlmIntegrationDemo.Api/Services/EvalRunner.cs`
- `LlmIntegrationDemo.Api/Services/ScoringService.cs`
- `LlmIntegrationDemo.Tests/Phase4IntegrationTests.cs`

### New files
- `eval/fixtures/planner/`
- `eval/fixtures/synthesizer/`
- `LlmIntegrationDemo.Api/Services/ReplayPlannerGateway.cs`
- `LlmIntegrationDemo.Api/Services/ReplayAnswerGateway.cs`
- `LlmIntegrationDemo.Tests/EvalIntegrationTests.cs`

## Implementation Tasks

### Task 1. Expand benchmark schema

Each case should support:
- `CaseId`
- `Category`
- `Question`
- `ExpectedOutcomeType`
- `ExpectedFailureCategory`
- optional `ExpectedPlanAssertions`
- optional tags

### Task 2. Expand benchmark corpus

Minimum target:
- 5 aggregate
- 5 grouped
- 5 ranking
- 5 time-series
- 5 unsupported or ambiguous
- 5 adversarial or schema-escape attempts

Recommended target:
- 30-40 total cases

### Task 3. Add replay mode

Replay mode should:
- load captured model outputs from disk
- return stable token/latency metadata
- be used in automated tests

### Task 4. Upgrade eval scoring output

Keep the current correctness scoring and add reporting for:
- planner validity rate
- execution success rate
- grounding rate
- failure counts by category
- average latency
- average token usage

## Tests Required

- replay mode returns deterministic planner output
- eval suite runs without network access
- benchmark loader supports expanded case schema
- regression comparison still works with larger corpus

## Phase Exit Criteria

- Eval suite has enough breadth to catch prompt regressions.
- CI can run deterministically in replay mode.
- Prompt changes can be compared against meaningful numbers.

---

## Phase 4: Real Answer Synthesis

## Objective

Replace the deterministic synthesizer with a real provider-backed answer generation step while preserving grounding guarantees.

## Files To Change

### Existing files
- `LlmIntegrationDemo.Api/Services/AnswerSynthesizer.cs`
- `LlmIntegrationDemo.Api/Services/LlmGateway.cs`
- `LlmIntegrationDemo.Api/Services/AnswerOutputValidator.cs`
- `prompts/answer-synthesizer/v1.md`

### New files
- `LlmIntegrationDemo.Api/Services/OpenAiCompatibleAnswerGateway.cs`
- `LlmIntegrationDemo.Api/Services/AnswerGroundingValidator.cs`

## Implementation Tasks

### Task 1. Add real provider-backed answer gateway

Use the same shared transport infrastructure added earlier.

### Task 2. Strengthen grounding validation

Validator should check:
- summary is non-empty
- key points exist when rows support them
- no unsupported dimensions
- no numeric claims absent from rows where feasible

### Task 3. Preserve visible fallback behavior

If synthesis fails:
- return fallback answer
- mark trace clearly
- classify as `synthesis_failure`

## Tests Required

- real/replay synthesis path returns structured answer
- unsupported answer content is rejected
- synthesis failure returns fallback plus visible trace status

## Phase Exit Criteria

- Synthesizer is real in production mode.
- Replay mode preserves deterministic tests.
- Grounding failures are observable.

---

## Phase 5: Compact Conversation State

## Objective

Add narrow, deterministic follow-up support without turning the system into a general chat memory product.

## Files To Change

### Existing files
- `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs`
- `LlmIntegrationDemo.Api/Services/QueryPlanValidator.cs`
- `LlmIntegrationDemo.Api/Controllers/AnalyticsController.cs`

### New files
- `LlmIntegrationDemo.Api/Services/ConversationStateService.cs`
- `LlmIntegrationDemo.Api/Models/ConversationModels.cs`
- persistence/schema files for conversation state

## Implementation Tasks

### Task 1. Persist structured prior-turn state

Store only:
- previous metric
- previous dimension
- previous filters
- previous time range
- previous question type

### Task 2. Support a narrow follow-up slice

Examples:
- "now only for electronics"
- "what about last quarter"
- "same thing by category"

### Task 3. Keep context deterministic

Do not pass full raw chat history.
Do not use LLM-generated summaries yet.

## Tests Required

- follow-up modifies prior filters correctly
- unsupported follow-up still rejects deterministically
- planner context remains bounded

## Phase Exit Criteria

- Narrow follow-up support works.
- Conversation state remains structured and explainable.

---

## Cross-Cutting Refactors

These should happen as needed during phases, not as a separate rewrite.

### 1. Rename project surface from `llm-integration-demo` to `Grounded`

Recommended eventual updates:
- solution name
- project names
- namespaces if desired
- docs titles
- eval artifacts

This can be done after Phase 1 if you want to avoid mixing renaming churn with planner integration.

### 2. Unify prompt handling

Right now prompt handling is synthesizer-centered. Move toward one prompt-loading system for:
- planner prompts
- repair prompts
- synthesizer prompts

### 3. Keep SQL registry as source of truth

Do not duplicate supported metrics/dimensions/filter definitions in prompt builders by hand if they can be derived from `SqlFragmentRegistry`.

---

## Recommended Milestone Breakdown

## Milestone A

Scope:
- real planner integration
- planner parsing
- repair path
- planner traces in memory/API response

Success condition:
- the app demonstrates genuine NL-to-plan behavior

## Milestone B

Scope:
- trace persistence
- failure taxonomy
- expanded eval reporting

Success condition:
- prompt/model failures are explainable and measurable

## Milestone C

Scope:
- larger benchmark suite
- replay fixtures
- deterministic CI path

Success condition:
- prompt iteration becomes benchmark-driven rather than anecdotal

## Milestone D

Scope:
- real answer synthesizer

Success condition:
- both model boundaries are real and observable

## Milestone E

Scope:
- compact conversation state

Success condition:
- narrow follow-up support works without losing determinism

---

## Suggested Next 10 Concrete Tasks

1. Add `prompts/planner/v1.md`.
2. Implement `PlannerContextBuilder`.
3. Extend prompt loading to support planner prompt retrieval.
4. Add real provider config settings to app configuration.
5. Implement `OpenAiCompatiblePlannerGateway`.
6. Implement `PlannerResponseParser`.
7. Implement one-shot `PlannerResponseRepairService`.
8. Extend trace models to include planner metadata.
9. Add integration tests for planner success, repair, and failure paths.
10. Replace the fixed planner in `AnalyticsQueryPlanService.ExecuteFromQuestionAsync`.

---

## Recommendation

If implementation starts immediately, Phase 1 should be the only active focus.

Do not split effort across:
- planner
- synthesizer
- memory
- renaming

at the same time.

The best next move is to make the planner real, measurable, and testable before touching anything else.
