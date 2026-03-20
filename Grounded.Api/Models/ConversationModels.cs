namespace Grounded.Api.Models;

public sealed record ExecuteAnalyticsQuestionRequest(
    string? Question,
    string? ConversationId = null);

public sealed record ConversationStateSnapshot(
    string QuestionType,
    string Metric,
    string? Dimension,
    IReadOnlyList<FilterSpec> Filters,
    TimeRangeSpec TimeRange);

public sealed record FollowUpResolutionResult(
    bool IsFollowUp,
    bool IsSupported,
    QueryPlan? QueryPlan,
    string? ErrorCode,
    string? ErrorMessage);
