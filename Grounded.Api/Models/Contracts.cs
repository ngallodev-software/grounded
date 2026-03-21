namespace Grounded.Api.Models;

public sealed record QueryPlan(
    string Version,
    string QuestionType,
    string? Dimension,
    IReadOnlyList<FilterSpec> Filters,
    string Metric,
    TimeRangeSpec TimeRange,
    string? TimeGrain,
    SortSpec Sort,
    int? Limit,
    bool UsePriorState,
    ResolvedFromSpec? ResolvedFrom = null,
    decimal? Confidence = null);

public sealed record ResolvedFromSpec(
    string? Metric = null,
    string? Dimension = null);

public sealed record FilterSpec(
    string Field,
    string Operator,
    IReadOnlyList<string> Values);

public sealed record SortSpec(
    string By,
    string Direction);

public sealed record TimeRangeSpec(
    string Preset,
    string? StartDate,
    string? EndDate);

public sealed record ExecuteQueryPlanRequest(
    QueryPlan QueryPlan,
    string? UserQuestion);

public sealed record ExecuteQueryPlanResponse(
    string Status,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Rows,
    QueryExecutionMetadata? Metadata,
    IReadOnlyList<ValidationErrorDto>? Errors,
    string? FailureCategory = null,
    AnswerDto? Answer = null,
    QueryExecutionTrace? Trace = null);

public sealed record CompiledQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters,
    int EffectiveLimit,
    bool ReturnsDimensionColumn,
    bool ReturnsTimeBucketColumn);

public sealed record QueryExecutionResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    QueryExecutionMetadata Metadata);

public sealed record QueryExecutionMetadata(
    string CompiledSql,
    IReadOnlyDictionary<string, object?> Parameters,
    int RowCount,
    long DurationMs,
    int AppliedRowLimit,
    DateTimeOffset? TimeRangeStartUtc,
    DateTimeOffset? TimeRangeEndExclusiveUtc);

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors);

public sealed record ValidationError(
    string Code,
    string Message);

public sealed record ValidationErrorDto(
    string Code,
    string Message);

public sealed record ResolvedTimeRange(
    DateTimeOffset? RangeStartUtc,
    DateTimeOffset? RangeEndExclusiveUtc);

public sealed record AnalyticsQueryPlanServiceResult(
    bool IsSuccess,
    ExecuteQueryPlanResponse Response);

public sealed record EvalResponse(EvalRun Run, RegressionComparisonResult Comparison);
