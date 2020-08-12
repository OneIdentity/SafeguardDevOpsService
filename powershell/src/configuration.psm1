function Initialize-SgDevOpsConfiguration
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$CertificateFile,
        [Parameter(Mandatory=$false, Position=1)]
        [SecureString]$Password
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\certificates.psm1" -Scope Local
    if (-not $CertificateFile)
    {
        try
        {
            $local:ClientCertificate = (Get-SgDevOpsClientCertificate)
            Write-Host "Using client certificate $($local:ClientCertificate.Subject) ($($local:ClientCertificate.Thumbprint))"
        }
        catch
        {
            Write-Host -ForegroundColor Magenta "No client certificate is currently configured, and one was not provided for the configuration."
            Write-Host "You must specify a client certificate using Install-SgDevOpsClientCertificate or pass a file to this cmdlet."
            throw "No client certificate provided"
        }
        Invoke-SgDevOpsMethod POST "Safeguard/Configuration" -JsonBody "{}"
    }
    else
    {
        # this internal function works because this endpoint has certificate upload request semantics
        # even though it doesn't return a new certificate as the response
        Install-CertificateViaApi -CertificateFile $CertificateFile -Password $Password -Url "Safeguard/Configuration"
    }
}

function Get-SgDevOpsConfiguration
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/Configuration"
}

function Clear-SgDevOpsConfiguration
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false)]
        [switch]$Force
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Status = (Get-SgDevOpsApplianceStatus)
    Write-Host -ForegroundColor Yellow "WARNING: The Safeguard DevOps Service configuration will be removed from $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
    if ($Force)
    {
        $local:Confirmed = $true
    }
    else
    {
        Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
        $local:Confirmed = (Get-Confirmation "Clear Safeguard DevOps Service Configuration" "Do you want to clear configuration from the Safeguard appliance?" `
                                             "Clear configuration." "Cancels this operation.")
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod DELETE "Safeguard/Configuration" -Parameters @{ confirm = "yes" }
        Write-Host "Configuration has been cleared."
        Write-Host "The DevOps service will restart, you must reinitialize using Initialize-SgDevOpsAppliance."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

function Get-SgDevOpsAvailableAssetAccount
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/AvailableAccounts"
}

function Get-SgDevOpsRegisteredAssetAccount
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/A2ARegistration/RetrievableAccounts"
}

function Register-SgDevOpsAssetAccount
{
    [CmdletBinding(DefaultParameterSetName="Attributes")]
    Param(
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=0)]
        [string]$Asset,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=1)]
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
        $local:NewList += (Resolve-SgDevOpsAvailableAccount $Asset $Account -Domain $Domain)
    }
    else
    {
        $AccountObjects | ForEach-Object {
            $local:NewList += (Resolve-SgDevOpsAvailableAccount -Account $_)
        }
    }

    Invoke-SgDevOpsMethod PUT "Safeguard/A2ARegistration/RetrievableAccounts" -Body $local:NewList
}

function Unregister-SgDevOpsAssetAccount
{
    [CmdletBinding(DefaultParameterSetName="Attributes")]
    Param(
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=0)]
        [string]$Asset,
        [Parameter(ParameterSetName="Attributes", Mandatory=$true, Position=1)]
        [string]$Account,
        [Parameter(ParameterSetName="Attributes", Mandatory=$false)]
        [string]$Domain,
        [Parameter(ParameterSetName="Objects")]
        [object[]]$AccountObjects
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:RegisteredAccounts = (Get-SgDevOpsRegisteredAssetAccount)
    [object[]]$local:RemoveList = @()
    if ($PsCmdlet.ParameterSetName -eq "Attributes")
    {
        if ($Domain)
        {
            $local:RemoveList += ($local:RegisteredAccounts | Where-Object { $_.SystemName -ieq $Asset -and $_.AccountName -ieq $Account -and $_.DomainName -ieq $Domain})
        }
        else
        {
            $local:RemoveList += ($local:RegisteredAccounts | Where-Object { $_.SystemName -ieq $Asset -and $_.AccountName -ieq $Account })
        }
    }
    else
    {
        $AccountObjects | ForEach-Object {
            $local:Object = $_
            if ($local:Object.AccountId)
            {
                $local:RemoveList += ($local:RegisteredAccounts | Where-Object { $_.AccountId -eq $local:Object.AccountId })
            }
            else
            {
                # try to match available asset accounts (they have an Id rather than an AccountId)
                $local:RemoveList += ($local:RegisteredAccounts | Where-Object { $_.AccountId -eq $local:Object.Id })
            }
        }
    }

    if (-not $local:RemoveList)
    {
        throw "Unable to find specified asset accounts to unregister."
    }

    Invoke-SgDevOpsMethod DELETE "Safeguard/A2ARegistration/RetrievableAccounts" -Body $local:RemoveList

    # return the current list
    Get-SgDevOpsRegisteredAssetAccount
}
