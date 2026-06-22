# Deploy

1. Install azd: https://aka.ms/azd
2. Run `az login`
3. Run `azd init` if this is your first time using the repo
4. Set the required azd environment values:
   - `azd env set AZURE_LOCATION eastus2`
   - `azd env set AZURE_SUBSCRIPTION_ID <subscription-id>`
   - `azd env set AZURE_TENANT_ID <tenant-id>`
   - `azd env set AZURE_PRINCIPAL_ID <object-id>`
5. Optionally configure the Terraform azurerm backend with backend config values before provisioning shared state
6. Run `azd provision`

## Notes

- `azure.yaml` uses Terraform from `infra/`.
- `infra/main.tfvars.json` maps azd environment variables into Terraform variables.
- Container Apps use public images from `ghcr.io/seiggy/foundry-billing-api:latest` and `ghcr.io/seiggy/foundry-billing-web:latest`.
- Images are expected to be published to GitHub Container Registry by GitHub Actions before deployment.
- `azd package` is a no-op in this configuration because Container Apps are provisioned directly from Terraform using public GHCR images.
