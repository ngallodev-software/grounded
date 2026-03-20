# Phase 4 Remediation Plan

Addresses deficiencies identified in the Phase 4 post-implementation review (spec §12).
Issues are ordered by severity and dependency.

---

## Task 1 — Fix scoring: replace exact-string matching with structural scoring

**Spec ref:** §12.1
**Files:** `ScoringService.cs`, `EvalModels.cs`, `EvalRunner.cs`

### Changes

**`ScoringService.cs`**

Replace `ForCase(bool executionSuccess, bool answerMatches)` with:

```
ForCase(bool executionSuccess, bool structuralCorrectness, bool answerGrounding) → decimal
```

Scoring weights:
- `executionSuccess`: +0.5
- `structuralCorrectness`: +0.3
- `answerGrounding`: +0.2

`IsPass`: requires `executionSuccess && structuralCorrectness`.

**Structural correctness** — evaluated by a new `StructuralAnswerChecker` (or inline in `EvalRunner`):
- `answer.Summary` is non-empty
- `answer.KeyPoints` has at least one entry

**Answer grounding** — evaluated inline in `EvalRunner`:
- extract all numeric and string leaf values from result rows
- check that `answer.Summary` contains at least one of those values as a substring

**`BenchmarkCaseResult`**

Remove `AnswerMatches`. Add:
```
bool StructuralCorrectness
bool AnswerGrounding
```

**`BenchmarkCase`**

Remove `ExpectedAnswer` (or demote to `string? ExpectedAnswer` kept for documentation only, never used in scoring).

**`EvalRunner`**

Update case evaluation loop to compute `structuralCorrectness` and `answerGrounding` from the actual answer + rows, then pass to `ScoringService.ForCase`.

---

## Task 2 — Add `/analytics/eval` HTTP endpoint

**Spec ref:** §12.2
**Files:** `AnalyticsController.cs`

### Changes

Add `POST /analytics/eval` to `AnalyticsController`:

```csharp
[HttpPost("eval")]
public async Task<ActionResult<EvalResponse>> RunEval(CancellationToken cancellationToken)
```

Where `EvalResponse` is a new contract record:
```csharp
public sealed record EvalResponse(EvalRun Run, RegressionComparisonResult Comparison);
```

Add `EvalResponse` to `Contracts.cs` or `EvalModels.cs`.

The endpoint injects `EvalRunner` (already scoped), calls `RunAsync`, and returns 200 with the result. On exception, return 500.

---

## Task 3 — Wire planner into eval pipeline

**Spec ref:** §12.3
**Files:** `EvalModels.cs`, `EvalRunner.cs`, `AnalyticsQueryPlanService.cs`

### Changes

**`BenchmarkCase`**

Remove `QueryPlan QueryPlan`. Replace with `string? ExpectedQueryPlan` (optional, for documentation). The only required fields become `CaseId`, `Category`, `Question`, and optionally `Notes`.

Update `benchmark_cases.jsonl` accordingly — remove the inline `queryPlan` objects.

**`AnalyticsQueryPlanService`**

Add overload (or new entry point):
```csharp
public async Task<AnalyticsQueryPlanServiceResult> ExecuteFromQuestionAsync(
    string userQuestion, CancellationToken cancellationToken)
```

This method calls the Phase 3 planner to produce a `QueryPlan`, then calls the existing `ExecuteAsync(QueryPlan, userQuestion, cancellationToken)` path. The planner integration already exists from Phase 3 — this just exposes it as the eval entry point.

**`EvalRunner`**

Change `_queryPlanService.ExecuteAsync(benchmarkCase.QueryPlan, ...)` to `_queryPlanService.ExecuteFromQuestionAsync(benchmarkCase.Question, ...)`.

Capture the planned `QueryPlan` from the service result trace for inclusion in `BenchmarkCaseResult`.

**`BenchmarkCaseResult`**

Add `QueryPlan? PlannedQueryPlan` to capture what the planner produced for each case.

