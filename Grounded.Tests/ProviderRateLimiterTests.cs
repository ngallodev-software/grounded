using System.Diagnostics;
using System.Net;
using System.Text;
using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

public sealed class ProviderRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_WaitsForNextWindow_WhenRequestBudgetIsExceeded()
    {
        var previousRequests = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_REQUESTS_PER_MINUTE");
        var previousTokens = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_TOKENS_PER_MINUTE");
        var previousWindow = Environment.GetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_REQUESTS_PER_MINUTE", "1");
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_TOKENS_PER_MINUTE", "1000");
            Environment.SetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS", "20");

            var limiter = new ProviderRateLimiter();

            var first = await limiter.AcquireAsync("anthropic", 10, CancellationToken.None);
            var watch = Stopwatch.StartNew();
            var second = await limiter.AcquireAsync("anthropic", 10, CancellationToken.None);
            watch.Stop();

            Assert.True(first.QueueWaitMs >= 0);
            Assert.True(second.QueueWaitMs > 0);
            Assert.True(watch.ElapsedMilliseconds >= 10);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_REQUESTS_PER_MINUTE", previousRequests);
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_RATE_LIMIT_TOKENS_PER_MINUTE", previousTokens);
            Environment.SetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS", previousWindow);
        }
    }

    [Fact]
    public async Task AcquireAsync_AllowsOversizedSingleRequest_WithoutDeadlock()
    {
        var previousTokens = Environment.GetEnvironmentVariable("GROUNDED_OPENAI_RATE_LIMIT_TOKENS_PER_MINUTE");
        var previousWindow = Environment.GetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_OPENAI_RATE_LIMIT_TOKENS_PER_MINUTE", "100");
            Environment.SetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS", "10");

            var limiter = new ProviderRateLimiter();
            var lease = await limiter.AcquireAsync("openai", 500, CancellationToken.None);

            Assert.Equal("openai", lease.Provider);
            Assert.Equal(500, lease.EstimatedTokens);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_OPENAI_RATE_LIMIT_TOKENS_PER_MINUTE", previousTokens);
            Environment.SetEnvironmentVariable("GROUNDED_PROVIDER_RATE_LIMIT_WINDOW_MS", previousWindow);
        }
    }

    [Fact]
    public async Task AnthropicInvoker_Retries429_AndCapturesTelemetry()
    {
        var previousModel = Environment.GetEnvironmentVariable("GROUNDED_PLANNER_MODEL");
        var previousApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var previousVersion = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION");
        var previousRetries = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_MAX_RETRIES");
        var previousBackoff = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_BASE_BACKOFF_MS");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_MODEL", "claude-haiku-test");
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "anthropic-test-key");
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION", "2023-06-01");
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_MAX_RETRIES", "2");
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_BASE_BACKOFF_MS", "1");

            var attempts = 0;
            var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            {
                attempts++;
                if (attempts == 1)
                {
                    var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        Content = new StringContent("{\"error\":\"rate limited\"}", Encoding.UTF8, "application/json")
                    };
                    throttled.Headers.TryAddWithoutValidation("retry-after-ms", "1");
                    return Task.FromResult(throttled);
                }

                const string payload = """
                {
                  "content": [
                    {
                      "type": "tool_use",
                      "id": "toolu_123",
                      "name": "query_plan",
                      "input": {
                        "version": "1.0",
                        "questionType": "aggregate",
                        "dimension": null,
                        "filters": [],
                        "metric": "revenue",
                        "timeRange": {
                          "preset": "last_month",
                          "startDate": null,
                          "endDate": null
                        },
                        "timeGrain": null,
                        "sort": {
                          "by": "metric",
                          "direction": "desc"
                        },
                        "limit": null,
                        "usePriorState": false
                      }
                    }
                  ],
                  "usage": {
                    "input_tokens": 12,
                    "output_tokens": 8
                  }
                }
                """;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            });

            var invoker = new AnthropicModelInvoker(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://api.anthropic.com/v1/")
            }, new PassthroughRateLimiter());

            var result = await invoker.InvokeAsync(
                new ModelRequest(
                    "anthropic",
                    "planner prompt",
                    null,
                    "planner",
                    "v2",
                    "checksum",
                    "GROUNDED_PLANNER_MODEL",
                    "GROUNDED_PLANNER_API_KEY",
                    UseStructuredOutput: true,
                    StructuredOutputSchemaJson: "{\"type\":\"object\"}",
                    StructuredOutputSchemaName: "query_plan"),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Response);
            Assert.Equal(2, attempts);
            Assert.Equal(1, result.Response!.Telemetry.RetryCount);
            Assert.True(result.Response.Telemetry.RateLimited);
            Assert.True(result.Response.Telemetry.RetryDelayMs >= 1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_MODEL", previousModel);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION", previousVersion);
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_MAX_RETRIES", previousRetries);
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_BASE_BACKOFF_MS", previousBackoff);
        }
    }

    private sealed class PassthroughRateLimiter : IProviderRateLimiter
    {
        public Task<ProviderLease> AcquireAsync(string provider, int estimatedTokens, CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderLease(provider, estimatedTokens, 0));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
