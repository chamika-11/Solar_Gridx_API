resource "azurerm_virtual_network" "solargridx" {
  name                = "solargridx-vnet"
  location            = data.azurerm_resource_group.solargridx.location
  resource_group_name = data.azurerm_resource_group.solargridx.name
  address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "solargridx" {
  name                 = "solargridx-subnet"
  resource_group_name  = data.azurerm_resource_group.solargridx.name
  virtual_network_name = azurerm_virtual_network.solargridx.name
  address_prefixes     = ["10.0.1.0/24"]
}

resource "azurerm_subnet_network_security_group_association" "solargridx" {
  subnet_id                 = azurerm_subnet.solargridx.id
  network_security_group_id = azurerm_network_security_group.solargridx.id
}