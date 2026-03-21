# Brief planning document

## Project goal

Build a small, production-shaped **LLM analytics chat service** in .NET that answers natural-language questions over structured data. The project exists to create credible, concrete experience in:

- real-world LLM API integration
- prompt engineering
- context-window management
- orchestration
- output validation / guardrails
- evaluation and regression testing

This is primarily a **career-targeted proof project**, not a broad platform. It should be narrowly scoped, technically clean, demoable, and easy to explain in interviews.

## Why this project

A recruiter-highlighted requirement is:

> “Real-world LLM integration experience: you’ve wired up LLM API calls in production, tackled prompt engineering challenges, and managed context windows.”

This project must produce direct experience and artifacts that support that claim honestly.

## Product concept

A user asks a natural-language question about a structured dataset. The system decides how to answer, selects the minimum necessary context, uses an LLM to help plan or generate SQL, validates output, executes against a database, and returns a grounded answer.

Example domain options:
- personal finance transactions
- job application tracker analytics
- dev activity metrics
- ecommerce orders

Pick **one** domain only for MVP.

## MVP objectives

The MVP must demonstrate:

- LLM API wiring in an ASP.NET Core service
- prompt versioning
- bounded context construction
- conversation summarization or context compression
- schema-aware SQL generation or query planning
- output guardrails before execution
- basic evaluation harness
- structured logging of prompt/model/result metadata

## Non-goals

Do not turn this into:
- a generic agent platform
- multi-agent orchestration
- autonomous tool ecosystem
- vector search platform unless clearly required
- enterprise auth / multi-tenant permissions
- polished frontend-heavy product
- broad RAG framework
- benchmark science project
- production deployment infra beyond what is needed for a credible local demo

## Suggested MVP architecture

Components:
- ASP.NET Core Web API
- Postgres database
- one LLM provider integration
- prompt templates stored as files or DB records with versioning
- orchestration service for chat flow
- validation layer for LLM outputs
- evaluation harness with fixed benchmark questions
- Docker Compose for local setup

Basic request flow:
1. receive question
2. classify intent / determine route
3. build bounded context
4. call LLM for planning or SQL generation
5. validate output
6. execute safe query
7. synthesize grounded answer
8. log metadata and result
9. optionally summarize conversation for future turns

## MVP feature list

### Required
- chat/question endpoint
- seeded structured dataset
- schema summary available to prompt builder
- prompt versioning
- token/context budgeting
- SQL safety validation
- result size limits
- structured logs
- benchmark test set
- evaluation report for prompt/model changes

### Nice to have
- Swagger demo flow
- simple minimal web UI
- prompt diff notes
- per-run saved traces
- retry / timeout handling

## Technical rules

- .NET first
- keep architecture simple and explainable
- optimize for interview credibility, not novelty
- prefer deterministic flows over agentic complexity
- all LLM outputs that affect execution must be validated
- every prompt should have a clear purpose and owner
- every context addition must justify its token cost
- every new feature must support the recruiter requirement or be cut

## Suggested implementation phases

### Phase 1: narrow spec
- choose one dataset/domain
- define exact user questions supported
- define output contract
- decide whether MVP uses direct SQL generation or plan-then-SQL

### Phase 2: core backend
- create API
- load seed data
- expose schema metadata
- implement LLM client
- add prompt storage/versioning
- implement orchestration pipeline

### Phase 3: safety and context
- add schema whitelist
- enforce SELECT-only policy
- cap result size/time
- add token budgeting
- add chat summarization / trimmed history

### Phase 4: evaluation
- create 25–50 benchmark questions
- define expected result types or expected SQL properties
- measure valid SQL rate, correctness, latency, token usage, failure types

### Phase 5: demo readiness
- Docker Compose
- setup/readme
- example queries
- architecture notes
- interview story bullets

## Deliverables

By the end, the project should give you:
- a working local demo
- a credible architecture diagram or explanation
- concrete examples of prompt engineering decisions
- a real story about context-window management
- measurable evaluation results
- resume bullets grounded in actual implementation

## Success criteria

The project is successful if you can truthfully say:

- I built an ASP.NET Core service that integrated an LLM into a real application flow.
- I versioned prompts and evaluated changes against a benchmark set.
- I managed context windows through schema selection, summarization, and token budgeting.
- I added guardrails so model outputs were validated before execution.
- I observed failure modes and improved reliability with concrete changes.
