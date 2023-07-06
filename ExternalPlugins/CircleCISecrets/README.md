# CircleCI Secrets Plugin

The CircleCI Secrets plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a context or a project as environment secrets.

***Plugin Details***

* **Supported Credential Types** - Password, SSH key and API key
* **Supports Reverse Flow** - No
* **Organization Id** - Full URL to the Hashicorp vault.
* **Context Name** - Name of the mount point within the Hashicorp vault where the plugin will store the key/value pair secrets.
* **Repository Url** - Full URL to the root of the Github or Bitbucket project that is being built by CircleCI.
