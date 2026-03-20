using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class RegressionComparer
{
    private readonly string _historyPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public RegressionComparer(IConfiguration configuration)
    {
        var relativePath = configuration.GetValue<string>("Eval:HistoryPath") ?? "eval/regression_history.json";
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        _historyPath = Path.Combine(AppContext.BaseDirectory, normalized);
    }

    public RegressionComparisonResult CompareAndPersist(EvalRun currentRun)
    {
        var previousRun = LoadPreviousRun();
        PersistRun(currentRun);
        return GenerateComparison(previousRun, currentRun);
    }

    private EvalRun? LoadPreviousRun()
    {
        if (!File.Exists(_historyPath))
        {
            return null;
        }

        var content = File.ReadAllText(_historyPath);
        return JsonSerializer.Deserialize<EvalRun>(content, _serializerOptions);
    }

    private void PersistRun(EvalRun run)
    {
        var folder = Path.GetDirectoryName(_historyPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_historyPath, JsonSerializer.Serialize(run, _serializerOptions));
    }

    private RegressionComparisonResult GenerateComparison(EvalRun? previous, EvalRun current)
    {
        if (previous is null)
        {
            return new RegressionComparisonResult(false, 0m, Array.Empty<string>());
        }

        var notes = new List<string>();
        var hasRegression = false;

        foreach (var currentCase in current.CaseResults)
        {
            var previousCase = previous.CaseResults.FirstOrDefault(c => c.CaseId == currentCase.CaseId);
            if (previousCase is null)
            {
                notes.Add($"New benchmark case added: {currentCase.CaseId}.");
                continue;
            }

            if (previousCase.Passed && !currentCase.Passed)
            {
                hasRegression = true;
                notes.Add($"Case {currentCase.CaseId} regressed from pass to fail.");
            }

            if (!previousCase.Passed && currentCase.Passed)
            {
                notes.Add($"Case {currentCase.CaseId} recovered after last run.");
            }
        }

        var delta = current.Score - previous.Score;
        return new RegressionComparisonResult(hasRegression, Math.Round(delta, 3), notes);
    }
}
