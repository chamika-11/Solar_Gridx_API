resource "azurerm_windows_virtual_machine" "solargridx" {
  name                = "solargridx-vm"
  location            = data.azurerm_resource_group.solargridx.location
  resource_group_name = data.azurerm_resource_group.solargridx.name
  size                = var.vm_size

  admin_username = var.admin_username
  admin_password = var.admin_password

  network_interface_ids = [
    azurerm_network_interface.solargridx.id
  ]

  os_disk {
    name                 = "solargridx-osdisk"
    caching              = "ReadWrite"
    storage_account_type = "Standard_LRS"
  }

  source_image_reference {
    publisher = "MicrosoftWindowsServer"
    offer     = "WindowsServer"
    sku       = "2022-datacenter-azure-edition"
    version   = "latest"
  }
}