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
            new ModelResponse(payload, "deterministic", "deterministic-local", now, now, usage),
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

    public OpenAiCompatibleModelInvoker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Name => "openai_compatible";

    public async Task<ModelInvocationResult> InvokeAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        var model = RequireSetting("GROUNDED_PLANNER_MODEL");
        if (!string.IsNullOrWhiteSpace(request.ModelEnvironmentVariable))
        {
            model = RequireSetting(request.ModelEnvironmentVariable);
        }
        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        var apiKey = string.IsNullOrWhiteSpace(request.ApiKeyEnvironmentVariable)
            ? RequireSetting("GROUNDED_PLANNER_API_KEY")
            : RequireSetting(request.ApiKeyEnvironmentVariable);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            temperature = 0,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = request.PromptText }
            }
        }), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.Timeout, exception.Message));
        }
        catch (HttpRequestException exception)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.TransportFailure, exception.Message));
        }

        var respondedAt = DateTimeOffset.UtcNow;
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, payload));
        }

        OpenAiChatCompletionResponse? completionResponse;
        try
        {
            completionResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(payload, SerializerOptions);
        }
        catch (JsonException exception)
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, exception.Message));
        }

        var content = completionResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ModelInvocationResult(false, null, new ModelFailure(FailureCategories.ProviderError, "provider returned no message content"));
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
                    completionResponse?.Usage?.CompletionTokens ?? 0)),
            null);
    }

    private static string RequireSetting(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Environment variable '{key}' must be set for model invocation.");

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
                new ModelUsage(fixture.TokensIn, fixture.TokensOut)),
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
