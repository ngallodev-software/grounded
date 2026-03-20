using System.Text.RegularExpressions;

namespace Grounded.Api.Services;

internal static class DatabaseSchemaSettings
{
    private static readonly Regex SchemaNamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public const string DefaultAppSchema = "grounded";

    public static string GetAppSchema(IConfiguration configuration)
    {
        var configured = configuration["Database:AppSchema"];
        var schema = string.IsNullOrWhiteSpace(configured) ? DefaultAppSchema : configured.Trim();

        if (!SchemaNamePattern.IsMatch(schema))
        {
            throw new InvalidOperationException($"Database app schema '{schema}' is invalid. Use an unquoted PostgreSQL identifier.");
        }

        return schema;
    }

    public static string BuildDefaultSearchPath(string appSchema) => $"{appSchema},public";
}
