---
status: completed
title: Tracking service (Redis 5-stage projection)
type: backend
complexity: medium
dependencies:
  - task_06
---

# Tracking service (Redis 5-stage projection)

## Overview
Tracking consumes order lifecycle events and projects them into the consumer's 5-stage status (PRD
F8) in Redis, serving as the single source the SignalR hub fans out. It makes "one order, three
live views" possible and is rebuildable purely from events.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST consume the order lifecycle events and maintain a `TrackingView` (5 stages) in Redis per TechSpec "Data Models" and ADR-006.
- MUST map events to the 5 stages: Order placed → Preparing → Driver assigned/en route → Out for delivery → Delivered (and the refunded terminal state).
- MUST expose a current-status read used on (re)connect to resync, and emit a status-changed signal for the SignalR hub (ADR-007).
- MUST be rebuildable from events (Redis state is a disposable projection).
</requirements>

## Subtasks
- [x] 12.1 Consume order events and project to the 5-stage `TrackingView` in Redis.
- [x] 12.2 Map each lifecycle event to its tracking stage (including refunded terminal).
- [x] 12.3 Expose a current-status read endpoint for resync.
- [x] 12.4 Emit a status-changed signal consumed by the gateway SignalR hub.

## Implementation Details
Create the service under `src/Services/Tracking/`. Reference TechSpec "Data Models" (TrackingView),
"System Architecture" (data flow), and ADR-007. Consumes events from Order/restaurant/driver legs
(task_06/08/10/11).

### Relevant Files
- `src/Services/Tracking/*` — new; event projectors, Redis store, status read.
- `tests/Tracking.Tests/*` — new; projection tests.

### Dependent Files
- `src/Gateway/*` — the SignalR hub fans out Tracking's status-changed signal.

### Related ADRs
- [ADR-007: Real-Time Status Updates](../adrs/adr-007.md) — Tracking is the projection feeding the hub.
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Tracking uses Redis.

## Deliverables
- A Tracking service projecting order events to the 5-stage status in Redis.
- A current-status read endpoint and a status-changed signal for the hub.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against Redis + broker via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `OrderPlaced` sets stage 1 (Order placed).
  - [x] `OrderAccepted` advances the view to stage 2 (Preparing).
  - [x] `OrderDelivered` advances to stage 5 (Delivered); `OrderRefunded` sets the refunded terminal stage.
  - [x] Replaying the event stream from empty reconstructs the same `TrackingView`.
- Integration tests:
  - [x] A sequence of lifecycle events on the broker is projected into Redis and the read endpoint returns the latest stage (Testcontainers).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- The consumer status reflects the correct stage for each lifecycle event.
- The projection can be rebuilt from events alone.
