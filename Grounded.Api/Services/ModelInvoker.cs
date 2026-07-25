using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public interface IModelInvoker
{
    string Name { get; }

    Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken);
}

public sealed class ModelInvokerResolver
{
    private readonly IReadOnlyDictionary<string, IModelInvoker> _invokers;

    public ModelInvokerResolver(IEnumerable<IModelInvoker> invokers)
    {
        _invokers = invokers.ToDictionary(invoker => invoker.Name, StringComparer.Ordinal);
    }

    public IModelInvoker GetRequired(string name)
    {
        if (!_invokers.TryGetValue(name, out var invoker))
        {
            throw new InvalidOperationException($"Model invoker '{name}' is not registered.");
        }

        return invoker;
    }
}

public sealed class DeterministicModelInvoker : IModelInvoker
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly DeterministicAnswerSynthesizerEngine _engine;

    public DeterministicModelInvoker(DeterministicAnswerSynthesizerEngine engine)
    {
        _engine = engine;
    }

    public string Name => "deterministic";

    public Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.PayloadJson))
        {
            return Task.FromResult(new ModelInvocationResult(
                false,
                null,
                new ModelFailure(FailureCategories.ProviderError, "deterministic invoker requires a payload")));
        }

        var typedRequest = JsonSerializer.Deserialize<AnswerSynthesizerRequest>(request.PayloadJson, SerializerOptions)
            ?? throw new InvalidOperationException("Unable to deserialize deterministic answer request payload.");
        var answerOutput = _engine.Build(typedRequest);
        var payload = JsonSerializer.Serialize(answerOutput, SerializerOptions);
        var now = DateTimeOffset.UtcNow;
        var usage = new ModelUsage(
            Math.Max(1, typedRequest.UserQuestion.Length + typedRequest.Rows.Count + typedRequest.Columns.Count),
            Math.Max(1, payload.Length / 4));
        return Task.FromResult(new ModelInvocationResult(
            true,
            new ModelResponse(
                payload,
                "deterministic",
                "deterministic-local",
                now,
                now,
                usage,
                new ProviderTelemetry(null, 0, 0, 0, usage.TokensIn, null, false)),
            null));
    }
}

