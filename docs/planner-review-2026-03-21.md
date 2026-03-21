# Planner Review 2026-03-21

Basis:
- Hosted UI accessed at `https://ngallodev-software.uk/grounded/`.
- Public-host spot checks matched local behavior for sampled questions (`What was total revenue last month?`, `Revenue by category last quarter.`, `Show gross margin by channel last month.`).
- Detailed case packet below comes from real executions against the current Grounded deployment with persisted planner traces pulled from `llm_traces`, because the public response does not expose `rawResponse` or `repairedResponse`.
- Live planner prompt version observed in traces: `v1`.
- Live planner model observed in traces: `gpt-4o-mini`.
- Live planner prompt checksum observed in traces: `275BBDAC95AD1DD510C6076FFA6143E2FC810774F5A0F4B4949EE170C11F5D17`.
- Repair-path note: `repairAttempted = true` count in `llm_traces` is `0`, so no repaired planner outputs were available.

## Real Planner Cases

### Case 1
- User question: `What was total revenue last month?`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"revenue","dimension":null,"filters":[],"timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT SUM((oi.quantity * oi.unit_price) - oi.discount_amount) AS metric
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc
```
- Final result status: `success`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5227 in / 69 out`, `latency=2341ms`, `cost≈$0.000825`, `trace_id=3501797e-580a-4ff0-ab42-3be25002edca`

### Case 2
- User question: `Which 5 products sold the most units this year?`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"ranking","metric":"units_sold","dimension":"product_name","filters":[],"timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"units_sold","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT p.product_name AS dimension, SUM(oi.quantity) AS metric
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
JOIN products p ON p.id = oi.product_id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc
GROUP BY p.product_name
ORDER BY metric DESC, dimension ASC
LIMIT 5
```
- Final result status: `success`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5231 in / 72 out`, `latency=1711ms`, `cost≈$0.000828`, `trace_id=28af0c7b-e770-4056-839f-009a22467e65`

### Case 3
- User question: `Show revenue by product category for the last 90 days where sales channel is Web and customer region is West.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"grouped_breakdown","metric":"revenue","dimension":"product_category","filters":[{"field":"sales_channel","operator":"eq","values":["Web"]},{"field":"customer_region","operator":"eq","values":["West"]}],"timeRange":{"preset":"last_90_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[{"field":"sales_channel","operator":"eq","values":["Web"]},{"field":"customer_region","operator":"eq","values":["West"]}],"metric":"revenue","timeRange":{"preset":"last_90_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT p.category AS dimension, SUM((oi.quantity * oi.unit_price) - oi.discount_amount) AS metric
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
JOIN customers c ON c.id = o.customer_id
JOIN products p ON p.id = oi.product_id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc AND o.sales_channel = @p2 AND c.region = @p3
GROUP BY p.category
ORDER BY metric DESC, dimension ASC
LIMIT 200
```
- Final result status: `success`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5242 in / 104 out`, `latency=1937ms`, `cost≈$0.000849`, `trace_id=90506411-0910-479c-9910-d21569e48ed3`

### Case 4
- User question: `Top 5 customers by order count last year where acquisition channel is Email and customer segment is SMB.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"ranking","metric":"order_count","dimension":"customer_name","filters":[{"field":"acquisition_channel","operator":"eq","values":["Email"]},{"field":"customer_segment","operator":"eq","values":["SMB"]}],"timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"ranking","dimension":"customer_name","filters":[{"field":"acquisition_channel","operator":"eq","values":["Email"]},{"field":"customer_segment","operator":"eq","values":["SMB"]}],"metric":"order_count","timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT c.customer_name AS dimension, COUNT(DISTINCT o.id) AS metric
FROM orders o
JOIN customers c ON c.id = o.customer_id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc AND c.acquisition_channel = @p2 AND c.segment = @p3
GROUP BY c.customer_name
ORDER BY metric DESC, dimension ASC
LIMIT 5
```
- Final result status: `success`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5240 in / 101 out`, `latency=2142ms`, `cost≈$0.000847`, `trace_id=0a23b8a5-a224-486d-85f4-65ae3babf040`

