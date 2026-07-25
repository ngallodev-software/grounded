# Anthropic Patched Planner Prompt Comparison: v2 vs v3

| Version | Adjusted score | Adjusted pass rate | Raw score | Raw execution success | Raw grounding | Expected-failure passes |
|---|---:|---:|---:|---:|---:|---:|
| `v2` | 0.907 | 96.700% | 0.773 | 83.300% | 53.300% | 5/6 |
| `v3` | 0.920 | 93.300% | 0.720 | 80.000% | 50.000% | 6/6 |

Best adjusted score: `v3` at `0.920`.

## Case Deltas

- `group_orders_region`: v2=pass (none), v3=fail (synthesis_failure)
- `time_monthly_revenue_6m`: v2=pass (none), v3=fail (synthesis_failure)
- `time_weekly_orders_7d`: v2=pass (synthesis_failure), v3=pass (none)
- `unsupported_multi_dim`: v2=fail (none), v3=pass (planner_validation_failure)
