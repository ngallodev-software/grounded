# Phase 2 Artifact

## 1. Phase 2 Objective

Phase 2 delivers a deterministic execution core for the frozen analytics contract.

The exact goal is to accept a manual `QueryPlan`, validate it against the Phase 1 whitelist and shape rules, compile it into parameterized PostgreSQL `SELECT` SQL, enforce execution-time safety rules, execute it through a read-only database connection, and return result rows plus execution metadata.

## 2. Request/Response Contract

### Route

- Method: `POST`
- Route: `/analytics/query-plan`
- Content type: `application/json`

`QueryPlan` remains the frozen Phase 1 contract. The route is validated at the HTTP layer. No `route` field is added to `QueryPlan`.

### Request shape

```json
{
  "queryPlan": {
    "version": "1.0",
    "questionType": "aggregate | grouped_breakdown | ranking | time_series | simple_follow_up",
    "metric": "revenue | order_count | units_sold | average_order_value | new_customer_count",
    "dimension": "customer_region | customer_segment | acquisition_channel | product_category | product_subcategory | product_name | sales_channel | shipping_region | null",
    "filters": [
      {
        "field": "customer_region | customer_segment | acquisition_channel | product_category | product_subcategory | product_name | sales_channel | shipping_region | customer_type",
        "operator": "eq | in",
        "values": ["string"]
      }
    ],
    "timeRange": {
      "preset": "last_7_days | last_30_days | last_90_days | last_6_months | last_12_months | month_to_date | quarter_to_date | year_to_date | last_month | last_quarter | last_year | all_time | custom_range",
      "startDate": "YYYY-MM-DD | null",
      "endDate": "YYYY-MM-DD | null"
    },
    "timeGrain": "day | week | month | quarter | null",
    "sort": {
      "by": "metric | dimension",
      "direction": "asc | desc"
    },
    "limit": "integer 1..50 | null",
    "usePriorState": "boolean"
  }
}
```

### Response shape

Successful execution returns:

```json
{
  "status": "success",
  "rows": [
    {
      "dimension": "Electronics",
      "metric": 125430.55
    }
  ],
  "metadata": {
    "compiledSql": "SELECT ...",
    "parameters": {
      "p0": "2025-01-01",
      "p1": "2025-03-31"
    },
    "rowCount": 1,
    "durationMs": 18,
    "appliedRowLimit": 1,
    "timeRangeStartUtc": "2025-01-01T00:00:00Z",
    "timeRangeEndExclusiveUtc": "2025-04-01T00:00:00Z"
  }
}
```

Rejected input returns:

```json
{
  "status": "error",
  "errors": [
    {
      "code": "invalid_dimension",
      "message": "questionType 'aggregate' requires dimension = null"
    }
  ]
}
```

### Status code behavior

- `200 OK`: query plan validated, SQL passed safety checks, execution completed
- `400 Bad Request`: malformed JSON, missing `queryPlan`, or request body shape invalid
- `422 Unprocessable Entity`: `QueryPlan` shape is syntactically valid JSON but fails business validation
- `500 Internal Server Error`: unexpected application failure before database execution completes

No other status code is required for Phase 2.

## 3. C# Domain / DTO Models

### QueryPlan

```csharp
public sealed record QueryPlan(
    string Version,
    string QuestionType,
    string? Dimension,
    string Metric,
    IReadOnlyList<FilterSpec> Filters,
    TimeRangeSpec TimeRange,
    string? TimeGrain,
    SortSpec Sort,
    int? Limit,
    bool UsePriorState);
```

- `Version`: must be `"1.0"`
- `QuestionType`: frozen query category
- `Dimension`: single allowed business dimension or `null`
- `Metric`: single canonical metric
- `Filters`: whitelist-only filter list
- `TimeRange`: controlled preset plus optional explicit dates
- `TimeGrain`: required only for `time_series`
- `Sort`: deterministic sort contract
- `Limit`: required only for `ranking`, otherwise `null`
- `UsePriorState`: must remain `false` in Phase 2 execution, except a `simple_follow_up` plan can be rejected as unsupported for execution

### FilterSpec

```csharp
public sealed record FilterSpec(
    string Field,
    string Operator,
    IReadOnlyList<string> Values);
```

- `Field`: whitelisted filter field
- `Operator`: `eq` or `in`
- `Values`: one or more string values; `eq` still uses a single-item array

### SortSpec

```csharp
public sealed record SortSpec(
    string By,
    string Direction);
```

- `By`: `metric` or `dimension`
- `Direction`: `asc` or `desc`

