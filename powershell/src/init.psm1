
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
API version for the Safeguard appliance. (default: 3)

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

    Write-Host -ForegroundColor Yellow "Before you begin ..."
    Write-Host "This cmdlet is works as a multi-step wizard to set up Secrets Broker to work with Safeguard."
    Write-Host "In order to be successful using this cmdlet, you need the following:"
    Write-Host " - Credentials for a user with administrative rights to create users and configure A2A in Safeguard"
    Write-Host "   i.e. *User* and *Security Policy* permissions"
    Write-Host " - PFX or PKCS#12 file to create a certificate user in Safeguard"
    Write-Host "   Safeguard must already trust the issuer of the certificate you want to upload"
    Write-Host "Optionally, you might also want to have the following:"
    Write-Host " - PFX or PKCS#12 file to install as TLS certificate for Secrets Broker"
    Write-Host " - Asset and account names from Safeguard that you would like to use with Secrets Broker"
    Write-Host " - ZIP files for one or more plugins that you would like Secrets Broker to use"
    Write-Host "   Secrets Broker requires at least one plugin in order to push secrets"
    Write-Host "   On Windows, no Secrets Broker plugins are installed by default."
    Write-Host "   In Docker, only the HashiCorp plugin is installed by default."
    Write-Host ""
    Write-Host "Press any key to continue or Ctrl-C to quit ..."
    Read-Host " "

    Write-Host -ForegroundColor Yellow "Associating Secrets Broker to trust Safeguard for authentication ..."
    Write-Host "Secrets Broker leverages Safeguard for authentication."
    Write-Host "  As connections are made you will be asked to validate TLS server certificates for Secrets Broker and Safeguard."
    Write-Host "  You may be asked for Appliance and login credentials to Safeguard."
    Write-Host "  You may be asked for ServiceAddress for Secrets Broker."
    $local:Status = (Initialize-SgDevOpsAppliance -ServiceAddress $ServiceAddress -ServicePort $ServicePort -ServiceApiVersion $ServiceApiVersion `
                                                  -Appliance $Appliance -ApplianceApiVersion $ApplianceApiVersion -Gui:$Gui -Insecure)
    if ($local:Status)
    {
        Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
        Write-Host -ForegroundColor Yellow "Connecting to Secrets Broker using Safeguard user ..."
        try
        {
            Connect-SgDevOps -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui
        }
        catch
        {
            Connect-SgDevOps -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui -Insecure
            Write-Host -ForegroundColor Magenta "You are not using a trusted TLS server certificate in Secrets Broker."
            Write-Host "You can fix this problem now by uploading a certificate with a private key."
            Write-Host "Another option is to fix this later a CSR with New-SgDevOpsCsr and Install-SgDevOpsSslCertificate cmdlets."
            $local:Confirmed = $true
            while ($local:Confirmed)
            {
                $local:Confirmed = (Get-Confirmation "Fix TLS server certificate" "Would you like to upload a PFX or PKCS#12 file now?" `
                                                     "Fix certificate." "Skip this step.")
                if ($local:Confirmed)
                {
                    try
                    {
                        Install-SgDevOpsSslCertificate
                        $local:Success = $true
                        $local:Confirmed = $false
                    }
                    catch
                    {
                        Write-Host -ForegroundColor Magenta "Operation failed."
                        Write-Host $_.Exception
                    }
                }
            }
            if ($local:Success)
            {
                Write-Host -ForegroundColor Yellow "Reconnecting to Secrets Broker using Safeguard user ..."
                Connect-SgDevOps -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui
            }
        }

        if ($SgDevOpsSession)
        {
            Write-Host "Secrets Broker needs to initialize some configuration in Safeguard in order to establish permanent communications."
            Write-Host -ForegroundColor Yellow "Configuring Secrets Broker instance certificate user in Safeguard ..."
            Write-Host "Secrets Broker uses a Safeguard certicate user for A2A communications."
            Write-Host "The easiest way to configure this is to upload a certificate with a private key now to continue with this wizard."
            Write-Host "Alternatively, you can use New-SgDevOpsCsr and Install-SgDevOpsClientCertificate to set everything up manually."
            $local:Confirmed = $true
            while ($local:Confirmed)
            {
                $local:Confirmed = (Get-Confirmation "Configure TLS client certificate user" "Would you like to upload a PFX or PKCS#12 file now?" `
                                                     "Configure certificate user." "Do everything manually instead.")
                if ($local:Confirmed)
                {
                    try
                    {
                        $local:CertificateUser = (Install-SgDevOpsClientCertificate)
                        $local:Confirmed = $false
                    }
                    catch
                    {
                        Write-Host -ForegroundColor Magenta "Operation failed."
                        Write-Host $_.Exception
                    }
                }
            }
            if ($local:CertificateUser)
            {
                Write-Host -ForegroundColor Yellow "Initializing configuration in Safeguard ..."
                Initialize-SgDevOpsConfiguration # this will throw an exception if it fails, otherwise continue with optional steps below

                # registered accounts
                Write-Host -ForegroundColor Yellow "Configure registered accounts for Secrets Broker ..."
                Write-Host "For security reasons, the asset accounts in Safeguard are not immediately made available to Secrets Broker."
                Write-Host "The next step is to find Safeguard asset accounts that you would like to use with Secrets Broker."
                $local:Confirmed = (Get-Confirmation "Configure registered accounts" "Would you like to configure registered accounts now?" `
                                                     "Configure now." "Skip this step.")
                if ($local:Confirmed)
                {

                }

                # plugins
                Write-Host -ForegroundColor Yellow "Install Secrets Broker plugins ..."
                Write-Host "Secrets Broker needs plugins to push secrets."
                $local:Confirmed = (Get-Confirmation "Install plugins" "Would you like to install plugins now?" `
                                                     "Install now." "Skip this step.")
                if ($local:Confirmed)
                {

                }

                # Fix TLS
                Write-Host -ForegroundColor Yellow "Fix TLS certificate validation with Safeguard ..."
                if (-not (Get-SgDevOpsTlsValidation))
                {
                    Write-Host -ForegroundColor Magenta "TLS certificate validation is not enabled between Secrets Broker and Safeguard."
                    Write-Host "Usually the easiest way to fix this is to synchronize trusted certificates from Safeguard to Secrets Broker."
                    $local:Confirmed = (Get-Confirmation "Synchronize trusted certificates" "Would you like to synchronize trusted certificates now?" `
                                                         "Synchronize." "Skip this step.")
                    if ($local:Confirmed)
                    {
                        Sync-SgDevOpsTrustedCertificate
                        Write-Host "Now you can try to enable TLS certificate validation between Secrets Broker and Safeguard."
                        Write-Host "If for some reason this doesn't work you can go back by running Disable-SgDevOpsTlsValidation."
                        $local:Confirmed = (Get-Confirmation "Enable TLS certificate validation" "Would you like to enable validation now?" `
                                                             "Enable." "Skip this step.")
                        if ($local:Confirmed)
                        {
                            Enable-SgDevOpsTlsValidation
                        }
                    }
                }
                else
                {
                    Write-Host "TLS certificate validation is already enabled."
                }
            }
        }
    }
}
