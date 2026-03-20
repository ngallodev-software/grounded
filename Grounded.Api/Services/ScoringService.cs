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
}
