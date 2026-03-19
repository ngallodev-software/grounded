# LLM Analytics Chat MVP Plan

## 1. Recommended MVP definition

**Exact product**

A local, production-shaped **ASP.NET Core Web API** that answers natural-language analytics questions over a fixed **e-commerce orders dataset in Postgres**.

The user sends a chat question like “What were the top 5 product categories by revenue last quarter?”  
The system:

1. builds a bounded context package
2. calls an LLM to produce a **structured analytics plan**
3. validates that plan
4. compiles it into safe SQL
5. executes it against Postgres
6. optionally calls the LLM again to produce a grounded natural-language answer
7. logs the full trace
8. stores compact conversation state for follow-up questions

**Chosen domain**

Use **e-commerce order analytics** only.

Recommended business tables:

- `customers`
- `products`
- `orders`
- `order_items`

That is enough to support joins, filters, aggregation, ranking, time-series, and follow-up questions without turning the project into a BI platform.

**Supported question types**

Support only these five categories:

1. **Single-value aggregates**  
   “What was total revenue last month?”

2. **Grouped breakdowns**  
   “Revenue by category for Q1.”

3. **Top-N rankings**  
   “Top 10 products by units sold this year.”

4. **Time-series trends**  
   “Monthly revenue for the last 6 months.”

5. **Simple follow-ups using prior context**  
   “Now only for new customers.”  
   “What about just electronics?”

**Out of scope**

Explicitly cut all of this from MVP:

- general chat assistant behavior
- arbitrary SQL generation from raw schema dumps
- multi-agent orchestration
- vector DB / RAG over documents
- dashboards / chart rendering
- auth / roles / tenancy
- cloud deployment
- streaming responses
- write queries
- CSV upload
- arbitrary business-document definitions
- support for multiple business domains
- autonomous schema discovery
- natural-language explanations of database design
- “why did revenue drop?” causal inference beyond observed data

**Opinionated tradeoff**

Do **not** let the LLM emit raw executable SQL in MVP.  
Have it emit a **structured QueryPlan JSON**, then compile that to SQL in code.

That is the best choice for:

- shipping quickly
- interview credibility
- strict validation
- explaining guardrails
- keeping failures understandable

It still gives you real LLM integration, prompt work, orchestration, and context-window management, but without the uglier failure surface of free-form SQL.

---

## 2. Architecture

Use a **single ASP.NET Core Web API** plus **Postgres** plus **Docker Compose**. No agent framework.

### Runtime components

#### API layer

**`ChatController`**  
Receives user question + conversation ID, returns answer + metadata.

**`EvalController`**  
Runs benchmark suites locally. This can be internal/admin-only.

#### Application layer

**`AnalyticsChatOrchestrator`**  
Main workflow coordinator. This is the center of the project.

**`ContextBuilder`**  
Builds the bounded prompt context:
- relevant schema subset
- metric glossary
- supported operations
- compact conversation state
- few-shot examples

**`ConversationStateService`**  
Stores and retrieves compressed prior-turn state.  
Do this **deterministically in code**, not via a third summarization LLM call in MVP.

**`PromptRegistry`**  
Loads prompt files from disk, exposes prompt version IDs/checksums.

**`LlmGateway`**  
Thin wrapper over the provider SDK/API.  
Responsibilities:
- model selection
- timeout handling
- retries for transient failures
- correlation IDs
- token accounting
- raw request/response trace logging

Do **not** use Semantic Kernel, LangChain, or an agent framework for MVP.  
They add abstraction without helping your interview story.

**`PlannerResponseValidator`**  
Validates the LLM’s structured output against JSON schema and business rules.

**`QueryPlanCompiler`**  
Converts validated `QueryPlan` into parameterized SQL.

**`SqlSafetyGuard`**  
Final enforcement before execution:
- SELECT-only
- whitelist checks
- row limits
- timeout/session settings

