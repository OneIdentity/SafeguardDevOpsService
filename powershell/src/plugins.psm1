<#
.SYNOPSIS
Get a list of all registered plugins or a specific plugin by name.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credential information to a specific third party vault. Each plugin must be
installed and configured individually.

This cmdlet lists all of the plugins that have been installed along with
the specific configuration parameters if invoked with no parameters.  If
a plugin name is provided, it will return the configuration parameters
for the specific plugin.

.PARAMETER PluginName
The name of an installed plugin.

.EXAMPLE
Get-SgDevOpsPlugin

.EXAMPLE
Get-SgDevOpsPlugin -PluginName HashiCorpVault

#>
function Get-SgDevOpsPlugin
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$PluginName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($PluginName)
    {
        Invoke-SgDevOpsMethod GET "Plugins/$PluginName"
    }
    else
    {
        Invoke-SgDevOpsMethod GET "Plugins"
    }
}

<#
.SYNOPSIS
Upload and install a new plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credential information to a specific third party vault. Each plugin must be
installed and configured individually.

The plugin must be a zip compressed file. The plugin is installed into
the \ProgramData\SafeguardDevOpsService\ExternalPlugins folder.

If a new plugin is being installed, restarting the service may not be
necessary. However, if an existing plugin is being upgraded, the service
does not have the ability to unload a loaded plugin. Therefore all plugin
updates will be installed to a staging folder. The next time that the
Secrets Broker service is restarted, all staged plugins will be moved
to the external plugin folder and loaded. To restart automatically after
installing a plugin, set the restart flag to true.

.PARAMETER PluginZipFile
The full path and file name of the plugin to be installed.

.PARAMETER Restart
A boolean that indicates whether the Secrets Broker should be restarted
after installing the plugin.

.EXAMPLE
Install-SgDevOpsPlugin c:\my\plugin\path\pluginfile.zip

#>
function Install-SgDevOpsPlugin
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginZipFile,
        [Parameter(Mandatory=$false)]
        [switch]$Restart
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    try
    {
        $PluginZipFile = (Resolve-Path $PluginZipFile)
        $local:Bytes = [System.IO.File]::ReadAllBytes($PluginZipFile)
    }
    catch
    {
        Write-Host -ForegroundColor Magenta "Unable to read plugin zip file."
        Write-Host -ForegroundColor Red $_
        throw "Invalid plugin zip file specified"
    }

    $local:Base64PluginData = [System.Convert]::ToBase64String($local:Bytes)
    Invoke-SgDevOpsMethod POST "Plugins" -Parameters @{ restart = [bool]$Restart } -Body @{
            Base64PluginData = $local:Base64PluginData
        }

    Write-Host "Plugin has been installed.  Call Get-SgDevOpsPlugin to see installed plugins."

    if ($Restart)
    {
        Write-Host "The Secrets Broker will restart, you must reconnect using Connect-SgDevOps."
    }
}

<#
.SYNOPSIS
Delete the configuration for a specific plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credential information to a specific third party vault. Each plugin must be
installed and configured individually.

This cmdlet removes the configuration for a specific plugin by name and
unregisters the plugin from the Secrets Broker. However, it does not remove
the plugin from the \ProgramData\SafeguardDevOpsService\ExternalPlugins
folder. The plugin files must be manually removed from the ExternalPlugins
folder once Secrets Broker service has been stopped.

.PARAMETER PluginName
The name of an installed plugin.

.EXAMPLE
Remove-SgDevOpsPlugin

.EXAMPLE
Remove-SgDevOpsPlugin -PluginName HashiCorpVault
#>
function Remove-SgDevOpsPlugin
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod DELETE "Plugins/$PluginName"
}

<#
.SYNOPSIS
Get a list of accounts that are mapped to a vault plugin.

.DESCRIPTION
Secrets Broker uses individualized plugins that are capable of pushing
credential information to a specific third party vault. Accounts must be
mapped to each plugin so that the corresponding credential can be pushed
to the third party vault. By mapping an account to a plugin, the Secrets
Broker monitor will detect a password change for the mapped account and
push the new credential to the plugin.

.PARAMETER PluginName
The name of an installed plugin.

.EXAMPLE
Get-SgDevOpsPluginVaultAccount
#>
function Get-SgDevOpsPluginVaultAccount
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Plugins/$PluginName/VaultAccount"
}

