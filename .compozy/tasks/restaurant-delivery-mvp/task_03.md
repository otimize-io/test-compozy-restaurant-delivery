---
status: pending
title: Shared Platform library (messaging, logging, correlation, health)
type: backend
complexity: medium
dependencies:
  - task_01
  - task_02
---

# Shared Platform library (messaging, logging, correlation, health)

## Overview
Provide the common cross-service wiring every microservice reuses: MassTransit/RabbitMQ setup,
Serilog structured logging with correlation-ID propagation, health-check registration, and an
idempotent-consumer helper. This keeps all seven services consistent and observable and removes
boilerplate from each service task.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST provide reusable MassTransit + RabbitMQ configuration (consumer registration, retry/redelivery) per TechSpec "Inter-Service Communication".
- MUST provide Serilog structured logging with a correlation-ID enricher and middleware that propagates the ID across HTTP requests and published/consumed messages (TechSpec "Monitoring and Observability").
- MUST provide a health-check registration helper exposing `/health`.
- MUST provide an idempotent-consumer helper keyed on `(orderId, correlationId)`.
- SHOULD provide a standard service host builder so each service starts the same way.
</requirements>

## Subtasks
- [ ] 3.1 Implement the MassTransit + RabbitMQ configuration helper.
- [ ] 3.2 Implement Serilog logging with the correlation-ID enricher and HTTP/message middleware.
- [ ] 3.3 Implement the `/health` registration helper.
- [ ] 3.4 Implement the idempotent-consumer helper.
- [ ] 3.5 Provide a common host builder used by all services.

## Implementation Details
Create the helpers under `src/Shared/Platform/`. Reference TechSpec "Inter-Service Communication" and
"Monitoring and Observability". Depends on the Contracts library for message-type registration.

### Relevant Files
- `src/Shared/Platform/Messaging/*.cs` — new; MassTransit/RabbitMQ + idempotency helpers.
- `src/Shared/Platform/Observability/*.cs` — new; Serilog + correlation middleware + health checks.

### Dependent Files
- `src/Services/*` and `src/Gateway/*` — all use Platform for messaging, logging, and health.

### Related ADRs
- [ADR-004: Inter-Service Communication & Saga Orchestration](../adrs/adr-004.md) — MassTransit transport and idempotency.
- [ADR-007: Real-Time Status Updates](../adrs/adr-007.md) — event-driven status that the hub later fans out.

## Deliverables
- A Platform library exposing messaging, logging/correlation, health, and idempotency helpers.
- A standard host builder consumed by service tasks.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration test proving a Platform-configured service connects to RabbitMQ and reports healthy **(REQUIRED)**.

## Tests
- Unit tests:
  - [ ] The correlation middleware adds an incoming `X-Correlation-ID` to the Serilog log context.
  - [ ] A request without a correlation ID gets a newly generated GUID.
  - [ ] The idempotency helper returns the cached result for a repeated `(orderId, correlationId)` and executes only once.
- Integration tests:
  - [ ] A service built via the host builder connects to RabbitMQ and serves a healthy `/health` (Testcontainers RabbitMQ).
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- A service can be stood up using only the Platform host builder plus its own handlers.
- Correlation IDs appear in logs across an HTTP→message hop.
