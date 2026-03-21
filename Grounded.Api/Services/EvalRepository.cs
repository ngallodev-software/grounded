using System.Text.Json;
using Grounded.Api.Models;
using NpgsqlTypes;

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
        command.Parameters.Add("case_results_json", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(run.CaseResults, SerializerOptions);
        command.Parameters.Add("comparison_json", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(run.Comparison, SerializerOptions);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

}
