namespace Grounded.Api.Services;

/// <summary>
/// JSON Schema for structured-output planner responses.
/// Enforces shape only — no allow-lists, no business rules.
/// QueryPlanValidator remains the authority for all semantic constraints.
/// </summary>
public static class QueryPlanSchema
{
    public const string Name = "QueryPlan";

    public const string Json = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "additionalProperties": false,
          "required": ["version","questionType","metric","dimension","filters","timeRange","timeGrain","sort","limit","usePriorState","resolvedFrom","confidence"],
          "properties": {
            "version": { "type": "string" },
            "questionType": {
              "type": "string",
              "enum": ["aggregate","grouped_breakdown","ranking","time_series"]
            },
            "metric": { "type": "string" },
            "dimension": { "type": ["string","null"] },
            "filters": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["field","operator","values"],
                "properties": {
                  "field": { "type": "string" },
                  "operator": {
                    "type": "string",
                    "enum": ["eq","in"]
                  },
                  "values": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                }
              }
            },
            "timeRange": {
              "type": "object",
              "additionalProperties": false,
              "required": ["preset","startDate","endDate"],
              "properties": {
                "preset": { "type": "string" },
                "startDate": { "type": ["string","null"] },
                "endDate": { "type": ["string","null"] }
              }
            },
            "timeGrain": { "type": ["string","null"] },
            "sort": {
              "type": "object",
              "additionalProperties": false,
              "required": ["by","direction"],
              "properties": {
                "by": { "type": "string", "enum": ["metric","dimension"] },
                "direction": { "type": "string", "enum": ["asc","desc"] }
              }
            },
            "limit": { "type": ["integer","null"] },
            "usePriorState": { "type": "boolean" },
            "resolvedFrom": { "type": ["string","null"] },
            "confidence": { "type": ["number","null"] }
          }
        }
        """;
}
