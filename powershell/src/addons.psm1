<#
.SYNOPSIS
Get a list of all the installed addons or a specific addon by name.

.DESCRIPTION
Secrets Broker functionality can be extended via addons available from
One Identity. Each addon must be installed and configured individually.

This cmdlet lists all of the addons that have been installed if invoked
with no parameters along with the specific configuration parameters. If
an addon name is provided, it will return the configuration parameters
for the specific addon.

.PARAMETER AddonName
The name of an installed addon.

.EXAMPLE
Get-SgDevOpsAddon

.EXAMPLE
Get-SgDevOpsAddon -AddonName HashiCorpVault

#>
function Get-SgDevOpsAddon
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$AddonName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($AddonName)
    {
        Invoke-SgDevOpsMethod GET "Safeguard/Addons/$AddonName"
    }
    else
    {
        Invoke-SgDevOpsMethod GET "Safeguard/Addons"
    }
}


function Install-SgDevOpsAddon
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$AddonFile,
        [Parameter(Mandatory=$false)]
        [switch]$Restart,
        [Parameter(Mandatory=$false)]
        [switch]$Force
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    try
    {
        $AddonFile = (Resolve-Path $AddonFile)
        $local:Bytes = [System.IO.File]::ReadAllBytes($AddonFile)
    }
    catch
    {
        Write-Host -ForegroundColor Magenta "Unable to read addon file."
        Write-Host -ForegroundColor Red $_
        throw "Invalid addon file specified"
    }

    $local:Base64AddonData = [System.Convert]::ToBase64String($local:Bytes)
    Invoke-SgDevOpsMethod POST "Safeguard/Addons" -Parameters @{ restart = [bool]$Restart } -Body @{
            Base64AddonData = $local:Base64AddonData
        }

    Write-Host "Addon has been installed.  Call Get-SgDevOpsAddon to see installed addons."

    if ($Restart)
    {
        Write-Host "The Secrets Broker will restart, you must reconnect using Connect-SgDevOps."
    }
}

<#
.SYNOPSIS
Remove an installed addon along with its configuration.

.DESCRIPTION
Secrets Broker functionality can be extended via addons available from
One Identity. Each addon must be installed and configured individually.

This cmdlet removes a specific addin by name.

.PARAMETER AddonFile
The name of an installed addon.

.EXAMPLE
Remove-SgDevOpsAddon

.EXAMPLE
Remove-SgDevOpsAddon -AddonFile HashiCorpVault
#>
function Remove-SgDevOpsAddon
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$AddonName
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod DELETE "Safeguard/Addons/$AddonName"
}