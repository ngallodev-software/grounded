using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

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
            var answerMatches = false;
            QueryExecutionMetadata? executionMetadata = null;
            AnswerDto? answer = null;
            string? notes = null;

            try
            {
                var serviceResult = await _queryPlanService.ExecuteAsync(benchmarkCase.QueryPlan, benchmarkCase.Question, cancellationToken);
                executionSuccess = serviceResult.IsSuccess;
                executionMetadata = serviceResult.Response.Metadata;
                answer = serviceResult.Response.Answer;
                if (!string.IsNullOrWhiteSpace(benchmarkCase.ExpectedAnswer) && answer is not null)
                {
                    answerMatches = string.Equals(answer.Summary.Trim(), benchmarkCase.ExpectedAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception exception)
            {
                notes = exception.Message;
            }

            var score = _scoringService.ForCase(executionSuccess, answerMatches);
            var passed = _scoringService.IsPass(executionSuccess, answerMatches);
            results.Add(new BenchmarkCaseResult(
                benchmarkCase.CaseId,
                benchmarkCase.Question,
                executionSuccess,
                answerMatches,
                passed,
                score,
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
}
