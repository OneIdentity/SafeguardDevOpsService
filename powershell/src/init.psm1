
<#
.SYNOPSIS
Get Secrets Broker status summary.

.DESCRIPTION
Summary information includes Safeguard Appliance association information,
Secrets Broker configuration, and list of plugins.

.EXAMPLE
Get-SgDevOpsStatus
#>
function Get-SgDevOpsStatus
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\configuration.psm1" -Scope Local
    Import-Module -Name "$PSScriptRoot\plugins.psm1" -Scope Local

    Write-Host "---Appliance Connection---"
    Write-Host (Get-SgDevOpsApplianceStatus | Format-List | Out-String)

    Write-Host "---Configuration---"
    Write-Host (Get-SgDevOpsConfiguration | Format-List | Out-String)

    Write-Host "---Plugins---"
    Write-Host (Get-SgDevOpsPlugin | Format-Table | Out-String)

    Write-Host "---Monitor---"
    Write-Host (Get-SgDevOpsMonitor | Format-Table | Out-String)
}

<#
.SYNOPSIS
An interactive command to initialize Secrets Broker and prepare it for use.

.DESCRIPTION
This cmdlet will associate Secrets Broker with a Safeguard appliance, configure
trusted root certificates, configure a Safeguard client certificate, and
initialize the A2A configuration in Safeguard that Secrets Broker will use.

.PARAMETER ServiceAddress
Network address (IP or DNS) of the Secrets Broker.  This value may also include
the port information delimited with a colon (e.g. ssbdevops.example.com:12345).

.PARAMETER ServicePort
Port information for connecting to the Secrets Broker. (default: 443)

.PARAMETER Appliance
Network address (IP or DNS) of the Safeguard appliance.

.PARAMETER Gui
Display Safeguard login window in a browser. Supports 2FA.

.PARAMETER ApplianceApiVersion
API version for the Safeguard Appliance. (default: 3)

.PARAMETER ServiceApiVersion
API version for the Secrets Broker. (default: 1)

.EXAMPLE
Initialize-SgDevOps localhost:443

.EXAMPLE
Initialize-SgDevOps ssbdevops.example.com:12345 -Gui
#>
function Initialize-SgDevOps
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$ServiceAddress,
        [Parameter(Mandatory=$false)]
        [int]$ServicePort,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$Appliance,
        [Parameter(Mandatory=$false)]
        [switch]$Gui,
        [Parameter(Mandatory=$false)]
        [int]$ApplianceApiVersion = 3,
        [Parameter(Mandatory=$false)]
        [int]$ServiceApiVersion = 1
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host -ForegroundColor Yellow "Associating Secrets Broker to trust SPP appliance for authentication ..."
    $local:Status = (Initialize-SgDevOpsAppliance -ServiceAddress $ServiceAddress -ServicePort $ServicePort -ServiceApiVersion $ServiceApiVersion `
                                                  -Appliance $Appliance -ApplianceApiVersion $ApplianceApiVersion -Gui:$Gui -Insecure)
    if ($local:Status)
    {
        Write-Host -ForegroundColor Yellow "Connecting to Secrets Broker using SPP user ..."
        Connect-SgDevOps -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui -Insecure:$Insecure

        # TODO: test whether insecure flag is necessary
        #       if so, walk the user through fixing it

        Write-Host -ForegroundColor Yellow "Configuring Secrets Broker instance account in SPP ..."

        # TODO: Configure SgDevOps user
    }
}
