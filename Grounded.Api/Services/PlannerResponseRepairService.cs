using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class PlannerResponseRepairService
{
    private readonly PlannerResponseParser _parser;

    public PlannerResponseRepairService(PlannerResponseParser parser)
    {
        _parser = parser;
    }

    public PlannerParseResult TryRepair(PlannerParseResult parseResult)
    {
        if (parseResult.IsSuccess)
        {
            return parseResult;
        }

        var repairedContent = ExtractJsonObject(parseResult.OriginalContent);
        if (repairedContent is null)
        {
            return parseResult with { RepairAttempted = true };
        }

        var repairedResult = _parser.Parse(repairedContent);
        return repairedResult with
        {
            OriginalContent = parseResult.OriginalContent,
            RepairedContent = repairedContent,
            RepairAttempted = true,
            RepairSucceeded = repairedResult.IsSuccess
        };
    }

    private static string? ExtractJsonObject(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            trimmed = firstLineEnd >= 0 ? trimmed[(firstLineEnd + 1)..] : trimmed;
            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }
        }

        var start = trimmed.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escape = false;
        for (var index = start; index < trimmed.Length; index++)
        {
            var current = trimmed[index];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (current == '\\')
            {
                escape = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return trimmed[start..(index + 1)];
                }
            }
        }

        return null;
    }
}
