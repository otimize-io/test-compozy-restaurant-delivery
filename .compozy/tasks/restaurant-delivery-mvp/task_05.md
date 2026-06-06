---
status: completed
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
- [x] 5.1 Consume the catalog-available event and index restaurants into Elasticsearch.
- [x] 5.2 Implement the search endpoint (name/cuisine/location query).
- [x] 5.3 Handle the no-match case as an empty result set.
- [x] 5.4 Wire Platform host, logging, and health.

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
  - [x] A query matching a seeded restaurant name returns that restaurant.
  - [x] A query by cuisine returns all restaurants of that cuisine.
  - [x] A query with no matches returns an empty array and HTTP 200.
- Integration tests:
  - [x] Consuming a catalog-available event indexes the restaurant and it becomes searchable (Testcontainers Elasticsearch).

> Done: 7 tests (search endpoint, consumer harness, + ES Testcontainers integration); coverage 89.83%.
> Indexes from `RestaurantDelivery.Contracts.Catalog.RestaurantPublished` only (ADR-004); endpoint
> `GET /api/restaurants?q=` (multi_match name/cuisine; blank q browses all; no match → empty 200).
> Client `Elastic.Clients.Elasticsearch` pinned to 8.13.4 to match the ES server image.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A consumer can find a seeded restaurant by name or cuisine.
- The index is populated purely from Catalog events.
