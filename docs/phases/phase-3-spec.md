# Phase 3 Spec — Planner LLM Integration

## 1. Purpose

Integrate an LLM into the request pipeline to convert natural-language analytics questions into structured QueryPlans, which are validated and executed via the Phase 2 deterministic core.

---

## 2. High-Level Architecture

```
[Client]
   ↓
POST /analytics/query
   ↓
[AnalyticsQueryPlanService.ExecuteFromQuestionAsync]
   ↓
[PlannerContextBuilder + PlannerPromptRenderer]
   ↓
[ILlmPlannerGateway (OpenAiCompatiblePlannerGateway)]
   ↓
[PlannerResponseParser + PlannerResponseRepairService]
   ↓
[Phase 2 Pipeline (QueryPlanValidator → Compiler → Safety → Executor)]
   ↓
[Response]
```

---

## 3. Detailed Flow

```
User Message
   ↓
ContextBuilder
   ↓
Prompt + Context
   ↓
LLM Call
   ↓
Raw Output
   ↓
JSON Parse + Validate
   ↓
Valid QueryPlan?
 ├─ No → Repair (1x) → Reject
 └─ Yes
      ↓
Phase 2 Pipeline
      ↓
Results
```

---

## 4. API Contract

### Endpoint
POST /analytics/query

### Request
```json
{
  "question": "Top 5 categories by revenue last quarter",
  "conversationId": "optional-string-max-128-chars"
}
```

### Response
```json
{
  "queryPlan": {},
  "rows": [],
  "metadata": {
    "rowCount": 0,
    "durationMs": 0,
    "llmLatencyMs": 0
  }
}
```

---

## 5. Components

- `AnalyticsQueryPlanService` — orchestrates the full NL → execution path
- `PlannerContextBuilder` — builds the `PlannerContext` from `SqlFragmentRegistry`
- `PlannerPromptRenderer` — assembles the versioned prompt with context and user question
- `PromptStore` — loads versioned prompt files from disk (`prompts/planner/v1.md`)
- `ILlmPlannerGateway` / `OpenAiCompatiblePlannerGateway` — calls the model; `DeterministicLlmPlannerGateway` used in tests
- `PlannerResponseParser` — parses raw model JSON into `QueryPlan`
- `PlannerResponseRepairService` — attempts one repair pass (strip markdown fences, extract JSON object)

---

## 6. Context Strategy

### Included
- user question
- schema catalog
- metric glossary
- allowed dimensions
- few-shot examples

### Excluded
- full schema
- chat history
- SQL logs

---

## 7. Prompt Structure

- role
- rules
- schema
- examples
- strict JSON output

---

## 8. LLM Rules

- timeout: 20s
- retry: 1
- single model
- log tokens

---

## 9. Validation + Repair

```
Parse → Validate → Repair (1x) → Reject if invalid
```

---

## 10. Trace Logging

- requestId
- prompt version
- model
- input/output
- tokens
- latency

---

## 11. Build Order

1. ContextBuilder
2. PromptRegistry
3. LlmGateway
4. Validator
5. Orchestrator
6. Endpoint

---

## 12. Test Plan

- valid query
- malformed JSON
- unsupported
- timeout
- retry
- invalid schema
- execution success

---

## 13. Acceptance Criteria

- NL → QueryPlan works
- validation enforced
- repair works once
- logs captured
- Phase 2 reused