**`AnalyticsQueryExecutor`**  
Executes SQL against Postgres with a read-only role.

**`AnswerSynthesizer`**  
Optional second LLM call that turns results into a concise, grounded answer.

**`TraceLogger`**  
Persists traces for prompts, LLM requests, outputs, validation failures, SQL, timings, token usage.

**`EvalRunner`**  
Executes benchmark cases and compares results across prompt versions.

#### Infrastructure layer

**Postgres**
- business schema for analytics data
- app schema for conversations, traces, evals

**Docker Compose**
- `api`
- `postgres`

That is enough.

### Recommended project structure

- `Api`
- `Application`
- `Domain`
- `Infrastructure`
- `prompts/`
- `context/`
- `eval/`

### Key design decision

The LLM should be used for:
- intent interpretation within a narrow domain
- mapping natural language to structured analytics plans
- answer phrasing

The application code should own:
- context trimming
- validation
- SQL generation
- execution safety
- storage
- eval orchestration

That boundary is the right one.

---

## 3. End-to-end request flow

Example question:  
**“What were the top 5 categories by revenue last quarter?”**

### Step 1: Request received

`ChatController` receives:
- `conversationId`
- `userMessage`

It creates a request trace ID immediately.

### Step 2: Load compact conversation state

`ConversationStateService` loads:
- prior active filters
- prior time range
- last metric
- last grouping
- last referenced entities

For a first-turn question, this is empty.

### Step 3: Build bounded context

`ContextBuilder` assembles only:

- supported metric glossary  
  Example: revenue = sum(order_items.quantity * order_items.unit_price)

- relevant schema subset  
  Likely `orders`, `order_items`, `products`

- allowed dimensions  
  Example: category, product_name, order_date, customer_region

- allowed filter fields

- few-shot examples for ranking and time filtering

- compact conversation state

It does **not** include:
- full DDL dump
- unrelated tables
- all prior messages
- large result history

### Step 4: Planner LLM call

`LlmGateway` calls the planner model with:
- system prompt
- bounded context
- user question

Expected output: structured `QueryPlan` JSON, not prose.

Example shape:

- `route`: `analytics`
- `metric`: `revenue`
- `dimensions`: `category`
- `timeRange`: `last_quarter`
- `filters`: `[]`
- `sort`: `revenue desc`
- `limit`: `5`

### Step 5: Validate planner output

`PlannerResponseValidator` checks:
- valid JSON
- required fields present
- only allowed metrics/dimensions used
- no unsupported operations
- question routed correctly
- limit within bounds

If JSON is malformed, allow **one repair attempt** using validation errors.  
If still invalid, fail safely.

### Step 6: Compile to SQL

`QueryPlanCompiler` converts the validated plan into parameterized SQL.

The LLM never chooses arbitrary join paths or raw expressions at execution time.

### Step 7: SQL safety enforcement

`SqlSafetyGuard` confirms:
- single SELECT
- approved tables only
- approved columns only
- no semicolons / multi-statements
- row limit enforced
- statement timeout set
- execution uses read-only DB credentials

### Step 8: Execute query

`AnalyticsQueryExecutor` runs the SQL against Postgres.

It stores:
- SQL text
- parameters
- execution time
- row count
- errors if any

### Step 9: Synthesize answer

`AnswerSynthesizer` gets:
- original question
- validated plan summary
- small result payload
- result metadata

It returns a concise grounded answer.

Example:  
“Last quarter, the top 5 categories by revenue were Electronics, Home, Accessories, Fitness, and Office Supplies. Electronics led by a clear margin.”

### Step 10: Store compact next-turn state

Update conversation state in code:
- last metric = revenue
- last dimension = category
- last time range = last_quarter
- last limit = 5
- last filters = none

This is what powers: “What about just for new customers?”

### Step 11: Persist trace

Store:
- prompt version
- context size
- LLM latency
- tokens in/out
- planner output
- validation result
- compiled SQL
- DB latency
- final answer

