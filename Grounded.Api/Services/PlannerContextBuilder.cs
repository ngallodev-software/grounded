using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class PlannerContextBuilder
{
    private readonly SqlFragmentRegistry _sqlFragmentRegistry;

    public PlannerContextBuilder(SqlFragmentRegistry sqlFragmentRegistry)
    {
        _sqlFragmentRegistry = sqlFragmentRegistry;
    }

    public PlannerContext Build()
    {
        var filterValueAllowList = _sqlFragmentRegistry.GetPlannerFilterValueAllowList();
        var filters = SqlFragmentRegistry.SupportedFilterFields
            .OrderBy(static field => field, StringComparer.Ordinal)
            .Select(field => new PlannerFilterDefinition(
                field,
                SqlFragmentRegistry.SupportedOperators.OrderBy(static op => op, StringComparer.Ordinal).ToArray(),
                filterValueAllowList.TryGetValue(field, out var allowedValues)
                    ? allowedValues?.OrderBy(static value => value, StringComparer.Ordinal).ToArray()
                    : null))
            .ToArray();

        var schema = new[]
        {
            new PlannerSchemaEntity("customers", ["segment", "region", "acquisition_channel"]),
            new PlannerSchemaEntity("products", ["product_name", "category", "subcategory"]),
            new PlannerSchemaEntity("orders", ["order_date", "sales_channel", "shipping_region", "status"]),
            new PlannerSchemaEntity("order_items", ["quantity", "unit_price", "discount_amount"])
        };

        var examples = new[]
        {
            new PlannerExample(
                "What was total revenue last month?",
                new QueryPlan("1.0", "aggregate", null, [], "revenue", new("last_month", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Show revenue by product category for the last 90 days where sales channel is Web.",
                new QueryPlan("1.0", "grouped_breakdown", "product_category", [new("sales_channel", "eq", ["Web"])], "revenue", new("last_90_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Top 5 products by units sold this year.",
                new QueryPlan("1.0", "ranking", "product_name", [], "units_sold", new("year_to_date", null, null), null, new("metric", "desc"), 5, false)),
            new PlannerExample(
                "Monthly revenue for the last 6 months.",
                new QueryPlan("1.0", "time_series", null, [], "revenue", new("last_6_months", null, null), "month", new("metric", "desc"), null, false)),
            new PlannerExample(
                "Show gross margin by channel last month.",
                new QueryPlan("1.0", "aggregate", null, [], "__unsupported__", new("last_30_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Revenue by shipping region last year.",
                new QueryPlan("1.0", "grouped_breakdown", "shipping_region", [], "revenue", new("last_year", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Revenue by customer region last quarter.",
                new QueryPlan("1.0", "grouped_breakdown", "customer_region", [], "revenue", new("last_quarter", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "SELECT product_name, SUM(quantity) FROM order_items GROUP BY product_name;",
                new QueryPlan("1.0", "aggregate", null, [], "__unsupported__", new("last_30_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Top products by units sold this year.",
                new QueryPlan("1.0", "aggregate", null, [], "__unsupported__", new("last_30_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Show revenue by product category where country is Canada.",
                new QueryPlan("1.0", "aggregate", null, [], "__unsupported__", new("last_30_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Show revenue by product category where sales channel is Retail.",
                new QueryPlan("1.0", "aggregate", null, [], "__unsupported__", new("last_30_days", null, null), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Top 5 customers by order count last year.",
                new QueryPlan("1.0", "ranking", "customer_name", [], "order_count", new("last_year", null, null), null, new("metric", "desc"), 5, false)),
            new PlannerExample(
                "What was total revenue in 2024?",
                new QueryPlan("1.0", "aggregate", null, [], "revenue", new("custom_range", "2024-01-01", "2024-12-31"), null, new("metric", "desc"), null, false)),
            new PlannerExample(
                "Show units sold by category for 2025.",
                new QueryPlan("1.0", "grouped_breakdown", "product_category", [], "units_sold", new("custom_range", "2025-01-01", "2025-12-31"), null, new("metric", "desc"), null, false))
        };

        return new PlannerContext(
            SqlFragmentRegistry.SupportedQuestionTypes
                .Where(static value => !string.Equals(value, "simple_follow_up", StringComparison.Ordinal))
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray(),
            SqlFragmentRegistry.SupportedMetrics.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            SqlFragmentRegistry.SupportedDimensions.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            filters,
            SqlFragmentRegistry.SupportedOperators.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            SqlFragmentRegistry.SupportedTimePresets.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            SqlFragmentRegistry.SupportedTimeGrains.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            SqlFragmentRegistry.SupportedSortBy.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            SqlFragmentRegistry.SupportedSortDirections.OrderBy(static value => value, StringComparer.Ordinal).ToArray(),
            schema,
            examples);
    }
}
