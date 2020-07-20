function Get-SgDevOpsStatus
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host "---Appliance Connection---"
    Write-Host (Get-SgDevOpsApplianceStatus | Format-List | Out-String)

    Write-Host "---Configuration---"
    Write-Host (Invoke-SgDevOpsMethod GET "Safeguard/Configuration" | Format-List | Out-String)
}

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
        [switch]$Insecure,
        [Parameter(Mandatory=$false)]
        [switch]$Force,
        [Parameter(Mandatory=$false)]
        [int]$ApplianceApiVersion = 3,
        [Parameter(Mandatory=$false)]
        [int]$ServiceApiVersion = 1
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host -ForegroundColor Yellow "Associating Safeguard DevOps Service to trust SPP appliance for authentication ..."
    Initialize-SgDevOpsAppliance -ServiceAddress $ServiceAddress -ServicePort $ServicePort -ServiceApiVersion $ServiceApiVersion `
                                 -Appliance $Appliance -ApplianceApiVersion $ApplianceApiVersion -Gui:$Gui -Insecure:$Insecure -Force:$Force

    Write-Host -ForegroundColor Yellow "Connecting to Safeguard DevOps Service using SPP user ..."
    Connect-SgDevOps -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui -Insecure:$Insecure

    # TODO: test whether insecure flag is necessary
    #       if so, walk the user through fixing it

    Write-Host -ForegroundColor Yellow "Configuring Safeguard DevOps Service instance account in SPP ..."

    # TODO: Configure SgDevOps user
}