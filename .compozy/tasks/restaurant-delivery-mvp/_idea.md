# Restaurant Delivery MVP (iFood-style, Mocked Microservices)

## Overview

A mocked, microservices-based food-delivery marketplace in the style of iFood, built as a
**proof-of-concept and foundation for a real product**. It demonstrates the full three-sided journey
of an online delivery marketplace — **consumer** (search, order, pay, track), **restaurant**
(receive, accept, prepare), and **driver** (receive assignment, pick up, deliver) — over a backbone
of independent microservices.

The product is for the team that will later turn this skeleton into a real delivery service: every
external dependency (payment, maps/ETA, notifications) is a *mock behind a swappable port*, so a mock
can be replaced by a real provider later with minimal blast radius. V1 is deliberately demonstrable:
all three sides are interactive, exercising a single end-to-end happy path where **payment is
confirmed before a driver is assigned**. Ambition is a Strategic Bet — a realistic, legible skeleton
that de-risks the real build — not a market competitor to iFood.

## Problem

Building a delivery marketplace means coordinating three independent actors (eater, restaurant,
courier) across several specialized concerns — search, catalog, ordering, payment, dispatch,
tracking — each with different scaling, failure, and ownership characteristics. Teams that start this
as a monolith discover late that payment, driver-matching, and notifications need to evolve and fail
independently, and that retrofitting clean boundaries after launch is expensive. A greenfield team
needs a **concrete, runnable reference** of how these pieces fit before committing real integrations
and real money.

The second problem is integration risk. Real providers (payment gateways, maps APIs, push/SMS) are
asynchronous, rate-limited, and failure-rich. If a prototype wires them as synchronous, never-failing
calls, swapping mocks for real services later forces cascading rewrites across services. A foundation
PoC must get the *shape* of these seams right early — even while everything is still mocked — so the
eventual integration is a plug-in, not a rebuild.

The third problem is demonstrability. Stakeholders and future contributors need to *see* the journey
work end to end across all three roles to trust the design and align on scope. A purely architectural
slice with no interfaces does not communicate the product; a visible three-sided flow does.

### Market Data

- **Brazil food-delivery market:** ~US$1.3B (2024) projected to ~US$4.5B by 2033, ~15% CAGR (*IMARC
  Group, 2024/25*; estimates vary by methodology). Active food-app users in Brazil surpassed 58M in
  2023, +37% vs 2020.
- **iFood** dominates Brazil with ~80%+ share, 1,700+ cities, 55M+ users, 350,000+ partner
  restaurants, ~3.5M monthly restaurant transactions (*Statista / market reports, 2023-24*).
- **Competitive set:** Rappi (#2, super-app); 99Food/DiDi (relaunched mid-2025 with R$1B on lower
  commissions, no exclusivity, fast driver payouts); Keeta/Meituan (new entrant litigating
  exclusivity at CADE). The Brazilian market is **reopening to competition** in 2025-26.
- **Reference architecture:** DoorDash (~67% US share) and Uber Eats (~23%) are the canonical public
  engineering references. Industry uses an **orchestrated saga** for order→payment→delivery with
  **compensating actions** (refund on dispatch failure), and **ETA-driven, batched driver matching**
  (evolved from greedy nearest-driver). Brazilian incumbents do not publish internal topology, so
  architecture references are generalized from DoorDash/Uber Eats.

## Core Features

| #   | Feature | Priority | Description |
| --- | ------- | -------- | ----------- |
| F1 | Restaurant Search & Discovery | Critical | Consumer searches/browses restaurants by name, cuisine, or location; results served by a Search/Discovery service over mocked catalog data. |
| F2 | Menu & Item Detailing | Critical | Consumer opens a restaurant, views its menu, item details and prices, and adds items to a cart; served by a Catalog/Menu service. |
| F3 | Cart & Order Placement | Critical | Consumer reviews the cart and places the order; an Order service creates the order aggregate and starts its lifecycle. |
| F4 | Payment (mock, swappable, async-shaped) | Critical | Payment is taken **before** dispatch via a Payment service behind a swappable port: async capture with a correlation/idempotency key, settlement via callback, and a configurable decline. Mock now, real provider later. |
| F5 | Restaurant Order Management | High | Restaurant-side interactive UI: receive the paid order, **accept** it, mark it preparing/ready — a real cross-boundary transition on the shared order. |
| F6 | Driver Dispatch & Assignment (mock, swappable) | High | After payment, the system finds and associates a driver via a Dispatch service (nearest-available mock behind a swappable port); the driver accepts the assignment. |
| F7 | Driver Delivery Flow | High | Driver-side interactive UI: view the assignment, mark picked up, mark delivered — advancing the order to its terminal state. |
| F8 | Real-time Order Tracking | High | Consumer sees live order status across the journey (placed → paid → accepted → assigned → picked up → delivered), driven by service events. |
| F9 | Order Orchestration (saga backbone) | Medium | The order lifecycle is an explicit orchestration across services, defined so the orchestration engine stays swappable. *Strongly recommended:* one compensation path (payment captured → no driver → refund). |

## KPIs

