using System.Globalization;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class QueryPlanValidator
{
    public ValidationResult Validate(QueryPlan plan)
    {
        var errors = new List<ValidationError>();

        if (!string.Equals(plan.Version, SqlFragmentRegistry.QueryPlanVersion, StringComparison.Ordinal))
        {
            errors.Add(new("invalid_version", "version must be '1.0'"));
        }

        ValidateQuestionType(plan, errors);
        ValidateMetric(plan, errors);
        ValidateDimension(plan, errors);
        ValidateFilters(plan, errors);
        ValidateTimeRange(plan, errors);
        ValidateLimit(plan, errors);
        ValidateTimeGrain(plan, errors);
        ValidateSort(plan, errors);
        ValidateCombinations(plan, errors);

        return new(errors.Count == 0, errors);
    }

    private static void ValidateQuestionType(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (!SqlFragmentRegistry.SupportedQuestionTypes.Contains(plan.QuestionType))
        {
            errors.Add(new("invalid_question_type", $"questionType '{plan.QuestionType}' is not supported"));
            return;
        }

        if (string.Equals(plan.QuestionType, "simple_follow_up", StringComparison.Ordinal))
        {
            errors.Add(new("unsupported_question_type", "questionType 'simple_follow_up' is not executable in Phase 2"));
        }
    }

    private static void ValidateMetric(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (!SqlFragmentRegistry.SupportedMetrics.Contains(plan.Metric))
        {
            errors.Add(new("invalid_metric", $"metric '{plan.Metric}' is not supported"));
        }
    }

    private static void ValidateDimension(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (plan.Dimension is not null && !SqlFragmentRegistry.SupportedDimensions.Contains(plan.Dimension))
        {
            errors.Add(new("invalid_dimension", $"dimension '{plan.Dimension}' is not supported"));
            return;
        }

        switch (plan.QuestionType)
        {
            case "aggregate" when plan.Dimension is not null:
                errors.Add(new("invalid_dimension", "questionType 'aggregate' requires dimension = null"));
                break;
            case "grouped_breakdown" when plan.Dimension is null:
                errors.Add(new("invalid_dimension", "questionType 'grouped_breakdown' requires exactly one non-null dimension"));
                break;
            case "ranking" when plan.Dimension is null:
                errors.Add(new("invalid_dimension", "questionType 'ranking' requires exactly one non-null dimension"));
                break;
            case "time_series" when plan.Dimension is not null:
                errors.Add(new("invalid_dimension", "questionType 'time_series' requires dimension = null"));
                break;
        }
    }

    private static void ValidateFilters(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (plan.Filters.Count > 8)
        {
            errors.Add(new("invalid_filter_count", "a maximum of 8 filters is allowed"));
        }

        var seenSerialized = new HashSet<string>(StringComparer.Ordinal);
        var seenFields = new HashSet<string>(StringComparer.Ordinal);

        foreach (var filter in plan.Filters)
        {
            if (!SqlFragmentRegistry.SupportedFilterFields.Contains(filter.Field))
            {
                errors.Add(new("invalid_filter_field", $"filter field '{filter.Field}' is not supported"));
                continue;
            }

            if (!SqlFragmentRegistry.SupportedOperators.Contains(filter.Operator))
            {
                errors.Add(new("invalid_filter_operator", $"filter operator '{filter.Operator}' is not supported"));
                continue;
            }

            if (!seenFields.Add(filter.Field))
            {
                errors.Add(new("duplicate_filter_field", $"only one filter per field is allowed; '{filter.Field}' was repeated"));
            }

            var serialized = $"{filter.Field}|{filter.Operator}|{string.Join('\u001f', filter.Values)}";
            if (!seenSerialized.Add(serialized))
            {
                errors.Add(new("duplicate_filter", $"duplicate filter detected for field '{filter.Field}'"));
            }

            if (filter.Operator == "eq" && filter.Values.Count != 1)
            {
                errors.Add(new("invalid_filter_values", $"filter field '{filter.Field}' with operator 'eq' requires exactly one value"));
                continue;
            }

            if (filter.Operator == "in" && (filter.Values.Count < 1 || filter.Values.Count > 20))
            {
                errors.Add(new("invalid_filter_values", $"filter field '{filter.Field}' with operator 'in' requires between 1 and 20 values"));
                continue;
            }

            if (filter.Values.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add(new("invalid_filter_values", $"filter field '{filter.Field}' contains an empty value"));
                continue;
            }

            ValidateFilterValueWhitelist(plan, filter, errors);
        }
    }

    private static void ValidateFilterValueWhitelist(QueryPlan plan, FilterSpec filter, ICollection<ValidationError> errors)
    {
        IReadOnlySet<string>? allowedValues = filter.Field switch
        {
            "customer_region" or "shipping_region" => new HashSet<string>(StringComparer.Ordinal) { "West", "Central", "East", "South" },
            "customer_segment" => new HashSet<string>(StringComparer.Ordinal) { "Consumer", "SMB", "Enterprise" },
            "acquisition_channel" => new HashSet<string>(StringComparer.Ordinal) { "Organic", "Paid Search", "Email", "Affiliate", "Social" },
            "product_category" => new HashSet<string>(StringComparer.Ordinal) { "Electronics", "Home", "Office", "Fitness", "Accessories" },
            "sales_channel" => new HashSet<string>(StringComparer.Ordinal) { "Web", "Mobile", "Marketplace" },
            "customer_type" => new HashSet<string>(StringComparer.Ordinal) { "new", "existing" },
            _ => null
        };

        if (allowedValues is not null)
        {
            var invalidValue = filter.Values.FirstOrDefault(value => !allowedValues.Contains(value));
            if (invalidValue is not null)
            {
                errors.Add(new("invalid_filter_values", $"filter field '{filter.Field}' contains unsupported value '{invalidValue}'"));
            }
        }

        if (string.Equals(filter.Field, "customer_type", StringComparison.Ordinal) &&
            string.Equals(plan.Metric, "new_customer_count", StringComparison.Ordinal) &&
            filter.Values.Contains("existing", StringComparer.Ordinal))
        {
            errors.Add(new("invalid_filter_values", "metric 'new_customer_count' cannot be combined with customer_type 'existing'"));
        }

        if (string.Equals(filter.Field, "customer_type", StringComparison.Ordinal) &&
            string.Equals(plan.TimeRange.Preset, "all_time", StringComparison.Ordinal))
        {
            errors.Add(new("invalid_time_range", "customer_type filters require a bounded time range"));
        }
    }

    private static void ValidateTimeRange(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (!SqlFragmentRegistry.SupportedTimePresets.Contains(plan.TimeRange.Preset))
        {
            errors.Add(new("invalid_time_range", $"timeRange.preset '{plan.TimeRange.Preset}' is not supported"));
            return;
        }

        if (string.Equals(plan.TimeRange.Preset, "custom_range", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(plan.TimeRange.StartDate) || string.IsNullOrWhiteSpace(plan.TimeRange.EndDate))
            {
                errors.Add(new("invalid_time_range", "custom_range requires both startDate and endDate"));
                return;
            }

            if (!DateOnly.TryParseExact(plan.TimeRange.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
                !DateOnly.TryParseExact(plan.TimeRange.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            {
                errors.Add(new("invalid_time_range", "custom_range startDate and endDate must use ISO YYYY-MM-DD"));
                return;
            }

            if (startDate > endDate)
            {
                errors.Add(new("invalid_time_range", "custom_range startDate must be less than or equal to endDate"));
            }

            return;
        }

        if (plan.TimeRange.StartDate is not null || plan.TimeRange.EndDate is not null)
        {
            errors.Add(new("invalid_time_range", $"preset '{plan.TimeRange.Preset}' requires startDate = null and endDate = null"));
        }
    }

    private static void ValidateLimit(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (string.Equals(plan.QuestionType, "ranking", StringComparison.Ordinal))
        {
            if (plan.Limit is null || plan.Limit < 1 || plan.Limit > 50)
            {
                errors.Add(new("invalid_limit", "questionType 'ranking' requires limit between 1 and 50"));
            }

            return;
        }

        if (plan.Limit is not null)
        {
            errors.Add(new("invalid_limit", $"questionType '{plan.QuestionType}' requires limit = null"));
        }
    }

    private static void ValidateTimeGrain(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (string.Equals(plan.QuestionType, "time_series", StringComparison.Ordinal))
        {
            if (plan.TimeGrain is null)
            {
                errors.Add(new("invalid_time_grain", "questionType 'time_series' requires timeGrain"));
                return;
            }

            if (!SqlFragmentRegistry.SupportedTimeGrains.Contains(plan.TimeGrain))
            {
                errors.Add(new("invalid_time_grain", $"timeGrain '{plan.TimeGrain}' is not supported"));
            }

            return;
        }

        if (plan.TimeGrain is not null)
        {
            errors.Add(new("invalid_time_grain", $"questionType '{plan.QuestionType}' requires timeGrain = null"));
        }
    }

    private static void ValidateSort(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (!SqlFragmentRegistry.SupportedSortBy.Contains(plan.Sort.By))
        {
            errors.Add(new("invalid_sort", $"sort.by '{plan.Sort.By}' is not supported"));
        }

        if (!SqlFragmentRegistry.SupportedSortDirections.Contains(plan.Sort.Direction))
        {
            errors.Add(new("invalid_sort", $"sort.direction '{plan.Sort.Direction}' is not supported"));
        }
    }

    private static void ValidateCombinations(QueryPlan plan, ICollection<ValidationError> errors)
    {
        if (plan.UsePriorState)
        {
            errors.Add(new("invalid_use_prior_state", "usePriorState must be false in Phase 2"));
        }

        if (string.Equals(plan.QuestionType, "ranking", StringComparison.Ordinal) &&
            !string.Equals(plan.Sort.By, "metric", StringComparison.Ordinal))
        {
            errors.Add(new("invalid_sort", "questionType 'ranking' requires sort.by = 'metric'"));
        }

        if (string.Equals(plan.QuestionType, "aggregate", StringComparison.Ordinal) &&
            string.Equals(plan.Metric, "average_order_value", StringComparison.Ordinal) &&
            string.Equals(plan.Sort.By, "dimension", StringComparison.Ordinal))
        {
            errors.Add(new("invalid_sort", "metric 'average_order_value' cannot sort by dimension when questionType = 'aggregate'"));
        }
    }
}
