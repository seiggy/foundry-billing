---
title: Deployment Guide
---

# Deployment guide

[Back to docs home](index.md)

This page expands the shorter repository-root `DEPLOY.md` checklist.

## Deployment model

Foundry Billing deploys as a single Azure Container App that serves both:

- the .NET 10 API
- the built React frontend copied into `wwwroot`

The app depends on:

- PostgreSQL Flexible Server
- Key Vault for the database connection string
- a user-assigned managed identity for Azure discovery and metrics access
- Microsoft Entra ID for the browser sign-in flow

## Infrastructure overview

```text
azd + Terraform
      │
      ▼
┌─────────────────────────────────────────────────────────────┐
│ Resource Group                                             │
│  ├── Log Analytics workspace                               │
│  ├── Container Apps environment                            │
│  ├── Container App (API + built SPA)                       │
│  ├── User-assigned managed identity                        │
│  ├── Key Vault                                             │
│  ├── PostgreSQL Flexible Server                            │
│  └── PostgreSQL database                                   │
└─────────────────────────────────────────────────────────────┘
      │                          │
      ▼                          ▼
Managed identity         Entra app registration
• Reader                 • callback URI /auth/callback
• Monitoring Reader      • client ID + client secret
• Cognitive Services User
```

## Prerequisites

- Azure Developer CLI (`azd`)
- Azure CLI (`az`)
- Terraform 1.5+
- Rights to create resources in the target subscription
- Rights to create or configure an Entra app registration

## Provision infrastructure

```bash
az login --tenant <tenant-id>
azd auth login --tenant-id <tenant-id>
azd init
azd env set AZURE_LOCATION canadacentral
azd env set AZURE_SUBSCRIPTION_ID <subscription-id>
azd env set AZURE_TENANT_ID <tenant-id>
azd up
```

`azure.yaml` points `azd` at `infra/`, so `azd up` executes the Terraform deployment.

## Terraform resources created

| Resource | Notes |
|---|---|
| Resource group | Name pattern `rg-fb-{env}-{suffix}` |
| Log Analytics workspace | `PerGB2018`, 30-day retention |
| Container Apps environment | Hosting environment for the application |
| User-assigned managed identity | Assigned to the Container App |
| Key Vault | Stores the Postgres admin password and app connection string |
| PostgreSQL Flexible Server | Version 17, `B_Standard_B1ms`, public network enabled |
| PostgreSQL database | Database name `foundry-billing` |
| PostgreSQL firewall rule | `0.0.0.0` to `0.0.0.0` (Allow Azure services) |
| Container App | Single revision mode, 0.5 CPU, 1 GiB RAM, 1-3 replicas |
| Role assignments | `Reader`, `Monitoring Reader`, `Cognitive Services User` at subscription scope |

Terraform outputs:

- `AZURE_RESOURCE_GROUP`
- `APP_URL`
- `AZURE_KEY_VAULT_NAME`

## Container App runtime configuration

The deployed app receives:

- `Azure__SubscriptionId`
- `Azure__TenantId`
- `AZURE_CLIENT_ID`
- `AzureAd__TenantId`
- `ConnectionStrings__foundry-billing-db` as a Key Vault-backed secret reference
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
- `HTTP_PORT=8080`

The Container App health probes use:

- `/alive` for liveness
- `/health` for readiness

## Post-deploy Entra setup

Terraform does not create the Entra app registration or client secret. Configure them after `azd up`.

### 1. Get the application URL

```bash
azd env get-values
```

Look for `APP_URL`, or query the Container App FQDN directly:

```bash
az containerapp show -n <container-app-name> -g <resource-group> --query "properties.configuration.ingress.fqdn" -o tsv
```

### 2. Create the app registration

In Microsoft Entra ID:

- **Name:** `Foundry Billing Portal`
- **Supported account types:** single tenant
- **Redirect URI:** `https://<container-app-fqdn>/auth/callback`

Also add the local development redirect URI:

- `https://localhost:7220/auth/callback`

### 3. Create a client secret

Generate a client secret on the app registration and copy the secret value immediately.

