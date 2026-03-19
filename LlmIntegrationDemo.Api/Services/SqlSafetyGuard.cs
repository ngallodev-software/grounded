using System.Text.RegularExpressions;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class SqlSafetyGuard
{
    private static readonly Regex DisallowedKeywordPattern = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|GRANT|REVOKE|CALL|COPY|DO)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ValidationResult Validate(CompiledQuery compiledQuery)
    {
        var errors = new List<ValidationError>();
        var sql = compiledQuery.Sql.Trim();

        if (!(sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
              sql.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new("unsafe_sql", "compiled SQL must start with SELECT or WITH"));
        }

        var semicolonCount = sql.Count(character => character == ';');
        if (semicolonCount > 1 || (semicolonCount == 1 && !sql.EndsWith(";", StringComparison.Ordinal)))
        {
            errors.Add(new("unsafe_sql", "compiled SQL must contain exactly one statement"));
        }

        if (DisallowedKeywordPattern.IsMatch(sql))
        {
            errors.Add(new("unsafe_sql", "compiled SQL contains a disallowed keyword"));
        }

        if (compiledQuery.EffectiveLimit is < 1 or > 366)
        {
            errors.Add(new("unsafe_sql", "compiled SQL row cap is outside the allowed range"));
        }

        if (compiledQuery.Parameters.Any(parameter => string.IsNullOrWhiteSpace(parameter.Key)))
        {
            errors.Add(new("unsafe_sql", "compiled SQL parameters must use named bindings"));
        }

        return new(errors.Count == 0, errors);
    }

    public string SanitizeForExecution(string sql) => sql.Trim().TrimEnd(';');
}
