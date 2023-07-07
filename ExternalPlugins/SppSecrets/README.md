# Safeguard for Privileged Passwords Plugin

The Safeguard for Privileged Passwords plugin allows Secrets Broker to pull passwords from a Safeguard for Privileged Passwords (SPP) cluster appliance and push them into a downstream SPP appliance. The secrets can be pulled from the downstream SPP appliance using the SafeguardDotnet client library APIs, SafeguardJava, Safeguard-ps cmdlets or any other Safeguard client.

## Safeguard for Privileged Passwords plugin specific configuration

### Downstream SPP configuration requirements

* Create a local SPP user that will be used by Secrets Broker to manage the A2A registration and accounts group in the downstream SPP appliance.
  * Assign policy admin permissions to the new SPP user.
  * This user name will be entered as the ```SPP user``` in the configuration of the SPPtoSPP Secrets Broker plugin.
* Create an A2A certificate with ```Client Authentication``` attribute.
  * Install the A2A certificate with private key .pfx in the ```Local Computer``` certificate store of the Secrets Broker server.
* Create an local A2A certificate user in the downstream SPP appliance.
  * This user will be entered as the ```SPP A2a Certificate User``` in the configuration of the SPPtoSPP Secrets Broker plugin.
* Add the A2A client certificate or certificate chain to the ```Trusted Certificates``` of the downstream SPP appliance.

### Upstream SPP configuration requirements

* Create an ```Other``` platform type asset that represents the downstream SPP appliance.
  * Add an account to the SPP appliance asset with an account password that matches the password of the local user in the downstream SPP appliance.
  * This account will be used as the ```Vault Account``` in the SPPtoSPP Secrets Broker plugin.

### Plugin Details

* **Supported Credential Types** - Password, SSH key and API key
* **Supports Reverse Flow** - No
* **SPP Appliance** - IP address or host name of the downstream SPP appliance.
* **SPP User** - Downstream user name of an SPP local user with policy admin permissions.
* **SPP A2A Registration Name** - Name of the downstream A2A registration that is used to provide A2A access to the credentials that are pushed by Secrets Broker.
* **SPP A2a Certificate User** - Downstream A2A certificate user.
* **SPP Account Group** - Name of an account group for the credentials that are pushed by Secrets Broker.
