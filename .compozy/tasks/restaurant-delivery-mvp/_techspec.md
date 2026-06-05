# TechSpec — Restaurant Delivery MVP (iFood-style, Mocked Microservices)

## Executive Summary

A greenfield .NET + Angular system (ADR-003) implementing the PRD's three-sided delivery journey as
**seven event-driven microservices behind an API Gateway/BFF** (ADR-005): Search, Catalog, Order,
Payment, Dispatch, Tracking, Notification. Services communicate asynchronously over **RabbitMQ via
MassTransit**; the **Order service hosts the saga** that orchestrates
`place → pay → accept → assign driver → deliver` plus one compensation path (payment captured → no
driver → refund) (ADR-004). Each service owns a **polyglot datastore** matched to its access pattern
(ADR-006). The Angular client (single app with a role switcher) receives live status over **SignalR**
so the 5-stage tracking bar and all three role views update in ≤2 s (ADR-007). External dependencies
(payment, maps/ETA, notifications) sit behind **swappable ports/adapters**, with the payment seam
**async-shaped** (accepted → settlement via callback, idempotency key, declinable).

**Primary technical trade-off:** maximal microservices fidelity and realism (7 services, polyglot
persistence, async broker, real-time push) deliver a strong foundation and demonstration but impose
the heaviest operational footprint a mocked PoC can carry. This is mitigated by a single
`docker compose up` bootstrap, thin happy-path-only services, and seeded mock data — accepting higher
infrastructure weight in exchange for a credible, swap-ready architecture.

## System Architecture

### Component Overview

**Frontend**

- **Angular app** — single SPA with a **role switcher** (consumer/restaurant/driver views, no auth).
  Rich consumer UI (search, menu, cart, checkout, 5-stage tracking bar); functional-clean restaurant
  and driver views. Consumes the Gateway REST API and a SignalR hub.

**Edge**

- **API Gateway / BFF (YARP)** — single entry point; routes/aggregates reads for the client; hosts
  the **SignalR hub**; injects the demo role (`X-Demo-Role` + pre-seeded identity).

**Services (each independently deployable, owns its data)**

| Service | Responsibility | Datastore |
| ------- | -------------- | --------- |
| Search | Restaurant discovery/search | Elasticsearch |
| Catalog | Restaurants, menus, items | MongoDB |
| Order | Order aggregate + **saga orchestrator** + outbox | PostgreSQL |
| Payment | Async-shaped payment (mock seam) | PostgreSQL |
| Dispatch | Driver matching/assignment (mock seam) | Redis |
| Tracking | Order-status projection → SignalR source | Redis |
| Notification | Fire-and-forget notifications (mock seam) | Stateless |

**Messaging:** RabbitMQ (MassTransit). **Cross-service data flows only via integration events** — no
service reads another's store.

**Data flow (happy path):** Client → Gateway → Order (`POST /orders`) emits `OrderPlaced` → Payment
captures (async) → `PaymentSettled` → saga → restaurant `accept`/`ready` → saga requests dispatch →
`DriverAssigned` → driver `pickup`/`deliver` → `OrderDelivered`. Tracking consumes each event and
pushes `OrderStatusChanged` over SignalR. **Compensation:** Dispatch emits `DriverUnavailable` → saga
issues `RefundPayment` → `OrderRefunded` (terminal).

**Mocked external systems (behind ports):** payment provider, maps/ETA, notification channels — see
Integration Points.

## Implementation Design

### Core Interfaces

```csharp
// Payment seam — async-shaped: capture is ACCEPTED now; settlement arrives later via event.
public interface IPaymentPort
{
    // Idempotent: the same idempotencyKey returns the same PaymentAccepted.
    Task<PaymentAccepted> CaptureAsync(
        Guid orderId, decimal amount, string idempotencyKey, CancellationToken ct);

    Task RefundAsync(Guid orderId, string correlationId, CancellationToken ct);
}
public record PaymentAccepted(string CorrelationId);
// Outcome is delivered asynchronously as PaymentSettled / PaymentDeclined integration events.
```

```csharp
// Dispatch seam — V1 mock returns the nearest available driver, or null when none exists.
public interface IDriverMatcher
{
    Task<DriverAssignment?> FindDriverAsync(Guid orderId, GeoPoint restaurant, CancellationToken ct);
}
public record DriverAssignment(Guid DriverId, string DriverName, int EtaMinutes);
public record GeoPoint(double Lat, double Lng);
```

```csharp
// Order lifecycle — saga states; NoDriverRefunded is the terminal compensation state.
public enum OrderStatus
{
    Placed, AwaitingPayment, Paid, Accepted, Preparing, ReadyForPickup,
    DriverAssigned, PickedUp, Delivered, NoDriverRefunded
}
```

*Error conventions:* services return typed results; cross-service failures surface as integration
events (e.g., `PaymentDeclined`, `DriverUnavailable`) that the saga handles, never as cross-service
exceptions. Each consumer is idempotent on `(orderId, correlationId)`.

### Data Models

- **Order** — `Id, ConsumerId, RestaurantId, Items[], Total, Status, CorrelationId, CreatedAt,
  UpdatedAt` (PostgreSQL; saga instance + transactional outbox).
