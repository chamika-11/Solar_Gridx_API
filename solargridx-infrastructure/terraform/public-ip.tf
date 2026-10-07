resource "azurerm_public_ip" "solargridx" {
  name                = "solargridx-pip"
  location            = data.azurerm_resource_group.solargridx.location
  resource_group_name = data.azurerm_resource_group.solargridx.name
  allocation_method   = "Static"
  sku                 = "Standard"
}