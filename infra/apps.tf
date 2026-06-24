resource "azurerm_container_app" "api" {
  name                         = local.api_container_app_name
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  secret {
    name                = "connectionstrings-foundry-billing-db"
    identity            = azurerm_user_assigned_identity.api.id
    key_vault_secret_id = azurerm_key_vault_secret.postgres_connection_string.versionless_id
  }

  template {
    min_replicas = 1
    max_replicas = 3

    container {
      name   = "app"
      image  = local.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_FORWARDEDHEADERS_ENABLED"
        value = "true"
      }

      env {
        name  = "HTTP_PORT"
        value = "8080"
      }

      env {
        name  = "Azure__SubscriptionId"
        value = local.effective_subscription_id
      }

      env {
        name  = "Azure__TenantId"
        value = local.effective_tenant_id
      }

      env {
        name  = "AzureAd__TenantId"
        value = local.effective_tenant_id
      }

      env {
        name        = "ConnectionStrings__foundry-billing-db"
        secret_name = "connectionstrings-foundry-billing-db"
      }

      liveness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/alive"
        initial_delay           = 10
        interval_seconds        = 30
        timeout                 = 5
        failure_count_threshold = 3
      }

      readiness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health"
        initial_delay           = 5
        interval_seconds        = 10
        timeout                 = 5
        failure_count_threshold = 3
      }
    }
  }

  ingress {
    allow_insecure_connections = false
    external_enabled           = true
    target_port                = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  tags = merge(local.tags, {
    "azd-service-name" = "api"
  })

  lifecycle {
    ignore_changes = [
      template[0].container[0].image,
    ]
  }

  depends_on = [azurerm_key_vault_access_policy.api]
}
