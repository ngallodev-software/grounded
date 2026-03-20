# Project Evaluation: Grounded

Scoring rubric per category (0-3):
- 0 = no evidence
- 1 = weak or indirect evidence
- 2 = meaningful evidence
- 3 = strong and clearly relevant evidence

Evidence labeling:
- Direct evidence: explicitly present in code/docs.
- Indirect evidence: strongly suggested by structure/naming, but not clearly demonstrated.
- Missing evidence: expected for the role, but not present.

---

## 1. Project Summary
- What it appears to do: `Grounded` is a narrow, production-shaped ASP.NET Core analytics API that turns natural-language questions into a structured `QueryPlan`, compiles that plan into parameterized SQL, executes it against Postgres, and synthesizes a grounded answer.
- Main components:
  - API + DI wiring: `LlmIntegrationDemo.Api/Program.cs:3`
  - HTTP endpoints: `LlmIntegrationDemo.Api/Controllers/AnalyticsController.cs:7`
  - Core orchestration: `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs:6`
  - Query validation/compiler/safety: `LlmIntegrationDemo.Api/Services/QueryPlanValidator.cs:6`, `LlmIntegrationDemo.Api/Services/QueryPlanCompiler.cs:6`, `LlmIntegrationDemo.Api/Services/SqlSafetyGuard.cs:6`
  - Execution: `LlmIntegrationDemo.Api/Services/AnalyticsQueryExecutor.cs:27`
  - Synthesis + prompt loading: `LlmIntegrationDemo.Api/Services/AnswerSynthesizer.cs:11`, `LlmIntegrationDemo.Api/Services/PromptStore.cs:10`
  - Eval/regression loop: `LlmIntegrationDemo.Api/Services/EvalRunner.cs:9`, `LlmIntegrationDemo.Api/Services/RegressionComparer.cs:11`
- Problem solved: demonstrates the safer "LLM emits structured plan, code owns SQL and execution" pattern for analytics QA instead of free-form SQL generation.

## 2. Category Scores Table
| Category | Score | Confidence | Justification |
|---|---:|---|---|
| 1. LLM Integration | 2 | High | Real LLM boundaries are modeled in code, but both planner and synthesizer currently use deterministic local gateways rather than a provider SDK. (`LlmIntegrationDemo.Api/Services/LlmGateway.cs:9`) |
| 2. Prompt Engineering | 2 | Medium | Prompt files are stored in-repo, checksumed, and loaded via a prompt store; the shipped synthesizer prompt is concrete and constrained. (`prompts/answer-synthesizer/v1.md:1`, `LlmIntegrationDemo.Api/Services/PromptStore.cs:14`) |
| 3. Context Management | 2 | Medium | The synthesis request is explicitly bounded to question, `QueryPlan`, rows, columns, and prompt checksum; ADR/docs also show deterministic packaging intent. (`LlmIntegrationDemo.Api/Services/AnswerSynthesizer.cs:36`, `docs/adrs/adr-003-deterministic-content-packaging.md:23`) |
| 4. Orchestration / Logical Layer | 3 | High | There is a clean pipeline from question -> plan -> validation -> SQL -> safety -> execution -> synthesis -> trace/eval. (`LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs:34`) |
| 5. Evaluation / Reliability | 3 | High | Eval runner, structural scoring, answer-grounding checks, regression persistence, and surfaced synthesis failures are all implemented. (`LlmIntegrationDemo.Api/Services/EvalRunner.cs:31`, `LlmIntegrationDemo.Api/Services/ScoringService.cs:8`) |
| 6. Backend / API Engineering | 3 | High | ASP.NET Core controller endpoints, DI composition, scoped execution services, and test-hosted API integration coverage are present. (`LlmIntegrationDemo.Api/Program.cs:3`, `LlmIntegrationDemo.Api/Controllers/AnalyticsController.cs:20`) |
| 7. SQL / Structured Data Work | 3 | High | Strong evidence of structured SQL work: whitelist-driven plan validation, parameterized SQL compilation, read-only Postgres execution, and safety gating. (`LlmIntegrationDemo.Api/Services/QueryPlanCompiler.cs:15`, `LlmIntegrationDemo.Api/Services/AnalyticsQueryExecutor.cs:40`) |
| 8. Performance / Cost / Latency | 2 | Medium | The repo tracks token counts and request timestamps on the LLM side and enforces query row caps + 15s DB timeouts, but deeper optimization is absent. (`LlmIntegrationDemo.Api/Services/LlmGateway.cs:41`, `LlmIntegrationDemo.Api/Services/AnalyticsQueryExecutor.cs:46`) |
| 9. Documentation / Architecture | 3 | High | The project is unusually well-documented for a demo: spec, phased artifacts, remediation notes, and ADRs all align to the implementation direction. (`docs/project-spec.md:1`, `docs/phases/phase-4-spec.md:174`, `docs/adrs/adr-004-query-plan-pipeline-structure.md:1`) |
| 10. Product Thinking / Ownership | 3 | High | Scope is sharply constrained, non-goals are explicit, and the Phase 4 remediation notes show iterative self-review rather than vague "AI demo" hand-waving. (`docs/project-spec.md:54`, `docs/phases/phase-4-spec.md:174`) |

