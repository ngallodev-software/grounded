using System;
using System.Collections.Generic;

namespace LlmIntegrationDemo.Api.Models;

public sealed record BenchmarkCase(
    string CaseId,
    string Category,
    string Question,
    QueryPlan QueryPlan,
    string? ExpectedAnswer,
    string? Notes);

public sealed record BenchmarkCaseResult(
    string CaseId,
    string Question,
    bool ExecutionSuccess,
    bool AnswerMatches,
    bool Passed,
    decimal Score,
    string? Notes,
    QueryExecutionMetadata? ExecutionMetadata,
    AnswerDto? Answer);

public sealed record EvalRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string PlannerPromptVersion,
    string SynthesizerPromptVersion,
    decimal Score,
    IReadOnlyList<BenchmarkCaseResult> CaseResults);

public sealed record RegressionComparisonResult(
    bool HasRegression,
    decimal ScoreDelta,
    IReadOnlyList<string> Notes);
