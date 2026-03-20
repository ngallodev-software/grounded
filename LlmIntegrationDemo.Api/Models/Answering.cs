using System;
using System.Collections.Generic;

namespace LlmIntegrationDemo.Api.Models;

public sealed record AnswerDto(
    string Summary,
    IReadOnlyList<string> KeyPoints,
    bool TableIncluded);

public sealed record SynthesizerTrace(
    string PromptVersion,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    int TokensIn,
    int TokensOut,
    string? ErrorMessage);

public sealed record EvaluationTrace(
    string RunId,
    decimal Score,
    bool Passed,
    string? Notes);

public sealed record QueryExecutionTrace(
    string TraceId,
    QueryPlan QueryPlan,
    QueryExecutionMetadata? Metadata,
    string CompiledSql,
    AnswerDto? Answer,
    SynthesizerTrace? Synthesizer,
    EvaluationTrace? Evaluation,
    bool SynthesisFailed,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
