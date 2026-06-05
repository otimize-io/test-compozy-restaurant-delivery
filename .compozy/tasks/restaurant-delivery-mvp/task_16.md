---
status: pending
title: Angular restaurant & driver views
type: frontend
complexity: medium
dependencies:
  - task_15
---

# Angular restaurant & driver views

## Overview
The functional-clean restaurant and driver views within the Angular app (PRD F5–F7): the restaurant
order queue with accept/ready actions and the driver assignment view with pickup/deliver actions.
Both reuse the shell and SignalR store so one order updates live across all three views.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST implement the restaurant view: order queue (New/In-Progress/Ready) with accept and mark-ready actions (PRD F5).
- MUST implement the driver view: assignment list with pickup and deliver actions (PRD F7).
- MUST reuse the shell's role switcher and SignalR status store from task_15 so the three views update live from one order.
- MUST present functional-clean UIs (action buttons advancing the shared order), not full consumer-grade polish, per ADR-002.
- MUST call only the gateway endpoints (task_14).
</requirements>

## Subtasks
- [ ] 16.1 Implement the restaurant order-queue view with accept and ready actions.
- [ ] 16.2 Implement the driver assignment view with pickup and deliver actions.
- [ ] 16.3 Wire both views to the shared SignalR status store for live updates.
- [ ] 16.4 Ensure switching roles shows the same order advancing across views.

## Implementation Details
Add the views under `src/Web/src/app/restaurant/` and `src/Web/src/app/driver/`, reusing the shell
and SignalR store from task_15. Reference TechSpec "API Endpoints" (restaurant/driver actions) and
"User Experience".

### Relevant Files
- `src/Web/src/app/restaurant/*` — new; order queue + accept/ready actions.
- `src/Web/src/app/driver/*` — new; assignments + pickup/deliver actions.
- `src/Web/**/*.spec.ts` — new; component tests.

### Dependent Files
- `src/Web/src/app/core/signalr/*` — reused store from task_15 (no changes expected).

### Related ADRs
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — restaurant/driver are functional, consumer is rich.
- [ADR-007: Real-Time Status Updates](../adrs/adr-007.md) — shared live updates.

## Deliverables
- Functional restaurant and driver views wired to the live status store.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration/component tests for the restaurant and driver flows **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] Clicking "Accept" on a New order calls the gateway accept endpoint and moves the card to In-Progress.
  - [ ] Clicking "Ready" calls the ready endpoint and moves the card to Ready.
  - [ ] Clicking "Pickup" then "Deliver" in the driver view calls the respective endpoints and clears the assignment.
  - [ ] A restaurant action emits an update that the consumer tracking store reflects (shared store).
- Integration tests:
  - [ ] Driving one order across views: accepting in the restaurant view advances the consumer tracking bar live (mocked gateway/hub).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- Restaurant and driver actions advance the shared order and update all three views live.
- The views are functional-clean and call only the gateway.
