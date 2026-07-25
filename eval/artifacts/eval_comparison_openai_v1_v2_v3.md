# Openai Planner Prompt Comparison

| Version | Adjusted score | Adjusted pass rate | Raw score | Raw execution success | Raw grounding | Expected-failure passes |
|---|---:|---:|---:|---:|---:|---:|
| `v1` | 0.913 | 96.700% | 0.773 | 83.300% | 53.300% | 5/6 |
| `v2` | 0.880 | 93.300% | 0.747 | 80.000% | 53.300% | 5/6 |
| `v3` | 0.880 | 93.300% | 0.807 | 86.700% | 56.700% | 4/6 |

Best adjusted score: `v1` at `0.913`.

## Cases That Changed Across Versions

- `time_weekly_orders_7d`: v1=pass (none), v2=fail (planner_validation_failure), v3=pass (none)
- `unsupported_anomaly`: v1=pass (unsupported_request), v2=pass (unsupported_request), v3=fail (none)
