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

    public EvalRunner(
        BenchmarkLoader benchmarkLoader,
        AnalyticsQueryPlanService queryPlanService,
        ScoringService scoringService,
        RegressionComparer regressionComparer,
        PromptStore promptStore)
    {
        _benchmarkLoader = benchmarkLoader;
        _queryPlanService = queryPlanService;
        _scoringService = scoringService;
        _regressionComparer = regressionComparer;
        _promptStore = promptStore;
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

            try
            {
                var serviceResult = await _queryPlanService.ExecuteFromQuestionAsync(benchmarkCase.Question, cancellationToken);
                executionSuccess = serviceResult.IsSuccess;
                executionMetadata = serviceResult.Response.Metadata;
                compiledSql = executionMetadata?.CompiledSql;
                answer = serviceResult.Response.Answer;
                plannedQueryPlan = serviceResult.Response.Trace?.QueryPlan;

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
                notes,
                executionMetadata,
                answer));
        }

        var completedAt = DateTimeOffset.UtcNow;
        var averageScore = _scoringService.Aggregate(results);
        var prompt = _promptStore.GetPrompt("answer-synthesizer/v1.md");
        var run = new EvalRun(
            Guid.NewGuid().ToString("D"),
            startedAt,
            completedAt,
            "planner:v1",
            prompt.Checksum,
            averageScore,
            results);

        var comparison = _regressionComparer.CompareAndPersist(run);
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
