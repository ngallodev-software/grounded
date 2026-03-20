using System.Text.Json;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class OpenAiCompatiblePlannerGateway : ILlmPlannerGateway
{
    private readonly ModelInvokerResolver _modelInvokerResolver;
    private readonly PlannerPromptRenderer _promptRenderer;
    private readonly PlannerResponseParser _parser;
    private readonly PlannerResponseRepairService _repairService;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OpenAiCompatiblePlannerGateway(
        ModelInvokerResolver modelInvokerResolver,
        PlannerPromptRenderer promptRenderer,
        PlannerResponseParser parser,
        PlannerResponseRepairService repairService)
    {
        _modelInvokerResolver = modelInvokerResolver;
        _promptRenderer = promptRenderer;
        _parser = parser;
        _repairService = repairService;
    }

    public async Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken)
    {
        var prompt = _promptRenderer.Render(question, conversationState);
        var invokerName = IsReplayEnabled() ? "replay" : "openai_compatible";
        var invocation = await _modelInvokerResolver.GetRequired(invokerName).InvokeAsync(
            new ModelRequest(
                invokerName,
                prompt.RenderedPrompt,
                null,
                prompt.Prompt.PromptKey,
                prompt.Prompt.Version,
                prompt.Prompt.Checksum,
                "GROUNDED_PLANNER_MODEL",
                "GROUNDED_PLANNER_API_KEY"),
            cancellationToken);

        if (!invocation.IsSuccess || invocation.Response is null)
        {
            return Failure(
                prompt,
                "openai_compatible",
                Environment.GetEnvironmentVariable("GROUNDED_PLANNER_MODEL") ?? "unknown",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                0,
                0,
                invocation.Failure?.Category ?? FailureCategories.ProviderError,
                invocation.Failure?.Message ?? "planner invocation failed");
        }

        var response = invocation.Response;
        var rawResponse = new PlannerRawResponse(
            response.Content,
            response.Provider,
            response.ModelName,
            response.RequestedAt,
            response.RespondedAt,
            new PlannerUsage(response.Usage.TokensIn, response.Usage.TokensOut));
        var parsed = _repairService.TryRepair(_parser.Parse(response.Content));
        if (!parsed.IsSuccess)
        {
            return new PlannerGatewayResult(
                false,
                null,
                CreateTrace(prompt, rawResponse, parsed),
                CreateAttempt(prompt, rawResponse, parsed),
                [new ValidationErrorDto(parsed.FailureCategory, parsed.FailureMessage ?? "planner output could not be parsed")]);
        }

        return new PlannerGatewayResult(
            true,
            parsed.QueryPlan,
            CreateTrace(prompt, rawResponse, parsed),
            CreateAttempt(prompt, rawResponse, parsed),
            null);
    }

    private PlannerGatewayResult Failure(
        PlannerPromptRenderResult prompt,
        string provider,
        string model,
        DateTimeOffset requestedAt,
        DateTimeOffset respondedAt,
        int tokensIn,
        int tokensOut,
        string failureCategory,
        string failureMessage)
    {
        var trace = new PlannerTrace(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            provider,
            model,
            requestedAt,
            respondedAt,
            Math.Max(0, (long)(respondedAt - requestedAt).TotalMilliseconds),
            tokensIn,
            tokensOut,
            false,
            false,
            false,
            failureCategory,
            failureMessage);
        var attempt = new PersistedPlannerAttempt(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            provider,
            model,
            requestedAt,
            respondedAt,
            Math.Max(0, (long)(respondedAt - requestedAt).TotalMilliseconds),
            tokensIn,
            tokensOut,
            false,
            false,
            false,
            failureCategory,
            failureMessage,
            null,
            null,
            null);
        return new PlannerGatewayResult(false, null, trace, attempt, [new ValidationErrorDto(failureCategory, failureMessage)]);
    }

    private static PlannerTrace CreateTrace(PlannerPromptRenderResult prompt, PlannerRawResponse rawResponse, PlannerParseResult parseResult) =>
        new(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            rawResponse.Provider,
            rawResponse.ModelName,
            rawResponse.RequestedAt,
            rawResponse.RespondedAt,
            Math.Max(0, (long)(rawResponse.RespondedAt - rawResponse.RequestedAt).TotalMilliseconds),
            rawResponse.Usage.TokensIn,
            rawResponse.Usage.TokensOut,
            parseResult.IsSuccess,
            parseResult.RepairAttempted,
            parseResult.RepairSucceeded,
            parseResult.FailureCategory,
            parseResult.FailureMessage);

    private PersistedPlannerAttempt CreateAttempt(PlannerPromptRenderResult prompt, PlannerRawResponse rawResponse, PlannerParseResult parseResult) =>
        new(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            rawResponse.Provider,
            rawResponse.ModelName,
            rawResponse.RequestedAt,
            rawResponse.RespondedAt,
            Math.Max(0, (long)(rawResponse.RespondedAt - rawResponse.RequestedAt).TotalMilliseconds),
            rawResponse.Usage.TokensIn,
            rawResponse.Usage.TokensOut,
            parseResult.IsSuccess,
            parseResult.RepairAttempted,
            parseResult.RepairSucceeded,
            parseResult.FailureCategory,
            parseResult.FailureMessage,
            parseResult.OriginalContent,
            parseResult.RepairedContent,
            parseResult.QueryPlan is null ? null : JsonSerializer.Serialize(parseResult.QueryPlan, _serializerOptions));

    private static bool IsReplayEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE"), "true", StringComparison.OrdinalIgnoreCase);
}
