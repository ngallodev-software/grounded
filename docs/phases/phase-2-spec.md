# Phase 2 Spec — Deterministic Query Execution Core

## 1. Purpose

Phase 2 builds the non-LLM execution backbone for the analytics system.

The goal is to prove that the application can safely and deterministically execute analytics requests from a structured QueryPlan, without relying on model output.

At the end of Phase 2, the system should:
- accept a manual QueryPlan
- validate it
- compile it into deterministic SQL
- enforce safety
- execute against Postgres
- return results with metadata

---

## 2. Scope

### In scope
- QueryPlan DTOs
- validation layer
- SQL compiler
- SQL safety guard
- Postgres execution
- API endpoint
- deterministic test cases

### Out of scope
- LLM integration
- prompts
- chat system
- eval harness
- UI / frontend
- vector search
- cloud / auth

---

## 3. Core Flow

QueryPlan → Validator → Compiler → Safety → Executor → Response

---

## 4. Request / Response Contract

### Endpoint
POST /analytics/query-plan

### Request
{
  "queryPlan": { ... }
}

### Response
{
  "status": "success | error",
  "rows": [],
  "metadata": {
    "rowCount": number,
    "durationMs": number
  }
}

---

## 5. DTO Models

### QueryPlan
- ~~route~~ *(route is validated at the HTTP layer; not a QueryPlan field)*
- version
- questionType
- metric
- ~~dimensions[]~~ dimension *(single nullable string; only one dimension allowed)*
- filters[]
- timeRange
- timeGrain
- ~~sort[]~~ sort *(single SortSpec object, not an array)*
- limit
- usePriorState

### Supporting Types
- FilterSpec
- SortSpec
- TimeRangeSpec

### Request / Response DTOs
- ExecuteQueryPlanRequest
- ExecuteQueryPlanResponse
- QueryExecutionMetadata
- ValidationErrorDto

### Result Types
- CompiledQuery
- QueryExecutionResult
- ValidationResult

---

## 6. Validation Rules

- ~~route must be analytics~~ *(route validated at HTTP layer, not in QueryPlanValidator)*
- metric must be whitelisted
- ~~dimensions must be whitelisted~~ dimension must be whitelisted (single nullable value)
- `aggregate` and `time_series` require `dimension = null`; `grouped_breakdown` and `ranking` require exactly one non-null dimension
- filters must be whitelisted (field, operator, and values)
- ~~limit must be <= 200~~ `ranking` requires limit between 1 and 50; all other question types require `limit = null` (compiler enforces fixed caps: grouped_breakdown=200, time_series=366)
- time range must be valid; `custom_range` requires both startDate and endDate; startDate must be <= endDate
- `simple_follow_up` is rejected as unsupported in Phase 2
- `time_series` requires `timeGrain` in `day`, `week`, `month`, `quarter`; other types require `timeGrain = null`
- `ranking` requires `sort.by = metric`
- `usePriorState` must be `false` for all executable question types
- invalid combinations rejected

---

## 7. SQL Compilation

### Metrics
- revenue → `SUM((oi.quantity * oi.unit_price) - oi.discount_amount)`
- order_count → `COUNT(DISTINCT o.id)`
- units_sold → `SUM(oi.quantity)`
- ~~average_order_value → revenue / COUNT(DISTINCT o.id)~~ average_order_value → `COALESCE(SUM((oi.quantity * oi.unit_price) - oi.discount_amount) / NULLIF(COUNT(DISTINCT o.id), 0), 0)` *(guards division by zero)*
- new_customer_count → `COUNT(DISTINCT o.customer_id)` via first-completed-order CTE

### Dimensions
- ~~category → p.category~~ *(full whitelist below)*
- customer_region → `c.region`
- customer_segment → `c.segment`
- acquisition_channel → `c.acquisition_channel`
- product_category → `p.category`
- product_subcategory → `p.subcategory`
- product_name → `p.product_name`
- sales_channel → `o.sales_channel`
- shipping_region → `o.shipping_region`
- ~~order_month → DATE_TRUNC('month', o.order_date)~~ *(time grain handled separately via timeGrain field)*

### Joins
- Start from minimum required tables for the metric
- Add `customers c` only when dimension or filter requires customer attributes
- Add `order_items oi` only when metric or product filter requires it
- Add `products p` only when dimension or filter requires product attributes