- **OrderItem** — `ItemId, Name, Quantity, UnitPrice`.
- **Restaurant** — `Id, Name, Cuisine, Location(GeoPoint)`; **MenuItem** — `Id, RestaurantId, Name,
  Description, Price` (MongoDB; indexed copy in Elasticsearch for Search).
- **Driver** — `Id, Name, Location(GeoPoint), Available` (Redis; seeded availability).
- **Payment** — `Id, OrderId, Amount, IdempotencyKey, Status(Accepted|Settled|Declined|Refunded),
  CorrelationId` (PostgreSQL).
- **TrackingView** — `OrderId, Stage(1..5), UpdatedAt` (Redis projection).
- **Integration events** — `OrderPlaced, PaymentAccepted, PaymentSettled, PaymentDeclined,
  OrderAccepted, OrderReady, DriverRequested, DriverAssigned, DriverUnavailable, OrderPickedUp,
  OrderDelivered, OrderRefunded` (shared `Contracts` library).

### API Endpoints (via Gateway/BFF)

| Method | Path | Description |
| ------ | ---- | ----------- |
| GET | `/api/restaurants?q=` | Search restaurants (Search) |
| GET | `/api/restaurants/{id}` | Restaurant detail (Catalog) |
| GET | `/api/restaurants/{id}/menu` | Menu/items (Catalog) |
| POST | `/api/orders` | Place order → starts saga (Order) |
| POST | `/api/orders/{id}/pay` | Initiate payment (Order→Payment) |
| POST | `/api/payments/callback` | Mock settlement webhook → `PaymentSettled/Declined` |
| GET | `/api/orders/{id}` | Current order status (Order/Tracking) |
| GET | `/api/restaurant/orders` | Restaurant order queue (New/In-Progress/Ready) |
| POST | `/api/orders/{id}/accept` | Restaurant accepts |
| POST | `/api/orders/{id}/ready` | Restaurant marks ready |
| GET | `/api/driver/assignments` | Driver's assignment(s) |
| POST | `/api/orders/{id}/pickup` | Driver confirms pickup |
| POST | `/api/orders/{id}/deliver` | Driver confirms delivery |
| WS | `/hubs/orders` | SignalR — server pushes `OrderStatusChanged` (per-order/role groups) |

Requests carry `X-Demo-Role` and a pre-seeded identity; standard codes (200/202/400/404/409).
`POST /orders/{id}/pay` returns **202 Accepted** with a `correlationId` (async settlement).

## Integration Points

All external dependencies are mocked behind ports (adapter pattern), each with a `Mock` adapter in V1
and an interface ready for a `Real` adapter later (Phase 2/3):

- **Payment provider** (`IPaymentPort`) — async capture → settlement via `/api/payments/callback`;
  idempotency key honored; configurable decline. No real auth/PCI in V1. Retry: MassTransit
  redelivery on the settlement consumer; saga timeout drives a terminal state if settlement never
  arrives.
- **Maps/ETA** (`IEtaPort`) — trivial mock returning a fixed ETA; ready for a real routing API.
- **Notification** (`INotificationPort`) — fire-and-forget; returns "accepted for delivery".
  Outbox-friendly so a real channel (email/SMS/push) can replace it.

## Impact Analysis

| Component | Impact Type | Description and Risk | Required Action |
| --------- | ----------- | -------------------- | --------------- |
| Solution + shared `Contracts` lib | new | Defines events/commands all services depend on; risk: contract churn | Scaffold first; version contracts |
| 7 services | new | Each a .NET service + datastore; risk: distributed-systems overhead | Build per Build Order; thin/happy-path |
| API Gateway/BFF + SignalR hub | new | Single entry + real-time; risk: connection management | YARP routing + hub groups |
| RabbitMQ | new (infra) | Async backbone; risk: message loss/dupes | Outbox + idempotent consumers |
| PostgreSQL/MongoDB/Elasticsearch/Redis | new (infra) | Polyglot stores; risk: bootstrap weight | docker-compose + seed scripts |
| Angular SPA | new | 3 role views + role switcher + SignalR client | Single app; consumer rich |

## Testing Approach

### Unit Tests

- **Per service** (all 7): domain logic, port adapters (mock payment idempotency/decline,
  nearest-driver matcher, notification), and especially the **Order saga state machine** (every
  transition + the compensation branch). Mock the broker and ports at unit level.

### Integration Tests

- **Per service** against its real datastore and broker using **Testcontainers**
  (PostgreSQL/Mongo/Elasticsearch/Redis/RabbitMQ): persistence, event publish/consume, idempotency,
  outbox.
- **End-to-end happy path** across the running stack: place → pay (callback) → accept → ready →
  assign → pickup → deliver, asserting the consumer reaches `Delivered` and SignalR emitted each
  stage.
- **Compensation test**: force `DriverUnavailable`, assert `RefundPayment` fires and the order
  terminates at `NoDriverRefunded` (PRD "compensation correctness").
