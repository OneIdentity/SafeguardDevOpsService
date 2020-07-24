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

    <# form data --- this might be more efficient, needs work

    if (-not $SgDevOpsSession)
    {
        Write-Host -ForegroundColor Magenta "Run Connect-SgDevOps to initialize a session."
        throw "This cmdlet requires a connect session with the Safeguard DevOps Service"
    }

    $local:Boundary = [System.Guid]::NewGuid().ToString();
    $local:NewLine = "`r`n"

    $local:BodyLines = (
        "--$($local:Boundary)",
        "Content-Disposition: form-data; name=`"file`"; filename=`"temp.txt`"",
        "Content-Type: application/octet-stream$($local:NewLine)",
        [System.Text.Encoding]::UTF8.GetString($local:Bytes),
        "--$($local:NewLine)--$($local:NewLine)"
    ) -join $local:NewLine

    $local:Url =
    Invoke-RestMethod -Uri "" -Method Post -ContentType "multipart/form-data; boundary=`"$($local:Boundary)`"" -Body $local:BodyLines

    #>

    $local:Base64PluginData = [System.Convert]::ToBase64String($local:Bytes)
    Invoke-SgDevOpsMethod POST "Plugins" -Parameters @{ restart = [bool]$Restart } -Body @{
            Base64PluginData = $local:Base64PluginData
        }

    Write-Host "Plugin has been installed.  Call Get-SgDevOpsPlugin to see installed plugins."

    if ($Restart)
    {
        Write-Host "The DevOps service will restart, you must reconnect using Connect-SgDevOps."
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
    $local:AssetAccount = (Resolve-SgDevOpsAssetAccount $Asset $Account -Domain $Domain)[0]

    Invoke-SgDevOpsMethod PUT "Plugins/$PluginName/VaultAccount" -Body $local:AssetAccount
}

function Get-SgDevOpsPluginSetting
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$PluginName,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$SettingName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Plugin = (Get-SgDevOpsPlugin $PluginName)
    $local:Plugin.Configuration[$SettingName]
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
