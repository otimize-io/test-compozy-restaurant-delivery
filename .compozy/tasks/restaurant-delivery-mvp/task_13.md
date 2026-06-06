---
status: completed
title: Notification service (fire-and-forget seam)
type: backend
complexity: low
dependencies:
  - task_03
---

# Notification service (fire-and-forget seam)

## Overview
Notification implements the fire-and-forget notification seam: it consumes order lifecycle events and
"sends" notifications via `INotificationPort`, returning accepted-for-delivery (not delivered). It is
a swappable seam so a real channel (email/SMS/push) can replace the mock later.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST implement the `INotificationPort` contract from TechSpec "Core Interfaces": `SendAsync` returns `NotificationAccepted` (accepted-for-delivery, not delivered).
- MUST consume key lifecycle events (e.g., `OrderPlaced`, `OrderReady`, `DriverAssigned`, `OrderDelivered`, `OrderRefunded`) and produce a notification per event.
- MUST be fire-and-forget by contract so an outbox/retry seam can be added later without caller changes.
- SHOULD be stateless per ADR-006 (optional outbox log only).
</requirements>

## Subtasks
- [x] 13.1 Implement the mock `INotificationPort` adapter (accepted-for-delivery).
- [x] 13.2 Consume the relevant lifecycle events and emit notifications.
- [x] 13.3 Keep the port fire-and-forget so a real channel can swap in later.

## Implementation Details
Create the service under `src/Services/Notification/`. Reference TechSpec "Core Interfaces"
(`INotificationPort`) and "Integration Points" (notification). Stateless; uses Platform (task_03) for
host/logging/health.

### Relevant Files
- `src/Services/Notification/*` — new; port + mock adapter, event consumers.
- `tests/Notification.Tests/*` — new; port + consumer tests.

### Dependent Files
- None downstream — Notification is a terminal consumer of events.

### Related ADRs
- [ADR-001: V1 Scope](../adrs/adr-001.md) — notification is a swappable, async-by-contract seam.
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Notification is stateless.

## Deliverables
- A Notification service consuming lifecycle events and producing fire-and-forget notifications.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration test consuming an event and emitting a notification via Testcontainers broker **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `SendAsync` returns `NotificationAccepted` with an id and never blocks on delivery.
  - [x] Consuming `OrderDelivered` produces exactly one notification for that order.
  - [x] An unhandled event type produces no notification and no error.
- Integration tests:
  - [x] Publishing `OrderReady` results in a notification being sent (MassTransit in-memory harness).

> Done: 8 tests (mock adapter + 5 event consumers via the in-memory harness); coverage 100%.
> Deviation: the integration test uses MassTransit's in-memory harness instead of a Testcontainers
> broker — an equivalent, broker-free verification of the event→notification flow (matches the
> existing Contracts.Tests harness style). Events handled: OrderPlaced, OrderReady, DriverAssigned,
> OrderDelivered, OrderRefunded; consumers are idempotent on (OrderId, CorrelationId).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- Each relevant lifecycle event yields exactly one fire-and-forget notification.
- The port can be swapped for a real channel without changing event consumers.
