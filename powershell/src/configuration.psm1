<#
.SYNOPSIS
Generate and configure the Safeguard client configuration information that
the Secrets Broker will use.

.DESCRIPTION
Safeguard Secrets uses client certificate authentication and the A2A
service when accessing Safeguard to monitor account secret changes and
to pull secrets. It also proxies configuration requests to Safeguard
as the currently authenticated administrator user.

This cmdlet will add or modify some configuration stored in
Safeguard.  The client certificate that will be used to create the A2A
user in Safeguard can be uploaded as part of the this cmdlet or it can
be uploaded separately in the Install-SgDevOpsClientCertificate cmdlet.
If the certificate is installed separately, it does not need to be
included in this call.

.PARAMETER CertificateFile
The path to a certificate file (either PFX or PEM).

.PARAMETER Password
In the case of a PFX file including a private key, this parameter may be used
to specify a password to decrypt the PFX file.

.EXAMPLE
Initialize-SgDevOpsConfiguration clientcert.pfx

.EXAMPLE
Initialize-SgDevOpsConfiguration C:\clientcert.pem

.EXAMPLE
Initialize-SgDevOpsConfiguration .\path\to\clientcert.crt
#>
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

<#
.SYNOPSIS
Get the Safeguard client configuration information that is being used by
the Secrets Broker.

.DESCRIPTION
Safeguard Secrets uses client certificate authentication and the A2A
service when accessing Safeguard to monitor account secret changes and
to pull secrets. It also proxies configuration requests to Safeguard
as the currently authenticated administrator user.

This cmdlet get the current configuration for the Secrets Broker.

.EXAMPLE
Get-SgDevOpsConfiguration
#>
function Get-SgDevOpsConfiguration
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/Configuration"
}

<#
.SYNOPSIS
Delete the Safeguard client configuration being used by the Secrets Broker.

.DESCRIPTION
The Secrets Broker for DevOps uses client certificate authentication and
the A2A service to access Safeguard to monitor account secret changes
and to pull secrets. The Secrets Broker also proxies configuration
requests to Safeguard as the currently authenticated administrator user.

This endpoint will remove all A2A credential account registrations, the A2A
registration and the A2A user from Safeguard. It will also remove the
Secrets Broker configuration database and restart the service.

.PARAMETER Force
This option will force clearing the configuration without confirmation.

