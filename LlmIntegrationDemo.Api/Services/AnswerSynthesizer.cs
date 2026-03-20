using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

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

    public async Task<(AnswerDto Answer, SynthesizerTrace Trace)> SynthesizeAsync(
        string userQuestion,
        QueryPlan queryPlan,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        QueryExecutionMetadata? metadata,
        CancellationToken cancellationToken)
    {
        var prompt = _promptStore.GetPrompt("answer-synthesizer/v1.md");
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

        try
        {
            var payload = JsonSerializer.Deserialize<AnswerSynthesizerResponse>(response.Content, _serializerOptions)
                ?? new AnswerSynthesizerResponse("No data available for the requested query.", Array.Empty<string>(), false);
            _outputValidator.Validate(payload);
            var answer = new AnswerDto(payload.Summary, payload.KeyPoints, payload.TableIncluded);
            var trace = new SynthesizerTrace(prompt.Checksum, response.ModelName, response.RequestedAt, response.RespondedAt, response.TokensIn, response.TokensOut, null);
            return (answer, trace);
        }
        catch (Exception ex)
        {
            var fallback = new AnswerDto("Unable to synthesize an answer from the provided data.", Array.Empty<string>(), normalizedRows.Count > 1);
            var trace = new SynthesizerTrace(prompt.Checksum, response.ModelName, response.RequestedAt, response.RespondedAt, response.TokensIn, response.TokensOut, ex.Message);
            return (fallback, trace);
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
}