- **Swap contract test (Phase 2 gate)**: replace the payment mock with a stub-real adapter satisfying
  `IPaymentPort`; assert zero changes outside the Payment service (PRD "mock swappability").

## Development Sequencing

### Build Order

1. **Solution scaffolding + shared `Contracts` library + `docker-compose`** (RabbitMQ + all
   datastores) — no dependencies.
2. **Order service + saga skeleton + PostgreSQL + outbox** — depends on step 1.
3. **Catalog + Search services + seed data** (MongoDB, Elasticsearch) — depends on step 1.
4. **Payment service** (async-shaped mock + `/payments/callback`) — depends on step 1; integrates
   with the saga from step 2.
5. **Restaurant flow** (`accept`/`ready` → saga transitions) — depends on steps 2 and 3.
6. **Dispatch service** (nearest-available mock, Redis) + driver-request step in the saga — depends on
   step 2.
7. **Driver flow** (`pickup`/`deliver` → saga transitions) — depends on steps 2 and 6.
8. **Compensation path** (`DriverUnavailable` → `RefundPayment` → `NoDriverRefunded`) — depends on
   steps 4 and 6.
9. **Tracking service** (Redis projection) + **SignalR hub** in the gateway — depends on steps 2, 6,
   and 7.
10. **Notification service** (fire-and-forget seam) — depends on step 2.
11. **API Gateway/BFF** (YARP aggregations + role switcher) — depends on steps 2, 3, 4, 5, 6, 7, and 9.
12. **Angular app** (single app, role switcher, 3 views, SignalR client) — depends on steps 11 and 9.
13. **Test suites** — per-service unit/integration grow with each service (steps 2–10); **E2E happy
    path** depends on step 12; **compensation test** depends on step 8; **swap contract test** depends
    on step 4.

### Technical Dependencies

- Docker/Docker Compose (one-command bootstrap is a success metric).
- RabbitMQ + PostgreSQL + MongoDB + Elasticsearch + Redis containers available locally.
- Shared `Contracts` library (step 1) blocks all service work.

## Monitoring and Observability

- **Structured logging** (Serilog) with a **correlation ID** propagated across events and HTTP, so
  one order is traceable end-to-end.
- **Health checks** (`/health`) per service + gateway; surfaced in the compose stack.
- **Saga metrics**: count/age of orders per `OrderStatus`; alert (dev-level) on orders stuck >
  timeout or landing in `NoDriverRefunded` unexpectedly.
- **Event log**: publish/consume counts per message type; dead-letter queue for poison messages.

## Technical Considerations

### Key Decisions

- **Decision:** RabbitMQ + in-code MassTransit saga in Order. **Rationale:** visible orchestration +
  compensation; transport-swappable. **Trade-off:** broker + eventual consistency. **Rejected:**
  choreography (hides the saga), sync-only (breaks async payment).
- **Decision:** 7 services + gateway. **Rationale:** microservices fidelity/foundation. **Trade-off:**
  ops weight. **Rejected:** ~5-6 consolidated, ~3 minimal.
- **Decision:** polyglot persistence. **Rationale:** per-service autonomy/realism. **Trade-off:**
  heaviest footprint. **Rejected:** shared Postgres, in-memory.
- **Decision:** SignalR. **Rationale:** true ≤2 s real-time. **Trade-off:** connection management.
  **Rejected:** polling, SSE.

### Known Risks

- **Operational/bootstrap weight** (7 services + 5 infra containers) — likely on modest machines →
  mitigation: one compose file, seeded data, thin services; document resource needs.
- **Eventual-consistency bugs** (lost/dup messages, stuck sagas) → mitigation: outbox, idempotent
  consumers, saga timeouts, DLQ.
- **Swap claim unproven until performed** → mitigation: the Phase-2 swap contract test is the gate.
- **Async payment shape** adds an `AwaitingPayment` state + callback → mitigation: modeled explicitly
  in the saga from step 2.

## Architecture Decision Records

- [ADR-001: V1 Scope — Full Three-Sided Demonstrable Journey on Swappable Microservice Seams](adrs/adr-001.md) — demonstrable three-sided journey with swappable mock seams.
- [ADR-002: V1 Product Approach — Fulfillable-Order Layering, Full Three-Sided MVP](adrs/adr-002.md) — Approach A; driver + compensation in the MVP; role switcher; consumer-rich UI.
- [ADR-003: Technology Stack — .NET Backend Microservices + Angular Frontend](adrs/adr-003.md) — mandated .NET + Angular.
- [ADR-004: Inter-Service Communication & Saga Orchestration](adrs/adr-004.md) — RabbitMQ + in-code MassTransit saga with compensation.
- [ADR-005: Service Decomposition — Full Canonical Seven Services + Gateway/BFF](adrs/adr-005.md) — 7 bounded-context services behind a YARP gateway.
- [ADR-006: Polyglot Persistence — Datastore per Service](adrs/adr-006.md) — PostgreSQL/MongoDB/Elasticsearch/Redis per access pattern.
- [ADR-007: Real-Time Status Updates — SignalR WebSocket Hub](adrs/adr-007.md) — SignalR push for the ≤2 s live tracking.
