# Grounded Current Status

## Purpose

This document replaces earlier speculative implementation notes with a verified snapshot of the current `Grounded` codebase.

It answers two questions:
- what is already implemented
- what still looks unfinished or worth improving

Verification source:
- direct inspection of the current `Grounded.Api` / `Grounded.Tests` tree
- local test run on `Grounded.slnx`

---

## Already Implemented

### 1. Real planner architecture

These components are present in the current codebase:
- `Grounded.Api/Services/PlannerContextBuilder.cs`
- `Grounded.Api/Services/PlannerPromptRenderer.cs`
- `Grounded.Api/Services/PlannerResponseParser.cs`
- `Grounded.Api/Services/PlannerResponseRepairService.cs`
- `Grounded.Api/Services/OpenAiCompatiblePlannerGateway.cs`
- `Grounded.Api/Models/PlannerModels.cs`
- `prompts/planner/v1.md`

This means the planner path is no longer just a deterministic stub. The repo now has a real planner boundary, JSON parsing, repair flow, and planner prompt infrastructure.

### 2. Shared model invocation layer

These pieces are present:
- `Grounded.Api/Services/ModelInvoker.cs`
  - `IModelInvoker`
  - `ModelInvokerResolver`
  - `DeterministicModelInvoker`
  - `ReplayModelInvoker`
  - `OpenAiCompatibleModelInvoker`

That is the abstraction layer earlier planning docs said still needed to be built.

### 3. Provider-backed gateways

The current repo includes:
- `Grounded.Api/Services/OpenAiCompatiblePlannerGateway.cs`
- `Grounded.Api/Services/OpenAiCompatibleAnswerGateway.cs`

So the codebase has already moved beyond "deterministic only" in production wiring.

### 4. Trace and eval persistence

The current repo includes:
- `Grounded.Api/Services/TraceRepository.cs`
- `Grounded.Api/Services/EvalRepository.cs`
- `Grounded.Api/Models/TraceModels.cs`
- `Grounded.Api/Models/EvalModels.cs`

This means trace/eval persistence is already part of the implementation, not just a future idea.

### 5. Compact conversation state

The current repo includes:
- `Grounded.Api/Services/ConversationStateService.cs`
- `Grounded.Api/Models/ConversationModels.cs`

The implementation supports deterministic follow-up resolution for a narrow set of patterns, which earlier planning docs treated as later-phase work.

### 6. Replay-aware evaluation and tests

The current repo includes:
- replay-capable model invocation
- planner tests
- trace tests
- replay tests
- conversation tests

Current test files include:
- `Grounded.Tests/AnalyticsPhase1PlannerTests.cs`
- `Grounded.Tests/AnalyticsPhase2Tests.cs`
- `Grounded.Tests/AnalyticsPhase2TraceTests.cs`
- `Grounded.Tests/AnalyticsPhase3ReplayTests.cs`
- `Grounded.Tests/Phase4IntegrationTests.cs`
- `Grounded.Tests/AnalyticsPhase5ConversationTests.cs`

This is materially beyond the assumptions in the older implementation docs.

---

## Verified Gaps And Remaining Work

### 1. There is a current planner-prompt/test mismatch

`dotnet test Grounded.slnx --no-restore` currently fails with:
- failed: 1
- passed: 44
- failing test: `Grounded.Tests.AnalyticsPhase1PlannerTests.PlannerPrompt_IncludesCanonicalUnsupportedInstructionsAndExamples`

Failure summary:
- the planner prompt content does not currently contain the expected string:
  - `Return exactly one JSON object matching the QueryPlan contract.`

This is the clearest immediate correctness issue verified locally.

### 2. Documentation now has drift

The repo contains several planning and execution docs written before or during implementation. Many now describe work as future work even though the code already exists.

Practical implication:
- documentation consolidation is now a real task

### 3. The next-value question is quality, not missing architecture

At this point, the highest-value work is likely:
- tightening prompt quality
- validating provider-backed behavior
- expanding benchmark depth
- improving observability of real runs
- cleaning up naming/documentation drift

The project is no longer blocked on missing top-level design pieces.

---

## Recommended Next Steps

### Priority 1. Fix the failing planner prompt contract

Reconcile:
- `prompts/planner/v1.md`
- `Grounded.Tests/AnalyticsPhase1PlannerTests.cs`

The repo should not carry a known failing planner-prompt contract test.

### Priority 2. Consolidate docs

Recommended action:
- keep one status doc
- keep one forward-looking roadmap
- mark older implementation docs as historical

Right now the docs overstate future work that the code already contains.

### Priority 3. Validate the provider-backed path end-to-end

The main remaining product question is not "does the architecture exist?" but:
- does the planner produce good plans reliably?
- does repair happen rarely enough?
- are failures categorized cleanly in real runs?

This should be exercised with real credentials in a controlled local run.

### Priority 4. Expand benchmark realism

The architecture for eval exists. The next improvement is to strengthen the benchmark corpus and use it to evaluate:
- unsupported requests
- ambiguous phrasing
- synonym-heavy phrasing
- adversarial schema-escape attempts
- follow-up handling boundaries

### Priority 5. Audit naming and leftovers

The repo has largely moved to `Grounded.*`, but there are still legacy remnants such as:
- old `LlmIntegrationDemo` names in generated artifacts
- deleted or renamed files still visible in git status

This is lower priority than correctness, but worth cleaning after tests are green.

---

## Bottom Line

The earlier implementation docs are no longer the active bottleneck.

`Grounded` already contains most of the architecture those docs were proposing. The right next move is to treat this as a verification, quality, and consolidation effort:
- fix the failing planner prompt contract
- consolidate stale docs
- validate the real provider path
- strengthen benchmarks
