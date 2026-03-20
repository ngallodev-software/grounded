using System.Text;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class QueryPlanCompiler
{
    private readonly SqlFragmentRegistry _registry;

    public QueryPlanCompiler(SqlFragmentRegistry registry)
    {
        _registry = registry;
    }

    public CompiledQuery Compile(QueryPlan plan, ResolvedTimeRange resolvedTimeRange)
    {
        var metricSpec = _registry.GetMetric(plan.Metric);
        return metricSpec.UsesNewCustomerFlow
            ? CompileNewCustomerQuery(plan, metricSpec, resolvedTimeRange)
            : CompileStandardQuery(plan, metricSpec, resolvedTimeRange);
    }

    private CompiledQuery CompileStandardQuery(QueryPlan plan, MetricSqlSpec metricSpec, ResolvedTimeRange resolvedTimeRange)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var sql = new StringBuilder();
        var whereClauses = new List<string> { "o.status = 'Completed'" };
        var joins = new List<string>();
        var ctes = new List<string>();

        var dimensionSpec = plan.Dimension is null ? null : _registry.GetDimension(plan.Dimension);
        var requiresCustomerJoin = dimensionSpec?.RequiresCustomerJoin == true;
        var requiresOrderItemsJoin = metricSpec.RequiresOrderItems || dimensionSpec?.RequiresOrderItemsJoin == true;
        var requiresProductsJoin = dimensionSpec?.RequiresProductsJoin == true;

        foreach (var filter in plan.Filters)
        {
            var filterSpec = _registry.GetFilter(filter.Field);
            requiresCustomerJoin |= filterSpec.Target == FilterTarget.Customer;
            requiresOrderItemsJoin |= filterSpec.Target == FilterTarget.Product;
            requiresProductsJoin |= filterSpec.Target == FilterTarget.Product;
        }

        var hasCustomerTypeFilter = plan.Filters.Any(filter => string.Equals(filter.Field, "customer_type", StringComparison.Ordinal));
        if (hasCustomerTypeFilter)
        {
            ctes.Add("""
                first_completed_orders AS (
                    SELECT o.customer_id, MIN(o.order_date) AS first_completed_at
                    FROM orders o
                    WHERE o.status = 'Completed'
                    GROUP BY o.customer_id
                )
                """);
            joins.Add("JOIN first_completed_orders fco ON fco.customer_id = o.customer_id");
        }

        if (requiresOrderItemsJoin)
        {
            joins.Add("JOIN order_items oi ON oi.order_id = o.id");
        }

        if (requiresCustomerJoin)
        {
            joins.Add("JOIN customers c ON c.id = o.customer_id");
        }

        if (requiresProductsJoin)
        {
            joins.Add("JOIN products p ON p.id = oi.product_id");
        }

        AddTimeRangePredicate(whereClauses, parameters, resolvedTimeRange, "o.order_date");
        AddFilterPredicates(plan.Filters, whereClauses, parameters, allowCustomerType: hasCustomerTypeFilter);

        AppendCtes(sql, ctes);
        sql.AppendLine($"SELECT {BuildSelectList(plan, metricSpec.Expression, dimensionSpec, "o.order_date")}");
        sql.AppendLine("FROM orders o");
        foreach (var join in joins.Distinct(StringComparer.Ordinal))
        {
            sql.AppendLine(join);
        }

        sql.AppendLine($"WHERE {string.Join(" AND ", whereClauses)}");
        AppendGroupingOrderingAndLimit(sql, plan, dimensionSpec, "o.order_date");

        return new(
            sql.ToString().Trim(),
            parameters,
            DetermineEffectiveLimit(plan.QuestionType, plan.Limit),
            ReturnsDimensionColumn: plan.Dimension is not null,
            ReturnsTimeBucketColumn: string.Equals(plan.QuestionType, "time_series", StringComparison.Ordinal));
    }

    private CompiledQuery CompileNewCustomerQuery(QueryPlan plan, MetricSqlSpec metricSpec, ResolvedTimeRange resolvedTimeRange)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var sql = new StringBuilder();
        var scopedFilters = new List<string>();
        var dimensionSpec = plan.Dimension is null ? null : _registry.GetDimension(plan.Dimension);

        var requiresOrderItemsJoin = dimensionSpec?.RequiresOrderItemsJoin == true ||
                                     plan.Filters.Any(filter => _registry.GetFilter(filter.Field).Target == FilterTarget.Product);
        var requiresProductsJoin = dimensionSpec?.RequiresProductsJoin == true ||
                                   plan.Filters.Any(filter => _registry.GetFilter(filter.Field).Target == FilterTarget.Product);

        AddTimeRangePredicate(scopedFilters, parameters, resolvedTimeRange, "fo.first_completed_at");
        AddFilterPredicates(plan.Filters, scopedFilters, parameters, allowCustomerType: false, sourceAlias: null, skipCustomerType: true);

        sql.AppendLine("WITH first_completed_orders AS (");
        sql.AppendLine("    SELECT o.customer_id, MIN(o.order_date) AS first_completed_at");
        sql.AppendLine("    FROM orders o");
        sql.AppendLine("    WHERE o.status = 'Completed'");
        sql.AppendLine("    GROUP BY o.customer_id");
        sql.AppendLine("),");
        sql.AppendLine("scoped_first_orders AS (");
        sql.AppendLine("    SELECT");
        sql.AppendLine("        o.id,");
        sql.AppendLine("        o.customer_id,");
        sql.AppendLine("        fo.first_completed_at,");
        sql.AppendLine("        o.sales_channel,");
        sql.AppendLine("        o.shipping_region,");
        sql.AppendLine("        c.region,");
        sql.AppendLine("        c.segment,");
        sql.AppendLine($"        c.acquisition_channel{(requiresOrderItemsJoin ? "," : string.Empty)}");
        if (requiresOrderItemsJoin)
        {
            sql.AppendLine($"        oi.product_id{(requiresProductsJoin ? "," : string.Empty)}");
        }

        if (requiresProductsJoin)
        {
            sql.AppendLine("        p.category,");
            sql.AppendLine("        p.subcategory,");
            sql.AppendLine("        p.product_name");
        }

        sql.AppendLine("    FROM first_completed_orders fo");
        sql.AppendLine("    JOIN orders o ON o.customer_id = fo.customer_id AND o.order_date = fo.first_completed_at AND o.status = 'Completed'");
        sql.AppendLine("    JOIN customers c ON c.id = o.customer_id");
        if (requiresOrderItemsJoin)
        {
            sql.AppendLine("    JOIN order_items oi ON oi.order_id = o.id");
        }

        if (requiresProductsJoin)
        {
            sql.AppendLine("    JOIN products p ON p.id = oi.product_id");
        }

        if (scopedFilters.Count > 0)
        {
            sql.AppendLine($"    WHERE {string.Join(" AND ", scopedFilters)}");
        }

        sql.AppendLine(")");
        sql.AppendLine($"SELECT {BuildSelectList(plan, metricSpec.Expression.Replace("o.customer_id", "sfo.customer_id", StringComparison.Ordinal), dimensionSpec, "sfo.first_completed_at", "sfo")}");
        sql.AppendLine("FROM scoped_first_orders sfo");
        AppendGroupingOrderingAndLimit(sql, plan, dimensionSpec, "sfo.first_completed_at", "sfo");

        return new(
            sql.ToString().Trim(),
            parameters,
            DetermineEffectiveLimit(plan.QuestionType, plan.Limit),
            ReturnsDimensionColumn: plan.Dimension is not null,
            ReturnsTimeBucketColumn: string.Equals(plan.QuestionType, "time_series", StringComparison.Ordinal));
    }

    private string BuildSelectList(QueryPlan plan, string metricExpression, DimensionSqlSpec? dimensionSpec, string timeAnchorExpression, string? sourceAlias = null)
    {
        var segments = new List<string>();

        if (plan.Dimension is not null)
        {
            segments.Add($"{RewriteAlias(dimensionSpec!.SqlExpression, sourceAlias)} AS dimension");
        }

        if (string.Equals(plan.QuestionType, "time_series", StringComparison.Ordinal))
        {
            var grain = _registry.GetTimeGrain(plan.TimeGrain!);
            segments.Add($"DATE_TRUNC('{grain}', {timeAnchorExpression}) AS time_bucket");
        }

        segments.Add($"{RewriteAlias(metricExpression, sourceAlias)} AS metric");
        return string.Join(", ", segments);
    }

    private void AppendGroupingOrderingAndLimit(StringBuilder sql, QueryPlan plan, DimensionSqlSpec? dimensionSpec, string timeAnchorExpression, string? sourceAlias = null)
    {
        switch (plan.QuestionType)
        {
            case "grouped_breakdown":
                sql.AppendLine($"GROUP BY {RewriteAlias(dimensionSpec!.SqlExpression, sourceAlias)}");
                if (string.Equals(plan.Sort.By, "dimension", StringComparison.Ordinal))
                {
                    sql.AppendLine($"ORDER BY dimension {plan.Sort.Direction.ToUpperInvariant()}");
                }
                else
                {
                    sql.AppendLine($"ORDER BY metric {plan.Sort.Direction.ToUpperInvariant()}, dimension ASC");
                }

                sql.AppendLine("LIMIT 200");
                break;
            case "ranking":
                sql.AppendLine($"GROUP BY {RewriteAlias(dimensionSpec!.SqlExpression, sourceAlias)}");
                sql.AppendLine($"ORDER BY metric {plan.Sort.Direction.ToUpperInvariant()}, dimension ASC");
                sql.AppendLine($"LIMIT {plan.Limit!.Value}");
                break;
            case "time_series":
                var grain = _registry.GetTimeGrain(plan.TimeGrain!);
                sql.AppendLine($"GROUP BY DATE_TRUNC('{grain}', {timeAnchorExpression})");
                sql.AppendLine("ORDER BY time_bucket ASC");
                sql.AppendLine("LIMIT 366");
                break;
        }
    }

    private static int DetermineEffectiveLimit(string questionType, int? limit) =>
        questionType switch
        {
            "aggregate" => 1,
            "grouped_breakdown" => 200,
            "ranking" => limit ?? throw new InvalidOperationException("ranking requires limit"),
            "time_series" => 366,
            _ => throw new InvalidOperationException($"Unsupported questionType '{questionType}'.")
        };

    private static void AppendCtes(StringBuilder sql, IReadOnlyList<string> ctes)
    {
        if (ctes.Count == 0)
        {
            return;
        }

        sql.AppendLine("WITH");
        for (var index = 0; index < ctes.Count; index++)
        {
            var suffix = index == ctes.Count - 1 ? string.Empty : ",";
            sql.AppendLine($"{ctes[index].TrimEnd()}{suffix}");
        }
    }

    private void AddFilterPredicates(
        IReadOnlyList<FilterSpec> filters,
        ICollection<string> whereClauses,
        IDictionary<string, object?> parameters,
        bool allowCustomerType,
        string? sourceAlias = null,
        bool skipCustomerType = false)
    {
        foreach (var filter in filters)
        {
            var filterSpec = _registry.GetFilter(filter.Field);
            if (filterSpec.Target == FilterTarget.CustomerType)
            {
                if (skipCustomerType)
                {
                    continue;
                }

                if (!allowCustomerType)
                {
                    throw new InvalidOperationException("customer_type requires first completed order context.");
                }

                var values = filter.Values.Distinct(StringComparer.Ordinal).ToArray();
                if (values.Length == 2)
                {
                    continue;
                }

                whereClauses.Add(string.Equals(values[0], "new", StringComparison.Ordinal)
                    ? "fco.first_completed_at >= @rangeStartUtc AND fco.first_completed_at < @rangeEndExclusiveUtc"
                    : "fco.first_completed_at < @rangeStartUtc");
                continue;
            }

            var sqlExpression = RewriteAlias(filterSpec.SqlExpression, sourceAlias);
            var parameterName = $"p{parameters.Count}";
            if (string.Equals(filter.Operator, "eq", StringComparison.Ordinal))
            {
                parameters[parameterName] = filter.Values[0];
                whereClauses.Add($"{sqlExpression} = @{parameterName}");
            }
            else
            {
                parameters[parameterName] = filter.Values.ToArray();
                whereClauses.Add($"{sqlExpression} = ANY(@{parameterName})");
            }
        }
    }

    private static void AddTimeRangePredicate(
        ICollection<string> whereClauses,
        IDictionary<string, object?> parameters,
        ResolvedTimeRange resolvedTimeRange,
        string timeAnchorExpression)
    {
        if (resolvedTimeRange.RangeStartUtc is null || resolvedTimeRange.RangeEndExclusiveUtc is null)
        {
            return;
        }

        parameters["rangeStartUtc"] = resolvedTimeRange.RangeStartUtc.Value;
        parameters["rangeEndExclusiveUtc"] = resolvedTimeRange.RangeEndExclusiveUtc.Value;
        whereClauses.Add($"{timeAnchorExpression} >= @rangeStartUtc AND {timeAnchorExpression} < @rangeEndExclusiveUtc");
    }

    private static string RewriteAlias(string sqlExpression, string? sourceAlias)
    {
        if (string.IsNullOrWhiteSpace(sourceAlias))
        {
            return sqlExpression;
        }

        return sqlExpression
            .Replace("c.", $"{sourceAlias}.", StringComparison.Ordinal)
            .Replace("o.", $"{sourceAlias}.", StringComparison.Ordinal)
            .Replace("p.", $"{sourceAlias}.", StringComparison.Ordinal);
    }
}
