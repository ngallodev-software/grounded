# Phase 3 Artifact

## 1. Phase 3 Objective

Phase 3 adds a bounded planner LLM step in front of the existing Phase 2 deterministic execution core.

The exact goal is to accept a natural-language analytics question, assemble a deterministic planner context package from frozen domain artifacts, load one versioned planner prompt from disk, call one configured LLM, parse a structured `QueryPlan` from the model response, validate it, optionally repair malformed JSON once, and pass only a validated `QueryPlan` into the existing Phase 2 validation, SQL compilation, safety enforcement, and Postgres execution pipeline.

Phase 3 does not add answer synthesis, conversation memory, retrieval, model routing, or any execution logic inside the model boundary.

## 2. Request/Response Contract

### Route

- Method: `POST`
- Route: `/analytics/query`
- Content type: `application/json`

This route is the only Phase 3 entry point. It accepts natural language only. It does not accept raw SQL and does not accept a caller-supplied `QueryPlan`.

### Request shape

```json
{
  "question": "Top 5 product categories by revenue last quarter"
}
```

### Request DTO

```csharp
public sealed record ExecuteAnalyticsQuestionRequest(
    string Question);
```

- `Question`: required, trimmed, length `1..500` characters

### Response shape

Successful planning and execution returns:

```json
{
  "status": "success",
  "queryPlan": {
    "version": "1.0",
    "questionType": "ranking",
    "metric": "revenue",
    "dimension": "product_category",
    "filters": [],
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
  },
  "rows": [
    {
      "dimension": "Electronics",
      "metric": 125430.55
    }
  ],
  "metadata": {
    "rowCount": 1,
    "durationMs": 31,
    "planner": {
      "requestId": "8f2a55b2a4f44ab08f935df3fb7a91e4",
      "promptVersion": "planner-v1",
      "model": "gpt-5-mini",
      "llmLatencyMs": 1180,
      "repairAttempted": false,
      "repairSucceeded": false
    },
    "execution": {
      "compiledSql": "SELECT ...",
      "parameters": {
        "p0": "2025-01-01",
        "p1": "2025-04-01"
      },
      "appliedRowLimit": 5,
      "timeRangeStartUtc": "2025-01-01T00:00:00Z",
      "timeRangeEndExclusiveUtc": "2025-04-01T00:00:00Z"
    }
  }
}
```

Rejected requests return:

```json
{
  "status": "error",
  "errors": [
    {
      "code": "planner_validation_failed",
      "message": "questionType 'aggregate' requires dimension = null"
    }
  ],
  "metadata": {
    "planner": {
      "requestId": "8f2a55b2a4f44ab08f935df3fb7a91e4",
      "promptVersion": "planner-v1",
      "model": "gpt-5-mini",
      "llmLatencyMs": 1180,
      "repairAttempted": true,
      "repairSucceeded": false
    }
  }
}
```

### Response DTOs

```csharp
public sealed record ExecuteAnalyticsQuestionResponse(
    string Status,
    QueryPlan? QueryPlan,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Rows,
    ExecuteAnalyticsQuestionMetadata? Metadata,
    IReadOnlyList<ValidationErrorDto>? Errors);

public sealed record ExecuteAnalyticsQuestionMetadata(
    int? RowCount,
    long? DurationMs,
    PlannerTraceDto Planner,
    QueryExecutionMetadata? Execution);

public sealed record PlannerTraceDto(
    string RequestId,
    string PromptVersion,
    string Model,
    long LlmLatencyMs,
    bool RepairAttempted,
    bool RepairSucceeded);
```

### Status code behavior

- `200 OK`: planner produced a valid `QueryPlan`, Phase 2 validation passed, SQL passed safety checks, and execution completed
- `400 Bad Request`: malformed HTTP JSON body, missing `question`, empty `question`, or request body shape invalid
- `422 Unprocessable Entity`: planner output parsed but failed `QueryPlan` validation, planner returned a domain-unsupported request, or repair still produced invalid output
- `504 Gateway Timeout`: planner LLM call exceeded the configured timeout
- `500 Internal Server Error`: unexpected application failure outside the defined validation and timeout cases

No other status code is required for Phase 3.

## 3. Components To Build

### API layer

- `AnalyticsQueryController`
  - Owns `POST /analytics/query`
  - Validates HTTP request shape
  - Calls `AnalyticsPlannerOrchestrator`
  - Maps orchestrator results into HTTP responses

### Application layer

- `AnalyticsPlannerOrchestrator`
  - End-to-end coordinator for Phase 3
  - Creates planner request ID
  - Calls context builder, prompt loader, LLM gateway, response parser, validator, repair path, and Phase 2 execution service

