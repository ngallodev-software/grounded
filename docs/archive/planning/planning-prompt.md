I want you to act as a senior AI application architect and pragmatic staff-level backend engineer.

Your job is to help me plan a tightly scoped project whose purpose is to give me real, concrete, interview-ready experience with:
- LLM API integration
- prompt engineering
- context-window management
- orchestration
- output validation / guardrails
- evaluation and regression testing

## Project intent

This is not a generic startup brainstorm. It is a focused, production-shaped portfolio project meant to let me truthfully discuss this requirement in interviews:

“Real-world LLM integration experience: you’ve wired up LLM API calls in production, tackled prompt engineering challenges, and managed context windows.”

The project should be small enough to ship, but concrete enough that I can speak about design tradeoffs, failures, fixes, and measurable outcomes.

## Proposed project concept

A .NET-based natural-language analytics chat service over structured data.

The rough flow:
1. user asks a natural-language question
2. system determines intent / route
3. system builds a bounded context package
4. LLM helps plan or generate SQL
5. output is validated
6. query executes against Postgres
7. answer is synthesized and returned
8. metadata is logged
9. conversation history is summarized or compressed for future turns

## What I need from you

I want a planning output, not implementation code yet.

Give me a concrete, opinionated project plan with:
1. final MVP scope
2. architecture
3. component breakdown
4. development phases
5. exact guardrails
6. context-window strategy
7. prompt strategy
8. evaluation strategy
9. sample benchmark questions
10. likely failure modes
11. what to defer until after MVP
12. specific talking points I can later use in interviews

## Hard guardrails

You must follow these rules:

### Scope control
- Keep the MVP narrow.
- Pick one domain only.
- Do not expand into a general AI platform.
- Do not introduce multi-agent architecture unless there is an overwhelming reason.
- Do not recommend broad RAG/vector-search infrastructure unless it is truly necessary for the MVP.
- Do not over-design for scale, tenancy, auth, or cloud deployment.
- Do not add features just because they are impressive.
- Every feature must directly support one of these themes:
  - LLM integration
  - prompt engineering
  - context management
  - orchestration
  - validation / guardrails
  - evals

### Technology bias
- Favor ASP.NET Core Web API
- Favor Postgres
- Favor Docker Compose for local setup
- Favor simple, explainable architecture over novelty
- Favor deterministic service flows over “AI agent” complexity

### Truthfulness / realism
- Assume this is a local but production-shaped system, not an actual large-scale production deployment
- Do not invent fake metrics
- Do not suggest resume claims I could not back up after completing the MVP
- Keep the project achievable by one experienced backend engineer

### Planning style
- Be opinionated
- Make tradeoffs explicit
- Prefer concrete decisions over vague option lists
- Identify what to cut
- Call out where teams commonly overbuild
- Optimize for “strong interview artifact” rather than “maximum feature count”

## Required design themes

Your plan must explicitly address all of these:

### 1. LLM API integration
I need real application-layer integration, not a toy wrapper.
Cover:
- request/response flow
- retries/timeouts
- model configuration
- structured outputs where appropriate
- logging and traceability

### 2. Prompt engineering
I need to be able to talk concretely about prompt design.
Cover:
- prompt roles
- system prompt structure
- few-shot examples
- prompt versioning
- how prompt changes are evaluated
- what belongs in prompt vs code

### 3. Context-window management
This is a core requirement.
Cover:
- what context goes into each LLM call
- how schema information is compressed
- how conversation history is handled
- how token budgets are enforced
- how irrelevant context is excluded
- how much room to reserve for outputs

### 4. Output guardrails
Anything the LLM returns that affects execution must be validated.
Cover:
- JSON schema validation
- SQL safety checks
- schema/table/column whitelisting
- row/result limits
- timeout/cost controls
- failure handling when output is invalid

### 5. Evaluation
I need a benchmark/evals story.
Cover:
- benchmark dataset of questions
- expected outcomes or grading criteria
- regression testing across prompt versions
- metrics to track
- manual vs automated review

## What I want the output to look like

Please structure your answer exactly like this:

# 1. Recommended MVP definition
State the exact product, domain, supported question types, and what is explicitly out of scope.

# 2. Architecture
Describe the full architecture in concrete terms.
Name the services/classes/components I will likely need.

# 3. End-to-end request flow
Walk through one request from user input to final answer.

# 4. Prompt design
List each prompt needed, what it does, what inputs it gets, and what output contract it should follow.

# 5. Context-window strategy
Explain exactly how to manage context and token budgets.

# 6. Guardrails and validation rules
Be explicit and strict.

# 7. Data model / storage
Suggest tables/files/structures needed for prompts, chat history, traces, eval runs, and benchmark questions.

# 8. Evaluation plan
Give me a small but real eval strategy I can implement locally.

# 9. Implementation phases
Break the work into sensible phases with clear exit criteria.

# 10. Anti-scope-creep rules
List the rules I should use to reject tempting but unnecessary additions.

# 11. Interview payoff
Tell me exactly what concrete experience this project would let me claim and discuss.

# 12. First build order
Give me the very first sequence of tasks I should do after planning is complete.

## Additional instruction

Whenever there are multiple valid options, choose the one that is:
- easiest to ship
- easiest to explain
- strongest for interview credibility
- most directly aligned with the recruiter requirement

Do not give me a giant brainstorm. Give me a disciplined project plan.