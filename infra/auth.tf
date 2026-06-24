resource "azuread_application" "foundry_billing" {
  display_name     = "Foundry Billing Portal (${var.environment_name})"
  sign_in_audience = "AzureADMyOrg"

  web {
    redirect_uris = [
      "https://${local.api_container_app_name}.${azurerm_container_app_environment.main.default_domain}/auth/callback",
      "https://localhost:7220/auth/callback",
    ]

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }

  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000"

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"
      type = "Scope"
    }
  }
}

resource "azuread_application_password" "foundry_billing" {
  application_id = azuread_application.foundry_billing.id
  display_name   = "terraform-managed"
  end_date       = timeadd(timestamp(), "8760h")
}

resource "azuread_service_principal" "foundry_billing" {
  client_id = azuread_application.foundry_billing.client_id
}

resource "azurerm_key_vault_secret" "entra_client_secret" {
  name         = "entra-client-secret"
  value        = azuread_application_password.foundry_billing.value
  key_vault_id = azurerm_key_vault.main.id
  depends_on   = [azurerm_key_vault_access_policy.deployer]
}
