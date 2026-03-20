using System.Text.Json;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class PlannerResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PlannerParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new(false, null, "planner response was empty", FailureCategories.JsonParseFailure, content, null, false, false);
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new(false, null, "planner response must be a single JSON object", FailureCategories.JsonParseFailure, content, null, false, false);
            }

            var queryPlan = document.RootElement.Deserialize<QueryPlan>(SerializerOptions);
            if (queryPlan is null)
            {
                return new(false, null, "planner response did not deserialize into a query plan", FailureCategories.JsonParseFailure, content, null, false, false);
            }

            return new(true, queryPlan, null, FailureCategories.None, content, null, false, false);
        }
        catch (JsonException exception)
        {
            return new(false, null, exception.Message, FailureCategories.JsonParseFailure, content, null, false, false);
        }
    }
}
