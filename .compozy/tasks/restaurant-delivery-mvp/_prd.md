# PRD — Restaurant Delivery MVP (iFood-style, Mocked Microservices)

## Overview

A mocked, microservices-based food-delivery marketplace in the style of iFood, built as a
**proof-of-concept and foundation for a real product**. It delivers the full three-sided journey —
**consumer** (search, order, pay, track), **restaurant** (receive, accept, prepare, hand off), and
**driver** (receive assignment, pick up, deliver) — over independent services, with every external
dependency (payment, maps/ETA, notifications) behind a *swappable mock seam* so it can later be
replaced by a real provider with minimal blast radius.

The product is for the team that will evolve this skeleton into a real delivery service, and for
stakeholders who need to *see* a complete, fulfillable order move across all three roles. V1
exercises a single end-to-end happy path where **payment is confirmed before a driver is assigned**,
plus one failure path (no driver → refund). It is explicitly a mocked demo — no real money, no real
personal data.

## Goals

- Demonstrate a **complete, fulfillable order** end-to-end across all three sides, including **driver
  finding & assignment** and delivery, within the MVP.
- Prove the **swappable-seam architecture**: a mock integration can later be replaced without
  rewriting neighboring services.
- Produce a **legible demo**: one order object whose status updates across all three views in
  near-real-time.
- Establish a **.NET + Angular** foundation the team can extend toward production.
- Validate the **payment-before-dispatch** sequence, including the refund path when no driver is
  available.

## User Stories

**Consumer (primary)**

- As a consumer, I want to search and browse restaurants so that I can find somewhere to order from.
- As a consumer, I want to view a restaurant's menu and item details so that I can choose what to
  order.
- As a consumer, I want to add items to a cart and place an order so that I can buy the food I want.
- As a consumer, I want to pay before the order proceeds so that my order is confirmed.
- As a consumer, I want to track my order through clear status stages so that I know when my food
  will arrive.
- As a consumer, I want to be refunded and notified if no driver can be found so that I am not
  charged for an undeliverable order.

**Restaurant (primary)**

- As a restaurant operator, I want to receive incoming paid orders so that I know what to prepare.
- As a restaurant operator, I want to accept an order so that the customer and system know it is
  being prepared.
- As a restaurant operator, I want to mark an order ready for pickup so that a driver can collect it.

**Driver (primary)**

- As a driver, I want to receive a delivery assignment so that I know there is work to do.
- As a driver, I want to accept an assignment and see pickup/dropoff details so that I can carry it
  out.
- As a driver, I want to confirm pickup and delivery so that the order advances to completion.

**Demo presenter / future builder (secondary)**

- As a demo presenter, I want to switch between the three role views so that I can show one order
  updating live across all sides.
- As a builder toward a real product, I want each external integration behind a swappable seam so
  that mocks can be replaced by real providers without rewriting other services.

## Core Features

| #   | Feature | Priority | What it does |
| --- | ------- | -------- | ------------ |
| F1 | Restaurant Search & Discovery | Critical | Consumer searches/browses restaurants (name, cuisine, location) over mocked catalog data. |
| F2 | Menu & Item Detailing | Critical | Consumer views a restaurant's menu, item details and prices, and adds items to the cart. |
| F3 | Cart & Order Placement | Critical | Consumer reviews the cart and places the order, creating the order and starting its lifecycle. |
| F4 | Payment Before Dispatch | Critical | Consumer pays before a driver is sought; the order proceeds only after payment is confirmed. Mock payment behind a swappable seam. |
| F5 | Restaurant Order Management | Critical | Restaurant receives the paid order, **accepts** it, and marks it **ready for pickup** (New → In Progress → Ready). |
| F6 | Driver Dispatch & Assignment | Critical | After payment, the system finds and **associates a driver** (nearest-available mock); the driver accepts. *(Required in MVP.)* |
| F7 | Driver Delivery Flow | Critical | Driver views the assignment, confirms pickup, and confirms delivery, advancing the order to its terminal state. |
| F8 | Real-time Order Tracking | High | Consumer sees a **5-stage status bar**: Order placed → Preparing → Driver assigned/en route → Out for delivery → Delivered, driven by events from the other roles. |
| F9 | No-Driver Compensation (Refund) | High | If no driver can be assigned after payment, the order is refunded and reaches a terminal cancelled/refunded state; the consumer is notified. |
| F10 | Role Switcher | Medium | Pre-seeded identities with a switch between consumer/restaurant/driver views; no login. |

## User Experience

**Personas & goals:** consumer (order food, know when it arrives), restaurant (process incoming
orders), driver (complete deliveries), demo presenter (show the synchronized journey).

**Primary flow (happy path):**

1. Consumer opens the app (consumer view via role switcher), searches/browses, opens a restaurant,
   adds items, places the order.
2. Consumer pays; the order is confirmed; the tracking bar shows **Order placed**.
3. Restaurant view receives the order, **accepts** it → tracking advances to **Preparing**;
   restaurant marks **ready for pickup**.
4. System assigns a driver; **Driver assigned/en route** appears; driver view shows the assignment
   and accepts.
5. Driver confirms pickup → **Out for delivery**; driver confirms delivery → **Delivered**. Terminal
   state across all views.

**Compensation flow:** after payment, if no driver is available, the consumer sees a
**cancelled/refunded** status and a notification; the order terminates consistently.

