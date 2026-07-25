# OpenAI vs Patched Anthropic by Prompt Version

| Version | OpenAI adjusted score | Anthropic adjusted score | Delta (Anthropic - OpenAI) | OpenAI pass rate | Anthropic pass rate |
|---|---:|---:|---:|---:|---:|
| `v1` | 0.913 | 0.863 | -0.050 | 96.700% | 90.000% |
| `v2` | 0.880 | 0.907 | 0.027 | 93.300% | 96.700% |
| `v3` | 0.880 | 0.920 | 0.040 | 93.300% | 93.300% |

## Per-Version Case Deltas

### `v1`
- `ranking_top_products_units`: openai=pass (none), anthropic=fail (synthesis_failure)
- `unsupported_anomaly`: openai=pass (unsupported_request), anthropic=fail (none)
- `unsupported_multi_dim`: openai=fail (none), anthropic=fail (synthesis_failure)

### `v2`
- `time_weekly_orders_7d`: openai=fail (planner_validation_failure), anthropic=pass (synthesis_failure)

### `v3`
- `group_orders_region`: openai=pass (none), anthropic=fail (synthesis_failure)
- `time_monthly_revenue_6m`: openai=pass (none), anthropic=fail (synthesis_failure)
- `unsupported_anomaly`: openai=fail (none), anthropic=pass (unsupported_request)
- `unsupported_multi_dim`: openai=fail (none), anthropic=pass (planner_validation_failure)