- `PlannerContextBuilder`
  - Builds the bounded planner context package from frozen Phase 1 artifacts
  - Emits deterministic sections in fixed order

- `PlannerPromptRegistry`
  - Loads one prompt file from disk
  - Exposes `PromptVersion`, raw template text, and checksum
  - Fails startup if the configured prompt file is missing

- `PlannerPromptRenderer`
  - Combines prompt template plus bounded context plus current question into the final planner request payload
  - Performs no inference and no conditional prompt selection

- `PlannerLlmGateway`
  - Thin wrapper over one model deployment
  - Accepts a rendered planner request and returns raw text plus usage and latency
  - Owns timeout handling and one transient retry for transport failures only

- `PlannerResponseParser`
  - Extracts JSON text from raw model output
  - Parses JSON into a `QueryPlan`
  - Rejects prose-only or multi-object outputs

- `PlannerOutputValidator`
  - Runs Phase 2 `QueryPlanValidator`
  - Adds Phase 3-specific checks for planner boundary conditions such as `usePriorState = false`
  - Returns ordered `ValidationError` values

- `PlannerRepairService`
  - Performs one repair call when the first planner response is malformed JSON but still plausibly recoverable
  - Uses the same prompt family and same model
  - Never repairs semantic validation failures

- `PlannerTraceLogger`
  - Writes one structured trace record per planner attempt
  - Logs initial and repair attempts separately under one request ID

- `AnalyticsQueryPlanExecutionService`
  - Existing Phase 2 facade reused here
  - Accepts only a validated `QueryPlan`
  - Returns `QueryExecutionResult`

### Domain / config models

- `PlannerContextPackage`
- `PlannerPromptTemplate`
- `PlannerRequest`
- `PlannerResponse`
- `PlannerAttemptTrace`
- `PlannerSettings`

## 4. Context Packaging Design

The planner call receives one bounded, deterministic context package assembled in code in this exact order:

1. Current user question
2. QueryPlan output contract
3. Supported question types
4. Metric glossary
5. Dimensions whitelist
6. Filters whitelist
7. Time range vocabulary
8. Time grain rules
9. Sort and limit rules
10. Fixed domain boundaries
11. Few-shot examples

### Included data

#### Current user question

- The exact trimmed `question` string from the request

#### Schema context

- Table names only: `customers`, `products`, `orders`, `order_items`
- Business relationship summary only:
  - `orders.customer_id -> customers.id`
  - `order_items.order_id -> orders.id`
  - `order_items.product_id -> products.id`
- Time anchor summary:
  - standard metrics use `orders.order_date`
  - `new_customer_count` uses first completed order date

#### Metric glossary

- Canonical metric names from Phase 1:
  - `revenue`
  - `order_count`
  - `units_sold`
  - `average_order_value`
  - `new_customer_count`
- Short deterministic definition per metric

#### Allowed dimensions and filters

- Full dimensions whitelist from Phase 1
- Full filters whitelist from Phase 1
- Enum-backed allowed values for:
  - `customer_region`
  - `customer_segment`
  - `acquisition_channel`
  - `product_category`
  - `sales_channel`
  - `shipping_region`
- Explicit note that `product_subcategory` and `product_name` are free-text string values

#### Few-shot examples

- Exactly 4 examples
- One each for:
  - `aggregate`
  - `grouped_breakdown`
  - `ranking`
  - `time_series`
- Each example includes:
  - user question
  - valid `QueryPlan` JSON only
- Examples are stored in source-controlled JSON or markdown fragments and emitted unchanged

#### Output contract

- Full `QueryPlan` JSON shape
- Required fields
- Allowed enums
- `additionalProperties = false`
- Rule that the response must be a single JSON object and nothing else
- Rule that `usePriorState` must always be `false` in Phase 3

### Excluded data

- Full DDL
- Raw SQL examples
- Database connection details
- Runtime query results
- Chat history
- Prior request state
- Prompt alternatives
- Evaluation notes
- Error stack traces
- Any unsupported metrics, dimensions, filters, or date vocabularies

### Boundedness rules

- The context builder uses only frozen files and constants checked into the repo
- The section order never changes
- The few-shot example count is fixed at `4`
- No dynamic retrieval or example selection is allowed
- No schema introspection is allowed at request time

## 5. Prompt Design

One planner prompt is used for all Phase 3 requests.

### Prompt file

- Path: `prompts/planner-v1.md`
- Version ID: `planner-v1`

### Structure

#### Role

The model is instructed to act as a planner that converts one analytics question into one valid `QueryPlan` JSON object for the frozen e-commerce analytics contract.

