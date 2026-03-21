using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public interface ILlmGateway
{
    Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken);
}

public sealed record LlmAnswerResponse(
    string Content,
    string ModelName,
    int TokensIn,
    int TokensOut,
    DateTimeOffset RequestedAt,
    DateTimeOffset RespondedAt);

public sealed class DeterministicLlmGateway : ILlmGateway
{
    private readonly ModelInvokerResolver _modelInvokerResolver;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DeterministicLlmGateway(ModelInvokerResolver modelInvokerResolver)
    {
        _modelInvokerResolver = modelInvokerResolver;
    }

    public async Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken)
    {
        var result = await _modelInvokerResolver.GetRequired("deterministic").InvokeAsync(
            new ModelRequest(
                "deterministic",
                prompt.Content,
                JsonSerializer.Serialize(request, _serializerOptions),
                prompt.PromptKey,
                prompt.Version,
                prompt.Checksum,
                string.Empty,
                string.Empty),
            cancellationToken);
        if (!result.IsSuccess || result.Response is null)
        {
            throw new InvalidOperationException(result.Failure?.Message ?? "deterministic invocation failed");
        }

        return new LlmAnswerResponse(
            result.Response.Content,
            result.Response.ModelName,
            result.Response.Usage.TokensIn,
            result.Response.Usage.TokensOut,
            result.Response.RequestedAt,
            result.Response.RespondedAt);
    }
}

public interface ILlmPlannerGateway
{
    Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken);
}

public sealed class DeterministicLlmPlannerGateway : ILlmPlannerGateway
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<PlannerGatewayResult> PlanFromQuestionAsync(string question, ConversationStateSnapshot? conversationState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = new QueryPlan(
            "1.0",
            "aggregate",
            Dimension: null,
            Filters: Array.Empty<FilterSpec>(),
            Metric: "revenue",
            TimeRange: new TimeRangeSpec("last_30_days", null, null),
            TimeGrain: null,
            Sort: new SortSpec("metric", "desc"),
            Limit: null,
            UsePriorState: false);
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new PlannerGatewayResult(
            true,
            plan,
            new PlannerTrace(
                "planner",
                "v2",
                "deterministic",
                "deterministic",
                "deterministic-local",
                now,
                now,
                0,
                Math.Max(1, question.Length / 4),
                1,
                true,
                false,
                false,
                false,
                FailureCategories.None,
                null),
            new PersistedPlannerAttempt(
                "planner",
                "v2",
                "deterministic",
                "deterministic",
                "deterministic-local",
                now,
                now,
                0,
                Math.Max(1, question.Length / 4),
                1,
                true,
                false,
                false,
                false,
                FailureCategories.None,
                null,
                null,
                null,
                JsonSerializer.Serialize(plan, _serializerOptions)),
            null));
    }
}
