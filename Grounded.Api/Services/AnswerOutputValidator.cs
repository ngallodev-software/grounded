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
            .SelectMany(FormatVariants)
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

    // Yields multiple representations of a value so the grounding check matches
    // LLM-formatted output (e.g. "$16,993,824.67" or "16.99M") against raw row values.
    private static IEnumerable<string> FormatVariants(object? value)
    {
        switch (value)
        {
            case null:
                yield break;
            case DateTimeOffset dto:
                yield return dto.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                yield break;
            case DateTime dt:
                yield return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                yield break;
            case decimal or double or float:
            {
                var d = Convert.ToDecimal(value);
                // Raw unformatted: "16993824.67"
                yield return d.ToString("0.##", CultureInfo.InvariantCulture);
                // With thousands separator: "16,993,824.67" — matches LLM currency formatting
                yield return d.ToString("N2", CultureInfo.InvariantCulture);
                // Integer form (for whole-number metrics): "16993825"
                var rounded = Math.Round(d, 0);
                yield return rounded.ToString("0", CultureInfo.InvariantCulture);
                yield break;
            }
            default:
                yield return value.ToString() ?? string.Empty;
                break;
        }
    }
}
