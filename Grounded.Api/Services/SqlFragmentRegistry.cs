using System.Collections.ObjectModel;

namespace Grounded.Api.Services;

internal enum FilterTarget
{
    Customer,
    Order,
    Product,
    CustomerType
}

internal sealed record MetricSqlSpec(
    string Expression,
    bool RequiresOrderItems,
    bool UsesNewCustomerFlow);

internal sealed record DimensionSqlSpec(
    string SqlExpression,
    bool RequiresCustomerJoin,
    bool RequiresOrderItemsJoin,
    bool RequiresProductsJoin);

internal sealed record FilterSqlSpec(
    string SqlExpression,
    FilterTarget Target,
    IReadOnlySet<string>? AllowedValues = null);

public sealed class SqlFragmentRegistry
{
    public const string QueryPlanVersion = "1.0";

    public static readonly IReadOnlySet<string> SupportedQuestionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "aggregate",
        "grouped_breakdown",
        "ranking",
        "time_series",
        "simple_follow_up"
    };

    public static readonly IReadOnlySet<string> SupportedMetrics = new HashSet<string>(StringComparer.Ordinal)
    {
        "revenue",
        "order_count",
        "units_sold",
        "average_order_value",
        "new_customer_count"
    };

    public static readonly IReadOnlySet<string> SupportedDimensions = new HashSet<string>(StringComparer.Ordinal)
    {
        "customer_region",
        "customer_segment",
        "acquisition_channel",
        "product_category",
        "product_subcategory",
        "product_name",
        "sales_channel",
        "shipping_region"
    };

    public static readonly IReadOnlySet<string> SupportedFilterFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "customer_region",
        "customer_segment",
        "acquisition_channel",
        "product_category",
        "product_subcategory",
        "product_name",
        "sales_channel",
        "shipping_region",
        "customer_type"
    };

    public static readonly IReadOnlySet<string> SupportedOperators = new HashSet<string>(StringComparer.Ordinal)
    {
        "eq",
        "in"
    };

    public static readonly IReadOnlySet<string> SupportedTimePresets = new HashSet<string>(StringComparer.Ordinal)
    {
        "last_7_days",
        "last_30_days",
        "last_90_days",
        "last_6_months",
        "last_12_months",
        "month_to_date",
        "quarter_to_date",
        "year_to_date",
        "last_month",
        "last_quarter",
        "last_year",
        "all_time",
        "custom_range"
    };

    public static readonly IReadOnlySet<string> SupportedTimeGrains = new HashSet<string>(StringComparer.Ordinal)
    {
        "day",
        "week",
        "month",
        "quarter"
    };

    public static readonly IReadOnlySet<string> SupportedSortBy = new HashSet<string>(StringComparer.Ordinal)
    {
        "metric",
        "dimension"
    };

    public static readonly IReadOnlySet<string> SupportedSortDirections = new HashSet<string>(StringComparer.Ordinal)
    {
        "asc",
        "desc"
    };

    private static readonly IReadOnlySet<string> RegionValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "West",
        "Central",
        "East",
        "South"
    };

    private static readonly IReadOnlySet<string> SegmentValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "Consumer",
        "SMB",
        "Enterprise"
    };

    private static readonly IReadOnlySet<string> AcquisitionValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "Organic",
        "Paid Search",
        "Email",
        "Affiliate",
        "Social"
    };

    private static readonly IReadOnlySet<string> ProductCategoryValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "Electronics",
        "Home",
        "Office",
        "Fitness",
        "Accessories"
    };

    private static readonly IReadOnlySet<string> SalesChannelValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "Web",
        "Mobile",
        "Marketplace"
    };

    private static readonly IReadOnlySet<string> CustomerTypeValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "new",
        "existing"
    };

    private readonly IReadOnlyDictionary<string, MetricSqlSpec> _metrics =
        new ReadOnlyDictionary<string, MetricSqlSpec>(new Dictionary<string, MetricSqlSpec>(StringComparer.Ordinal)
        {
            ["revenue"] = new("SUM((oi.quantity * oi.unit_price) - oi.discount_amount)", RequiresOrderItems: true, UsesNewCustomerFlow: false),
            ["order_count"] = new("COUNT(DISTINCT o.id)", RequiresOrderItems: false, UsesNewCustomerFlow: false),
            ["units_sold"] = new("SUM(oi.quantity)", RequiresOrderItems: true, UsesNewCustomerFlow: false),
            ["average_order_value"] = new("COALESCE(SUM((oi.quantity * oi.unit_price) - oi.discount_amount) / NULLIF(COUNT(DISTINCT o.id), 0), 0)", RequiresOrderItems: true, UsesNewCustomerFlow: false),
            ["new_customer_count"] = new("COUNT(DISTINCT o.customer_id)", RequiresOrderItems: false, UsesNewCustomerFlow: true)
        });

    private readonly IReadOnlyDictionary<string, DimensionSqlSpec> _dimensions =
        new ReadOnlyDictionary<string, DimensionSqlSpec>(new Dictionary<string, DimensionSqlSpec>(StringComparer.Ordinal)
        {
            ["customer_region"] = new("c.region", RequiresCustomerJoin: true, RequiresOrderItemsJoin: false, RequiresProductsJoin: false),
            ["customer_segment"] = new("c.segment", RequiresCustomerJoin: true, RequiresOrderItemsJoin: false, RequiresProductsJoin: false),
            ["acquisition_channel"] = new("c.acquisition_channel", RequiresCustomerJoin: true, RequiresOrderItemsJoin: false, RequiresProductsJoin: false),
            ["product_category"] = new("p.category", RequiresCustomerJoin: false, RequiresOrderItemsJoin: true, RequiresProductsJoin: true),
            ["product_subcategory"] = new("p.subcategory", RequiresCustomerJoin: false, RequiresOrderItemsJoin: true, RequiresProductsJoin: true),
            ["product_name"] = new("p.product_name", RequiresCustomerJoin: false, RequiresOrderItemsJoin: true, RequiresProductsJoin: true),
            ["sales_channel"] = new("o.sales_channel", RequiresCustomerJoin: false, RequiresOrderItemsJoin: false, RequiresProductsJoin: false),
            ["shipping_region"] = new("o.shipping_region", RequiresCustomerJoin: false, RequiresOrderItemsJoin: false, RequiresProductsJoin: false)
        });

    private readonly IReadOnlyDictionary<string, FilterSqlSpec> _filters =
        new ReadOnlyDictionary<string, FilterSqlSpec>(new Dictionary<string, FilterSqlSpec>(StringComparer.Ordinal)
        {
            ["customer_region"] = new("c.region", FilterTarget.Customer, RegionValues),
            ["customer_segment"] = new("c.segment", FilterTarget.Customer, SegmentValues),
            ["acquisition_channel"] = new("c.acquisition_channel", FilterTarget.Customer, AcquisitionValues),
            ["product_category"] = new("p.category", FilterTarget.Product, ProductCategoryValues),
            ["product_subcategory"] = new("p.subcategory", FilterTarget.Product),
            ["product_name"] = new("p.product_name", FilterTarget.Product),
            ["sales_channel"] = new("o.sales_channel", FilterTarget.Order, SalesChannelValues),
            ["shipping_region"] = new("o.shipping_region", FilterTarget.Order, RegionValues),
            ["customer_type"] = new("first_completed_at", FilterTarget.CustomerType, CustomerTypeValues)
        });

    private readonly IReadOnlyDictionary<string, string> _timeGrains =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["day"] = "day",
            ["week"] = "week",
            ["month"] = "month",
            ["quarter"] = "quarter"
        });

    internal MetricSqlSpec GetMetric(string metric) => _metrics[metric];

    internal DimensionSqlSpec GetDimension(string dimension) => _dimensions[dimension];

    internal FilterSqlSpec GetFilter(string field) => _filters[field];

    public string GetTimeGrain(string timeGrain) => _timeGrains[timeGrain];
}
