# Azure Key Vault Plugin

The Azure Key Vault plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into an Azure key vault as secrets. The secrets can be pulled from the Azure key vault using the appropriate Microsoft APIs.

## Azure Key Vault plugin specific configuration

***Plugin Details***

* Application Id - Accessing the Azure key vault is done using an Active Directory App Registration. Creating an App Registration generates an Application Id (also known as the Client Id). The Application Id must be provided in the configuration of the Azure Key Vault plugin. In addition to the Application Id/Client Id, the app registration will also generate a client secret. The client secret must be added to SPP as the password for the account used to access the key vault.
* Vault Uri - This is the full URL that points to the Azure Key Vault.
* Tenant Id - This is the tenant id that is associated with the Azure key vault. This is also known as the Directory Id in the properties of the key vault.