### TimeRangeSpec

```csharp
public sealed record TimeRangeSpec(
    string Preset,
    string? StartDate,
    string? EndDate);
```

- `Preset`: controlled vocabulary
- `StartDate`: `YYYY-MM-DD` for `custom_range`, else `null`
- `EndDate`: `YYYY-MM-DD` for `custom_range`, else `null`

### ExecuteQueryPlanRequest

```csharp
public sealed record ExecuteQueryPlanRequest(
    QueryPlan QueryPlan);
```

- `QueryPlan`: wrapped request payload for the endpoint

### ExecuteQueryPlanResponse

```csharp
public sealed record ExecuteQueryPlanResponse(
    string Status,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Rows,
    QueryExecutionMetadata? Metadata,
    IReadOnlyList<ValidationErrorDto>? Errors);
```

- `Status`: `success` or `error`
- `Rows`: result set for successful execution
- `Metadata`: execution metadata for successful execution
- `Errors`: validation failures for rejected plans

### CompiledQuery

```csharp
public sealed record CompiledQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters,
    int EffectiveLimit,
    bool ReturnsDimensionColumn,
    bool ReturnsTimeBucketColumn);
```

- `Sql`: final parameterized SQL text
- `Parameters`: named SQL parameters
- `EffectiveLimit`: final row cap applied to the SQL
- `ReturnsDimensionColumn`: whether result rows include `dimension`
- `ReturnsTimeBucketColumn`: whether result rows include `time_bucket`

### QueryExecutionResult

```csharp
public sealed record QueryExecutionResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    QueryExecutionMetadata Metadata);
```

- `Rows`: materialized result rows
- `Metadata`: execution metadata returned to API callers

### QueryExecutionMetadata

```csharp
public sealed record QueryExecutionMetadata(
    string CompiledSql,
    IReadOnlyDictionary<string, object?> Parameters,
    int RowCount,
    long DurationMs,
    int AppliedRowLimit,
    DateTimeOffset? TimeRangeStartUtc,
    DateTimeOffset? TimeRangeEndExclusiveUtc);
```

- `CompiledSql`: SQL text after compilation
- `Parameters`: actual bound parameter values
- `RowCount`: number of returned rows
- `DurationMs`: database execution duration
- `AppliedRowLimit`: enforced result cap
- `TimeRangeStartUtc`: resolved inclusive lower bound
- `TimeRangeEndExclusiveUtc`: resolved exclusive upper bound

### ValidationResult

```csharp
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors);

public sealed record ValidationError(
    string Code,
    string Message);

public sealed record ValidationErrorDto(
    string Code,
    string Message);
```

- `IsValid`: overall validation outcome
- `Errors`: ordered validation failure list
- `Code`: machine-usable error key
- `Message`: exact human-readable explanation

## 4. Validation Rules

### Route

- Only `POST /analytics/query-plan` is valid.
- Any other route is outside Phase 2.

### Question type

- Allowed values: `aggregate`, `grouped_breakdown`, `ranking`, `time_series`, `simple_follow_up`
- `simple_follow_up` is not executable in Phase 2 because Phase 2 has no prior-state store or resolver. Reject it with `422` and code `unsupported_question_type`.

### Metric

- Allowed values: `revenue`, `order_count`, `units_sold`, `average_order_value`, `new_customer_count`
- Any other metric is rejected.

### Dimensions

- Allowed values: `customer_region`, `customer_segment`, `acquisition_channel`, `product_category`, `product_subcategory`, `product_name`, `sales_channel`, `shipping_region`, `null`
- `aggregate` requires `dimension = null`
- `grouped_breakdown` requires exactly one non-null dimension
- `ranking` requires exactly one non-null dimension
- `time_series` requires `dimension = null`

### Filters

- Allowed fields: `customer_region`, `customer_segment`, `acquisition_channel`, `product_category`, `product_subcategory`, `product_name`, `sales_channel`, `shipping_region`, `customer_type`
- Allowed operators: `eq`, `in`
- `eq` requires exactly one value
- `in` requires 1 to 20 values
- `values` entries must be non-empty strings
- Maximum total filters: `8`
- Duplicate filter objects are rejected
- More than one filter for the same field is rejected
- `customer_type` allowed values: `new`, `existing`
- `customer_type = eq` must use exactly one of `new` or `existing`
- `customer_type = in` may contain one or both of `new`, `existing`
- Enum-backed fields may only contain frozen enum values:
  - `customer_region`: `West`, `Central`, `East`, `South`
  - `customer_segment`: `Consumer`, `SMB`, `Enterprise`
  - `acquisition_channel`: `Organic`, `Paid Search`, `Email`, `Affiliate`, `Social`
  - `product_category`: `Electronics`, `Home`, `Office`, `Fitness`, `Accessories`
  - `sales_channel`: `Web`, `Mobile`, `Marketplace`
  - `shipping_region`: `West`, `Central`, `East`, `South`
