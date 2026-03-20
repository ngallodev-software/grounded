# Phase 1 Artifact

## 1. Final Domain Definition

### Exact problem statement
Build a fixed-scope analytics domain for natural-language questions over an e-commerce dataset stored in Postgres. The domain is limited to four tables: `customers`, `products`, `orders`, and `order_items`. The system answers bounded analytical questions by mapping them to a validated `QueryPlan` over canonical metrics, whitelisted dimensions, whitelisted filters, and controlled time ranges.

### Supported question categories
1. `aggregate`
   Single-value metric requests with optional filters and one time range.
2. `grouped_breakdown`
   One metric grouped by exactly one whitelisted dimension.
3. `ranking`
   One metric ranked by exactly one whitelisted dimension with an explicit limit.
4. `time_series`
   One metric over time using a single time grain.
5. ~~`simple_follow_up`~~ *(removed in Phase 5)*
   Follow-up questions are now intercepted by `ConversationStateService` before planning. The three supported follow-up patterns (last quarter, same thing by category, electronics only) are resolved deterministically from compact stored state. The planner no longer emits a `simple_follow_up` question type; any attempt to do so is rejected by `QueryPlanValidator` as `unsupported_question_type`.

### Explicit unsupported cases
- More than one grouping dimension in a single query
- Comparisons between two time ranges in one query
- Forecasting, anomaly detection, causality, or explanation of why something changed
- Free-form SQL requests
- Write operations
- Joins beyond the four frozen tables
- Metrics outside the canonical glossary
- Filters outside the whitelist
- Arbitrary date math outside the controlled time-range vocabulary
- Chart rendering, dashboards, alerts, or reporting workflows
- Document Q&A, RAG, embeddings, vector search, or multi-agent behavior

## 2. Postgres Schema (Final)

### Allowed enum values
- `customers.segment`: `Consumer`, `SMB`, `Enterprise`
- `customers.region`: `West`, `Central`, `East`, `South`
- `customers.acquisition_channel`: `Organic`, `Paid Search`, `Email`, `Affiliate`, `Social`
- `products.category`: `Electronics`, `Home`, `Office`, `Fitness`, `Accessories`
- `orders.status`: `Completed`, `Cancelled`, `Refunded`
- `orders.sales_channel`: `Web`, `Mobile`, `Marketplace`
- `orders.shipping_region`: `West`, `Central`, `East`, `South`

```sql
CREATE TABLE customers (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_name TEXT NOT NULL,
    email TEXT NOT NULL UNIQUE,
    segment TEXT NOT NULL CHECK (segment IN ('Consumer', 'SMB', 'Enterprise')),
    region TEXT NOT NULL CHECK (region IN ('West', 'Central', 'East', 'South')),
    acquisition_channel TEXT NOT NULL CHECK (
        acquisition_channel IN ('Organic', 'Paid Search', 'Email', 'Affiliate', 'Social')
    ),
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE products (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sku TEXT NOT NULL UNIQUE,
    product_name TEXT NOT NULL,
    category TEXT NOT NULL CHECK (
        category IN ('Electronics', 'Home', 'Office', 'Fitness', 'Accessories')
    ),
    subcategory TEXT NOT NULL,
    unit_cost NUMERIC(12, 2) NOT NULL CHECK (unit_cost >= 0),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE orders (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    customer_id BIGINT NOT NULL REFERENCES customers(id),
    order_date TIMESTAMPTZ NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Completed', 'Cancelled', 'Refunded')),
    sales_channel TEXT NOT NULL CHECK (sales_channel IN ('Web', 'Mobile', 'Marketplace')),
    shipping_region TEXT NOT NULL CHECK (shipping_region IN ('West', 'Central', 'East', 'South'))
);

CREATE TABLE order_items (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    order_id BIGINT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id BIGINT NOT NULL REFERENCES products(id),
    quantity INTEGER NOT NULL CHECK (quantity > 0),
    unit_price NUMERIC(12, 2) NOT NULL CHECK (unit_price >= 0),
    discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0 CHECK (
        discount_amount >= 0 AND discount_amount <= (quantity * unit_price)
    ),
    CONSTRAINT uq_order_items_order_product UNIQUE (order_id, product_id)
);

CREATE INDEX ix_orders_order_date ON orders(order_date);
CREATE INDEX ix_orders_customer_id ON orders(customer_id);
CREATE INDEX ix_order_items_order_id ON order_items(order_id);
CREATE INDEX ix_order_items_product_id ON order_items(product_id);
CREATE INDEX ix_products_category ON products(category);
CREATE INDEX ix_customers_region ON customers(region);
```