**UI/UX considerations:** the **consumer side is rich** (search, menu, cart, checkout, animated
5-stage tracking bar); **restaurant and driver are functional-clean** (order list + action buttons
that advance the shared order). The demo's credibility comes from one action on one side visibly
updating the others in near-real-time. Onboarding is the role switcher over pre-seeded restaurants,
menus, and drivers — no signup.

## High-Level Technical Constraints

- **Mandated stack:** **.NET** for the backend microservices; **Angular** for the web frontend
  (consumer/restaurant/driver views with the role switcher). *(ADR-003.)*
- **Swappable seams:** payment, maps/ETA, and notifications must sit behind adapters so a real
  provider can replace a mock without rewriting other services. Payment and notification seams should
  be **async-shaped** (settlement via callback, idempotency key, declinable) even while mocked.
- **Sequencing constraint:** payment must complete **before** driver assignment.
- **Privacy/safety:** mocked only — **no real money and no real personal data**; pre-seeded demo
  data.
- **User-perceived performance:** a role action should reflect in the other views in near-real-time
  (target ~2 s).

*(Deeper design — service decomposition, messaging, orchestration engine, persistence, Angular
project structure — belongs to the TechSpec.)*

## Non-Goals (Out of Scope)

- **Combinatorial edge cases** beyond the single no-driver refund (order cancellation by user, item
  out-of-stock, restaurant rejection, partial refunds, retries, timeouts).
- **Real third-party integrations** (real card/PIX payment, real maps/Google, real SMS/push) —
  mocked behind swappable seams; real integrations are post-MVP.
- **Global/batched driver matching & route optimization** — V1 uses nearest-available mock.
- **Identity & authentication** — replaced by the role switcher; no accounts.
- **Ratings/reviews, promotions/coupons, multi-restaurant cart, scheduling** — not needed for the
  core journey.
- **Production concerns** — horizontal scaling, full observability, security hardening, PCI
  compliance, financial reconciliation.

## Phased Rollout Plan

### MVP (Phase 1)

- **Included:** F1–F10 — the complete three-sided happy path **through driver assignment and
  delivery**, plus the no-driver refund (F9), the 5-stage tracking bar (F8), and the role switcher
  (F10), on .NET + Angular with swappable mock seams.
- **Success criteria to proceed:** a consumer completes an order end-to-end with all three role views
  advancing one shared order live; the no-driver path refunds and terminates correctly; the full
  stack boots with one command.

### Phase 2

- **Added:** prove swappability by replacing one mock seam (payment) with a stub-real adapter; add a
  small set of real edge cases (restaurant reject, item unavailable).
- **Success criteria:** swapping the payment seam touches only that adapter (zero neighbor changes);
  the added edge cases resolve to consistent states.

### Phase 3

- **Added:** real integrations (payment/PIX, maps/ETA, notifications), ETA-driven/global driver
  matching, and basic identity for the three roles.
- **Long-term success:** at least one real integration runs end-to-end; the foundation is ready for
  production planning.

## Success Metrics

| Metric | Target | How to measure |
| ------ | ------ | -------------- |
| End-to-end happy-path completion | 100% of demo runs complete search → pay → accept → assign → deliver without manual data fixes | Scripted/automated end-to-end run |
| Three-sided journey functional | All 3 role views can complete their part of one order | Walkthrough per role |
| Cross-view update latency | A role action reflects in other views in ≤ ~2 s | Timed observation during the demo |
| Compensation correctness | 100% of injected no-driver cases refund and terminate consistently | Fault-injection run |
| Mock swappability (Phase 2 gate) | 0 cross-service code changes to swap a mock for a stub-real adapter | Count files/services touched |
| One-command bootstrap | Full stack up with a single command in < 5 min | Time the bootstrap |

## Risks and Mitigations

- **Demo not legible / underwhelming** → lead with the 5-stage tracking bar and synchronized views;
  rehearse the "one order, three views" narrative.
- **Scope creep across three sides + compensation** → enforce YAGNI: happy path + exactly one
  compensation; everything else is a Non-Goal.
- **Competitive/positioning risk** (features are commodity vs iFood) → position explicitly as a
  *foundation/PoC*, not a market entrant; the value is the swappable architecture, not feature parity.
- **Mocks diverging from real-provider shapes** (rework when integrating later) → keep
  payment/notification seams async-shaped now.
- **Misread as production-ready** → label clearly as a mocked PoC; no real money or PII; document the
  swap-then-integrate path.

## Architecture Decision Records

- [ADR-001: V1 Scope — Full Three-Sided Demonstrable Journey on Swappable Microservice Seams](adrs/adr-001.md) — V1 prioritizes a demonstrable three-sided journey while retaining swappable mock seams.
- [ADR-002: V1 Product Approach — Fulfillable-Order Layering, Full Three-Sided MVP](adrs/adr-002.md) — Approach A sequencing; driver assignment + compensation are in the MVP; role switcher; consumer-rich UI.
- [ADR-003: Technology Stack — .NET Backend Microservices + Angular Frontend](adrs/adr-003.md) — mandated .NET + Angular stack as a high-level constraint.

## Open Questions

- **Consumer visual design** — the exact look/branding for the "rich" consumer UI needs design input
  (out of scope to invent here).
- **Phase 2 swap target** — confirm payment as the first seam to prove swappability (recommended).
- **Mock data** — volume/realism of seeded restaurants, menus, and drivers.
- **Notification channels** — which to simulate in V1 (in-app status only vs. mocked email/SMS).
- **Angular delivery shape** — one app with a role switcher vs. separate builds per role — deferred
  to the TechSpec.