<#
.SYNOPSIS
Map an account with the vault credential to a plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Each plugin usually has a
credential that is used to authenticate to the third party vault. This
credential must be stored in the Safeguard appliance and fetched at the
time when Safeguard Secrets Broker for DevOps needs to authenticate to the
third party vault.

The Asset and Account parameters will be used to resolve an asset-account
that should be used by the plugin to authenticate to the third party valult.
This asset-account will be mapped to the plugin and the credential that is
associated with the asset-account will be pulled from Safeguard at the time
when the plugin needs to authenticate to the third party vault.

(See get-SgDevOpsAvailableAssetAccount)

.PARAMETER PluginName
The name of an installed plugin.

.PARAMETER Asset
The name of an asset.

.PARAMETER Account
The name of an account.

.EXAMPLE
Set-SgDevOpsPluginVaultAccount -PluginName HashiCorpVault -Asset MyVaultAsset -Account MyVaultAccount
#>
function Set-SgDevOpsPluginVaultAccount
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(Mandatory=$true, Position=1)]
        [object]$Asset,
        [Parameter(Mandatory=$true, Position=2)]
        [object]$Account
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
    $local:AssetAccount = (Resolve-SgDevOpsAvailableAccount $Asset $Account -Domain $Domain)[0]

    Invoke-SgDevOpsMethod PUT "Plugins/$PluginName/VaultAccount" -Body $local:AssetAccount
}

<#
.SYNOPSIS
Get the settings for a specific plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Each plugin must be installed
and configured individually.

.PARAMETER PluginName
The name of an installed plugin.

.PARAMETER SettingName
The name of an plugin setting.

.EXAMPLE
Get-SgDevOpsPluginSetting -PluginName HashiCorpVault

.EXAMPLE
Get-SgDevOpsPluginSetting -PluginName HashiCorpVault -SettingName NetworkAddress
#>
function Get-SgDevOpsPluginSetting
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$SettingName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Plugin = (Get-SgDevOpsPlugin $PluginName)
    if ($SettingName)
    {
        $local:Plugin.Configuration[$SettingName]
    }
    else
    {
        $local:Plugin.Configuration
    }
}

<#
.SYNOPSIS
Update a setting for a plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Each plugin must be installed
and configured individually.

.PARAMETER PluginName
The name of an installed plugin.

.PARAMETER SettingName
The name of an plugin setting.

.PARAMETER SettingValue
New value for the plugin setting.

.EXAMPLE
Set-SgDevOpsPluginSetting -PluginName HashiCorpVault -SettingName NetworkAddress -SettingValue 192.168.1.1
#>
function Set-SgDevOpsPluginSetting
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$SettingName,
        [Parameter(Mandatory=$true, Position=2)]
        [string]$SettingValue
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Plugin = (Get-SgDevOpsPlugin $PluginName)
    $local:Plugin.Configuration.$SettingName = $SettingValue

    Invoke-SgDevOpsMethod PUT "Plugins/$PluginName" -Body $local:Plugin
}

<#
.SYNOPSIS
Get the list of accounts that are mapped to a vault plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Accounts must be mapped to each
plugin so that the corresponding credential can be pushed to the third party
vault. By mapping an account to a plugin, the Secrets Broker monitor will
detect a password change for the mapped account and push the new credential
to the plugin.

.PARAMETER PluginName
The name of an installed plugin.

.EXAMPLE
Get-SgDevOpsMappedAssetAccount -PluginName HashiCorpVault
#>
function Get-SgDevOpsMappedAssetAccount
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Plugins/$PluginName/Accounts"
}

<#
.SYNOPSIS
Map an account or an array of accounts to a vault plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Accounts must be mapped to each
plugin so that the corresponding credential can be pushed to the third party
vault. By mapping an account to a plugin, the Secrets Broker monitor will
detect a password change for the mapped account and push the new credential
to the plugin.

The Asset, Account and Domain parameters will be used to resolve an
asset-account that will be added to the A2A registration. If an array of
asset-accounts is provided, the Asset, Account and Domain parameters
should omitted.

.PARAMETER PluginName
The name of an installed plugin.

.PARAMETER Asset
The name of an asset.

.PARAMETER Account
The name of an account.

