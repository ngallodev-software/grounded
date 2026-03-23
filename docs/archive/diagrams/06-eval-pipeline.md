# Eval Pipeline Diagram

`POST /analytics/eval` runs all benchmark cases through the live pipeline and scores each one.

```mermaid
flowchart TD
    TRIGGER["POST /analytics/eval\n(AnalyticsController)"]
    BL["BenchmarkLoader\nLoadCases() → benchmark_cases.jsonl"]
    LOOP["For each BenchmarkCase"]
    QPS["AnalyticsQueryPlanService\nExecuteFromQuestionAsync()"]
    SS["ScoringService\nScore(result, case)"]

    subgraph Scoring["Scoring Dimensions"]
        S1["Execution Success\n(weight 0.5)\nquery ran + returned rows"]
        S2["Structural Correctness\n(weight 0.3)\nnon-empty summary + ≥1 keyPoint"]
        S3["Answer Grounding\n(weight 0.2)\n≥1 result value in summary text"]
    end

    AOV["AnswerOutputValidator\nIsAnswerGrounded()"]
    RC["RegressionComparer\nFlag cases: passed→failed"]
    EVR["IEvalRepository\nPersistAsync(evalRun)"]
    RESP["EvalResponse\n{EvalRun, RegressionComparisonResult}"]

    subgraph InvokerMode["Invoker Selection"]
        REPLAY["GROUNDED_REPLAY_MODE=true\n→ ReplayModelInvoker\n(fixture-based, stable)"]
        LIVE["GROUNDED_REPLAY_MODE=false\n→ OpenAiCompatibleModelInvoker\n(live LLM, expensive)"]
    end

    TRIGGER --> BL --> LOOP --> QPS
    QPS --> InvokerMode
    QPS --> SS
    SS --> S1 & S2 & S3
    S3 --> AOV
    SS --> RC
    RC --> EVR --> RESP

    note1["Passed = execution_success AND structural_correctness\nGrounding is a signal, not a gate"]

    style Scoring fill:#1a4a3a,color:#e2e8f0
    style InvokerMode fill:#4a235a,color:#e2e8f0
```

## Scoring Formula

| Dimension | Weight | Gate? | Description |
|---|---|---|---|
| Execution success | 0.50 | Yes | Query ran without error and returned ≥1 row |
| Structural correctness | 0.30 | Yes | Non-empty `summary` and ≥1 `keyPoint` present |
| Answer grounding | 0.20 | No (signal) | ≥1 scalar value from result rows appears in `summary` |

**Pass condition**: `execution_success AND structural_correctness`
A case can pass without perfect grounding; the score reflects the grounding gap.
