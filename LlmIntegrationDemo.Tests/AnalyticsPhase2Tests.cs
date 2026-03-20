using System.Net;
using System.Net.Http.Json;
using LlmIntegrationDemo.Api.Models;
using LlmIntegrationDemo.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LlmIntegrationDemo.Tests;

public sealed class AnalyticsPhase2Tests
{
    private readonly QueryPlanValidator _validator = new();
    private readonly QueryPlanCompiler _compiler = new(new SqlFragmentRegistry());
    private readonly SqlSafetyGuard _sqlSafetyGuard = new();
    private readonly TimeRangeResolver _timeRangeResolver = new(new FixedClock(new DateTimeOffset(2026, 03, 19, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void AggregateRevenuePlan_CompilesToSingleMetricSelect()
    {
        var plan = CreatePlan(questionType: "aggregate", metric: "revenue", timeRange: new("last_month", null, null));

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("SELECT SUM((oi.quantity * oi.unit_price) - oi.discount_amount) AS metric", compiled.Sql);
        Assert.DoesNotContain("GROUP BY", compiled.Sql, StringComparison.Ordinal);
        Assert.Equal(1, compiled.EffectiveLimit);
        Assert.False(compiled.ReturnsDimensionColumn);
    }

    [Fact]
    public void GroupedBreakdown_CompilesExpectedGroupBy()
    {
        var plan = CreatePlan(
            questionType: "grouped_breakdown",
            metric: "revenue",
            dimension: "product_category",
            sort: new("metric", "desc"));

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("GROUP BY p.category", compiled.Sql);
        Assert.Contains("AS dimension", compiled.Sql);
        Assert.Contains("LIMIT 200", compiled.Sql);
    }

    [Fact]
    public void Ranking_CompilesExpectedOrderingAndLimit()
    {
        var plan = CreatePlan(
            questionType: "ranking",
            metric: "units_sold",
            dimension: "product_name",
            sort: new("metric", "desc"),
            limit: 5);

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("ORDER BY metric DESC, dimension ASC", compiled.Sql);
        Assert.Contains("LIMIT 5", compiled.Sql);
        Assert.Equal(5, compiled.EffectiveLimit);
    }

    [Fact]
    public void TimeSeries_CompilesDateTruncAndStableOrdering()
    {
        var plan = CreatePlan(
            questionType: "time_series",
            metric: "revenue",
            timeGrain: "month",
            sort: new("metric", "desc"));

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("DATE_TRUNC('month', o.order_date) AS time_bucket", compiled.Sql);
        Assert.Contains("ORDER BY time_bucket ASC", compiled.Sql);
        Assert.Contains("LIMIT 366", compiled.Sql);
    }

    [Fact]
    public void InvalidMetric_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(metric: "gross_margin"));

        Assert.Contains(result.Errors, error => error.Code == "invalid_metric");
    }

    [Fact]
    public void InvalidDimension_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(questionType: "aggregate", dimension: "customer_region"));

