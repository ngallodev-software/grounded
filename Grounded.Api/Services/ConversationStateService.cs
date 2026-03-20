using System.Text.Json;
using System.Text.RegularExpressions;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public interface IConversationStateRepository
{
    Task<ConversationStateSnapshot?> GetAsync(string conversationId, CancellationToken cancellationToken);
    Task UpsertAsync(string conversationId, ConversationStateSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed class ConversationStateService
{
    private static readonly Regex LastQuarterPattern = new(@"^\s*(what about|how about)\s+last\s+quarter\??\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SameThingByCategoryPattern = new(@"^\s*(same thing|same question|same query)(,\s*but)?\s+by\s+category\??\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ElectronicsOnlyPattern = new(@"^\s*((what about|how about)\s+just|now only for)\s+electronics\??\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly IConversationStateRepository _repository;

    public ConversationStateService(IConversationStateRepository repository)
    {
        _repository = repository;
    }

    public Task<ConversationStateSnapshot?> GetAsync(string? conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return Task.FromResult<ConversationStateSnapshot?>(null);
        }

        return _repository.GetAsync(conversationId.Trim(), cancellationToken);
    }

    public Task SaveAsync(string? conversationId, QueryPlan plan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return Task.CompletedTask;
        }

        var snapshot = new ConversationStateSnapshot(
            plan.QuestionType,
            plan.Metric,
            plan.Dimension,
            plan.Filters.ToArray(),
            plan.TimeRange);
        return _repository.UpsertAsync(conversationId.Trim(), snapshot, cancellationToken);
    }

    public FollowUpResolutionResult Resolve(string question, ConversationStateSnapshot? priorState)
    {
        if (!LooksLikeFollowUp(question))
        {
            return new(false, false, null, null, null);
        }

        if (priorState is null)
        {
            return new(true, false, null, "unsupported_follow_up", "follow-up questions require prior conversation state");
        }

        if (!CanResolveFromCompactState(priorState))
        {
            return new(true, false, null, "unsupported_follow_up", "follow-up question requires prior fields that are intentionally not stored in compact conversation state");
        }

        if (SameThingByCategoryPattern.IsMatch(question))
        {
            return new(true, true, CreateByCategoryPlan(priorState), null, null);
        }

        if (LastQuarterPattern.IsMatch(question))
        {
            return new(true, true, CreateLastQuarterPlan(priorState), null, null);
        }

        if (ElectronicsOnlyPattern.IsMatch(question))
        {
            return new(true, true, CreateElectronicsOnlyPlan(priorState), null, null);
        }

        return new(true, false, null, "unsupported_follow_up", "follow-up question is outside the supported compact-memory patterns");
    }

    private static bool LooksLikeFollowUp(string question)
    {
        var normalized = question.Trim().ToLowerInvariant();
        return normalized.StartsWith("what about ", StringComparison.Ordinal) ||
               normalized.StartsWith("how about ", StringComparison.Ordinal) ||
               normalized.StartsWith("same thing", StringComparison.Ordinal) ||
               normalized.StartsWith("same question", StringComparison.Ordinal) ||
               normalized.StartsWith("same query", StringComparison.Ordinal) ||
               normalized.StartsWith("now ", StringComparison.Ordinal);
    }

    private static QueryPlan CreateByCategoryPlan(ConversationStateSnapshot state) =>
        new(
            "1.0",
            "grouped_breakdown",
            "product_category",
            state.Filters.ToArray(),
            state.Metric,
            state.TimeRange,
            null,
            new("metric", "desc"),
            null,
            false);

    private static QueryPlan CreateLastQuarterPlan(ConversationStateSnapshot state) =>
        new(
            "1.0",
            state.QuestionType,
            state.Dimension,
            state.Filters.ToArray(),
            state.Metric,
            new("last_quarter", null, null),
            null,
            new("metric", "desc"),
            null,
            false);

    private static QueryPlan CreateElectronicsOnlyPlan(ConversationStateSnapshot state)
    {
        var filters = state.Filters
            .Where(static filter => !string.Equals(filter.Field, "product_category", StringComparison.Ordinal))
            .ToList();
        filters.Add(new FilterSpec("product_category", "eq", ["Electronics"]));

        return new QueryPlan(
            "1.0",
            state.QuestionType,
            state.Dimension,
            filters,
            state.Metric,
            state.TimeRange,
            null,
            new("metric", "desc"),
            null,
            false);
    }

    private static bool CanResolveFromCompactState(ConversationStateSnapshot state) =>
        string.Equals(state.QuestionType, "aggregate", StringComparison.Ordinal) ||
        string.Equals(state.QuestionType, "grouped_breakdown", StringComparison.Ordinal);
}

public sealed class NpgsqlConversationStateRepository : IConversationStateRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly INpgsqlConnectionFactory _connectionFactory;

    public NpgsqlConversationStateRepository(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ConversationStateSnapshot?> GetAsync(string conversationId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT question_type, metric, dimension, filters_json, time_range_json
            FROM conversation_states
            WHERE conversation_id = @conversation_id
            """;
        command.Parameters.AddWithValue("conversation_id", conversationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var filtersJson = reader.GetString(3);
        var timeRangeJson = reader.GetString(4);
        var filters = JsonSerializer.Deserialize<IReadOnlyList<FilterSpec>>(filtersJson, SerializerOptions) ?? [];
        var timeRange = JsonSerializer.Deserialize<TimeRangeSpec>(timeRangeJson, SerializerOptions) ?? new TimeRangeSpec("last_30_days", null, null);
        return new ConversationStateSnapshot(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            filters,
            timeRange);
    }

    public async Task UpsertAsync(string conversationId, ConversationStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO conversation_states (
                conversation_id,
                question_type,
                metric,
                dimension,
                filters_json,
                time_range_json,
                updated_at_utc
            )
            VALUES (
                @conversation_id,
                @question_type,
                @metric,
                @dimension,
                @filters_json,
                @time_range_json,
                @updated_at_utc
            )
            ON CONFLICT (conversation_id)
            DO UPDATE SET
                question_type = EXCLUDED.question_type,
                metric = EXCLUDED.metric,
                dimension = EXCLUDED.dimension,
                filters_json = EXCLUDED.filters_json,
                time_range_json = EXCLUDED.time_range_json,
                updated_at_utc = EXCLUDED.updated_at_utc
            """;
        command.Parameters.AddWithValue("conversation_id", conversationId);
        command.Parameters.AddWithValue("question_type", snapshot.QuestionType);
        command.Parameters.AddWithValue("metric", snapshot.Metric);
        command.Parameters.AddWithValue("dimension", (object?)snapshot.Dimension ?? DBNull.Value);
        command.Parameters.AddWithValue("filters_json", JsonSerializer.Serialize(snapshot.Filters, SerializerOptions));
        command.Parameters.AddWithValue("time_range_json", JsonSerializer.Serialize(snapshot.TimeRange, SerializerOptions));
        command.Parameters.AddWithValue("updated_at_utc", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

}
