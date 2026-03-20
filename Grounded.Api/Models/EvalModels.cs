using System;
using System.Collections.Generic;

namespace Grounded.Api.Models;

public sealed record BenchmarkCase(
    string CaseId,
    string Category,
    string Question,
    string? ExpectedAnswer = null,
    string? ExpectedOutcomeType = null,
    string? ExpectedFailureCategory = null,
    IReadOnlyDictionary<string, string>? ExpectedPlanAssertions = null,
    IReadOnlyList<string>? Tags = null,
    string? Notes = null);

public sealed record BenchmarkCaseResult(
    string CaseId,
    string Question,
    bool ExecutionSuccess,
    bool StructuralCorrectness,
    bool AnswerGrounding,
    bool Passed,
    decimal Score,
    string? CompiledSql,
    QueryPlan? PlannedQueryPlan,
    string? FailureCategory,
    long PlannerLatencyMs,
    long SynthesisLatencyMs,
    int TotalTokensIn,
    int TotalTokensOut,
    string? Notes,
    QueryExecutionMetadata? ExecutionMetadata,
    AnswerDto? Answer);

public sealed record EvalRunSummary(
    decimal PlannerValidityRate,
    decimal ExecutionSuccessRate,
    decimal GroundingRate,
    decimal AverageLatencyMs,
    decimal AverageTokensIn,
    decimal AverageTokensOut,
    IReadOnlyDictionary<string, int> FailureCounts);

public sealed record EvalRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string PlannerPromptVersion,
    string SynthesizerPromptVersion,
    decimal Score,
    EvalRunSummary Summary,
    IReadOnlyList<BenchmarkCaseResult> CaseResults);

public sealed record RegressionComparisonResult(
    bool HasRegression,
    decimal ScoreDelta,
    IReadOnlyList<string> Notes);
