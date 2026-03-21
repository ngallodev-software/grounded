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

            queryPlan = NormalizeAliases(queryPlan);
            return new(true, queryPlan, null, FailureCategories.None, content, null, false, false);
        }
        catch (JsonException exception)
        {
            return new(false, null, exception.Message, FailureCategories.JsonParseFailure, content, null, false, false);
        }
    }

    // Normalize common LLM synonym/alias variations to canonical field names.
    private static QueryPlan NormalizeAliases(QueryPlan plan)
    {
        var metricResolution = ResolveMetric(plan.Metric);
        var dimensionResolution = ResolveDimension(plan.Dimension);

        var metric = metricResolution.Canonical;
        var dimension = dimensionResolution.Canonical;

        var resolvedFromParts = new List<string>();
        if (metricResolution.WasResolved && metricResolution.Original is not null)
        {
            resolvedFromParts.Add(metricResolution.Original);
        }

        if (dimensionResolution.WasResolved && dimensionResolution.Original is not null)
        {
            resolvedFromParts.Add(dimensionResolution.Original);
        }

        var resolvedFrom = resolvedFromParts.Count switch
        {
            0 => plan.ResolvedFrom,
            1 => resolvedFromParts[0],
            _ => string.Join("; ", resolvedFromParts)
        };

        var confidence = plan.Confidence ?? EstimateConfidence(metric, dimension, resolvedFromParts.Count);

        if (ReferenceEquals(metric, plan.Metric) &&
            ReferenceEquals(dimension, plan.Dimension) &&
            string.Equals(resolvedFrom, plan.ResolvedFrom, StringComparison.Ordinal) &&
            confidence == plan.Confidence)
        {
            return plan;
        }

        return plan with
        {
            Metric = metric,
            Dimension = dimension,
            ResolvedFrom = resolvedFrom,
            Confidence = confidence
        };
    }

    private static (string Canonical, string? Original, bool WasResolved) ResolveMetric(string metric) => metric switch
    {
        "avg_order_value" => ("average_order_value", metric, true),
        "aov" => ("average_order_value", metric, true),
        "orders" => ("order_count", metric, true),
        "num_orders" => ("order_count", metric, true),
        "number_of_orders" => ("order_count", metric, true),
        "units" => ("units_sold", metric, true),
        "quantity_sold" => ("units_sold", metric, true),
        "new_customers" => ("new_customer_count", metric, true),
        _ => (metric, null, false)
    };

    private static (string? Canonical, string? Original, bool WasResolved) ResolveDimension(string? dimension) => dimension switch
    {
        "category" => ("product_category", dimension, true),
        "subcategory" => ("product_subcategory", dimension, true),
        "channel" => ("sales_channel", dimension, true),
        "region" => ("shipping_region", dimension, true),
        "segment" => ("customer_segment", dimension, true),
        "customer" => ("customer_name", dimension, true),
        "product" => ("product_name", dimension, true),
        _ => (dimension, null, false)
    };

    private static decimal EstimateConfidence(string metric, string? dimension, int resolutionCount)
    {
        if (string.Equals(metric, "__unsupported__", StringComparison.Ordinal))
        {
            return 0.35m;
        }

        if (resolutionCount >= 2)
        {
            return 0.89m;
        }

        if (resolutionCount == 1)
        {
            return 0.92m;
        }

        return dimension is null ? 0.98m : 0.96m;
    }
}
