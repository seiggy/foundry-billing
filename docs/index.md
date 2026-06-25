---
title: Documentation Home
---

# Foundry Billing documentation

Foundry Billing is a unified billing observability portal for Azure AI Foundry usage, inventory, and PTU planning. These pages are written from the current source in `src/`, `infra/`, and `DEPLOY.md`.

## Contents

- [Architecture](architecture.md) — system components, data flow, database schema, auth, and endpoint map
- [Configuration](configuration.md) — `appsettings.json`, environment variables, user secrets, and Aspire topology
- [API reference](api-reference.md) — every auth, health, analytics, inventory, and sync endpoint with request and response examples
- [Development guide](development.md) — local setup, Aspire workflow, tests, project structure, and migrations
- [Deployment guide](deployment.md) — `azd` + Terraform deployment, Entra post-configuration, updates, and troubleshooting
- [PTU calculator](ptu-calculator.md) — TPM inputs, sizing rules, cost tiers, and recommendation logic

## Start here

- For a quick project overview, read the repository [README](../README.md).
- For local setup, go to [Development](development.md).
- For production rollout, go to [Deployment](deployment.md).
- For data model and sync behavior, go to [Architecture](architecture.md).

## Documentation scope

These docs cover:

- The .NET 10 Minimal API backend
- The React/TypeScript dashboard
- PostgreSQL storage and EF Core schema
- Background sync with ARM, Azure Monitor, and Azure AI Projects
- Local orchestration with Aspire
- Azure deployment with `azd`, Terraform, Container Apps, Key Vault, and PostgreSQL Flexible Server