That traceability is part of the project’s value.

---

## 4. Prompt design

Use **two prompts only** in MVP.

### Prompt 1: `analytics_planner_v1`

**Purpose**  
Convert a user question into a structured analytics plan.

**Why this exists**  
This is where your real prompt engineering work lives.

**Inputs**
- user question
- compact conversation state
- allowed metrics glossary
- relevant schema subset
- allowed dimensions/filters
- business rules
- few-shot examples
- output JSON schema instructions

**Role**
“Cautious analytics planner for a fixed e-commerce schema. Only use allowed fields. If unsupported or ambiguous, say so explicitly.”

**Output contract**
Structured JSON.

Recommended shape:

- `route`: `analytics | unsupported | needs_clarification`
- `questionType`: `aggregate | grouped_breakdown | ranking | time_series | follow_up`
- `metric`
- `dimensions`
- `filters`
- `timeRange`
- `sort`
- `limit`
- `notes`
- `clarificationReason`
- `confidence`

**Prompt responsibilities**
Put these in prompt:
- domain role
- allowed task types
- metric definitions
- how to interpret follow-ups
- how to handle ambiguity
- strict output contract
- examples

**Keep out of prompt**
Do not put these in prompt:
- SQL safety logic
- whitelist enforcement
- row-limit enforcement
- timeout logic
- retries
- execution behavior

Those belong in code.

### Prompt 2: `answer_synthesizer_v1`

**Purpose**  
Turn query results into a user-facing answer without inventing facts.

**Inputs**
- original user question
- validated plan summary
- query result rows, truncated
- row count
- execution metadata if useful

**Role**
“Grounded analytics responder. Only describe what is present in the results. Do not speculate.”

**Output contract**
Plain text is fine here.

**Prompt responsibilities**
- answer directly
- stay grounded in results
- mention when results are empty
- avoid causal explanations not supported by data
- keep concise

### Few-shot example policy

Use **3 to 5 fixed examples** in planner prompt:
- one aggregate
- one grouped breakdown
- one ranking
- one time-series
- one follow-up

Do not build semantic example retrieval for MVP.  
That is classic overbuilding.

### Prompt versioning

Store prompts in repo as files:

- `prompts/analytics_planner/v1.md`
- `prompts/answer_synthesizer/v1.md`

Every run should log:
- prompt key
- version
- file checksum or git commit SHA
- model name

That lets you compare prompt changes honestly.

### Prompt evaluation rule

No prompt change should be accepted based on vibe.

Each prompt change must be tested against the benchmark suite and compared on:
- valid plan rate
- execution success rate
- answer correctness
- follow-up handling
- token usage

---

## 5. Context-window strategy

This is one of the strongest parts of the project if you do it right.

### Core rule

Do **not** dump the whole schema and whole conversation into every LLM call.

That is the beginner mistake.

### What goes into the planner call

Only include:

1. **User question**
2. **Compact conversation state**
3. **Relevant schema subset**
4. **Metric glossary**
5. **Allowed operations**
6. **Few-shot examples**
7. **Output contract**

### How schema is compressed

Create a hand-authored file like `context/schema_catalog.json` with:
- table names
- short business descriptions
- column names
- important relationships
- allowed dimensions
- allowed filters
- allowed metrics

Example idea:

- `orders`: order header, contains customer/date/status
- `order_items`: line items, contains quantity/unit_price
- `products`: product/category metadata
- `customers`: customer attributes

Do not send:
- indexes
- constraints not relevant to reasoning
- full DDL
- every column if not useful

### Relevant-schema selection

Use deterministic selection in code.

Given the question and conversation state, include only tables and fields likely needed.

Example:
- “revenue by category” → `orders`, `order_items`, `products`
- “new customers last month” → `orders`, `customers`

This should be rule-based for MVP.

### Conversation history strategy

