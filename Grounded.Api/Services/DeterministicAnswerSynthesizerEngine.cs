using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class DeterministicAnswerSynthesizerEngine
{
    public AnswerSynthesizerResponse Build(AnswerSynthesizerRequest request)
    {
        var rows = request.Rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
        if (rows.Count == 0)
        {
            return new AnswerSynthesizerResponse(
                "No data available for the requested query.",
                Array.Empty<string>(),
                false);
        }

        var normalizedColumns = request.Columns.Any()
            ? request.Columns
            : rows.First().Keys.ToList();
        var metricColumn = DetermineMetricColumn(normalizedColumns, request);
        var dimensionColumn = DetermineDimensionColumn(normalizedColumns, metricColumn, request);
        var timeColumn = DetermineTimeColumn(normalizedColumns);

        var summary = BuildSummary(rows, metricColumn, dimensionColumn, timeColumn, request);
        var keyPoints = BuildKeyPoints(rows, metricColumn, dimensionColumn, timeColumn, request);
        var tableIncluded = rows.Count > 1;

        return new AnswerSynthesizerResponse(summary, keyPoints, tableIncluded);
    }

    private static string BuildSummary(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string metricColumn,
        string? dimensionColumn,
        string? timeColumn,
        AnswerSynthesizerRequest request)
    {
        var metricLabel = request.QueryPlan.Metric ?? metricColumn;
        var questionType = (request.QueryPlan.QuestionType ?? string.Empty).ToLowerInvariant();
        var topRow = rows.OrderByDescending(row => ConvertToDecimal(row[metricColumn])).FirstOrDefault();
        if (topRow is null)
        {
            return "The query returned no metrics to summarize.";
        }

        var topValue = topRow[metricColumn];
        var formattedTop = FormatMetric(topValue);
        var dimensionValue = dimensionColumn is null ? null : GetNormalizedValue(topRow, dimensionColumn);
        var timeValue = timeColumn is null ? null : GetNormalizedValue(topRow, timeColumn);

        if (rows.Count == 1 || questionType == "aggregate")
        {
            return $"{formattedTop} {metricLabel} for the selected context.";
        }

        if (dimensionColumn is not null && dimensionValue is not null)
        {
            return $"{dimensionValue} leads with {formattedTop} {metricLabel}.";
        }

        if (timeColumn is not null && timeValue is not null)
        {
            var lastRow = rows.Last();
            var lastTimeValue = GetNormalizedValue(lastRow, timeColumn);
            var lastMetric = FormatMetric(lastRow[metricColumn]);
            return $"Between {timeValue} and {lastTimeValue}, {metricLabel} changed from {formattedTop} to {lastMetric}.";
        }

        return $"Top {metricLabel} is {formattedTop}.";
    }

    private static IReadOnlyList<string> BuildKeyPoints(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string metricColumn,
        string? dimensionColumn,
        string? timeColumn,
        AnswerSynthesizerRequest request)
    {
        var points = new List<string>(capacity: 4);
        var metricLabel = request.QueryPlan.Metric ?? metricColumn;

        if (dimensionColumn is not null)
        {
            var top = rows.OrderByDescending(row => ConvertToDecimal(row[metricColumn])).First();
            var bottom = rows.OrderBy(row => ConvertToDecimal(row[metricColumn])).First();
            var topLabel = GetNormalizedValue(top, dimensionColumn);
            var bottomLabel = GetNormalizedValue(bottom, dimensionColumn);
            if (topLabel is not null)
            {
                points.Add($"{topLabel} has the highest {metricLabel} at {FormatMetric(top[metricColumn])}.");
            }

            if (bottomLabel is not null && !string.Equals(topLabel, bottomLabel, StringComparison.Ordinal))
            {
                points.Add($"{bottomLabel} has the lowest {metricLabel} at {FormatMetric(bottom[metricColumn])}.");
            }
        }

        if (timeColumn is not null && rows.Count > 1)
        {
            var ordered = rows
                .Select(row => (row, parsed: ParseDate(row, timeColumn)))
                .Where(pair => pair.parsed != null)
                .OrderBy(pair => pair.parsed!.Value)
                .ToList();

            if (ordered.Count >= 2)
            {
                var first = ordered.First();
                var last = ordered.Last();
                var firstMetric = FormatMetric(first.row[metricColumn]);
                var lastMetric = FormatMetric(last.row[metricColumn]);
                var firstDate = first.parsed!.Value;
                var lastDate = last.parsed!.Value;
                points.Add($"{timeColumn} starts at {firstDate:yyyy-MM-dd} with {metricLabel} {firstMetric}, ending at {lastDate:yyyy-MM-dd} with {metricLabel} {lastMetric}.");
            }
        }

        if (rows.Count == 1 && points.Count == 0)
        {
            points.Add($"Recorded {metricLabel} is {FormatMetric(rows[0][metricColumn])}.");
        }

        return points.Count > 5 ? points.Take(5).ToList() : points;
    }

    private static string DetermineMetricColumn(IReadOnlyList<string> columns, AnswerSynthesizerRequest request)
    {
        var preferredMetric = columns.FirstOrDefault(column => string.Equals(column, "metric", StringComparison.OrdinalIgnoreCase));
        if (preferredMetric is not null)
        {
            return preferredMetric;
        }

        if (!string.IsNullOrWhiteSpace(request.QueryPlan.Metric))
        {
            var match = columns.FirstOrDefault(column => string.Equals(column, request.QueryPlan.Metric, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var rows = request.Rows;
        foreach (var candidate in columns)
        {
            if (rows.Any(row => row.TryGetValue(candidate, out var val) && ConvertToDecimal(val) is not null))
            {
                return candidate;
            }
        }

        return columns.First();
    }

    private static string? DetermineDimensionColumn(
        IReadOnlyList<string> columns,
        string metricColumn,
        AnswerSynthesizerRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.QueryPlan.Dimension))
        {
            var match = columns.FirstOrDefault(column => string.Equals(column, request.QueryPlan.Dimension, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.Equals(match, metricColumn, StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }
        }

        return columns.FirstOrDefault(column =>
            !string.Equals(column, metricColumn, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(column, "time_bucket", StringComparison.OrdinalIgnoreCase));
    }

    private static string? DetermineTimeColumn(IReadOnlyList<string> columns)
    {
        return columns.FirstOrDefault(column =>
            string.Equals(column, "time_bucket", StringComparison.OrdinalIgnoreCase) || column.Contains("time", StringComparison.OrdinalIgnoreCase));
    }

    private static decimal? ConvertToDecimal(object? value)
    {
        if (value is decimal d)
        {
            return d;
        }

        if (value is double dbl)
        {
            return (decimal)dbl;
        }

        if (value is float fl)
        {
            return (decimal)fl;
        }

        if (value is long l)
        {
            return l;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is string text && decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string FormatMetric(object? value)
    {
        return value switch
        {
            decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
            double d => d.ToString("0.##", CultureInfo.InvariantCulture),
            float f => f.ToString("0.##", CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            string s => s,
            null => "0",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string? GetNormalizedValue(IReadOnlyDictionary<string, object?> row, string column)
    {
        if (!row.TryGetValue(column, out var value) || value is null)
        {
            return null;
        }

        return value.ToString();
    }

    private static DateTime? ParseDate(IReadOnlyDictionary<string, object?> row, string column)
    {
        if (!row.TryGetValue(column, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) => parsed,
            _ => null
        };
    }
}
