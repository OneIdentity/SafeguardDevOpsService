<#
.SYNOPSIS
Get the current state of all Secrets Broker monitoring.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet gets the current state of the A2A monitor and the
reverse flow monitor.

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
Enable all Secrets Broker monitoring.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet starts the A2A account monitor and the reverse flow polling.

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
Disable all Secrets Broker monitoring.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet stops the A2A account monitor and the reverse flow monitor.

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

<#
Set the current state of all Secrets Broker monitoring.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet sets the current state of the A2A account monitor and the
reverse flow monitor.

.PARAMETER EnableA2a
Enable or disable the A2A account monitoring.

.PARAMETER EnableReverseFlow
Enable or disable the reverse flow polling.

.PARAMETER PollIntervalReverseFlow
The polling interval in seconds (Default 60 second).

.EXAMPLE
Set-SgDevOpsReverseFlowMonitor -Enable $True -PollInterval 30
#>
function Set-SgDevOpsMonitor
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [boolean]$EnableA2a,
        [Parameter(Mandatory=$true, Position=1)]
        [boolean]$EnableReverseFlow,
        [Parameter(Mandatory=$false, Position=2)]
        [int]$PollIntervalReverseFlow
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($PollIntervalReverseFlow -eq 0) {
        $currentState = Invoke-SgDevOpsMethod GET "Monitor/ReverseFlow"
        $PollIntervalReverseFlow = $currentState.ReverseFlowPollingInterval
    }
    $local:Body = @{
        ReverseFlowMonitorState = @{
            Enabled = $EnableReverseFlow;
            ReverseFlowPollingInterval = $PollIntervalReverseFlow;
        }
        Enabled = $EnableA2a
    }

    Invoke-SgDevOpsMethod PUT "Monitor" -Body $local:Body
}

<#
.SYNOPSIS
Get the current state of the reverse flow monitor.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet gets the current state of the reverse flow account monitor.

.EXAMPLE
Get-SgDevOpsReverseFlowMonitor

#>
function Get-SgDevOpsReverseFlowMonitor
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Monitor/ReverseFlow"
}

<#
Set the current state of the reverse flow monitor.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet sets the current state of the reverse flow account monitor.

.PARAMETER Enable
Enable or disable the reverse flow monitor.

.PARAMETER PollInterval
The polling interval in seconds (Default 60 second).

.EXAMPLE
Set-SgDevOpsReverseFlowMonitor -Enable $True -PollInterval 30
#>
function Set-SgDevOpsReverseFlowMonitor
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [boolean]$Enable,
        [Parameter(Mandatory=$false, Position=1)]
        [int]$PollInterval
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($PollInterval -eq 0) {
        $currentState = Invoke-SgDevOpsMethod GET "Monitor/ReverseFlow"
        $PollInterval = $currentState.ReverseFlowPollingInterval
    }
    $local:Body = @{
        Enabled = $Enable;
        ReverseFlowPollingInterval = $PollInterval;
    }

    Invoke-SgDevOpsMethod PUT "Monitor/ReverseFlow" -Body $local:Body
}

<#
Starts a single cycle polling interval on demand.

.DESCRIPTION
The Secrets Broker monitors the associated Safeguard appliance for any
password change to any account that has been registered with the
Secrets Broker. It can also monitor third party vaults for credential
changes and push those changes back to the Safeguard appliance.

This cmdlet starts a single cycle polling interval on demand. Normal
monitoring can be enabled or disabled at the time that is cmdlet is called.

.EXAMPLE
Invoke-SgDevOpsReverseFlowPollNow
#>
function Invoke-SgDevOpsReverseFlowPollNow
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod POST "Monitor/ReverseFlow/PollNow"

    Write-Host "Successfully initiated a polling cycle."
}