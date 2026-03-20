using Microsoft.Extensions.Configuration;
using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

public sealed class AnalyticsPhase2TraceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 03, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FailedParse_PersistsCategorizedTrace()
    {
        var traceRepository = new InMemoryTraceRepository();
        var plannerTrace = CreatePlannerTrace(FailureCategories.JsonParseFailure, parseSucceeded: false, failureMessage: "invalid JSON");
        var service = CreateService(
            new StubPlannerGateway(new PlannerGatewayResult(
                false,
                null,
                plannerTrace,
                CreateAttempt(plannerTrace, null),
                [new ValidationErrorDto(FailureCategories.JsonParseFailure, "invalid JSON")])),
            new PassThroughLlmGateway(),
            traceRepository);

        var result = await service.ExecuteFromQuestionAsync("bad question", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(traceRepository.Items);
        Assert.Equal(FailureCategories.JsonParseFailure, traceRepository.Items[0].FailureCategory);
        Assert.Equal("error", traceRepository.Items[0].FinalStatus);
    }

    [Fact]
    public async Task Timeout_PersistsCategorizedTrace()
    {
        var traceRepository = new InMemoryTraceRepository();
        var plannerTrace = CreatePlannerTrace(FailureCategories.Timeout, parseSucceeded: false, failureMessage: "timed out");
        var service = CreateService(
            new StubPlannerGateway(new PlannerGatewayResult(
                false,
                null,
                plannerTrace,
                CreateAttempt(plannerTrace, null),
                [new ValidationErrorDto(FailureCategories.Timeout, "timed out")])),
            new PassThroughLlmGateway(),
            traceRepository);

        var result = await service.ExecuteFromQuestionAsync("slow question", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(traceRepository.Items);
        Assert.Equal(FailureCategories.Timeout, traceRepository.Items[0].FailureCategory);
    }

    [Fact]
    public async Task SynthesisFailure_SurfacesInResponseAndTrace()
    {
        var traceRepository = new InMemoryTraceRepository();
        var service = CreateService(
            new StubPlannerGateway(new PlannerGatewayResult(
                true,
                CreatePlan(),
                CreatePlannerTrace(FailureCategories.None, parseSucceeded: true, failureMessage: null),
                CreateAttempt(CreatePlannerTrace(FailureCategories.None, parseSucceeded: true, failureMessage: null), CreatePlan()),
                null)),
            new InvalidJsonLlmGateway(),
            traceRepository);

        var result = await service.ExecuteFromQuestionAsync("valid question", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FailureCategories.SynthesisFailure, result.Response.FailureCategory);
        Assert.NotNull(result.Response.Trace?.Synthesizer);
        Assert.Equal("failed", result.Response.Trace!.SynthesisStatus);
        Assert.Equal("partial_success", result.Response.Trace.FinalStatus);
        Assert.Single(traceRepository.Items);
        Assert.Equal(FailureCategories.SynthesisFailure, traceRepository.Items[0].FailureCategory);
        Assert.NotNull(traceRepository.Items[0].SynthesisAttempt);
    }

    [Fact]
    public async Task EvalRun_PersistsToRepository()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var casesPath = Path.Combine(tempDir.FullName, "cases.jsonl");
            await File.WriteAllTextAsync(casesPath, """{"caseId":"case-1","category":"aggregate","question":"What was revenue last month?"}""" + Environment.NewLine);
            var historyPath = Path.Combine(tempDir.FullName, "history.json");
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Eval:BenchmarkCasesPath"] = casesPath,
                    ["Eval:HistoryPath"] = historyPath
                })
                .Build();

            var evalRepository = new InMemoryEvalRepository();
            var traceRepository = new InMemoryTraceRepository();
            var service = CreateService(
                new StubPlannerGateway(new PlannerGatewayResult(
                    true,
                    CreatePlan(),
                    CreatePlannerTrace(FailureCategories.None, parseSucceeded: true, failureMessage: null),
                    CreateAttempt(CreatePlannerTrace(FailureCategories.None, parseSucceeded: true, failureMessage: null), CreatePlan()),
                    null)),
                new PassThroughLlmGateway(),
                traceRepository);
            var runner = new EvalRunner(
                new BenchmarkLoader(config),
                service,
                new ScoringService(),
                new RegressionComparer(config),
                new PromptStore(),
                evalRepository);

            var (run, _) = await runner.RunAsync(CancellationToken.None);

            Assert.Single(evalRepository.Items);
            Assert.Equal(run.RunId, evalRepository.Items[0].RunId);
            Assert.NotEmpty(evalRepository.Items[0].CaseResults);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static AnalyticsQueryPlanService CreateService(ILlmPlannerGateway plannerGateway, ILlmGateway llmGateway, InMemoryTraceRepository traceRepository)
    {
        var promptStore = new PromptStore();
        var answerSynthesizer = new AnswerSynthesizer(promptStore, llmGateway, new AnswerOutputValidator());
        return new AnalyticsQueryPlanService(
            new QueryPlanValidator(),
            new TimeRangeResolver(new FixedClock(FixedNow)),
            new QueryPlanCompiler(new SqlFragmentRegistry()),
            new SqlSafetyGuard(),
            new NoOpExecutor(),
            answerSynthesizer,
            plannerGateway,
            traceRepository,
            new ConversationStateService(new InMemoryConversationStateRepository()));
    }

    private static QueryPlan CreatePlan() =>
        new("1.0", "aggregate", null, [], "revenue", new("last_30_days", null, null), null, new("metric", "desc"), null, false);

    private static PlannerTrace CreatePlannerTrace(string failureCategory, bool parseSucceeded, string? failureMessage) =>
        new(
            "planner",
            "v1",
            "checksum",
            "provider",
            "model",
            FixedNow,
            FixedNow.AddMilliseconds(10),
            10,
            10,
            5,
            parseSucceeded,
            false,
            false,
            failureCategory,
            failureMessage);

    private static PersistedPlannerAttempt CreateAttempt(PlannerTrace trace, QueryPlan? plan) =>
        new(
            trace.PromptKey,
            trace.PromptVersion,
            trace.PromptChecksum,
            trace.Provider,
            trace.ModelName,
            trace.RequestedAt,
            trace.RespondedAt,
            trace.LatencyMs,
            trace.TokensIn,
            trace.TokensOut,
            trace.ParseSucceeded,
            trace.RepairAttempted,
            trace.RepairSucceeded,
            trace.FailureCategory,
            trace.FailureMessage,
            null,
            null,
            plan is null ? null : System.Text.Json.JsonSerializer.Serialize(plan));

    private sealed class StubPlannerGateway : ILlmPlannerGateway
    {
        private readonly PlannerGatewayResult _result;

        public StubPlannerGateway(PlannerGatewayResult result)
        {
            _result = result;
        }

        public Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken) => Task.FromResult(_result);
    }

    private sealed class PassThroughLlmGateway : ILlmGateway
    {
        public Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken)
        {
            var content = """{"summary":"Revenue was 123","keyPoints":["123"],"tableIncluded":false}""";
            return Task.FromResult(new LlmAnswerResponse(content, "deterministic-local", 10, 5, FixedNow, FixedNow));
        }
    }

    private sealed class InvalidJsonLlmGateway : ILlmGateway
    {
        public Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new LlmAnswerResponse("{not-json", "deterministic-local", 10, 5, FixedNow, FixedNow));
    }

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
}
