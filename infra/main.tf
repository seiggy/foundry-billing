data "azurerm_client_config" "current" {}

locals {
  effective_subscription_id = var.subscription_id != "" ? var.subscription_id : data.azurerm_client_config.current.subscription_id
  effective_tenant_id       = var.tenant_id != "" ? var.tenant_id : data.azurerm_client_config.current.tenant_id
  effective_principal_id    = var.principal_id != "" ? var.principal_id : data.azurerm_client_config.current.object_id

  raw_env_slug    = trim(replace(lower(var.environment_name), "/[^0-9a-z-]/", "-"), "-")
  env_slug        = local.raw_env_slug != "" ? local.raw_env_slug : "dev"
  raw_env_compact = replace(lower(var.environment_name), "/[^0-9a-z]/", "")
  env_compact     = local.raw_env_compact != "" ? substr(local.raw_env_compact, 0, min(length(local.raw_env_compact), 8)) : "dev"
  name_suffix     = "${local.env_compact}-${random_string.resource_suffix.result}"

  tags = merge(
    {
      "azd-env-name" = var.environment_name
      "managed-by"   = "azd"
      "workload"     = "foundry-billing"
    },
    var.tags
  )

  postgres_database_name  = "foundry-billing"
  postgres_admin_username = "fbadmin"
  api_image               = "ghcr.io/seiggy/foundry-billing:latest"
  resource_group_name     = "rg-fb-${local.name_suffix}"
  log_analytics_name      = "log-fb-${local.name_suffix}"
  container_env_name      = "cae-fb-${local.name_suffix}"
  postgres_server_name    = "psql-fb-${local.name_suffix}"
  key_vault_name          = substr("kvfb${local.env_compact}${random_string.resource_suffix.result}", 0, 24)
  api_identity_name       = "id-fb-${local.name_suffix}-api"
  api_container_app_name  = "ca-fb-${local.name_suffix}"
}

resource "random_string" "resource_suffix" {
  length  = 5
  lower   = true
  numeric = true
  special = false
  upper   = false
}

resource "random_password" "postgres_admin" {
  length           = 32
  special          = true
  min_lower        = 4
  min_numeric      = 4
  min_special      = 2
  min_upper        = 4
  override_special = "!@#%^*-_=+?"
}

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = local.log_analytics_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_app_environment" "main" {
  name                       = local.container_env_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  tags                       = local.tags
}

resource "azurerm_user_assigned_identity" "api" {
  name                = local.api_identity_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.tags
}

resource "azurerm_key_vault" "main" {
  name                       = local.key_vault_name
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = local.effective_tenant_id
  sku_name                   = "standard"
  purge_protection_enabled   = false
  soft_delete_retention_days = 7
  public_network_access_enabled = true
  tags                       = local.tags
}

resource "azurerm_key_vault_access_policy" "deployer" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = local.effective_tenant_id
  object_id    = local.effective_principal_id

  secret_permissions = [
    "Delete",
    "Get",
    "List",
    "Purge",
    "Recover",
    "Set",
  ]
}

resource "azurerm_key_vault_access_policy" "api" {
  key_vault_id = azurerm_key_vault.main.id
  tenant_id    = local.effective_tenant_id
  object_id    = azurerm_user_assigned_identity.api.principal_id

  secret_permissions = [
    "Get",
    "List",
  ]
}

resource "azapi_resource" "horizondb_cluster" {
  type      = "Microsoft.HorizonDb/clusters@2026-01-20-preview"
  name      = local.postgres_server_name
  location  = azurerm_resource_group.main.location
  parent_id = azurerm_resource_group.main.id

  schema_validation_enabled = false

  body = {
    properties = {
      administratorLogin         = local.postgres_admin_username
      administratorLoginPassword = random_password.postgres_admin.result
      createMode                 = "Create"
      version                    = "17"
      vCores                     = 2
      storageSizeInGb            = 32
      highAvailability           = { mode = "Disabled" }
      network                    = { publicNetworkAccess = "Enabled" }
    }
  }

  tags = local.tags
}

resource "azurerm_key_vault_secret" "postgres_admin_password" {
  name         = "postgres-admin-password"
  value        = random_password.postgres_admin.result
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.deployer]
}

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "foundry-billing-db-connection-string"
  value        = "Host=${azapi_resource.horizondb_cluster.name}.cluster.postgres.database.azure.com;Database=${local.postgres_database_name};Username=${local.postgres_admin_username};Password=${random_password.postgres_admin.result};Ssl Mode=Require;Trust Server Certificate=true"
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [
    azurerm_key_vault_access_policy.deployer,
    azapi_resource.horizondb_cluster,
  ]
}

resource "azurerm_role_assignment" "api_reader" {
  scope                = "/subscriptions/${local.effective_subscription_id}"
  role_definition_name = "Reader"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "api_monitoring_reader" {
  scope                = "/subscriptions/${local.effective_subscription_id}"
  role_definition_name = "Monitoring Reader"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "api_cognitive_services_user" {
  scope                = "/subscriptions/${local.effective_subscription_id}"
  role_definition_name = "Cognitive Services User"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
  principal_type       = "ServicePrincipal"
}