Do not keep full raw chat history in the prompt.

Instead keep a compact structured state, such as:
- last metric
- last dimension
- last filters
- last time range
- last top-N limit
- last referenced entities

Also keep the **last 1 raw user turn only** if needed for pronoun resolution.

That is enough for:
- “what about by month?”
- “now only for electronics”
- “same but last year”

### Token budget policy

Use explicit budgets.

Recommended planner budget:
- max input: about **4,500 tokens**
- reserve output: **800–1,000 tokens**

Recommended synthesis budget:
- max input: about **2,000 tokens**
- reserve output: **300–500 tokens**

### Budget allocation order

For planner call, prioritize in this order:

1. user question
2. output contract
3. metric glossary
4. relevant schema subset
5. conversation state
6. few-shot examples

If trimming is needed, cut in this order:
1. older raw history
2. verbose schema descriptions
3. number of few-shot examples
4. nonessential notes

### Output reservation

Always reserve space for the model’s response.  
Do not pack input context to the edge.

That is another common real-world mistake.

### What is explicitly excluded

Never include:
- full database schema
- unrelated tables
- prior SQL text from many turns
- full result sets
- verbose logs
- hidden implementation notes

### Why this is interview-strong

You will be able to say:  
“I treated context as a scarce resource and built deterministic context packaging instead of blindly stuffing everything into the prompt.”

That is exactly the kind of sentence interviewers want to hear.

---

## 6. Guardrails and validation rules

Be strict.

### Planner output validation

The planner output must pass:

1. **JSON parse**
2. **JSON schema validation**
3. **business rule validation**

Reject if:
- required fields missing
- invalid enum values
- unsupported metric/dimension used
- unknown filter field
- unsupported question type
- ambiguous follow-up with no resolvable prior context

Allow **one repair attempt only** for structural issues.

### Route validation

Only three routes allowed:
- `analytics`
- `unsupported`
- `needs_clarification`

If unsupported, do not try to “be helpful” with guessed SQL.

### QueryPlan restrictions

The plan may only reference:
- whitelisted metrics
- whitelisted dimensions
- whitelisted filters
- approved time-grain values
- approved sort directions

No free-form expressions from the LLM.

### SQL compilation rule

Only application code generates executable SQL.

Requirements:
- parameterized values only
- approved join templates only
- approved aggregations only
- enforced row limits
- enforced grouping rules

### SQL safety checks

Even compiled SQL must be checked before execution:

- SELECT only
- one statement only
- no comments
- no semicolons
- no DDL/DML keywords
- only whitelisted tables/columns
- hard row limit
- hard timeout
- read-only connection

### DB execution controls

Use:
- read-only Postgres user
- `statement_timeout`
- result row cap
- command timeout in app

Recommended defaults:
- DB timeout: **3 seconds**
- internal result cap: **200 rows**
- rows passed to synthesis: **20 max**

### LLM cost and reliability controls

Planner call:
- timeout around **20 seconds**
- retry only on transient provider errors
- no retry on unsafe/invalid semantics

Synthesis call:
- timeout around **10 seconds**
- same retry policy

Global request policy:
- max LLM calls per user request: **3**
  - planner
  - optional repair
  - synthesizer

### Failure handling

If planner output is invalid after repair:
- return safe failure
- log validation details
- do not execute anything

If query returns empty set:
- return grounded empty-result response
- do not treat as system failure

If question is unsupported:
- return deterministic unsupported message
- optionally state supported types

If synthesis fails:
- fall back to deterministic templated answer from raw results

That fallback matters.  
Do not make the whole request depend on answer phrasing.

---

## 7. Data model / storage

Use Postgres for runtime data.  
Use repo files for prompts and benchmark definitions.

### Business data schema

Recommended analytics tables:
- `customers`
- `products`
- `orders`
- `order_items`

Seed enough realistic data to make trend/ranking questions meaningful.

### App runtime tables

