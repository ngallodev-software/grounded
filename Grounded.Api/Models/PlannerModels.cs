using System;
using System.Collections.Generic;
using Grounded.Api.Services;

namespace Grounded.Api.Models;

public sealed record PlannerContext(
    IReadOnlyList<string> SupportedQuestionTypes,
    IReadOnlyList<string> SupportedMetrics,
    IReadOnlyList<string> SupportedDimensions,
    IReadOnlyList<PlannerFilterDefinition> SupportedFilters,
    IReadOnlyList<string> SupportedOperators,
    IReadOnlyList<string> SupportedTimePresets,
    IReadOnlyList<string> SupportedTimeGrains,
    IReadOnlyList<string> SupportedSortBy,
    IReadOnlyList<string> SupportedSortDirections,
    IReadOnlyList<PlannerSchemaEntity> Schema,
    IReadOnlyList<PlannerExample> Examples);

public sealed record PlannerFilterDefinition(
    string Field,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string>? AllowedValues);

public sealed record PlannerSchemaEntity(
    string Name,
    IReadOnlyList<string> Columns);

public sealed record PlannerExample(
    string Question,
    QueryPlan Plan);

public sealed record PlannerPromptRenderResult(
    PromptDefinition Prompt,
    string RenderedPrompt,
    PlannerContext Context);

public sealed record PlannerRequest(
    string UserQuestion,
    PlannerPromptRenderResult Prompt);

public sealed record PlannerUsage(
    int TokensIn,
    int TokensOut);

public sealed record PlannerRawResponse(
    string Content,
    string Provider,
    string ModelName,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt,
    PlannerUsage Usage);

public sealed record PlannerParseResult(
    bool IsSuccess,
    QueryPlan? QueryPlan,
    string? FailureMessage,
    string FailureCategory,
    string? OriginalContent,
    string? RepairedContent,
    bool RepairAttempted,
    bool RepairSucceeded);

public sealed record PlannerGatewayResult(
    bool IsSuccess,
    QueryPlan? QueryPlan,
    PlannerTrace Trace,
    PersistedPlannerAttempt Attempt,
    IReadOnlyList<ValidationErrorDto>? Errors);

public sealed record PlannerTrace(
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
    bool CacheHit,
    string FailureCategory,
    string? FailureMessage);