### 4. Set the Container App secret and environment values

```bash
RESOURCE_GROUP=<resource-group>
APP_NAME=<container-app-name>
CLIENT_ID=<entra-app-client-id>
CLIENT_SECRET=<entra-client-secret>
TENANT_ID=<tenant-id>

az containerapp secret set -n $APP_NAME -g $RESOURCE_GROUP \
  --secrets "entra-client-secret=$CLIENT_SECRET"

az containerapp update -n $APP_NAME -g $RESOURCE_GROUP \
  --set-env-vars \
    "AzureAd__ClientId=$CLIENT_ID" \
    "AzureAd__ClientSecret=secretref:entra-client-secret" \
    "AzureAd__TenantId=$TENANT_ID"
```

## Local development after deployment

To use the same app registration locally, copy the Entra values into user secrets:

```bash
cd src/FoundryBilling.Api
dotnet user-secrets set "AzureAd:TenantId" "<tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<client-id>"
dotnet user-secrets set "AzureAd:ClientSecret" "<client-secret>"
dotnet user-secrets set "Azure:SubscriptionId" "<subscription-id>"
dotnet user-secrets set "Azure:TenantId" "<tenant-id>"
```

## Updating the deployed app

### Infrastructure updates

- Change Terraform in `infra/`
- Update any required `azd` environment values
- Run `azd up` again

### Application image updates

The GitHub Actions workflow `.github/workflows/build-publish.yml` builds and pushes `ghcr.io/seiggy/foundry-billing:latest` on pushes to `main`.

Important detail: the Container App resource ignores image changes in Terraform, so `terraform apply` does not force a new image revision.

To roll forward the running app after a new image is pushed, update the Container App explicitly:

```bash
az containerapp update -n <container-app-name> -g <resource-group> --image ghcr.io/seiggy/foundry-billing:latest
```

### Configuration-only updates

- For app settings and secrets, use `az containerapp update` and `az containerapp secret set`.
- For Key Vault-backed database connection changes, update the secret value and redeploy or restart the app.

## Monitoring

- **UI health:** use the Sync page to verify worker progress and recent run history.
- **Container health:** probe `/alive` and `/health`.
- **Logs:** view Container App logs and revision status.
- **Telemetry:** if `OTEL_EXPORTER_OTLP_ENDPOINT` is configured, the app emits OpenTelemetry traces and metrics.

Useful commands:

```bash
az containerapp logs show -n <container-app-name> -g <resource-group> --follow
az containerapp revision list -n <container-app-name> -g <resource-group> -o table
```

## Troubleshooting

### Sign-in loops or `401` on every request

Check:

- `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret`
- redirect URI exactly matches `/auth/callback`
- HTTPS is being used for the public URL

### No hubs, projects, or deployments are discovered

Check:

- `Azure__SubscriptionId`
- managed identity role assignments at the subscription scope
- whether the target accounts are AI Services accounts with `kind=AIServices`

### No agents are discovered

Check:

- DNS and network access to `https://{hub}.services.ai.azure.com`
- managed identity permission to the AI Foundry project plane
- warning logs for `401`, `403`, or `404` from the AI Projects SDK

### Usage charts stay empty

Check:

- `Monitoring Reader` role assignment
- `Sync:MetricLookbackHours`
- whether enough sync cycles have elapsed to accumulate the target time window
- sync history for `usageSlicesInserted` counts

### Database issues on startup

`MigrateDatabaseAsync()` logs migration errors but does not stop the process. If the database is unavailable during startup, the app can come up without a usable schema. Verify:

- `ConnectionStrings__foundry-billing-db`
- Key Vault secret resolution
- PostgreSQL server accessibility from Container Apps

## Operational cautions

- Terraform state is local. There is no remote backend, so coordinate carefully if multiple people deploy the same environment.
- PostgreSQL is publicly reachable and uses password auth; there is no VNet integration in the current Terraform.
- `min_replicas = 1` is intentional so the background sync worker is always running.
- Historical data is not backfilled automatically; a new environment starts with only the recent metric lookback window.
