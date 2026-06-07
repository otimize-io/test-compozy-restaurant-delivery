# Restaurant Delivery MVP

A mocked, microservices-based food-delivery marketplace (iFood-style), built as a proof-of-concept and
**foundation for a real product**. It delivers the full three-sided journey — **consumer**, **restaurant**,
and **driver** — over seven independent .NET services behind a YARP gateway, with an Angular SPA and
real-time order tracking. Every external dependency (payment, maps/ETA, notifications) is a *mock behind a
swappable port*.

Stack: **.NET 10 microservices + Angular 20**. Async backbone: **RabbitMQ / MassTransit** with an
orchestrated **saga** (transactional outbox). Polyglot persistence per service. Planning artifacts live under
[`.compozy/tasks/restaurant-delivery-mvp/`](.compozy/tasks/restaurant-delivery-mvp/) (`_idea.md`, `_prd.md`,
`_techspec.md`, `_tasks.md`, `adrs/`). Consolidated architecture: [`SYSTEM_DESIGN.md`](SYSTEM_DESIGN.md).
UI conventions: see [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md).

---

## Architecture (C4)

### Level 1 — System Context

```mermaid
C4Context
  title System Context — Restaurant Delivery MVP (mocked PoC)
  Person(consumer, "Consumer", "Searches, orders, pays, tracks")
  Person(restaurant, "Restaurant operator", "Accepts & prepares orders")
  Person(driver, "Driver", "Accepts assignment, picks up, delivers")
  System(rd, "Restaurant Delivery Platform", "Three-sided marketplace: Angular SPA + .NET microservices")
  System_Ext(pay, "Payment provider", "Mocked, async-shaped (swappable seam)")
  System_Ext(maps, "Maps / ETA provider", "Mocked (swappable seam)")
  System_Ext(notify, "Notification channels", "Mocked fire-and-forget (swappable seam)")
  Rel(consumer, rd, "Browses, orders, pays, tracks", "HTTPS / WebSocket")
  Rel(restaurant, rd, "Manages incoming orders", "HTTPS / WebSocket")
  Rel(driver, rd, "Handles deliveries", "HTTPS / WebSocket")
  Rel(rd, pay, "Capture / refund", "port → adapter")
  Rel(rd, maps, "ETA", "port → adapter")
  Rel(rd, notify, "Notify", "port → adapter")
```

### Level 2 — Containers

```mermaid
C4Container
  title Container diagram — Restaurant Delivery MVP
  Person(user, "Consumer / Restaurant / Driver", "Uses the single SPA")
  System_Boundary(rd, "Restaurant Delivery Platform") {
    Container(spa, "Angular SPA", "Angular 20", "Role switcher; consumer-rich UI; live tracking")
    Container(gw, "API Gateway / BFF", ".NET, YARP + SignalR", "Single entry; routes REST; /hubs/orders; X-Demo-Role")
    Container(order, "Order", ".NET", "Order aggregate + orchestration saga + outbox")
    Container(payment, "Payment", ".NET", "Async-shaped payment seam")
    Container(dispatch, "Dispatch", ".NET", "Driver matching seam")
    Container(catalog, "Catalog", ".NET", "Restaurants + menus")
    Container(search, "Search", ".NET", "Restaurant discovery")
    Container(tracking, "Tracking", ".NET", "5-stage status projection")
    Container(notification, "Notification", ".NET", "Fire-and-forget notifications")
    ContainerQueue(bus, "RabbitMQ", "MassTransit", "Events / commands / saga")
    ContainerDb(pg, "PostgreSQL", "relational", "Order + Payment")
    ContainerDb(mongo, "MongoDB", "document", "Catalog")
    ContainerDb(es, "Elasticsearch", "search", "Search index")
    ContainerDb(redis, "Redis", "key-value", "Dispatch + Tracking")
  }
  Rel(user, spa, "Uses", "HTTPS")
  Rel(spa, gw, "REST + SignalR", "HTTPS / WSS")
  Rel(gw, search, "routes", "HTTP")
  Rel(gw, catalog, "routes", "HTTP")
  Rel(gw, order, "routes", "HTTP")
  Rel(gw, payment, "routes", "HTTP")
  Rel(gw, tracking, "routes", "HTTP")
  Rel(gw, bus, "consumes order events → SignalR", "AMQP")
  Rel(order, bus, "saga events + commands", "AMQP")
  Rel(payment, bus, "settle / refund", "AMQP")
  Rel(dispatch, bus, "assign driver", "AMQP")
  Rel(catalog, bus, "RestaurantPublished", "AMQP")
  Rel(search, bus, "index from events", "AMQP")
  Rel(tracking, bus, "project events", "AMQP")
  Rel(notification, bus, "notify on events", "AMQP")
  Rel(order, pg, "reads/writes", "")
  Rel(payment, pg, "reads/writes", "")
  Rel(catalog, mongo, "reads/writes", "")
  Rel(search, es, "reads/writes", "")
  Rel(dispatch, redis, "reads/writes", "")
  Rel(tracking, redis, "reads/writes", "")
```

### Level 3 — Order service components (the core)

```mermaid
C4Component
  title Component diagram — Order service
  Container_Boundary(order, "Order service") {
    Component(api, "Order endpoints", "Minimal API", "POST /orders, GET /orders/{id}, accept/ready/pickup/deliver")
    Component(saga, "OrderStateMachine", "MassTransit saga", "Placed→Paid→Preparing→AwaitingDriver→…→Delivered (+ compensation)")
    Component(outbox, "Transactional outbox", "EF Core", "Atomic save + publish")
    Component(repo, "Saga + aggregate store", "EF Core / PostgreSQL", "Order rows + saga instances")
  }
  ContainerQueue(bus, "RabbitMQ", "MassTransit", "")
  ContainerDb(pg, "PostgreSQL", "", "")
  Rel(api, repo, "reads status / guards", "")
  Rel(api, outbox, "publish lifecycle events", "")
  Rel(saga, outbox, "send CapturePayment / DriverRequested / RefundPayment", "")
  Rel(saga, repo, "persist instance", "")
  Rel(outbox, bus, "deliver on commit", "AMQP")
  Rel(repo, pg, "", "")
```