#### Instructions

- Produce exactly one `QueryPlan` JSON object
- Use only the provided canonical metric, dimension, filter, time range, and time grain values
- Do not produce SQL
- Do not explain reasoning
- Do not add markdown fences
- Do not add commentary before or after JSON
- Set `usePriorState` to `false`
- If the question is outside the supported analytics surface, emit the best explicit unsupported `QueryPlan` candidate only if it is still contract-valid; otherwise emit contract-shaped JSON that Phase 2 validation will reject deterministically

#### Output contract section

- Embedded `QueryPlan` schema summary
- Required field list
- Allowed enum values
- Single-object-only rule

#### Few-shot section

- Four fixed examples from the bounded context package

#### Domain boundaries section

- Only the fixed e-commerce domain is valid
- No forecasting
- No causal analysis
- No comparisons across two independent ranges in one request
- No multiple dimensions
- No unsupported filters
- No free-form date math outside the preset vocabulary or valid `custom_range`

The prompt must not include any second-stage answer generation instructions.

## 6. LLM Gateway Rules

### Model configuration

- One model only: `gpt-5-mini`
- Temperature: `0`
- Max output tokens: `800`
- Response format target: plain text containing one JSON object

### Timeout

- Per attempt timeout: `20` seconds

### Retry rules

- No automatic retry for model-content failures
- One automatic retry for transient transport failures only:
  - connection reset
  - upstream `429`
  - upstream `5xx`
- Retry delay: `250` milliseconds
- Maximum planner attempts before repair path: `2` transport attempts for the same prompt payload

### Raw response handling

- Capture raw text exactly as returned
- Trim leading and trailing whitespace before parsing
- Do not execute or trust any content until parsing and validation succeed
- If markdown code fences are present, strip one outer fenced block before JSON parse
- If more than one JSON object is present, reject as malformed and route to repair only if the first parse failure is syntactic

### Usage logging

- Log prompt token count if provider returns it
- Log completion token count if provider returns it
- Log total token count if provider returns it
- Log request start UTC
- Log request end UTC
- Log latency in milliseconds

## 7. Output Validation And Repair Flow

### Initial parse flow

1. Receive raw planner text
2. Trim whitespace
3. Strip one outer markdown fence if present
4. Parse as exactly one JSON object
5. Deserialize into `QueryPlan`
6. Reject immediately if deserialization leaves unknown fields or missing required fields
7. Run Phase 2 `QueryPlanValidator`
8. Enforce `usePriorState = false`

### JSON parsing rules

- Top-level payload must be one JSON object
- Arrays, strings, or mixed prose plus JSON are invalid
- Duplicate property names are invalid
- Unknown properties are invalid
- Enum values are case-sensitive
- `null` is allowed only where the frozen contract allows it

### Schema validation rules

- `version` must equal `1.0`
- All frozen required fields must be present
- `questionType`, `metric`, `dimension`, `filters`, `timeRange`, `timeGrain`, `sort`, `limit`, and `usePriorState` must satisfy the Phase 1 and Phase 2 rules
- `simple_follow_up` is rejected in Phase 3 because no conversation state exists
- Semantic combinations are validated by the existing Phase 2 validator and returned unchanged

### One repair attempt policy

Repair is allowed exactly once and only when the first planner output failed due to syntactic JSON issues that are plausibly recoverable, such as:

- trailing commentary around otherwise recognizable JSON
- truncated closing brace
- markdown fences around a single JSON object
- minor quoting or comma errors

Repair is not allowed for:

- planner timeout
- transport failure after retry exhaustion
- semantically invalid but well-formed `QueryPlan`
- unsupported question shape that maps to contract-invalid values
- empty model response

### Repair call behavior

- Use the same model
- Use the same prompt version
- Send the original question, original bounded context, and the raw malformed output
- Instruct the model only to return corrected JSON for the same intended `QueryPlan`
- If repair output still fails parse or validation, return `422`

### Handoff into Phase 2

Only a fully validated `QueryPlan` is passed to the existing Phase 2 execution facade.

Phase 2 remains responsible for:

- final `QueryPlan` business validation
- time-range resolution
- SQL compilation
- SQL safety enforcement
- database execution

Phase 3 does not bypass or weaken any Phase 2 checks.

## 8. Trace Logging

One trace record must be written for each planner attempt. The exact fields are:

