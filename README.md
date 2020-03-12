<!--
[![Build status](https://ci.appveyor.com/api/projects/status/wgd68b7qrwhc7oc3?svg=true)](https://ci.appveyor.com/project/petrsnd/safeguarddotnet)
[![nuget](https://img.shields.io/nuget/v/OneIdentity.SafeguardDotNet.svg)](https://www.nuget.org/packages/OneIdentity.SafeguardDotNet/)
[![GitHub](https://img.shields.io/github/license/OneIdentity/SafeguardDotNet.svg)](https://github.com/OneIdentity/SafeguardDotNet/blob/master/LICENSE)
-->

# Safeguard DevOps Service

The term DevOps can mean different things to different people.  It is important to make sure that we understand what a customer means when they say they need help securing DevOps.

DevOps is any sort of automation that occurs between software development teams and IT teams to enable them to build, test, and release software faster.  Most often, people think of DevOps in the context of automating the deployment of a SaaS solution to a cloud environment. But DevOps can also be as simple as a source code repository hook that triggers a build server to check out and build a .NET library and push it to a NuGet server.

## Challenges
The following are security challenges of DevOps technologies:

    Source code security -- secrets used to pull code from a source code repository
    Build system security -- secrets used to access storage and other resources for sensitive components, code-signing operations, etc.
    Package/image repository security -- secrets used to push build artifacts (packages and images) to repositories as well as pulling artifacts from those repositories
    Securely deploying to infrastructure -- virtual machine root passwords, cloud privileged accounts, privileged accounts in orchestration frameworks, etc.
    Secure microservice communication -- inter-process or service to service communications--these can be passwords, API keys, PKI, etc.
    Secure persistence -- secrets used for persistence technologoes: database passwords, s3 buckets, etc.

All of the security problems listed above involve restricting access to resources.  Access control requires authentication.  It is impossible to make an access control decision unless the system granting access to the resource can identify the requester, or at least know whether the requester can be trusted.  This process of authentication / access control is accomplished through secrets.  Possession of a secret authenticates the requester as trusted.  A secret could be a password, a private key, an API key, etc.

Authentication via possession of a secret becomes more complicated with DevOps because the requester is always an automated process rather than a human being.  Speed is also important in DevOps scenarios.  The immediate needs of DevOps automation cannot wait for a manual approval process (with the notable exception of release gating) as is common with traditional PAM in order to obtain a secret to authenticate.

The easiest way to automate a DevOps process is to use static embedded secrets.  However, security and compliance would dictate that secrets need to be stored securely and periodically rotated.  The secret needs to be securely delivered to the automated process that needs it, whether that be a build system, an orchestrator, a script, or whatever automated process.

In addition to these problems there is just a certain amount of fear that developers are not doing the right thing with DevOps.  There aren't easy ways to attest that developers aren't embedding secrets into code, or configuration files, or virtual machines.  The IT organization feels like DevOps is an opportunity for shadow IT to creep into their environment.

# Solution

The Safeguard recommended practice is to keep the less secure DevOps environment completely separate from the PAM environment with ZERO ACCESS to the PAM environment.  Instead, we will develop a solution for Safeguard "to push the secret to DevOps": 

    Push means there is no access from DevOps to PAM:
        No need for a bootstrap secret with access to PAM.
        No need even for firewall access to PAM.
    Push is more efficient
        The secret is only updated when it actually changes.  
        There is no need to continuously poll for a secret.

![SafeguardDevOpsService](images/SafeguardDevOpsService-1.png)

## Component Description

**Safeguard API** -- Safeguard has the A2A (Application to Application) REST API and the Core REST API (labeled Config in the diagram) that is used to configure the A2A service as well as other Safeguard services.  There are aksi open source SDKs for accessing these APIs from a .NET Standard 2.0 library.  
- Discover -- A2A registrations are visible to certificate users via the core API.
- Monitor -- The A2A API includes a SignalR web socket connection that will give real-time updates for when passwords change (no polling).
- Retrieve -- Pull the secret password from the A2A API in a single HTTPS round trip. 

**Safeguard DevOps Service** -- An open source component that can be deployed as a service or as a container in a customer environment and includes plugins that can be added to communicate with various DevOps technologies.  This service discovers A2A secrets that are configured to be pushed to different DevOps secrets solutions.

**Safeguard DevOps Config Utility** -- Single page web application designed to bootstrap the authentication between the Safeguard DevOps Service and Safeguard.

**PARCache** -- This is a new PARCache service written for Safeguard customers transitioning from TPAM.

## DevOps Technologies Plugins
- HashiCorp Vault
- Azure Key Vault
- Kubernetes Secrets Storage
- ...

### Safeguard for Privileged Passwords Setup

- Navigate to Settings->Appliance->Enable or Disable Services and enable the A2A service
- Add an asset and account (including a service account)
- Set a password on the account
- Add a certificate user and certificate that can be used with A2A
- Assign Auditor permission to the certificate user
- Import the certificate user certificate thumbprint as a trusted certificate
- Navigate to Settings->External Integration->Application to Application
- Add a new A2A registration
- Give the registration a Name and assign it to the new certificate user
- Check the checkboxes for Credential Retrieval and Visible To Certificate Users
- Navigate to the Credential Retrieval tab and add the Account that was previously entered
- Make sure that the A2A registration is enabled

### Safeguard DevOps Service Setup

- Checkout and rebuild all (Rebuild Solution) the SafeguardDevOpsService (https://github.com/OneIdentity/SafeguardDevOpsService)
- Start the SafeguardDevOpsService
- In a browser navigate to http://localhost:5000/swagger/index.html
- Run endpoint: POST /devops/Configuration  
`{
  "SppAddress": "<spp-address>", "CertificateUserThumbprint": "<your-certificate-thumbprint>"
}`
- Run endpoint: GET /devops/Configuration -- Returns the new configuraiton
- Run endpoint: GET /devops/Configuration/RetrievableAccounts -- Returns a list of all of the retrievable accounts
- Run endpoint: GET /devops/Configuration/Plugins -- Return a list of all of the registered plugins.
- Set the HashiCorpVault plugin configuration using endpoint: PUT /devops/Configuration/Plugins/{name}/Configuration
- Enter HashiCorpVault as the name in the URL  
`{
  "Configuration": {"authToken":"<hashicorp-root-token>","address":"<hasicorp-url>", "mountPoint":"secret", "secretsPath":"oneidentity"}
}`
- Replace the authToken with the vault root token that you saved in the HashiCorp Vault setup.  Everything else can be left at the default.
- Set up the Account Mapping using endpoint: PUT /devops/configuration/AccountMapping  
`[
  {
    "accountName": "<account-name>",
    "apiKey": "<a2a-apikey>",
    "VaultName":"HashiCorpVault"
  }
]`
- Replace the accountName with the account name from the RetrievableAccounts output above
- Replace the apiKey with the api key from the RetrievableAccounts output above
- VaultName should be HashiCorpVault
- Set the AzureKeyVault plugin configuration using endpoint: PUT /devops/Configuration/Plugins/{name}/Configuration
- Enter AzureKeyVault as the name in the URL  
`{
  "Configuration": {"applicationId":"<azure-application-id>","clientSecret":"<azure-client-secret>", "vaultUri":"<azure-vault-url>"}
}`
- Set up the Account Mapping using endpoint: PUT /devops/configuration/AccountMapping

[
  {
    "accountName": "bnichuser1",
    "apiKey": "OHFG69KGsF3aIuHzvfZIqSKlGDL0zPgW2VQFJdoDDF4=",
    "VaultName":"AzureKeyVault"
  }
]

    Replace the accountName with the account name from the RetrievableAccounts output above
    Replace the apiKey with the api key from the RetrievableAccounts output above
    Set the KubernetesVault plugin configuration using endpoint: PUT /devops/Configuration/Plugins/{name}/Configuration
        Enter KubernetesVault as the name in the URL

{
  "Configuration": {"configFilePath": null, "vaultNamespace": "default"}
}

    Set up the Account Mapping using endpoint: PUT /devops/configuration/AccountMapping

[
  {
    "accountName": "bnichuser1",
    "apiKey": "OHFG69KGsF3aIuHzvfZIqSKlGDL0zPgW2VQFJdoDDF4=",
    "VaultName":"KubernetesVault"
  }
]

    Replace the accountName with the account name from the RetrievableAccounts output above
    Replace the apiKey with the api key from the RetrievableAccounts output above
    Enable password monitoring using the endpoint: POST /devops/Configuration/Monitoring?enable=true


This project is currently in **technology preview**. It should not be used for any
production environment.

It is still in the **proof of concept** stages. The plugin interface is still being
developed and **may be changed**.

There are many security considerations that have not yet been addressed.

More documentation will be provided in the near future.

Feedback welcome.

