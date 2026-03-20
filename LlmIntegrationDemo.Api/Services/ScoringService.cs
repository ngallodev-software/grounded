using System;
using System.Collections.Generic;
using System.Linq;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class ScoringService
{
    public decimal ForCase(bool executionSuccess, bool answerMatches)
    {
        var score = 0m;
        if (executionSuccess)
        {
            score += 0.5m;
        }

        if (answerMatches)
        {
            score += 0.5m;
        }

        return Math.Min(1m, score);
    }

    public decimal Aggregate(IReadOnlyList<BenchmarkCaseResult> results)
    {
        if (results is null || results.Count == 0)
        {
            return 0m;
        }

        var average = results.Average(result => result.Score);
        return Math.Round(average, 3);
    }

    public bool IsPass(bool executionSuccess, bool answerMatches) => executionSuccess && answerMatches;
}
