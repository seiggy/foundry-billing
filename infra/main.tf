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
  api_image               = "ghcr.io/seiggy/foundry-billing-api:latest"
  web_image               = "ghcr.io/seiggy/foundry-billing-web:latest"
  resource_group_name     = "rg-fb-${local.name_suffix}"
  log_analytics_name      = "log-fb-${local.name_suffix}"
  container_env_name      = "cae-fb-${local.name_suffix}"
  postgres_server_name    = "psql-fb-${local.name_suffix}"
  key_vault_name          = substr("kvfb${local.env_compact}${random_string.resource_suffix.result}", 0, 24)
  api_identity_name       = "id-fb-${local.name_suffix}-api"
  api_container_app_name  = "ca-fb-${local.name_suffix}-api"
  web_container_app_name  = "ca-fb-${local.name_suffix}-web"
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

resource "azurerm_postgresql_flexible_server" "main" {
  name                          = local.postgres_server_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "16"
  administrator_login           = local.postgres_admin_username
  administrator_password        = random_password.postgres_admin.result
  public_network_access_enabled = true
  sku_name                      = "B_Standard_B1ms"
  storage_mb                    = 32768
  backup_retention_days         = 7
  tags                          = local.tags
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = local.postgres_database_name
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_key_vault_secret" "postgres_admin_password" {
  name         = "postgres-admin-password"
  value        = random_password.postgres_admin.result
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_key_vault_access_policy.deployer]
}

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "foundry-billing-db-connection-string"
  value        = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=${azurerm_postgresql_flexible_server_database.main.name};Username=${local.postgres_admin_username}@${azurerm_postgresql_flexible_server.main.name};Password=${random_password.postgres_admin.result};Ssl Mode=Require;Trust Server Certificate=true"
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [
    azurerm_key_vault_access_policy.deployer,
    azurerm_postgresql_flexible_server_database.main,
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
