using System.Net;
using System.Net.Http.Json;
using Grounded.Api.Models;
using Grounded.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Grounded.Tests;

public sealed class Phase4IntegrationTests
{
    [Fact]
    public async Task ExecuteQueryPlan_WithRealPlan_ReturnsSynthesizedAnswer()
    {
        await using var factory = new Phase4ApiFactory();
        using var client = factory.CreateClient();

        var request = new ExecuteQueryPlanRequest(CreateAggregatePlan(), "What was total revenue last month?");
        var response = await client.PostAsJsonAsync("/analytics/query-plan", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecuteQueryPlanResponse>();
        Assert.NotNull(body);
        Assert.Equal("success", body!.Status);
        Assert.NotNull(body.Answer);
        Assert.False(string.IsNullOrWhiteSpace(body.Answer!.Summary));
        Assert.NotEmpty(body.Answer.KeyPoints);
    }

    [Fact]
    public async Task ExecuteQueryPlan_WithEmptyRows_ReturnsFallbackSummary()
    {
        await using var factory = new Phase4ApiFactory(emptyRows: true);
        using var client = factory.CreateClient();

        var request = new ExecuteQueryPlanRequest(CreateAggregatePlan(), "What was total revenue last month?");
        var response = await client.PostAsJsonAsync("/analytics/query-plan", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecuteQueryPlanResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Answer);
        Assert.Equal("No data available for the requested query.", body.Answer!.Summary);
    }

    [Fact]
    public async Task RunEval_ReturnsEvalRunWithScores()
    {
        await using var factory = new Phase4ApiFactory(historyPath: TempHistoryPath());
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/analytics/eval", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EvalResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Run.CaseResults);
        Assert.All(body.Run.CaseResults, result => Assert.True(result.Score >= 0m));
    }

    [Fact]
    public async Task RunEval_Twice_ProducesRegressionComparison()
    {
        var historyPath = TempHistoryPath();
        await using var factory = new Phase4ApiFactory(historyPath: historyPath);
        using var client = factory.CreateClient();

        await client.PostAsync("/analytics/eval", null);
        var secondResponse = await client.PostAsync("/analytics/eval", null);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadFromJsonAsync<EvalResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Comparison);
    }

    private static QueryPlan CreateAggregatePlan() =>
        new(
            "1.0",
            "aggregate",
            Dimension: null,
            Filters: [],
            Metric: "revenue",
            TimeRange: new TimeRangeSpec("last_30_days", null, null),
            TimeGrain: null,
            Sort: new SortSpec("metric", "desc"),
            Limit: null,
            UsePriorState: false);

    private static string TempHistoryPath() =>
        Path.Combine(Path.GetTempPath(), $"regression_history_{Guid.NewGuid():N}.json");

    private sealed class FixedClock : IUtcClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 03, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class NumericRowExecutor : IAnalyticsQueryExecutor
    {
        public Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken)
        {
            var result = new QueryExecutionResult(
                [new Dictionary<string, object?> { ["metric"] = 500m }],
                new(compiledQuery.Sql, compiledQuery.Parameters, 1, 1, compiledQuery.EffectiveLimit, resolvedTimeRange.RangeStartUtc, resolvedTimeRange.RangeEndExclusiveUtc));
            return Task.FromResult(result);
        }
    }

    private sealed class EmptyRowExecutor : IAnalyticsQueryExecutor
    {
        public Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken)
        {
            var result = new QueryExecutionResult(
                [],
                new(compiledQuery.Sql, compiledQuery.Parameters, 0, 1, compiledQuery.EffectiveLimit, resolvedTimeRange.RangeStartUtc, resolvedTimeRange.RangeEndExclusiveUtc));
            return Task.FromResult(result);
        }
    }

    private sealed class Phase4ApiFactory : WebApplicationFactory<Program>
    {
        private readonly bool _emptyRows;
        private readonly string? _historyPath;

        public Phase4ApiFactory(bool emptyRows = false, string? historyPath = null)
        {
            _emptyRows = emptyRows;
            _historyPath = historyPath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            if (_historyPath is not null)
            {
                builder.ConfigureAppConfiguration(config =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Eval:HistoryPath"] = _historyPath
                    }));
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUtcClock>();
                services.RemoveAll<IAnalyticsQueryExecutor>();
                services.RemoveAll<ILlmPlannerGateway>();
                services.RemoveAll<ILlmGateway>();
                services.RemoveAll<ITraceRepository>();
                services.RemoveAll<IEvalRepository>();
                services.RemoveAll<IConversationStateRepository>();
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IUtcClock, FixedClock>();
                services.AddSingleton<ILlmPlannerGateway, DeterministicLlmPlannerGateway>();
                services.AddSingleton<ILlmGateway, DeterministicLlmGateway>();
                services.AddSingleton<ITraceRepository, InMemoryTraceRepository>();
                services.AddSingleton<IEvalRepository, InMemoryEvalRepository>();
                services.AddSingleton<IConversationStateRepository, InMemoryConversationStateRepository>();
                if (_emptyRows)
                {
                    services.AddScoped<IAnalyticsQueryExecutor, EmptyRowExecutor>();
                }
                else
                {
                    services.AddScoped<IAnalyticsQueryExecutor, NumericRowExecutor>();
                }
            });
        }
    }
}
