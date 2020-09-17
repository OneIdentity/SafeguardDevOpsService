# Helper
function Get-FileName
{
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$InitialDirectory
    )
    [System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms") | Out-Null
    $local:OpenFileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $local:OpenFileDialog.InitialDirectory = $InitialDirectory
    $local:OpenFileDialog.Filter = "All files (*.*)| *.*"
    $local:OpenFileDialog.ShowDialog() | Out-Null
    $local:OpenFileDialog.Filename
}

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

    if (-not $SgDevOpsSession)
    {
        Write-Host -ForegroundColor Magenta "Run Connect-SgDevOps to initialize a session."
        throw "This cmdlet requires a connect session with the Secrets Broker"
    }

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
    Write-Host -ForegroundColor Yellow "This cmdlet is works as a multi-step wizard to set up Secrets Broker to work with Safeguard."
    Write-Host -ForegroundColor Yellow "In order to be successful using this cmdlet, you need the following:"
    Write-Host -ForegroundColor Cyan " - Credentials for a user with administrative rights to create users and configure A2A in Safeguard"
    Write-Host -ForegroundColor Cyan "   i.e. *User* and *Security Policy* permissions"
    Write-Host -ForegroundColor Cyan " - PFX or PKCS#12 file to create a certificate user in Safeguard"
    Write-Host -ForegroundColor Cyan "   Safeguard must already trust the issuer of the certificate you want to upload"
    Write-Host -ForegroundColor Yellow "Optionally, you might also want to have the following:"
    Write-Host -ForegroundColor Cyan " - PFX or PKCS#12 file to install as TLS certificate for Secrets Broker"
    Write-Host -ForegroundColor Cyan " - Asset and account names from Safeguard that you would like to use with Secrets Broker"
    Write-Host -ForegroundColor Cyan " - ZIP files for one or more plugins that you would like Secrets Broker to use"
    Write-Host -ForegroundColor Cyan "   Secrets Broker requires at least one plugin in order to push secrets"
    Write-Host -ForegroundColor Cyan "   On Windows, no Secrets Broker plugins are installed by default."
    Write-Host -ForegroundColor Cyan "   In Docker, only the HashiCorp plugin is installed by default."
    Write-Host ""
    Write-Host "Press any key to continue or Ctrl-C to quit ..."
    Read-Host " "

    Write-Host -ForegroundColor Yellow "Associating Secrets Broker to trust Safeguard for authentication ..."
    Write-Host -ForegroundColor Cyan "Secrets Broker leverages Safeguard for authentication."
    Write-Host -ForegroundColor Cyan "  As connections are made you will be asked to validate TLS server certificates for Secrets Broker and Safeguard."
    Write-Host -ForegroundColor Cyan "  You may be asked for Appliance for Safeguard."
    Write-Host -ForegroundColor Cyan "  You may be asked for ServiceAddress for Secrets Broker. (You can specify port with colon, e.g. server:443)"
    Write-Host -ForegroundColor Cyan "  You may be asked multiple times login credentials to Safeguard. (Use Connect-Safeguard to prevent this)"

    if (-not $Appliance -and -not $SafeguardSession)
    {
        $Appliance = (Read-Host "Appliance") ## Getting this set in this parent cmdlet provides better UX
    }
    if (-not $ServiceAddress)
    {
        $ServiceAddress = (Read-Host "ServiceAddress") ## Getting this set in this parent cmdlet provides better UX
    }
    $local:Status = (Initialize-SgDevOpsAppliance -ServiceAddress $ServiceAddress -ServicePort $ServicePort -ServiceApiVersion $ServiceApiVersion `
                                                  -Appliance $Appliance -ApplianceApiVersion $ApplianceApiVersion -Gui:$Gui -Insecure)
    if ($local:Status)
    {
        Write-Host "Successfully associated Secrets Broker ($ServiceAddress) to Safeguard ($Appliance)."
        Write-Host ""
        Write-Host ""
        Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
        Write-Host -ForegroundColor Yellow "Connecting to Secrets Broker using Safeguard user ..."
        try
        {
            Write-Host -ForegroundColor Cyan "This will be your connection for the rest of the steps in the wizard."
            Connect-SgDevOps -Insecure -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui
        }
        catch
        {
            Connect-SgDevOps -Insecure -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui -Insecure
            Write-Host -ForegroundColor Magenta "You are not using a trusted TLS server certificate in Secrets Broker."
            Write-Host -ForegroundColor Cyan "You can fix this problem now by uploading a certificate with a private key."
            Write-Host -ForegroundColor Cyan "Another option is to fix this later a CSR with New-SgDevOpsCsr and Install-SgDevOpsSslCertificate cmdlets."
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
                Connect-SgDevOps -Insecure -ServiceAddress $ServiceAddress -ServicePort $ServicePort -Gui:$Gui
            }
        }
        Write-Host ""
        Write-Host ""

        if ($SgDevOpsSession)
        {
            Write-Host -ForegroundColor Yellow "Configuring Secrets Broker instance certificate user in Safeguard ..."
            Write-Host -ForegroundColor Cyan "Secrets Broker needs to initialize some configuration in Safeguard in order to establish permanent communications."
            Write-Host -ForegroundColor Cyan "Secrets Broker uses a Safeguard certificate user for A2A communications."
            Write-Host -ForegroundColor Cyan "The easiest way to configure this is to upload a certificate with a private key now to continue with this wizard."
            Write-Host -ForegroundColor Cyan "Alternatively, you can use New-SgDevOpsCsr and Install-SgDevOpsClientCertificate to set everything up manually."
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
            Write-Host ""
            Write-Host ""

            if ($local:CertificateUser)
            {
                Write-Host -ForegroundColor Yellow "Initializing configuration in Safeguard ..."
                Initialize-SgDevOpsConfiguration # this will throw an exception if it fails, otherwise continue with optional steps below
                Write-Host ""

                # registered accounts
                Write-Host -ForegroundColor Yellow "Configure registered accounts for Secrets Broker ..."
                Write-Host -ForegroundColor Cyan "For security reasons, the asset accounts in Safeguard are not immediately made available to Secrets Broker."
                Write-Host -ForegroundColor Cyan "The next step is to find Safeguard asset accounts that you would like to use with Secrets Broker."
                $local:Confirmed = (Get-Confirmation "Configure registered asset accounts" "Would you like to configure registered asset accounts now?" `
                                                     "Configure now." "Skip this step.")
                if ($local:Confirmed)
                {
                    Invoke-SgDevOpsRegisteredAccountSetup
                    Write-Host ""
                    Write-Host -ForegroundColor Cyan "You are still going to need to map these registered asset accounts to individual plugins."
                    Write-Host -ForegroundColor Cyan "You can do this with Add-SgDevOpsMappedAssetAccount."
                }
                Write-Host ""
                Write-Host ""

                # plugins
                Write-Host -ForegroundColor Yellow "Install Secrets Broker plugins ..."
                Write-Host -ForegroundColor Cyan "Secrets Broker needs plugins to push secrets."
                $local:Plugins = (Get-SgDevOpsPlugin)
                if ($local:Plugins)
                {
                    Write-Host -ForegroundColor Cyan "The following plugin(s) are already installed:"
                    $local:Plugins.Name
                }
                else
                {
                    Write-Host -ForegroundColor Cyan "Currently there are no plugins installed."
                }
                $local:Confirmed = $true
                $local:Success = $false
                while ($local:Confirmed)
                {
                    if ($local:Success) { $local:Word = "another" } else { $local:Word = "a" }
                    $local:Confirmed = (Get-Confirmation "Install plugins" "Would you like to install $($local:Word) plugin now?" `
                                                         "Install now." "Skip this step.")
                    if ($local:Confirmed)
                    {
                        try
                        {
                            Install-SgDevOpsPlugin
                            $local:Status = $true
                        }
                        catch
                        {
                            Write-Host -ForegroundColor Magenta "Operation failed."
                            Write-Host $_.Exception
                        }
                    }
                }
                Write-Host ""
                Write-Host ""

                # Fix TLS
                Write-Host -ForegroundColor Yellow "Fix TLS certificate validation with Safeguard ..."
                if (-not (Get-SgDevOpsTlsValidation))
                {
                    Write-Host -ForegroundColor Magenta "TLS certificate validation is not enabled between Secrets Broker and Safeguard."
                    Write-Host -ForegroundColor Cyan "Usually the easiest way to fix this is to synchronize trusted certificates from Safeguard to Secrets Broker."
                    $local:Confirmed = (Get-Confirmation "Synchronize trusted certificates" "Would you like to synchronize trusted certificates now?" `
                                                         "Synchronize." "Skip this step.")
                    if ($local:Confirmed)
                    {
                        Sync-SgDevOpsTrustedCertificate
                        Write-Host -ForegroundColor Cyan "Now you can try to enable TLS certificate validation between Secrets Broker and Safeguard."
                        Write-Host -ForegroundColor Cyan "If for some reason this doesn't work you can try later by setting up trusted certificates and calling Enable-SgDevOpsTlsValidation."
                        $local:Confirmed = (Get-Confirmation "Enable TLS certificate validation" "Would you like to enable validation now?" `
                                                             "Enable." "Skip this step.")
                        if ($local:Confirmed)
                        {
                            try
                            {
                                Enable-SgDevOpsTlsValidation
                            }
                            catch
                            {
                                Write-Host -ForegroundColor Magenta "Operation failed."
                                Write-Host $_.Exception
                            }
                        }
                    }
                }
                else
                {
                    Write-Host -ForegroundColor Yellow "TLS certificate validation is already enabled."
                }
            }
            else
            {
                Write-Host -ForegroundColor Cyan "Without a configured Certificate user you cannot configure the rest of Secrets Broker."
            }
            Write-Host ""
            Write-Host ""
            Write-Host -ForegroundColor Yellow "End of initialization cmdlet."
            Disconnect-SgDevOps
        }
    }
}


<#
.SYNOPSIS
An interactive command that finds specific asset accounts, based on queries,
that should be registered with the Secrets Broker.  Once the asset accounts
have been found, the command will allow the user to confirm whether the accounts
should be registered.

.DESCRIPTION
This cmdlet will associate Secrets Broker with specific asset accounts in an
interactive way.

.EXAMPLE
Invoke-SgDevOpsRegisteredAccountSetup

#>
function Invoke-SgDevOpsRegisteredAccountSetup
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local

    Write-Host -ForegroundColor Cyan "Enter a search string to look for available asset accounts in Safeguard to register with Secrets Broker."
    Write-Host -ForegroundColor Cyan "There are two types of search strings:"
    Write-Host -ForegroundColor Cyan "  Query (q)  - Searches all text fields for the specified string"
    Write-Host -ForegroundColor Cyan "  Filter (f) - Filters on specific fields using operators: eq, ne, gt, ge, lt, le, and, or, not, contains, ieq, icontains, in"
    Write-Host -ForegroundColor Cyan "To specify a search string select the type by specify 'q' or 'f', followed by a ':' and then search string value."
    Write-Host "For example:"
    Write-Host "  Search String: q:oracle-adm  --  This searches all text fields for oracle-adm"
    Write-Host "  Search String: f:SystemName eq 'fake.addr.com' and Name eq 'admin'  --  This filters for admin account on fake.addr.com"
    Write-Host -ForegroundColor Cyan "Some properties that can be used in Filter are: Id, Name, DomainName, SystemId, SystemName, SystemNetworkAddress"
    Write-Host ""
    Write-Host ""
    $local:Confirmed = $true
    while ($local:Confirmed)
    {
        $local:AvailableAccounts = @()
        $local:SearchString = (Read-Host "Search String")
        $local:Pair = ($local:SearchString -split ":")
        if ($local:Pair.Length -ne 2 -or ($local:Pair[0] -ne "q" -and $local:Pair[0] -ne "f"))
        {
            Write-Host -ForegroundColor Magenta "Invalid search string, must start with 'q:' or 'f:'"
        }
        elseif ($local:Pair[0] -eq "q")
        {
            try
            {
                $local:AvailableAccounts = (Find-SgDevOpsAvailableAssetAccount -SearchString $local:Pair[1])
            }
            catch
            {
                Write-Host -ForegroundColor Magenta "Operation failed."
                Write-Host $_.Exception
            }
        }
        elseif ($local:Pair[0] -eq "f")
        {
            try
            {
                $local:AvailableAccounts = (Find-SgDevOpsAvailableAssetAccount -QueryFilter $local:Pair[1])
            }
            catch
            {
                Write-Host -ForegroundColor Magenta "Operation failed."
                Write-Host $_.Exception
            }
        }

        if ($local:AvailableAccounts)
        {
            Write-Host "Found $($local:AvailableAccounts.Count) asset accounts:"
            Write-Host ($local:AvailableAccounts | Format-Table | Out-String)
            $local:Confirmed = (Get-Confirmation "Register Asset Accounts" "Would you like to register these with Secrets Broker?" `
                                                 "Register." "Cancel.")
            if ($local:Confirmed)
            {
                Register-SgDevOpsAssetAccount -AccountObjects $local:AvailableAccounts
            }
        }
        else
        {
            Write-Host "No asset accounts found."
        }

        $local:Confirmed = (Get-Confirmation "Register Asset Accounts" "Would you like to continue finding and registering asset accounts?" `
                                             "Continue." "Stop.")
    }
}
