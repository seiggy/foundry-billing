---
title: Architecture
---

# Architecture

[Back to docs home](index.md)

## System components

### Browser SPA (`src/web`)

- React 19 + TypeScript 6 + Vite 8 single-page app.
- Uses hash routes for the six top-level views: dashboard, projects, agents, analytics, PTU calculator, and sync.
- Calls the backend with same-origin requests and `credentials: 'same-origin'`.
- Uses `/auth/me` as a session probe and redirects to `/auth/login` on `401` responses.

### API (`src/FoundryBilling.Api`)

- ASP.NET Core Minimal API targeting `.NET 10`.
- Applies `RequireAuthorization()` to the `/api` route group.
- Hosts auth endpoints under `/auth`, health probes under `/health` and `/alive`, static files, and the SPA fallback.
- Runs EF Core migrations on startup.

### Background sync worker (`MetricsSyncWorker`)

- Singleton `BackgroundService` and `ISyncTriggerService` implementation.
- Starts one sync cycle immediately on process startup.
- Schedules future runs with `Sync:IntervalMinutes`.
- Accepts manual sync requests through a bounded channel so queued runs do not pile up.

### Services

- **`BillingService`** — raw usage metrics and summary queries.
- **`ProjectService`** — project list and single-project lookup.
- **`PtuCalculatorService`** — PTU sizing and cost recommendation logic.
- **`FoundryDiscoveryService`** — Azure ARM and Azure AI Projects discovery.
- **`MetricsSyncService`** — Azure Monitor metric queries and bucket assembly.

### Persistence

- PostgreSQL database accessed through `BillingDbContext`.
- EF Core stores hubs, projects, deployments, agents, hourly metric slices, daily rollup rows, and sync runs.
- `DailyUsageRollups` is part of the schema but is not populated by the current worker implementation.

### Azure dependencies

- **ARM / Resource Manager** — lists AI Services accounts, projects, and deployments.
- **Azure Monitor Metrics** — reads hourly token metrics from `Microsoft.CognitiveServices/accounts`.
- **Azure AI Projects SDK** — discovers agents per project through `https://{hub}.services.ai.azure.com/api/projects/{project}`.

## End-to-end data flow

```text
┌─────────────────────────────────────────────────────────────────────┐
│ Browser                                                            │
│  React SPA                                                         │
│  • GET /auth/me                                                    │
│  • GET /api/* and POST /api/* with cookie session                  │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ FoundryBilling.Api                                                 │
│  Program.cs                                                        │
│  • HTTPS redirection                                               │
│  • CORS policy                                                     │
│  • Authentication + authorization                                  │
│  • /auth routes                                                    │
│  • /api routes                                                     │
│  • static files + SPA fallback                                     │
└───────────────┬───────────────────────────────┬─────────────────────┘
                │                               │
                │                               │ background schedule / trigger
                ▼                               ▼
┌──────────────────────────────┐     ┌────────────────────────────────┐
│ PostgreSQL                   │     │ MetricsSyncWorker              │
│  FoundryHubs                 │     │  1. Discover hubs              │
│  FoundryProjects             │     │  2. Discover projects          │
│  FoundryAgents               │     │  3. Discover deployments       │
│  ModelDeployments            │     │  4. Discover agents            │
│  UsageMetricSlices           │     │  5. Query Azure Monitor        │
│  DailyUsageRollups           │     │  6. Insert new slices          │
│  SyncRuns                    │     │  7. Record sync status         │
└──────────────────────────────┘     └──────────────┬─────────────────┘
                                                    │
                                                    ▼
                               ┌─────────────────────────────────────┐
                               │ Azure                               │
                               │  • Cognitive Services accounts      │
                               │  • Foundry projects                 │
                               │  • Foundry deployments              │
                               │  • Azure Monitor token metrics      │
                               │  • Azure AI Projects agents         │
                               └─────────────────────────────────────┘
```

## Database schema

### Entities

