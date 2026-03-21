# Planner Review V2 Comparison 2026-03-21

Basis:
- Original results are attached from the first planner review run in `docs/planner-review-2026-03-21.md`.
- New results below were run after integrating `planner/v2.md` and switching the active planner prompt to `v2`.
- Current run target: `http://127.0.0.1:5252/analytics/query` against the rebuilt local API container.
- Raw planner outputs for the new run were pulled from `llm_traces` by `trace_id`.

## Case 1

- User question: `What was total revenue last month?`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"revenue","dimension":null,"filters":[],"timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_month","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.98}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6691 in / 78 out`, `latency=2181ms`, `cost≈$0.001050`, `trace_id=57a94d8c-fcbc-44ef-990c-9a3d058fa407`
- New planner metadata: `resolvedFrom=None`, `confidence=0.98`, `cacheHit=False`

## Case 2

- User question: `Which 5 products sold the most units this year?`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"ranking","metric":"units_sold","dimension":"product_name","filters":[],"timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"units_sold","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":0.96}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6695 in / 81 out`, `latency=1659ms`, `cost≈$0.001053`, `trace_id=26929acf-492b-4ee9-93eb-60a5b1605780`
- New planner metadata: `resolvedFrom=None`, `confidence=0.96`, `cacheHit=False`

## Case 3

- User question: `Show revenue by product category for the last 90 days where sales channel is Web and customer region is West.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"grouped_breakdown","metric":"revenue","dimension":"product_category","filters":[{"field":"sales_channel","operator":"eq","values":["Web"]},{"field":"customer_region","operator":"eq","values":["West"]}],"timeRange":{"preset":"last_90_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[{"field":"sales_channel","operator":"eq","values":["Web"]},{"field":"customer_region","operator":"eq","values":["West"]}],"metric":"revenue","timeRange":{"preset":"last_90_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.96}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6706 in / 113 out`, `latency=1733ms`, `cost≈$0.001074`, `trace_id=d5ecd6a8-ca43-4e12-86fb-19400161558d`
- New planner metadata: `resolvedFrom=None`, `confidence=0.96`, `cacheHit=False`

## Case 4

- User question: `Top 5 customers by order count last year where acquisition channel is Email and customer segment is SMB.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"ranking","metric":"order_count","dimension":"customer_name","filters":[{"field":"acquisition_channel","operator":"eq","values":["Email"]},{"field":"customer_segment","operator":"eq","values":["SMB"]}],"timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"ranking","dimension":"customer_name","filters":[{"field":"acquisition_channel","operator":"eq","values":["Email"]},{"field":"customer_segment","operator":"eq","values":["SMB"]}],"metric":"order_count","timeRange":{"preset":"last_year","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":0.96}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6704 in / 110 out`, `latency=2585ms`, `cost≈$0.001072`, `trace_id=b904f921-5bf4-427c-b581-b738163b886c`
- New planner metadata: `resolvedFrom=None`, `confidence=0.96`, `cacheHit=False`

## Case 5

- User question: `Revenue by category last quarter.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"grouped_breakdown","metric":"revenue","dimension":"product_category","filters":[],"timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"grouped_breakdown","dimension":"product_category","filters":[],"metric":"revenue","timeRange":{"preset":"last_quarter","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.96}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6690 in / 83 out`, `latency=1621ms`, `cost≈$0.001053`, `trace_id=b3ee14ad-e4c1-4711-8555-fb3413cd9539`
- New planner metadata: `resolvedFrom=None`, `confidence=0.96`, `cacheHit=False`

## Case 6

- User question: `Monthly revenue for the last 6 months.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"time_series","metric":"revenue","dimension":null,"filters":[],"timeRange":{"preset":"last_6_months","startDate":null,"endDate":null},"timeGrain":"month","sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"time_series","dimension":null,"filters":[],"metric":"revenue","timeRange":{"preset":"last_6_months","startDate":null,"endDate":null},"timeGrain":"month","sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.98}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
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
- Final result status: `success`
- Trace metadata: `model=gpt-4o-mini`, `tokens=6693 in / 82 out`, `latency=1844ms`, `cost≈$0.001053`, `trace_id=8f2eabbb-34c4-4d62-85dc-e9b63b393dbd`
- New planner metadata: `resolvedFrom=None`, `confidence=0.98`, `cacheHit=False`

## Case 7

- User question: `Top 5 products by units sold this year.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"ranking","metric":"units_sold","dimension":"product_name","filters":[],"timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"ranking","dimension":"product_name","filters":[],"metric":"units_sold","timeRange":{"preset":"year_to_date","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":5,"usePriorState":false,"resolvedFrom":null,"confidence":0.96}
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
- Trace metadata: `model=gpt-4o-mini`, `tokens=6694 in / 81 out`, `latency=1828ms`, `cost≈$0.001053`, `trace_id=9d2b61ff-ab3c-4f27-ac90-f48e896edeeb`
- New planner metadata: `resolvedFrom=None`, `confidence=0.96`, `cacheHit=False`