### Case 5
- User question: `Revenue by category last quarter.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"grouped_breakdown","metric":"revenue","dimension":"product_category","filters":[],"timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[],"metric":"revenue","timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT p.category AS dimension, SUM((oi.quantity * oi.unit_price) - oi.discount_amount) AS metric
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
JOIN products p ON p.id = oi.product_id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc
GROUP BY p.category
ORDER BY metric DESC, dimension ASC
LIMIT 200
```
- Final result status: `success`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5226 in / 74 out`, `latency=1600ms`, `cost≈$0.000828`, `trace_id=f11f47ed-40db-4fbc-9b2f-712635cc4475`
- Borderline note: this succeeded by resolving the shorthand `category` to `product_category`.

### Case 6
- User question: `Monthly revenue for the last 6 months.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"time_series","metric":"revenue","dimension":null,"filters":[],"timeRange":{"preset":"last_6_months","startDate":null,"endDate":null},"timeGrain":"month","sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"time_series","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_6_months","startDate":null,"endDate":null},"timeGrain":"month","sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `synthesis_failure`, `The synthesized answer may include at most 5 key points.`
- Compiled SQL if it passed:
```sql
SELECT DATE_TRUNC('month', o.order_date) AS time_bucket, SUM((oi.quantity * oi.unit_price) - oi.discount_amount) AS metric
FROM orders o
JOIN order_items oi ON oi.order_id = o.id
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc
GROUP BY DATE_TRUNC('month', o.order_date)
ORDER BY time_bucket ASC
LIMIT 366
```
- Final result status: `failed`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5229 in / 73 out`, `latency=1752ms`, `cost≈$0.000828`, `trace_id=f2d601ac-4ef3-4f5d-82a0-27cfbe5388fc`
- Borderline note: planner was correct; the end-to-end result failed later in synthesis.

### Case 7
- User question: `Top 5 products by units sold this year.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `planner_validation_failure`, `invalid_metric: metric '__unsupported__' is not supported`
- Compiled SQL if it passed: none
- Final result status: `failed`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5230 in / 72 out`, `latency=1919ms`, `cost≈$0.000828`, `trace_id=da46ebc2-ae0e-497c-8216-1732f6324849`
- Borderline note: this is especially important because the same intent is present in the benchmark as an expected success case.

### Case 8
- User question: `Show orders by channel.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `planner_validation_failure`, `invalid_metric: metric '__unsupported__' is not supported`
- Compiled SQL if it passed: none
- Final result status: `unsupported`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5225 in / 72 out`, `latency=1851ms`, `cost≈$0.000827`, `trace_id=3da1afdb-8f91-474e-9e9c-fd0f546325d9`
- Borderline note: generic shorthand `orders` + `channel` did not resolve to `order_count` + `sales_channel`.

### Case 9
- User question: `Show gross margin by channel last month.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `planner_validation_failure`, `invalid_metric: metric '__unsupported__' is not supported`
- Compiled SQL if it passed: none
- Final result status: `unsupported`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5228 in / 72 out`, `latency=3535ms`, `cost≈$0.000827`, `trace_id=746b0f1b-9fc4-49cd-b344-53a4c30c2d47`

### Case 10
- User question: `Show revenue by product category where sales channel is Retail.`
- Planner prompt version used: `v1`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `planner_validation_failure`, `invalid_metric: metric '__unsupported__' is not supported`
- Compiled SQL if it passed: none
- Final result status: `unsupported`
- Trace metadata if easy: `model=gpt-4o-mini`, `tokens=5231 in / 72 out`, `latency=2327ms`, `cost≈$0.000828`, `trace_id=2746c73d-168d-4096-be7f-d1c82b32a7c7`

## Best-Case Mix Coverage

- Successful cases: 1, 2, 3, 4
- Borderline / ambiguous cases: 5, 6, 8
- Unsupported / failure cases: 7, 9, 10
- Filter-heavy cases: 3, 4
- Time-series case: 6
- Ranking cases: 2, 4, 7

## Must-Have Artifacts

- Current planner prompt: [prompts/planner/v1.md](/lump/apps/llm-integration-demo/prompts/planner/v1.md)
- Current structured-output JSON schema: [Grounded.Api/Services/QueryPlanSchema.cs](/lump/apps/llm-integration-demo/Grounded.Api/Services/QueryPlanSchema.cs)
- Current validator rules: [Grounded.Api/Services/QueryPlanValidator.cs](/lump/apps/llm-integration-demo/Grounded.Api/Services/QueryPlanValidator.cs)
- Repair-path logic: [Grounded.Api/Services/PlannerResponseRepairService.cs](/lump/apps/llm-integration-demo/Grounded.Api/Services/PlannerResponseRepairService.cs)
- Planner context / schema shown to planner: [Grounded.Api/Services/PlannerContextBuilder.cs](/lump/apps/llm-integration-demo/Grounded.Api/Services/PlannerContextBuilder.cs)
- Current schema/help panel shown to users: [grounded-ui/src/components/AnswerPanel.tsx](/lump/apps/llm-integration-demo/grounded-ui/src/components/AnswerPanel.tsx)

Exact current UI help panel content:
- Metrics: `revenue`, `units_sold`, `order_count`, `avg_order_value`
- Dimensions: `product_category`, `product_name`, `customer_segment`, `customer_region`, `customer_name`, `sales_channel`, `acquisition_channel`
- Time presets: `last 7 / 30 / 90 days`, `last month / quarter / year`, `month / quarter / year to date`, `specific year`, `all time`
- Filters: `status`, `segment / region / channel`

Important current mismatches:
- UI says `avg_order_value`, backend validator supports `average_order_value`.
- UI advertises `status` as a filter, backend validator does not support `status`.
- UI omits backend-supported dimensions/filters such as `shipping_region` and `product_subcategory`.

Current structured-output schema summary:
- Required fields: `version`, `questionType`, `metric`, `dimension`, `filters`, `timeRange`, `timeGrain`, `sort`, `limit`, `usePriorState`
- `questionType` enum: `aggregate`, `grouped_breakdown`, `ranking`, `time_series`
- `filters[].operator` enum: `eq`, `in`
- `sort.by` enum: `metric`, `dimension`
- `sort.direction` enum: `asc`, `desc`

Current validator rules summary:
- Exact version must be `1.0`.
- Allowed metrics: `revenue`, `order_count`, `units_sold`, `average_order_value`, `new_customer_count`.
- Allowed dimensions: `customer_region`, `customer_segment`, `acquisition_channel`, `product_category`, `product_subcategory`, `product_name`, `sales_channel`, `shipping_region`, `customer_name`.
- Allowed filter fields: `customer_region`, `customer_segment`, `acquisition_channel`, `product_category`, `product_subcategory`, `product_name`, `sales_channel`, `shipping_region`, `customer_type`.
- Ranking requires non-null dimension, `sort.by=metric`, `limit 1..50`.
- Time series requires non-null `timeGrain`; all other question types require `timeGrain=null`.
- At most 8 filters, one per field, `eq` exactly 1 value, `in` 1..20 values.
- `customer_type=existing` is invalid with `new_customer_count`.
- `customer_type` is invalid with `all_time`.

## Very Helpful Artifacts

Representative benchmark questions from [eval/benchmark_cases.jsonl](/lump/apps/llm-integration-demo/eval/benchmark_cases.jsonl):
- `What was total revenue last month?`
- `How many completed orders did we have in the last 30 days?`
- `What is average order value quarter to date?`
- `How many new customers have we acquired month to date?`
- `How many units sold did electronics generate in the last 90 days?`
- `What was revenue from the mobile sales channel this year?`
- `Show revenue by product category for the last 90 days.`
- `Break down order count by shipping region for the last quarter.`
- `Units sold by customer segment this year.`
- `New customer count by acquisition channel for the last 6 months.`
- `Revenue by product subcategory this quarter.`
- `Revenue by sales channel year to date.`
- `Top 5 products by units sold this year.`
- `Top 3 categories by revenue last quarter.`
- `Monthly revenue for the last 6 months.`
- `Quarterly revenue for last year.`
- `Write me the SQL to list every customer email and revenue.`
- `Forecast next quarter revenue by category.`

Worst 5 actual failures observed:
- `Top 5 products by units sold this year.`: benchmark says expected success, live planner returned canonical unsupported pattern.
- `Monthly revenue for the last 6 months.`: planner succeeded, but final request downgraded to `partial_success` because synthesis produced too many key points.
- `Show orders by channel.`: shorthand failed to resolve to `order_count` + `sales_channel`.
- `Show revenue by product category where sales channel is Retail.`: unsupported filter value correctly pushed the planner into canonical unsupported.
- `Show gross margin by channel last month.`: unsupported metric correctly pushed the planner into canonical unsupported.

Repair-path outputs:
- None observed. `llm_traces` currently shows `0` planner attempts with `repairAttempted = true`.

Most common mistakes seen right now:
- The planner overuses the canonical unsupported sentinel, and those requests surface as `planner_validation_failure` via `invalid_metric` rather than a cleaner `unsupported_request`.
- Near-identical ranking phrasings are unstable. `Which 5 products sold the most units this year?` succeeded, while `Top 5 products by units sold this year.` failed in a live run even though that exact prompt exists in the benchmark as a success case.
- End-to-end failures are not only planner failures. At least one canonical time-series request failed later in synthesis.
- The user-facing help panel is out of sync with backend allow-lists, which likely increases planner and user confusion.
