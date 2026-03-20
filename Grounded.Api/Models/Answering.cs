using System;
using System.Collections.Generic;

namespace Grounded.Api.Models;

public sealed record AnswerDto(
    string Summary,
    IReadOnlyList<string> KeyPoints,
    bool TableIncluded);

public sealed record SynthesizerTrace(
    string Provider,
    string PromptVersion,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    int TokensIn,
    int TokensOut,
    string FailureCategory,
    string? ErrorMessage);

public sealed record EvaluationTrace(
    string RunId,
    decimal Score,
    bool Passed,
    string? Notes);

public sealed record QueryExecutionTrace(
    string RequestId,
    string TraceId,
    QueryPlan? QueryPlan,
    PlannerTrace? Planner,
    QueryExecutionMetadata? Metadata,
    string CompiledSql,
    AnswerDto? Answer,
    SynthesizerTrace? Synthesizer,
    EvaluationTrace? Evaluation,
    string PlannerStatus,
    string SynthesisStatus,
    string FinalStatus,
    string FailureCategory,
    bool SynthesisFailed,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
