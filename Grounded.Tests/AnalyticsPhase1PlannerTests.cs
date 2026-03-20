using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

public sealed class AnalyticsPhase1PlannerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 03, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidAggregateQuestion_ProducesSuccessfulExecutionAndPlannerTrace()
    {
        var plannerResult = PlannerSuccess(CreatePlan(
            questionType: "aggregate",
            metric: "revenue",
            timeRange: new("last_month", null, null)));
        var service = CreateService(new StubPlannerGateway(plannerResult));

        var result = await service.ExecuteFromQuestionAsync("What was total revenue last month?", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response.Trace);
        Assert.NotNull(result.Response.Trace!.Planner);
        Assert.True(result.Response.Trace.Planner!.ParseSucceeded);
        Assert.Equal("planner", result.Response.Trace.Planner.PromptKey);
        Assert.Equal("v1", result.Response.Trace.Planner.PromptVersion);
    }

    [Fact]
    public async Task ValidRankingQuestion_ProducesDistinctPlanShape()
    {
        var plannerResult = PlannerSuccess(CreatePlan(
            questionType: "ranking",
            metric: "units_sold",
            dimension: "product_name",
            limit: 5));
        var service = CreateService(new StubPlannerGateway(plannerResult));

        var result = await service.ExecuteFromQuestionAsync("Top 5 products by units sold this year.", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ranking", result.Response.Trace?.QueryPlan?.QuestionType);
        Assert.Equal("product_name", result.Response.Trace?.QueryPlan?.Dimension);
        Assert.Equal(5, result.Response.Trace?.QueryPlan?.Limit);
    }

    [Fact]
    public void MalformedJson_RepairSucceeds()
    {
        var parser = new PlannerResponseParser();
        var repairService = new PlannerResponseRepairService(parser);
        var malformed = """
            ```json
            {"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
            ```
            """;

        var result = repairService.TryRepair(parser.Parse(malformed));

        Assert.True(result.IsSuccess);
        Assert.True(result.RepairAttempted);
        Assert.True(result.RepairSucceeded);
        Assert.NotNull(result.RepairedContent);
        Assert.Equal("aggregate", result.QueryPlan?.QuestionType);
    }

    [Fact]
    public void MalformedJson_RepairFailsCleanly()
    {
        var parser = new PlannerResponseParser();
        var repairService = new PlannerResponseRepairService(parser);
        var malformed = "{\"version\":\"1.0\",\"questionType\":\"aggregate\"";

        var result = repairService.TryRepair(parser.Parse(malformed));

        Assert.False(result.IsSuccess);
        Assert.True(result.RepairAttempted);
        Assert.False(result.RepairSucceeded);
        Assert.Equal(FailureCategories.JsonParseFailure, result.FailureCategory);
    }

    [Fact]
    public async Task UnsupportedQuestion_MapsToUnsupportedRequest()
    {
        var plannerResult = PlannerSuccess(CreatePlan(questionType: "simple_follow_up"));
        var service = CreateService(new StubPlannerGateway(plannerResult));

        var result = await service.ExecuteFromQuestionAsync("Continue the previous analysis.", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Response.Trace?.Planner);
        Assert.Equal(FailureCategories.UnsupportedRequest, result.Response.Trace!.Planner!.FailureCategory);
        Assert.Contains(result.Response.Errors!, error => error.Code == "unsupported_question_type");
    }

    [Fact]
    public async Task PlannerTimeout_MapsToDeterministicFailureCategory()
    {
        var trace = CreateTrace(parseSucceeded: false, failureCategory: FailureCategories.Timeout, failureMessage: "planner timed out");
        var service = CreateService(new StubPlannerGateway(new PlannerGatewayResult(
            false,
            null,
            trace,
            CreateAttempt(trace, null),
            [new ValidationErrorDto(FailureCategories.Timeout, "planner timed out")])));

        var result = await service.ExecuteFromQuestionAsync("What was total revenue last month?", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Response.Trace?.Planner);
        Assert.Equal(FailureCategories.Timeout, result.Response.Trace!.Planner!.FailureCategory);
        Assert.Contains(result.Response.Errors!, error => error.Code == FailureCategories.Timeout);
    }

    [Fact]
    public async Task UnsupportedMetricSentinel_MapsToDeterministicPlannerValidationFailure()
    {
        var plannerResult = PlannerSuccess(CreateUnsupportedSentinelPlan());
        var service = CreateService(new StubPlannerGateway(plannerResult));

        var result = await service.ExecuteFromQuestionAsync("Show gross margin by channel last month.", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureCategories.PlannerValidationFailure, result.Response.FailureCategory);
        Assert.Equal("__unsupported__", result.Response.Trace?.QueryPlan?.Metric);
        Assert.Contains(result.Response.Errors!, error => error.Code == "invalid_metric");
    }

    [Fact]
    public void PlannerContextBuilder_ExcludesSimpleFollowUpAndIncludesSentinelExamples()
    {
        var context = new PlannerContextBuilder(new SqlFragmentRegistry()).Build();

        Assert.DoesNotContain("simple_follow_up", context.SupportedQuestionTypes);
        Assert.Contains(context.Examples, example =>
            example.Question == "Top products by units sold this year." &&
            example.Plan.Metric == "__unsupported__");
        Assert.Contains(context.Examples, example =>
            example.Question == "SELECT product_name, SUM(quantity) FROM order_items GROUP BY product_name;" &&
            example.Plan.Metric == "__unsupported__");
        Assert.Contains(context.Examples, example =>
            example.Question == "Show revenue by product category where country is Canada." &&
            example.Plan.Metric == "__unsupported__");
        Assert.Contains(context.Examples, example =>
            example.Question == "Show revenue by product category where sales channel is Retail." &&
            example.Plan.Metric == "__unsupported__");
    }

    [Fact]
    public void PlannerPrompt_IncludesCanonicalUnsupportedInstructionsAndExamples()
    {
        var prompt = new PromptStore().GetVersionedPrompt("planner", "v1").Content;

        Assert.Contains("Return exactly one JSON object matching the `QueryPlan` contract.", prompt);
        Assert.Contains("Do not generate SQL.", prompt);
        Assert.Contains("metric = \"__unsupported__\"", prompt);
        Assert.Contains("Question: Show sales by region.", prompt);
        Assert.Contains("Question: SELECT product_name, SUM(quantity) FROM order_items GROUP BY product_name;", prompt);
        Assert.Contains("Question: Top products by units sold this year.", prompt);
        Assert.Contains("Question: Show revenue by product category where country is Canada.", prompt);
        Assert.Contains("Question: Show revenue by product category where sales channel is Retail.", prompt);
    }

    private static AnalyticsQueryPlanService CreateService(ILlmPlannerGateway plannerGateway)
    {
        var promptStore = new PromptStore();
        var engine = new DeterministicAnswerSynthesizerEngine();
        var resolver = new ModelInvokerResolver([new DeterministicModelInvoker(engine)]);
        var llmGateway = new DeterministicLlmGateway(resolver);
        var answerSynthesizer = new AnswerSynthesizer(promptStore, llmGateway, new AnswerOutputValidator());

        return new AnalyticsQueryPlanService(
            new QueryPlanValidator(),
            new TimeRangeResolver(new FixedClock(FixedNow)),
            new QueryPlanCompiler(new SqlFragmentRegistry()),
            new SqlSafetyGuard(),
            new NoOpExecutor(),
            answerSynthesizer,
            plannerGateway,
            new InMemoryTraceRepository(),
            new ConversationStateService(new InMemoryConversationStateRepository()));
    }

    private static PlannerGatewayResult PlannerSuccess(QueryPlan queryPlan) =>
        new(true, queryPlan, CreateTrace(parseSucceeded: true, failureCategory: FailureCategories.None, failureMessage: null), CreateAttempt(CreateTrace(parseSucceeded: true, failureCategory: FailureCategories.None, failureMessage: null), queryPlan), null);

    private static PlannerTrace CreateTrace(bool parseSucceeded, string failureCategory, string? failureMessage) =>
        new(
            "planner",
            "v1",
            "checksum",
            "test_provider",
            "test_model",
            FixedNow,
            FixedNow.AddMilliseconds(25),
            25,
            42,
            17,
            parseSucceeded,
            false,
            false,
            failureCategory,
            failureMessage);

    private static PersistedPlannerAttempt CreateAttempt(PlannerTrace trace, QueryPlan? queryPlan) =>
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
            queryPlan is null ? null : System.Text.Json.JsonSerializer.Serialize(queryPlan));

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

    private static QueryPlan CreateUnsupportedSentinelPlan() =>
        new(
            "1.0",
            "aggregate",
            null,
            [],
            "__unsupported__",
            new("last_30_days", null, null),
            null,
            new("metric", "desc"),
            null,
            false);

    private sealed class StubPlannerGateway : ILlmPlannerGateway
    {
        private readonly PlannerGatewayResult _result;

        public StubPlannerGateway(PlannerGatewayResult result)
        {
            _result = result;
        }

        public Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_result);
        }
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
