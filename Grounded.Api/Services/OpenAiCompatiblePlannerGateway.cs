using System.Text.Json;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class OpenAiCompatiblePlannerGateway : ILlmPlannerGateway
{
    private readonly ModelInvokerResolver _modelInvokerResolver;
    private readonly PlannerPromptRenderer _promptRenderer;
    private readonly PlannerResponseParser _parser;
    private readonly PlannerResponseRepairService _repairService;
    private readonly PlannerResultCache _cache;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OpenAiCompatiblePlannerGateway(
        ModelInvokerResolver modelInvokerResolver,
        PlannerPromptRenderer promptRenderer,
        PlannerResponseParser parser,
        PlannerResponseRepairService repairService,
        PlannerResultCache cache)
    {
        _modelInvokerResolver = modelInvokerResolver;
        _promptRenderer = promptRenderer;
        _parser = parser;
        _repairService = repairService;
        _cache = cache;
    }

    public async Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken)
    {
        var prompt = _promptRenderer.Render(question, conversationState);
        if (_cache.TryGet(prompt.RenderedPrompt, out var cached) && cached is not null)
        {
            return new PlannerGatewayResult(
                true,
                cached.ParseResult.QueryPlan,
                CreateTrace(prompt, cached.RawResponse, cached.ParseResult, cacheHit: true),
                CreateAttempt(prompt, cached.RawResponse, cached.ParseResult, cacheHit: true),
                null);
        }

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
                "GROUNDED_PLANNER_API_KEY",
                UseStructuredOutput: true,
                StructuredOutputSchemaJson: QueryPlanSchema.Json,
                StructuredOutputSchemaName: QueryPlanSchema.Name),
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
                CreateTrace(prompt, rawResponse, parsed, cacheHit: false),
                CreateAttempt(prompt, rawResponse, parsed, cacheHit: false),
                [new ValidationErrorDto(parsed.FailureCategory, parsed.FailureMessage ?? "planner output could not be parsed")]);
        }

        _cache.Set(prompt.RenderedPrompt, rawResponse, parsed);

        return new PlannerGatewayResult(
            true,
            parsed.QueryPlan,
            CreateTrace(prompt, rawResponse, parsed, cacheHit: false),
            CreateAttempt(prompt, rawResponse, parsed, cacheHit: false),
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
            false,
            failureCategory,
            failureMessage,
            null,
            null,
            null);
        return new PlannerGatewayResult(false, null, trace, attempt, [new ValidationErrorDto(failureCategory, failureMessage)]);
    }

    private static PlannerTrace CreateTrace(PlannerPromptRenderResult prompt, PlannerRawResponse rawResponse, PlannerParseResult parseResult, bool cacheHit) =>
        new(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            cacheHit ? "planner_cache" : rawResponse.Provider,
            rawResponse.ModelName,
            cacheHit ? DateTimeOffset.UtcNow : rawResponse.RequestedAt,
            cacheHit ? DateTimeOffset.UtcNow : rawResponse.RespondedAt,
            cacheHit ? 0 : Math.Max(0, (long)(rawResponse.RespondedAt - rawResponse.RequestedAt).TotalMilliseconds),
            cacheHit ? 0 : rawResponse.Usage.TokensIn,
            cacheHit ? 0 : rawResponse.Usage.TokensOut,
            parseResult.IsSuccess,
            parseResult.RepairAttempted,
            parseResult.RepairSucceeded,
            cacheHit,
            parseResult.FailureCategory,
            parseResult.FailureMessage);

    private PersistedPlannerAttempt CreateAttempt(PlannerPromptRenderResult prompt, PlannerRawResponse rawResponse, PlannerParseResult parseResult, bool cacheHit) =>
        new(
            prompt.Prompt.PromptKey,
            prompt.Prompt.Version,
            prompt.Prompt.Checksum,
            cacheHit ? "planner_cache" : rawResponse.Provider,
            rawResponse.ModelName,
            cacheHit ? DateTimeOffset.UtcNow : rawResponse.RequestedAt,
            cacheHit ? DateTimeOffset.UtcNow : rawResponse.RespondedAt,
            cacheHit ? 0 : Math.Max(0, (long)(rawResponse.RespondedAt - rawResponse.RequestedAt).TotalMilliseconds),
            cacheHit ? 0 : rawResponse.Usage.TokensIn,
            cacheHit ? 0 : rawResponse.Usage.TokensOut,
            parseResult.IsSuccess,
            parseResult.RepairAttempted,
            parseResult.RepairSucceeded,
            cacheHit,
            parseResult.FailureCategory,
            parseResult.FailureMessage,
            parseResult.OriginalContent,
            parseResult.RepairedContent,
            parseResult.QueryPlan is null ? null : JsonSerializer.Serialize(parseResult.QueryPlan, _serializerOptions));

    private static bool IsReplayEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable("GROUNDED_REPLAY_MODE"), "true", StringComparison.OrdinalIgnoreCase);
}
