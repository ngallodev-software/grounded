using System.Text;
using System.Text.Json;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class OpenAiCompatibleAnswerGateway : ILlmGateway
{
    private readonly ModelInvokerResolver _modelInvokerResolver;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OpenAiCompatibleAnswerGateway(ModelInvokerResolver modelInvokerResolver)
    {
        _modelInvokerResolver = modelInvokerResolver;
    }

    public async Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(request, _serializerOptions);
        var renderedPrompt = new StringBuilder(prompt.Content)
            .AppendLine()
            .AppendLine()
            .AppendLine("## Input")
            .AppendLine("```json")
            .AppendLine(payload)
            .AppendLine("```")
            .ToString();

        var invokerName = IsReplayEnabled() ? "replay" : "openai_compatible";
        var result = await _modelInvokerResolver.GetRequired(invokerName).InvokeAsync(
            new ModelRequest(
                invokerName,
                renderedPrompt,
                payload,
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                "GROUNDED_SYNTHESIS_MODEL",
                "GROUNDED_SYNTHESIS_API_KEY"),
            cancellationToken);

        if (!result.IsSuccess || result.Response is null)
        {
            throw new InvalidOperationException(result.Failure?.Message ?? "answer model invocation failed");
        }

        return new LlmAnswerResponse(
            result.Response.Content,
            result.Response.ModelName,
            result.Response.Usage.TokensIn,
            result.Response.Usage.TokensOut,
            result.Response.RequestedAt,
            result.Response.RespondedAt);
    }

    private static bool IsReplayEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE"), "true", StringComparison.OrdinalIgnoreCase);
}