public sealed class OpenAiCompatibleModelInvoker : IModelInvoker
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IProviderRateLimiter _rateLimiter;

    public OpenAiCompatibleModelInvoker(HttpClient httpClient, IProviderRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public string Name => "openai_compatible";

    public async Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var provider = "openai";
        var model = RequireSetting("GROUNDED_PLANNER_MODEL");
        if (!string.IsNullOrWhiteSpace(request.ModelEnvironmentVariable))
        {
            model = RequireSetting(request.ModelEnvironmentVariable);
        }
        var estimatedTokens = ModelInvocationTransport.EstimateTokens(request.PromptText, request.PayloadJson);
        var lease = await _rateLimiter.AcquireAsync(provider, estimatedTokens, cancellationToken);
        var options = ProviderRateLimitOptions.FromEnvironment(provider);

        object responseFormat = request.UseStructuredOutput && request.StructuredOutputSchemaJson is not null
            ? new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = request.StructuredOutputSchemaName ?? "response",
                    strict = true,
                    schema = JsonSerializer.Deserialize<object>(request.StructuredOutputSchemaJson)
                }
            }
            : new { type = "json_object" };

        var transport = await ModelInvocationTransport.SendWithRetryAsync(
            _httpClient,
            provider,
            options,
            lease,
            cancellationToken,
            () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                var apiKey = ResolveApiKey("OPENAI_API_KEY", request.ApiKeyEnvironmentVariable, "GROUNDED_PLANNER_API_KEY");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                message.Content = new StringContent(JsonSerializer.Serialize(new
                {
                    model,
                    temperature = 0,
                    max_tokens = 500,
                    response_format = responseFormat,
                    messages = new[]
                    {
                        new { role = "system", content = request.PromptText }
                    }
                }), Encoding.UTF8, "application/json");
                return message;
            });

        if (!transport.IsSuccess || transport.Response is null)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(
                transport.Failure?.Category ?? FailureCategories.ProviderError,
                transport.Failure?.Message ?? "provider invocation failed",
                transport.Telemetry));
        }

        var respondedAt = DateTimeOffset.UtcNow;
        var payload = transport.Response;

        OpenAiChatCompletionResponse? completionResponse;
        try
        {
            completionResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(payload, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, exception.Message, transport.Telemetry));
        }

        var content = completionResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, "provider returned no message content", transport.Telemetry));
        }

        return new ModelInvocationResult(
            true,
            new ModelResponse(
                content,
                "openai_compatible",
                model,
                requestedAt,
                respondedAt,
                new ModelUsage(
                    completionResponse?.Usage?.PromptTokens ?? 0,
                    completionResponse?.Usage?.CompletionTokens ?? 0),
                transport.Telemetry),
            null);
    }

    private static string RequireSetting(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Environment variable '{key}' must be set for model invocation.");

    private static string ResolveApiKey(string preferredKey, string? stageSpecificKey, string legacyFallbackKey)
    {
        var value = Environment.GetEnvironmentVariable(preferredKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(stageSpecificKey))
        {
            value = Environment.GetEnvironmentVariable(stageSpecificKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        value = Environment.GetEnvironmentVariable(legacyFallbackKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Environment variable '{preferredKey}' must be set for provider invocation.");
    }

    private sealed record OpenAiChatCompletionResponse(
        OpenAiChoice[]? Choices,
        OpenAiUsage? Usage);

    private sealed record OpenAiChoice(
        OpenAiMessage? Message);

    private sealed record OpenAiMessage(
        string? Content);

    private sealed record OpenAiUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
}

public sealed class AnthropicModelInvoker : IModelInvoker
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IProviderRateLimiter _rateLimiter;

    public AnthropicModelInvoker(HttpClient httpClient, IProviderRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public string Name => "anthropic";

    public async Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        const string provider = "anthropic";
        var model = RequireSetting(request.ModelEnvironmentVariable);
        var estimatedTokens = ModelInvocationTransport.EstimateTokens(request.PromptText, request.PayloadJson);
        var lease = await _rateLimiter.AcquireAsync(provider, estimatedTokens, cancellationToken);
        var options = ProviderRateLimitOptions.FromEnvironment(provider);
        var requestBody = BuildRequestBody(model, request);
        var transport = await ModelInvocationTransport.SendWithRetryAsync(
            _httpClient,
            provider,
            options,
            lease,
            cancellationToken,
            () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, "messages");
                var apiKey = ResolveApiKey("ANTHROPIC_API_KEY", request.ApiKeyEnvironmentVariable);
                message.Headers.Add("x-api-key", apiKey);
                message.Headers.Add("anthropic-version", Environment.GetEnvironmentVariable("GROUNDED_ANTHROPIC_VERSION") ?? "2023-06-01");
                message.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                return message;
            });

        if (!transport.IsSuccess || transport.Response is null)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(
                transport.Failure?.Category ?? FailureCategories.ProviderError,
                transport.Failure?.Message ?? "provider invocation failed",
                transport.Telemetry));
        }

        var respondedAt = DateTimeOffset.UtcNow;
        var payload = transport.Response;

        AnthropicMessageResponse? completionResponse;
        try
        {
            completionResponse = JsonSerializer.Deserialize<AnthropicMessageResponse>(payload, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, exception.Message, transport.Telemetry));
        }

        var content = ExtractContent(completionResponse, request.UseStructuredOutput);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, "provider returned no message content", transport.Telemetry));
        }

        return new ModelInvocationResult(
            true,
            new ModelResponse(
                content,
                "anthropic",
                model,
                requestedAt,
                respondedAt,
                new ModelUsage(
                    completionResponse?.Usage?.InputTokens ?? 0,
                    completionResponse?.Usage?.OutputTokens ?? 0),
                transport.Telemetry),
            null);
    }

    private static object BuildRequestBody(string model, ModelRequest request)
    {
        if (request.UseStructuredOutput && request.StructuredOutputSchemaJson is not null)
        {
            return new
            {
                model,
                max_tokens = 500,
                temperature = 0,
                system = request.PromptText,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "Return the structured result by calling the output tool exactly once."
                    }
                },
                tools = new[]
                {
                    new
                    {
                        name = request.StructuredOutputSchemaName ?? "response",
                        description = "Return the structured response payload.",
                        input_schema = JsonSerializer.Deserialize<object>(request.StructuredOutputSchemaJson)
                    }
                },
                tool_choice = new
                {
                    type = "tool",
                    name = request.StructuredOutputSchemaName ?? "response",
                    disable_parallel_tool_use = true
                }
            };
        }

        return new
        {
            model,
            max_tokens = 500,
            temperature = 0,
            system = request.PromptText,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = "Return the requested response."
                }
            }
        };
    }

    private static string? ExtractContent(AnthropicMessageResponse? response, bool useStructuredOutput)
    {
        if (response?.Content is null || response.Content.Length == 0)
        {
            return null;
        }

        if (useStructuredOutput)
        {
            var toolBlock = response.Content.FirstOrDefault(block => string.Equals(block.Type, "tool_use", StringComparison.OrdinalIgnoreCase));
            if (toolBlock?.Input is not null)
            {
                return JsonSerializer.Serialize(toolBlock.Input);
            }
        }

        var textBlock = response.Content.FirstOrDefault(block => string.Equals(block.Type, "text", StringComparison.OrdinalIgnoreCase));
        return textBlock?.Text;
    }

    private static string RequireSetting(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Environment variable '{key}' must be set for model invocation.");

    private static string ResolveApiKey(string preferredKey, string? stageSpecificKey)
    {
        var value = Environment.GetEnvironmentVariable(preferredKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(stageSpecificKey))
        {
            value = Environment.GetEnvironmentVariable(stageSpecificKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Environment variable '{preferredKey}' must be set for provider invocation.");
    }

    private sealed record AnthropicMessageResponse(
        AnthropicContentBlock[]? Content,
        AnthropicUsage? Usage);

    private sealed record AnthropicContentBlock(
        string? Type,
        string? Text,
        JsonElement? Input);

    private sealed record AnthropicUsage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
}

file sealed record TransportAttemptResult(
    bool IsSuccess,
    string? Response,
    ModelFailure? Failure,
    ProviderTelemetry Telemetry);

file static class ModelInvocationTransport
{
    public static async Task<TransportAttemptResult> SendWithRetryAsync(
        HttpClient httpClient,
        string provider,
        ProviderRateLimitOptions options,
        ProviderLease lease,
        CancellationToken cancellationToken,
        Func<HttpRequestMessage> messageFactory)
    {
        var retryCount = 0;
        long retryDelayMs = 0;
        int? lastStatusCode = null;
        int? retryAfterMs = null;
        var rateLimited = false;

        while (true)
        {
            using var message = messageFactory();
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(message, cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                return Failure(FailureCategories.Timeout, exception.Message);
            }
            catch (HttpRequestException exception)
            {
                if (retryCount < options.MaxRetries)
                {
                    var delay = ComputeBackoff(options.BaseBackoffMs, retryCount, null);
                    retryCount++;
                    retryDelayMs += (long)delay.TotalMilliseconds;
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return Failure(FailureCategories.TransportFailure, exception.Message);
            }

            using (response)
            {
                var payload = await response.Content.ReadAsStringAsync(cancellationToken);
                lastStatusCode = (int)response.StatusCode;
                retryAfterMs = TryGetRetryAfterMs(response);

                if (response.IsSuccessStatusCode)
                {
                    return new TransportAttemptResult(
                        true,
                        payload,
                        null,
                        BuildTelemetry(lastStatusCode, retryCount, lease.QueueWaitMs, retryDelayMs, lease.EstimatedTokens, retryAfterMs, rateLimited));
                }

                if (IsRetryable(response.StatusCode) && retryCount < options.MaxRetries)
                {
                    rateLimited |= response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
                    var delay = ComputeBackoff(options.BaseBackoffMs, retryCount, retryAfterMs);
                    retryCount++;
                    retryDelayMs += (long)delay.TotalMilliseconds;
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return new TransportAttemptResult(
                    false,
                    null,
                    new ModelFailure(FailureCategories.ProviderError, payload),
                    BuildTelemetry(lastStatusCode, retryCount, lease.QueueWaitMs, retryDelayMs, lease.EstimatedTokens, retryAfterMs, rateLimited || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests));
            }
        }

        TransportAttemptResult Failure(string category, string message) =>
            new(
                false,
                null,
                new ModelFailure(category, message),
                BuildTelemetry(lastStatusCode, retryCount, lease.QueueWaitMs, retryDelayMs, lease.EstimatedTokens, retryAfterMs, rateLimited));
    }

    private static ProviderTelemetry BuildTelemetry(
        int? statusCode,
        int retryCount,
        long queueWaitMs,
        long retryDelayMs,
        int estimatedTokens,
        int? retryAfterMs,
        bool rateLimited) =>
        new(
            statusCode,
            retryCount,
            queueWaitMs,
            retryDelayMs,
            estimatedTokens,
            retryAfterMs,
            rateLimited);

    private static bool IsRetryable(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.TooManyRequests || (int)statusCode == 529;

    private static TimeSpan ComputeBackoff(int baseBackoffMs, int attempt, int? retryAfterMs)
    {
        if (retryAfterMs is > 0)
        {
            return TimeSpan.FromMilliseconds(retryAfterMs.Value);
        }

        var multiplier = Math.Pow(2, attempt);
        var jitter = Random.Shared.Next(50, 251);
        return TimeSpan.FromMilliseconds((baseBackoffMs * multiplier) + jitter);
    }

    private static int? TryGetRetryAfterMs(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return Math.Max(0, (int)delta.TotalMilliseconds);
        }

        if (response.Headers.TryGetValues("retry-after-ms", out var millisecondValues) &&
            int.TryParse(millisecondValues.FirstOrDefault(), out var milliseconds))
        {
            return Math.Max(0, milliseconds);
        }

        if (response.Headers.TryGetValues("retry-after", out var secondValues) &&
            int.TryParse(secondValues.FirstOrDefault(), out var seconds))
        {
            return Math.Max(0, seconds * 1000);
        }

        return null;
    }

    public static int EstimateTokens(string promptText, string? payloadJson)
    {
        var totalLength = (promptText?.Length ?? 0) + (payloadJson?.Length ?? 0);
        return Math.Max(1, (int)Math.Ceiling(totalLength / 4d));
    }
}

public sealed class ReplayModelInvoker : IModelInvoker
{
    private static readonly JsonSerializerOptions FixtureSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _fixturePath;
    private IReadOnlyList<ReplayFixture>? _fixtures;

    public ReplayModelInvoker(IConfiguration configuration)
    {
        _fixturePath = configuration.GetValue<string>("Eval:ReplayFixturesPath") ?? "eval/replay_fixtures.json";
    }

    public string Name => "replay";

    private IReadOnlyList<ReplayFixture> LoadFixtures()
    {
        if (_fixtures is not null)
        {
            return _fixtures;
        }

        var resolved = ResolvePath(_fixturePath);
        var content = File.ReadAllText(resolved);
        _fixtures = JsonSerializer.Deserialize<List<ReplayFixture>>(content, FixtureSerializerOptions) ?? [];
        return _fixtures;
    }

    public Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ReplayFixture> fixtures;
        try
        {
            fixtures = LoadFixtures();
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ModelInvocationResult(
                false,
                null,
                new ModelFailure(FailureCategories.ProviderError, $"Replay fixtures could not be loaded: {ex.Message}")));
        }

        var haystack = $"{request.PromptText}\n{request.PayloadJson}";
        var fixture = fixtures.FirstOrDefault(candidate =>
            string.Equals(candidate.PromptKey, request.PromptKey, StringComparison.OrdinalIgnoreCase) &&
            haystack.Contains(candidate.MatchText, StringComparison.OrdinalIgnoreCase));

        if (fixture is null)
        {
            return Task.FromResult(new ModelInvocationResult(
                false,
                null,
                new ModelFailure(FailureCategories.ProviderError, $"No replay fixture matched prompt '{request.PromptKey}'.")));
        }

        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new ModelInvocationResult(
            true,
            new ModelResponse(
                fixture.ResponseContent,
                fixture.Provider,
                fixture.ModelName,
                now,
                now,
                new ModelUsage(fixture.TokensIn, fixture.TokensOut),
                new ProviderTelemetry(null, 0, 0, 0, fixture.TokensIn, null, false)),
            null));
    }

    private static string ResolvePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, normalized);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException($"Replay fixtures file '{relativePath}' was not found.");
    }
}
