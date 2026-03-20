# ADR-004: Query plan pipeline structure

## Status
Accepted

## Date
2026-03-19

## Context
Phase 2 introduces a `POST /analytics/query-plan` endpoint that accepts a structured `QueryPlan`, validates it, compiles it into SQL, enforces safety rules, and executes it against Postgres. The pipeline involves multiple discrete responsibilities: business validation, time range resolution, SQL compilation, safety gating, and database execution.

The key question is how to structure these responsibilities so that downstream components never receive unvetted SQL or unsupported metrics, and so that each step is independently testable.

## Decision
All analytics query plan handling is concentrated in `LlmIntegrationDemo.Api.Services`. The call stack must not leave this assembly before returning a result to the controller.

The pipeline is implemented as a strict linear sequence:

1. `QueryPlanValidator` — enforces version, question type, metric, dimension, filter, limit, sort, time range, and combination rules
2. `TimeRangeResolver` — converts controlled time presets into concrete UTC boundaries before any SQL is built
3. `QueryPlanCompiler` — builds parameterized SELECT or CTE SQL using fixed fragment mappings from `SqlFragmentRegistry`
4. `SqlSafetyGuard` — gates the compiled SQL against a disallowed keyword list, multi-statement check, and row cap bounds before execution is attempted
5. `AnalyticsQueryExecutor` — runs the sanitized query via Npgsql on a read-only transaction with a 15-second statement timeout

`SqlFragmentRegistry` is the single source of truth for all supported values and SQL mappings — metrics, dimensions, filter fields, operators, time presets, time grains, and sort options — keeping the validator, compiler, and tests in lock-step.

`AnalyticsController` is the only controller. It delegates entirely to `AnalyticsQueryPlanService`, which orchestrates the pipeline and maps outcomes to `200`, `400`, `422`, or `500` responses.

Stateless services (validator, compiler, registry, resolver, safety guard) are registered as singletons. Runtime-bound services (clock, executor) are registered behind interfaces (`IUtcClock`, `IAnalyticsQueryExecutor`) so tests can substitute fixed implementations without touching the HTTP layer.

## Consequences

### Positive
- Each pipeline stage has a single responsibility and can be tested independently.
- `SqlFragmentRegistry` as the single source of truth prevents validator/compiler drift.
- `SqlSafetyGuard` provides a defense-in-depth layer: even if the compiler is changed, unsafe SQL cannot reach the executor.
- The interface-backed clock and executor allow fully deterministic tests without a live database or real time dependency.

### Negative
- Adding a new metric, dimension, or filter requires touching the registry, validator, compiler, and tests in lock-step — there is no single registration point.
- `AnalyticsQueryPlanService` is the only orchestrator; if a second endpoint needs a subset of the pipeline, the pipeline cannot be partially reused without refactoring.
- All SQL generation logic lives in one compiler class; as the number of supported metrics and question types grows, this class will need careful organization to remain readable.

## Alternatives considered

### 1. Separate domain/application layer project
Rejected for Phase 2. The scope is a single endpoint with a fixed contract. A separate project adds indirection without benefit at this scale; the boundary can be introduced in a later phase if the API surface expands.

### 2. Inline validation and compilation in the controller
Rejected because it couples HTTP concerns to business logic, making individual steps untestable in isolation and the controller difficult to reason about.

### 3. Dynamic SQL generation from free-form QueryPlan fields
Rejected entirely. Deterministic fragment mappings in `SqlFragmentRegistry` are the core safety property of the design. Dynamic generation would undermine both the safety guard and the ability to audit what SQL the system can produce.
