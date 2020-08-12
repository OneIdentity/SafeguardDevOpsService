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

    $local:Bytes = [System.IO.File]::ReadAllBytes($PluginZipFile)

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
    $local:Plugin.Configuration[$SettingName] = $SettingValue

    Invoke-SgDevOpsMethod PUT "Plugins/$PluginName" -Body $local:Plugin
}

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

    $local:MappedAccounts = (Get-SgDevOpsRegisteredAssetAccount)
    [object[]]$local:RemoveList = @()
    if ($PsCmdlet.ParameterSetName -eq "Attributes")
    {
        if ($Domain)
        {
            $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.SystemName -ieq $Asset -and $_.AccountName -ieq $Account -and $_.DomainName -ieq $Domain})
        }
        else
        {
            $local:RemoveList += ($local:MappedAccounts | Where-Object { $_.SystemName -ieq $Asset -and $_.AccountName -ieq $Account })
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