| Entity | Purpose | Key fields |
|---|---|---|
| `FoundryHub` | One discovered Azure AI Foundry hub / AI Services account | `Id`, `AzureResourceId`, `Name`, `SubscriptionId`, `ResourceGroup`, `Region`, `LastSyncedAt` |
| `FoundryProject` | One project under a hub | `Id`, `HubId`, `AzureResourceId`, `Name`, `LastSyncedAt` |
| `FoundryAgent` | One agent discovered from the AI Projects SDK | `Id`, `ProjectId`, `AgentId`, `Name`, `Description`, `ModelName`, `Kind`, `CreatedAt`, `LastSyncedAt` |
| `ModelDeployment` | One model deployment under a hub | `Id`, `HubId`, `AzureResourceId`, `DeploymentName`, `ModelName`, `ModelVersion`, `SkuName`, `Capacity`, `LastSyncedAt` |
| `UsageMetricSlice` | One hourly token usage slice for a deployment | `Id`, `DeploymentId`, `Timestamp`, `IntervalMinutes`, `PromptTokens`, `CompletionTokens`, `TotalTokens` |
| `DailyUsageRollup` | One per-day aggregate row for a deployment | `Id`, `DeploymentId`, `Date`, `PromptTokens`, `CompletionTokens`, `TotalTokens` |
| `SyncRun` | One worker execution record | `Id`, `StartedAt`, `CompletedAt`, `Status`, `ErrorMessage`, `HubsDiscovered`, `ProjectsDiscovered`, `DeploymentsDiscovered`, `AgentsDiscovered`, `UsageSlicesInserted` |

### Relationships

```text
FoundryHub (1) ───< FoundryProject (many) ───< FoundryAgent (many)
     │
     └────< ModelDeployment (many) ───< UsageMetricSlice (many)
                                  └───< DailyUsageRollup (many)

SyncRun is standalone.
```

### Indexes and constraints

- `FoundryHub.AzureResourceId` — unique
- `ModelDeployment.AzureResourceId` — unique
- `FoundryAgent (ProjectId, AgentId)` — unique composite key
- `UsageMetricSlice (Timestamp, DeploymentId)` — indexed for time-window queries
- `DailyUsageRollup (Date, DeploymentId)` — indexed for day-based queries
- `SyncRun.StartedAt DESC` — indexed for newest-first history lookups

### Cascade behavior

- Deleting a hub deletes its projects and deployments.
- Deleting a project deletes its agents.
- Deleting a deployment deletes its usage slices and daily rollups.

## Sync pipeline

### 1. Discovery

`MetricsSyncWorker` creates a `SyncRun` row, then asks `FoundryDiscoveryService` to:

1. Discover hubs from the configured subscription.
2. Discover projects for each hub.
3. Discover deployments for each hub.
4. Discover agents for each project.

Discovery is resilient to Azure credential, authentication, and common authorization failures. Those failures are logged and returned as empty result sets instead of crashing the entire application.

### 2. Upsert inventory

The worker:

- Inserts new hubs, projects, deployments, and agents.
- Updates name, location, capacity, model, and last-sync metadata on existing rows.
- Saves after hub inventory updates and again after project/deployment/agent updates.

### 3. Pull metrics

For each hub, `MetricsSyncService` queries Azure Monitor with these rules:

- Namespace: `Microsoft.CognitiveServices/accounts`
- Metrics: `ProcessedPromptTokens`, `GeneratedTokens`, `TokenTransaction`
- Granularity: 1 hour
- Filter: `ModelDeploymentName eq '*'`
- Time window: `syncTimestamp - MetricLookbackHours` through `syncTimestamp`

### 4. Store slices

- Metric buckets are matched back to `ModelDeployment` by deployment name.
- Existing `(DeploymentId, Timestamp)` slice keys in the same window are loaded first.
- Only new hourly slices are inserted, which makes reruns for the same window idempotent.

### 5. Roll up and expose

- The current worker only inserts `UsageMetricSlices`.
- `DailyUsageRollups` exists in the schema but is not populated yet.
- Analytics endpoints compute 30/60/90 day rollups directly from `UsageMetricSlices` at request time.

