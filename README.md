<!--
[![Build status](https://ci.appveyor.com/api/projects/status/wgd68b7qrwhc7oc3?svg=true)](https://ci.appveyor.com/project/petrsnd/safeguarddotnet)
[![nuget](https://img.shields.io/nuget/v/OneIdentity.SafeguardDotNet.svg)](https://www.nuget.org/packages/OneIdentity.SafeguardDotNet/)
[![GitHub](https://img.shields.io/github/license/OneIdentity/SafeguardDotNet.svg)](https://github.com/OneIdentity/SafeguardDotNet/blob/master/LICENSE)
-->

# Safeguard DevOps Service

The term DevOps can mean different things to different people.  It is important to make sure that we understand what we mean when we say we need help securing DevOps.

DevOps is any form of automation used between software development teams and operations teams to to build, test, and release software with speed and resilience.  Most often, people think of DevOps in the context of automating the deployment of a SaaS solution to a cloud environment. However, DevOps can also be as simple as a source code repository hook that triggers a build server to check out and build a .NET library and push it to a NuGet server.

## Support

One Identity open source projects are supported through [One Identity GitHub issues](https://github.com/OneIdentity/SafeguardDevOpsService/issues) and the [One Identity Community](https://www.oneidentity.com/community/). This includes all scripts, plugins, SDKs, modules, code snippets or other solutions. For assistance with any One Identity GitHub project, please raise a new Issue on the [One Identity GitHub project](https://github.com/OneIdentity/SafeguardDevOpsService/issues) page. You may also visit the [One Identity Community](https://www.oneidentity.com/community/) to ask questions.  Requests for assistance made through official One Identity Support will be referred back to GitHub and the One Identity Community forums where those requests can benefit all users.

## Challenges

The following are security challenges of DevOps technologies:

- Source code security -- secrets used to pull code from a source code repository
- Build system security -- secrets used to access storage and other resources for sensitive components, code-signing operations, etc.
- Package/image repository security -- secrets used to push build artifacts (packages and images) to repositories as well as pulling artifacts from those repositories
- Securely deploying to infrastructure -- virtual machine root passwords, cloud privileged accounts, privileged accounts in orchestration frameworks, etc.
- Secure microservice communication -- inter-process or service to service communications--these can be passwords, API keys, PKI, etc.
- Secure persistence -- secrets used for persistence technologoes: database passwords, s3 buckets, etc.

All of the security problems listed above involve restricting access to resources.  Access control requires authentication.  It is impossible to make an access control decision unless the system granting access to the resource can identify the requester, or at least know whether the requester can be trusted.  This process of authentication / access control is accomplished through secrets.  Possession of a secret authenticates the requester as trusted.  A secret could be a password, a private key, an API key, etc.

Authentication via possession of a secret becomes more complicated with DevOps because the requester is always an automated process rather than a human being.  Speed is also important in DevOps scenarios.  The immediate needs of DevOps automation cannot wait for a manual approval process (with the notable exception of release gating) as is common with traditional PAM in order to obtain a secret to authenticate.

The easiest way to automate a DevOps process is to use static embedded secrets.  However, security and compliance would dictate that secrets need to be stored securely and periodically rotated.  The secret needs to be securely delivered to the automated process that needs it, whether that be a build system, an orchestrator, a script, or whatever automated process.

In addition to these problems there is just a certain amount of fear that developers are not doing the right thing with DevOps.  There aren't easy ways to attest that developers aren't embedding secrets into code, or configuration files, or virtual machines.  The IT organization feels like DevOps is an opportunity for shadow IT to creep into their environment.

# Solution

The Safeguard recommended practice is to keep the less secure DevOps environment completely separate from the PAM environment with ZERO ACCESS to the PAM environment.  Instead, we will develop a solution for Safeguard "to push the secret to DevOps":

- Push means there is no access from DevOps to PAM:
  - No need for a bootstrap secret with access to PAM.
  - No need even for firewall access to PAM.
- Push is more efficient
  - The secret is only updated when it actually changes.
  - There is no need to continuously poll for a secret.

![SafeguardDevOpsService](Images/SafeguardDevOpsService-1.png)

## Component Description

**Safeguard API** -- Safeguard has the A2A (Application to Application) REST API and the Core REST API (labeled Config in the diagram) that is used to configure the A2A service as well as other Safeguard services.  There are also open source SDKs for accessing these APIs from a .NET Standard 2.0 library.

- Discover -- A2A registrations are visible to certificate users via the core API.
- Monitor -- The A2A API includes a SignalR web socket connection that will give real-time updates for when passwords change (no polling).
- Retrieve -- Pull the secret password from the A2A API in a single HTTPS round trip.

**Safeguard DevOps Service** -- An open source component that can be deployed as a service or as a container in a customer environment and includes plugins that can be added to communicate with various DevOps technologies.  This service discovers A2A secrets that are configured to be pushed to different DevOps secrets solutions.

**Safeguard DevOps Config Utility** -- Single page web application designed to bootstrap the authentication between the Safeguard DevOps Service and Safeguard.

**PARCache** -- This is a new PARCache service written for Safeguard customers transitioning from TPAM.

[![Safeguad DevOps Demo Video](Images/SafeguardDevOpsServiceDemo-1.png)](https://www.youtube.com/watch?v=QFNllIpQxQ8)

## DevOps Technologies Plugins

- HashiCorp Vault
- Azure Key Vault
- Kubernetes Secrets Storage
- ...

## Safeguard for Privileged Passwords Setup

- Navigate to Settings->Appliance->Enable or Disable Services and enable the A2A service
- Add an asset and account (including a service account)
- Set a password on the account
- Create an AssetAccount for each third party vault that will be used by the DevOps service.  The account should contain the vault credential that will be used to authenticate to the vault itself.  This account will be used as part of the configuration of the third party vault plugin.
- Optional: Create a new certificate with a private key (PFX format) that will be assigned to the certificate user.  The public certificate will be uploaded into SPP as a trusted certificate along with any other issuer certificates that may be part of the certificate chain.  The certificate and private key will be uploaded into the DevOps service during configuration and be used to create a new certificate user and A2A registration.  This certificate can be created independent of the DevOps service or from a CSR that is created by the DevOps service.  (See Configuring the DevOps Service)

## Safeguard DevOps Service Setup

### From Source

- Checkout and rebuild all (Rebuild Solution) the SafeguardDevOpsService (<https://github.com/OneIdentity/SafeguardDevOpsService>)
- Start the SafeguardDevOpsService
- In a browser navigate to <https://localhost/service/devops/swagger/index.html>
- Right-click on the SetupSafeguardDevOpsService project and select "Build" to build the installer MSI package.

### From Installer

- Copy the installer MSI package to the local file system of a Windows 10 or Windows Server 2016 or better, computer.
- Open a PowerShell command window as an administrator and invoke the above MSI installer package.
- Follow all prompts - This should deploy the package and automatically start it as a Windows service.
- At start up the DevOps service will create a new folder under the root directory as /SafeguardDevOpsService.  This folder will contain the log file and the external plugins folder.  The external plugins folder will be initially empty (See Deploying Vault Plugins)  The configuration database will be created in the folder C:\Windows\system32\config\systemprofile\AppData\Roaming\SafeguardDevOpsService\Configuration.db.
- Make sure that the firewall on the Windows computer has an inbound rule for allowing https port 443
- Acquire a valid login token to SPP.  Use the Powershell cmdlet (See <https://github.com/OneIdentity/safeguard-ps>):

```powershell
    Connect-Safeguard insecure <spp-ip-address> local <user-with-admin-permissions> -NoSessionVariable
```

- In a browser navigate to `<https://<your-server-ip>/service/devops/swagger/index.html>`
- Click on the "Authorize" button on the upper left-hand side of the DevOps Service swagger page.
Enter `spp-token <paste token>` as the value and click the Authorize button and then Close button
  - At this point the swagger page has a login token that will be used in every call made to the DevOps API
- Navigate to and call: `PUT /service/devops/Safeguard`

```json
    {
    "NetworkAddress": "<your SPP appliance>",
    "ApiVersion": 3,
    "IgnoreSsl": true
    }
```

- This endpoint will check the connectivity to the SPP appliance and fetch and store the token signing certificate
  - It is also a little unique in that the call must contain a valid authorization token just like all other calls, but it can be called before the user actually logs into the DevOps service.  The user authorization will still be validated but it is a one-time validation just to make sure that the user is authorized to setup the SPP network information.
- Navigate to and call" `GET /service/devops/Safeguard/Logon`
  - At this point the swagger page is logged into the DevOps service and will remain logged in until the page is refreshed, closed or `POST /service/devops/Safeguard/Logoff` is called.

## Configuring the DevOps Service

- There are two different certificates that the DevOps service needs in order to function properly.
  - The first certificate is the web service SSL certificate.  A default self-signed SSL certificate was create when the DevOps service was launched for the first time.  This certificate can be replaced with your own server authentication SSL certificate if desired.  This is optional.
  - The second certificate is a client authentication certificate which will be used to create the SPP certificate user and A2A registration.
  - Both of these certificates with their corresponding private keys can be generated outside of the DevOps service and uploaded in PFX format or the DevOps service can generate a private key and CSR which can be signed and uploaded.
- Install a client certificate and private key - Since the web service SSL certificate is optional, only the steps for creating the client certificate will be described here.  A similar procedure can be used to generate and upload the web service SSL certificate.
  - Navigate to and call: `GET /service/devops/Safeguard/CSR` with the certificate type `A2AClient`.  An optional certificate size and subject name can be provided.
  - Sign the CSR to produce a public certificate
    - KeyUsage - DigitalSignature, KeyEncipherment
    - ExtendedKeyUsage - ClientAuth
  - Navigate to and call the POST /service/devops/Safeguard/ClientCertificate with the JSON body

    ```json
    {
      "Base64CertificateData" : "<string>",
      "Passphrase" : "<string>" - Only if uploading a PFX with a private key otherwise omit
    }
    ```

  - Navigate to and call: `POST /service/devops/Safeguard/Configuration` with an empty body  `{}`
    - Optionally the client certificate can be uploaded as part of configuring the DevOps service in this call, by passing the same body as above.
    - This call will store the client certificate and private key in the DevOps database, create a new DevOpsService User in SPP with the appropriate permissions, create a two new A2A registrations with the appropriate IP restrictions and prepare both the DevOps service and SPP to start pulling passwords.

## Deploying Vault Plugins

- Copy one or more plugin zip files to the Windows local file system
- Navigate to and call: `POST /service/devops/Plugins/File` to upload the plugin zip file.
  - The DevOps service will automatically detect, load and register each plugin.
- Navigate to and call: `GET /service/devops/Plugins` to verify that the plugin(s) were deployed and registered in the DevOps service
- Since each plugin has its own unique configuration, each one must be configured individually.
  - Navigate to and call: `PUT /service/devops/Plugins/{name}` with the appropriate body to configure the plugin.
  - The appropriate body can be copy and pasted from the corresponding JSON that is returned from `GET /service/devops/Plugins/{name}`. The PUT API for configuring the plugin will only recognize the entries under the "Configuration" tag even though the body will accept the entire plugin JSON body. For example, the following can be used to configure the HashiCorp Vault plugin:

    ```json
    {
      "Configuration":
      {
        "address":"<hasicorp-url>",
        "mountPoint":"secret",
      }
    }
    ```

## Configuring and Mapping Accounts to the Vault Plugins

- Navigate to and call: `GET /service/devops/Safeguard/AvailableAccounts`
  - This call will produce a list of all of the available accounts in SPP that can be requested.
  - Copy and paste the desired contents of this call to the following API for adding retrievable accounts
- Navigate to and call: `POST /service/Devops/Safeguard/A2ARegistration/RetrievableAccounts`
  - The body of this call should be copied and pasted from the previous results.  The body can be edited to remove any account data that should not be include in the A2A retrievable accounts.
  - Copy the results of this call into the following API for mapping accounts to plugins.
- Navigate to and call: `POST /service/devops/Plugins/{name}/Accounts`
  - The body of this call should be copied and pasted from the previous results.  The body can be edited to remove any account data that should not be used to pull a password and send it to the vault plugin.
  - Repeat the above call for each plugin which needs to be configured for pulling account passwords.

## Configuring the Vault Credential Account for the Plugin

- Navigate to and call: `GET /service/devops/Safeguard/AvailableAccounts`
  - This call will produce a list of all of the available accounts in SPP that can be requested.
  - Copy and paste the Asset-Account that corresponds to the third party vault, to the following API for adding a vault account.
- Navigate to and call: `POST /service/devops/Plugins/{name}/VaultAccount`
  - The body of this call should be copied and pasted from the previous results.  It should be just the account information that corresponds to the third party vault.
  - Repeat the above call for each plugin that needs to be configured for pulling the vault credential.

## Start the DevOps Password Monitoring Service

- Navigate to and call POST /service/devops/Monitor

  ```json
  {
    "Enabled": true
  }
  ```

- The same API can be used to stop the password monitoring service.
- At this point the DevOps service will detect whenever a password changes in SPP, pull the password and push it to the appropriate plugin(s).  The custom code in the plugin(s) will push the password to the third party vault.

## Notice

This project is currently in **technology preview**. It should not be used for any
production environment.

It is still in the **Beta** stages. The plugin interface is still being
developed and **may be changed**.

There are some security considerations that have not yet been addressed.

More documentation will be provided in the near future.

Feedback welcome.
