You are a senior backend engineer and AI application architect.

You are executing Phase 1 of a tightly scoped AI portfolio project.

Your job is to produce ONLY Phase 1 artifacts with zero scope creep.

Do not suggest new features. Do not redesign the system. Do not introduce new concepts.

---------------------------------------
PROJECT CONTEXT (DO NOT MODIFY)
---------------------------------------

We are building a .NET-based natural-language analytics API over a fixed Postgres schema.

Core flow (for later phases, not to implement now):
- user question → LLM planner → structured QueryPlan → validated → compiled to SQL → executed → answer synthesized

Phase 1 is ONLY about:
- freezing the domain
- defining schema
- defining metrics
- defining allowed analytics surface
- defining QueryPlan contract
- creating benchmark dataset

NO LLM integration in this phase.

---------------------------------------
STRICT SCOPE RULES
---------------------------------------

You MUST:
- stay within e-commerce analytics domain
- use ONLY these tables: customers, products, orders, order_items
- support ONLY these question types:
  1. aggregate
  2. grouped breakdown
  3. ranking
  4. time series
  5. simple follow-up

You MUST NOT:
- introduce RAG, embeddings, or vector search
- introduce multi-agent systems
- add new domains or schemas
- add auth, UI, or cloud concerns
- generate implementation code beyond schema/seed scripts
- expand beyond Phase 1 artifacts

If you feel tempted to add something → DO NOT.

---------------------------------------
REQUIRED OUTPUTS
---------------------------------------

Produce a single, clean, structured markdown document with the following sections.

Be concise, precise, and deterministic.

---------------------------------------
1. FINAL DOMAIN DEFINITION
---------------------------------------
- exact problem statement
- supported question categories
- explicit unsupported cases

---------------------------------------
2. POSTGRES SCHEMA (FINAL)
---------------------------------------
Provide exact SQL for:

- customers
- products
- orders
- order_items

Include:
- constraints
- realistic field types
- allowed enum values (documented)

---------------------------------------
3. METRIC GLOSSARY (CANONICAL)
---------------------------------------
Define precisely:

- revenue
- order_count
- units_sold
- average_order_value
- new_customer_count

Each must:
- be unambiguous
- specify filters (e.g. Completed orders only)
- be implementable in SQL

---------------------------------------
4. ALLOWED ANALYTICS SURFACE
---------------------------------------

Explicitly define:

Dimensions (whitelist)
Filters (whitelist)
Time ranges (controlled vocabulary)

This will later be enforced in validation.

---------------------------------------
5. QUERYPLAN CONTRACT (STRICT)
---------------------------------------

Define the exact JSON structure the LLM will produce.

Include:
- full JSON schema (fields + types)
- allowed enum values
- example valid QueryPlan
- example invalid QueryPlan

This must be strict and minimal.

---------------------------------------
6. COMPRESSED SCHEMA CONTEXT (PROMPT INPUT)
---------------------------------------

Produce a JSON structure that:
- describes tables in business terms
- lists only relevant fields
- includes relationships

This will be used in prompts later.

Keep it compact and intentional.

---------------------------------------
7. SEED DATA PLAN
---------------------------------------

Define:
- record counts
- time span
- distribution rules
- important skew (very important)
- repeat customer behavior

DO NOT generate actual data yet.
Only define the plan.

---------------------------------------
8. BENCHMARK DATASET (30 CASES)
---------------------------------------

Produce 30 benchmark cases.

Each case must include:

- id
- category
- question
- prior_state (if any)
- expected_route
- expected_metric
- expected_dimensions
- expected_filters
- expected_time_range
- should_execute

Coverage must include:
- aggregates
- grouped
- rankings
- time series
- follow-ups (multi-turn)
- unsupported cases

---------------------------------------
9. ACCEPTANCE CRITERIA
---------------------------------------

Define a strict checklist that must be true before moving to Phase 2.

---------------------------------------
OUTPUT RULES
---------------------------------------

- Output ONLY the markdown document
- No explanations outside the doc
- No “options” or “alternatives”
- No fluff
- No future-phase discussion
- No code outside schema definitions

---------------------------------------
QUALITY BAR
---------------------------------------

The result must be:
- implementation-ready
- internally consistent
- interview-defensible
- free of ambiguity

If anything is vague → fix it.

---------------------------------------
BEGIN
---------------------------------------