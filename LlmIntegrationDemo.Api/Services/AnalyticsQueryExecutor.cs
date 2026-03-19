using System.Data;
using System.Diagnostics;
using LlmIntegrationDemo.Api.Models;
using Npgsql;

namespace LlmIntegrationDemo.Api.Services;

public interface INpgsqlConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory
{
    private readonly IConfiguration _configuration;

    public NpgsqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public NpgsqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("AnalyticsDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'AnalyticsDatabase' is not configured.");
        }

        return new(connectionString);
    }
}

public interface IAnalyticsQueryExecutor
{
    Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken);
}

public sealed class AnalyticsQueryExecutor : IAnalyticsQueryExecutor
{
    private readonly INpgsqlConnectionFactory _connectionFactory;
    private readonly SqlSafetyGuard _sqlSafetyGuard;

    public AnalyticsQueryExecutor(INpgsqlConnectionFactory connectionFactory, SqlSafetyGuard sqlSafetyGuard)
    {
        _connectionFactory = connectionFactory;
        _sqlSafetyGuard = sqlSafetyGuard;
    }

    public async Task<QueryExecutionResult> ExecuteAsync(CompiledQuery compiledQuery, ResolvedTimeRange resolvedTimeRange, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SET statement_timeout = 15000";
            command.CommandTimeout = 15;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SET TRANSACTION READ ONLY";
            command.CommandTimeout = 15;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        var rows = new List<IReadOnlyDictionary<string, object?>>();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = _sqlSafetyGuard.SanitizeForExecution(compiledQuery.Sql);
            command.CommandTimeout = 15;

            foreach (var parameter in compiledQuery.Parameters)
            {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }

                rows.Add(row);
                if (rows.Count > compiledQuery.EffectiveLimit)
                {
                    throw new InvalidOperationException("Query returned more rows than the compiled row cap.");
                }
            }
        }

        await transaction.CommitAsync(cancellationToken);
        stopwatch.Stop();

        return new(
            rows,
            new(
                compiledQuery.Sql,
                new Dictionary<string, object?>(compiledQuery.Parameters, StringComparer.Ordinal),
                rows.Count,
                stopwatch.ElapsedMilliseconds,
                compiledQuery.EffectiveLimit,
                resolvedTimeRange.RangeStartUtc,
                resolvedTimeRange.RangeEndExclusiveUtc));
    }
}
