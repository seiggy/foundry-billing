# Project Context

- **Owner:** Zack Way
- **Project:** Unified billing observability portal for tracking metrics from multiple foundry projects in a tenant
- **Stack:** .NET 10 Minimal APIs, Azure Resource endpoints, React (dashboard/reporting), Aspire (dev orchestration)
- **Created:** 2026-06-18

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
📌 Team update (2026-06-18T15:30:12-04:00): Repo structure is anchored by `foundry-billing.slnx`, with .NET projects under `src/` and `tests/`, the frontend under `src/web/`, and local orchestration owned by `FoundryBilling.AppHost` plus `FoundryBilling.ServiceDefaults`. — decided by Randy
📌 Team update (2026-06-18T15:30:12-04:00): Backend seams are standardized around `/api/billing` and `/api/projects`, with Azure SDK and CORS registration isolated in `Infrastructure` and `Services`. — decided by Kyle
📌 Team update (2026-06-18T15:30:12-04:00): Frontend-to-API routing flows through AppHost service discovery plus the Vite proxy, and the frontend contract stays behind a typed `/api` client in `src/web/`. — decided by Stan, Zack Way (via Copilot), Tweek
