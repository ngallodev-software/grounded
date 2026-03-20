using System;
using System.Globalization;
using System.Linq;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class AnswerOutputValidator
{
    public void Validate(AnswerSynthesizerResponse response, AnswerSynthesizerRequest request)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (string.IsNullOrWhiteSpace(response.Summary))
        {
            throw new InvalidOperationException("The synthesized answer must include a summary.");
        }

        if (response.KeyPoints is null)
        {
            throw new InvalidOperationException("The synthesized answer must include keyPoints.");
        }

        if (response.KeyPoints.Count > 5)
        {
            throw new InvalidOperationException("The synthesized answer may include at most 5 key points.");
        }

        var rows = request.Rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
        if (rows.Count == 0)
        {
            return;
        }

        var visibleValues = rows
            .SelectMany(row => row.Values)
            .Where(value => value is not null)
            .Select(FormatValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!ContainsVisibleValue(response.Summary, visibleValues) &&
            !response.KeyPoints.Any(point => ContainsVisibleValue(point, visibleValues)))
        {
            throw new InvalidOperationException("The synthesized answer must reference at least one value present in the result rows.");
        }
    }

    private static bool ContainsVisibleValue(string text, IReadOnlyList<string> visibleValues)
    {
        return visibleValues.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            float number => number.ToString("0.##", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
}
