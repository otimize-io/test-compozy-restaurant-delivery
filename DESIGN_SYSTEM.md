# Design System — Restaurant Delivery MVP

The visual language of the Angular SPA ([`src/Web`](src/Web)). It is intentionally light and food-app-like.
Per [ADR-002](.compozy/tasks/restaurant-delivery-mvp/adrs/adr-002.md) the **consumer** side is the rich,
polished surface; the **restaurant** and **driver** sides reuse the same tokens but stay functional-clean
(lists + action buttons).

Tokens and base components live in [`src/Web/src/styles.scss`](src/Web/src/styles.scss) as CSS custom
properties; component-specific styles live next to each component (`*.component.scss`). This document is the
source of truth — keep it and `styles.scss` in sync.

## Principles
- **One language, two intensities.** Same tokens everywhere; consumer screens add imagery, spacing, and
  motion, the operational screens stay dense and quick.
- **Status is the hero.** The order's progress (the 5-stage bar, status badges, live connection) is the most
  prominent, color-coded element.
- **Calm surface, decisive accent.** Neutral greys for structure; the brand red only for primary actions and
  the active step.
- **Round, soft, legible.** 12px radii, soft shadows, system font, 1.5 line-height.

## Foundations

### Color tokens
| Token | Value | Role |
| ----- | ----- | ---- |
| `--brand` | `#ea1d2c` | Primary actions, active step, brand mark, accents |
| `--brand-dark` | `#b71522` | Primary hover |
| `--ink` | `#1f2429` | Primary text |
| `--muted` | `#6b7480` | Secondary text, inactive steps, labels |
| `--line` | `#e6e9ee` | Borders, dividers, inactive connector |
| `--bg` | `#f5f6f8` | Page background, inset controls (pills) |
| `--surface` | `#ffffff` | Cards, top bar, step dots |
| `--ok` | `#1ba672` | Completed steps, "Delivered", live connection |
| `--warn` | `#e0a800` | Warnings / attention |
| `--danger` | `#d33` | Errors, refund/cancel banner |

Semantic mapping: **success = `--ok`**, **error/refund = `--danger`**, **brand/active = `--brand`**,
**neutral/pending = `--muted` on `--line`**.

### Typography
- Family: `'Segoe UI', Roboto, system-ui, -apple-system, sans-serif`; base line-height `1.5`; color `--ink`.
- Scale (observed): page title ~1.5rem/800; brand 1.15rem/800; body 1rem; labels/meta 0.85rem (uppercase +
  `letter-spacing: 0.04em` for switch labels); "delivered" 1.2rem/800.

### Shape, elevation, spacing, layout
- Radius: `--radius: 12px`; **pills** use `999px` (role switcher, cart, connection badge).
- Elevation: `--shadow: 0 1px 3px rgba(20,28,38,.08), 0 6px 18px rgba(20,28,38,.06)` — cards, top bar, active dot.
- Spacing: rem-based; cards/sections ~1.5rem padding; controls ~0.4–0.6rem × 0.85–1.1rem.
- Layout: centered `.container`, `max-width: 980px`, `box-sizing: border-box` globally.

## Components

### Buttons — `.btn`, `.btn-primary`
- Base `.btn`: surface bg, `--line` border, 12px radius, weight 600. Hover `#f0f2f5`; active `translateY(1px)`.
- `.btn-primary`: `--brand` bg, white text; hover `--brand-dark`; **disabled** `#f0a6ab` + `not-allowed`.
- Use one primary action per view (place & pay, accept, pick up, deliver).

### Card — `.card`
Surface + `--line` border + 12px radius + `--shadow`. The default container for restaurants, menu items,
queue cards, and the tracking panel.

### Role switcher (shell)
A pill **group** (`--bg` inset, `999px`, 1px `--line`) of **role chips**. Inactive chip: transparent,
`--muted`, weight 600. Active chip: `--brand` bg, white, shadow. Switching role navigates to that role's view
and sets the `X-Demo-Role` header on every gateway request. The brand mark uses `--brand` for the name.

### Cart pill (shell)
Inset `999px` pill with a count badge: brand-filled circle (`min 1.4rem`), white text — the consumer's
running cart indicator.

### 5-stage tracking bar (consumer) — the signature component
Horizontal stepper of 5 steps over a connector line (`--line`, 3px). Each step = a **dot** + label.
- **Pending** dot: surface bg, 3px `--line` border, `--muted` number.
- **Complete** dot: `--ok` fill + border, white.
- **Active** dot: `--brand` fill + border, white, `0 0 0 6px rgba(234,29,44,.15)` ring with a 1.6s `pulse`
  animation. Labels for complete/active steps switch to `--ink`.
- Stages: `1 Order placed → 2 Preparing → 3 Driver assigned/en route → 4 Out for delivery → 5 Delivered`.
  Advances **monotonically** from live `OrderStatusChanged` pushes; resyncs on (re)connect.

### Connection badge — `.conn`
Small `999px` outlined badge: default `--muted`; **`--conn--live`** turns `--ok` with a tinted border when the
SignalR hub is connected.

### Delivered / Refund states
- **Delivered**: centered, 1.2rem/800, `--ok`.
- **Refund/cancel banner** (`.refund`): left border `4px --danger`, danger-colored title — shown on the
  `NoDriverRefunded` terminal (compensation path).

### Status labels / queue grouping (restaurant & driver)
Operational views map `OrderStatus` to labels and group the restaurant queue into **New / In-Progress /
Ready**; the driver view lists assignments (driver + ETA) with **Pick up** / **Deliver** actions. Same tokens,
no imagery — speed over polish.

## States & motion
- Interactive: `:hover` (lighten), `:active` (1px nudge), `:disabled` (muted + `not-allowed`).
- Async actions are 202-style: buttons disable in-flight; lists refresh after the action and via live pushes.
- Motion is restrained — the only looping animation is the active-step `pulse`; transitions are 0.15–0.25s ease.

## Accessibility
- Brand red on white and `--ok`/`--danger` on white meet contrast for large/bold UI text; keep body text on
  `--ink`. Don't encode state with color alone — pair with the step number, label, and "Live/Delivered" text.
- Buttons are real `<button>`s (keyboard + focus); the tracking steps expose text labels, not just dots.
- Respect reduced-motion (suppress the `pulse` under `prefers-reduced-motion`) — recommended enhancement.

## Where it lives
- Tokens + base (`.btn`, `.btn-primary`, `.card`, `.container`, `.muted`): `src/Web/src/styles.scss`.
- Component styles: `src/Web/src/app/**/**.component.scss` (shell role switcher, consumer tracking bar, etc.).
- Consumer = rich; restaurant/driver = functional-clean — all from these shared tokens.
