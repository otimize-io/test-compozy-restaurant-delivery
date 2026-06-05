---
status: pending
title: Shared Contracts library (events/commands)
type: backend
complexity: medium
dependencies:
  - task_01
---

# Shared Contracts library (events/commands)

## Overview
Define the integration events and commands that all services exchange over RabbitMQ in one shared
library, so services depend on stable message contracts rather than on each other. This library is
the single cross-service coupling point and is referenced by every service and the gateway.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST define the integration events and commands listed in TechSpec "Data Models" (e.g., `OrderPlaced`, `PaymentAccepted`, `PaymentSettled`, `PaymentDeclined`, `OrderAccepted`, `OrderReady`, `DriverRequested`, `DriverAssigned`, `DriverUnavailable`, `OrderPickedUp`, `OrderDelivered`, `OrderRefunded`) plus the commands `CapturePayment`, `RefundPayment`, `RequestDriver`.
- MUST include an order/correlation identifier on every message and an idempotency key on payment-capture messages.
- MUST be the only place service-to-service message shapes are defined; no service domain models may leak into it.
- SHOULD namespace/version messages to allow later evolution without breaking consumers.
</requirements>

## Subtasks
- [ ] 2.1 Define the integration event record types from the TechSpec event list.
- [ ] 2.2 Define the command record types (`CapturePayment`, `RefundPayment`, `RequestDriver`).
- [ ] 2.3 Add correlation/order identifiers and the payment idempotency key field.
- [ ] 2.4 Package as a referenced library and confirm it has no dependency on any service project.

## Implementation Details
Create the contract types under `src/Shared/Contracts/`. Reference TechSpec "Data Models →
Integration events". Keep these as plain message records (DTOs) only — no behavior, no persistence.

### Relevant Files
- `src/Shared/Contracts/Events/*.cs` — new; integration event records.
- `src/Shared/Contracts/Commands/*.cs` — new; command records.

### Dependent Files
- `src/Services/*` and `src/Gateway/*` — all reference Contracts to publish/consume messages.

### Related ADRs
- [ADR-004: Inter-Service Communication & Saga Orchestration](../adrs/adr-004.md) — events/commands are the only cross-service data path.

## Deliverables
- A referenced Contracts library containing all integration events and commands.
- Correlation identifiers and a payment idempotency key on the relevant messages.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration test proving a published message is consumed with an identical payload **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] Every event/command record round-trips through the configured JSON serializer with no field loss.
  - [ ] `OrderPlaced` carries both `OrderId` and `CorrelationId`.
  - [ ] `CapturePayment` carries a non-empty `IdempotencyKey` field.
- Integration tests:
  - [ ] A published `OrderPlaced` is received with an identical payload via an in-memory MassTransit test harness.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- The Contracts project has zero references to any service project.
- All message types from the TechSpec event list are present.
