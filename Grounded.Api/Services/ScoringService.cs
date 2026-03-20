using System;
using System.Collections.Generic;
using System.Linq;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class ScoringService
{
    public decimal ForCase(bool executionSuccess, bool structuralCorrectness, bool answerGrounding)
    {
        var score = 0m;
        if (executionSuccess)
        {
            score += 0.5m;
        }

        if (structuralCorrectness)
        {
            score += 0.3m;
        }

        if (answerGrounding)
        {
            score += 0.2m;
        }

        return Math.Min(1m, score);
    }

    public decimal Aggregate(IReadOnlyList<BenchmarkCaseResult> results)
    {
        if (results is null || results.Count == 0)
        {
            return 0m;
        }

        var average = results.Average(result => (double)result.Score);
        return Math.Round((decimal)average, 3);
    }

    public bool IsPass(bool executionSuccess, bool structuralCorrectness) => executionSuccess && structuralCorrectness;

    public EvalRunSummary BuildSummary(IReadOnlyList<BenchmarkCaseResult> results)
    {
        if (results.Count == 0)
        {
            return new EvalRunSummary(0m, 0m, 0m, 0m, 0m, 0m, new Dictionary<string, int>(StringComparer.Ordinal));
        }

        var failureCounts = results
            .Where(result => !string.IsNullOrWhiteSpace(result.FailureCategory) && !string.Equals(result.FailureCategory, FailureCategories.None, StringComparison.Ordinal))
            .GroupBy(result => result.FailureCategory!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new EvalRunSummary(
            PlannerValidityRate: Math.Round((decimal)results.Count(result => result.PlannedQueryPlan is not null) / results.Count, 3),
            ExecutionSuccessRate: Math.Round((decimal)results.Count(result => result.ExecutionSuccess) / results.Count, 3),
            GroundingRate: Math.Round((decimal)results.Count(result => result.AnswerGrounding) / results.Count, 3),
            AverageLatencyMs: Math.Round((decimal)results.Average(result => result.PlannerLatencyMs + result.SynthesisLatencyMs), 2),
            AverageTokensIn: Math.Round((decimal)results.Average(result => result.TotalTokensIn), 2),
            AverageTokensOut: Math.Round((decimal)results.Average(result => result.TotalTokensOut), 2),
            FailureCounts: failureCounts);
    }
}
