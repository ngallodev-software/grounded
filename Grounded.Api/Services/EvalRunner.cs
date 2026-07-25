using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class EvalRunner
{
    private readonly BenchmarkLoader _benchmarkLoader;
    private readonly AnalyticsQueryPlanService _queryPlanService;
    private readonly ScoringService _scoringService;
    private readonly RegressionComparer _regressionComparer;
    private readonly PromptStore _promptStore;
    private readonly IEvalRepository _evalRepository;
    private readonly IConfiguration _configuration;

    public EvalRunner(
        BenchmarkLoader benchmarkLoader,
        AnalyticsQueryPlanService queryPlanService,
        ScoringService scoringService,
        RegressionComparer regressionComparer,
        PromptStore promptStore,
        IEvalRepository evalRepository,
        IConfiguration configuration)
    {
        _benchmarkLoader = benchmarkLoader;
        _queryPlanService = queryPlanService;
        _scoringService = scoringService;
        _regressionComparer = regressionComparer;
        _promptStore = promptStore;
        _evalRepository = evalRepository;
        _configuration = configuration;
    }

    public async Task<(EvalRun Run, RegressionComparisonResult Comparison)> RunAsync(CancellationToken cancellationToken)
    {
        var benchmarkCases = _benchmarkLoader.LoadCases();
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<BenchmarkCaseResult>();
        var providerStats = new Dictionary<(string Provider, string Stage), ProviderStatsAccumulator>();

        foreach (var benchmarkCase in benchmarkCases)
        {
            var executionSuccess = false;
            var structuralCorrectness = false;
            var answerGrounding = false;
            QueryExecutionMetadata? executionMetadata = null;
            QueryPlan? plannedQueryPlan = null;
            string? compiledSql = null;
            AnswerDto? answer = null;
            string? notes = null;
            string? failureCategory = null;
            long plannerLatencyMs = 0;
            long synthesisLatencyMs = 0;
            var totalTokensIn = 0;
            var totalTokensOut = 0;

            try
            {
                var serviceResult = await _queryPlanService.ExecuteFromQuestionAsync(
                    benchmarkCase.Question,
                    requestId: $"eval:{benchmarkCase.CaseId}",
                    cancellationToken);
                executionSuccess = serviceResult.IsSuccess;
                executionMetadata = serviceResult.Response.Metadata;
                compiledSql = executionMetadata?.CompiledSql;
                answer = serviceResult.Response.Answer;
                plannedQueryPlan = serviceResult.Response.Trace?.QueryPlan;
                failureCategory = serviceResult.Response.FailureCategory ?? serviceResult.Response.Trace?.FailureCategory;
                plannerLatencyMs = serviceResult.Response.Trace?.Planner?.LatencyMs ?? 0;
                synthesisLatencyMs = serviceResult.Response.Trace?.Synthesizer is null
                    ? 0
                    : Math.Max(0, (long)(serviceResult.Response.Trace.Synthesizer.RespondedAt - serviceResult.Response.Trace.Synthesizer.RequestedAt).TotalMilliseconds);
                totalTokensIn = (serviceResult.Response.Trace?.Planner?.TokensIn ?? 0) + (serviceResult.Response.Trace?.Synthesizer?.TokensIn ?? 0);
                totalTokensOut = (serviceResult.Response.Trace?.Planner?.TokensOut ?? 0) + (serviceResult.Response.Trace?.Synthesizer?.TokensOut ?? 0);
                Accumulate(providerStats, "planner", serviceResult.Response.Trace?.Planner);
                Accumulate(providerStats, "synthesizer", serviceResult.Response.Trace?.Synthesizer);

                if (answer is not null)
                {
                    // Summary is required; keyPoints are required for multi-row results but optional for single-value aggregates.
                    var rowCount = serviceResult.Response.Rows?.Count ?? 0;
                    structuralCorrectness = !string.IsNullOrWhiteSpace(answer.Summary) &&
                        (rowCount <= 1 || answer.KeyPoints is { Count: > 0 });
                    answerGrounding = IsAnswerGrounded(answer.Summary, serviceResult.Response.Rows);
                }
            }
            catch (Exception exception)
            {
                notes = exception.Message;
            }

            var score = _scoringService.ForCase(executionSuccess, structuralCorrectness, answerGrounding);
            var passed = _scoringService.IsPass(executionSuccess, structuralCorrectness);
            results.Add(new BenchmarkCaseResult(
                benchmarkCase.CaseId,
                benchmarkCase.Question,
                executionSuccess,
                structuralCorrectness,
                answerGrounding,
                passed,
                score,
                compiledSql,
                plannedQueryPlan,
                failureCategory,
                plannerLatencyMs,
                synthesisLatencyMs,
                totalTokensIn,
                totalTokensOut,
                notes,
                executionMetadata,
                answer));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var averageScore = _scoringService.Aggregate(results);
        var summary = _scoringService.BuildSummary(results);
        var aggregatedProviderStats = providerStats
            .OrderBy(static entry => entry.Key.Provider, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.Stage, StringComparer.Ordinal)
            .Select(static entry => entry.Value.ToStats(entry.Key.Provider, entry.Key.Stage))
            .ToArray();
        var plannerPromptVersion = _configuration["GROUNDED_PLANNER_PROMPT_VERSION"] ?? "v2";
        var plannerPrompt = _promptStore.GetVersionedPrompt("planner", plannerPromptVersion);
        var prompt = _promptStore.GetVersionedPrompt("answer-synthesizer", "v1");
        var run = new EvalRun(
            Guid.NewGuid().ToString("D"),
            startedAt,
            completedAt,
            $"{plannerPrompt.PromptKey}/{plannerPrompt.Version}:{plannerPrompt.Checksum}",
            $"{prompt.PromptKey}/{prompt.Version}:{prompt.Checksum}",
            averageScore,
            summary,
            aggregatedProviderStats,
            results);

        var comparison = _regressionComparer.CompareAndPersist(run);
        await _evalRepository.PersistAsync(
            new PersistedEvalRun(
                run.RunId,
                run.StartedAt,
                run.CompletedAt,
                run.PlannerPromptVersion,
                run.SynthesizerPromptVersion,
                run.Score,
                run.ProviderStats,
                run.CaseResults,
                comparison),
            cancellationToken);
        return (run, comparison);
    }

    private static void Accumulate(IDictionary<(string Provider, string Stage), ProviderStatsAccumulator> stats, string stage, PlannerTrace? trace)
    {
        if (trace is null)
        {
            return;
        }

        var key = (trace.Provider, stage);
        if (!stats.TryGetValue(key, out var accumulator))
        {
            accumulator = new ProviderStatsAccumulator();
            stats[key] = accumulator;
        }

        accumulator.Add(trace.FailureCategory == FailureCategories.None, trace.RateLimited, trace.RetryCount, trace.QueueWaitMs, trace.RetryDelayMs, trace.EstimatedInputTokens, trace.TokensIn, trace.TokensOut);
    }

    private static void Accumulate(IDictionary<(string Provider, string Stage), ProviderStatsAccumulator> stats, string stage, SynthesizerTrace? trace)
    {
        if (trace is null)
        {
            return;
        }

        var key = (trace.Provider, stage);
        if (!stats.TryGetValue(key, out var accumulator))
        {
            accumulator = new ProviderStatsAccumulator();
            stats[key] = accumulator;
        }

        accumulator.Add(trace.FailureCategory == FailureCategories.None, trace.RateLimited, trace.RetryCount, trace.QueueWaitMs, trace.RetryDelayMs, trace.EstimatedInputTokens, trace.TokensIn, trace.TokensOut);
    }

    private static bool IsAnswerGrounded(string summary, IReadOnlyList<IReadOnlyDictionary<string, object?>>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return false;
        }

        foreach (var row in rows)
        {
            foreach (var value in row.Values)
            {
                if (value is null)
                {
                    continue;
                }

                var leafValue = value.ToString();
                if (!string.IsNullOrEmpty(leafValue) && summary.Contains(leafValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class ProviderStatsAccumulator
    {
        public int RequestCount { get; private set; }
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public int RateLimitedCount { get; private set; }
        public int RetryCount { get; private set; }
        public long TotalQueueWaitMs { get; private set; }
        public long TotalRetryDelayMs { get; private set; }
        public int TotalEstimatedInputTokens { get; private set; }
        public int TotalTokensIn { get; private set; }
        public int TotalTokensOut { get; private set; }

        public void Add(bool success, bool rateLimited, int retryCount, long queueWaitMs, long retryDelayMs, int estimatedInputTokens, int tokensIn, int tokensOut)
        {
            RequestCount++;
            if (success)
            {
                SuccessCount++;
            }
            else
            {
                FailureCount++;
            }

            if (rateLimited)
            {
                RateLimitedCount++;
            }

            RetryCount += retryCount;
            TotalQueueWaitMs += queueWaitMs;
            TotalRetryDelayMs += retryDelayMs;
            TotalEstimatedInputTokens += estimatedInputTokens;
            TotalTokensIn += tokensIn;
            TotalTokensOut += tokensOut;
        }

        public EvalProviderStats ToStats(string provider, string stage) =>
            new(
                provider,
                stage,
                RequestCount,
                SuccessCount,
                FailureCount,
                RateLimitedCount,
                RetryCount,
                TotalQueueWaitMs,
                TotalRetryDelayMs,
                TotalEstimatedInputTokens,
                TotalTokensIn,
                TotalTokensOut);
    }
}
