# Jenkins Secrets Plugin

The Jenkins Secrets plugin allows Secrets Broker to pull passwords from Safeguard for Privileged Passwords (SPP) and push them into a Jenkins secrets store.

## Jenkins Secrets plugin specific configuration

***Plugin Details***

* Address - Full URL to the Jenkins server.
* User Name - User name of a Jenkins account that has permissions to add secrets to the Jenkins environment. An API token must be created for the Jenkins user and the token must be added to SPP as the password of the account that is selected in the plugin configuration.