#### `conversations`
Fields:
- `id`
- `created_at`
- `updated_at`
- `latest_state_json`

#### `conversation_turns`
Fields:
- `id`
- `conversation_id`
- `role` (`user` / `assistant`)
- `message_text`
- `created_at`
- `trace_id`

#### `llm_traces`
Fields:
- `trace_id`
- `conversation_id`
- `prompt_key`
- `prompt_version`
- `model_name`
- `request_json`
- `response_json`
- `input_tokens`
- `output_tokens`
- `latency_ms`
- `status`
- `created_at`

#### `query_runs`
Fields:
- `id`
- `trace_id`
- `conversation_id`
- `validated_plan_json`
- `compiled_sql`
- `sql_params_json`
- `row_count`
- `latency_ms`
- `execution_status`
- `error_text`
- `created_at`

#### `app_events`
Optional but useful for auditing:
- `id`
- `trace_id`
- `event_type`
- `payload_json`
- `created_at`

### Prompt storage

Store prompts in repo, not DB:

- `prompts/analytics_planner/v1.md`
- `prompts/answer_synthesizer/v1.md`

This is better because:
- easy diffing
- real version control
- simple eval comparisons

### Context files

Store these in repo:

- `context/schema_catalog.json`
- `context/metric_glossary.json`
- `context/few_shot_examples.json`

### Eval files

Store benchmark cases in repo:

- `eval/benchmark_cases.jsonl`

Each case should contain:
- case ID
- category
- user question
- optional prior conversation state
- expected route
- gold query spec or gold SQL
- expected result snapshot
- grading method
- notes

### Eval result tables

#### `eval_runs`
Fields:
- `id`
- `started_at`
- `completed_at`
- `planner_prompt_version`
- `synth_prompt_version`
- `model_name`
- `notes`

#### `eval_case_results`
Fields:
- `eval_run_id`
- `case_id`
- `route_correct`
- `plan_valid`
- `execution_success`
- `result_correct`
- `answer_review_status`
- `latency_ms`
- `input_tokens`
- `output_tokens`
- `notes`

---

## 8. Evaluation plan

Keep this small, real, and repeatable.

### Benchmark suite size

Start with **30 benchmark questions**.

That is enough to be meaningful without becoming a research project.

### Benchmark categories

Use 6 buckets with 5 questions each:

1. aggregate
2. grouped breakdown
3. ranking
4. time-series
5. follow-up context
6. unsupported / ambiguity handling

### Example benchmark questions

Examples you can use:

1. “What was total revenue last month?”
2. “How many orders were placed in January 2025?”
3. “Top 5 products by revenue in Q4 2025.”
4. “Monthly revenue for the last 6 months.”
5. “Revenue by category for last quarter.”
6. “Which customer segment had the highest average order value this year?”
7. “What were the top 10 customers by spend in 2025?”
8. “How many first-time customers did we have last month?”
9. “Show order count by week for the last 8 weeks.”
10. “What about just electronics?”  
    with prior state: revenue by category last quarter
11. “Now do that by month.”
12. “Same question, but only for new customers.”
13. “Why did sales drop?”  
    should be unsupported or carefully constrained
14. “Delete the cancelled orders.”  
    should be unsupported
15. “Which warehouse was slowest?”  
    unsupported if warehouse is not in schema

### Automated grading

Automate what is objective.

Track these metrics:

- **route accuracy**
- **plan validity rate**
- **query execution success rate**
- **result correctness**
- **repair rate**
- **unsupported precision**
- **latency**
- **token usage**

For result correctness:
- compare scalar values exactly or with numeric tolerance
- compare ordered top-N results by expected IDs/names and order
- compare grouped/time-series results against gold outputs

Do **not** try to auto-grade prose quality deeply in MVP.

### Manual review

Use manual review for:
- answer wording
- grounding quality
- whether follow-up interpretation was reasonable
- whether caveats were appropriate

