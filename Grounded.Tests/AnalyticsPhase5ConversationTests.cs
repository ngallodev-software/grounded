using System.Net;
using System.Net.Http.Json;
using Grounded.Api.Models;
using Grounded.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Grounded.Tests;

public sealed class AnalyticsPhase5ConversationTests
{
    [Fact]
    public async Task SupportedFollowUp_ModifiesPriorStateDeterministically()
    {
        await using var factory = new Phase5ApiFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/analytics/query", new ExecuteAnalyticsQuestionRequest(
            "What was total revenue last month?",
            "conv-1"));
        var secondResponse = await client.PostAsJsonAsync("/analytics/query", new ExecuteAnalyticsQuestionRequest(
            "same thing by category",
            "conv-1"));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var body = await secondResponse.Content.ReadFromJsonAsync<ExecuteQueryPlanResponse>();
        Assert.NotNull(body);
        Assert.Equal("success", body!.Status);
        Assert.Equal("grouped_breakdown", body.Trace?.QueryPlan?.QuestionType);
        Assert.Equal("product_category", body.Trace?.QueryPlan?.Dimension);
        Assert.Equal("revenue", body.Trace?.QueryPlan?.Metric);
        Assert.Equal("not_requested", body.Trace?.PlannerStatus);
    }

    [Fact]
    public async Task UnsupportedFollowUp_RejectsDeterministically()
    {
        await using var factory = new Phase5ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/analytics/query", new ExecuteAnalyticsQuestionRequest(
            "what about just electronics?",
            "missing-conversation"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExecuteQueryPlanResponse>();
        Assert.NotNull(body);
        Assert.Equal("error", body!.Status);
        Assert.Equal(FailureCategories.UnsupportedRequest, body.FailureCategory);
        Assert.Contains(body.Errors!, error => error.Code == "unsupported_follow_up");
    }

    [Fact]
    public void PlannerPromptRenderer_FollowUpModeRemainsBounded()
    {
        var renderer = new PlannerPromptRenderer(new PromptStore(), new PlannerContextBuilder(new SqlFragmentRegistry()));
        var snapshot = new ConversationStateSnapshot(
            "aggregate",
            "revenue",
            null,
            [new("sales_channel", "eq", ["Web"])],
            new("last_month", null, null));

        var rendered = renderer.Render("same thing by category", snapshot);

        Assert.Contains("## Prior Conversation State", rendered.RenderedPrompt);
        Assert.Contains("\"questionType\": \"aggregate\"", rendered.RenderedPrompt);
        Assert.Contains("\"metric\": \"revenue\"", rendered.RenderedPrompt);
        Assert.DoesNotContain("all prior messages", rendered.RenderedPrompt, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Phase5ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
                services.AddSingleton<IUtcClock>(new FixedClock());
                services.AddSingleton<ILlmPlannerGateway, DeterministicLlmPlannerGateway>();
                services.AddSingleton<ILlmGateway, DeterministicLlmGateway>();
                services.AddSingleton<ITraceRepository, InMemoryTraceRepository>();
                services.AddSingleton<IEvalRepository, InMemoryEvalRepository>();
                services.AddSingleton<IConversationStateRepository, InMemoryConversationStateRepository>();
                services.AddScoped<IAnalyticsQueryExecutor, NoOpExecutor>();
            });
        }
    }

    private sealed class FixedClock : IUtcClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 03, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class NoOpExecutor : IAnalyticsQueryExecutor
    {
        public Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken)
        {
            QueryExecutionResult result = new(
                [new Dictionary<string, object?> { ["metric"] = 100m }],
                new(compiledQuery.Sql, compiledQuery.Parameters, 1, 1, compiledQuery.EffectiveLimit, resolvedTimeRange.RangeStartUtc, resolvedTimeRange.RangeEndExclusiveUtc));
            return Task.FromResult(result);
        }
    }
}
