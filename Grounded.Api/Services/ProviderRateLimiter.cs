using System.Collections.Concurrent;
using System.Globalization;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public interface IProviderRateLimiter
{
    Task<ProviderLease> AcquireAsync(string provider, int estimatedTokens, CancellationToken cancellationToken);
}

public sealed record ProviderLease(
    string Provider,
    int EstimatedTokens,
    long QueueWaitMs);

public sealed class ProviderRateLimiter : IProviderRateLimiter
{
    private readonly ConcurrentDictionary<string, ProviderWindowState> _states = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ProviderLease> AcquireAsync(string provider, int estimatedTokens, CancellationToken cancellationToken)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var options = ProviderRateLimitOptions.FromEnvironment(normalizedProvider);
        var effectiveTokens = options.TokensPerMinute > 0
            ? Math.Min(Math.Max(0, estimatedTokens), options.TokensPerMinute)
            : Math.Max(0, estimatedTokens);
        var startedAt = DateTimeOffset.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = _states.GetOrAdd(normalizedProvider, _ => new ProviderWindowState());
            TimeSpan delay;

            lock (state.SyncRoot)
            {
                state.ResetIfExpired(options.Window);
                var canConsumeRequest = options.RequestsPerMinute <= 0 || state.RequestCount < options.RequestsPerMinute;
                var canConsumeTokens = options.TokensPerMinute <= 0 || state.TokenCount + effectiveTokens <= options.TokensPerMinute;
                if (canConsumeRequest && canConsumeTokens)
                {
                    state.RequestCount++;
                    state.TokenCount += effectiveTokens;
                    return new ProviderLease(
                        normalizedProvider,
                        estimatedTokens,
                        Math.Max(0, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds));
                }

                delay = state.DelayUntilNextWindow(options.Window);
            }

            if (delay <= TimeSpan.Zero)
            {
                delay = TimeSpan.FromMilliseconds(50);
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    private static string NormalizeProvider(string provider) =>
        string.Equals(provider, "openai_compatible", StringComparison.OrdinalIgnoreCase)
            ? "openai"
            : provider.Trim().ToLowerInvariant();

    private sealed class ProviderWindowState
    {
        public object SyncRoot { get; } = new();
        public DateTimeOffset WindowStartedAt { get; private set; } = DateTimeOffset.UtcNow;
        public int RequestCount { get; set; }
        public int TokenCount { get; set; }

        public void ResetIfExpired(TimeSpan window)
        {
            if (DateTimeOffset.UtcNow - WindowStartedAt < window)
            {
                return;
            }

            WindowStartedAt = DateTimeOffset.UtcNow;
            RequestCount = 0;
            TokenCount = 0;
        }

        public TimeSpan DelayUntilNextWindow(TimeSpan window)
        {
            var elapsed = DateTimeOffset.UtcNow - WindowStartedAt;
            if (elapsed >= window)
            {
                return TimeSpan.Zero;
            }

            return window - elapsed;
        }
    }
}

public sealed record ProviderRateLimitOptions(
    int RequestsPerMinute,
    int TokensPerMinute,
    int MaxRetries,
    int BaseBackoffMs,
    TimeSpan Window)
{
    public static ProviderRateLimitOptions FromEnvironment(string provider)
    {
        var prefix = provider.Equals("anthropic", StringComparison.OrdinalIgnoreCase)
            ? "GROUNDED_ANTHROPIC"
            : "GROUNDED_OPENAI";
        var windowMs = ReadInt("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS", (int)TimeSpan.FromMinutes(1).TotalMilliseconds);
        return new ProviderRateLimitOptions(
            ReadInt($"{prefix}_RATE_LIMIT_REQUESTS_PER_MINUTE", provider.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ? 8 : 60),
            ReadInt($"{prefix}_RATE_LIMIT_TOKENS_PER_MINUTE", provider.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ? 30000 : 120000),
            ReadInt($"{prefix}_MAX_RETRIES", 3),
            ReadInt($"{prefix}_BASE_BACKOFF_MS", provider.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ? 1500 : 750),
            TimeSpan.FromMilliseconds(Math.Max(1, windowMs)));
    }

    private static int ReadInt(string key, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
