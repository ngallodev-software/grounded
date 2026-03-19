using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class AnalyticsQueryPlanService
{
    private readonly QueryPlanValidator _validator;
    private readonly TimeRangeResolver _timeRangeResolver;
    private readonly QueryPlanCompiler _compiler;
    private readonly SqlSafetyGuard _sqlSafetyGuard;
    private readonly IAnalyticsQueryExecutor _queryExecutor;

    public AnalyticsQueryPlanService(
        QueryPlanValidator validator,
        TimeRangeResolver timeRangeResolver,
        QueryPlanCompiler compiler,
        SqlSafetyGuard sqlSafetyGuard,
        IAnalyticsQueryExecutor queryExecutor)
    {
        _validator = validator;
        _timeRangeResolver = timeRangeResolver;
        _compiler = compiler;
        _sqlSafetyGuard = sqlSafetyGuard;
        _queryExecutor = queryExecutor;
    }

    public async Task<AnalyticsQueryPlanServiceResult> ExecuteAsync(QueryPlan queryPlan, CancellationToken cancellationToken)
    {
        var validationResult = _validator.Validate(queryPlan);
        if (!validationResult.IsValid)
        {
            return Invalid(validationResult.Errors);
        }

        var resolvedTimeRange = _timeRangeResolver.Resolve(queryPlan.TimeRange);
        var compiledQuery = _compiler.Compile(queryPlan, resolvedTimeRange);

        var sqlSafetyResult = _sqlSafetyGuard.Validate(compiledQuery);
        if (!sqlSafetyResult.IsValid)
        {
            return Invalid(sqlSafetyResult.Errors);
        }

        var executionResult = await _queryExecutor.ExecuteAsync(compiledQuery, resolvedTimeRange, cancellationToken);
        return new(
            true,
            new(
                "success",
                executionResult.Rows,
                executionResult.Metadata,
                Errors: null));
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
