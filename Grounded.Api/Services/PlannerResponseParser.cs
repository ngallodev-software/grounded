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
        var metric = plan.Metric switch
        {
            "avg_order_value"        => "average_order_value",
            "aov"                    => "average_order_value",
            "orders"                 => "order_count",
            "num_orders"             => "order_count",
            "number_of_orders"       => "order_count",
            "units"                  => "units_sold",
            "quantity_sold"          => "units_sold",
            "new_customers"          => "new_customer_count",
            _                        => plan.Metric
        };

        var dimension = plan.Dimension switch
        {
            "category"               => "product_category",
            "subcategory"            => "product_subcategory",
            "channel"                => "sales_channel",
            "region"                 => "shipping_region",
            "segment"                => "customer_segment",
            "customer"               => "customer_name",
            "product"                => "product_name",
            _                        => plan.Dimension
        };

        if (ReferenceEquals(metric, plan.Metric) && ReferenceEquals(dimension, plan.Dimension))
            return plan;

        return plan with { Metric = metric, Dimension = dimension };
    }
}
