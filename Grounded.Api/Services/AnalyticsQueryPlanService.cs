using System;
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

    public AnalyticsQueryPlanService(
        QueryPlanValidator validator,
        TimeRangeResolver timeRangeResolver,
        QueryPlanCompiler compiler,
        SqlSafetyGuard sqlSafetyGuard,
        IAnalyticsQueryExecutor queryExecutor,
        AnswerSynthesizer answerSynthesizer,
        ILlmPlannerGateway plannerGateway)
    {
        _validator = validator;
        _timeRangeResolver = timeRangeResolver;
        _compiler = compiler;
        _sqlSafetyGuard = sqlSafetyGuard;
        _queryExecutor = queryExecutor;
        _answerSynthesizer = answerSynthesizer;
        _plannerGateway = plannerGateway;
    }

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteFromQuestionAsync(string userQuestion, CancellationToken cancellationToken)
    {
        var queryPlan = await _plannerGateway.PlanFromQuestionAsync(userQuestion, cancellationToken);
        return await ExecuteAsync(queryPlan, userQuestion, cancellationToken);
    }

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteAsync(QueryPlan queryPlan, string userQuestion, CancellationToken cancellationToken)
    {
        var validationResult = _validator.Validate(queryPlan);
        if (!validationResult.IsValid)
        {
            return Invalid(validationResult.Errors);
        }

        var startAt = DateTimeOffset.UtcNow;
        var resolvedTimeRange = _timeRangeResolver.Resolve(queryPlan.TimeRange);
        var compiledQuery = _compiler.Compile(queryPlan, resolvedTimeRange);

        var sqlSafetyResult = _sqlSafetyGuard.Validate(compiledQuery);
        if (!sqlSafetyResult.IsValid)
        {
            return Invalid(sqlSafetyResult.Errors);
        }

        var executionResult = await _queryExecutor.ExecuteAsync(compiledQuery, resolvedTimeRange, cancellationToken);
        var (answer, synthesizerTrace) = await _answerSynthesizer.SynthesizeAsync(
            userQuestion ?? string.Empty,
            queryPlan,
            executionResult.Rows,
            executionResult.Metadata,
            cancellationToken);

        var synthesisFailed = synthesizerTrace.ErrorMessage is not null;
        var trace = new QueryExecutionTrace(
            Guid.NewGuid().ToString("D"),
            queryPlan,
            executionResult.Metadata,
            compiledQuery.Sql,
            answer,
            synthesizerTrace,
            Evaluation: null,
            SynthesisFailed: synthesisFailed,
            StartedAt: startAt,
            CompletedAt: DateTimeOffset.UtcNow);

        var errors = synthesisFailed
            ? new[] { new ValidationErrorDto("synthesis_failed", synthesizerTrace.ErrorMessage!) }
            : (IReadOnlyList<ValidationErrorDto>?)null;

        return new(
            true,
            new(
                "success",
                executionResult.Rows,
                executionResult.Metadata,
                Errors: errors,
                Answer: answer,
                Trace: trace));
    }

    private static AnalyticsQueryPlanServiceResult Invalid(IReadOnlyList<ValidationError> errors) =>
        new(
            false,
            new(
                "error",
                Rows: null,
                Metadata: null,
                Errors: errors.Select(error => new ValidationErrorDto(error.Code, error.Message)).ToArray()));
}
