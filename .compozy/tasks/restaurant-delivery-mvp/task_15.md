---
status: completed
title: Angular shell + role switcher + consumer view
type: frontend
complexity: high
dependencies:
  - task_14
---

# Angular shell + role switcher + consumer view

## Overview
The Angular single-page app shell with the role switcher and the rich consumer journey (PRD F1–F4,
F8): search, restaurant/menu, cart, checkout, and the live 5-stage tracking bar driven by SignalR.
This is the demonstrable face of the consumer side and the legible spine of the demo.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST provide a single Angular app shell with a role switcher (consumer/restaurant/driver) sending `X-Demo-Role` per ADR-002.
- MUST implement the rich consumer flow: search → restaurant/menu → cart → checkout/pay → live tracking (PRD F1–F4, F8).
- MUST render the 5-stage tracking bar (Order placed → Preparing → Driver assigned/en route → Out for delivery → Delivered) updating live via the SignalR client per ADR-007.
- MUST connect only to the gateway REST API and SignalR hub (task_14) — no direct service calls.
- MUST resync status via REST on (re)connect.
</requirements>

## Subtasks
- [x] 15.1 Scaffold the Angular app shell and the role switcher.
- [x] 15.2 Implement search and restaurant/menu browsing screens.
- [x] 15.3 Implement cart and checkout/payment screens.
- [x] 15.4 Implement the live 5-stage tracking bar with the SignalR client.
- [x] 15.5 Implement reconnect resync via the status read.

## Implementation Details
Create the app under `src/Web/`. Reference TechSpec "System Architecture" (frontend), "API Endpoints",
and ADR-007. Talks exclusively to the gateway (task_14).

### Relevant Files
- `src/Web/src/app/shell/*`, `src/Web/src/app/consumer/*` — new; shell, role switcher, consumer screens.
- `src/Web/src/app/core/signalr/*` — new; SignalR client + status store.
- `src/Web/**/*.spec.ts` — new; component/service tests.

### Dependent Files
- `src/Web/src/app/restaurant/*`, `src/Web/src/app/driver/*` — added by task_16, reuse the shell + SignalR store.

### Related ADRs
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — consumer-rich UI, role switcher.
- [ADR-007: Real-Time Status Updates](../adrs/adr-007.md) — SignalR client.

## Deliverables
- An Angular shell + role switcher + complete consumer journey with a live tracking bar.
- A reusable SignalR client/status store for the other role views.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration/component tests for the consumer flow **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] Searching a term calls the gateway search endpoint and renders the returned restaurants.
  - [x] Adding items updates the cart total correctly.
  - [x] An `OrderStatusChanged` for "Preparing" advances the tracking bar to stage 2.
  - [x] The role switcher sets the `X-Demo-Role` header on outgoing requests.
- Integration tests:
  - [x] Consumer component flow: search → add to cart → checkout → pay renders the tracking screen and reflects a delivered status from the hub (mocked gateway/hub).
- Test coverage target: >=80%
- All tests must pass

> Done: Angular 20.1 SPA in `src/Web` (standalone components, signals), test runner **Jest** (jest-preset-angular).
> Shell + role-switcher HttpInterceptor (`X-Demo-Role`); consumer journey search → menu → cart → checkout
> (places order then settles payment) → live 5-stage tracking bar via `@microsoft/signalr` (Subscribe +
> OrderStatusChanged + reconnect resync from `GET /api/orders/{id}/status`). Talks only to the gateway
> (`environment.apiBase`). `ng build` clean; **72 Jest tests pass, 96.73% line coverage**. Restaurant/driver
> routes are placeholders, filled by task_16.

## Success Criteria
- All tests passing
- Test coverage >=80%
- A consumer can complete search → pay → track with a live-advancing 5-stage bar.
- The app talks only to the gateway and SignalR hub.
