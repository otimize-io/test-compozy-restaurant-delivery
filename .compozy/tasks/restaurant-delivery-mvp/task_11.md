---
status: completed
title: Compensation path (no-driver → refund) + compensation test
type: backend
complexity: high
dependencies:
  - task_06
  - task_07
  - task_09
---

# Compensation path (no-driver → refund) + compensation test

## Overview
Implements the single failure path that justifies the saga (PRD F9): when Dispatch reports no driver
after payment, the saga issues a refund and drives the order to the terminal `NoDriverRefunded`
state. This is the load-bearing distributed-rollback behavior; the task includes the compensation
test that proves it.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST handle `DriverUnavailable` in the saga by issuing `RefundPayment` and transitioning to `NoDriverRefunded` per TechSpec "Core Interfaces" and ADR-004.
- MUST cause the Payment service to refund the captured payment via `IPaymentPort.RefundAsync` and emit `OrderRefunded`.
- MUST leave the order in a consistent terminal state with no "paid but undelivered" orphan.
- MUST be exactly ONE compensation path — no cancellation, retries, or partial refunds (Non-Goals).
</requirements>

## Subtasks
- [x] 11.1 Add the `DriverUnavailable` handler to the saga issuing `RefundPayment`.
- [x] 11.2 Implement payment refund (`RefundAsync`) and `OrderRefunded` emission.
- [x] 11.3 Transition the order to the terminal `NoDriverRefunded` state.
- [x] 11.4 Add the compensation test (fault injection → refund → terminal state).

## Implementation Details
Extend the Order saga (task_06) with the compensation branch; reuse the Dispatch no-driver toggle
(task_09) and the Payment refund (task_07). Reference TechSpec "Testing Approach → Compensation
test" and "Core Interfaces" (`OrderStatus.NoDriverRefunded`).

### Relevant Files
- `src/Services/Order/Saga/Compensation*.cs` — new; compensation branch.
- `src/Services/Payment/*` — refund handling (extends task_07).
- `tests/Order.Tests/Compensation/*` — new; compensation test.

### Dependent Files
- `src/Services/Tracking/*` — reflects the refunded/cancelled terminal status.

### Related ADRs
- [ADR-002: V1 Product Approach](../adrs/adr-002.md) — exactly one compensation path in the MVP.
- [ADR-004: Inter-Service Communication](../adrs/adr-004.md) — compensation via command + event.

## Deliverables
- A saga compensation branch: `DriverUnavailable → RefundPayment → NoDriverRefunded`.
- Payment refund + `OrderRefunded` event.
- The compensation test proving consistent termination.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration test for the full compensation path via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] On `DriverUnavailable`, the saga issues exactly one `RefundPayment` command.
  - [x] After `OrderRefunded`, the order is in `NoDriverRefunded` and accepts no further transitions.
  - [x] A duplicate `DriverUnavailable` does not issue a second refund (idempotent).
- Integration tests:
  - [x] Compensation test: with the Dispatch no-driver toggle on, a paid order ends at `NoDriverRefunded`, the payment is refunded, and no order remains "paid but undelivered" (Testcontainers).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- 100% of injected no-driver cases refund and terminate consistently (PRD "compensation correctness").
- Only one compensation path exists; no extra failure branches were added.
