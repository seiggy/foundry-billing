# Project Context

- **Owner:** Zack Way
- **Project:** Unified billing observability portal for tracking metrics from multiple foundry projects in a tenant
- **Stack:** .NET 10 Minimal APIs, Azure Resource endpoints, React (dashboard/reporting), Aspire (dev orchestration)
- **Created:** 2026-06-18

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
📌 Team update (2026-06-18T15:30:12-04:00): Repo structure is anchored by `foundry-billing.slnx`, with .NET projects under `src/` and `tests/`, the frontend under `src/web/`, and local orchestration owned by `FoundryBilling.AppHost` plus `FoundryBilling.ServiceDefaults`. — decided by Randy
📌 Team update (2026-06-18T15:30:12-04:00): The frontend contract is a typed `/api` client with lightweight hash navigation, and frontend-to-API routing should flow through AppHost service discovery plus the Vite proxy rather than a dedicated `VITE_API_URL`. — decided by Stan, Zack Way (via Copilot), Tweek
📌 Team update (2026-06-18T15:30:12-04:00): Test coverage expects a Development-mode `WebApplicationFactory` with HTTPS health probes and reflection-based smoke tests while backend implementations are still evolving. — decided by Butters