## 3. Metric Glossary (Canonical)

### `revenue`
- Definition: `SUM((order_items.quantity * order_items.unit_price) - order_items.discount_amount)`
- Included rows: only rows whose parent `orders.status = 'Completed'`
- Time anchor: `orders.order_date`
- SQL note: aggregate over `order_items` joined to `orders`

### `order_count`
- Definition: `COUNT(DISTINCT orders.id)`
- Included rows: only `orders.status = 'Completed'`
- Time anchor: `orders.order_date`
- SQL note: filters may require joins to `customers`, `products`, and `order_items`, but the count remains distinct on `orders.id`

### `units_sold`
- Definition: `SUM(order_items.quantity)`
- Included rows: only rows whose parent `orders.status = 'Completed'`
- Time anchor: `orders.order_date`
- SQL note: aggregate over `order_items` joined to `orders`

### `average_order_value`
- Definition: `revenue / NULLIF(order_count, 0)`
- Included rows: same scope as `revenue` and `order_count`
- Time anchor: `orders.order_date`
- SQL note: compute from completed-order revenue divided by distinct completed-order count in the same filtered scope

### `new_customer_count`
- Definition: `COUNT(DISTINCT customer_id)` where the customer's first completed order date falls inside the requested time range
- Included rows: only customers with at least one `orders.status = 'Completed'`
- Time anchor: first completed `orders.order_date` per customer
- Filter rule: customer-level and order-level filters must be satisfied by that first completed order; if product filters are present, that first completed order must include at least one matching `order_items` row
- SQL note: implement with a first-completed-order CTE per customer, then apply scoped filters to that first order

## 4. Allowed Analytics Surface

### Dimensions whitelist
- `customer_region` -> `customers.region`
- `customer_segment` -> `customers.segment`
- `acquisition_channel` -> `customers.acquisition_channel`
- `product_category` -> `products.category`
- `product_subcategory` -> `products.subcategory`
- `product_name` -> `products.product_name`
- `sales_channel` -> `orders.sales_channel`
- `shipping_region` -> `orders.shipping_region`

Rules:
- `aggregate`: no dimension
- `grouped_breakdown`: exactly one dimension
- `ranking`: exactly one dimension
- `time_series`: no business dimension; use `timeGrain`
- `simple_follow_up`: resolves to one of the above shapes after prior-state application

### Filters whitelist
- `customer_region`
- `customer_segment`
- `acquisition_channel`
- `product_category`
- `product_subcategory`
- `product_name`
- `sales_channel`
- `shipping_region`
- `customer_type`

Rules:
- Allowed operators: `eq`, `in`
- `customer_type` is a derived filter with allowed values `new`, `existing`
- `existing` means customers whose first completed order date is before the requested time range start
- Status is not user-filterable; canonical metrics already enforce completed-order logic

### Time ranges (controlled vocabulary)
- `last_7_days`
- `last_30_days`
- `last_90_days`
- `last_6_months`
- `last_12_months`
- `month_to_date`
- `quarter_to_date`
- `year_to_date`
- `last_month`
- `last_quarter`
- `last_year`
- `all_time`
- `custom_range`

Rules:
- `custom_range` requires both `startDate` and `endDate`
- All other presets require `startDate = null` and `endDate = null`
- Time-series queries must also specify `timeGrain`

## 5. QueryPlan Contract (Strict)

### JSON structure

