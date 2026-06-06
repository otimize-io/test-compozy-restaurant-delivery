---
status: completed
title: Payment service (async-shaped mock + swap-contract test)
type: backend
complexity: high
dependencies:
  - task_03
---

# Payment service (async-shaped mock + swap-contract test)

## Overview
Payment implements the async-shaped payment seam (PRD F4): it accepts a capture, settles
asynchronously via a callback, honors an idempotency key, and can decline. It is the flagship
swappable seam — this task also delivers the swap-contract test that proves a mock can be replaced by
a stub-real adapter with zero changes to neighboring services (PRD "mock swappability", Phase-2 gate).

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST implement the `IPaymentPort` contract from TechSpec "Core Interfaces": `CaptureAsync` returns `Accepted{correlationId}` and settlement arrives later via the `/api/payments/callback` endpoint as `PaymentSettled`/`PaymentDeclined`.
- MUST honor an idempotency key so a repeated capture returns the same result and charges once.
- MUST support a configurable decline path and a settlement-never-arrives path.
- MUST persist payment records in PostgreSQL (own database) per ADR-006.
- MUST provide a second `IPaymentPort` adapter (stub-real) and a swap-contract test asserting no neighbor service changes when swapping it in.
</requirements>

## Subtasks
- [x] 7.1 Implement the mock `IPaymentPort` adapter (async capture + settlement via callback).
- [x] 7.2 Implement the `/api/payments/callback` settlement webhook publishing `PaymentSettled`/`PaymentDeclined`.
- [x] 7.3 Implement idempotency-key dedupe and persistence of payment records.
- [x] 7.4 Add the configurable decline and settlement-timeout behaviors.
- [x] 7.5 Add a stub-real `IPaymentPort` adapter and the swap-contract test.

## Implementation Details
Create the service under `src/Services/Payment/`. Reference TechSpec "Core Interfaces"
(`IPaymentPort`), "Integration Points" (payment), and "Testing Approach" (swap contract test). Order
(task_06) issues `CapturePayment` and consumes the settlement events; do not couple to Order's
internals.

### Relevant Files
- `src/Services/Payment/*` — new; port + adapters, callback endpoint, persistence.
- `tests/Payment.Tests/*` — new; idempotency, decline, and swap-contract tests.

### Dependent Files
- `src/Services/Order/*` — consumes `PaymentSettled`/`PaymentDeclined` to advance the saga.
- `src/Gateway/*` — routes the `/api/payments/callback` and pay endpoints.

### Related ADRs
- [ADR-001: V1 Scope](../adrs/adr-001.md) — swappable seams; async-shaped payment.
- [ADR-004: Inter-Service Communication](../adrs/adr-004.md) — capture command + settlement events.
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Payment uses its own PostgreSQL database.

## Deliverables
- A Payment service with an async-shaped mock adapter, callback settlement, idempotency, and decline.
- A second (stub-real) adapter plus a passing swap-contract test.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against PostgreSQL + broker via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] `CaptureAsync` returns `Accepted{correlationId}` and does not return a terminal outcome inline.
  - [x] A repeated capture with the same idempotency key returns the same result and records one charge.
  - [x] A capture flagged to decline produces `PaymentDeclined` via the callback.
- Integration tests:
  - [x] Posting the settlement callback publishes `PaymentSettled` consumed by a test harness (Postgres Testcontainers + in-memory broker harness).
  - [x] Swap-contract test: swapping the mock for the stub-real adapter keeps the full capture→settlement flow passing with the only changed line being the single DI registration (zero blast radius outside Payment).

> Done: 24 tests (mock + stub-real adapters, settlement service, consumer harness, Postgres
> Testcontainers, swap-contract theory); coverage 91.83%. Async seam: `CaptureAsync → PaymentCaptureAccepted`,
> settlement via `POST /api/payments/callback`; decline via `Payment:DeclineAtOrAbove`; never-settle via
> `Payment:NeverSettleAtOrAbove`; idempotency on the store (unique key) + consumer `RunOnceAsync`.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- Payment settlement is asynchronous, idempotent, and declinable.
- The swap-contract test proves zero blast radius on neighbors (PRD Phase-2 gate metric).