- `product_subcategory` and `product_name` accept any non-empty string values

### Time range

- Allowed presets: `last_7_days`, `last_30_days`, `last_90_days`, `last_6_months`, `last_12_months`, `month_to_date`, `quarter_to_date`, `year_to_date`, `last_month`, `last_quarter`, `last_year`, `all_time`, `custom_range`
- `custom_range` requires both `startDate` and `endDate`
- Non-`custom_range` presets require `startDate = null` and `endDate = null`
- `custom_range.startDate` and `custom_range.endDate` must parse as ISO `YYYY-MM-DD`
- `custom_range.startDate` must be less than or equal to `custom_range.endDate`
- Resolved SQL range must be `[startDate 00:00:00 UTC, endDate + 1 day 00:00:00 UTC)`

### Limit

- `ranking` requires `limit` and it must be between `1` and `50`
- `aggregate`, `grouped_breakdown`, and `time_series` require `limit = null`

### Invalid combinations

- `aggregate` requires `timeGrain = null`
- `grouped_breakdown` requires `timeGrain = null`
- `ranking` requires `timeGrain = null`
- `ranking` requires `sort.by = metric`
- `time_series` requires `timeGrain` in `day`, `week`, `month`, `quarter`
- `time_series` allows `sort.by = metric` or `sort.by = dimension`; both compile to `ORDER BY time_bucket`
- `aggregate` and `grouped_breakdown` require `usePriorState = false`
- `ranking` and `time_series` require `usePriorState = false`
- `average_order_value` cannot sort by `dimension` when `questionType = aggregate` because no dimension column exists
- `new_customer_count` with `questionType = time_series` is allowed and uses first completed order date as the time anchor

## 5. SQL Compilation Design

Compilation is table-driven. The compiler does not infer columns. It maps each allowed value to a fixed SQL fragment.

### Base aliases

- `customers` -> `c`
- `orders` -> `o`
- `order_items` -> `oi`
- `products` -> `p`

### Metric mapping rules

- `revenue`:
  - expression: `SUM((oi.quantity * oi.unit_price) - oi.discount_amount)`
  - required joins: `orders o` + `order_items oi`
  - time anchor: `o.order_date`
  - implicit filter: `o.status = 'Completed'`
- `order_count`:
  - expression: `COUNT(DISTINCT o.id)`
  - required joins: `orders o`
  - add `order_items oi`, `customers c`, `products p` only when needed by filters or dimension
  - time anchor: `o.order_date`
  - implicit filter: `o.status = 'Completed'`
- `units_sold`:
  - expression: `SUM(oi.quantity)`
  - required joins: `orders o` + `order_items oi`
  - time anchor: `o.order_date`
  - implicit filter: `o.status = 'Completed'`
- `average_order_value`:
  - expression: `COALESCE(SUM((oi.quantity * oi.unit_price) - oi.discount_amount) / NULLIF(COUNT(DISTINCT o.id), 0), 0)`
  - required joins: `orders o` + `order_items oi`
  - time anchor: `o.order_date`
  - implicit filter: `o.status = 'Completed'`
- `new_customer_count`:
  - use a fixed CTE:
    - `first_completed_orders` finds `MIN(o.order_date)` per `customer_id` over completed orders
    - `scoped_first_orders` joins the first order row back to `orders`, `customers`, optional `order_items`, and optional `products`
  - expression: `COUNT(DISTINCT o.customer_id)`
  - time anchor: first completed `o.order_date`
  - implicit filter: first completed order must satisfy all order/customer filters, and if product filters exist, at least one matching product row must exist on that first order

### Dimension mapping rules

- `customer_region` -> `c.region`
- `customer_segment` -> `c.segment`
- `acquisition_channel` -> `c.acquisition_channel`
- `product_category` -> `p.category`
- `product_subcategory` -> `p.subcategory`
- `product_name` -> `p.product_name`
- `sales_channel` -> `o.sales_channel`
- `shipping_region` -> `o.shipping_region`

Result column aliases:

- grouped or ranking dimension column alias: `dimension`
- time-series bucket alias: `time_bucket`
- metric column alias: `metric`