---

## Task 4 — Fix regression history path

**Spec ref:** §12.4
**Files:** `RegressionComparer.cs`

### Changes

Replace `AppContext.BaseDirectory`-based path resolution with the same root-walk pattern used by `BenchmarkLoader`:

```
start from GetCurrentDirectory()
walk up until a directory containing eval/ is found
resolve historyPath relative to that root
```

This ensures history survives `dotnet clean` and is co-located with `benchmark_cases.jsonl`.

---

## Task 5 — Add SQL to `BenchmarkCaseResult`

**Spec ref:** §12.5
**Files:** `EvalModels.cs`, `EvalRunner.cs`

### Changes

**`BenchmarkCaseResult`**

Add `string? CompiledSql`.

**`EvalRunner`**

After execution, populate from `serviceResult.Response.Metadata?.CompiledSql`.

---

## Task 6 — Surface synthesis failures in service result

**Spec ref:** §12.6
**Files:** `Answering.cs`, `AnalyticsQueryPlanService.cs`

### Changes

**`QueryExecutionTrace`**

Add `bool SynthesisFailed` (true when `SynthesizerTrace.ErrorMessage` is non-null).

**`AnalyticsQueryPlanService`**

After synthesis, check `synthesizerTrace.ErrorMessage != null`. If so, set `SynthesisFailed = true` on the trace and include a `ValidationErrorDto` with code `synthesis_failed` in the response alongside the fallback answer. The response status remains `"success"` (execution succeeded) but the error is surfaced.

---

## Task 7 — Replace unit tests with integration tests

**Spec ref:** §12.7
**Files:** `AnswerSynthesizerTests.cs`, `BenchmarkLoaderTests.cs`

### Changes

Delete both existing test files. Replace with a single `Phase4IntegrationTests.cs` using `AnalyticsApiFactory` as the test host (same pattern as `AnalyticsPhase2Tests.cs`).

Tests to write:

1. `ExecuteQueryPlan_WithRealPlan_ReturnsSynthesizedAnswer`
   — POST to `/analytics/query-plan` with a valid plan + question
   — Assert: 200, `answer.summary` non-empty, `answer.keyPoints` non-empty

2. `ExecuteQueryPlan_WithEmptyRows_ReturnsFallbackSummary`
   — POST with a plan that returns no rows
   — Assert: 200, `answer.summary == "No data available for the requested query."`

3. `RunEval_ReturnsEvalRunWithScores`
   — POST to `/analytics/eval`
   — Assert: 200, `run.caseResults` non-empty, each case has `score >= 0`

4. `RunEval_Twice_ProducesRegressionComparison`
   — POST `/analytics/eval` twice
   — Assert: second response has `comparison` with `scoreDelta` field populated

---

## Task 8 — Fix unsafe metric column fallback

**Spec ref:** §12.8
**Files:** `DeterministicAnswerSynthesizerEngine.cs`

### Changes

In `DetermineMetricColumn`, after falling back to `columns.First()`, scan all columns to find the first one where at least one row yields a non-null result from `ConvertToDecimal`. If found, use that column. If none found, return `columns.First()` as a last resort (behavior unchanged for truly non-numeric data, but numeric columns now take precedence over arbitrary first-column fallback).

---

## Execution Order

| # | Task | Depends on |
|---|---|---|
| 1 | Fix scoring | — |
| 2 | Add /analytics/eval endpoint | — |
| 3 | Wire planner into eval | 2 |
| 4 | Fix regression history path | — |
| 5 | Add SQL to BenchmarkCaseResult | — |
| 6 | Surface synthesis failures | — |
| 7 | Replace unit tests with integration tests | 1, 2, 3 |
| 8 | Fix metric column fallback | — |

Tasks 1, 2, 4, 5, 6, 8 are independent and can be done in parallel.
Task 3 depends on 2 (needs the eval endpoint contract).
Task 7 depends on 1, 2, 3 (tests exercise the corrected paths).
