# Structured Outputs Upgrade: Grounded Planner

## Objective

Upgrade the Grounded planner from JSON mode to Structured Outputs using a JSON Schema-backed response format.

**Goal:** Improve planner reliability by enforcing QueryPlan structure at model generation time, while preserving the existing validation/compiler/safety architecture.

---

## Core Architecture (Immutable)

```
LLM → structured QueryPlan → QueryPlanValidator → QueryPlanCompiler → SQL safety guard → execution
```

All stages must remain intact and functional. No architectural shortcuts or removals.

---

## Non-Negotiable Constraints

These constraints are not flexible and must be respected throughout implementation:

| # | Constraint |
|---|-----------|
| 1 | Do NOT remove or weaken `QueryPlanValidator` |
| 2 | Do NOT remove or weaken `QueryPlanCompiler` |
| 3 | Do NOT remove or weaken SQL safety checks |
| 4 | Do NOT replace semantic/business validation with schema validation |
| 5 | Do NOT change the meaning of unsupported-request handling |
| 6 | Do NOT redesign the QueryPlan contract unless absolutely required |
| 7 | Do NOT add new product features, memory, synthesis changes, UI work, or unrelated cleanup |
| 8 | Keep changes small, reviewable, and directly tied to planner reliability |
| 9 | Preserve existing failure taxonomy and traceability |
| 10 | If parser/repair logic already exists, keep it unless clearly obsolete; reduce usage but do not rip it out casually |

---

## Implementation Steps

### 1. Inspect Current Planner Call Path

**Identify:**
- Where JSON mode is currently used
- Current planner request/response flow
- Current QueryPlan model, validator, parser, and trace metadata

### 2. Add JSON Schema-Backed Structured Output Path

**Changes:**
- Replace plain JSON mode for the planner with Structured Outputs using `response_format` type `json_schema`
- Keep temperature low/deterministic
- Keep timeout/retry behavior conservative

### 3. Build or Generate JSON Schema for QueryPlan

**Preferred Approach:**
- Generate the schema from the existing C# QueryPlan-related models if feasible
- Simplify/adjust to be OpenAI-compatible

### 4. Keep Semantic Validation Downstream

Schema enforces *shape*, not business rules.

**Examples of checks that remain in `QueryPlanValidator`:**
- Allow-listed metrics
- Allow-listed dimensions
- Allow-listed filter fields and values
- Ranking requires explicit limit (1-50)
- Time range/business rules
- Unsupported sentinel behavior
- `usePriorState=false` rule (if current design)
- Any domain-specific semantics

### 5. Preserve Canonical Unsupported Behavior

- Unsupported, ambiguous, non-analytic, or free-form SQL requests must be represented the same way the system currently expects
- Do NOT invent a new unsupported mechanism
- If current design uses a canonical invalid sentinel (e.g., `metric="__unsupported__"`), preserve that behavior
- Ensure prompt + validator still support it

### 6. Update Planner Prompt (Only as Needed)

**Remove:**
- Instructions that only compensate for plain JSON mode

**Keep:**
- No SQL generation
- No guessing unsupported entities
- No guessing ambiguous intent
- Canonical unsupported behavior
- Ranking-limit rules
- Bounded planning behavior

**Align:**
- Prompt examples with schema and current validator behavior

### 7. Update Traces and Diagnostics

- Clearly indicate planner uses Structured Outputs / `json_schema` response format
- Preserve token/latency/error tracing
- Preserve categorized failure reporting

### 8. Update or Add Tests

**Minimum Required Test Coverage:**

- ✓ Valid aggregate request returns schema-conformant QueryPlan
- ✓ Valid grouped/ranking/time-series requests return schema-conformant QueryPlan
- ✓ Unsupported request still follows canonical unsupported behavior
- ✓ Ambiguous request still follows canonical unsupported behavior
- ✓ Ranking without explicit limit still fails deterministically through existing downstream validation
- ✓ Parser/repair path behavior updated appropriately (if still needed)
- ✓ QueryPlanValidator still runs and rejects semantically invalid plans even if shape is valid

---

## Critical Schema Constraints

These constraints ensure the schema is compatible with the existing codebase architecture.

### Contract Fidelity (Non-Negotiable)

| Requirement | Details |
|---|---|
| **Match C# exactly** | Same property names, casing, nullability, nesting |
| **No field changes** | Do NOT rename fields or introduce new ones |
| **Do NOT encode business rules** | No allow-lists, ranking limits, or domain constraints in schema |

### Schema Responsibility (Limited Scope)

The schema is responsible for **structure only**:

- ✓ Required fields
- ✓ Object structure
- ✓ Enums (only where already fixed in code)
- ✓ Array typing
- ✓ Nullability

### Validator Responsibility (Remains Authoritative)

The `QueryPlanValidator` remains responsible for:

- ✓ Metrics/dimension allow-lists
- ✓ Filter validation
- ✓ Ranking limit rules (1-50)
- ✓ Unsupported sentinel logic
- ✓ Time semantics and business rules

### Decision Rule

**If there is any ambiguity between schema vs validator: prefer keeping logic in `QueryPlanValidator`**

### Schema Generation

Derive directly from C# model using:
- `System.Text.Json` serialization shape, OR
- Manual mapping from model definitions

---

## Suggested Schema Structure

Adapt to exact C# contract names and nullability. This is a reference template, not absolute specification.

### Top-Level Object

```
version                 string
questionType            enum: "aggregate" | "grouped_breakdown" | "ranking" | "time_series"
metric                  string
dimension               string | null
filters                 array of FilterSpec
timeRange               TimeRangeSpec
timeGrain               string | null
sort                    SortSpec
limit                   integer | null
usePriorState           boolean
```

### FilterSpec

```
field                   string
operator                string
values                  array of string
```

### SortSpec

```
by                      enum: "metric" | "dimension"
direction               enum: "asc" | "desc"
```

### TimeRangeSpec

```
preset                  string | null
startDate               string | null
endDate                 string | null
```

### Important Mapping Rules

- If current contract uses different property names or casing, **match the codebase**, not this template
- If nullability differs in code, **follow code**
- If additional fields exist in real contract, include them **only if genuinely part of current planner output**

---

## Implementation Philosophy

- **Structured Outputs reduce shape/parsing failures**, not semantic failures
- **`QueryPlanValidator` remains the authority** for semantic correctness
- **Do NOT encode every validator rule** into JSON Schema if it makes schema brittle/overcomplicated
- **Prefer practical split:**
  - Schema = structure + required fields + enums + nested shape
  - Validator = domain/business constraints

---

## Deliverables

1. Files changed
2. Exact planner call change from JSON mode to Structured Outputs
3. Final JSON Schema added/generated
4. Explanation of what remains in `QueryPlanValidator` vs what moved into schema enforcement
5. Test changes
6. Compatibility risks or limitations

## Success Criteria

- ✓ Planner uses Structured Outputs with JSON Schema
- ✓ `QueryPlanValidator` remains in place and active
- ✓ Compiler/safety path unchanged
- ✓ Semantic/business-rule failures still happen downstream as intended
- ✓ Planner reliability improves without architecture drift