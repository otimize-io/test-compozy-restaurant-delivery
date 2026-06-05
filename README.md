# Restaurant Delivery MVP

A mocked, microservices-based food-delivery marketplace (iFood-style), built as a proof-of-concept
and foundation for a real product. Stack: **.NET microservices + Angular**. See the planning
artifacts under [`.compozy/tasks/restaurant-delivery-mvp/`](.compozy/tasks/restaurant-delivery-mvp/)
(`_idea.md`, `_prd.md`, `_techspec.md`, `_tasks.md`, and `adrs/`).

## Prerequisites

- .NET SDK (pinned in [`global.json`](global.json))
- Docker / Docker Compose

## One-command bootstrap (infrastructure)

Bring up the full polyglot infrastructure (RabbitMQ, PostgreSQL, MongoDB, Elasticsearch, Redis) and
wait until every container reports healthy:

```bash
docker compose up -d --wait
```

Tear it down:

```bash
docker compose down
```

| Service | Port(s) | Purpose |
| ------- | ------- | ------- |
| RabbitMQ | 5682→5672 (AMQP), 15682→15672 (UI) | Async message broker (ADR-004). Host ports remapped to avoid a clash with another local RabbitMQ; internal port is still 5672. |
| PostgreSQL | 5432 | Order + Payment stores (ADR-006) |
| MongoDB | 27017 | Catalog store (ADR-006) |
| Elasticsearch | 9200 | Search index (ADR-006) |
| Redis | 6379 | Dispatch + Tracking stores (ADR-006) |

## Build & test

```bash
dotnet build
dotnet test                                  # all tests (requires the infra stack for integration)
dotnet test --filter "Category!=Integration" # unit tests only (no Docker needed)
```

## Repository layout

```
src/
  Shared/      # Contracts + Platform + Bootstrap helpers shared across services
  Services/    # The seven microservices (Search, Catalog, Order, Payment, Dispatch, Tracking, Notification)
  Gateway/     # API Gateway / BFF + SignalR hub
  Web/         # Angular single-page app (consumer/restaurant/driver views)
tests/         # Test projects
```

Services and the Angular app are added incrementally per `.compozy/tasks/restaurant-delivery-mvp/_tasks.md`.