.EXAMPLE
Clear-SgDevOpsConfiguration
#>
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
    Write-Host -ForegroundColor Yellow "WARNING: The Secrets Broker configuration will be removed from $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
    if ($Force)
    {
        $local:Confirmed = $true
    }
    else
    {
        Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
        $local:Confirmed = (Get-Confirmation "Clear Secrets Broker Configuration" "Do you want to clear configuration from the Safeguard appliance?" `
                                             "Clear configuration." "Cancels this operation.")
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod DELETE "Safeguard/Configuration" -Parameters @{ confirm = "yes" }
        Write-Host "Configuration has been cleared."
        Write-Host "The Secrets Broker will restart, you must reinitialize using Initialize-SgDevOpsAppliance."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

<#
.SYNOPSIS
Get the available Safeguard asset accounts that can be registered with the
Secrets Broker.

.DESCRIPTION
This cmdlet returns the asset accounts from the associated Safeguard appliance
that can be registered with the Secrets Broker. The registration occurs by
adding the available asset accounts as retrievable accounts to the A2A
registration associated with the Secrets Broker. Adding and removing asset
account registrations should be done using the Secrets Broker DevOps.

(see Get-SgDevOpsRegisteredAssetAccount)
(see Register-SgDevOpsAssetAccount)

.EXAMPLE
Get-SgDevOpsAvailableAssetAccount
#>
function Get-SgDevOpsAvailableAssetAccount
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/AvailableAccounts"
}

<#
.SYNOPSIS
Search for the available Safeguard asset accounts that can be registered with the
Secrets Broker.

.DESCRIPTION
This cmdlet returns the asset accounts from the associated Safeguard appliance
that can be registered with the Secrets Broker. The registration occurs by
adding the available asset accounts as retrievable accounts to the A2A
registration associated with the Secrets Broker. Adding and removing asset
account registrations should be done using the Secrets Broker DevOps.

(see Get-SgDevOpsAvailableAssetAccount)
(see Get-SgDevOpsRegisteredAssetAccount)
(see Register-SgDevOpsAssetAccount)

.PARAMETER Search
A string to search for in the available asset account.

.PARAMETER Query
A string to pass as a filter in the query of the available asset accounts.
Available operators: eq, ne, gt, ge, lt, le, and, or, not, contains, ieq,
icontains, in [ {item1}, {item2}, etc], (). Use \ to escape quotes in strings.

.EXAMPLE
Find-SgDevOpsAvailableAssetAccount "gsmith"

.EXAMPLE
Find-SgDevOpsAvailableAssetAccount -Query "HasPassword eq true"
#>
function Find-SgDevOpsAvailableAssetAccount
{
    [CmdletBinding(DefaultParameterSetName="Search")]
    Param(
        [Parameter(Mandatory=$true,Position=0,ParameterSetName="Search")]
        [string]$SearchString,
        [Parameter(Mandatory=$true,Position=0,ParameterSetName="Query")]
        [string]$QueryFilter
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($PSCmdlet.ParameterSetName -eq "Search")
    {
        $local:Parameters = @{ q = $SearchString }
    }
    else
    {
        $local:Parameters = @{ filter = $QueryFilter }
    }

    Invoke-SgDevOpsMethod GET "Safeguard/AvailableAccounts" -Parameters $local:Parameters
}

<#
.SYNOPSIS
Get accounts registered with the Secrets Broker A2A registration.

.DESCRIPTION
The Secrets Broker uses the Safeguard A2A service to access secrets, monitor
account secret changes and to pull secrets. The Secrets Broker creates a
special A2A registration that contains registered accounts. Each account that
is registered with this A2A registration, will be monitored by the
Secrets Broker.

.EXAMPLE
Get-SgDevOpsRegisteredAssetAccount
#>
function Get-SgDevOpsRegisteredAssetAccount
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/A2ARegistration/RetrievableAccounts"
}

<#
.SYNOPSIS
Register accounts with the Secrets Broker A2A registration.

.DESCRIPTION
The Secrets Broker uses the Safeguard A2A service to access secrets, monitor
account secret changes and to pull secrets. The Secrets Broker creates a
special A2A registration that contains registered accounts. Each account that
is registered with this A2A registration, will be monitored by the
Secrets Broker.

The Asset, Account and Domain parameters will be used to resolve an
asset-account that will be added to the A2A registration. If an array of
asset-accounts is provided, the Asset, Account and Domain parameters
should omitted.

An array of asset-accounts can be acquired from Get-SgDevOpsAvailableAssetAccount

.PARAMETER Asset
The name of an asset.

.PARAMETER Account
The name of an account.

.PARAMETER Domain
The name of a domain that the asset-account belong to.

.PARAMETER AccountObjects
An array of asset-account objects to register with the A2A registration.
(see Get-SgDevOpsAvailableAssetAccount).

.EXAMPLE
Register-SgDevOpsAssetAccount -Asset MyServer -Account MyAccount

.EXAMPLE
Register-SgDevOpsAssetAccount -AccountObjects $MyAccounts

#>
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

<#
.SYNOPSIS
Unregister accounts with the Secrets Broker A2A registration.

.DESCRIPTION
The Secrets Broker uses the Safeguard A2A service to access secrets, monitor
account secret changes and to pull secrets. The Secrets Broker creates a
special A2A registration that contains registered accounts. Each account that
is registered with this A2A registration, will be monitored by the
Secrets Broker.

The Asset, Account and Domain parameters will be used to resolve an
asset-account that will be added to the A2A registration. If an array of
asset-accounts is provided, the Asset, Account and Domain parameters
should omitted.

An array of asset-accounts can be acquired from Get-SgDevOpsRegisteredAssetAccount

.PARAMETER Asset
The name of an asset.

.PARAMETER Account
The name of an account.

.PARAMETER Domain
The name of a domain that the asset-account belong to.

.PARAMETER AccountObjects
An array of account objects to unregister with the A2A registration.
(see Get-SgDevOpsRegisteredAssetAccount).

.EXAMPLE
Unregister-SgDevOpsAssetAccount -Asset MyServer -Account MyAccount

.EXAMPLE
Unregister-SgDevOpsAssetAccount -AccountObjects $MyAccounts

#>
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
