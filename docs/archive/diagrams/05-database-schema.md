# Database Schema — ER Diagram

Grounded uses a single PostgreSQL instance with two logical schemas:
- **public** — analytics domain data (seeded, read-only at runtime)
- **grounded** — trace, eval, and conversation state (written by the API)

```mermaid
erDiagram

    %% ── Analytics Domain (public schema) ─────────────────────
    customers {
        BIGINT id PK
        TEXT customer_name
        TEXT email "unique"
        TEXT segment "Consumer|SMB|Enterprise"
        TEXT region "West|Central|East|South"
        TEXT acquisition_channel
        TIMESTAMPTZ created_at
    }

    products {
        BIGINT id PK
        TEXT sku "unique"
        TEXT product_name
        TEXT category "Electronics|Home|Office|Fitness|Accessories"
        TEXT subcategory
        NUMERIC unit_cost
        BOOLEAN is_active
        TIMESTAMPTZ created_at
    }

    orders {
        BIGINT id PK
        BIGINT customer_id FK
        TIMESTAMPTZ order_date
        TEXT status "Completed|Cancelled|Refunded"
        TEXT sales_channel "Web|Mobile|Marketplace"
        TEXT shipping_region
    }

    order_items {
        BIGINT id PK
        BIGINT order_id FK
        BIGINT product_id FK
        INTEGER quantity
        NUMERIC unit_price
        NUMERIC discount_amount
    }

    customers ||--o{ orders : "places"
    orders ||--|{ order_items : "contains"
    products ||--o{ order_items : "appears in"

    %% ── Operational Schema (grounded schema) ─────────────────
    traces {
        TEXT trace_id PK
        TEXT request_id
        TEXT conversation_id "nullable"
        TEXT question
        JSONB query_plan "nullable"
        TEXT compiled_sql "nullable"
        TEXT failure_category "nullable"
        TIMESTAMPTZ started_at
        TIMESTAMPTZ completed_at
    }

    planner_attempts {
        TEXT trace_id FK
        TEXT prompt_key
        TEXT prompt_version
        TEXT provider
        TEXT model
        TEXT endpoint
        TIMESTAMPTZ requested_at
        TIMESTAMPTZ responded_at
        INTEGER latency_ms
        INTEGER tokens_in
        INTEGER tokens_out
        BOOLEAN success
        BOOLEAN repaired
        TEXT failure_category "nullable"
        JSONB raw_response "nullable"
    }

    conversation_state {
        TEXT conversation_id PK
        TEXT last_question
        JSONB last_query_plan
        TIMESTAMPTZ updated_at
    }

    eval_runs {
        TEXT run_id PK
        TIMESTAMPTZ started_at
        TIMESTAMPTZ completed_at
        INTEGER total_cases
        INTEGER passed_cases
        NUMERIC score
        TEXT prompt_key
        TEXT prompt_version
    }

    eval_results {
        TEXT run_id FK
        TEXT case_id
        TEXT question
        BOOLEAN execution_success
        BOOLEAN structural_correctness
        BOOLEAN answer_grounding
        NUMERIC weighted_score
        TEXT failure_category "nullable"
        TEXT notes "nullable"
    }

    traces ||--o| planner_attempts : "has"
    eval_runs ||--|{ eval_results : "contains"
```
