output "AZURE_RESOURCE_GROUP" {
  value       = azurerm_resource_group.main.name
  description = "Provisioned resource group name."
}

output "API_URL" {
  value       = "https://${azurerm_container_app.api.latest_revision_fqdn}"
  description = "Public API URL."
}

output "WEB_URL" {
  value       = "https://${azurerm_container_app.web.latest_revision_fqdn}"
  description = "Public web URL."
}

output "AZURE_KEY_VAULT_NAME" {
  value       = azurerm_key_vault.main.name
  description = "Key Vault name holding deployment secrets."
}
