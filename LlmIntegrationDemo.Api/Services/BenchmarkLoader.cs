using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class BenchmarkLoader
{
    private readonly string _casesPath;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BenchmarkLoader(IConfiguration configuration)
    {
        var relativePath = configuration.GetValue<string>("Eval:BenchmarkCasesPath") ?? "eval/benchmark_cases.jsonl";
        var normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        _casesPath = ResolveCasesPath(normalizedRelative);
    }

    public IReadOnlyList<BenchmarkCase> LoadCases()
    {
        var cases = new List<BenchmarkCase>();
        foreach (var line in File.ReadLines(_casesPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var benchmarkCase = JsonSerializer.Deserialize<BenchmarkCase>(trimmed, _serializerOptions);
            if (benchmarkCase is not null)
            {
                cases.Add(benchmarkCase);
            }
        }

        if (!cases.Any())
        {
            throw new InvalidOperationException($"No benchmark cases were loaded from '{_casesPath}'.");
        }

        return cases;
    }

    private static string ResolveCasesPath(string relativePath)
    {
        string[] roots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var root in roots)
        {
            var candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Benchmark cases file '{relativePath}' was not found.");
    }
}
