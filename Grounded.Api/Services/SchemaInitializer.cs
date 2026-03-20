namespace Grounded.Api.Services;

public sealed class SchemaInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SchemaInitializer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<INpgsqlConnectionFactory>();
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
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

            CREATE TABLE IF NOT EXISTS conversation_states (
                conversation_id TEXT PRIMARY KEY,
                question_type TEXT NOT NULL,
                metric TEXT NOT NULL,
                dimension TEXT NULL,
                filters_json JSONB NOT NULL,
                time_range_json JSONB NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
