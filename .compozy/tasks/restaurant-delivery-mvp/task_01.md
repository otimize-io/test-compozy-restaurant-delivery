---
status: completed
title: Solution scaffolding & infrastructure compose
type: infra
complexity: medium
dependencies: []
---

# Solution scaffolding & infrastructure compose

## Overview
Establish the greenfield repository foundation: a .NET solution with a consistent `src/`/`tests/`
layout, an Angular workspace placeholder, and a single-command `docker compose` that brings up the
polyglot infrastructure (RabbitMQ, PostgreSQL, MongoDB, Elasticsearch, Redis). Every other task
builds on this scaffolding and the one-command bootstrap is a PRD success metric.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST create a .NET solution and the `src/Shared`, `src/Services`, `src/Gateway`, `src/Web`, and `tests/` layout described in TechSpec "System Architecture".
- MUST provide a `docker-compose.yml` that starts RabbitMQ, PostgreSQL, MongoDB, Elasticsearch, and Redis, each with a health check.
- MUST achieve one-command bootstrap: `docker compose up` brings the infrastructure to healthy within the PRD target (< 5 min).
- MUST include `.editorconfig` and `global.json` pinning the .NET SDK version.
- SHOULD centralize per-store connection settings so later services consume them via configuration/environment.
</requirements>

## Subtasks
- [x] 1.1 Create the solution file and the `src/Shared`, `src/Services`, `src/Gateway`, `src/Web`, `tests/` skeleton.
- [x] 1.2 Author `docker-compose.yml` with the five infrastructure containers and health checks.
- [x] 1.3 Add `.editorconfig`, `global.json`, and a README documenting the one-command bootstrap.
- [x] 1.4 Add a bootstrap smoke check that asserts all five infrastructure services report healthy.

> Note: the RabbitMQ host ports are published as 5682→5672 / 15682→15672 to avoid a clash with
> another RabbitMQ already running on this dev machine. The internal port is unchanged (services
> use `rabbitmq:5672` on the Docker network); see `README.md` and `docker-compose.yml`.

## Implementation Details
Create `RestaurantDelivery.sln`, `docker-compose.yml`, `.editorconfig`, `global.json`, `README.md`,
and the empty folder skeleton. See TechSpec "System Architecture" (component/datastore table) and
"Technical Dependencies". Do not add service code here — only the shell and infrastructure.

### Relevant Files
- `RestaurantDelivery.sln` — new solution that aggregates all service/test projects.
- `docker-compose.yml` — new; defines the five infra containers + health checks.
- `.editorconfig`, `global.json` — new; code style and SDK pin.
- `README.md` — new; bootstrap instructions.

### Dependent Files
- `src/Services/*`, `src/Gateway/*`, `src/Web/*` — every later project lives under this layout and consumes the infra connection settings.

### Related ADRs
- [ADR-005: Service Decomposition](../adrs/adr-005.md) — defines the seven services + gateway layout.
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — defines which datastores the compose file must provide.

## Deliverables
- A buildable .NET solution and the documented folder layout.
- A `docker-compose.yml` bringing up RabbitMQ + PostgreSQL + MongoDB + Elasticsearch + Redis with health checks.
- README bootstrap instructions and a smoke check script.
- Unit tests with 80%+ coverage of any bootstrap/health-check helper code **(REQUIRED)**.
- Integration test verifying the full infra stack reaches healthy **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `global.json` pins a .NET SDK major version that is installed on the build agent.
  - [x] `docker-compose.yml` parses and declares services named `rabbitmq`, `postgres`, `mongo`, `elasticsearch`, `redis`.
  - [x] The health-check helper reports "unhealthy" when a container port is unreachable.
- Integration tests:
  - [x] `docker compose up` brings all five infrastructure services to `healthy` within 5 minutes.
  - [x] Each infrastructure port is reachable from the host after bootstrap.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- `docker compose up` is the only command needed to start the infrastructure stack.
- The solution builds clean with the pinned SDK.
