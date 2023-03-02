# Github Secrets Plugin

The Github Secrets plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a Github project secrets environment.

## Github Secrets plugin specific configuration

***Plugin Details***

* Repository Name - Name of the Github project repository.

An personal access token (PAT) for a Github account that has admin rights for the project, must be created. The access token must be added to an account in SPP as the password for the account. This account must be selected as the vault account on the configuration page of the Github plugin.  The PAT just have been granted ```Full Control``` permissions to the ```repo```.
