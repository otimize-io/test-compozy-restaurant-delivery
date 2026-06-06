---
status: completed
title: Order service & saga skeleton (PostgreSQL + outbox)
type: backend
complexity: high
dependencies:
  - task_03
---

# Order service & saga skeleton (PostgreSQL + outbox)

## Overview
Order is the heart of the system: it owns the order aggregate in PostgreSQL and hosts the MassTransit
saga that orchestrates the happy-path lifecycle (place → pay → accept → assign → deliver). This task
delivers the order-placement entry point and the saga state machine through the happy-path states,
with a transactional outbox for reliable event publishing.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST persist the order aggregate and saga instance in PostgreSQL with a transactional outbox per TechSpec "Data Models" and ADR-004.
- MUST expose order placement (`POST /api/orders`) and order status read (`GET /api/orders/{id}`) per TechSpec "API Endpoints".
- MUST implement the saga state machine through the happy-path `OrderStatus` transitions defined in TechSpec "Core Interfaces", reacting to contract events (payment, accept, dispatch, delivery).
- MUST initiate payment by issuing `CapturePayment` and model the `AwaitingPayment` state until settlement (async-shaped seam).
- MUST be idempotent on `(orderId, correlationId)` for all consumed events.
</requirements>

## Subtasks
- [x] 6.1 Define the order aggregate + EF Core persistence + transactional outbox.
- [x] 6.2 Implement `POST /api/orders` (creates the order, emits `OrderPlaced`) and `GET /api/orders/{id}`.
- [x] 6.3 Implement the saga state machine across the happy-path states (Placed→AwaitingPayment→Paid→Accepted→Preparing→ReadyForPickup→DriverAssigned→PickedUp→Delivered).
- [x] 6.4 Issue `CapturePayment` and transition on `PaymentSettled`/`PaymentDeclined`.
- [x] 6.5 Make all event consumers idempotent on `(orderId, correlationId)`.

## Implementation Details
Create the service under `src/Services/Order/`. Reference TechSpec "Core Interfaces" (`OrderStatus`),
"Data Models" (Order/OrderItem), and "Inter-Service Communication". Restaurant, dispatch, and driver
transitions are added by task_08, task_10, and task_11; this task wires the states and the
payment/placement legs and leaves the others as defined transition points.

### Relevant Files
- `src/Services/Order/*` — new; aggregate, EF Core context, saga state machine, endpoints, outbox.
- `tests/Order.Tests/*` — new; saga + endpoint tests.

### Dependent Files
- `src/Services/Restaurant flow (task_08)`, `Driver flow (task_10)`, `Compensation (task_11)`, `Tracking (task_12)` — extend or consume the saga's states/events.
- `src/Gateway/*` — routes order placement and status.

### Related ADRs
- [ADR-004: Inter-Service Communication & Saga Orchestration](../adrs/adr-004.md) — orchestration saga + outbox.
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Order uses PostgreSQL.
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — payment precedes dispatch.

## Deliverables
- An Order service with the order aggregate, placement/status endpoints, and the happy-path saga.
- Transactional outbox for reliable event publishing.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against PostgreSQL + broker via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `POST /api/orders` with a valid cart creates an order in `Placed` and emits `OrderPlaced`.
  - [x] The saga transitions `AwaitingPayment → Paid` on `PaymentSettled` and issues no driver request yet.
  - [x] A duplicate `PaymentSettled` for the same `(orderId, correlationId)` is ignored (idempotent).
  - [x] `GET /api/orders/{id}` returns the current `OrderStatus`.
- Integration tests:
  - [x] Placing an order persists it to PostgreSQL and publishes `OrderPlaced` via the outbox (Testcontainers).
  - [x] The saga reaches `Paid` end-to-end when a `PaymentSettled` event is delivered through the broker.

> Done: 29 tests (saga harness through Delivered, order service, status map, endpoints, + Postgres
> Testcontainers); coverage 97.31%. MassTransit state machine + EF Core saga repository + transactional
> outbox. The saga reacts to the FULL happy-path events, so tasks 08/10 only add the triggering
> endpoints and task_11 adds the `DriverUnavailable` compensation branch.
> Integration fix: the saga publishes the **DriverRequested event** (what Dispatch/task_09 consumes),
> reconciling the redundant RequestDriver-command vs DriverRequested-event pair in Contracts.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- An order can be placed and driven to `Paid` purely through events.
- The saga exposes clearly named transition points for restaurant, dispatch, and driver legs.
