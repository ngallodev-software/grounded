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

        var resolvedFrom = MergeResolvedFrom(
            plan.ResolvedFrom,
            metricResolution.WasResolved ? metricResolution.Original : null,
            dimensionResolution.WasResolved ? dimensionResolution.Original : null);

        var confidence = plan.Confidence ?? EstimateConfidence(
            metric,
            dimension,
            hasMetricResolution: metricResolution.WasResolved,
            hasDimensionResolution: dimensionResolution.WasResolved,
            isBorderlineInference: false);

        if (ReferenceEquals(metric, plan.Metric) &&
            ReferenceEquals(dimension, plan.Dimension) &&
            Equals(resolvedFrom, plan.ResolvedFrom) &&
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

    public static QueryPlan EnrichFromQuestion(QueryPlan plan, string userQuestion)
    {
        var text = userQuestion.Trim();
        var metricAlias = plan.ResolvedFrom?.Metric ?? DetectMetricAlias(text, plan.Metric);
        var dimensionAlias = plan.ResolvedFrom?.Dimension ?? DetectDimensionAlias(text, plan.Dimension);

        var resolvedFrom = metricAlias is null && dimensionAlias is null
            ? plan.ResolvedFrom
            : new ResolvedFromSpec(metricAlias, dimensionAlias);

        var confidence = plan.Confidence ?? EstimateConfidence(
            plan.Metric,
            plan.Dimension,
            hasMetricResolution: metricAlias is not null,
            hasDimensionResolution: dimensionAlias is not null,
            isBorderlineInference: IsBorderlineInference(text, plan, metricAlias, dimensionAlias));

        return plan with
        {
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

    private static ResolvedFromSpec? MergeResolvedFrom(ResolvedFromSpec? existing, string? metric, string? dimension)
    {
        var mergedMetric = metric ?? existing?.Metric;
        var mergedDimension = dimension ?? existing?.Dimension;
        return mergedMetric is null && mergedDimension is null ? existing : new ResolvedFromSpec(mergedMetric, mergedDimension);
    }

    private static string? DetectMetricAlias(string question, string metric)
    {
        var lower = question.ToLowerInvariant();
        return metric switch
        {
            "order_count" when lower.Contains("orders") || lower.Contains("order count") || lower.Contains("number of orders") || lower.Contains("order volume") => "orders",
            "units_sold" when lower.Contains("units sold") || lower.Contains("units") || lower.Contains("quantity sold") => lower.Contains("quantity sold") ? "quantity sold" : "units",
            "average_order_value" when lower.Contains("aov") => "aov",
            "average_order_value" when lower.Contains("avg order value") => "avg order value",
            "average_order_value" when lower.Contains("average order value") => "average order value",
            "new_customer_count" when lower.Contains("new customers") => "new customers",
            _ => null
        };
    }

    private static string? DetectDimensionAlias(string question, string? dimension)
    {
        if (dimension is null)
        {
            return null;
        }

        var lower = question.ToLowerInvariant();
        return dimension switch
        {
            "product_category" when lower.Contains(" by category") || lower.Contains(" categories") || lower.Contains(" category ") || lower.EndsWith("category.") || lower.EndsWith("category") => "category",
            "product_subcategory" when lower.Contains("subcategory") => "subcategory",
            "product_name" when lower.Contains(" by product") || lower.Contains(" top products") || lower.Contains(" products ") => "product",
            "sales_channel" when lower.Contains(" by channel") || lower.Contains(" channel ") || lower.EndsWith("channel.") || lower.EndsWith("channel") => "channel",
            "customer_region" when lower.Contains(" customer region") || lower.Contains(" by region") || lower.Contains(" region ") => "region",
            "customer_segment" when lower.Contains("segment") => "segment",
            _ => null
        };
    }

    private static bool IsBorderlineInference(string question, QueryPlan plan, string? metricAlias, string? dimensionAlias)
    {
        var lower = question.ToLowerInvariant();
        if (string.Equals(plan.Metric, "__unsupported__", StringComparison.Ordinal))
        {
            return false;
        }

        if (plan.QuestionType == "ranking" && plan.Limit == 5 &&
            !(lower.Contains("top 5") || lower.Contains("bottom 5") || lower.Contains("5 ")))
        {
            return true;
        }

        return metricAlias is not null || dimensionAlias is not null;
    }

    private static decimal EstimateConfidence(string metric, string? dimension, bool hasMetricResolution, bool hasDimensionResolution, bool isBorderlineInference)
    {
        if (string.Equals(metric, "__unsupported__", StringComparison.Ordinal))
        {
            return 0.35m;
        }

        if (isBorderlineInference)
        {
            return hasMetricResolution || hasDimensionResolution ? 0.72m : 0.68m;
        }

        if (hasMetricResolution && hasDimensionResolution)
        {
            return 0.82m;
        }

        if (hasMetricResolution || hasDimensionResolution)
        {
            return 0.87m;
        }

        return dimension is null ? 0.97m : 0.95m;
    }
}
