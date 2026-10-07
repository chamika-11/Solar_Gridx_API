resource "azurerm_network_interface" "solargridx" {
  name                = "solargridx-nic"
  location            = data.azurerm_resource_group.solargridx.location
  resource_group_name = data.azurerm_resource_group.solargridx.name

  ip_configuration {
    name                          = "internal"
    subnet_id                     = azurerm_subnet.solargridx.id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.solargridx.id
  }
}