## Case 8

- User question: `Show orders by channel.`

### Original Result

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

### New Result

- HTTP status: `200`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"grouped_breakdown","metric":"order_count","dimension":"sales_channel","filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.9}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"grouped_breakdown","dimension":"sales_channel","filters":[],"metric":"order_count","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.9}
```
- Validator result: `pass`
- pass/fail: `pass`
- failure category / validation errors: `none`
- Compiled SQL if it passed:
```sql
SELECT o.sales_channel AS dimension, COUNT(DISTINCT o.id) AS metric
FROM orders o
WHERE o.status = 'Completed' AND o.order_date >= @rangeStartUtc AND o.order_date < @rangeEndExclusiveUtc
GROUP BY o.sales_channel
ORDER BY metric DESC, dimension ASC
LIMIT 200
```
- Final result status: `success`
- Trace metadata: `model=gpt-4o-mini`, `tokens=6689 in / 86 out`, `latency=2098ms`, `cost≈$0.001055`, `trace_id=3c4f6db7-437e-41c7-b732-46294b5b92b2`
- New planner metadata: `resolvedFrom=None`, `confidence=0.9`, `cacheHit=False`

## Case 9

- User question: `Show gross margin by channel last month.`

### Original Result

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

### New Result

- HTTP status: `422`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.35}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `unsupported_request`
```json
[
  {
    "code": "invalid_metric",
    "message": "metric '__unsupported__' is not supported"
  }
]
```
- Compiled SQL if it passed:
```sql
none
```
- Final result status: `error`
- Trace metadata: `model=gpt-4o-mini`, `tokens=6692 in / 81 out`, `latency=2071ms`, `cost≈$0.001052`, `trace_id=f867a5b0-867e-494a-9c91-72e4ffb52fba`
- New planner metadata: `resolvedFrom=None`, `confidence=0.35`, `cacheHit=False`

## Case 10

- User question: `Show revenue by product category where sales channel is Retail.`

### Original Result

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

### New Result

- HTTP status: `422`
- Planner prompt version used: `v2`
- Raw model output from the planner:
```json
{"version":"1.0","questionType":"aggregate","metric":"__unsupported__","dimension":null,"filters":[],"timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":null}
```
- Parsed QueryPlan after parsing/repair:
```json
{"version":"1.0","questionType":"aggregate","dimension":null,"filters":[],"metric":"__unsupported__","timeRange":{"preset":"last_30_days","startDate":null,"endDate":null},"timeGrain":null,"sort":{"by":"metric","direction":"desc"},"limit":null,"usePriorState":false,"resolvedFrom":null,"confidence":0.35}
```
- Validator result: `fail`
- pass/fail: `fail`
- failure category / validation errors: `unsupported_request`
```json
[
  {
    "code": "invalid_metric",
    "message": "metric '__unsupported__' is not supported"
  }
]
```
- Compiled SQL if it passed:
```sql
none
```
- Final result status: `error`
- Trace metadata: `model=gpt-4o-mini`, `tokens=6695 in / 81 out`, `latency=1882ms`, `cost≈$0.001053`, `trace_id=c0906bc3-4d0f-4c2d-8ac3-e8dd75d6ae7f`
- New planner metadata: `resolvedFrom=None`, `confidence=0.35`, `cacheHit=False`

## Delta Summary

Improvements:
- Case 6: synthesis failure cleared and end-to-end status is now success.
- Case 7: `Top 5 products by units sold this year.` now succeeds (status=success, failureCategory=none, finalStatus=success).
- Case 8: `Show orders by channel.` now succeeds (status=success, failureCategory=none, finalStatus=success).
- No regressions observed in this 10-case batch.

Notes:
- Because this run was performed after adding parser-side metadata enrichment, `resolvedFrom` and `confidence` may appear even when the raw planner output omitted them.
- `cacheHit` is expected to be `false` for the first pass of each question in this batch.