```json
{
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
```

### Full JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "required": [
    "version",
    "questionType",
    "metric",
    "dimension",
    "filters",
    "timeRange",
    "timeGrain",
    "sort",
    "limit",
    "usePriorState"
  ],
  "properties": {
    "version": {
      "const": "1.0"
    },
    "questionType": {
      "enum": [
        "aggregate",
        "grouped_breakdown",
        "ranking",
        "time_series",
        "simple_follow_up"
      ]
    },
    "metric": {
      "enum": [
        "revenue",
        "order_count",
        "units_sold",
        "average_order_value",
        "new_customer_count"
      ]
    },
    "dimension": {
      "type": ["string", "null"],
      "enum": [
        "customer_region",
        "customer_segment",
        "acquisition_channel",
        "product_category",
        "product_subcategory",
        "product_name",
        "sales_channel",
        "shipping_region",
        null
      ]
    },
    "filters": {
      "type": "array",
      "maxItems": 8,
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["field", "operator", "values"],
        "properties": {
          "field": {
            "enum": [
              "customer_region",
              "customer_segment",
              "acquisition_channel",
              "product_category",
              "product_subcategory",
              "product_name",
              "sales_channel",
              "shipping_region",
              "customer_type"
            ]
          },
          "operator": {
            "enum": ["eq", "in"]
          },
          "values": {
            "type": "array",
            "minItems": 1,
            "maxItems": 20,
            "items": {
              "type": "string",
              "minLength": 1
            }
          }
        }
      }
    },
    "timeRange": {
      "type": "object",
      "additionalProperties": false,
      "required": ["preset", "startDate", "endDate"],
      "properties": {
        "preset": {
          "enum": [
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
          ]
        },
        "startDate": {
          "type": ["string", "null"],
          "pattern": "^\\d{4}-\\d{2}-\\d{2}$"
        },
        "endDate": {
          "type": ["string", "null"],
          "pattern": "^\\d{4}-\\d{2}-\\d{2}$"
        }
      }
    },
    "timeGrain": {
      "type": ["string", "null"],
      "enum": ["day", "week", "month", "quarter", null]
    },
    "sort": {
      "type": "object",
      "additionalProperties": false,
      "required": ["by", "direction"],
      "properties": {
        "by": {
          "enum": ["metric", "dimension"]
        },
        "direction": {
          "enum": ["asc", "desc"]
        }
      }
    },
    "limit": {
      "type": ["integer", "null"],
      "minimum": 1,
      "maximum": 50
    },
    "usePriorState": {
      "type": "boolean"
    }
  },
  "allOf": [
    {
      "if": {
        "properties": {
          "questionType": {
            "const": "aggregate"
          }
        }
      },
      "then": {
        "properties": {
          "dimension": {
            "const": null
          },
          "timeGrain": {
            "const": null
          },
          "limit": {
            "const": null
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "questionType": {
            "const": "grouped_breakdown"
          }
        }
      },
      "then": {
        "properties": {
          "dimension": {
            "type": "string"
          },
          "timeGrain": {
            "const": null
          },
          "limit": {
            "const": null
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "questionType": {
            "const": "ranking"
          }
        }
      },
      "then": {
        "properties": {
          "dimension": {
            "type": "string"
          },
          "timeGrain": {
            "const": null
          },
          "sort": {
            "properties": {
              "by": {
                "const": "metric"
              }
            }
          },
          "limit": {
            "type": "integer"
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "questionType": {
            "const": "time_series"
          }
        }
      },
      "then": {
        "properties": {
          "dimension": {
            "const": null
          },
          "timeGrain": {
            "enum": ["day", "week", "month", "quarter"]
          },
          "limit": {
            "const": null
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "questionType": {
            "const": "simple_follow_up"
          }
        }
      },
      "then": {
        "properties": {
          "usePriorState": {
            "const": true
          }
        }
      }
    },
    {
      "if": {
        "properties": {
          "timeRange": {
            "properties": {
              "preset": {
                "const": "custom_range"
              }
            }
          }
        }
      },
      "then": {
        "properties": {
          "timeRange": {
            "properties": {
              "startDate": {
                "type": "string"
              },
              "endDate": {
                "type": "string"
              }
            }
          }
        }
      },
      "else": {
        "properties": {
          "timeRange": {
            "properties": {
              "startDate": {
                "const": null
              },
              "endDate": {
                "const": null
              }
            }
          }
        }
      }
    }
  ]
}
```

### Example valid QueryPlan

```json
{
  "version": "1.0",
  "questionType": "ranking",
  "metric": "revenue",
  "dimension": "product_category",
  "filters": [
    {
      "field": "sales_channel",
      "operator": "eq",
      "values": ["Web"]
    }
  ],
  "timeRange": {
    "preset": "last_quarter",
    "startDate": null,
    "endDate": null
  },
  "timeGrain": null,
  "sort": {
    "by": "metric",
    "direction": "desc"
  },
  "limit": 5,
  "usePriorState": false
}
```

### Example invalid QueryPlan

Invalid because `ranking` requires a non-null `dimension`, `limit` must be an integer, `customer_age_band` is not a whitelisted filter, and `last_month` must not include explicit dates.

```json
{
  "version": "1.0",
  "questionType": "ranking",
  "metric": "revenue",
  "dimension": null,
  "filters": [
    {
      "field": "customer_age_band",
      "operator": "eq",
      "values": ["18-25"]
    }
  ],
  "timeRange": {
    "preset": "last_month",
    "startDate": "2025-02-01",
    "endDate": "2025-02-28"
  },
  "timeGrain": null,
  "sort": {
    "by": "metric",
    "direction": "desc"
  },
  "limit": null,
  "usePriorState": false
}
```

## 6. Compressed Schema Context (Prompt Input)

```json
{
  "domain": "ecommerce_analytics",
  "tables": [
    {
      "name": "customers",
      "business_role": "customer attributes used for segmentation",
      "fields": ["id", "segment", "region", "acquisition_channel", "created_at"]
    },
    {
      "name": "products",
      "business_role": "product catalog used for product and category analysis",
      "fields": ["id", "product_name", "category", "subcategory", "is_active"]
    },
    {
      "name": "orders",
      "business_role": "order header with customer, date, status, and channel",
      "fields": ["id", "customer_id", "order_date", "status", "sales_channel", "shipping_region"]
    },
    {
      "name": "order_items",
      "business_role": "order line items used for revenue and units",
      "fields": ["id", "order_id", "product_id", "quantity", "unit_price", "discount_amount"]
    }
  ],
  "relationships": [
    {
      "from": "customers.id",
      "to": "orders.customer_id",
      "type": "one_to_many"
    },
    {
      "from": "orders.id",
      "to": "order_items.order_id",
      "type": "one_to_many"
    },
    {
      "from": "products.id",
      "to": "order_items.product_id",
      "type": "one_to_many"
    }
  ],
  "business_rules": [
    "completed orders drive canonical metrics",
    "order_date is the time anchor for revenue, order_count, units_sold, and average_order_value",
    "new_customer_count is based on first completed order date"
  ]
}
```

## 7. Seed Data Plan

### Record counts
- `customers`: 3,000
- `products`: 180
- `orders`: 36,000
- `order_items`: 97,000 to 108,000

### Time span
- Order history: `2024-01-01` through `2025-12-31`
- Customer creation dates: `2023-07-01` through `2025-12-31`
- Product creation dates: `2023-01-01` through `2025-03-31`

### Distribution rules
- Average 1.0 to 1.2 orders per month per active repeat customer cohort
- Average 2.7 to 3.0 line items per order
- Completed orders: 90%
- Cancelled orders: 6%
- Refunded orders: 4%
- Sales channel mix: Web 58%, Mobile 27%, Marketplace 15%
- Customer segment mix: Consumer 72%, SMB 20%, Enterprise 8%
- Region mix: West 32%, East 27%, South 23%, Central 18%
- Acquisition mix: Organic 35%, Paid Search 22%, Email 18%, Affiliate 15%, Social 10%

### Important skew
- Revenue concentration: top 15 products should generate about 38% of total revenue
- Category concentration: Electronics should contribute about 34% of revenue, Home 24%, Office 18%, Fitness 14%, Accessories 10%
- Seasonal lift:
  - November to December revenue at least 1.6x the monthly baseline
  - Fitness spikes in January at about 1.4x its median month
  - Home and Accessories dip in February
- Channel skew:
  - Marketplace over-indexes on Accessories
  - Mobile over-indexes on Electronics and Fitness
- Regional skew:
  - West over-indexes on Electronics
  - South over-indexes on Home
  - Central has lower average order value than other regions

### Repeat customer behavior
- 41% of customers place exactly one completed order
- 37% place 2 to 4 completed orders
- 17% place 5 to 9 completed orders
- 5% place 10 or more completed orders
- First-to-second order median gap: 46 days
- Repeat customers should have higher average order value than one-time customers by about 12%

### Data integrity rules
- Every order has at least one order item
- Every refunded or cancelled order still keeps its items for realism, but canonical metrics exclude them through `orders.status`
- Product prices vary by product but remain stable within a narrow band; discounts create most effective-price movement
- At least 12 inactive products remain in the catalog with historical sales

## 8. Benchmark Dataset (30 Cases)

Benchmark reference date for resolving relative phrases: `2025-07-01`

### B01
- `id`: `B01`
- `category`: `aggregate`
- `question`: `What was total revenue last month?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_month`
- `should_execute`: `true`

### B02
- `id`: `B02`
- `category`: `aggregate`
- `question`: `How many completed orders did we have in the last 30 days?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `order_count`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_30_days`
- `should_execute`: `true`

### B03
- `id`: `B03`
- `category`: `aggregate`
- `question`: `How many units were sold through mobile last quarter?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `units_sold`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"sales_channel","operator":"eq","values":["Mobile"]}]`
- `expected_time_range`: `last_quarter`
- `should_execute`: `true`

### B04
- `id`: `B04`
- `category`: `aggregate`
- `question`: `What was average order value for enterprise customers this year?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `average_order_value`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"customer_segment","operator":"eq","values":["Enterprise"]}]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B05
- `id`: `B05`
- `category`: `aggregate`
- `question`: `How many new customers did we acquire from paid search in the last 90 days?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `new_customer_count`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"acquisition_channel","operator":"eq","values":["Paid Search"]}]`
- `expected_time_range`: `last_90_days`
- `should_execute`: `true`

