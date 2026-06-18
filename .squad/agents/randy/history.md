# Project Context

- **Owner:** Zack Way
- **Project:** Unified billing observability portal for tracking metrics from multiple foundry projects in a tenant
- **Stack:** .NET 10 Minimal APIs, Azure Resource endpoints, React (dashboard/reporting), Aspire (dev orchestration)
- **Created:** 2026-06-18

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
📌 Team update (2026-06-18T15:30:12-04:00): Backend seams are standardized around `/api/billing` and `/api/projects`, with Azure SDK and CORS registration isolated in `Infrastructure` and `Services`. — decided by Kyle
📌 Team update (2026-06-18T15:30:12-04:00): The frontend lives in `src/web`, uses a typed `/api` client plus hash navigation, and reaches the API through AppHost service discovery plus the Vite proxy instead of a dedicated `VITE_API_URL`. — decided by Stan, Zack Way (via Copilot), Tweek
📌 Team update (2026-06-18T15:30:12-04:00): The initial test baseline uses a Development-mode `WebApplicationFactory` with HTTPS health probes and reflection-based smoke tests so coverage can land before service implementations fully settle. — decided by Butters