## 3. Detailed Evidence by Category

### 1) LLM Integration
- Evidence found:
  - `AnalyticsQueryPlanService.ExecuteFromQuestionAsync` explicitly creates a planning boundary from raw user question to `QueryPlan`. Direct evidence: `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs:34`.
  - `AnswerSynthesizer` loads a prompt, sends a request through `ILlmGateway`, validates structured output, and records model/token metadata. Direct evidence: `LlmIntegrationDemo.Api/Services/AnswerSynthesizer.cs:29`.
  - The LLM gateways are deterministic local stand-ins, not provider SDK integrations. Direct evidence: `LlmIntegrationDemo.Api/Services/LlmGateway.cs:22`, `LlmIntegrationDemo.Api/Services/LlmGateway.cs:63`.
- Why it matters: this shows solid model-boundary design, but not yet "I shipped OpenAI/Anthropic in production" evidence.
- Missing pieces:
  - No real provider transport, auth/config, retry, or timeout behavior at the SDK boundary.
  - The planner gateway currently ignores the actual question semantics and returns one static plan.

### 2) Prompt Engineering
- Evidence found:
  - Prompt files are loaded from disk, normalized, and checksumed for version tracking. Direct evidence: `LlmIntegrationDemo.Api/Services/PromptStore.cs:14`.
  - The synthesizer prompt has concrete anti-hallucination rules, JSON output constraints, and behavior by query type. Direct evidence: `prompts/answer-synthesizer/v1.md:1`.
  - Eval output stores the synthesizer prompt checksum per run. Direct evidence: `LlmIntegrationDemo.Api/Services/EvalRunner.cs:87`.
- Missing pieces:
  - The runtime path for a real planner prompt is not implemented in the current code.
  - No prompt-diff tooling or richer prompt experiment matrix beyond checksum capture.

### 3) Context Management
- Evidence found:
  - The synthesis request is intentionally bounded to `userQuestion`, `QueryPlan`, result rows, derived columns, execution metadata, and prompt checksum. Direct evidence: `LlmIntegrationDemo.Api/Services/AnswerSynthesizer.cs:36`.
  - The architecture docs emphasize deterministic, bounded content packaging instead of replaying full schema/history. Direct evidence: `docs/adrs/adr-003-deterministic-content-packaging.md:23`.
- Why it matters: this is the right mental model for keeping analytics prompts explainable and small.
- Missing pieces:
  - Conversation state storage/compression is specified heavily in docs but not implemented in the shipped code.
  - The planner-side bounded context packager from the spec is not present in runtime code.

### 4) Orchestration / Logical Layer
- Evidence found:
  - The main service does exactly what you would want in an interview story: plan, validate, resolve time range, compile SQL, run safety checks, execute, synthesize, and emit a trace. Direct evidence: `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs:40`.
  - ADR-004 documents the linear pipeline and responsibility split cleanly. Direct evidence: `docs/adrs/adr-004-query-plan-pipeline-structure.md:11`.
- Why it matters: this is the strongest part of the repo. It demonstrates disciplined separation between model reasoning and deterministic execution.

### 5) Evaluation / Reliability
- Evidence found:
  - `EvalRunner` executes benchmark questions through the full question-first path and captures planned query, SQL, metadata, answer, scoring, and notes. Direct evidence: `LlmIntegrationDemo.Api/Services/EvalRunner.cs:31`.
  - `ScoringService` uses structural scoring rather than brittle exact-string matching. Direct evidence: `LlmIntegrationDemo.Api/Services/ScoringService.cs:10`.
  - Regression history is persisted under `eval/` and compared run-over-run. Direct evidence: `LlmIntegrationDemo.Api/Services/RegressionComparer.cs:20`.
  - Synthesis failures are surfaced back into the response trace instead of silently disappearing. Direct evidence: `LlmIntegrationDemo.Api/Services/AnalyticsQueryPlanService.cs:66`.
