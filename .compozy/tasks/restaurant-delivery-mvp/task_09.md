---
status: completed
title: Dispatch service (nearest-available mock, Redis)
type: backend
complexity: medium
dependencies:
  - task_03
---

# Dispatch service (nearest-available mock, Redis)

## Overview
Dispatch implements the driver-matching seam (PRD F6): on a driver request it finds the nearest
available driver from Redis-backed seed data and publishes an assignment, or reports that no driver
is available (the trigger for the compensation path). The matcher sits behind `IDriverMatcher` so a
real/global matcher can replace it later.

<critical>
- ALWAYS READ the PRD and TechSpec before starting
- REFERENCE TECHSPEC for implementation details — do not duplicate here
- FOCUS ON "WHAT" — describe what needs to be accomplished, not how
- MINIMIZE CODE — show code only to illustrate current structure or problem areas
- TESTS REQUIRED — every task MUST include tests in deliverables
</critical>

<requirements>
- MUST implement the `IDriverMatcher` contract from TechSpec "Core Interfaces": return the nearest available driver or null.
- MUST consume `DriverRequested` and publish `DriverAssigned` (driver found) or `DriverUnavailable` (none) per ADR-004.
- MUST store and seed driver availability/location in Redis per ADR-006.
- MUST keep the matcher behind the port so a batched/ETA matcher can swap in later (no caller changes).
</requirements>

## Subtasks
- [x] 9.1 Implement the Redis-backed driver store and seed data.
- [x] 9.2 Implement the nearest-available `IDriverMatcher` mock.
- [x] 9.3 Consume `DriverRequested`; publish `DriverAssigned` or `DriverUnavailable`.
- [x] 9.4 Provide a deterministic "no driver" toggle for the compensation path.

## Implementation Details
Create the service under `src/Services/Dispatch/`. Reference TechSpec "Core Interfaces"
(`IDriverMatcher`), "Integration Points", and "Data Models" (Driver). The saga's driver-request step
and the consumption of assignment results live in task_06/task_10/task_11; Dispatch only responds to
the `DriverRequested` event.

### Relevant Files
- `src/Services/Dispatch/*` — new; matcher port + mock, Redis store, event handlers, seed.
- `tests/Dispatch.Tests/*` — new; matcher + event tests.

### Dependent Files
- `src/Services/Order/*` — consumes `DriverAssigned`/`DriverUnavailable` to advance/compensate the saga.
- `src/Services/Driver flow (task_10)` — uses the assignment.

### Related ADRs
- [ADR-006: Polyglot Persistence](../adrs/adr-006.md) — Dispatch uses Redis.
- [ADR-005: Service Decomposition](../adrs/adr-005.md) — Dispatch bounded context.

## Deliverables
- A Dispatch service with a Redis-backed nearest-available matcher behind `IDriverMatcher`.
- `DriverAssigned`/`DriverUnavailable` publishing and a deterministic no-driver toggle.
- Unit tests with 80%+ coverage **(REQUIRED)**.
- Integration tests against Redis + broker via Testcontainers **(REQUIRED)**.

## Tests
- Unit tests:
  - [x] With one available seeded driver, `FindDriverAsync` returns that driver.
  - [x] With no available drivers, `FindDriverAsync` returns null and the handler emits `DriverUnavailable`.
  - [x] Given two drivers, the geographically nearer one is selected.
- Integration tests:
  - [x] A `DriverRequested` event results in a `DriverAssigned` event when a seeded driver exists (Testcontainers Redis + in-memory broker harness).

> Done: 13 tests (matcher, consumer-via-harness, DriverSeeder, + 2 Testcontainers-Redis integration);
> coverage 95.38% (Program.cs excluded as composition root). No-driver path is toggled by
> `Dispatch:SeedDrivers=false` (empty store) per subtask 9.4.
- Test coverage target: >=80%
- All tests must pass

## Success Criteria
- All tests passing
- Test coverage >=80%
- Dispatch assigns the nearest available driver or cleanly reports none.
- The matcher is swappable behind `IDriverMatcher` with no caller changes.
