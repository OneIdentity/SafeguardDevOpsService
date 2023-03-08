# Github Secrets Plugin

The Github Secrets plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a Github project secrets environment.

## Github Secrets plugin specific configuration

***Plugin Details***

* Repository Name - Name of the Github project repository.

A personal access token (PAT) for a Github account that has admin rights for the project, must be created. The access token must be added to an account in SPP as the password for the account. This account must be selected as the vault account on the configuration page of the Github plugin.  The PAT must have been granted ```Full Control``` permissions to the ```repo```.

[NOTE] The Github plugin uses a special library to encrypt the password before sending it to Github to be stored in the project environment. This encryption library may require the installation of an addition C++ runtime library in order to function correctly. The installer for the C++ runtime library can be downloaded from <https://aka.ms/vs/17/release/vc_redist.x64.exe>.
