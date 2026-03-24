using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

public sealed class ModelProviderTests
{
    [Fact]
    public void ResolveInvokerName_UsesConfiguredStageProvider_AndReplayOverride()
    {
        var previousReplay = Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE");
        var previousPlannerProvider = Environment.GetEnvironmentVariable("GROUNDED_PLANNER_PROVIDER");
        var previousSynthesisProvider = Environment.GetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", "false");
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_PROVIDER", "anthropic");
            Environment.SetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER", "openai");

            Assert.Equal("anthropic", ModelProviderSelector.ResolveInvokerName("planner"));
            Assert.Equal("openai_compatible", ModelProviderSelector.ResolveInvokerName("synthesizer"));

            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", "true");

            Assert.Equal("replay", ModelProviderSelector.ResolveInvokerName("planner"));
            Assert.Equal("replay", ModelProviderSelector.ResolveInvokerName("synthesizer"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", previousReplay);
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_PROVIDER", previousPlannerProvider);
            Environment.SetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER", previousSynthesisProvider);
        }
    }

    [Fact]
    public async Task AnthropicInvoker_ParsesToolUseStructuredOutput()
    {
        var previousModel = Environment.GetEnvironmentVariable("GROUNDED_PLANNER_MODEL");
        var previousApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var previousVersion = Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_MODEL", "claude-sonnet-test");
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "anthropic-test-key");
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION", "2023-06-01");

            var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.anthropic.com/v1/messages", request.RequestUri!.ToString());
                Assert.Equal("anthropic-test-key", request.Headers.GetValues("x-api-key").Single());
                Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

                var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                Assert.Contains("\"tool_choice\"", body);
                Assert.Contains("\"tools\"", body);
                Assert.Contains("\"name\":\"query_plan\"", body);

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
            });

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
            Assert.Equal("anthropic", result.Response!.Provider);
            Assert.Equal("claude-sonnet-test", result.Response.ModelName);
            Assert.Contains("\"metric\":\"revenue\"", result.Response.Content);
            Assert.Equal(12, result.Response.Usage.TokensIn);
            Assert.Equal(8, result.Response.Usage.TokensOut);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_PLANNER_MODEL", previousModel);
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION", previousVersion);
        }
    }

    [Fact]
    public async Task AnswerGateway_UsesAnthropicInvokerWhenConfigured()
    {
        var previousReplay = Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE");
        var previousProvider = Environment.GetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER");

        try
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", "false");
            Environment.SetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER", "anthropic");

            var resolver = new ModelInvokerResolver([new StubInvoker("anthropic")]);
            var gateway = new OpenAiCompatibleAnswerGateway(resolver);
            var prompt = new PromptStore().GetVersionedPrompt("answer-synthesizer", "v1");
            var request = new AnswerSynthesizerRequest(
                "What was revenue?",
                new QueryPlan("1.0", "aggregate", null, [], "revenue", new("last_month", null, null), null, new("metric", "desc"), null, false),
                [new Dictionary<string, object?> { ["metric"] = 123m }],
                ["metric"],
                null,
                prompt.Checksum);

            var response = await gateway.SendAnswerRequestAsync(prompt, request, CancellationToken.None);

            Assert.Equal("anthropic", response.Provider);
            Assert.Contains("\"summary\":\"ok\"", response.Content);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GROUNDED_REPLAY_MODE", previousReplay);
            Environment.SetEnvironmentVariable("GROUNDED_SYNTHESIS_PROVIDER", previousProvider);
        }
    }

    private sealed class StubInvoker : IModelInvoker
    {
        public StubInvoker(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new ModelInvocationResult(
                true,
                new ModelResponse(
                    "{\"summary\":\"ok\",\"keyPoints\":[\"123\"],\"tableIncluded\":false}",
                    Name,
                    "test-model",
                    now,
                    now,
                    new ModelUsage(10, 5)),
                null));
        }
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
