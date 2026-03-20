# Phase 1 MD Doc — Domain Freeze, Data Model, Metrics, and Benchmarks

## Purpose

Phase 1 exists to remove ambiguity before any LLM integration work starts.

This phase locks:
- the domain
- the supported analytics surface area
- the business schema
- the metric definitions
- the benchmark questions
- the anti-scope constraints

The goal is to prevent the project from drifting into a vague chatbot or general AI platform.

---

## Phase 1 outcome

At the end of Phase 1, you should have:

1. a frozen MVP domain and question scope
2. a stable Postgres schema for analytics
3. seed data requirements defined
4. a metric glossary with precise definitions
5. a compressed schema catalog draft for prompt context
6. a benchmark suite of 30 questions
7. explicit acceptance criteria for moving to implementation

No LLM code should be written before this phase is complete.

---

## Domain freeze

### Chosen domain
E-commerce order analytics.

### Business question categories supported in MVP
1. single-value aggregates
2. grouped breakdowns
3. top-N rankings
4. time-series trends
5. simple follow-up refinements based on prior turn state

### Supported example asks
- What was total revenue last month?
- Revenue by category for Q4 2025.
- Top 10 products by units sold this year.
- Monthly revenue for the last 6 months.
- What about just electronics?
- Same question, but only for new customers.

### Explicitly unsupported
- document Q&A
- arbitrary SQL execution
- write/update/delete operations
- causal inference
- anomaly detection
- forecasting
- dashboard generation
- chart rendering
- multiple business domains
- auth/multi-tenant concerns
- vector search
- agent teams
- broad BI functionality

---

## Stable MVP rules

### Rule 1
The model may help interpret the question, but application code owns execution.

### Rule 2
The LLM will produce a structured QueryPlan, not executable SQL.

### Rule 3
Only whitelisted metrics, dimensions, filters, and time grains are allowed.

### Rule 4
Conversation memory is compact structured state, not full-history replay.

### Rule 5
The benchmark suite is written before prompt iteration begins.

---

## Postgres business schema

Use four business tables only.

## 1. customers
Represents the customer dimension.

Suggested columns:
- id (uuid or bigint)
- customer_name
- email
- segment
- region
- acquisition_channel
- created_at

Notes:
- `segment` should be a small controlled set such as Consumer, SMB, Enterprise.
- `region` should be a small controlled set such as West, Central, East, South.
- `created_at` is used to reason about first-time/new customers when combined with orders.

## 2. products
Represents the product dimension.

Suggested columns:
- id
- sku
- product_name
- category
- subcategory
- unit_cost
- is_active
- created_at

Notes:
- `category` should be limited and realistic: Electronics, Home, Office, Fitness, Accessories, etc.
- `subcategory` helps with a few richer grouped queries but is still optional for MVP reasoning.

## 3. orders
Represents the order header.

Suggested columns:
- id
- customer_id
- order_date
- status
- sales_channel
- shipping_region

Notes:
- `status` should include at least Completed, Cancelled, Refunded.
- For MVP analytics, only Completed orders should count toward revenue unless explicitly defined otherwise.

## 4. order_items
Represents order line items.

Suggested columns:
- id
- order_id
- product_id
- quantity
- unit_price
- discount_amount

Notes:
- Revenue can be defined off line items.
- Keep discount handling explicit so metric definitions remain honest.

---

## Relationship model

- customers 1-to-many orders
- orders 1-to-many order_items
- products 1-to-many order_items

This is enough to support:
- time filtering
- customer breakdowns
- product/category rankings
- revenue calculations
- average order value
- follow-up filters

---

## Seed data shape

The dataset should be large enough to create realistic variation, but small enough to generate locally and reason about.

### Recommended approximate volumes
- customers: 2,000 to 5,000
- products: 100 to 300
- orders: 20,000 to 50,000
- order_items: 50,000 to 150,000

### Recommended time span
At least 18 months of order history.

This gives enough room for:
- last month
- last quarter
- year-over-year comparisons
- rolling six-month trends
- weekly trend questions

### Data distribution guidance
Intentionally include:
- category skews
- channel skews
- regional differences
- some cancelled/refunded orders
- repeat customers
- meaningful seasonality
- products with long-tail sales

Avoid perfectly uniform synthetic data. Uniform data makes benchmark questions feel fake and less useful.

---

## Metric glossary

These definitions should be frozen before implementation.

## Revenue
Definition:
Sum of `(order_items.quantity * order_items.unit_price) - order_items.discount_amount`
for orders where `orders.status = 'Completed'`.

## Order count
Definition:
Count of distinct `orders.id` where `orders.status = 'Completed'`.

## Units sold
Definition:
Sum of `order_items.quantity` for completed orders.

## Average order value
Definition:
Revenue divided by count of distinct completed orders in scope.

## New customer
Definition:
A customer whose first completed order falls within the requested time range.

Important:
Do not define “new customer” as `customers.created_at` alone. Use first completed order date.

## Top customer by spend
Definition:
Customer ranked by total completed-order revenue in scope.

## Monthly revenue
Definition:
Revenue aggregated by calendar month based on `orders.order_date`.

---

## Allowed dimensions

The planner should only be able to group by these in MVP:

- category
- subcategory
- product_name
- customer_segment
- region
- acquisition_channel
- sales_channel
- order_month
- order_week

