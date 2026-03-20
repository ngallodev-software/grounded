# ADR-002: No vector search for MVP

## Status
Accepted

## Date
2026-03-19

## Context
The project answers analytics questions over a small, fixed relational schema:
- customers
- products
- orders
- order_items

A common pattern in AI applications is to add embeddings and vector retrieval early.  
In this project, doing so would add infrastructure and complexity:
- embedding generation
- vector storage
- retrieval logic
- retrieval evaluation
- more moving parts in prompt assembly

The project goal is to demonstrate practical LLM application architecture, not to build a generic RAG platform.

## Decision
The MVP will not use embeddings, vector search, or RAG infrastructure.

Prompt context will instead be assembled from curated, deterministic artifacts:
- schema catalog
- metric glossary
- allowed analytics surface
- compact conversation state

## Consequences

### Positive
- simpler architecture
- faster path to working MVP
- lower debugging surface
- better determinism
- tighter alignment with the actual problem shape
- easier explanation in interviews

### Negative
- less extensible if the project later expands to documents or many schemas
- no semantic retrieval of examples or documentation in MVP

## Alternatives considered

### 1. Embedding-based schema retrieval
Rejected because the schema is small and fixed enough to manage with curated context.

### 2. Example retrieval using vector similarity
Rejected for MVP because a fixed few-shot set is sufficient at this stage.

### 3. Full RAG pipeline over schema docs and metrics docs
Rejected because it solves a problem this MVP does not yet have.