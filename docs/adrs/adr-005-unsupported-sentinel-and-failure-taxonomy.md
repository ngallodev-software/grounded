# ADR-005: `__unsupported__` sentinel and failure category taxonomy

## Status
Accepted

## Date
2026-03-20

## Context
When a user asks a question the planner cannot satisfy — an unsupported metric, an ambiguous request, a question outside the analytics domain — the system needs a well-defined signal to distinguish "the model tried but the question is out of scope" from "the model failed to produce valid JSON" or "the database returned an error."

Three options exist for communicating planner-level rejection:

1. The prompt instructs the model to return an error message in free-form text.
2. The model returns a structurally valid `QueryPlan` with a sentinel value that the validator rejects.
3. The model returns a special `error` object outside the `QueryPlan` shape.

Option 3 conflicts with Structured Outputs: a single strict JSON Schema cannot simultaneously describe a `QueryPlan` and an error object with different fields. Option 1 produces unstructured output that requires additional parsing. Option 2 keeps the response shape uniform and lets the validator own rejection logic.

Separately, the system produces failures at many layers: HTTP transport, API provider, JSON parsing, planner business validation, SQL safety, Postgres execution, and answer synthesis. Without a shared vocabulary, failure signals are inconsistent across logs, API responses, traces, and eval records.

## Decision

### Sentinel pattern for unsupported requests
The planner prompt instructs the model to emit `metric = "__unsupported__"` for any question it cannot map to a valid `QueryPlan`. The `QueryPlan` JSON Schema deliberately keeps `metric` as a plain `string` (not an enum) so the sentinel passes schema validation and reaches `QueryPlanValidator`.

`QueryPlanValidator` detects `__unsupported__` as `invalid_metric` and returns a `PlannerValidationFailure`. The validator, not the schema, owns the allowlist. This keeps the JSON Schema responsible for shape and the validator responsible for business rules — a clean separation that avoids duplicating allowlists in two places.

### Failure category taxonomy
All failures across all pipeline stages are tagged with a constant string from `FailureCategories`:

| Category | Layer |
|---|---|
| `none` | No failure |
| `transport_failure` | HTTP client error |
| `timeout` | Request timeout exceeded |
| `provider_error` | Non-2xx from LLM provider, or empty response |
| `json_parse_failure` | LLM output could not be parsed as JSON |
| `planner_validation_failure` | QueryPlan failed business validation (includes sentinel) |
| `unsupported_request` | Explicit `__unsupported__` sentinel |
| `sql_safety_failure` | SqlSafetyGuard blocked the compiled SQL |
| `execution_failure` | Postgres execution error |
| `synthesis_failure` | Answer synthesis failed or grounding check failed |

The category is surfaced on `ExecuteQueryPlanResponse.failureCategory`, `QueryExecutionTrace.failureCategory`, `PlannerTrace.failureCategory`, and persisted to `llm_traces`.

## Consequences

### Positive
- The response shape is always a `QueryPlan` — no special-casing in the invoker or gateway for error shapes.
- `QueryPlanValidator` is the single point that maps sentinel → failure category. The planner prompt and the validator stay in sync through `SqlFragmentRegistry`.
- The failure taxonomy makes eval scoring, regression detection, and log analysis unambiguous. Every failure has exactly one category.
- Structured Outputs remains strict (`strict: true`) because the sentinel fits within the existing schema.

### Negative
- The sentinel value `__unsupported__` is a prompt-level convention; if a future model misinterprets it or uses a different value, the validator will fall through to `invalid_metric` rather than `unsupported_request` — a minor category mismatch, not a safety issue.
- The taxonomy is a fixed enum; new failure modes require a code change to `FailureCategories` and coordinated updates to all consumers.

## Alternatives considered

### 1. Free-form error text in a separate field
Rejected because it requires parsing unstructured model output and cannot be validated by a JSON Schema.

### 2. Separate `error` shape in the JSON Schema
Rejected because Structured Outputs with `strict: true` requires a single root schema. A union root would require `anyOf`, which many providers do not support in strict mode.

### 3. Reject at the prompt level only (no sentinel)
Rejected because the model may still produce an invalid plan despite prompt instructions. The sentinel + validator combination provides two layers of rejection.
