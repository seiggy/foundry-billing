---
title: Development Guide
---

# Development guide

[Back to docs home](index.md)

## Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker Desktop or another local container runtime
- Aspire CLI / .NET Aspire workload
- Azure CLI logged into the tenant you want to inspect

## Local setup

### 1. Clone the repository

```bash
git clone https://github.com/seiggy/foundry-billing.git
cd foundry-billing
```

### 2. Configure local secrets

The API project already has a `UserSecretsId`, so set secrets directly in `src/FoundryBilling.Api`:

```bash
cd src/FoundryBilling.Api
dotnet user-secrets set "Azure:SubscriptionId" "<subscription-id>"
dotnet user-secrets set "Azure:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<entra-app-client-id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<entra-app-client-secret>"
cd ../..
```

Your local Entra app registration must allow `https://localhost:7220/auth/callback`.

### 3. Restore frontend dependencies if needed

The AppHost runs the Vite development server from `src/web`. If this checkout does not already have frontend packages restored:

```bash
cd src/web
npm install
cd ../..
```

### 4. Run the app with Aspire

```bash
aspire run
```

What Aspire starts:

- PostgreSQL with a persistent named volume
- pgAdmin
- `FoundryBilling.Api`
- Vite dev server for `src/web`

Open the Aspire dashboard, then open the web endpoint and sign in.

### 5. Trigger an initial sync

Use the **Sync** page and click **Run Sync Now**. The worker also performs one sync cycle immediately on startup, but manual trigger gives you a visible run ID and progress row.

## Running tests and validation

### Backend tests

```bash
dotnet test tests/FoundryBilling.Api.Tests/FoundryBilling.Api.Tests.csproj
```

Current automated backend coverage includes:

- health endpoint behavior
- billing model serialization expectations
- service registration contracts
- sync discovery, metrics, and worker behavior

### Frontend validation

```bash
cd src/web
npm run lint
npm run build
```

The production Docker image also runs `npm run build` before publishing the API image.

## Project structure

```text
foundry-billing/
├── azure.yaml                         # azd entry point; Terraform-backed infra
├── DEPLOY.md                          # short deployment checklist
├── infra/                             # Terraform for Azure resources
├── src/
│   ├── FoundryBilling.Api/            # .NET 10 Minimal API, EF Core, worker, Dockerfile
│   ├── FoundryBilling.AppHost/        # Aspire local orchestration
│   ├── FoundryBilling.ServiceDefaults/ # health checks, service discovery, OpenTelemetry
│   └── web/                           # React/Vite dashboard
└── tests/
    └── FoundryBilling.Api.Tests/      # xUnit backend tests
```

`src/FoundryBilling.Api` is further organized by responsibility:

- `Endpoints/` — Minimal API route modules
- `Models/` — response DTOs and sync discovery records
- `Data/Entities/` — EF Core entities
- `Data/` — `BillingDbContext`, migration helpers, migrations
- `Infrastructure/` — options binding and Azure client registration
- `Services/` — query services and PTU calculator
- `Services/Sync/` — Azure discovery and metrics query adapters
- `Workers/` — background sync worker

## Adding a new endpoint or feature

A typical backend feature addition follows this sequence:

1. **Add or update persistence**
   - Extend an entity in `Data/Entities/` or add a new one.
   - Update `BillingDbContext` configuration if you need indexes, relationships, or table mapping changes.
2. **Add a response model**
   - Create or update a DTO record in `Models/`.
3. **Add service logic**
   - Put query or orchestration logic in `Services/` or `Services/Sync/`.
4. **Expose the endpoint**
   - Add a route module under `Endpoints/`.
   - Register it from `EndpointRouteBuilderExtensions.MapFoundryBillingEndpoints()`.
   - If it belongs under `/api`, it automatically inherits `RequireAuthorization()`.
5. **Update the frontend**
   - Add or update TypeScript interfaces in `src/web/src/types/billing.ts`.
   - Add the client call in `src/web/src/api/client.ts`.
   - Render it in the appropriate page or component.
6. **Add tests**
   - Prefer focused tests in `tests/FoundryBilling.Api.Tests` for service behavior, worker behavior, or endpoint integration.
7. **Update docs**
   - Keep `README.md` and the relevant page in `docs/` aligned with the code.

## EF Core migrations

The API applies migrations on startup, but schema changes should still be captured explicitly.

### Create a migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/FoundryBilling.Api/FoundryBilling.Api.csproj \
  --startup-project src/FoundryBilling.Api/FoundryBilling.Api.csproj
```

If `dotnet ef` is not installed on your machine, install it once as a global .NET tool.

### Apply migrations manually

```bash
dotnet ef database update \
  --project src/FoundryBilling.Api/FoundryBilling.Api.csproj \
  --startup-project src/FoundryBilling.Api/FoundryBilling.Api.csproj
```

### Current migration set

The repository currently includes these migration stages:

- `InitialCreate`
- `AddSyncRuns`
- `AddFoundryAgents`

## Local development notes

- The worker writes hourly usage slices only; `DailyUsageRollups` is not populated today.
- The sync window is limited by `Sync:MetricLookbackHours`, so a fresh environment will not immediately have 30/60/90 days of historical data.
- When running the API outside Aspire, the local launch settings expose HTTPS on `https://localhost:7220` and HTTP on `http://localhost:5020`.
- In production, the frontend is served from `wwwroot`; in local development, Vite serves it separately through the AppHost.
- When the API runs in Development, ASP.NET Core also exposes the generated OpenAPI document at `/openapi/v1.json`.