### Rules
- enforce completed orders (`o.status = 'Completed'`)
- group by dimension (grouped_breakdown, ranking) or time bucket (time_series); no GROUP BY for aggregate
- apply compiler-fixed limits: ranking=`@limit`, grouped_breakdown=200, time_series=366
- all filter values bound as SQL parameters — no string interpolation

---

## 8. SQL Safety

- SELECT only (SQL must start with `SELECT` or `WITH`)
- single statement (no `;` except optional trailing)
- no DDL/DML keywords: `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `ALTER`, `CREATE`, `TRUNCATE`, `GRANT`, `REVOKE`, `CALL`, `COPY`, `DO`
- row cap enforced per question type: aggregate=1, grouped_breakdown=200, ranking=limit (max 50), time_series=366
- timeout enforced: 15-second command timeout; session sets `statement_timeout = 15000`
- read-only connection (database role must not own tables)
- parameterized commands only — no string interpolation
- safety failures return `422` with code `unsafe_sql`

---

## 9. Services

- QueryPlanValidator
- TimeRangeResolver *(new: converts presets to concrete UTC boundaries)*
- SqlFragmentRegistry *(new: owns fixed metric, dimension, filter, and time-grain mappings)*
- QueryPlanCompiler
- SqlSafetyGuard
- AnalyticsQueryExecutor
- AnalyticsQueryPlanService *(new: orchestrates validate → resolve → compile → safety → execute)*
- AnalyticsController *(new: HTTP layer, maps results to status codes)*

---

## 10. Build Order

1. DTOs and response models
2. SqlFragmentRegistry (frozen mappings)
3. TimeRangeResolver (preset → UTC boundaries)
4. QueryPlanValidator
5. QueryPlanCompiler (standard metrics)
6. QueryPlanCompiler (new_customer_count CTE flow)
7. SqlSafetyGuard
8. AnalyticsQueryExecutor (Npgsql, read-only, timeout)
9. AnalyticsQueryPlanService (orchestration)
10. AnalyticsController (HTTP)
11. Tests

---

## 11. Test Plan

1. Aggregate revenue for `last_month` returns one row with only `metric`
2. Grouped breakdown by `product_category` compiles `GROUP BY p.category`, returns `dimension` + `metric`
3. Ranking by `product_name` with `limit = 5` compiles `ORDER BY metric DESC, dimension ASC LIMIT 5`
4. Time-series revenue with `timeGrain = month` compiles `DATE_TRUNC('month', o.order_date)`, orders by `time_bucket ASC`
5. `order_count` with a product filter still compiles `COUNT(DISTINCT o.id)`
6. `new_customer_count` with `customer_type = existing` is rejected (filter conflicts with metric definition)
7. `aggregate` with non-null `dimension` → `422 invalid_dimension`
8. `ranking` with `limit = null` → `422 invalid_limit`
9. `custom_range` with `startDate > endDate` → `422 invalid_time_range`
10. Unknown filter field → `422 invalid_filter_field`
11. `simple_follow_up` → `422 unsupported_question_type`
12. Compiled SQL with multiple statements blocked by SqlSafetyGuard
13. Compiled SQL with disallowed keyword (e.g. `UPDATE`) blocked by SqlSafetyGuard
14. `time_series` with `limit = 10` → `422 invalid_limit`
15. Grouped breakdown with `sort.by = dimension` compiles deterministic alphabetical ordering

---

## 12. Acceptance Criteria

- `POST /analytics/query-plan` exists and accepts `{ "queryPlan": ... }`
- manual QueryPlan executes end-to-end
- `simple_follow_up` explicitly rejected with `422`
- every allowed metric compiles through deterministic fixed mappings only
- every allowed dimension and filter compiles through deterministic fixed mappings only
- all time-range presets resolve to concrete UTC boundaries in code
- SQL execution uses parameterized commands only
- SQL safety enforcement blocks non-SELECT and multi-statement SQL
- database execution uses read-only connection and 15-second timeout
- result row caps enforced per question type
- invalid plans rejected with `422` and explicit error codes
- successful execution returns rows and execution metadata
- tests passing (aggregate, grouped, ranking, time-series, validation failures, safety failures)
