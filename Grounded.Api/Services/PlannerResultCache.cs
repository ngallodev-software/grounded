using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class PlannerResultCache
{
    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, CachedPlannerResult> _entries = new(StringComparer.Ordinal);

    public bool TryGet(string renderedPrompt, out CachedPlannerResult? cached)
    {
        var key = ComputeKey(renderedPrompt);
        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            cached = entry;
            return true;
        }

        if (entry is not null)
        {
            _entries.TryRemove(key, out _);
        }

        cached = null;
        return false;
    }

    public void Set(string renderedPrompt, PlannerRawResponse rawResponse, PlannerParseResult parseResult)
    {
        if (!parseResult.IsSuccess || parseResult.QueryPlan is null)
        {
            return;
        }

        var key = ComputeKey(renderedPrompt);
        _entries[key] = new CachedPlannerResult(
            key,
            rawResponse,
            parseResult,
            DateTimeOffset.UtcNow.Add(EntryTtl));
    }

    private static string ComputeKey(string renderedPrompt)
    {
        var bytes = Encoding.UTF8.GetBytes(renderedPrompt);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

public sealed record CachedPlannerResult(
    string CacheKey,
    PlannerRawResponse RawResponse,
    PlannerParseResult ParseResult,
    DateTimeOffset ExpiresAt);
