using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class AnalyticsQueryPlanService
{
    private readonly QueryPlanValidator _validator;
    private readonly TimeRangeResolver _timeRangeResolver;
    private readonly QueryPlanCompiler _compiler;
    private readonly SqlSafetyGuard _sqlSafetyGuard;
    private readonly IAnalyticsQueryExecutor _queryExecutor;
    private readonly AnswerSynthesizer _answerSynthesizer;
    private readonly ILlmPlannerGateway _plannerGateway;
    private readonly ITraceRepository _traceRepository;
    private readonly ConversationStateService _conversationStateService;

    public AnalyticsQueryPlanService(
        QueryPlanValidator validator,
        TimeRangeResolver timeRangeResolver,
        QueryPlanCompiler compiler,
        SqlSafetyGuard sqlSafetyGuard,
        IAnalyticsQueryExecutor queryExecutor,
        AnswerSynthesizer answerSynthesizer,
        ILlmPlannerGateway plannerGateway,
        ITraceRepository traceRepository,
        ConversationStateService conversationStateService)
    {
        _validator = validator;
        _timeRangeResolver = timeRangeResolver;
        _compiler = compiler;
        _sqlSafetyGuard = sqlSafetyGuard;
        _queryExecutor = queryExecutor;
        _answerSynthesizer = answerSynthesizer;
        _plannerGateway = plannerGateway;
        _traceRepository = traceRepository;
        _conversationStateService = conversationStateService;
    }

    public Task<AnalyticsQueryPlanServiceResult> ExecuteFromQuestionAsync(string userQuestion, CancellationToken cancellationToken) =>
        ExecuteFromQuestionAsync(userQuestion, requestId: Guid.NewGuid().ToString("D"), conversationId: null, cancellationToken);

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteFromQuestionAsync(string userQuestion, string requestId, CancellationToken cancellationToken)
        => await ExecuteFromQuestionAsync(userQuestion, requestId, conversationId: null, cancellationToken);

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteFromQuestionAsync(string userQuestion, string requestId, string? conversationId, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var traceId = Guid.NewGuid().ToString("D");
        var priorState = await _conversationStateService.GetAsync(conversationId, cancellationToken);
        var followUpResolution = _conversationStateService.Resolve(userQuestion, priorState);
        if (followUpResolution.IsFollowUp)
        {
            if (!followUpResolution.IsSupported || followUpResolution.QueryPlan is null)
            {
                return await InvalidAsync(
                    requestId,
                    traceId,
                    [new ValidationErrorDto(followUpResolution.ErrorCode ?? "unsupported_follow_up", followUpResolution.ErrorMessage ?? "unsupported follow-up")],
                    startedAt,
                    DateTimeOffset.UtcNow,
                    queryPlan: null,
                    plannerTrace: null,
                    plannerAttempt: null,
                    failureCategory: FailureCategories.UnsupportedRequest,
                    cancellationToken);
            }

            return await ExecuteInternalAsync(
                requestId,
                traceId,
                followUpResolution.QueryPlan,
                userQuestion,
                startedAt,
                cancellationToken,
                plannerTrace: null,
                plannerAttempt: null,
                conversationId);
        }

        var plannerResult = await _plannerGateway.PlanFromQuestionAsync(userQuestion, priorState, cancellationToken);
        if (!plannerResult.IsSuccess || plannerResult.QueryPlan is null)
        {
            return await InvalidAsync(
                requestId,
                traceId,
                plannerResult.Errors ?? [new ValidationErrorDto("planner_failure", "planner failed")],
                startedAt,
                completedAt: DateTimeOffset.UtcNow,
                queryPlan: null,
                plannerTrace: plannerResult.Trace,
                plannerAttempt: plannerResult.Attempt,
                failureCategory: plannerResult.Trace.FailureCategory,
                cancellationToken: cancellationToken);
        }

        return await ExecuteInternalAsync(
            requestId,
            traceId,
            plannerResult.QueryPlan,
            userQuestion,
            startedAt,
            cancellationToken,
            plannerResult.Trace,
            plannerResult.Attempt,
            conversationId);
    }

    public Task<AnalyticsQueryPlanServiceResult> ExecuteAsync(QueryPlan queryPlan, string userQuestion, CancellationToken cancellationToken) =>
        ExecuteAsync(queryPlan, userQuestion, Guid.NewGuid().ToString("D"), cancellationToken);

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteAsync(QueryPlan queryPlan, string userQuestion, string requestId, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var traceId = Guid.NewGuid().ToString("D");
        return await ExecuteInternalAsync(requestId, traceId, queryPlan, userQuestion, startedAt, cancellationToken, plannerTrace: null, plannerAttempt: null);
    }

    private async Task<AnalyticsQueryPlanServiceResult> ExecuteInternalAsync(
        string requestId,
        string traceId,
        QueryPlan queryPlan,
        string userQuestion,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken,
        PlannerTrace? plannerTrace,
        PersistedPlannerAttempt? plannerAttempt,
        string? conversationId = null)
    {
        var validationResult = _validator.Validate(queryPlan);
        if (!validationResult.IsValid)
        {
            var failureCategory =
                validationResult.Errors.Any(static error => error.Code == "unsupported_question_type") ||
                (queryPlan.Metric == "__unsupported__" && validationResult.Errors.Any(static error => error.Code == "invalid_metric"))
                    ? FailureCategories.UnsupportedRequest
                    : FailureCategories.PlannerValidationFailure;
            var traceWithFailure = plannerTrace is null
                ? null
                : plannerTrace with
                {
                    FailureCategory = failureCategory,
                    FailureMessage = string.Join("; ", validationResult.Errors.Select(static error => error.Message))
                };
            return await InvalidAsync(
                requestId,
                traceId,
                validationResult.Errors.Select(static error => new ValidationErrorDto(error.Code, error.Message)).ToArray(),
                startedAt,
                DateTimeOffset.UtcNow,
                queryPlan,
                traceWithFailure,
                plannerAttempt,
                failureCategory,
                cancellationToken);
        }

        var resolvedTimeRange = _timeRangeResolver.Resolve(queryPlan.TimeRange);
        var compiledQuery = _compiler.Compile(queryPlan, resolvedTimeRange);

        var sqlSafetyResult = _sqlSafetyGuard.Validate(compiledQuery);
        if (!sqlSafetyResult.IsValid)
        {
            return await InvalidAsync(
                requestId,
                traceId,
                sqlSafetyResult.Errors.Select(static error => new ValidationErrorDto(error.Code, error.Message)).ToArray(),
                startedAt,
                DateTimeOffset.UtcNow,
                queryPlan,
                plannerTrace,
                plannerAttempt,
                FailureCategories.SqlSafetyFailure,
                cancellationToken);
        }

        QueryExecutionResult executionResult;
        try
        {
            executionResult = await _queryExecutor.ExecuteAsync(compiledQuery, resolvedTimeRange, cancellationToken);
        }
        catch (Exception exception)
        {
            return await InvalidAsync(
                requestId,
                traceId,
                [new ValidationErrorDto(FailureCategories.ExecutionFailure, exception.Message)],
                startedAt,
                DateTimeOffset.UtcNow,
                queryPlan,
                plannerTrace,
                plannerAttempt,
                FailureCategories.ExecutionFailure,
                cancellationToken,
                compiledSql: compiledQuery.Sql);
        }

        var (answer, synthesizerTrace, synthesisAttempt) = await _answerSynthesizer.SynthesizeAsync(
            userQuestion,
            queryPlan,
            executionResult.Rows,
            executionResult.Metadata,
            cancellationToken);

        var completedAt = DateTimeOffset.UtcNow;
        var synthesisFailed = synthesizerTrace.FailureCategory != FailureCategories.None;
        var finalFailureCategory = synthesisFailed ? FailureCategories.SynthesisFailure : FailureCategories.None;
        var trace = new QueryExecutionTrace(
            requestId,
            traceId,
            queryPlan,
            plannerTrace,
            executionResult.Metadata,
            compiledQuery.Sql,
            answer,
            synthesizerTrace,
            Evaluation: null,
            plannerTrace is null ? "not_requested" : "completed",
            synthesisFailed ? "failed" : "completed",
            synthesisFailed ? "partial_success" : "success",
            finalFailureCategory,
            synthesisFailed,
            startedAt,
            completedAt);

        var errors = synthesisFailed
            ? new[] { new ValidationErrorDto(FailureCategories.SynthesisFailure, synthesizerTrace.ErrorMessage ?? "synthesis failed") }
            : null;

        await _traceRepository.PersistAsync(
            new PersistedTraceRecord(
                requestId,
                traceId,
                startedAt,
                completedAt,
                synthesisFailed ? "partial_success" : "success",
                finalFailureCategory,
                queryPlan,
                errors,
                compiledQuery.Sql,
                executionResult.Metadata.RowCount,
                plannerAttempt,
                synthesisAttempt),
            cancellationToken);

        await _conversationStateService.SaveAsync(conversationId, queryPlan, cancellationToken);

        return new AnalyticsQueryPlanServiceResult(
            true,
            new ExecuteQueryPlanResponse(
                "success",
                executionResult.Rows,
                executionResult.Metadata,
                errors,
                finalFailureCategory,
                answer,
                trace));
    }

    private async Task<AnalyticsQueryPlanServiceResult> InvalidAsync(
        string requestId,
        string traceId,
        IReadOnlyList<ValidationErrorDto> errors,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        QueryPlan? queryPlan,
        PlannerTrace? plannerTrace,
        PersistedPlannerAttempt? plannerAttempt,
        string failureCategory,
        CancellationToken cancellationToken,
        string? compiledSql = null)
    {
        await _traceRepository.PersistAsync(
            new PersistedTraceRecord(
                requestId,
                traceId,
                startedAt,
                completedAt,
                "error",
                failureCategory,
                queryPlan,
                errors,
                compiledSql,
                null,
                plannerAttempt,
                null),
            cancellationToken);

        return new AnalyticsQueryPlanServiceResult(
            false,
            new ExecuteQueryPlanResponse(
                "error",
                Rows: null,
                Metadata: null,
                Errors: errors,
                FailureCategory: failureCategory,
                Trace: new QueryExecutionTrace(
                    requestId,
                    traceId,
                    queryPlan,
                    plannerTrace,
                    null,
                    compiledSql ?? string.Empty,
                    null,
                    null,
                    null,
                    plannerTrace is null ? "not_requested" : "failed",
                    "not_requested",
                    "error",
                    failureCategory,
                    false,
                    startedAt,
                    completedAt)));
    }
}