### Runtime — happy path + compensation

```mermaid
sequenceDiagram
  actor C as Consumer
  participant GW as Gateway
  participant O as Order (saga)
  participant P as Payment
  participant D as Dispatch
  participant T as Tracking
  C->>GW: POST /api/orders
  GW->>O: place order (OrderPlaced)
  O--)P: CapturePayment
  C->>GW: POST /api/payments/callback (settle)
  P--)O: PaymentSettled  (→ Paid)
  Note over GW,O: restaurant accept / ready (OrderAccepted, OrderReady)
  O--)D: DriverRequested
  D--)O: DriverAssigned
  Note over GW,O: driver pickup / deliver (OrderPickedUp, OrderDelivered → Delivered)
  O--)T: lifecycle events → 5-stage projection
  GW--)C: OrderStatusChanged (SignalR, per stage)
  rect rgb(253,236,236)
    Note over O,D: Compensation — no driver: DriverUnavailable → RefundPayment + OrderRefunded → NoDriverRefunded
  end
```

---

## Services

| Service | Responsibility | Datastore | Notes |
| ------- | -------------- | --------- | ----- |
| **Gateway** | Single entry (YARP), role switcher, SignalR hub | — | `/hubs/orders`, `X-Demo-Role` |
| **Search** | Restaurant discovery | Elasticsearch | indexes from `RestaurantPublished` |
| **Catalog** | Restaurants + menus | MongoDB | seeds + publishes `RestaurantPublished` |
| **Order** | Order aggregate + orchestration saga | PostgreSQL | transactional outbox |
| **Payment** | Async-shaped payment seam | PostgreSQL | idempotent; declinable; swappable |
| **Dispatch** | Driver matching seam | Redis | nearest-available mock; swappable |
| **Tracking** | 5-stage status projection | Redis | rebuildable from events |
| **Notification** | Fire-and-forget notifications | stateless | swappable channel |

Shared libraries: `src/Shared/Contracts` (integration events/commands), `src/Shared/Platform`
(MassTransit + Serilog + correlation + health + idempotency), `src/Shared/Bootstrap` (infra smoke check).

---

## Running it

### Prerequisites
- .NET SDK (pinned in [`global.json`](global.json)) · Docker / Docker Compose · Node 20+ (for the SPA)

### Infrastructure only (datastores + broker)
```bash
docker compose up -d --wait rabbitmq postgres mongo elasticsearch redis
```
| Service | Host port(s) | Used by |
| ------- | ------------ | ------- |
| RabbitMQ | 5682→5672 (AMQP), 15682→15672 (UI) | all (async backbone) |
| PostgreSQL | 5432 | Order, Payment |
| MongoDB | 27017 | Catalog |
| Elasticsearch | 9200 | Search |
| Redis | 6379 | Dispatch, Tracking |

### Full stack (all services + gateway + web)
```bash
docker compose build                 # builds 9 images (7 services + gateway + web)
docker compose up -d --wait --wait-timeout 300   # all 14 containers; no license required
# Web UI:  http://localhost:4200      Gateway API: http://localhost:8080
docker compose down
```

Verified end-to-end on the composed stack: a live journey through the gateway
(`place → settle → accept → ready → pickup → deliver`) reaches tracking stage 5 (Delivered).

### Build & test (.NET)
```bash
dotnet build
dotnet test                                  # all tests (integration uses Testcontainers → needs Docker)
dotnet test --filter "Category!=Integration" # unit tests only (no Docker)
```

### Angular app
```bash
cd src/Web
npm install
npm start            # dev server on http://localhost:4200 (point environment.apiBase at the gateway)
npm run build        # production build
npm run test:coverage
```

---

## Repository layout
```
src/
  Shared/      Contracts · Platform · Bootstrap (shared libraries)
  Services/    Search · Catalog · Order · Payment · Dispatch · Tracking · Notification
  Gateway/     API Gateway / BFF + SignalR hub
  Web/         Angular SPA (consumer / restaurant / driver)
tests/         per-service test projects + E2E.Tests (full-stack)
Dockerfile     parameterized multi-stage build for all .NET services + gateway
docker-compose.yml   infra + application tier
infra/         postgres init (creates order + payment databases)
```

## Quality
- **~297 tests**: per-service unit + integration (Testcontainers: RabbitMQ, PostgreSQL, MongoDB, Redis,
  Elasticsearch), a **full-stack E2E** through the gateway with a real SignalR client, and Angular (Jest).
- Coverage: .NET services 90–100%, Gateway ~95%, Angular ~97% (`Program.cs` composition roots excluded via
  `coverlet.runsettings`).

## Known limitations
- **Messaging library:** the services use **MassTransit 8.5.10 (Apache-2.0)** so the RabbitMQ transport and
  the full `docker compose up` run require **no license**. (MassTransit 9 made the RabbitMQ transport
  commercial — needing `MT_LICENSE` — which is why this project pins v8.)
- **Mocked PoC:** payment, maps/ETA, and notifications are mocks behind swappable ports; no real money or PII.
- **Scope:** one happy path + one compensation (no-driver → refund); broad edge cases are out of scope (see
  `_prd.md` Non-Goals).
