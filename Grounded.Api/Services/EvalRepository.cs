using System.Text.Json;
using Grounded.Api.Models;
using Npgsql;

namespace Grounded.Api.Services;

public interface IEvalRepository
{
    Task PersistAsync(PersistedEvalRun run, CancellationToken cancellationToken);
}

public sealed class NpgsqlEvalRepository : IEvalRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly INpgsqlConnectionFactory _connectionFactory;

    public NpgsqlEvalRepository(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PersistAsync(PersistedEvalRun run, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO eval_runs (
                run_id,
                started_at_utc,
                completed_at_utc,
                planner_prompt_version,
                synthesizer_prompt_version,
                score,
                case_results_json,
                comparison_json
            )
            VALUES (
                @run_id,
                @started_at_utc,
                @completed_at_utc,
                @planner_prompt_version,
                @synthesizer_prompt_version,
                @score,
                @case_results_json,
                @comparison_json
            )
            """;
        command.Parameters.AddWithValue("run_id", run.RunId);
        command.Parameters.AddWithValue("started_at_utc", run.StartedAt.UtcDateTime);
        command.Parameters.AddWithValue("completed_at_utc", run.CompletedAt.UtcDateTime);
        command.Parameters.AddWithValue("planner_prompt_version", run.PlannerPromptVersion);
        command.Parameters.AddWithValue("synthesizer_prompt_version", run.SynthesizerPromptVersion);
        command.Parameters.AddWithValue("score", run.Score);
        command.Parameters.AddWithValue("case_results_json", JsonSerializer.Serialize(run.CaseResults, SerializerOptions));
        command.Parameters.AddWithValue("comparison_json", JsonSerializer.Serialize(run.Comparison, SerializerOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSchemaAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS eval_runs (
                run_id TEXT PRIMARY KEY,
                started_at_utc TIMESTAMPTZ NOT NULL,
                completed_at_utc TIMESTAMPTZ NOT NULL,
                planner_prompt_version TEXT NOT NULL,
                synthesizer_prompt_version TEXT NOT NULL,
                score NUMERIC(10,4) NOT NULL,
                case_results_json JSONB NOT NULL,
                comparison_json JSONB NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
