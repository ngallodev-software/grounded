using Microsoft.Extensions.Configuration;
using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

public sealed class AnalyticsPhase3ReplayTests
{
    [Fact]
    public void BenchmarkLoader_LoadsExpandedCorpus()
    {
        var config = new ConfigurationBuilder().Build();
        var loader = new BenchmarkLoader(config);

        var cases = loader.LoadCases();

        Assert.True(cases.Count >= 30);
        Assert.Contains(cases, benchmarkCase => benchmarkCase.Category == "adversarial");
        Assert.Contains(cases, benchmarkCase => benchmarkCase.Category == "rejected_follow_up");
    }

    [Fact]
    public async Task ReplayInvoker_ReturnsFixtureWithoutNetwork()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Eval:ReplayFixturesPath"] = "eval/replay_fixtures.json"
            })
            .Build();
        var invoker = new ReplayModelInvoker(config);

        var result = await invoker.InvokeAsync(
            new ModelRequest(
                "replay",
                "Question: What was total revenue last month?",
                null,
                "planner",
                "v1",
                "checksum",
                "unused",
                "unused"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Contains("\"questionType\":\"aggregate\"", result.Response!.Content);
    }

    [Fact]
    public async Task ReplayAnswerGateway_ReturnsStructuredAnswer()
    {
        var previousReplay = Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE");
        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", "true");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Eval:ReplayFixturesPath"] = "eval/replay_fixtures.json"
                })
                .Build();
            var resolver = new ModelInvokerResolver([new ReplayModelInvoker(config)]);
            var gateway = new OpenAiCompatibleAnswerGateway(resolver);
            var prompt = new PromptStore().GetVersionedPrompt("answer-synthesizer", "v1");
            var request = new AnswerSynthesizerRequest(
                "What was revenue?",
                new QueryPlan("1.0", "aggregate", null, [], "revenue", new("last_month", null, null), null, new("metric", "desc"), null, false),
                [new Dictionary<string, object?> { ["metric"] = 123m }],
                ["metric"],
                null,
                prompt.Checksum);

            var response = await gateway.SendAnswerRequestAsync(prompt, request, CancellationToken.None);

            Assert.Contains("\"summary\":\"Revenue was 123.\"", response.Content);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", previousReplay);
        }
    }

    [Fact]
    public void RegressionComparer_WithExpandedRun_ProducesComparison()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var historyPath = Path.Combine(tempDir.FullName, "history.json");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Eval:HistoryPath"] = historyPath
                })
                .Build();
            var comparer = new RegressionComparer(config);
            var summary = new EvalRunSummary(1m, 1m, 1m, 10m, 20m, 15m, new Dictionary<string, int>());
            var firstRun = new EvalRun("run-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "planner:v1", "answer:v1", 0.8m, summary, [CreateResult("case-1", true)]);
            var secondRun = new EvalRun("run-2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "planner:v1", "answer:v1", 0.7m, summary, [CreateResult("case-1", false)]);

            comparer.CompareAndPersist(firstRun);
            var comparison = comparer.CompareAndPersist(secondRun);

            Assert.True(comparison.HasRegression);
            Assert.NotEmpty(comparison.Notes);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static BenchmarkCaseResult CreateResult(string caseId, bool passed) =>
        new(
            caseId,
            "Question",
            passed,
            passed,
            passed,
            passed,
            passed ? 1m : 0m,
            "SELECT 1",
            null,
            passed ? FailureCategories.None : FailureCategories.PlannerValidationFailure,
            5,
            5,
            10,
            8,
            null,
            null,
            null);
}
