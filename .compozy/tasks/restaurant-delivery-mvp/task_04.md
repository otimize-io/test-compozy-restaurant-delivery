---
status: pending
title: Catalog service (MongoDB) + seed
type: backend
complexity: medium
dependencies:
  - task_03
---

# Catalog service (MongoDB) + seed

## Overview
Catalog owns restaurants, menus, and items, persisted in MongoDB and seeded with mock data, and
exposes restaurant-detail and menu reads. It is the source of truth for what the consumer browses
(PRD F2) and the data Search indexes.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST persist restaurants and menu items in MongoDB per TechSpec "Data Models".
- MUST seed mock restaurants and menus at startup so the demo has browsable data.
- MUST expose restaurant-detail and menu read endpoints (PRD F2).
- MUST publish a catalog-available signal/event that Search consumes to index restaurants (ADR-004).
- SHOULD use the Platform host builder, logging, and health checks from task_03.
</requirements>

## Subtasks
- [ ] 4.1 Define restaurant/menu-item models and MongoDB persistence.
- [ ] 4.2 Implement startup seeding of mock restaurants and menus.
- [ ] 4.3 Implement restaurant-detail and menu read endpoints.
- [ ] 4.4 Publish a catalog-seeded/restaurant-available event for Search.

## Implementation Details
Create the service under `src/Services/Catalog/`. Reference TechSpec "Data Models" (Restaurant,
MenuItem) and "API Endpoints" (`/api/restaurants/{id}`, `/api/restaurants/{id}/menu`). Use the
Platform library (task_03) for host, logging, health, and messaging.

### Relevant Files
- `src/Services/Catalog/*` — new; service host, models, Mongo repository, endpoints, seed.
- `tests/Catalog.Tests/*` — new; unit + integration tests.

### Dependent Files
- `src/Services/Search/*` — indexes restaurants published by Catalog.
- `src/Gateway/*` — routes restaurant/menu reads to Catalog.

### Related ADRs
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Catalog uses MongoDB.
- [ADR-005: Service Decomposition](../adrs/adr-005.md) — Catalog bounded context.

## Deliverables
- A Catalog service persisting/seeding restaurants and menus in MongoDB.
- Restaurant-detail and menu read endpoints.
- A catalog-available event published for Search.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against MongoDB via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] `GET /api/restaurants/{id}/menu` for a seeded restaurant returns its seeded items.
  - [ ] `GET /api/restaurants/{id}` with an unknown id returns 404.
  - [ ] Seeding produces the expected restaurant count on a fresh database.
- Integration tests:
  - [ ] Catalog writes and reads a restaurant document from MongoDB (Testcontainers).
  - [ ] On startup the catalog-seeded event is published and observable on the broker.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A consumer can retrieve a restaurant and its menu from seeded data.
- Search can obtain restaurant data via the published event.
