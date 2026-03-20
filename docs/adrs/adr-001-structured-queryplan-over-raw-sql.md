# ADR-001: Structured QueryPlan over raw SQL

## Status
Accepted

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

## Alternatives considered

### 1. Raw SQL generation by LLM
Rejected because validation would be weaker and failure modes would be broader and harder to explain.

### 2. Hybrid approach where LLM emits both plan and SQL
Rejected for MVP because it increases complexity without improving the core learning goals.

### 3. Rule-based intent parsing with no LLM planner
Rejected because the project specifically needs real LLM integration experience.