# Restaurant Delivery MVP — Task List

## Tasks

| # | Title | Status | Complexity | Dependencies |
|---|-------|--------|------------|--------------|
| 01 | Solution scaffolding & infrastructure compose | completed | medium | — |
| 02 | Shared Contracts library (events/commands) | completed | medium | task_01 |
| 03 | Shared Platform library (messaging, logging, correlation, health) | completed | medium | task_01, task_02 |
| 04 | Catalog service (MongoDB) + seed | completed | medium | task_03 |
| 05 | Search service (Elasticsearch) | completed | medium | task_03, task_04 |
| 06 | Order service & saga skeleton (PostgreSQL + outbox) | completed | high | task_03 |
| 07 | Payment service (async-shaped mock + swap-contract test) | completed | high | task_03 |
| 08 | Restaurant order flow (accept/ready + saga transitions) | completed | medium | task_06 |
| 09 | Dispatch service (nearest-available mock, Redis) | completed | medium | task_03 |
| 10 | Driver delivery flow (pickup/deliver + saga transitions) | completed | medium | task_06, task_09 |
| 11 | Compensation path (no-driver → refund) + compensation test | completed | high | task_06, task_07, task_09 |
| 12 | Tracking service (Redis 5-stage projection) | completed | medium | task_06 |
| 13 | Notification service (fire-and-forget seam) | completed | low | task_03 |
| 14 | API Gateway/BFF + SignalR hub + E2E happy-path test | completed | high | task_04, task_05, task_06, task_07, task_08, task_10, task_12 |
| 15 | Angular shell + role switcher + consumer view | completed | high | task_14 |
| 16 | Angular restaurant & driver views | pending | medium | task_15 |