- Missing pieces:
  - Benchmark coverage is still very small; the checked-in fixture file currently contains only two cases. Direct evidence: `eval/benchmark_cases.jsonl:1`.

### 6) Backend / API Engineering
- Evidence found:
  - Clean ASP.NET Core composition root with singleton/scoped lifetimes and interface-based seams for time and DB execution. Direct evidence: `LlmIntegrationDemo.Api/Program.cs:3`.
  - HTTP API exposes both execution and eval endpoints. Direct evidence: `LlmIntegrationDemo.Api/Controllers/AnalyticsController.cs:20`.
  - Integration tests use `WebApplicationFactory` and dependency replacement rather than only unit-testing pure classes. Direct evidence: `LlmIntegrationDemo.Tests/Phase4IntegrationTests.cs:13`.
- Why it matters: among portfolio demos, this is credible backend engineering rather than a notebook or script pile.

### 7) SQL / Structured Data Work
- Evidence found:
  - Validation constrains supported metrics, dimensions, operators, time presets, and filter values. Direct evidence: `LlmIntegrationDemo.Api/Services/QueryPlanValidator.cs:8`.
  - The compiler emits parameterized SQL with fixed fragments, join activation, time predicates, grouping, ordering, and row caps. Direct evidence: `LlmIntegrationDemo.Api/Services/QueryPlanCompiler.cs:23`.
  - SQL safety checks enforce `SELECT`/`WITH` only, no dangerous keywords, one statement, and legal row caps. Direct evidence: `LlmIntegrationDemo.Api/Services/SqlSafetyGuard.cs:12`.
  - Execution runs against Npgsql with read-only transaction semantics and statement timeout. Direct evidence: `LlmIntegrationDemo.Api/Services/AnalyticsQueryExecutor.cs:40`.

### 8) Performance / Cost / Latency
- Evidence found:
  - The deterministic LLM gateway records synthetic token-in/token-out counts and response timestamps. Direct evidence: `LlmIntegrationDemo.Api/Services/LlmGateway.cs:41`.
  - The query executor enforces a 15-second timeout and guards against results exceeding the compiled row cap. Direct evidence: `LlmIntegrationDemo.Api/Services/AnalyticsQueryExecutor.cs:46`.
- Missing pieces:
  - No caching, batching, provider-side timeout/retry policy, or measured latency budgets beyond the DB side.
  - Token accounting is simulated, not provider-returned.

### 9) Documentation / Architecture
- Evidence found:
  - The top-level spec clearly defines scope, architecture, prompt strategy, data model, and evaluation philosophy. Direct evidence: `docs/project-spec.md:1`.
  - Phase docs include a post-implementation review with concrete correction items, which is stronger than generic roadmap prose. Direct evidence: `docs/phases/phase-4-spec.md:174`.
  - ADR-004 explains the query pipeline split and tradeoffs. Direct evidence: `docs/adrs/adr-004-query-plan-pipeline-structure.md:11`.

### 10) Product Thinking / Ownership
- Evidence found:
  - MVP boundaries are explicit and defensible: fixed e-commerce domain, QueryPlan over raw SQL, no agent framework, no vector search, no general chat. Direct evidence: `docs/project-spec.md:54`, `docs/project-spec.md:74`.
  - The repo shows self-critique and remediation planning, not just aspiration. Direct evidence: `docs/phases/phase-4-spec.md:178`.
- Why it matters: this reads like someone deliberately shaping an interview-worthy system, not just assembling tools.

## 4. Strongest Signals For This Job
- Strong .NET backend signal: ASP.NET Core API, DI wiring, controller endpoints, Npgsql execution, and integration tests.
- Strong "LLM should output structured intent, not raw executable SQL" signal.
- Strong evaluation/reliability signal: benchmark runner, structural scoring, grounded-answer checks, and regression history.
- Strong architecture/documentation signal: ADRs and phased implementation/review artifacts are unusually concrete.

## 5. Weaknesses / Gaps
- The most important gap is that the LLM boundary is still deterministic. This weakens any claim of real provider integration experience.
- The planner path is especially incomplete: the current planner gateway returns the same fixed plan regardless of question. That means the most interesting NL-to-plan step is not genuinely demonstrated yet.
- Prompt/context discipline is stronger in docs than in runtime code. Conversation memory, bounded planner context packaging, and trace persistence are planned but not implemented.
- Eval coverage is too thin to support strong quality claims today.