### 6. Record run status

- Successful runs end as `Completed`.
- Runs with one or more failed hubs end as `Failed` and store a summary error message.
- Canceled or unexpected failures also finalize the `SyncRun` as `Failed`.

## Authentication flow (BFF pattern)

```text
Browser                    API                         Entra ID
   │                        │                              │
   │ GET /auth/login        │                              │
   ├───────────────────────▶│ Challenge OIDC              │
   │                        ├─────────────────────────────▶│
   │                        │                              │ sign-in
   │◀───────────────────────┤ redirect with auth cookie    │
   │                        │                              │
   │ GET /auth/me           │                              │
   ├───────────────────────▶│ reads server-side cookie     │
   │◀───────────────────────┤ 200 { name, email }          │
   │                        │                              │
   │ GET /api/...           │                              │
   ├───────────────────────▶│ cookie auth + authorization  │
   │◀───────────────────────┤ JSON response                │
```

Key points:

- The `/api` route group requires an authenticated cookie session.
- `/auth/login` and `/auth/logout` initiate and terminate the OIDC flow.
- `/auth/me` is intentionally public at the route level so the SPA can get `401` instead of a redirect during startup.
- For `/api/*` and `/auth/me`, unauthenticated requests return `401` and access-denied requests return `403`.
- For browser navigation to non-API routes, the cookie middleware redirects to `/auth/login`.

## API endpoint reference table

For example payloads and field-level schemas, see [API reference](api-reference.md).

| Method | Path | Auth | Parameters / body | Response |
|---|---|---|---|---|
| `GET` | `/auth/login` | Public | None | Redirect to `/` if already signed in; otherwise starts OIDC challenge |
| `GET` | `/auth/logout` | Public | None | Clears cookie + OIDC session, then redirects to `/` |
| `GET` | `/auth/me` | Session probe | None | `200 { name, email }` or `401` |
| `GET` | `/api/billing/metrics` | Required | `startDate?`, `endDate?` (`yyyy-MM-dd`) | `200 BillingMetricResponse[]` |
| `GET` | `/api/billing/summary` | Required | `startDate?`, `endDate?` (`yyyy-MM-dd`) | `200 UsageSummaryResponse` |
| `GET` | `/api/hubs` | Required | None | `200 FoundryHubResponse[]` |
| `GET` | `/api/projects` | Required | None | `200 FoundryProjectResponse[]` |
| `GET` | `/api/projects/{projectId}` | Required | Route `projectId: Guid` | `200 FoundryProjectResponse` or `404` |
| `GET` | `/api/deployments` | Required | `hubId?: Guid` | `200 DeploymentResponse[]` |
| `GET` | `/api/agents` | Required | `projectId?: Guid`, `hubId?: Guid` | `200 AgentResponse[]` |
| `GET` | `/api/analytics/usage` | Required | `days?: 30|60|90` | `200 UsageAnalyticsResponse` |
| `GET` | `/api/analytics/tpm` | Required | `days?: 30|60|90` | `200 TpmAnalyticsResponse` |
| `POST` | `/api/analytics/ptu-recommendation` | Required | `PtuCalculationRequest` JSON body | `200 PtuRecommendationResponse` |
| `POST` | `/api/sync/trigger` | Required | None | `202 SyncTriggerAcceptedResponse` + `Location: /api/sync/history` |
| `GET` | `/api/sync/status` | Required | None | `200 SyncStatusResponse` |
| `GET` | `/api/sync/history` | Required | None | `200 SyncHistoryResponse` |
| `GET` | `/health` | Public | None | Health probe for readiness |
| `GET` | `/alive` | Public | None | Health probe for liveness |
| `GET` | `/openapi/v1.json` | Development only | None | Generated OpenAPI document |

## Notes that affect operations

- The first sync runs automatically when the process starts.
- `MetricLookbackHours` limits how far back each sync reads; the app does not backfill the full 30/60/90 day window in one run.
- Health endpoints are intentionally unauthenticated because Container Apps probes use them.
- OpenAPI is mapped only in development environments.
