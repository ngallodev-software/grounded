You are a senior backend engineer and pragmatic AI application architect.

You are executing Phase 3 of a tightly scoped AI analytics project.

Your task is to design the implementation plan for planner LLM integration.

Do not redesign the project.
Do not add features.
Do not expand scope.
Do not introduce answer synthesis, multi-agent workflows, or vector retrieval.

---------------------------------------
PROJECT CONTEXT (FIXED)
---------------------------------------

This is a .NET-based analytics service over a fixed Postgres schema.

Phase 1 is complete:
- domain is frozen
- schema is frozen
- metrics are frozen
- allowed analytics surface is frozen
- QueryPlan contract is frozen

Phase 2 is complete:
- QueryPlan validation exists
- deterministic SQL compilation exists
- SQL safety enforcement exists
- Postgres execution exists

The current system can execute a manual QueryPlan safely.

Phase 3 is ONLY about:
- accepting a natural-language analytics question
- building bounded planner context
- loading a versioned planner prompt from disk
- calling an LLM
- parsing structured QueryPlan output
- validating it
- optionally repairing malformed JSON once
- passing valid output into the existing Phase 2 pipeline
- logging trace data

---------------------------------------
STRICT SCOPE RULES
---------------------------------------

You MUST:
- stay inside Phase 3 only
- use ASP.NET Core Web API
- keep architecture simple and explainable
- use one planner prompt only
- use one model only
- keep all execution enforcement in application code

You MUST NOT:
- add answer synthesis
- add conversation memory
- add multi-turn orchestration
- add vector search or RAG
- add semantic example retrieval
- add multi-agent systems
- add model routing
- add a frontend
- add auth, tenancy, or cloud deployment
- let raw model output be executed directly

If something is not necessary for natural-language-to-QueryPlan integration, do not add it.

---------------------------------------
GOAL
---------------------------------------

Design the request flow so that a developer can POST a natural-language analytics question and get:
- bounded context assembly
- planner prompt loading
- LLM call
- structured QueryPlan output
- validation
- safe handoff into the existing deterministic execution pipeline
- result rows and trace metadata

---------------------------------------
REQUIRED OUTPUT
---------------------------------------

Produce a single structured markdown document with the following sections.

Be concrete, implementation-oriented, and strict.

---------------------------------------
1. PHASE 3 OBJECTIVE
---------------------------------------
State the exact goal of this phase.

---------------------------------------
2. REQUEST/RESPONSE CONTRACT
---------------------------------------
Define:
- API route
- request shape
- response shape
- status code behavior

---------------------------------------
3. COMPONENTS TO BUILD
---------------------------------------
Define the exact components/classes/services needed for:
- context building
- prompt loading
- LLM access
- planner orchestration
- planner response validation
- trace logging

Use concrete names.

---------------------------------------
4. CONTEXT PACKAGING DESIGN
---------------------------------------
Define exactly what goes into the planner call.

Include:
- current user question
- schema context
- metric glossary
- allowed dimensions/filters
- few-shot examples
- output contract

Also define what must be excluded.

Keep it bounded and deterministic.

---------------------------------------
5. PROMPT DESIGN
---------------------------------------
Define the planner prompt structure:
- role
- instructions
- output contract
- few-shot examples
- domain boundaries

Do not design more than one prompt.

---------------------------------------
6. LLM GATEWAY RULES
---------------------------------------
Define:
- timeout
- retry rules
- model config
- raw response handling
- token/latency logging

Keep it single-model and minimal.

---------------------------------------
7. OUTPUT VALIDATION AND REPAIR FLOW
---------------------------------------
Define:
- JSON parsing rules
- schema validation rules
- one repair attempt policy
- when to reject without retry
- how validated output enters the Phase 2 pipeline

---------------------------------------
8. TRACE LOGGING
---------------------------------------
Define the exact fields that should be logged for each planner call.

---------------------------------------
9. BUILD ORDER
---------------------------------------
Give the exact order to implement the components.

---------------------------------------
10. TEST PLAN
---------------------------------------
Define at least 10 Phase 3 test cases covering:
- successful planner output
- malformed JSON
- unsupported route
- needs clarification
- timeout handling
- semantic validation failure
- successful execution through Phase 2

---------------------------------------
11. ACCEPTANCE CRITERIA
---------------------------------------
Define the exact checklist for Phase 3 completion.

---------------------------------------
OUTPUT RULES
---------------------------------------

- Output ONLY the markdown document
- No implementation code unless needed for signatures or examples
- No future-phase discussion
- No options list
- No fluff

---------------------------------------
QUALITY BAR
---------------------------------------

The result must be:
- directly implementable
- constrained
- safe
- consistent with the fixed project architecture
- strong enough to support interview discussion of real LLM integration

If anything is ambiguous, resolve it.

---------------------------------------
BEGIN
---------------------------------------