# Azure Key Vault Plugin

The Azure Key Vault plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into an Azure key vault as secrets. The secrets can be pulled from the Azure key vault using the appropriate Microsoft APIs.

## Installation

To install the plugin, click on the  ```Upload Custom Plugin``` button in the ```What would you like to plug in?``` card. This will display a file selection window that can be used to select a plugin installation file. Once a plugin intallation file as been selected, Secrets Broker will install the plugin and display a new card that can be used to provide addition connection configuration.

Updating a plugin is done in the same way. To update a plugin, click on the ```Upload Custom Plugin``` button and select the new plugin installation file. Updating a plugin will require a restart of the Secrets Broker service.

All of the plugins for Secrets Broker are available on the SafeguardDevOpsService Github project site (<https://github.com/OneIdentity/SafeguardDevOpsService>). They can be downloaded by clicking on the ```View Plugin Repository``` button in the ```What would you like to plug in?``` card.

## Configuration

Once installed, the plugin can be configured by click on the ```Edit Configuration``` link in the Azure Key Vault plugin card. This will cause Secrets Broker to display the Plugin Settings page for the Azure Key Vault plugin.

The Plugin Settings page contains three configuration panels.

**Basic Info** - This panel displays basic information about the plugin which includes the plugin name, description and version. The panel is readonly.

**Configuration** - This panel is used to configure the plugin with connection information. All plugins need to select an account/password that is used to access the third party secret store. The account and password must have been created in SPP prior to configuring the plugin. A checkbox is provided to set the enabled state of the plugin. If ```Disabled``` is checked, the plugin will not monitor SPP for password changes.

***Plugin Details***

* Application Id - Accessing the Azure key vault is done using an Active Directory App Registration. Creating an App Registration generates an Application Id (also known as the Client Id). The Application Id must be provided in the configuration of the Azure Key Vault plugin. In addition to the Application/Client Id, the app registration will also generate a client secret. The client secret must be added to SPP as the password for the account used to access the key vault.
* Vault Uri - This is the full URL that points to the Azure Key Vault.
* Tenant Id - This is the tenant id that is associated with the Azure key vault. This is also known as the Directory Id in the properties of the key vault.

***Test Configuration*** - Once the plugin has been completely configured, use the ```Test Configuration``` button to test whether the plugin is able to complete a connection to the Azure key vault interface using the current configuration.

**Managed Accounts** - This panel is used to select all of the accounts from SPP whose passwords should be pushed to the Azure key vault. Click on the ```Select Accounts``` button to display a list of available accounts from SPP. Each selected account will be displayed in the ```Managed Accounts``` list. Once an account has been selected, an alternate account name can be defined. By default, the Azure Key Vault plugin will construction an account name by concatenating the asset name with the account name (MyAsset-MyAccount). The constructed name is what will appear in the key vault as the name of the secret. If an alternate account name is specified, the plugin will push the password to the key vault as the alternate name.

**Other Functions***

***Delete Plugin*** - If the plugin is not longer needed or used, it can be removed from Secrets Broker by clicking on the ```Delete Plugin``` button.

***Save*** - To save all of the plugin configuration to the Secrets Broker database, click on the ```Save``` button.