.PARAMETER Domain
The name of a domain that the asset-account belong to.

.PARAMETER AccountObjects
An array of account objects to map to the plugin.
(see Get-SgDevOpsRegisteredAssetAccount).

.EXAMPLE
Add-SgDevOpsMappedAssetAccount -PluginName HashiCorpVault -Asset MyServer -Account MyAccount

.EXAMPLE
Add-SgDevOpsMappedAssetAccount -PluginName HashiCorpVault -AccountObjects $MyAccounts

#>
function Add-SgDevOpsMappedAssetAccount
{
    [CmdletBinding(DefaultParameterSetName="Attributes")]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=1)]
        [string]$Asset,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=2)]
        [string]$Account,
        [Parameter(ParameterSetName="Attributes", Mandatory=$false)]
        [string]$Domain,
        [Parameter(ParameterSetName="Objects", Mandatory=$true)]
        [object[]]$AccountObjects
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
    [object[]]$local:NewList = @()
    if ($PsCmdlet.ParameterSetName -eq "Attributes")
    {
        $local:NewList += (Resolve-SgDevOpsRegisteredAccount $Asset $Account -Domain $Domain)
    }
    else
    {
        $AccountObjects | ForEach-Object {
            $local:NewList += (Resolve-SgDevOpsRegisteredAccount -Account $_)
        }
    }

    Invoke-SgDevOpsMethod PUT "Plugins/$PluginName/Accounts" -Body $local:NewList
}

<#
.SYNOPSIS
Remove an account or an array of accounts from a vault plugin.

.DESCRIPTION
The Secrets Broker uses individualized plugins that are capable of pushing
credentials to a specific third party vault. Accounts must be mapped to each
plugin so that the corresponding credential can be pushed to the third party
vault. By mapping an account to a plugin, the Secrets Broker monitor will
detect a password change for the mapped account and push the new credential
to the plugin.

The Asset, Account and Domain parameters will be used to resolve an
asset-account that will be added to the A2A registration. If an array of
asset-accounts is provided, the Asset, Account and Domain parameters
should omitted.

.PARAMETER PluginName
The name of an installed plugin.

.PARAMETER Asset
The name of an asset.

.PARAMETER Account
The name of an account.

.PARAMETER Domain
The name of a domain that the asset-account belong to.

.PARAMETER AccountObjects
An array of account objects to unmap from the plugin.
(see Get-SgDevOpsMappedAssetAccount).

.EXAMPLE
Remove-SgDevOpsMappedAssetAccount -PluginName HashiCorpVault -Asset MyServer -Account MyAccount

.EXAMPLE
Remove-SgDevOpsMappedAssetAccount -PluginName HashiCorpVault -AccountObjects $MyAccounts
#>
function Remove-SgDevOpsMappedAssetAccount
{
    [CmdletBinding(DefaultParameterSetName="Attributes")]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=1)]
        [string]$Asset,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=2)]
        [string]$Account,
        [Parameter(ParameterSetName="Attributes", Mandatory=$false)]
        [string]$Domain,
        [Parameter(ParameterSetName="Objects")]
        [object[]]$AccountObjects
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:MappedAccounts = (Get-SgDevOpsMappedAssetAccount $PluginName)
    [object[]]$local:RemoveList = @()
    if ($PsCmdlet.ParameterSetName -eq "Attributes")
    {
        if ($Domain)
        {
            $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.AssetName -ieq $Asset -and $_.AccountName -ieq $Account -and $_.DomainName -ieq $Domain})
        }
        else
        {
            $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.AssetName -ieq $Asset -and $_.AccountName -ieq $Account })
        }
    }
    else
    {
        $AccountObjects | ForEach-Object {
            $local:Object = $_
            if ($local:Object.AccountId)
            {
                $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.AccountId -eq $local:Object.AccountId })
            }
            else
            {
                # try to match available asset accounts (they have an Id rather than an AccountId)
                $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.AccountId -eq $local:Object.Id })
            }
        }
    }

    if (-not $local:RemoveList)
    {
        throw "Unable to find specified mapped asset accounts to remove."
    }

    Invoke-SgDevOpsMethod DELETE "Plugins/$PluginName/Accounts" -Body $local:RemoveList

    # return the current list
    Get-SgDevOpsMappedAssetAccount $PluginName
}
