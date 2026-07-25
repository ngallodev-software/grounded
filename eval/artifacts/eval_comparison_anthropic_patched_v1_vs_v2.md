# Anthropic Patched Planner Prompt Comparison: v1 vs v2

| Version | Adjusted score | Adjusted pass rate | Raw score | Raw execution success | Raw grounding | Expected-failure passes |
|---|---:|---:|---:|---:|---:|---:|
| `v1` | 0.863 | 90.000% | 0.773 | 86.700% | 50.000% | 4/6 |
| `v2` | 0.907 | 96.700% | 0.773 | 83.300% | 53.300% | 5/6 |

Best adjusted score: `v2` at `0.907`.

## Case Deltas

- `ranking_top_products_units`: v1=fail (synthesis_failure), v2=pass (none)
- `time_weekly_orders_7d`: v1=pass (none), v2=pass (synthesis_failure)
- `unsupported_anomaly`: v1=fail (none), v2=pass (unsupported_request)
- `unsupported_multi_dim`: v1=fail (synthesis_failure), v2=fail (none)
