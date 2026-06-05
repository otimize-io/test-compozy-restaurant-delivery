---
status: pending
title: Driver delivery flow (pickup/deliver + saga transitions)
type: backend
complexity: medium
dependencies:
  - task_06
  - task_09
---

# Driver delivery flow (pickup/deliver + saga transitions)

## Overview
Implements the driver side of the journey (PRD F7): the driver sees the assignment, confirms pickup,
and confirms delivery, advancing the order saga to its terminal `Delivered` state. Exposes the
driver assignments endpoint the driver view consumes.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST expose `GET /api/driver/assignments`, `POST /api/orders/{id}/pickup`, and `POST /api/orders/{id}/deliver` per TechSpec "API Endpoints".
- MUST advance the saga `DriverAssigned → PickedUp → Delivered` using the transition points from task_06 and the assignment from task_09.
- MUST emit `OrderPickedUp` and `OrderDelivered` for Tracking.
- MUST reject pickup/deliver on an order not in a valid prior state with HTTP 409.
</requirements>

## Subtasks
- [ ] 10.1 Implement the driver assignments read endpoint.
- [ ] 10.2 Implement pickup and deliver endpoints/commands.
- [ ] 10.3 Wire saga transitions `DriverAssigned → PickedUp → Delivered`.
- [ ] 10.4 Enforce valid-state guards (409) and emit pickup/delivered events.

## Implementation Details
Extend the Order saga (task_06) with the driver leg; consume the `DriverAssigned` outcome from
Dispatch (task_09). Reference TechSpec "API Endpoints" and "Core Interfaces" (`OrderStatus`).

### Relevant Files
- `src/Services/Order/Driver/*` — new; pickup/deliver handlers + assignments read.
- `tests/Order.Tests/Driver/*` — new; transition and guard tests.

### Dependent Files
- `src/Services/Tracking/*` — consumes `OrderPickedUp`/`OrderDelivered` to advance/complete the status bar.
- `src/Gateway/*` — routes driver endpoints.

### Related ADRs
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — driver assignment + delivery are in the MVP.
- [ADR-004: Inter-Service Communication](../adrs/adr-004.md) — saga consumes the assignment event.

## Deliverables
- Driver assignments endpoint and pickup/deliver flow advancing the saga to `Delivered`.
- `OrderPickedUp`/`OrderDelivered` events published.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests for the driver transitions via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] `POST /api/orders/{id}/pickup` on a `DriverAssigned` order moves it to `PickedUp` and emits `OrderPickedUp`.
  - [ ] `POST /api/orders/{id}/deliver` on a `PickedUp` order moves it to `Delivered` and emits `OrderDelivered`.
  - [ ] Pickup on an order not in `DriverAssigned` returns HTTP 409.
  - [ ] `GET /api/driver/assignments` lists the assignment created for an assigned driver.
- Integration tests:
  - [ ] From `DriverAssigned`, pickup then deliver drives a persisted order to `Delivered` with both events observed (Testcontainers).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A driver can take an assigned order through pickup to delivery (terminal `Delivered`).
- Invalid transitions are safely rejected.