A small manual review pass is enough:
- 10 hardest benchmark cases after prompt changes

### Regression policy

Every prompt change should trigger the benchmark suite.

A prompt version should only be promoted if:
- plan validity does not regress
- execution success does not regress
- correctness does not regress
- token usage does not grow materially without benefit

### Strong eval story

This is the story you want:  
“I versioned prompts, ran them against a fixed benchmark set, and compared correctness, validity, latency, and token cost before accepting changes.”

That is real and interview-strong.

---

## 9. Implementation phases

### Phase 1: Freeze domain and dataset

Build:
- fixed business schema
- seeded Postgres data
- metric glossary
- schema catalog
- initial benchmark questions

**Exit criteria**
- schema is stable
- seed data produces meaningful analytics answers
- 20–30 benchmark cases exist before LLM work begins

### Phase 2: Build deterministic analytics core

Build:
- API scaffold
- Postgres integration
- `QueryPlan` model
- `QueryPlanCompiler`
- `SqlSafetyGuard`
- query execution path from hand-authored plans

No LLM yet.

**Exit criteria**
- you can submit a manual `QueryPlan` JSON and get safe SQL + results
- row limits and timeouts work
- read-only DB enforcement works

### Phase 3: Add planner LLM integration

Build:
- `LlmGateway`
- prompt loading/versioning
- planner prompt v1
- planner output schema validation
- one repair attempt flow
- trace logging

**Exit criteria**
- natural-language questions can produce validated plans for simple one-turn cases
- invalid output is rejected safely
- full traces are stored

### Phase 4: Add answer synthesis

Build:
- synthesis prompt v1
- synthesis LLM call
- deterministic fallback if synthesis fails

**Exit criteria**
- successful query returns a concise natural-language answer
- synthesis failure does not break the request path

### Phase 5: Add compact conversation memory

Build:
- `ConversationStateService`
- prior-turn state extraction in code
- follow-up resolution for simple refinements

**Exit criteria**
- follow-ups like “what about just electronics?” work
- full raw history is not needed in prompt

### Phase 6: Add eval harness and regression loop

Build:
- benchmark runner
- result comparison
- eval result storage
- before/after prompt comparison

**Exit criteria**
- you can run benchmark suite locally
- prompt changes can be compared with real metrics

### Phase 7: Polish for demo and interview use

Build:
- good request/trace logging
- sample Postman collection or Swagger examples
- short demo script
- README with architecture and known limitations

**Exit criteria**
- a stranger can run it locally
- you can demo 5–8 benchmark questions cleanly
- you can explain tradeoffs without hand-waving

---

## 10. Anti-scope-creep rules

Use these as hard rejection rules.

### Rule 1
If a feature does not directly improve:
- LLM integration
- prompt quality
- context management
- orchestration
- validation
- evals

cut it.

### Rule 2
One domain only.  
No second schema, no second business domain.

### Rule 3
No more than **two core prompts** in MVP.

### Rule 4
No more than **two primary LLM calls** per successful request.

### Rule 5
No vector search unless you hit a specific failure that cannot be solved with curated schema context.

You probably will not need it.

### Rule 6
No multi-agent design.

This project does not need planner agents, SQL agents, critic agents, or debate agents.

### Rule 7
No raw-SQL-from-LLM execution in MVP.

Structured plan → code-compiled SQL is the right scope.

### Rule 8
No frontend before the API, traces, and eval harness are working.

Swagger is enough.

### Rule 9
No cloud deployment until local evals and prompt versioning are solid.

### Rule 10
No “smart” semantic memory system.

Use compact structured state only.

### Rule 11
No prompt tweaks without benchmark comparison.

### Rule 12
No extra features for impressiveness alone.

Tempting but unnecessary additions to reject:
- chart generation
- auto-insights
- alerting
- anomaly detection
- embeddings
- fine-tuning
- multi-model routing
- auth
- role-based access control
- streaming
- natural-language dashboard builder

