output "AZURE_RESOURCE_GROUP" {
  value       = azurerm_resource_group.main.name
  description = "Provisioned resource group name."
}

output "APP_URL" {
  value       = "https://${azurerm_container_app.api.latest_revision_fqdn}"
  description = "Public application URL (API + frontend served together)."
}

output "AZURE_KEY_VAULT_NAME" {
  value       = azurerm_key_vault.main.name
  description = "Key Vault name holding deployment secrets."
}

output "ENTRA_CLIENT_ID" {
  value       = azuread_application.foundry_billing.client_id
  description = "Entra app registration client ID."
}