## 6. Interview Framing
- 30s: "I built a production-shaped .NET analytics API where the model produces a constrained `QueryPlan`, application code validates and compiles that into parameterized SQL, then a second model step synthesizes a grounded answer from real query results."
- 60-90s: "The core design choice was refusing free-form SQL generation. I defined a narrow analytics contract, validated the plan against whitelisted metrics/dimensions/filters, compiled only approved SQL fragments, and enforced read-only Postgres execution with timeouts and row caps. On top of that I added an eval loop that runs benchmark questions through the same path, scores structural correctness and grounding, and persists regression history."
- Phrases:
  - "structured query planning over raw SQL generation"
  - "deterministic guardrails around the LLM boundary"
  - "grounded answer synthesis from execution output"
  - "benchmark-driven prompt/version evaluation"

## 7. Resume Bullet Candidates
- Built an ASP.NET Core analytics API that converts natural-language questions into validated `QueryPlan` JSON, compiles parameterized SQL, and executes it safely against Postgres.
- Implemented defense-in-depth SQL guardrails with whitelist validation, fixed fragment compilation, read-only transactions, statement timeouts, and row-cap enforcement.
- Added an evaluation harness for benchmark questions with structural scoring, answer-grounding checks, and persisted regression comparison across prompt versions.

## 8. Gap-Closing Suggestions
- Replace `DeterministicLlmPlannerGateway` and `DeterministicLlmGateway` with one real provider integration and capture real latency/token/error metadata.
- Implement the planner prompt path for real, including prompt file loading, rendered bounded context, and question-dependent plan generation.
- Add at least 20-30 benchmark questions across aggregate, ranking, grouped, time-series, and follow-up rejection cases.
- Implement the missing conversation state and trace persistence pieces that are already described in the spec.

## 9. Design Improvements To Make Grounded More Valuable
- Add a real planner boundary first, not a real synthesizer boundary first. The biggest value in this project is NL -> validated `QueryPlan`; replacing the answer synthesizer with a real model before the planner would improve realism less.
- Introduce a provider-agnostic `ModelInvocation` layer with one concrete OpenAI-compatible implementation. Keep `ILlmPlannerGateway` and `ILlmGateway`, but back them with shared transport code for timeout policy, retries, model IDs, request IDs, token usage, and raw request/response capture.
- Make the planner prompt fully real and deterministic in inputs. Build a `PlannerContextBuilder` that assembles only the allowed schema summary, metric glossary, supported dimensions/filters, and a few fixed examples. This is the missing part that makes the project interview-credible as an actual LLM system instead of a compiler demo.
- Add strict structured-output parsing for the planner. The planner should return JSON only, parse into `QueryPlan`, and then pass through the existing validator. If parsing fails, add one repair attempt and persist both attempts in trace metadata.
- Persist traces to Postgres. Store question, prompt key/version/checksum, model, latency, token usage, raw model output, parsed `QueryPlan`, validation errors, compiled SQL, row count, and final answer. Right now the architecture talks about this, but implementing it would materially upgrade the project.
- Add a failure taxonomy and make it visible in eval results. Separate `transport_failure`, `timeout`, `json_parse_failure`, `schema_validation_failure`, `unsupported_request`, `sql_safety_failure`, and `execution_failure`. That gives you a much better benchmark story.
- Upgrade the benchmark suite from two happy-path cases to a proper matrix:
  - valid aggregate/ranking/grouped/time-series questions
  - unsupported questions
  - ambiguous wording
  - synonym-heavy phrasing
  - adversarial attempts to escape the schema contract
  - follow-up questions that should currently be rejected
- Add provider-free local replay fixtures. Capture real model responses once, then let tests replay them from disk so CI stays deterministic while the real provider path still exists in the application.
- Add cost and latency budgets in evals. Track p50/p95 planner latency, average tokens per request, and cost per benchmark run. That turns `Grounded` into a design that can support product tradeoff conversations, not just correctness demos.
- Implement compact conversation state only after the planner is real. The right order is: real planner -> trace/eval -> benchmark expansion -> compact memory. Otherwise you risk building memory on top of a fake planning step.

## 10. Overall Assessment
- This is one of the better "LLM app backend" demos in the repo set for a .NET-oriented role because the architecture is disciplined and the evaluation story is real.
- The ceiling is currently limited by the fake LLM boundary. As-is, it shows strong backend/system design and good LLM product judgment, but only moderate proof of production LLM integration.

## 11. Verification
- `dotnet test LlmIntegrationDemo.slnx --no-restore`
- Result: passed, 21/21 tests.
