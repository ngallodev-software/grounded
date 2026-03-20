# ADR-003: Deterministic context packaging

## Status
Accepted

## Date
2026-03-19

## Context
One of the explicit goals of the project is to gain real experience with context-window management.

A naive implementation would send:
- the full schema
- the full chat history
- large prior result sets
- miscellaneous implementation notes

That approach wastes tokens, increases noise, and makes model behavior less predictable.

The system only needs enough context to interpret a constrained analytics question over a fixed domain.

## Decision
Each planner call will receive a bounded, deterministic context package composed of:

- the current user question
- compact structured conversation state
- relevant schema subset
- metric glossary
- allowed dimensions and filters
- fixed few-shot examples
- strict output contract

The system will not replay the full raw conversation history into every call.

Conversation memory will be stored as structured state, such as:
- last metric
- last dimensions
- last filters
- last time range
- last limit
- last referenced entity

Schema context will be compressed and hand-authored rather than generated from full DDL dumps.

## Consequences

### Positive
- lower token usage
- more predictable prompts
- easier control of prompt size
- cleaner handling of follow-up questions
- stronger interview story around context-window design

### Negative
- requires additional application logic for context building
- some follow-up cases may fail if structured state is incomplete
- less flexible than replaying full history for unusual conversations

## Alternatives considered

### 1. Full conversation replay
Rejected because it is token-heavy and noisy for this bounded use case.

### 2. Full schema dump in every planner prompt
Rejected because it increases prompt size and exposes irrelevant detail.

### 3. LLM-generated conversation summaries each turn
Rejected for MVP because deterministic structured state is simpler and easier to validate.