If a user asks for a grouping outside this set, the request should be unsupported.

---

## Allowed filters

Support filters only on:
- date range
- category
- subcategory
- customer_segment
- region
- acquisition_channel
- sales_channel
- product_name
- new_customer flag

No arbitrary filter expressions in MVP.

---

## Time range vocabulary

Support a constrained set:
- today
- yesterday
- last_7_days
- last_30_days
- last_month
- this_month
- last_quarter
- this_quarter
- year_to_date
- last_year
- custom_month
- custom_quarter

If free-form date parsing becomes messy, constrain the benchmark set to supported relative/date forms.

---

## QueryPlan draft contract

This is the target structured output from the planner.

```json
{
  "route": "analytics",
  "questionType": "ranking",
  "metric": "revenue",
  "dimensions": ["category"],
  "filters": [],
  "timeRange": {
    "type": "last_quarter"
  },
  "sort": [
    {
      "field": "revenue",
      "direction": "desc"
    }
  ],
  "limit": 5,
  "clarificationReason": null,
  "notes": []
}
```

### Allowed route values
- analytics
- unsupported
- needs_clarification

### Allowed questionType values
- aggregate
- grouped_breakdown
- ranking
- time_series
- follow_up

---

## Compressed schema catalog draft

Create a hand-authored context file from this structure.

### customers
Business meaning:
Customer account and segmentation attributes.

Useful fields:
- id
- segment
- region
- acquisition_channel
- created_at

### products
Business meaning:
Product catalog and grouping attributes.

Useful fields:
- id
- product_name
- category
- subcategory

### orders
Business meaning:
Order header with customer, date, and channel information.

Useful fields:
- id
- customer_id
- order_date
- status
- sales_channel
- shipping_region

### order_items
Business meaning:
Line-item facts used for revenue and units calculations.

Useful fields:
- order_id
- product_id
- quantity
- unit_price
- discount_amount

---

## Benchmark suite

Create 30 total benchmark cases.

### Bucket A — Aggregate questions
1. What was total revenue last month?
2. How many completed orders were placed last quarter?
3. What was average order value in January 2025?
4. How many units were sold in the last 30 days?
5. How many new customers did we have last month?

### Bucket B — Grouped breakdowns
6. Revenue by category for last quarter.
7. Order count by sales channel for the last 30 days.
8. Revenue by customer segment this quarter.
9. Units sold by region last month.
10. Average order value by acquisition channel this year.

### Bucket C — Rankings
11. Top 5 categories by revenue last quarter.
12. Top 10 products by units sold this year.
13. Top 5 customers by spend in 2025.
14. Which sales channel generated the most revenue last month?
15. Which product category had the highest average order value last quarter?

### Bucket D — Time series
16. Monthly revenue for the last 6 months.
17. Weekly order count for the last 8 weeks.
18. Monthly new customers for the last 12 months.
19. Monthly revenue for electronics in the last 6 months.
20. Weekly units sold for accessories in the last 8 weeks.

### Bucket E — Follow-up / context carryover
21. Revenue by category for last quarter.  
22. What about just electronics?  
23. Now do that by month.  
24. Same question, but only for new customers.  
25. Limit it to the top 3.

Treat 21–25 as a short conversation sequence, not five unrelated one-turn cases.

### Bucket F — Unsupported / clarification cases
26. Why did sales drop last month?
27. Delete refunded orders from the database.
28. Which warehouse was slowest last quarter?
29. Show me profit by supplier.
30. Compare customer satisfaction by category.

These should route to unsupported or needs_clarification, depending on your final route rules.

---

## Benchmark grading rules

Each benchmark case should define:
- expected route
- expected metric
- expected dimensions
- expected filters
- expected time range
- whether execution should succeed
- whether the answer should be marked unsupported

For executable cases, also define:
- gold SQL or gold QueryPlan
- expected result snapshot or deterministic grading query

For unsupported cases, define:
- expected route = unsupported
- no execution should occur

---

## Acceptance criteria for Phase 1

Phase 1 is complete only when all of the following are true:

1. The e-commerce domain is frozen in writing.
2. Supported question types are frozen in writing.
3. Unsupported features are frozen in writing.
4. The Postgres schema is defined.
5. Metric definitions are explicit and testable.
6. Allowed dimensions and filters are explicit.
7. At least 30 benchmark cases exist.
8. The QueryPlan contract is defined.
9. The compressed schema catalog draft exists.
10. No unresolved “maybe we should also…” items remain.

---

## What not to do in Phase 1

Do not:
- integrate an LLM yet
- build a frontend
- debate model vendors for too long
- add another business domain
- add agent architecture
- add vector search
- build semantic memory
- start tuning prompts without benchmarks

---

## Phase 1 handoff to Phase 2

Once Phase 1 is done, the next implementation target is the deterministic execution core:

1. QueryPlan DTO
2. QueryPlan validation
3. QueryPlan-to-SQL compiler
4. SQL safety guard
5. query execution against Postgres
6. manual plan execution tests

Only after that should LLM planning be introduced.

---

## Why this phase matters for interview credibility

This phase gives you a real story about disciplined AI application design:

- you froze the domain before prompting
- you defined strict boundaries for model behavior
- you treated metrics and schema as contracts
- you created a benchmark set before prompt iteration
- you reduced project risk by building deterministic execution first

That is stronger than “I made a chatbot over a database.”