### Filter mapping rules

- `customer_region` -> `c.region`
- `customer_segment` -> `c.segment`
- `acquisition_channel` -> `c.acquisition_channel`
- `product_category` -> `p.category`
- `product_subcategory` -> `p.subcategory`
- `product_name` -> `p.product_name`
- `sales_channel` -> `o.sales_channel`
- `shipping_region` -> `o.shipping_region`
- `customer_type = new`:
  - for standard metrics: add `first_completed_at >= @range_start AND first_completed_at < @range_end_exclusive`
  - implement `first_completed_at` through a fixed customer-first-order CTE
- `customer_type = existing`:
  - add `first_completed_at < @range_start`

Operator compilation:

- `eq` -> `column = @pN`
- `in` -> `column = ANY(@pN)`

### Time range mapping rules

Resolve all presets in application code before SQL generation. The compiler receives two UTC instants:

- `rangeStartUtc`
- `rangeEndExclusiveUtc`

Preset resolution rules:

- `last_7_days`: start = today minus 7 days at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `last_30_days`: start = today minus 30 days at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `last_90_days`: start = today minus 90 days at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `last_6_months`: start = first day of month 5 months before current month at 00:00 UTC; end exclusive = first day of next month at 00:00 UTC
- `last_12_months`: start = first day of month 11 months before current month at 00:00 UTC; end exclusive = first day of next month at 00:00 UTC
- `month_to_date`: start = first day of current month at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `quarter_to_date`: start = first day of current quarter at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `year_to_date`: start = January 1 of current year at 00:00 UTC; end exclusive = tomorrow at 00:00 UTC
- `last_month`: start = first day of previous month at 00:00 UTC; end exclusive = first day of current month at 00:00 UTC
- `last_quarter`: start = first day of previous quarter at 00:00 UTC; end exclusive = first day of current quarter at 00:00 UTC
- `last_year`: start = January 1 of previous year at 00:00 UTC; end exclusive = January 1 of current year at 00:00 UTC
- `all_time`: no time predicate
- `custom_range`: start = provided `startDate` at 00:00 UTC; end exclusive = provided `endDate + 1 day` at 00:00 UTC

SQL predicate:

- standard metrics: `o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc`
- `new_customer_count`: apply the same predicate to first completed order date

### Join rules

- Always start from the minimum required table set for the metric.
- Add `customers c ON c.id = o.customer_id` only when the dimension or a filter requires customer attributes.
- Add `order_items oi ON oi.order_id = o.id` only when the metric or a product filter requires it.
- Add `products p ON p.id = oi.product_id` only when the dimension or a filter requires product attributes.
- For `order_count`, never count `oi` rows; always count `DISTINCT o.id`.
- For `new_customer_count`, use the fixed first-order CTE flow instead of the standard metric flow.

### Group by rules

- `aggregate`: no `GROUP BY`
- `grouped_breakdown`: `GROUP BY` exact dimension SQL fragment
- `ranking`: `GROUP BY` exact dimension SQL fragment
- `time_series`: `GROUP BY DATE_TRUNC(grain, time_anchor)`

Time grain mapping:

- `day` -> `DATE_TRUNC('day', o.order_date)`
- `week` -> `DATE_TRUNC('week', o.order_date)`
- `month` -> `DATE_TRUNC('month', o.order_date)`
- `quarter` -> `DATE_TRUNC('quarter', o.order_date)`
- For `new_customer_count` time series, replace `o.order_date` with first completed order date

### Order by rules

- `aggregate`: no explicit `ORDER BY`
- `grouped_breakdown`:
  - `sort.by = metric` -> `ORDER BY metric {direction}, dimension ASC`
  - `sort.by = dimension` -> `ORDER BY dimension {direction}`
- `ranking`:
  - `ORDER BY metric {direction}, dimension ASC`
- `time_series`:
  - always `ORDER BY time_bucket ASC`

### Limit rules

- `aggregate`: no SQL `LIMIT`
- `grouped_breakdown`: apply fixed `LIMIT 200`
- `ranking`: apply `LIMIT @limit`
- `time_series`: apply fixed `LIMIT 366`

Fixed result caps are part of compiler output, not caller choice.

## 6. SQL Safety Rules

The safety guard runs after compilation and before execution.

