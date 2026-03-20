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
    private readonly DeterministicAnswerSynthesizerEngine _engine;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DeterministicLlmGateway(DeterministicAnswerSynthesizerEngine engine)
    {
        _engine = engine;
    }

    public Task<LlmAnswerResponse> SendAnswerRequestAsync(PromptDefinition prompt, AnswerSynthesizerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var answerOutput = _engine.Build(request);
        var payload = JsonSerializer.Serialize(answerOutput, _serializerOptions);
        var now = DateTimeOffset.UtcNow;
        var tokensIn = Math.Max(1, request.UserQuestion.Length + request.Rows.Count + request.Columns.Count);
        var tokensOut = Math.Max(1, payload.Length / 4);
        var response = new LlmAnswerResponse(
            payload,
            ModelName,
            tokensIn,
            tokensOut,
            now,
            now);

        return Task.FromResult(response);
    }

    private const string ModelName = "deterministic-local";
}

public interface ILlmPlannerGateway
{
    Task<QueryPlan> PlanFromQuestionAsync(string question, CancellationToken cancellationToken);
}

public sealed class DeterministicLlmPlannerGateway : ILlmPlannerGateway
{
    public Task<QueryPlan> PlanFromQuestionAsync(string question, CancellationToken cancellationToken)
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
        return Task.FromResult(plan);
    }
}