| KPI | Target | How to Measure |
| --- | ------ | -------------- |
| End-to-end happy-path completion | 100% of demo runs complete search → pay → accept → assign → deliver without manual data intervention | Scripted/automated end-to-end run |
| Three-sided journey functional | All 3 role UIs (consumer, restaurant, driver) can complete their part of one order | Manual/automated walkthrough per role |
| Mock swappability | 0 cross-service code changes to replace a mock (e.g., payment) with a stub-real adapter | Count files/services touched during the swap |
| Service independence | 100% of services build & run independently; killing one degrades gracefully (others still start) | Isolated startup + kill-one-service test |
| One-command bootstrap | Full stack up with a single command in < 5 min | Time the bootstrap command |

## Feature Assessment

| Criteria | Question | Score |
| -------- | -------- | ----- |
| **Impact** | How much more valuable does this make the product? | Strong — it is the whole foundation, but mocked |
| **Reach** | What % of users would this affect? | Must do — the core flows every role touches |
| **Frequency** | How often would users encounter this value? | Strong — food ordering is high-frequency |
| **Differentiation** | Does this set us apart or just match competitors? | Maybe — features are commodity; differentiation is the swappable architecture, not the features |
| **Defensibility** | Easy to copy or compounds over time? | Pass — no market moat; the real delivery moat (network/logistics density) is not built by a mock |
| **Feasibility** | Can we actually build this? | Strong — mocked, happy-path, but three rich UIs + microservices is real work |

**Leverage type:** Strategic Bet (a foundation that opens future possibilities).

## Council Insights

- **Recommended approach:** The council and product strategist recommended a *depth-first* path — one
  tracer-bullet order across real seams, thin UIs, and the swap proof promoted to a CI-enforced
  invariant. **The user chose breadth instead** (a demonstrable three-sided product), accepting less
  architecture-proof depth. Both directions are documented; the depth-first path becomes the V2
  stretch goal.
- **Key trade-offs:** Demonstrable three-sided breadth vs. concentrated proof of swappability; rich
  UIs (commodity, do not compound) vs. seam machinery (compounds, reusable); "minimal happy path"
  vs. the one compensation path that makes a saga worth building.
- **Risks identified:**
  - *"Saga costume"* — a happy-path-only flow never fires a compensation, proving none of the hard
    distributed-rollback behavior. **Mitigation:** strongly recommend including the single payment →
    no-driver → refund path (see Open Questions).
  - *Sync/infallible mocks bake false shapes into callers*, forcing rewrites when real async
    providers arrive. **Mitigation:** keep payment and notification ports async-shaped (callback
    settlement, idempotency key, declinable) even in V1 — cheap insurance.
  - *Scope creep across three rich UIs.* **Mitigation:** cap each side at its happy path; defer all
    combinatorial edge cases.
- **Stretch goal (V2+):** Promote the swap proof to a CI-enforced invariant (two adapters per port +
  shared contract tests); adopt a workflow engine (Temporal/Cadence) for orchestration; eventually
  extract a domain-agnostic saga + swappable-adapter template.

## Differentiator

The honest competitive angle is **not** the features — those are table stakes that match iFood. It is
the **swappable mock-seam discipline**: every external integration (payment, maps/ETA, notifications)
lives behind a port/adapter with a mock implementation today and a clear path to a real one tomorrow.
Even in the breadth-first V1, this keeps the "foundation for a real product" promise intact at low
cost, and is the only part of the build that compounds when the mocks become real integrations.

## Sub-Features

- **Swappable Payment Seam** — async-shaped port (callback settlement, idempotency key, configurable
  decline); mock adapter in V1.
- **Swappable Dispatch Seam** — nearest-available-driver mock behind a port that a batched/ETA-based
  matcher can replace later.
- **Swappable Maps/ETA Seam** — trivial mock (hardcoded ETA) behind a port for a real maps/routing
  API later.
- **Swappable Notification Seam** — fire-and-forget by contract (accepted-for-delivery, not
  delivered), so an outbox/retry path exists later.

## Out of Scope (V1)

- **Combinatorial edge cases** (order cancellation, item out-of-stock, restaurant rejection, partial
  refunds, retries, timeouts) — beyond at most one compensation path, these explode scope without
  adding foundation value.
- **Real third-party integrations** (real card/PIX payment, real maps/Google, real SMS/push) —
  mocked behind swappable ports; real integrations are a post-PoC step.
- **Global/batched driver matching & route optimization** — V1 uses a nearest-available mock;
  ETA-driven global matching is deferred.
- **Identity, ratings/reviews, promotions/coupons, multi-restaurant cart** — not required to
  demonstrate the core journey.
- **Production concerns** (horizontal scaling, full observability stack, security hardening, PCI
  compliance, financial reconciliation) — out of scope for a mocked PoC.

## Architecture Decision Records

- [ADR-001: V1 Scope — Full Three-Sided Demonstrable Journey on Swappable Microservice Seams](adrs/adr-001.md) — V1 prioritizes a demonstrable three-sided journey while retaining swappable mock seams as a design principle; depth-first becomes the V2 stretch direction.

## Open Questions

- **Technology stack** per service (language, message broker, datastore, orchestration engine) —
  deferred to the TechSpec.
- **"Rich UI" bar** — how interactive/polished must the restaurant and driver UIs be versus just
  functional? Needs a concrete definition for the PRD.
- **Single compensation path** — should V1 include payment → no-driver → refund? The council strongly
  recommends it; the chosen breadth-first emphasis de-prioritized architecture depth. Decide in the
  PRD.
- **Authentication / role separation** across the three sides — assumed minimal or none for the mock;
  confirm.
- **Mock data seeding** — strategy and volume for mocked restaurants, menus, and drivers.
