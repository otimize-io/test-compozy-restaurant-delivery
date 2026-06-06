---
status: completed
title: Restaurant order flow (accept/ready + saga transitions)
type: backend
complexity: medium
dependencies:
  - task_06
---

# Restaurant order flow (accept/ready + saga transitions)

## Overview
Implements the restaurant side of the journey (PRD F5): the restaurant receives the paid order,
accepts it, and marks it ready for pickup, advancing the shared order saga. Exposes the restaurant
order queue (New → In Progress → Ready) that the restaurant view consumes.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST expose `POST /api/orders/{id}/accept` and `POST /api/orders/{id}/ready` per TechSpec "API Endpoints".
- MUST advance the saga `Paid → Accepted → Preparing → ReadyForPickup` using the transition points from task_06.
- MUST expose the restaurant order queue (`GET /api/restaurant/orders`) grouped New/In-Progress/Ready.
- MUST reject accept/ready on an order not in a valid prior state with HTTP 409.
</requirements>

## Subtasks
- [x] 8.1 Implement accept and ready endpoints/commands.
- [x] 8.2 Wire the saga transitions `Paid → Accepted` and `→ ReadyForPickup`.
- [x] 8.3 Implement the restaurant order-queue read grouped by status.
- [x] 8.4 Enforce valid-state guards (409 on invalid transition).

## Implementation Details
Extend the Order saga (task_06) with the restaurant leg; restaurant-facing endpoints live in the
Order service or a thin restaurant module that drives the saga. Reference TechSpec "API Endpoints"
and "Core Interfaces" (`OrderStatus`). Emits `OrderAccepted`/`OrderReady` for Tracking and Dispatch.

### Relevant Files
- `src/Services/Order/Restaurant/*` — new; accept/ready handlers + queue read.
- `tests/Order.Tests/Restaurant/*` — new; transition and guard tests.

### Dependent Files
- `src/Services/Tracking/*` — consumes `OrderAccepted`/`OrderReady` to advance the status bar.
- `src/Services/Dispatch/*` — `OrderReady` is the cue to request a driver.
- `src/Gateway/*` — routes restaurant endpoints.

### Related ADRs
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — restaurant-accept is a real cross-boundary transition in the MVP.

## Deliverables
- Accept/ready endpoints advancing the saga and the restaurant order queue.
- `OrderAccepted`/`OrderReady` events published.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests for the restaurant transitions via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `POST /api/orders/{id}/accept` on a `Paid` order moves it to `Accepted` and emits `OrderAccepted`.
  - [x] `POST /api/orders/{id}/ready` on an `Accepted`/`Preparing` order moves it to `ReadyForPickup` and emits `OrderReady`.
  - [x] Accepting an order that is not `Paid` returns HTTP 409.
  - [x] The restaurant queue groups orders into New/In-Progress/Ready correctly.
- Integration tests:
  - [x] Accept then ready drives a persisted order through both transitions and both events are observed (Testcontainers).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A paid order can be accepted and marked ready, emitting the events Dispatch/Tracking rely on.
- Invalid transitions are safely rejected.
