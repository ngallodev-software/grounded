using System.Text.Json;
using Grounded.Api.Models;
using NpgsqlTypes;

namespace Grounded.Api.Services;

public interface ITraceRepository
{
    Task PersistAsync(PersistedTraceRecord trace, CancellationToken cancellationToken);
}

public sealed class NpgsqlTraceRepository : ITraceRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly INpgsqlConnectionFactory _connectionFactory;

    public NpgsqlTraceRepository(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PersistAsync(PersistedTraceRecord trace, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO llm_traces (
                trace_id,
                request_id,
                started_at_utc,
                completed_at_utc,
                final_status,
                failure_category,
                query_plan_json,
                validation_errors_json,
                compiled_sql,
                row_count,
                planner_attempt_json,
                synthesis_attempt_json
            )
            VALUES (
                @trace_id,
                @request_id,
                @started_at_utc,
                @completed_at_utc,
                @final_status,
                @failure_category,
                @query_plan_json,
                @validation_errors_json,
                @compiled_sql,
                @row_count,
                @planner_attempt_json,
                @synthesis_attempt_json
            )
            """;
        command.Parameters.AddWithValue("trace_id", trace.TraceId);
        command.Parameters.AddWithValue("request_id", trace.RequestId);
        command.Parameters.AddWithValue("started_at_utc", trace.StartedAt.UtcDateTime);
        command.Parameters.AddWithValue("completed_at_utc", trace.CompletedAt.UtcDateTime);
        command.Parameters.AddWithValue("final_status", trace.FinalStatus);
        command.Parameters.AddWithValue("failure_category", trace.FailureCategory);
        AddJsonb(command, "query_plan_json", Serialize(trace.QueryPlan));
        AddJsonb(command, "validation_errors_json", Serialize(trace.ValidationErrors));
        command.Parameters.AddWithValue("compiled_sql", (object?)trace.CompiledSql ?? DBNull.Value);
        command.Parameters.AddWithValue("row_count", (object?)trace.RowCount ?? DBNull.Value);
        AddJsonb(command, "planner_attempt_json", Serialize(trace.PlannerAttempt));
        AddJsonb(command, "synthesis_attempt_json", Serialize(trace.SynthesisAttempt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddJsonb(Npgsql.NpgsqlCommand command, string name, string? json)
    {
        var p = command.Parameters.Add(name, NpgsqlDbType.Jsonb);
        p.Value = (object?)json ?? DBNull.Value;
    }

    private static string? Serialize<T>(T value) =>
        value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
}
