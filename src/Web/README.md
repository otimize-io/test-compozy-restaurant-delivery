# Web — Restaurant Delivery (consumer SPA)

Single Angular app (Angular 20, standalone components) for the mocked food-delivery PoC. It implements
the **consumer journey** (search → restaurant/menu → cart → checkout/pay → live 5-stage tracking) and a
**role switcher** (consumer / restaurant / driver) that stamps the `X-Demo-Role` header on every gateway
request. Restaurant/driver views are placeholders, filled in by task_16. The app talks **only to the API
Gateway / BFF** (REST + the SignalR hub at `${apiBase}/hubs/orders`).

## Setup

```bash
npm install
```

## Gateway base URL

Configured in `src/environments/environment.ts` (production) and `src/environments/environment.development.ts`
(development) as `apiBase` — default `http://localhost:5000`. Change it per deployment.

## Development server

```bash
npm start          # ng serve, http://localhost:4200
```

## Production build

```bash
npm run build      # ng build -> dist/web, optimized
```

## Unit tests (Jest)

This project uses **Jest** (browser-free) via `jest-preset-angular`. Karma/Jasmine were removed.

```bash
npm test           # run all tests
npm run test:coverage   # run with coverage (target >= 80% lines)
```

## Structure

- `src/app/shell/` — app shell, top bar + role switcher, restaurant/driver placeholders.
- `src/app/consumer/` — search, restaurant/menu, cart, track screens + cart/checkout services.
- `src/app/core/` — `ApiService` (single gateway HTTP client), `RoleService` + `roleInterceptor`
  (`X-Demo-Role`), shared models.
- `src/app/core/signalr/` — `HubConnectionFactory` + `OrderTrackingStore` (live 5-stage bar, reconnect
  resync). Reusable by the restaurant/driver views (task_16).
