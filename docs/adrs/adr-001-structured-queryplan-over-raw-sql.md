# ADR-001: Structured QueryPlan over raw SQL

## Status
Accepted — updated 2026-03-20 (Structured Outputs enforcement)

## Date
2026-03-19

## Context
The system must answer natural-language analytics questions over a fixed Postgres schema.  
The application needs real LLM integration while preserving execution safety, debuggability, and deterministic guardrails.

A straightforward option would be to ask the LLM to generate raw SQL and execute it after basic checks.  
That approach is fast to prototype, but it increases risk:
- invalid SQL
- schema hallucinations
- brittle validation
- weak explainability
- more difficult regression testing

The project goal is not to maximize autonomy. It is to create a production-shaped, interview-defensible architecture that demonstrates LLM integration, prompt design, context management, validation, and evals.

## Decision
The LLM will produce a structured `QueryPlan` JSON document, not executable SQL.

Application code will:
- validate the `QueryPlan`
- enforce metric/dimension/filter whitelists
- compile the plan into parameterized SQL
- apply final SQL safety checks
- execute the query using a read-only database role

The LLM will not be allowed to emit free-form SQL for execution in the MVP.

## Consequences

### Positive
- safer execution path
- clearer separation of model reasoning vs application control
- easier validation and regression testing
- more deterministic behavior
- better traceability in logs and eval runs
- stronger interview story around guardrails

### Negative
- more application code must be written up front
- less flexible than unconstrained SQL generation
- some complex query patterns may require explicit compiler support

## Structured Outputs enforcement (2026-03-20)

The QueryPlan commitment was strengthened by switching the planner from JSON mode (`response_format: json_object`) to OpenAI Structured Outputs (`response_format: json_schema`, `strict: true`). The schema is defined in `QueryPlanSchema.cs` and passed in every planner request.

Key schema design constraint: `metric` and `dimension` are plain `string`/`string|null` in the JSON Schema — not enums. This allows the `__unsupported__` sentinel value (`metric = "__unsupported__"`) to pass through schema validation and reach `QueryPlanValidator`, which enforces business rules. The schema enforces shape; the validator enforces values. See ADR-005.

`temperature = 0` and `max_tokens = 500` are fixed on all model invocations. A 15-second HTTP timeout is the default.

## Alternatives considered

### 1. Raw SQL generation by LLM
Rejected because validation would be weaker and failure modes would be broader and harder to explain.

### 2. Hybrid approach where LLM emits both plan and SQL
Rejected for MVP because it increases complexity without improving the core learning goals.

### 3. Rule-based intent parsing with no LLM planner
Rejected because the project specifically needs real LLM integration experience.

### 4. JSON mode instead of Structured Outputs
Used initially; replaced when Structured Outputs became available. JSON mode allowed any valid JSON object — the model could omit required fields or emit unexpected keys. Structured Outputs eliminates an entire class of parse/repair failures by making schema violations impossible at the API level.