---
title: Configuration
---

# Configuration

[Back to docs home](index.md)

## Configuration sources

Foundry Billing uses standard ASP.NET Core configuration precedence:

1. `appsettings.json`
2. `appsettings.Development.json`
3. user secrets (`src/FoundryBilling.Api`)
4. environment variables
5. Aspire-injected connection strings and service endpoints during local orchestration

The backend binds three options classes directly:

- `AzureBillingOptions` from `Azure`
- `SyncOptions` from `Sync`
- `AuthOptions` from `AzureAd`

## `appsettings.json`

```json
{
  "Azure": {
    "ManagementBaseUrl": "https://management.azure.com/"
  },
  "Sync": {
    "IntervalMinutes": 60,
    "MetricLookbackHours": 2
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "",
    "ClientId": "",
    "CallbackPath": "/auth/callback"
  },
  "Cors": {
    "AllowedOrigins": []
  }
}
```

## Backend options

### `Azure`

| Key | Required | Default | Description |
|---|---|---|---|
| `Azure:SubscriptionId` | Yes for sync | empty | Subscription scanned for AI Foundry hubs |
| `Azure:TenantId` | Recommended | empty | Pins `DefaultAzureCredential` to a tenant |
| `Azure:ManagementBaseUrl` | No | `https://management.azure.com/` | Base URL used by the registered ARM `HttpClient` |

### `Sync`

| Key | Required | Default | Description |
|---|---|---|---|
| `Sync:IntervalMinutes` | No | `60` | Wait time between automatic worker cycles |
| `Sync:MetricLookbackHours` | No | `2` | Metric query lookback window per cycle |

`MetricLookbackHours` is operationally important: the worker only pulls slices for that recent window. It does not fill historical gaps automatically.

### `AzureAd`

| Key | Required | Default | Description |
|---|---|---|---|
| `AzureAd:Instance` | No | `https://login.microsoftonline.com/` | OIDC authority base URL |
| `AzureAd:TenantId` | Yes | empty | Entra tenant for sign-in |
| `AzureAd:ClientId` | Yes | empty | App registration client ID |
| `AzureAd:ClientSecret` | Yes | empty | App registration secret; do not store in source control |
| `AzureAd:CallbackPath` | No | `/auth/callback` | Local and hosted redirect callback path |

### `Cors`

| Key | Required | Default | Description |
|---|---|---|---|
| `Cors:AllowedOrigins` | No | empty array | Explicit CORS allow-list |

If `Cors:AllowedOrigins` is empty, the API falls back to:

- `http://localhost:3000`
- `http://localhost:5173`

## Environment variables

### Required or commonly used in Azure Container Apps

| Variable | Example | Why it exists |
|---|---|---|
| `ConnectionStrings__foundry-billing-db` | secret reference | PostgreSQL connection string injected from Key Vault |
| `Azure__SubscriptionId` | subscription GUID | Enables hub discovery in the target subscription |
| `Azure__TenantId` | tenant GUID | Azure credential tenant pinning |
| `AZURE_CLIENT_ID` | managed identity client ID | Selects the user-assigned managed identity for `DefaultAzureCredential` |
| `AzureAd__TenantId` | tenant GUID | OIDC configuration for the BFF |
| `AzureAd__ClientId` | app registration GUID | OIDC client ID |
| `AzureAd__ClientSecret` | `secretref:entra-client-secret` | OIDC client secret |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Trust reverse-proxy forwarded headers in Container Apps |
| `HTTP_PORT` | `8080` | Container listen port |

### Optional telemetry variable

| Variable | Effect |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | When present, service-default OpenTelemetry exporters send traces and metrics to the configured OTLP endpoint |

## User secrets for local development

Configure secrets in `src/FoundryBilling.Api`:

```bash
dotnet user-secrets set "Azure:SubscriptionId" "<subscription-id>"
dotnet user-secrets set "Azure:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<entra-app-client-id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<entra-app-client-secret>"
```

Use an app registration that allows the local callback URI `https://localhost:7220/auth/callback`.

## Aspire AppHost topology

`src/FoundryBilling.AppHost/AppHost.cs` defines the local topology:

```text
DistributedApplication
├── postgres
│   ├── pgAdmin enabled
│   ├── persistent data volume: foundry-billing-pgdata
│   └── database: foundry-billing-db
├── api
│   ├── external HTTP endpoints
│   ├── reference to foundry-billing-db
│   └── waits for the database to be healthy
└── web
    ├── npm app in ../web using script: dev
    ├── target port 5173
    ├── external HTTP endpoints
    ├── reference to api
    └── NODE_OPTIONS=--max-http-header-size=32768
```

Local behavior to know:

- The API gets `ConnectionStrings__foundry-billing-db` from the database reference.
- The frontend is not served from `wwwroot` during local development; Aspire runs the Vite dev server directly.
- In the production container image, the built frontend is copied into `wwwroot` and served by the API process.

## Azure deployment configuration flow

`azure.yaml` points `azd` at the `infra/` Terraform project:

```yaml
name: foundry-billing
metadata:
  template: foundry-billing
infra:
  provider: terraform
  path: ./infra
```

Terraform computes environment-specific names for:

- resource group
- Log Analytics workspace
- Container Apps environment
- PostgreSQL Flexible Server and database
- Key Vault
- user-assigned managed identity
- Container App

## Configuration checklist by scenario

### Local development

- `Azure:*` user secrets
- `AzureAd:*` user secrets
- Docker running
- Node.js available for the Vite dev server

### Azure deployment

- `AZURE_SUBSCRIPTION_ID` and `AZURE_TENANT_ID` set in the `azd` environment
- post-deploy `AzureAd__ClientId` and `AzureAd__ClientSecret` on the Container App
- app registration redirect URI set to `https://<app-fqdn>/auth/callback`
- managed identity left in place so ARM, Monitor, and AI Projects can authenticate
