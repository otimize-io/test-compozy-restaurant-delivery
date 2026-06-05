---
status: pending
title: Search service (Elasticsearch)
type: backend
complexity: medium
dependencies:
  - task_03
  - task_04
---

# Search service (Elasticsearch)

## Overview
Search owns restaurant discovery, indexing restaurant data from Catalog into Elasticsearch and
serving the consumer's search/browse endpoint (PRD F1). It is the entry point of the consumer
journey.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST index restaurant data (from the Catalog event in task_04) into Elasticsearch per TechSpec "Data Models".
- MUST expose a search endpoint that queries by restaurant name, cuisine, and/or location (PRD F1).
- MUST return an empty result set (not an error) when nothing matches.
- SHOULD use the Platform host builder, logging, and health checks from task_03.
</requirements>

## Subtasks
- [ ] 5.1 Consume the catalog-available event and index restaurants into Elasticsearch.
- [ ] 5.2 Implement the search endpoint (name/cuisine/location query).
- [ ] 5.3 Handle the no-match case as an empty result set.
- [ ] 5.4 Wire Platform host, logging, and health.

## Implementation Details
Create the service under `src/Services/Search/`. Reference TechSpec "API Endpoints"
(`/api/restaurants?q=`) and "Data Models". Index population comes from the Catalog event (task_04);
do not read Catalog's database directly (ADR-004).

### Relevant Files
- `src/Services/Search/*` — new; indexer, query handler, endpoint.
- `tests/Search.Tests/*` — new; unit + integration tests.

### Dependent Files
- `src/Gateway/*` — routes `/api/restaurants?q=` to Search.

### Related ADRs
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Search uses Elasticsearch.
- [ADR-004: Inter-Service Communication](../adrs/adr-004.md) — indexing is fed by events, not cross-DB reads.

## Deliverables
- A Search service that indexes restaurants and serves discovery queries.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against Elasticsearch via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] A query matching a seeded restaurant name returns that restaurant.
  - [ ] A query by cuisine returns all restaurants of that cuisine.
  - [ ] A query with no matches returns an empty array and HTTP 200.
- Integration tests:
  - [ ] Consuming a catalog-available event indexes the restaurant and it becomes searchable (Testcontainers Elasticsearch).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A consumer can find a seeded restaurant by name or cuisine.
- The index is populated purely from Catalog events.
