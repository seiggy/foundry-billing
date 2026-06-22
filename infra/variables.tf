variable "location" {
  type        = string
  description = "Azure region for shared resources."
  default     = "eastus2"
}

variable "environment_name" {
  type        = string
  description = "Environment name supplied by azd."
}

variable "principal_id" {
  type        = string
  description = "Object ID of the deploying principal."
  default     = ""
}

variable "subscription_id" {
  type        = string
  description = "Target Azure subscription ID for deployment and billing discovery."
  default     = ""
}

variable "tenant_id" {
  type        = string
  description = "Target Azure tenant ID."
  default     = ""
}

variable "tags" {
  type        = map(string)
  description = "Additional tags merged with the default azd tags."
  default = {
    app = "foundry-billing"
  }
}