### B06
- `id`: `B06`
- `category`: `aggregate`
- `question`: `What was revenue for Electronics in the West region in Q1 2025?`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"product_category","operator":"eq","values":["Electronics"]},{"field":"customer_region","operator":"eq","values":["West"]}]`
- `expected_time_range`: `{"preset":"custom_range","startDate":"2025-01-01","endDate":"2025-03-31"}`
- `should_execute`: `true`

### B07
- `id`: `B07`
- `category`: `grouped_breakdown`
- `question`: `Revenue by product category last quarter.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `["product_category"]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_quarter`
- `should_execute`: `true`

### B08
- `id`: `B08`
- `category`: `grouped_breakdown`
- `question`: `Order count by sales channel this year.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `order_count`
- `expected_dimensions`: `["sales_channel"]`
- `expected_filters`: `[]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B09
- `id`: `B09`
- `category`: `grouped_breakdown`
- `question`: `Average order value by customer segment last month.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `average_order_value`
- `expected_dimensions`: `["customer_segment"]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_month`
- `should_execute`: `true`

### B10
- `id`: `B10`
- `category`: `grouped_breakdown`
- `question`: `Units sold by shipping region for Home products in the last 30 days.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `units_sold`
- `expected_dimensions`: `["shipping_region"]`
- `expected_filters`: `[{"field":"product_category","operator":"eq","values":["Home"]}]`
- `expected_time_range`: `last_30_days`
- `should_execute`: `true`

