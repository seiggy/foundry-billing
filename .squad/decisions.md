# Squad Decisions

## Active Decisions

### 2026-06-18T15:30:12-04:00: Use Aspire AppHost as the local API/frontend topology source of truth (consolidated)
**By:** Zack Way (via Copilot), Tweek
**What:** Keep the .NET API as an Aspire project resource, add the Vite frontend as an npm app on port 5173, and route frontend API traffic through Aspire service discovery plus the Vite `/api` proxy instead of a dedicated `VITE_API_URL` environment variable. Add a multi-stage Dockerfile for the API and a root `.dockerignore` for .NET and Node build artifacts.
**Why:** This preserves Aspire as the single source of truth for local development, keeps the topology reproducible, avoids redundant frontend environment configuration, and ensures the API can be built into a clean container image without shipping repo noise.

### 2026-06-18T15:31:25-04:00: Establish solution-first .NET 10 structure with Aspire orchestration
**By:** Randy
**What:** Use a root `foundry-billing.slnx` to organize backend and test projects under `src/` and `tests/`, keep the React frontend in `src/web/`, and wire local development orchestration through `FoundryBilling.AppHost` + `FoundryBilling.ServiceDefaults`.
**Why:** This keeps the repo aligned with Zack's `.slnx` preference, preserves a clean separation between API, orchestration, shared service defaults, and tests, and gives the team a stable foundation for adding the React app and Azure billing integrations next.

### 2026-06-18T15:36:58-04:00: Establish minimal API seams for billing observability
**By:** Kyle
**What:** Replace the template weather endpoint with `/api/billing` and `/api/projects` route groups, organize backend code into `Endpoints`, `Models`, `Services`, and `Infrastructure`, and centralize Azure credential, ARM client, and frontend CORS registration in DI.
**Why:** This gives the team a stable API backbone for the billing portal, keeps Azure SDK concerns out of `Program.cs`, and leaves clear extension points for real cost-management queries once the Azure integration work starts.

### 2026-06-18T15:36:59-04:00: Bootstrap the frontend with typed contracts and lightweight hash navigation
**By:** Stan
**What:** Scaffold `src/web/` with Vite + React + TypeScript, define shared billing contracts and a single `/api` client module, and use lightweight hash-based navigation for the initial Dashboard, Projects, and Reports views instead of adding a routing or UI library yet.
**Why:** This keeps the frontend minimal while giving the team a stable folder structure, a typed API boundary that works with Aspire's proxy setup, and enough navigation scaffolding to grow the portal without carrying extra dependencies this early.

### 2026-06-18T15:30:12-04:00: Use a Development WebApplicationFactory baseline for early API coverage
**By:** Butters
**What:** Use a `WebApplicationFactory` fixture that forces the API into the `Development` environment and issues HTTPS requests so Aspire's `/health` and `/alive` endpoints stay testable under `UseHttpsRedirection()`. Keep early service and model tests reflection-based until the concrete service implementations stabilize so the test project can compile and provide immediate smoke coverage without blocking parallel API work.
**Why:** This gives the team passing smoke coverage from day one while backend types and Azure integration seams are still evolving.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
