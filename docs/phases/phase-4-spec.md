# Phase 4 Spec — Answer Synthesis + Evaluation

## 1. Purpose

Phase 4 adds:

1. Answer synthesis
   - Convert structured query results into a human-readable response
   - Strictly grounded in execution output

2. Evaluation + regression testing
   - Measure correctness across benchmark questions
   - Detect regressions when prompts change

---

## 2. Scope

### In scope
- answer synthesizer prompt
- synthesis LLM call
- structured answer output
- synthesis validation
- benchmark execution
- eval runner
- scoring rules
- regression comparison

### Out of scope
- UI / dashboards
- human labeling tools
- advanced eval frameworks
- prompt auto-tuning
- feedback loops
- multi-model routing

---

## 3. End-to-End Flow

```
User Question
   ↓
Planner (Phase 3)
   ↓
QueryPlan
   ↓
Execution (Phase 2)
   ↓
Raw Results
   ↓
AnswerSynthesizer
   ↓
Final Answer
```

---

## 4. Components

### 4.1 AnswerSynthesizer
- consumes query results + QueryPlan + user question
- calls LLM
- returns structured answer

### 4.2 Prompt File
/prompts/answer-synthesizer/v1.md

### 4.3 Output Contract

```
{
  "summary": "...",
  "keyPoints": [],
  "tableIncluded": true
}
```

### 4.4 Synthesis Validator
- required fields present
- no hallucinated metrics/dimensions
- aligns with results

### 4.5 EvalRunner
- executes benchmark dataset
- captures outputs

### 4.6 Scoring Engine

Minimum scoring:
- execution correctness
- structural correctness
- answer grounding

### 4.7 Regression Comparison
- compare prompt versions
- track pass/fail and score changes

---

## 5. Prompt Strategy

Two prompts:
- planner (Phase 3)
- synthesizer (Phase 4)

Planner → structured  
Synthesizer → human-readable but grounded

---

## 6. Context Strategy

### Include
- user question
- QueryPlan
- result rows (capped)

### Exclude
- schema
- full history
- SQL

---

## 7. Guardrails

- no hallucinated values
- no new metrics
- no invented insights

---

## 8. Eval Dataset

Each case:
```
question → planner → execution → synthesis → scoring
```

---

## 9. Acceptance Criteria

- answers generated for NL queries
- grounded in real data
- no hallucinations
- eval runner executes all cases
- scoring produced
- regression comparison works
- failures categorized

---

## 10. Explicit Non-Goals

- no database access in synthesizer
- no QueryPlan modification
- no chat memory
- no multi-prompt system
- no over-engineered scoring

---

## 11. Outcome

Phase 4 enables:
- answer synthesis
- evaluation + regression testing
- grounded LLM outputs

---

## 12. Addendum — Post-Implementation Review Findings (2026-03-19)

A review of the Phase 4 implementation identified the following deficiencies. These are captured here as required corrections before Phase 4 can be considered complete.

### 12.1 Eval scoring is non-functional (High)

`ScoringService.IsPass` requires both `executionSuccess AND answerMatches`. `answerMatches` is an exact string comparison of `answer.Summary` against the `expectedAnswer` field in the benchmark case. Because the deterministic engine generates summaries that will rarely match the static expected strings exactly, and because execution against a real DB returns real data rather than the zero-value assumed by the benchmark fixtures, every case scores 0. The eval runner produces output but validates nothing.

**Required fix:** Replace exact-string matching with structural scoring:
- `executionSuccess` (+0.5): query executed without error
- `structuralCorrectness` (+0.3): answer has non-empty summary and at least one key point
- `answerGrounding` (+0.2): summary contains at least one value present in the result rows

Remove the `answerMatches` field or repurpose it as a soft signal, not a hard gate on `IsPass`.

### 12.2 No `/analytics/eval` endpoint (High)

`EvalRunner` is registered as a scoped service but is never reachable via HTTP. There is no way to trigger an eval run without running code directly.

**Required fix:** Add `POST /analytics/eval` endpoint that calls `EvalRunner.RunAsync` and returns `EvalRun` + `RegressionComparisonResult`.

### 12.3 Eval does not exercise the planner (High)

The spec states the eval pipeline as `question → planner → execution → synthesis → scoring`. However `BenchmarkCase` contains a pre-built `QueryPlan` and `EvalRunner` passes it directly to `AnalyticsQueryPlanService.ExecuteAsync`, bypassing planning entirely. The planner is never tested.

**Required fix:** `BenchmarkCase` should contain only `question`. The eval runner should pass the question through the full pipeline including the planner. The pre-built `QueryPlan` in `BenchmarkCase` should be removed or moved to an optional `expectedQueryPlan` field for plan-level assertion.

**Note:** This requires Phase 3 planner integration to be wired into the eval path. The `AnalyticsQueryPlanService` will need to accept a raw question and invoke planning before execution.

### 12.4 Regression history stored in build output directory (Medium)

`RegressionComparer` resolves `_historyPath` relative to `AppContext.BaseDirectory`, which is the build output directory (`bin/Debug/net8.0/`). This directory is deleted on `dotnet clean`, destroying regression history.

**Required fix:** Use the same root-walk strategy as `BenchmarkLoader` — walk up from `GetCurrentDirectory()` to find the repo root, then store history at `eval/regression_history.json` relative to the repo root.

### 12.5 SQL missing from `BenchmarkCaseResult` (Medium)

The execution prompt specifies that each benchmark run must capture: question, QueryPlan, SQL, result, answer text, pass/fail, score. `BenchmarkCaseResult` has no SQL field. The compiled SQL is only accessible via `QueryExecutionMetadata.CompiledSql`, which is nullable.

**Required fix:** Add `string? CompiledSql` to `BenchmarkCaseResult`. Populate it from `executionMetadata?.CompiledSql` after execution.

### 12.6 Synthesis failures silently fall back (Medium)

`AnswerSynthesizer.SynthesizeAsync` catches all exceptions and returns a fallback `AnswerDto`. The calling code in `AnalyticsQueryPlanService` cannot distinguish a successful synthesis from a silent failure. The `SynthesizerTrace.ErrorMessage` records the failure but is not checked by the caller.

**Required fix:** Surface synthesis failure in the service result. Either return a distinct status in `ExecuteQueryPlanResponse` when synthesis fails, or add a `SynthesisFailed` flag to `QueryExecutionTrace` that callers can inspect.

### 12.7 Tests are unit tests, not integration tests (Medium)

`AnswerSynthesizerTests` and `BenchmarkLoaderTests` construct services in isolation. Per project testing strategy, unit tests are not written. These should be integration tests that exercise the full pipeline.

**Required fix:** Replace both test files with integration tests that wire up the full service graph (validator → compiler → executor → synthesizer) and assert on end-to-end output. Use `AnalyticsApiFactory` (already present in `AnalyticsPhase2Tests`) as the test host pattern.

### 12.8 Metric column fallback is unsafe (Low)

`DeterministicAnswerSynthesizerEngine.DetermineMetricColumn` falls back to `columns.First()` when no metric column is identified. If the first column is a string dimension, `ConvertToDecimal` returns null for all rows and `BuildSummary` formats a non-numeric value as a metric.

**Required fix:** After falling back to `columns.First()`, verify that at least one row has a convertible numeric value in that column. If not, scan remaining columns for a numeric-valued column before giving up.
