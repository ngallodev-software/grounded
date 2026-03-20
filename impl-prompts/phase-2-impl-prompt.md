You are a senior backend engineer and pragmatic AI application architect.

You are executing Phase 2 of a tightly scoped AI analytics project.

Your task is to design and produce the Phase 2 implementation plan and code structure for the deterministic execution core.

Do not redesign the project.
Do not add features.
Do not introduce LLM integration.
Do not expand scope.

---------------------------------------
PROJECT CONTEXT (FIXED)
---------------------------------------

This project is a .NET-based natural-language analytics service over a fixed Postgres schema.

Phase 1 is already complete.
The domain, schema, metrics, allowed dimensions, allowed filters, and QueryPlan contract are already frozen.

The business schema is fixed to:
- customers
- products
- orders
- order_items

The eventual system flow will be:
user question -> LLM planner -> QueryPlan -> validation -> SQL compilation -> execution -> answer

But in Phase 2, there is NO LLM.

Phase 2 is ONLY about:
- accepting a manual QueryPlan
- validating it
- compiling it into deterministic SQL
- enforcing SQL safety
- executing it against Postgres
- returning results

---------------------------------------
STRICT SCOPE RULES
---------------------------------------

You MUST:
- stay inside Phase 2 only
- use ASP.NET Core Web API
- use Postgres
- keep architecture simple and explainable
- prefer deterministic rule-based logic

You MUST NOT:
- introduce prompts
- introduce model providers
- introduce chat endpoints
- introduce prompt versioning
- introduce vector search
- introduce RAG
- introduce multi-agent systems
- introduce auth, tenancy, or cloud deployment
- introduce a frontend
- introduce arbitrary SQL support

If something is not required for manual QueryPlan execution, do not add it.

---------------------------------------
GOAL
---------------------------------------

Design the execution core so that a developer can POST a manual QueryPlan JSON and get:
- validated input
- deterministic SQL
- safe execution
- result rows
- execution metadata

---------------------------------------
REQUIRED OUTPUT
---------------------------------------

Produce a single structured markdown document with the following sections.

Be concrete, implementation-oriented, and strict.

---------------------------------------
1. PHASE 2 OBJECTIVE
---------------------------------------
State the goal of this phase in exact terms.

---------------------------------------
2. REQUEST/RESPONSE CONTRACT
---------------------------------------
Define the API endpoint:
- route
- request shape
- response shape
- status code behavior

---------------------------------------
3. C# DOMAIN / DTO MODELS
---------------------------------------
Define the exact C# classes or records needed for:
- QueryPlan
- FilterSpec
- SortSpec
- TimeRangeSpec
- request DTO
- response DTO
- compiled query result
- execution result
- validation result

Use exact field names and explain each briefly.

---------------------------------------
4. VALIDATION RULES
---------------------------------------
Define strict validation rules for:
- route
- question type
- metric
- dimensions
- filters
- time range
- limit
- invalid combinations

Be explicit.
No vague language.

---------------------------------------
5. SQL COMPILATION DESIGN
---------------------------------------
Define exactly how QueryPlan becomes SQL.

Include:
- metric mapping rules
- dimension mapping rules
- filter mapping rules
- time range mapping rules
- join rules
- group by rules
- order by rules
- limit rules

Use deterministic mappings only.

---------------------------------------
6. SQL SAFETY RULES
---------------------------------------
Define the final execution-time safety checks.

Be strict:
- SELECT only
- single statement
- row cap
- timeout
- read-only connection
- parameterization

---------------------------------------
7. SERVICE BREAKDOWN
---------------------------------------
List the exact services/classes to build and what each owns.

---------------------------------------
8. BUILD ORDER
---------------------------------------
Give the exact order to implement the components.

Keep it practical.

---------------------------------------
9. TEST PLAN
---------------------------------------
Define at least 10 manual or automated test cases covering:
- aggregate
- grouped
- ranking
- time series
- invalid plan rejection
- safety failure cases

---------------------------------------
10. ACCEPTANCE CRITERIA
---------------------------------------
Define the exact checklist for Phase 2 completion.

---------------------------------------
OUTPUT RULES
---------------------------------------

- Output ONLY the markdown document
- No implementation code unless needed for example signatures
- No future-phase discussion
- No options list
- No brainstorming
- No fluff

---------------------------------------
QUALITY BAR
---------------------------------------

The result must be:
- directly implementable
- constrained
- deterministic
- safe
- consistent with a staff-level backend design

If anything is ambiguous, resolve it.

---------------------------------------
BEGIN
---------------------------------------