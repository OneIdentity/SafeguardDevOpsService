<#
.SYNOPSIS
Get the current state of the A2A monitor.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker.

This cmdlet gets the current state of the A2A account monitor.

.EXAMPLE
Get-SgDevOpsMonitor

#>
function Get-SgDevOpsMonitor
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Monitor"
}

<#
.SYNOPSIS
Enable the A2A account monitor

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker.

This cmdlet starts the A2A account monitor.

.EXAMPLE
Enable-SgDevOpsMonitor
#>
function Enable-SgDevOpsMonitor
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:monitorState = New-Object psobject
    $local:monitorState | Add-Member -type NoteProperty -Name Enabled -Value true

    Invoke-SgDevOpsMethod POST "Monitor" -Body $local:monitorState
}

<#
.SYNOPSIS
Disable the A2A account monitor

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker.

This cmdlet stops the A2A account monitor.

.EXAMPLE
Disable-SgDevOpsMonitor
#>
function Disable-SgDevOpsMonitor
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:monitorState = New-Object psobject
    $local:monitorState | Add-Member -type NoteProperty -Name Enabled -Value false

    Invoke-SgDevOpsMethod POST "Monitor" -Body $local:monitorState
}
