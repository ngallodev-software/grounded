using System.Text.Json;
using Grounded.Api.Models;
using Npgsql;

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
        await EnsureSchemaAsync(connection, cancellationToken);

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
        command.Parameters.AddWithValue("query_plan_json", (object?)Serialize(trace.QueryPlan) ?? DBNull.Value);
        command.Parameters.AddWithValue("validation_errors_json", (object?)Serialize(trace.ValidationErrors) ?? DBNull.Value);
        command.Parameters.AddWithValue("compiled_sql", (object?)trace.CompiledSql ?? DBNull.Value);
        command.Parameters.AddWithValue("row_count", (object?)trace.RowCount ?? DBNull.Value);
        command.Parameters.AddWithValue("planner_attempt_json", (object?)Serialize(trace.PlannerAttempt) ?? DBNull.Value);
        command.Parameters.AddWithValue("synthesis_attempt_json", (object?)Serialize(trace.SynthesisAttempt) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS llm_traces (
                trace_id TEXT PRIMARY KEY,
                request_id TEXT NOT NULL,
                started_at_utc TIMESTAMPTZ NOT NULL,
                completed_at_utc TIMESTAMPTZ NOT NULL,
                final_status TEXT NOT NULL,
                failure_category TEXT NOT NULL,
                query_plan_json JSONB NULL,
                validation_errors_json JSONB NULL,
                compiled_sql TEXT NULL,
                row_count INTEGER NULL,
                planner_attempt_json JSONB NULL,
                synthesis_attempt_json JSONB NULL
            );

            CREATE INDEX IF NOT EXISTS ix_llm_traces_request_id ON llm_traces(request_id);
            CREATE INDEX IF NOT EXISTS ix_llm_traces_failure_category ON llm_traces(failure_category);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? Serialize<T>(T value) =>
        value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
}
