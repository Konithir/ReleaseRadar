variable "prefix" {
  description = "Short lowercase prefix used for Azure resource names."
  type        = string
  default     = "rradar"
}

variable "location" {
  description = "Azure region for all resources."
  type        = string
  default     = "westeurope"
}

variable "environment" {
  description = "Environment suffix."
  type        = string
  default     = "prod"
}

variable "kubernetes_version" {
  description = "AKS version. Null lets Azure select the current default."
  type        = string
  default     = null
  nullable    = true
}

variable "system_node_count" {
  description = "Number of nodes in the AKS system pool."
  type        = number
  default     = 3
}

variable "system_node_vm_size" {
  description = "VM SKU for the AKS system pool."
  type        = string
  default     = "Standard_D4ds_v5"
}

variable "sql_database_sku" {
  description = "Azure SQL database SKU."
  type        = string
  default     = "GP_S_Gen5_1"
}