### B11
- `id`: `B11`
- `category`: `grouped_breakdown`
- `question`: `New customer count by acquisition channel year to date.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `new_customer_count`
- `expected_dimensions`: `["acquisition_channel"]`
- `expected_filters`: `[]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B12
- `id`: `B12`
- `category`: `grouped_breakdown`
- `question`: `Revenue by product subcategory for Marketplace orders in the last 6 months.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `["product_subcategory"]`
- `expected_filters`: `[{"field":"sales_channel","operator":"eq","values":["Marketplace"]}]`
- `expected_time_range`: `last_6_months`
- `should_execute`: `true`

### B13
- `id`: `B13`
- `category`: `ranking`
- `question`: `Top 5 product categories by revenue last quarter.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `["product_category"]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_quarter`
- `should_execute`: `true`

### B14
- `id`: `B14`
- `category`: `ranking`
- `question`: `Top 10 products by units sold this year.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `units_sold`
- `expected_dimensions`: `["product_name"]`
- `expected_filters`: `[]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B15
- `id`: `B15`
- `category`: `ranking`
- `question`: `Top 3 shipping regions by order count for SMB customers in the last 90 days.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `order_count`
- `expected_dimensions`: `["shipping_region"]`
- `expected_filters`: `[{"field":"customer_segment","operator":"eq","values":["SMB"]}]`
- `expected_time_range`: `last_90_days`
- `should_execute`: `true`

### B16
- `id`: `B16`
- `category`: `ranking`
- `question`: `Top 5 acquisition channels by new customer count last month.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `new_customer_count`
- `expected_dimensions`: `["acquisition_channel"]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_month`
- `should_execute`: `true`

