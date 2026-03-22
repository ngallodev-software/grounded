# ADR-006: Grounding-based eval scoring and model invoker abstraction

## Status
Accepted

## Date
2026-03-20

## Context
The system makes two LLM calls per request — one to plan and one to synthesize. Both need to be exercisable without live API keys (for tests and CI), reproducible from fixtures (for eval regression), and swappable at the gateway level (for provider changes).

Naively, this leads to two problems:

1. **Test isolation:** If the gateway interface is the only seam, tests must stub the entire gateway. But gateways compose multiple concerns: prompt rendering, HTTP, response parsing, retry logic. Stubbing at the gateway level means tests cannot exercise prompt building or response parsing.

2. **Eval grounding:** The synthesizer can produce plausible-sounding answers that don't reflect the actual query results. A structural check (non-empty summary, at least one key point) is insufficient — it doesn't verify that the model used the data it was given.

## Decision

### Model invoker abstraction
HTTP, deterministic, and replay behavior are separated from gateway logic via `IModelInvoker`. Three implementations exist:

- `OpenAiCompatibleModelInvoker` — production HTTP; reads model, API key, and base URL from environment variables; enforces `temperature=0`, `max_tokens=500`, and a configurable timeout.
- `DeterministicModelInvoker` — generates answers directly from result rows using `DeterministicAnswerSynthesizerEngine`; no API call; used in tests by default.
- `ReplayModelInvoker` — matches prompts against `eval/replay_fixtures.json` by `promptKey` + substring; returns canned responses; used for eval runs where LLM calls would be expensive or non-deterministic.

`ModelInvokerResolver` selects an invoker by name. Gateways (`OpenAiCompatiblePlannerGateway`, `OpenAiCompatibleAnswerGateway`) construct a `ModelRequest` and delegate to the resolver — they do not own HTTP. `GROUNDED_REPLAY_MODE=true` switches both gateways to the replay invoker.

This means gateway tests can verify prompt assembly and response parsing without touching HTTP. The HTTP invoker is tested by its integration with the provider, not by unit tests.

### Grounding-based eval scoring
Each benchmark case is scored across three dimensions with explicit weights:

| Dimension | Weight | Check |
|---|---|---|
| Execution success | 0.5 | Query ran without error and returned rows |
| Structural correctness | 0.3 | Answer has non-empty `summary` and ≥ 1 `keyPoint` |
| Answer grounding | 0.2 | `summary` contains ≥ 1 value present in the actual result rows |

`AnswerOutputValidator` enforces grounding: it extracts scalar values from result rows and checks that at least one appears in the answer summary. An answer that passes structural checks but contains no data-traceable value fails grounding.

`Passed = executionSuccess AND structuralCorrectness`. Grounding is a signal, not a gate — a query can pass without perfect grounding, but the score reflects it. This matches real-world eval practice where grounding is measured separately from correctness.

`RegressionComparer` flags any case that passed in the previous run but fails in the current run. Score deltas are tracked across runs in `eval/regression_history.json`.

The UI includes an Eval tab that displays a static snapshot of a captured eval run (baked into the frontend from `eval/artifacts/`). It is a quick read-only view; it does not invoke `POST /analytics/eval`.

## Consequences

### Positive
- Tests run entirely in deterministic mode — no API keys, no network, no non-determinism.
- Eval runs can use replay fixtures for stable regression baselines independent of LLM output variability.
- Grounding check prevents the synthesizer from producing answers unrelated to the query results without the eval suite catching it.
- The invoker abstraction isolates the HTTP client configuration (timeout, base URL, auth) from gateway and prompt logic.

### Negative
- `GROUNDED_REPLAY_MODE` is a global flag — there is no per-gateway replay toggle. If planner replay is needed without synthesis replay (or vice versa), the flag is insufficient.
- Replay fixture matching uses substring search on prompt text, which is fragile if prompt templates change. Fixtures must be regenerated when prompts change.
- The grounding check is lexical, not semantic — it looks for value substrings, not meaning. A model that formats numbers differently than the result rows (e.g. `$48,200` vs `48200.00`) may fail grounding despite being correct.

## Alternatives considered

### 1. Single gateway interface with no invoker layer
Rejected because it collapses HTTP, prompt assembly, and parsing into one class, making each concern harder to test independently.

### 2. Structural-only eval (no grounding check)
Rejected because it cannot distinguish a well-formed answer that uses actual data from one that invents plausible-sounding values. The grounding check is the minimum bar for detecting hallucination.

### 3. Semantic similarity for grounding
Rejected for MVP. Lexical value matching is deterministic, requires no embedding infrastructure, and is sufficient for numeric analytics answers where the key signal is whether a number from the result appears in the text.
