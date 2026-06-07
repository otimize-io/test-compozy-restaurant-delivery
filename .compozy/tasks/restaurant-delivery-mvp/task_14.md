---
status: completed
title: API Gateway/BFF + SignalR hub + E2E happy-path test
type: backend
complexity: high
dependencies:
  - task_04
  - task_05
  - task_06
  - task_07
  - task_08
  - task_10
  - task_12
---

# API Gateway/BFF + SignalR hub + E2E happy-path test

## Overview
The API Gateway/BFF is the single client entry point: it routes/aggregates reads to the services,
injects the demo role (no auth), and hosts the SignalR hub that pushes live status to the Angular
client (PRD F8/F10). This task also delivers the end-to-end happy-path API test proving the full
journey across the running stack.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST route the client-facing endpoints in TechSpec "API Endpoints" to Search, Catalog, Order, Payment, restaurant, and driver services.
- MUST inject the demo role via `X-Demo-Role` and a pre-seeded identity (no authentication) per ADR-002.
- MUST host a SignalR hub at `/hubs/orders` that fans out `OrderStatusChanged` from Tracking to per-order/role groups per ADR-007.
- MUST resync current status over REST on (re)connect so missed events are recovered.
- MUST provide an end-to-end happy-path API test across the running stack.
</requirements>

## Subtasks
- [x] 14.1 Configure YARP routing/aggregation to the services.
- [x] 14.2 Implement the role switcher (`X-Demo-Role` + pre-seeded identities).
- [x] 14.3 Implement the SignalR hub fanning out Tracking status to groups.
- [x] 14.4 Implement reconnect resync via the current-status read.
- [x] 14.5 Add the end-to-end happy-path API test.

## Implementation Details
Create the gateway under `src/Gateway/`. Reference TechSpec "System Architecture" (edge/BFF),
"API Endpoints", and ADR-007. Consumes Tracking's status-changed signal (task_12) and routes to the
service endpoints (task_04/05/06/07/08/10).

### Relevant Files
- `src/Gateway/*` — new; YARP config, role middleware, SignalR hub.
- `tests/E2E.Tests/*` — new; happy-path end-to-end API test.

### Dependent Files
- `src/Web/*` — the Angular app consumes the gateway REST + SignalR hub.

### Related ADRs
- [ADR-005: Service Decomposition](../adrs/adr-005.md) — gateway/BFF as single entry.
- [ADR-007: Real-Time Status Updates](../adrs/adr-007.md) — SignalR hub.
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — role switcher, no auth.

## Deliverables
- A gateway routing client endpoints, injecting the demo role, and hosting the SignalR hub.
- An end-to-end happy-path API test across the running stack.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration/E2E tests across the composed stack **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] A request with `X-Demo-Role: restaurant` is routed/authorized as the seeded restaurant identity.
  - [x] An unknown route returns 404 from the gateway.
  - [x] The hub places a subscriber into the correct per-order group.
- Integration tests:
  - [x] E2E happy path over the composed stack: place → pay (callback) → accept → ready → assign → pickup → deliver reaches `Delivered`, and `OrderStatusChanged` is pushed for each stage.
  - [x] On hub reconnect, the current status is re-fetched and matches the latest stage.
- Test coverage target: >=80%
- All tests must pass

> Done: Gateway (YARP routing + role switcher + SignalR hub consuming order events → push
> `OrderStatusChanged`); 27 tests (22 gateway unit/config + 5 full-stack E2E), Gateway coverage 94.66%.
> The full-stack E2E hosts Order/Payment/Dispatch/Tracking/Gateway in-process over a REAL RabbitMQ +
> Postgres/Redis (Testcontainers) and drives the whole journey through the gateway HTTP path with a real
> SignalR client. **Bug fixed during integration:** the Order accept/ready/pickup/deliver endpoints
> published to the EF bus outbox but never called `SaveChangesAsync`, so events were never delivered in
> production (per-service tests masked it via the in-memory harness). Added the outbox flush; the E2E now
> drives the real endpoints (no workaround).

## Success Criteria
- All tests passing
- Test coverage >=80%
- The full journey completes through the gateway and emits live status for each stage.
- The role switcher selects among the three pre-seeded identities without authentication.