### B17
- `id`: `B17`
- `category`: `ranking`
- `question`: `Top 5 subcategories by average order value for Mobile orders this quarter.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `average_order_value`
- `expected_dimensions`: `["product_subcategory"]`
- `expected_filters`: `[{"field":"sales_channel","operator":"eq","values":["Mobile"]}]`
- `expected_time_range`: `quarter_to_date`
- `should_execute`: `true`

### B18
- `id`: `B18`
- `category`: `ranking`
- `question`: `Top 10 product names by revenue for existing customers in 2024.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `["product_name"]`
- `expected_filters`: `[{"field":"customer_type","operator":"eq","values":["existing"]}]`
- `expected_time_range`: `{"preset":"custom_range","startDate":"2024-01-01","endDate":"2024-12-31"}`
- `should_execute`: `true`

### B19
- `id`: `B19`
- `category`: `time_series`
- `question`: `Monthly revenue for the last 6 months.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_6_months`
- `should_execute`: `true`

### B20
- `id`: `B20`
- `category`: `time_series`
- `question`: `Weekly order count for the last 12 months.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `order_count`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_12_months`
- `should_execute`: `true`

### B21
- `id`: `B21`
- `category`: `time_series`
- `question`: `Monthly units sold for Electronics this year.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `units_sold`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"product_category","operator":"eq","values":["Electronics"]}]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B22
- `id`: `B22`
- `category`: `time_series`
- `question`: `Quarterly average order value for Enterprise customers year to date.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `average_order_value`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"customer_segment","operator":"eq","values":["Enterprise"]}]`
- `expected_time_range`: `year_to_date`
- `should_execute`: `true`

### B23
- `id`: `B23`
- `category`: `time_series`
- `question`: `Monthly new customer count from Organic for the last 12 months.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `new_customer_count`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"acquisition_channel","operator":"eq","values":["Organic"]}]`
- `expected_time_range`: `last_12_months`
- `should_execute`: `true`

### B24
- `id`: `B24`
- `category`: `time_series`
- `question`: `Weekly revenue for Marketplace orders in the last 30 days.`
- `prior_state`: `null`
- `expected_route`: `plan_query`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"sales_channel","operator":"eq","values":["Marketplace"]}]`
- `expected_time_range`: `last_30_days`
- `should_execute`: `true`

