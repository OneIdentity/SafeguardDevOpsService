# Hashicorp Vault Plugin

The Hashicorp Vault plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a Hashicorp vault. The account name and password are added as key/value pairs and can be accessed from the vault using the Hashicorp API or web interface.

## Hashicorp plugin specific configuration

***Plugin Details***

* **Supported Credential Types** - Password, SSH key and API key
* **Supports Reverse Flow** - Yes
* **Address** - Full URL to the Hashicorp vault.
* **Mount Point** - Name of the mount point within the Hashicorp vault where the plugin will store the key/value pair secrets.
