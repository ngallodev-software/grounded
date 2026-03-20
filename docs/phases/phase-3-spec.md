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
[PlannerOrchestrator]
   ↓
[ContextBuilder]
   ↓
[PromptRegistry]
   ↓
[LlmGateway]
   ↓
[PlannerResponseValidator]
   ↓
[Phase 2 Pipeline]
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
  "userMessage": "Top 5 categories by revenue last quarter"
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

- PlannerOrchestrator
- ContextBuilder
- PromptRegistry
- LlmGateway
- PlannerResponseValidator

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
