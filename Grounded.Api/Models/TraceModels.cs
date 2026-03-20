using System;
using System.Collections.Generic;

namespace Grounded.Api.Models;

public static class FailureCategories
{
    public const string None = "none";
    public const string TransportFailure = "transport_failure";
    public const string Timeout = "timeout";
    public const string ProviderError = "provider_error";
    public const string JsonParseFailure = "json_parse_failure";
    public const string PlannerValidationFailure = "planner_validation_failure";
    public const string UnsupportedRequest = "unsupported_request";
    public const string SqlSafetyFailure = "sql_safety_failure";
    public const string ExecutionFailure = "execution_failure";
    public const string SynthesisFailure = "synthesis_failure";
}

public sealed record ModelRequest(
    string InvokerName,
    string PromptText,
    string? PayloadJson,
    string PromptKey,
    string PromptVersion,
    string PromptChecksum,
    string ModelEnvironmentVariable,
    string ApiKeyEnvironmentVariable);

public sealed record ModelUsage(
    int TokensIn,
    int TokensOut);

public sealed record ModelResponse(
    string Content,
    string Provider,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    ModelUsage Usage);

public sealed record ModelFailure(
    string Category,
    string Message);

public sealed record ModelInvocationResult(
    bool IsSuccess,
    ModelResponse? Response,
    ModelFailure? Failure);

public sealed record PersistedPlannerAttempt(
    string PromptKey,
    string PromptVersion,
    string PromptChecksum,
    string Provider,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    long LatencyMs,
    int TokensIn,
    int TokensOut,
    bool ParseSucceeded,
    bool RepairAttempted,
    bool RepairSucceeded,
    string FailureCategory,
    string? FailureMessage,
    string? RawResponse,
    string? RepairedResponse,
    string? ParsedQueryPlanJson);

public sealed record PersistedSynthesisAttempt(
    string PromptKey,
    string PromptVersion,
    string PromptChecksum,
    string Provider,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    long LatencyMs,
    int TokensIn,
    int TokensOut,
    string FailureCategory,
    string? FailureMessage,
    string? RawResponse,
    string? AnswerJson);

public sealed record PersistedTraceRecord(
    string RequestId,
    string TraceId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string FinalStatus,
    string FailureCategory,
    QueryPlan? QueryPlan,
    IReadOnlyList<ValidationErrorDto>? ValidationErrors,
    string? CompiledSql,
    int? RowCount,
    PersistedPlannerAttempt? PlannerAttempt,
    PersistedSynthesisAttempt? SynthesisAttempt);

public sealed record PersistedEvalRun(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string PlannerPromptVersion,
    string SynthesizerPromptVersion,
    decimal Score,
    IReadOnlyList<BenchmarkCaseResult> CaseResults,
    RegressionComparisonResult Comparison);