- SQL must start with `SELECT` or `WITH` after trimming whitespace
- SQL must contain exactly one statement
- SQL must not contain `;` anywhere except an optional trailing semicolon, which should be removed before execution
- SQL must not contain `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `GRANT`, `REVOKE`, `CALL`, `COPY`, `DO`
- Execution must use parameterized commands only; no string interpolation of filter values or dates
- Database role must be read-only and must not own tables
- Command timeout must be `15` seconds
- Session must set `statement_timeout` to `15000`
- Returned rows must not exceed the compiled cap:
  - aggregate: `1`
  - grouped_breakdown: `200`
  - ranking: requested `limit`, max `50`
  - time_series: `366`
- If the compiler emits `all_time`, the row cap rules still apply
- If any safety check fails, reject execution with `422` and code `unsafe_sql`

## 7. Service Breakdown

- `QueryPlanValidator`
  - Owns syntactic and business validation of the frozen `QueryPlan`
- `TimeRangeResolver`
  - Converts preset ranges into concrete UTC boundaries
- `SqlFragmentRegistry`
  - Owns fixed mappings for metrics, dimensions, filters, and time grains
- `QueryPlanCompiler`
  - Builds `CompiledQuery` from a validated plan plus resolved time range
- `SqlSafetyGuard`
  - Verifies final SQL text and compiled row caps before execution
- `AnalyticsQueryExecutor`
  - Executes the safe `CompiledQuery` via `Npgsql` on the read-only connection
- `AnalyticsQueryPlanService`
  - Orchestrates validate -> resolve time range -> compile -> safety -> execute
- `AnalyticsController`
  - Exposes `POST /analytics/query-plan` and maps results to HTTP responses

## 8. Build Order

1. Define immutable DTOs and response models.
2. Implement `SqlFragmentRegistry` with frozen metric, dimension, filter, and time-grain mappings.
3. Implement `TimeRangeResolver` for all controlled presets.
4. Implement `QueryPlanValidator` with exact rule coverage and explicit error codes.
5. Implement `QueryPlanCompiler` for standard metrics.
6. Implement `QueryPlanCompiler` support for `new_customer_count` first-order CTE flow.
7. Implement `SqlSafetyGuard`.
8. Implement `AnalyticsQueryExecutor` with `Npgsql`, timeout, and read-only session settings.
9. Implement `AnalyticsQueryPlanService`.
10. Implement `AnalyticsController`.
11. Add automated tests for validation, compilation, safety, and execution behavior.

## 9. Test Plan

1. Aggregate revenue plan for `last_month` returns one row with only `metric`.
2. Grouped breakdown by `product_category` compiles `GROUP BY p.category` and returns `dimension` plus `metric`.
3. Ranking by `product_name` with `limit = 5` compiles `ORDER BY metric DESC, dimension ASC` plus `LIMIT 5`.
4. Time-series revenue with `timeGrain = month` compiles `DATE_TRUNC('month', o.order_date)` and orders by `time_bucket ASC`.
5. `order_count` with a product filter still compiles `COUNT(DISTINCT o.id)`.
6. `new_customer_count` with `customer_type = existing` is rejected because the filter conflicts with the metric definition of first completed order in-range.
7. `aggregate` with non-null `dimension` returns `422 invalid_dimension`.
8. `ranking` with `limit = null` returns `422 invalid_limit`.
9. `custom_range` with `startDate > endDate` returns `422 invalid_time_range`.
10. Unknown filter field returns `422 invalid_filter_field`.
11. `simple_follow_up` returns `422 unsupported_question_type`.
12. Compiled SQL containing multiple statements is blocked by `SqlSafetyGuard`.
13. Compiled SQL with a disallowed keyword such as `UPDATE` is blocked by `SqlSafetyGuard`.
14. `time_series` with `limit = 10` returns `422 invalid_limit`.
15. Grouped breakdown with `sort.by = dimension` compiles deterministic alphabetical ordering.

## 10. Acceptance Criteria

- `POST /analytics/query-plan` exists and accepts `{ "queryPlan": ... }`.
- The endpoint accepts only the frozen Phase 1 `QueryPlan` contract.
- `simple_follow_up` is explicitly rejected in Phase 2.
- Every allowed metric compiles through deterministic fixed mappings only.
- Every allowed dimension and filter compiles through deterministic fixed mappings only.
- All time-range presets resolve to concrete UTC boundaries in code.
- SQL execution uses parameterized commands only.
- SQL safety enforcement blocks non-`SELECT` and multi-statement SQL.
- Database execution uses a read-only connection and a 15-second timeout.
- Result row caps are enforced per query shape.
- Validation failures return `422` with explicit error codes and messages.
- Successful execution returns rows and execution metadata.
- Automated tests cover aggregate, grouped, ranking, time-series, validation failures, and safety failures.