        Assert.Contains(result.Errors, error => error.Code == "invalid_dimension");
    }

    [Fact]
    public void InvalidFilterField_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(filters: [new("status", "eq", ["Completed"])]));

        Assert.Contains(result.Errors, error => error.Code == "invalid_filter_field");
    }

    [Fact]
    public void InvalidLimit_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(questionType: "ranking", dimension: "product_name", limit: null));

        Assert.Contains(result.Errors, error => error.Code == "invalid_limit");
    }

    [Fact]
    public void InvalidTimeRange_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(timeRange: new("custom_range", "2026-03-20", "2026-03-19")));

        Assert.Contains(result.Errors, error => error.Code == "invalid_time_range");
    }

    [Fact]
    public void SimpleFollowUp_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(questionType: "simple_follow_up"));

        Assert.Contains(result.Errors, error => error.Code == "unsupported_question_type");
    }

    [Fact]
    public void OrderCountWithProductFilter_UsesDistinctOrderCount()
    {
        var plan = CreatePlan(filters: [new("product_category", "eq", ["Electronics"])], metric: "order_count");

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("COUNT(DISTINCT o.id) AS metric", compiled.Sql);
        Assert.Contains("JOIN order_items oi ON oi.order_id = o.id", compiled.Sql);
        Assert.Contains("JOIN products p ON p.id = oi.product_id", compiled.Sql);
    }

    [Fact]
    public void NewCustomerCountWithExistingCustomerFilter_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(
            metric: "new_customer_count",
            filters: [new("customer_type", "eq", ["existing"])]));

        Assert.Contains(result.Errors, error => error.Code == "invalid_filter_values");
    }

    [Fact]
    public void GroupedBreakdownSortedByDimension_CompilesAlphabeticalOrdering()
    {
        var plan = CreatePlan(
            questionType: "grouped_breakdown",
            metric: "revenue",
            dimension: "customer_segment",
            sort: new("dimension", "asc"));

        var compiled = _compiler.Compile(plan, _timeRangeResolver.Resolve(plan.TimeRange));

        Assert.Contains("ORDER BY dimension ASC", compiled.Sql);
    }

    [Fact]
    public void TimeSeriesWithLimit_IsRejected()
    {
        var result = _validator.Validate(CreatePlan(questionType: "time_series", timeGrain: "month", limit: 10));

        Assert.Contains(result.Errors, error => error.Code == "invalid_limit");
    }

    [Fact]
    public void MultiStatementSql_IsBlocked()
    {
        var result = _sqlSafetyGuard.Validate(new CompiledQuery(
            "SELECT 1; SELECT 2;",
            new Dictionary<string, object?>(),
            1,
            false,
            false));

        Assert.Contains(result.Errors, error => error.Code == "unsafe_sql");
    }

    [Fact]
    public void DisallowedKeyword_IsBlocked()
    {
        var result = _sqlSafetyGuard.Validate(new CompiledQuery(
            "WITH data AS (SELECT 1) UPDATE orders SET status = 'Completed'",
            new Dictionary<string, object?>(),
            1,
            false,
            false));

        Assert.Contains(result.Errors, error => error.Code == "unsafe_sql");
    }

    [Fact]
    public async Task Controller_Returns422ForBusinessValidationFailure()
    {
        await using var factory = new AnalyticsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/analytics/query-plan", new ExecuteQueryPlanRequest(CreatePlan(questionType: "simple_follow_up")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecuteQueryPlanResponse>();
        Assert.NotNull(body);
        Assert.Equal("error", body!.Status);
        Assert.Contains(body.Errors!, error => error.Code == "unsupported_question_type");
    }

    private static QueryPlan CreatePlan(
        string questionType = "aggregate",
        string metric = "revenue",
        string? dimension = null,
        IReadOnlyList<FilterSpec>? filters = null,
        TimeRangeSpec? timeRange = null,
        string? timeGrain = null,
        SortSpec? sort = null,
        int? limit = null,
        bool usePriorState = false) =>
        new(
            "1.0",
            questionType,
            dimension,
            filters ?? [],
            metric,
            timeRange ?? new("last_30_days", null, null),
            timeGrain,
            sort ?? new("metric", "desc"),
            limit,
            usePriorState);

    private sealed class FixedClock : IUtcClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class NoOpExecutor : IAnalyticsQueryExecutor
    {
        public Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken)
        {
            QueryExecutionResult result = new(
                [new Dictionary<string, object?> { ["metric"] = 123m }],
                new(compiledQuery.Sql, compiledQuery.Parameters, 1, 1, compiledQuery.EffectiveLimit, resolvedTimeRange.RangeStartUtc, resolvedTimeRange.RangeEndExclusiveUtc));
            return Task.FromResult(result);
        }
    }

    private sealed class AnalyticsApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUtcClock>();
                services.RemoveAll<IAnalyticsQueryExecutor>();
                services.AddSingleton<IUtcClock>(new FixedClock(new DateTimeOffset(2026, 03, 19, 12, 0, 0, TimeSpan.Zero)));
                services.AddScoped<IAnalyticsQueryExecutor, NoOpExecutor>();
            });
        }
    }
}
