[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/safeguard-devops.svg)](https://www.powershellgallery.com/packages/safeguard-devops)
[![GitHub](https://img.shields.io/github/license/OneIdentity/SafeguardDevOpsService.svg)](https://github.com/OneIdentity/SafeguardDevOpsService/blob/master/LICENSE)

# Safeguard Secrets Broker for DevOps Powershell Module

One Identity Safeguard Secrets Broker module and scripting resources.

-----------

<p align="center">
<i>Check out our <a href="samples">samples</a> to get started scripting to Safeguard Secrets Broker!</i>
</p>

-----------

## Support

One Identity open source projects are supported through [One Identity GitHub issues](https://github.com/OneIdentity/SafeguardDevOpsService/issues) and the [One Identity Community](https://www.oneidentity.com/community/). This includes all scripts, plugins, SDKs, modules, code snippets or other solutions. For assistance with any One Identity GitHub project, please raise a new Issue on the [One Identity GitHub project](https://github.com/OneIdentity/SafeguardDevOpsService/issues) page. You may also visit the [One Identity Community](https://www.oneidentity.com/community/) to ask questions.  Requests for assistance made through official One Identity Support will be referred back to GitHub and the One Identity Community forums where those requests can benefit all users.

## Installation

This Powershell module is published to the
[PowerShell Gallery](https://www.powershellgallery.com/packages/safeguard-devops)
to make it as easy as possible to install using the built-in `Import-Module` cmdlet.
It can also be updated using the `Update-Module` to get the latest functionality.

NOTE: The Safeguard Secrets Broker Powershell module is dependent on the [safeguard-ps](https://www.powershellgallery.com/packages/safeguard-ps) cmdlets.  The safeguard-ps module must be installed before using the Safeguard Secrets Broker Powershell cmdlets.

By default, Powershell modules are installed for all users, and you need to be
running Powershell as an Administrator to install for all users.

```Powershell
> Install-Module safeguard-devops
```

Or, you can install them just for you using the `-Scope` parameter which will
never require Administrator permission:

```Powershell
> Install-Module safeguard-devops -Scope CurrentUser
```

## Upgrading

If you want to upgrade from the
[PowerShell Gallery](https://www.powershellgallery.com/packages/safeguard-devops)
you should use:

```Powershell
> Update-Module safeguard-devops
```

Or, for a specific user:

```Powershell
> Update-Module safeguard-devops -Scope CurrentUser
```

If you run into errors while upgrading make sure that you upgrade for all users
if the module was originally installed for all users.  If the module was originally
installed for just the current user, be sure to use the `-Scope` parameter to again
specify `CurrentUser` when running the `Update-Module` cmdlet.

## Prerelease Versions

To install a pre-release version of Safeguard-DevOps you need to use the latest version
of PowerShellGet if you aren't already. Windows comes with one installed, but you
want the newest and it requires the `-Force` parameter to get it.

If you don't have PowerShellGet, run:

```Powershell
> Install-Module PowerShellGet -Force
```

Then, you can install a pre-release version of Safeguard-DevOps by running:

```Powershell
> Install-Module -Name safeguard-devops -AllowPrerelease
```

## Getting Started

Once you have loaded the module, you can connect to the Safeguard Secrets Broker using the
`Connect-SgDevOps` cmdlet.  If you do not have SSL properly configured, you
must use the `-Insecure` parameter to avoid SSL trust errors.

Authentication in Safeguard is based on OAuth2.  In most cases the
`Connect-SgDevOps` cmdlet uses the Resource Owner Grant of OAuth2.

```Powershell
> Connect-SgDevOps -Insecure 192.168.123.123
(certificate, local)
Provider: local
Username: admin
Password: ********
Login Successful.
```

The `Connect-SgDevOps` cmdlet will create a session variable that includes
your access token and connection information.  This makes it easier to call
other cmdlets provided by the module.

Client certificate authentication is also available in `Connect-SgDevOps`.
This can be done either using a PFX certificate file or a SHA-1 thumbprint
of a certificate stored in the Current User personal certificate store.

Two-factor authentication can only be performed using the `-Gui` parameter,
so that the built-in secure token service can use the browser agent to
redirect you to multiple authentication providers.  This authentication
mechanism uses the Authorization Code Grant of OAuth2.

```Powershell
> Connect-SgDevOps -Insecure 192.168.123.123 -Gui
Login Successful.
```

Once you are logged in, you can call any cmdlet listed below.  For example:

```Powershell
> Get-SgDevOpsApplianceStatus
```

When you are finished, you can close the session or call the
`Disconnect-SgDevOps` cmdlet to invalidate and remove your access token.

## Discover Available cmdlets

Use the `Get-SgDevOpsCommand` to see what is available from the module.

Since there are so many cmdlets in Safeguard-DevOps you can use filters to find
exactly the cmdlet you are looking for.

For example:

```Powershell
> Get-SgDevOpsCommand Get Plugin

CommandType     Name                                               Version    Source
-----------     ----                                               -------    ------
Function        Get-SgDevOpsPlugin                                 1.0.152    safeguard-devops
Function        Get-SgDevOpsPluginSetting                          1.0.152    safeguard-devops
Function        Get-SgDevOpsPluginVaultAccount                     1.0.152    safeguard-devops
```

## Module Versioning

The version of Safeguard-DevOps mirrors the version of Safeguard Secrets
Broker for DevOps that it was developed and tested against.  However, the build
numbers (fourth number) should not be expected to match.

For Example:

Safeguard-DevOps 1.0.152 would correspond to Safeguard Secrets Broker 1.0.0.22435.

This does not mean that Safeguard-DevOps 1.0.152 won't work at all with
Safeguard Secrets Broker 1.0.0.22435.  For the most part the cmdlets will still work, but
you may occasionally come across things that are broken.

For the best results, please try to match the first two version numbers of
the Safeguard-DevOps module to the first two numbers of the Safeguard
Secrets Broker for Devops service that you are communicating with.  The most
important thing for Safeguard-DevOps is the version of the Safeguard
Secrets Broker Web API, which will never change between where only the third
and fourth numbers differ.

## Powershell cmdlets

The following cmdlets are currently supported.  More will be added to this
list over time.  Every cmdlet in the list supports `Get-Help` to provide
additional information as to how it can be called.

Please file GitHub Issues for cmdlets that are not working and to request
cmdlets for functionality that is missing.

The following list of cmdlets might not be complete.  To see everything that
Safeguard-DevOps can do, run:

```Powershell
> Get-SgDevOpsCommand
```

Please report anything you see from the output that is missing, and we will
update this list.

### Informational

- Get-SgDevOpsCommand

### Safeguard Secrets Broker

- Add-SgDevOpsMappedAssetAccount
- Clear-SgDevOpsAppliance
- Clear-SgDevOpsClientCertificate
- Clear-SgDevOpsConfiguration
- Clear-SgDevOpsSslCertificate
- Connect-SgDevOps
- Disable-SgDevOpsTlsValidation
- Disconnect-SgDevOps
- Enable-SgDevOpsTlsValidation
- Find-SgDevOpsAvailableAssetAccount
- Get-SgDevOpsApplianceStatus
- Get-SgDevOpsAvailableAssetAccount
- Get-SgDevOpsClientCertificate
- Get-SgDevOpsConfiguration
- Get-SgDevOpsMappedAssetAccount
- Get-SgDevOpsRegisteredAssetAccount
- Get-SgDevOpsSslCertificate
- Get-SgDevOpsStatus
- Get-SgDevOpsTlsValidation
- Get-SgDevOpsTrustedCertificate
- Initialize-SgDevOps
- Initialize-SgDevOpsAppliance
- Initialize-SgDevOpsConfiguration
- Install-SgDevOpsClientCertificate
- Install-SgDevOpsSslCertificate
- Install-SgDevOpsTrustedCertificate
- Invoke-SgDevOpsMethod
- Invoke-SgDevOpsRegisteredAccountSetup
- New-SgDevOpsCsr
- Register-SgDevOpsAssetAccount
- Remove-SgDevOpsMappedAssetAccount
- Remove-SgDevOpsTrustedCertificate
- Restart-SgDevOps
- Sync-SgDevOpsTrustedCertificate
- Unregister-SgDevOpsAssetAccount

### Account Monitor

- Disable-SgDevOpsMonitor
- Enable-SgDevOpsMonitor
- Get-SgDevOpsMonitor

### Plugins

- Get-SgDevOpsPlugin
- Get-SgDevOpsPluginSetting
- Get-SgDevOpsPluginVaultAccount
- Install-SgDevOpsPlugin
- Remove-SgDevOpsPlugin
- Set-SgDevOpsPluginSetting
- Set-SgDevOpsPluginVaultAccount
