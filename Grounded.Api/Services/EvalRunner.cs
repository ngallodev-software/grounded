using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public EvalRunner(
        BenchmarkLoader benchmarkLoader,
        AnalyticsQueryPlanService queryPlanService,
        ScoringService scoringService,
        RegressionComparer regressionComparer,
        PromptStore promptStore,
        IEvalRepository evalRepository)
    {
        _benchmarkLoader = benchmarkLoader;
        _queryPlanService = queryPlanService;
        _scoringService = scoringService;
        _regressionComparer = regressionComparer;
        _promptStore = promptStore;
        _evalRepository = evalRepository;
    }

    public async Task<(EvalRun Run, RegressionComparisonResult Comparison)> RunAsync(CancellationToken cancellationToken)
    {
        var benchmarkCases = _benchmarkLoader.LoadCases();
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<BenchmarkCaseResult>();

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

                if (answer is not null)
                {
                    structuralCorrectness = !string.IsNullOrWhiteSpace(answer.Summary) && answer.KeyPoints is { Count: > 0 };
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
        var plannerPrompt = _promptStore.GetVersionedPrompt("planner", "v1");
        var prompt = _promptStore.GetVersionedPrompt("answer-synthesizer", "v1");
        var run = new EvalRun(
            Guid.NewGuid().ToString("D"),
            startedAt,
            completedAt,
            $"{plannerPrompt.PromptKey}/{plannerPrompt.Version}:{plannerPrompt.Checksum}",
            $"{prompt.PromptKey}/{prompt.Version}:{prompt.Checksum}",
            averageScore,
            summary,
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
                run.CaseResults,
                comparison),
            cancellationToken);
        return (run, comparison);
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
}
