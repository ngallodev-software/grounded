using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class AnswerSynthesizer
{
    private readonly PromptStore _promptStore;
    private readonly ILlmGateway _llmGateway;
    private readonly AnswerOutputValidator _outputValidator;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AnswerSynthesizer(PromptStore promptStore, ILlmGateway llmGateway, AnswerOutputValidator outputValidator)
    {
        _promptStore = promptStore;
        _llmGateway = llmGateway;
        _outputValidator = outputValidator;
    }

    public async Task<(AnswerDto Answer, SynthesizerTrace Trace, PersistedSynthesisAttempt Attempt)> SynthesizeAsync(
        string userQuestion,
        QueryPlan queryPlan,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        QueryExecutionMetadata? metadata,
        CancellationToken cancellationToken)
    {
        var prompt = _promptStore.GetVersionedPrompt("answer-synthesizer", "v1");
        var normalizedRows = rows ?? Array.Empty<IReadOnlyDictionary<string, object?>>();
        var columns = ExtractColumns(normalizedRows);
        var request = new AnswerSynthesizerRequest(userQuestion, queryPlan, normalizedRows, columns, metadata, prompt.Checksum);

        LlmAnswerResponse response;
        try
        {
            response = await _llmGateway.SendAnswerRequestAsync(prompt, request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LlmGatewayException ex)
        {
            var now = DateTimeOffset.UtcNow;
            var telemetry = ex.Failure?.Telemetry;
            var failureCategory = ex.Failure?.Category ?? FailureCategories.SynthesisFailure;
            var providerName = ResolveConfiguredProviderName();
            var modelName = Environment.GetEnvironmentVariable("GROUNDED_SYNTHESIS_MODEL") ?? "unknown";
            var fallback = new AnswerDto("Unable to synthesize an answer from the provided data.", Array.Empty<string>(), normalizedRows.Count > 1);
            var trace = new SynthesizerTrace(
                providerName,
                prompt.Checksum,
                modelName,
                now,
                now,
                0,
                0,
                failureCategory,
                ex.Message,
                telemetry?.HttpStatusCode,
                telemetry?.RetryCount ?? 0,
                telemetry?.QueueWaitMs ?? 0,
                telemetry?.RetryDelayMs ?? 0,
                telemetry?.EstimatedInputTokens ?? 0,
                telemetry?.RetryAfterMs,
                telemetry?.RateLimited ?? false);
            var attempt = new PersistedSynthesisAttempt(
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                providerName,
                modelName,
                now,
                now,
                0,
                0,
                0,
                failureCategory,
                ex.Message,
                telemetry?.HttpStatusCode,
                telemetry?.RetryCount ?? 0,
                telemetry?.QueueWaitMs ?? 0,
                telemetry?.RetryDelayMs ?? 0,
                telemetry?.EstimatedInputTokens ?? 0,
                telemetry?.RetryAfterMs,
                telemetry?.RateLimited ?? false,
                null,
                JsonSerializer.Serialize(fallback, _serializerOptions));
            return (fallback, trace, attempt);
        }
        catch (Exception ex)
        {
            var now = DateTimeOffset.UtcNow;
            var providerName = ResolveConfiguredProviderName();
            var modelName = Environment.GetEnvironmentVariable("GROUNDED_SYNTHESIS_MODEL") ?? "unknown";
            var fallback = new AnswerDto("Unable to synthesize an answer from the provided data.", Array.Empty<string>(), normalizedRows.Count > 1);
            var trace = new SynthesizerTrace(
                providerName,
                prompt.Checksum,
                modelName,
                now,
                now,
                0,
                0,
                FailureCategories.SynthesisFailure,
                ex.Message,
                null,
                0,
                0,
                0,
                0,
                null,
                false);
            var attempt = new PersistedSynthesisAttempt(
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                providerName,
                modelName,
                now,
                now,
                0,
                0,
                0,
                FailureCategories.SynthesisFailure,
                ex.Message,
                null,
                0,
                0,
                0,
                0,
                null,
                false,
                null,
                JsonSerializer.Serialize(fallback, _serializerOptions));
            return (fallback, trace, attempt);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AnswerSynthesizerResponse>(response.Content, _serializerOptions)
                ?? new AnswerSynthesizerResponse("No data available for the requested query.", Array.Empty<string>(), false);
            _outputValidator.Validate(payload, request);
            var answer = new AnswerDto(payload.Summary, payload.KeyPoints, payload.TableIncluded);
            var trace = new SynthesizerTrace(
                response.Provider,
                prompt.Checksum,
                response.ModelName,
                response.RequestedAt,
                response.RespondedAt,
                response.TokensIn,
                response.TokensOut,
                FailureCategories.None,
                null,
                response.Telemetry.HttpStatusCode,
                response.Telemetry.RetryCount,
                response.Telemetry.QueueWaitMs,
                response.Telemetry.RetryDelayMs,
                response.Telemetry.EstimatedInputTokens,
                response.Telemetry.RetryAfterMs,
                response.Telemetry.RateLimited);
            var attempt = new PersistedSynthesisAttempt(
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                response.Provider,
                response.ModelName,
                response.RequestedAt,
                response.RespondedAt,
                Math.Max(0, (long)(response.RespondedAt - response.RequestedAt).TotalMilliseconds),
                response.TokensIn,
                response.TokensOut,
                FailureCategories.None,
                null,
                response.Telemetry.HttpStatusCode,
                response.Telemetry.RetryCount,
                response.Telemetry.QueueWaitMs,
                response.Telemetry.RetryDelayMs,
                response.Telemetry.EstimatedInputTokens,
                response.Telemetry.RetryAfterMs,
                response.Telemetry.RateLimited,
                response.Content,
                JsonSerializer.Serialize(answer, _serializerOptions));
            return (answer, trace, attempt);
        }
        catch (Exception ex)
        {
            var fallback = new AnswerDto("Unable to synthesize an answer from the provided data.", Array.Empty<string>(), normalizedRows.Count > 1);
            var trace = new SynthesizerTrace(
                response.Provider,
                prompt.Checksum,
                response.ModelName,
                response.RequestedAt,
                response.RespondedAt,
                response.TokensIn,
                response.TokensOut,
                FailureCategories.SynthesisFailure,
                ex.Message,
                response.Telemetry.HttpStatusCode,
                response.Telemetry.RetryCount,
                response.Telemetry.QueueWaitMs,
                response.Telemetry.RetryDelayMs,
                response.Telemetry.EstimatedInputTokens,
                response.Telemetry.RetryAfterMs,
                response.Telemetry.RateLimited);
            var attempt = new PersistedSynthesisAttempt(
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                response.Provider,
                response.ModelName,
                response.RequestedAt,
                response.RespondedAt,
                Math.Max(0, (long)(response.RespondedAt - response.RequestedAt).TotalMilliseconds),
                response.TokensIn,
                response.TokensOut,
                FailureCategories.SynthesisFailure,
                ex.Message,
                response.Telemetry.HttpStatusCode,
                response.Telemetry.RetryCount,
                response.Telemetry.QueueWaitMs,
                response.Telemetry.RetryDelayMs,
                response.Telemetry.EstimatedInputTokens,
                response.Telemetry.RetryAfterMs,
                response.Telemetry.RateLimited,
                response.Content,
                JsonSerializer.Serialize(fallback, _serializerOptions));
            return (fallback, trace, attempt);
        }
    }

    private static IReadOnlyList<string> ExtractColumns(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<string>();
        }

        return rows.First().Keys.ToList();
    }

    private static string ResolveConfiguredProviderName() =>
        ModelProviderSelector.ResolveProvider("synthesizer") switch
        {
            ModelProvider.Anthropic => "anthropic",
            _ => "openai_compatible"
        };
}
