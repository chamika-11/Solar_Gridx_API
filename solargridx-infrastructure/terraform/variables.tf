variable "resource_group_name" {
  description = "Azure resource group name"
  type        = string
  default     = "solargridx-rg"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "Southeast Asia"
}

variable "vm_size" {
  description = "Azure VM size"
  type        = string
  default     = "Standard_B2s_v2"
}

variable "admin_username" {
  description = "Windows VM administrator username"
  type        = string
  default     = "solargridxadmin"
}

variable "admin_password" {
  description = "Windows VM administrator password"
  type        = string
  sensitive   = true
}