# Anthropic Planner Prompt Comparison

| Version | Adjusted score | Adjusted pass rate | Raw score | Raw execution success | Raw grounding | Expected-failure passes |
|---|---:|---:|---:|---:|---:|---:|
| `v1` | 0.550 | 56.700% | 0.400 | 46.700% | 23.300% | 5/6 |
| `v2` | 0.253 | 26.700% | 0.053 | 6.700% | 0.000% | 6/6 |
| `v3` | 0.217 | 20.000% | 0.017 | 3.300% | 0.000% | 6/6 |

Best adjusted score: `v1` at `0.550`.

## Cases That Changed Across Versions

- `agg_revenue_last_month`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `agg_orders_last_30_days`: v1=pass (synthesis_failure), v2=pass (synthesis_failure), v3=fail (provider_error)
- `agg_aov_qtd`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `agg_new_customers_mtd`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `agg_units_electronics`: v1=pass (synthesis_failure), v2=fail (provider_error), v3=fail (provider_error)
- `agg_revenue_mobile`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `group_revenue_category`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `group_orders_region`: v1=pass (none), v2=fail (provider_error), v3=fail (synthesis_failure)
- `group_units_segment`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `group_new_customers_channel`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `group_revenue_subcategory`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `group_revenue_sales_channel`: v1=pass (none), v2=fail (provider_error), v3=fail (provider_error)
- `time_weekly_orders_7d`: v1=fail (provider_error), v2=pass (synthesis_failure), v3=fail (provider_error)
- `unsupported_multi_dim`: v1=fail (synthesis_failure), v2=pass (provider_error), v3=pass (provider_error)