Those are how teams lose the thread.

---

## 11. Interview payoff

After shipping this, you can truthfully say you have concrete experience with the recruiter requirement.

### What you will be able to claim

You will be able to say things like:

- “I built an ASP.NET Core analytics chat API that integrated an LLM into a real request pipeline over Postgres.”
- “I designed prompt-driven structured outputs for analytics planning rather than letting the model return free-form executable SQL.”
- “I versioned prompts in source control and evaluated prompt changes against a benchmark suite before accepting them.”
- “I treated context windows as a constrained resource and built deterministic context packaging for schema, glossary, and conversation state.”
- “I implemented output guardrails with JSON schema validation, whitelist enforcement, SQL compilation, read-only DB access, row limits, and timeouts.”
- “I logged LLM traces, token usage, latency, validation failures, and query execution metadata for debugging and regression testing.”
- “I built follow-up question handling using compact conversation state instead of replaying full history.”

### Concrete tradeoffs you will be able to discuss

- why you rejected multi-agent architecture
- why you rejected vector search for MVP
- why structured plan output was safer than raw SQL generation
- why context should be curated instead of dumping schema/history
- what belongs in prompt versus application code
- why answer synthesis should be optional/fallback-safe
- why prompt changes need evals, not intuition

### Real failure stories you are likely to generate

These are good interview material once you hit them:

- planner picked the wrong grain
- planner invented unsupported fields before whitelist validation
- follow-up turns carried stale filters
- too much schema context hurt accuracy
- too little schema context caused wrong joins
- synthesis phrased things too confidently for sparse results
- prompt changes improved one category but regressed another

That is excellent interview material because it is specific and believable.

### Why this project is a strong artifact

It is not a toy wrapper.  
It shows:
- real API integration
- prompt engineering with versioning
- context-window discipline
- orchestration
- validation and safety
- evals and regression thinking

That is exactly the right surface area.

---

## 12. First build order

Do these in this order.

### 1. Freeze the domain
Write down the exact supported domain, question types, and out-of-scope list.

### 2. Design the Postgres schema
Create:
- `customers`
- `products`
- `orders`
- `order_items`

Keep it stable.

### 3. Seed realistic data
Generate enough data to support:
- monthly trends
- category rankings
- customer segments
- follow-up filters

### 4. Write benchmark questions before LLM integration
Create 25–30 benchmark cases now.  
This prevents vague prompt tinkering later.

### 5. Write the metric glossary
Define exactly what:
- revenue
- average order value
- order count
- first-time customer
mean in the system.

### 6. Create the schema catalog
Hand-author the compressed schema/context files the prompt will use.

### 7. Scaffold the ASP.NET Core solution
Set up:
- Web API
- Postgres access
- Docker Compose
- migrations / seed path

### 8. Build the deterministic query pipeline first
Implement:
- `QueryPlan` DTO
- `QueryPlanCompiler`
- `SqlSafetyGuard`
- `AnalyticsQueryExecutor`

Test it with manually written plans.

### 9. Add trace storage
Create tables and logging for:
- conversations
- turns
- llm traces
- query runs

### 10. Build the LLM gateway
Add:
- provider integration
- timeouts
- retries
- model config
- token/latency logging

### 11. Write planner prompt v1
Start simple and strict.  
Do not overstuff it.

### 12. Wire planner → validation → compiler → execution
Get one-turn analytics questions working end to end.

### 13. Add synthesis prompt
Keep deterministic fallback in place.

### 14. Add compact conversation state
Support only simple follow-ups.

### 15. Build the eval runner
Run the benchmark set against the live pipeline and store results.

### 16. Only then iterate on prompts
From this point on, prompt changes should be benchmark-driven.

The single most important build-order rule is this:

**Do not start with chat UX or clever prompting. Start with the schema, benchmark cases, and deterministic execution core.**