### B25
- `id`: `B25`
- `category`: `simple_follow_up`
- `question`: `What about just Electronics?`
- `prior_state`: `{"metric":"revenue","dimension":"product_category","filters":[],"timeRange":"last_quarter"}`
- `expected_route`: `plan_query_with_prior_state`
- `expected_metric`: `revenue`
- `expected_dimensions`: `["product_category"]`
- `expected_filters`: `[{"field":"product_category","operator":"eq","values":["Electronics"]}]`
- `expected_time_range`: `last_quarter`
- `should_execute`: `true`

### B26
- `id`: `B26`
- `category`: `simple_follow_up`
- `question`: `Now only for new customers.`
- `prior_state`: `{"metric":"revenue","dimension":null,"filters":[],"timeRange":"last_month"}`
- `expected_route`: `plan_query_with_prior_state`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[{"field":"customer_type","operator":"eq","values":["new"]}]`
- `expected_time_range`: `last_month`
- `should_execute`: `true`

### B27
- `id`: `B27`
- `category`: `simple_follow_up`
- `question`: `Same question, but for Mobile.`
- `prior_state`: `{"metric":"units_sold","dimension":"shipping_region","filters":[{"field":"product_category","operator":"eq","values":["Home"]}],"timeRange":"last_30_days"}`
- `expected_route`: `plan_query_with_prior_state`
- `expected_metric`: `units_sold`
- `expected_dimensions`: `["shipping_region"]`
- `expected_filters`: `[{"field":"product_category","operator":"eq","values":["Home"]},{"field":"sales_channel","operator":"eq","values":["Mobile"]}]`
- `expected_time_range`: `last_30_days`
- `should_execute`: `true`

### B28
- `id`: `B28`
- `category`: `simple_follow_up`
- `question`: `Change that to monthly.`
- `prior_state`: `{"metric":"revenue","dimension":null,"filters":[],"timeRange":"last_6_months","timeGrain":"week"}`
- `expected_route`: `plan_query_with_prior_state`
- `expected_metric`: `revenue`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `last_6_months`
- `should_execute`: `true`

### B29
- `id`: `B29`
- `category`: `unsupported`
- `question`: `Compare revenue last quarter versus the quarter before that by category.`
- `prior_state`: `null`
- `expected_route`: `reject_unsupported`
- `expected_metric`: `null`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `null`
- `should_execute`: `false`

### B30
- `id`: `B30`
- `category`: `unsupported`
- `question`: `Why did revenue drop in the South last month?`
- `prior_state`: `null`
- `expected_route`: `reject_unsupported`
- `expected_metric`: `null`
- `expected_dimensions`: `[]`
- `expected_filters`: `[]`
- `expected_time_range`: `null`
- `should_execute`: `false`

## 9. Acceptance Criteria

- Domain is explicitly limited to e-commerce analytics over `customers`, `products`, `orders`, and `order_items`
- Only five supported question categories are documented, and unsupported cases are explicit
- Schema SQL is complete, executable in Postgres, and includes primary keys, foreign keys, checks, and indexes
- Enum values are frozen and match the schema, filters, and benchmark cases
- Canonical metrics are unambiguous, SQL-implementable, and consistently anchored to completed-order logic
- Allowed dimensions, filters, and time ranges are defined as whitelists with no extra surface area
- `QueryPlan` contract is strict, minimal, and uses `additionalProperties: false`
- `QueryPlan` conditional rules match the supported categories
- Compressed schema context includes only business-relevant fields and relationships
- Seed data plan defines counts, time span, skews, status distribution, and repeat customer behavior
- Benchmark dataset contains exactly 30 cases and covers aggregates, grouped breakdowns, rankings, time series, follow-ups, and unsupported inputs
- Every executable benchmark case can be represented by the `QueryPlan` contract without adding fields
- No Phase 1 artifact introduces LLM implementation, SQL generation logic, UI, auth, cloud concerns, RAG, embeddings, or multi-agent concepts