- `requestId`
- `route`
- `question`
- `questionLength`
- `promptVersion`
- `promptChecksum`
- `model`
- `attemptType` with values `initial` or `repair`
- `attemptNumber`
- `contextBytes`
- `fewShotExampleCount`
- `requestStartedUtc`
- `requestCompletedUtc`
- `llmLatencyMs`
- `timeoutMs`
- `transportRetried`
- `transportRetryCount`
- `providerRequestId` if available
- `promptTokens` if available
- `completionTokens` if available
- `totalTokens` if available
- `rawResponseText`
- `rawResponseBytes`
- `parseSucceeded`
- `repairAttempted`
- `repairSucceeded`
- `validationSucceeded`
- `validationErrors`
- `finalStatus` with values `success`, `validation_failed`, `timeout`, `transport_error`, `internal_error`
- `queryPlanJson` when parse succeeds
- `executionDurationMs` when Phase 2 execution succeeds

Raw trace persistence can be file-based structured logging or database-backed logging, but the field set must remain the same.

## 9. Build Order

1. Add Phase 3 request and response DTOs plus planner settings models.
2. Implement `PlannerPromptRegistry` to load `prompts/planner-v1.md` at startup and expose prompt version and checksum.
3. Implement `PlannerContextBuilder` using frozen Phase 1 constants and fixed few-shot examples.
4. Implement `PlannerPromptRenderer` to build the final single planner payload.
5. Implement `PlannerLlmGateway` with timeout, one transport retry, and usage capture.
6. Implement `PlannerResponseParser` for JSON extraction and strict deserialization.
7. Implement `PlannerOutputValidator` by reusing the existing Phase 2 `QueryPlanValidator`.
8. Implement `PlannerRepairService` with the one-repair policy.
9. Implement `PlannerTraceLogger` and wire trace emission around all planner attempts.
10. Implement `AnalyticsPlannerOrchestrator` to coordinate the complete Phase 3 flow and call the existing Phase 2 execution facade.
11. Add `AnalyticsQueryController` and HTTP status mapping.
12. Add integration tests covering planner success, rejection, repair, timeout, and Phase 2 handoff.

## 10. Test Plan

1. Valid ranking question returns `200`, a validated `QueryPlan`, result rows, and planner metadata.
2. Valid aggregate question returns `200` with `dimension = null` and no repair attempt.
3. Planner returns JSON inside markdown fences; parser strips the fence and execution still succeeds.
4. Planner returns malformed JSON with a recoverable missing brace; repair runs once and execution succeeds.
5. Planner returns malformed JSON that remains invalid after repair; API returns `422`.
6. Planner returns well-formed JSON with unsupported `metric`; validation fails and API returns `422` without repair.
7. Planner returns `questionType = simple_follow_up`; validator rejects it with `422`.
8. Planner call exceeds `20` seconds; API returns `504` and logs timeout trace fields.
9. Planner transport call returns transient `429` once, then succeeds on retry; request returns `200` and logs one transport retry.
10. Planner transport call returns repeated upstream `5xx`; retry exhausts and API returns `500` or mapped transport failure status according to gateway policy.
11. HTTP request body is missing `question`; API returns `400` before any planner call.
12. Unsupported route such as `POST /analytics/query-plan-nl` returns framework `404`; no planner trace is written.
13. Planner emits semantically invalid combination such as `aggregate` plus non-null `dimension`; Phase 2 validator rejects with `422`.
14. Planner emits valid `time_series` plan; Phase 2 execution succeeds and returns ordered `time_bucket` rows.
15. Successful planner output flows through Phase 2 and logs both planner latency and execution duration.

## 11. Acceptance Criteria

- `POST /analytics/query` accepts a natural-language question and rejects invalid HTTP payloads with `400`.
- One versioned planner prompt is loaded from disk and used for every Phase 3 request.
- Bounded context assembly is deterministic, fixed-order, and contains only approved Phase 1 artifacts.
- One model only is used for planner calls.
- Planner calls enforce a `20` second timeout and only one transport retry for transient failures.
- Raw model output is never executed directly.
- Planner output is parsed as strict JSON and deserialized into the frozen `QueryPlan` contract.
- One repair attempt occurs only for syntactic JSON failures and never for semantic validation failures.
- Every accepted `QueryPlan` is validated by the existing Phase 2 validator before execution.
- Only validated `QueryPlan` objects are passed into the Phase 2 deterministic pipeline.
- Successful requests return `queryPlan`, `rows`, and planner plus execution metadata.
- Failed planner requests return deterministic error payloads with trace metadata.
- Planner traces include prompt version, model, raw output, latency, token usage when available, validation outcome, and repair flags.
- The Phase 3 test suite covers success, malformed JSON, unsupported question shapes, timeout handling, semantic validation failure, unsupported route behavior, and successful Phase 2 execution handoff.
