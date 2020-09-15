# Make sure that safeguard-ps is installed
if (-not (Get-Module safeguard-ps)) { Import-Module safeguard-ps }
if (-not (Get-Module safeguard-ps))
{
    throw "safeguard-devops requires safeguard-ps.  Please using Install-Module to install safeguard-ps."
}
# Global session variable for login information
Remove-Variable -Name "SgDevOpsSession" -Scope Global -ErrorAction "SilentlyContinue"
New-Variable -Name "SgDevOpsSession" -Scope Global -Value $null
$MyInvocation.MyCommand.ScriptBlock.Module.OnRemove = {
    Set-Variable -Name "SgDevOpsSession" -Scope Global -Value $null -ErrorAction "SilentlyContinue"
}
# Make sure SSL is configured properly to use TLS instead
Edit-SslVersionSupport


# Helpers
function Out-SgDevOpsExceptionIfPossible
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=0)]
        [object]$ThrownException
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if (-not ([System.Management.Automation.PSTypeName]"Ex.SgDevOpsMethodException").Type)
    {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.Serialization;

namespace Ex
{
    public class SgDevOpsMethodException : System.Exception
    {
        public SgDevOpsMethodException()
            : base("Unknown SgDevOpsMethodException") {}
        public SgDevOpsMethodException(int httpCode, string httpMessage, string errorMessage, string errorJson)
            : base(httpCode + ": " + httpMessage + " -- " + errorMessage)
        {
            HttpStatusCode = httpCode;
            ErrorMessage = errorMessage;
            ErrorJson = errorJson;
        }
        public SgDevOpsMethodException(string message, Exception innerException)
            : base(message, innerException) {}
        protected SgDevOpsMethodException
            (SerializationInfo info, StreamingContext context)
            : base(info, context) {}
        public int HttpStatusCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorJson { get; set; }
    }
}
"@
    }
    $local:ExceptionToThrow = $ThrownException
    if ($ThrownException.Response)
    {
        Write-Verbose "---Response Status---"
        if ($ThrownException.Response | Get-Member StatusDescription -MemberType Properties)
        {
            $local:StatusDescription = $ThrownException.Response.StatusDescription
        }
        elseif ($ThrownException.Response | Get-Member ReasonPhrase -MemberType Properties)
        {
            $local:StatusDescription = $ThrownException.Response.ReasonPhrase
        }
        Write-Verbose "$([int]$ThrownException.Response.StatusCode) $($local:StatusDescription)"
        Write-Verbose "---Response Body---"
        if ($ThrownException.Response | Get-Member GetResponseStream -MemberType Methods)
        {
            $local:Stream = $ThrownException.Response.GetResponseStream()
            $local:Reader = New-Object System.IO.StreamReader($local:Stream)
            $local:Reader.BaseStream.Position = 0
            $local:Reader.DiscardBufferedData()
            $local:ResponseBody = $local:Reader.ReadToEnd()
            $local:Reader.Dispose()
        }
        elseif ($ThrownException.Response | Get-Member Content -MemberType Properties)
        { # different properties and methods on net core
            try
            {
                $local:ResponseBody = $ThrownException.Response.Content.ReadAsStringAsync().Result
            }
            catch {}
        }
        if ($local:ResponseBody)
        {
            Write-Verbose $local:ResponseBody
            try # try/catch is a workaround for this bug in PowerShell:
            {   # https://stackoverflow.com/questions/41272128/does-convertfrom-json-respect-erroraction
                $local:ResponseObject = (ConvertFrom-Json $local:ResponseBody) # -ErrorAction SilentlyContinue
            }
            catch
            {
                try
                {
                    $local:ResponseObject = (ConvertFrom-Json ($local:ResponseBody -replace '""','" "'))
                }
                catch {}
            }
            if ($local:ResponseObject.Message) # SgDevOps error
            {
                $local:Message = $local:ResponseObject.Message
                $local:ExceptionToThrow = (New-Object Ex.SgDevOpsMethodException -ArgumentList @(
                    [int]$ThrownException.Response.StatusCode, $local:StatusDescription,
                    $local:Message, $local:ResponseBody
                ))
            }
            elseif ($local:ResponseObject.errors) # validation error
            {
                $local:ExceptionToThrow = (New-Object Ex.SgDevOpsMethodException -ArgumentList @(
                    [int]$ThrownException.Response.StatusCode, $local:StatusDescription,
                    [string]($local:ResponseObject.errors." " -join ", "), $local:ResponseBody
                ))
            }
            elseif ($local:ResponseObject.error_description) # rSTS error
            {
                $local:ExceptionToThrow = (New-Object Ex.SgDevOpsMethodException -ArgumentList @(
                    [int]$ThrownException.Response.StatusCode, $local:StatusDescription,
                    $local:ResponseObject.error_description, $local:ResponseBody
                ))
            }
            else # ??
            {
                $local:ExceptionToThrow = (New-Object Ex.SgDevOpsMethodException -ArgumentList @(
                    [int]$ThrownException.Response.StatusCode, $local:StatusDescription,
                    "Unknown error", $local:ResponseBody
                ))
            }
        }
        else # ??
        {
            $local:ExceptionToThrow = (New-Object Ex.SgDevOpsMethodException -ArgumentList @(
                [int]$ThrownException.Response.StatusCode, $local:StatusDescription,
                "", "<unable to retrieve response content>"
            ))
        }
    }
    if ($ThrownException.Status -eq "TrustFailure")
    {
        Write-Host -ForegroundColor Magenta "To ignore SSL/TLS trust failure use the -Insecure parameter to bypass server certificate validation."
    }
    Write-Verbose "---Exception---"
    $ThrownException | Format-List * -Force | Out-String | Write-Verbose
    if ($ThrownException.InnerException)
    {
        Write-Verbose "---Inner Exception---"
        $ThrownException.InnerException | Format-List * -Force | Out-String | Write-Verbose
    }
    throw $local:ExceptionToThrow
}
function Resolve-ServiceAddressAndPort
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$ServiceAddress,
        [Parameter(Mandatory=$false, Position=1)]
        [int]$ServicePort
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if (-not $ServiceAddress)
    {
        if ($SgDevOpsSession -and $SgDevOpsSession.ServiceAddress)
        {
            $ServiceAddress = $SgDevOpsSession.ServiceAddress
        }
        else
        {
            $ServiceAddress = (Read-Host "ServiceAddress")
        }
    }

    if ($ServicePort)
    {
        $local:SpecifiedPort = $ServicePort
    }

    if ($ServiceAddress.ToCharArray() -contains ':')
    {
        $local:Values = $ServiceAddress.Split(":");
        $ServiceAddress = $local:Values[0]
        if ($local:SpecifiedPort -and $local:SpecifiedPort -ne $ServicePort)
        {
            throw "Specified different ports in ServiceAddress (${ServiceAddress}) and in ServicePort (${ServicePort})"
        }
        $ServicePort = [int]$local:Values[1]
    }

    if (-not $local:SpecifiedPort -and -not $ServicePort)
    {
        if ($SgDevOpsSession -and $SgDevOpsSession.ServicePort)
        {
            $ServicePort = $SgDevOpsSession.ServicePort
        }
        else
        {
            $ServicePort = 443 # default
        }
    }

    @{
        ServiceAddress = $ServiceAddress;
        ServicePort = $ServicePort
    }
}
function Get-TlsCertificateFromEndpoint
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$ServerAddress,
        [Parameter(Mandatory=$true, Position=1)]
        [int]$ServerPort
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Certificate = $null
    $local:TcpClient = New-Object -TypeName System.Net.Sockets.TcpClient
    try
    {
        $local:TcpClient.Connect($ServerAddress, $ServerPort)
        $local:TcpStream = $local:TcpClient.GetStream()

        $local:Callback = { param($s, $c, $ch, $e) return $true }

        $local:SslStream = New-Object -TypeName System.Net.Security.SslStream -ArgumentList @($local:TcpStream, $true, $local:Callback)
        try {
            $local:SslStream.AuthenticateAsClient('')
            $local:Certificate = $local:SslStream.RemoteCertificate
        } finally {
            $local:SslStream.Dispose()
        }
    }
    finally
    {
        $local:TcpClient.Dispose()
    }

    if ($local:Certificate)
    {
        if ($local:Certificate -isnot [System.Security.Cryptography.X509Certificates.X509Certificate2])
        {
            $local:Certificate = New-Object -TypeName System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList $Certificate
        }
        $local:Certificate
    }
    else
    {
        throw "Unable to get TLS server certificate information for ${ServerAddress}:${ServerPort}"
    }
}
function Confirm-TlsCertificateInformation
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$ServerName,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$ServerAddress,
        [Parameter(Mandatory=$true, Position=2)]
        [int]$ServerPort
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host -ForegroundColor Yellow "VALIDATE certificate information for $ServerName (${ServerAddress}:${ServerPort}):"
    $local:Certificate = (Get-TlsCertificateFromEndpoint $ServerAddress $ServerPort)
    Write-Host "Thumbprint: $($local:Certificate.Thumbprint)"
    Write-Host "-----BEGIN CERTIFICATE-----"
    Write-Host ([System.Convert]::ToBase64String($Certificate.RawData) -replace ".{64}","`$0`n")
    Write-Host "-----END CERTIFICATE-----"
    (Get-Confirmation "Validate $ServerName Certificate" "Do you want to trust this connection to ${ServerName}?" `
                      "Validate." "Cancels this operation.")
    Write-Host ""
    Write-Host ""
}
function Get-ApplianceToken
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Appliance,
        [Parameter(Mandatory=$false)]
        [switch]$Gui,
        [Parameter(Mandatory=$false)]
        [switch]$Insecure,
        [Parameter(Mandatory=$false)]
        [int]$ApplianceApiVersion = 3
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($SafeguardSession -and $SafeguardSession.AccessToken)
    {
        $SafeguardSession.AccessToken
    }
    else
    {
        if ($Gui)
        {
            (Connect-Safeguard $Appliance -Gui -Version $ApplianceApiVersion -Insecure:$Insecure -NoSessionVariable)
        }
        else
        {
            (Connect-Safeguard $Appliance -Version $ApplianceApiVersion -Insecure:$Insecure -NoSessionVariable)
        }
    }
}
function New-SgDevOpsUrl
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=3)]
        [string]$Url,
        [Parameter(Mandatory=$false)]
        [object]$Parameters
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($Parameters -and $Parameters.Length -gt 0)
    {
        $Url += "?"
        $Parameters.Keys | ForEach-Object {
            $Url += ($_ + "=" + [uri]::EscapeDataString($Parameters.Item($_)) + "&")
        }
        $Url = $local:Url -replace ".$"
    }
    $Url
}
function Invoke-WithoutBody
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [object]$HttpSession,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$Method,
        [Parameter(Mandatory=$true, Position=2)]
        [string]$Url,
        [Parameter(Mandatory=$true, Position=3)]
        [object]$Headers,
        [Parameter(Mandatory=$false)]
        [object]$Parameters,
        [Parameter(Mandatory=$false)]
        [string]$InFile,
        [Parameter(Mandatory=$false)]
        [string]$OutFile,
        [Parameter(Mandatory=$false)]
        [int]$Timeout
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $Url = (New-SgDevOpsUrl $Url -Parameters $Parameters)
    Write-Verbose "Url=$($Url)"
    Write-Verbose "Parameters=$(ConvertTo-Json -InputObject $Parameters)"
    if ($InFile)
    {
        Write-Verbose "File-based payload -- $InFile"
        Invoke-RestMethod -Method $Method -Headers $Headers -Uri $Url -InFile $InFile -OutFile $OutFile -TimeoutSec $Timeout -WebSession $HttpSession
    }
    else
    {
        Invoke-RestMethod -Method $Method -Headers $Headers -Uri $Url -OutFile $OutFile -TimeoutSec $Timeout -WebSession $HttpSession
    }
}
function Invoke-WithBody
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [object]$HttpSession,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$Method,
        [Parameter(Mandatory=$true, Position=2)]
        [string]$Url,
        [Parameter(Mandatory=$true, Position=3)]
        [object]$Headers,
        [Parameter(Mandatory=$false)]
        [object]$Parameters,
        [Parameter(Mandatory=$false)]
        [object]$Body,
        [Parameter(Mandatory=$false)]
        [object]$JsonBody,
        [Parameter(Mandatory=$false)]
        [string]$OutFile,
        [Parameter(Mandatory=$false)]
        [int]$Timeout
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:BodyInternal = $JsonBody
    if ($Body)
    {
        $local:BodyInternal = (ConvertTo-Json -Depth 20 -InputObject $Body)
    }
    $Url = (New-SgDevOpsUrl $Url -Parameters $Parameters)
    Write-Verbose "Url=$($Url)"
    Write-Verbose "Parameters=$(ConvertTo-Json -InputObject $Parameters)"
    Write-Verbose "---Request Body---"
    Write-Verbose "$($local:BodyInternal)"

    Invoke-RestMethod -Method $Method -Headers $Headers -Uri $Url `
            -Body ([System.Text.Encoding]::UTF8.GetBytes($local:BodyInternal)) `
            -OutFile $OutFile -TimeoutSec $Timeout -WebSession $HttpSession

}
function Invoke-Internal
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [object]$HttpSession,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$Method,
        [Parameter(Mandatory=$true, Position=2)]
        [string]$Url,
        [Parameter(Mandatory=$true, Position=3)]
        [object]$Headers,
        [Parameter(Mandatory=$false)]
        [object]$Parameters,
        [Parameter(Mandatory=$false)]
        [object]$Body,
        [Parameter(Mandatory=$false)]
        [object]$JsonBody,
        [Parameter(Mandatory=$false)]
        [string]$InFile,
        [Parameter(Mandatory=$false)]
        [string]$OutFile,
        [Parameter(Mandatory=$false)]
        [int]$Timeout
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    try
    {
        switch ($Method.ToLower())
        {
            {$_ -in "get","delete"} {
                if ($Body -or $JsonBody)
                {
                    Invoke-WithBody $HttpSession $Method $Url $Headers -Parameters $Parameters -Body $Body -JsonBody $JsonBody -OutFile $OutFile -Timeout $Timeout
                }
                else
                {
                    Invoke-WithoutBody $HttpSession $Method $Url $Headers -Parameters $Parameters -Timeout $Timeout
                }
                break
            }
            {$_ -in "put","post"} {
                if ($InFile)
                {
                    Invoke-WithoutBody $HttpSession $Method $Version $Url $Headers -Parameters $Parameters -InFile $InFile -OutFile $OutFile -Timeout $Timeout
                }
                else
                {
                    Invoke-WithBody $HttpSession $Method $Url $Headers -Parameters $Parameters -Body $Body -JsonBody $JsonBody -OutFile $OutFile -Timeout $Timeout
                }
                break
            }
        }
    }
    catch
    {
        Out-SgDevOpsExceptionIfPossible $_.Exception
    }
}


<#
.SYNOPSIS
Get the status of the Safeguard appliance associated with this Secrets Broker.

.DESCRIPTION
The status information includes Appliance ID, Appliance Name, Appliance Version,
Appliance State, Appliance Network Address, and API version.  It also includes
whether or not Secrets Broker has been instructed to ignore validation of the
Appliance's TLS server certificate.

If there is no associated Secrets Broker the response will contain empty values.

.PARAMETER ServiceAddress
Network address (IP or DNS) of the Secrets Broker.  This value may also include
the port information delimited with a colon (e.g. ssbdevops.example.com:12345).

.PARAMETER ServicePort
Port information for connecting to the Secrets Broker. (default: 443)

.PARAMETER Insecure
Whether or not to validate the Secrets Broker's TLS server certificate.

.PARAMETER ServiceApiVersion
API version for the Secrets Broker. (default: 1)

.EXAMPLE
Get-SgDevOpsApplianceStatus localhost -Insecure

.EXAMPLE
Get-SgDevOpsApplianceStatus ssbdevops.example.com:12345
#>
function Get-SgDevOpsApplianceStatus
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$ServiceAddress,
        [Parameter(Mandatory=$false, Position=1)]
        [int]$ServicePort,
        [Parameter(Mandatory=$false)]
        [switch]$Insecure,
        [Parameter(Mandatory=$false)]
        [int]$ServiceApiVersion = 1
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    try
    {
        Edit-SslVersionSupport
        if ($Insecure -or $SgDevOpsSession.Insecure)
        {
            Disable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
        $local:Resolved = (Resolve-ServiceAddressAndPort $ServiceAddress $ServicePort)
        $ServiceAddress = $local:Resolved.ServiceAddress
        $ServicePort = $local:Resolved.ServicePort
        $local:Url = "https://${ServiceAddress}:${ServicePort}/service/devops/v${ServiceApiVersion}/Safeguard"
        Invoke-RestMethod -Method GET -Uri $local:Url -Headers @{
                "Accept" = "application/json";
                "Content-type" = "application/json";
            }
    }
    catch
    {
        Out-SgDevOpsExceptionIfPossible $_.Exception
    }
    finally
    {
        if ($Insecure -or $SgDevOpsSession.Insecure)
        {
            Enable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
    }
}

<#
.SYNOPSIS
Get the current state of the Secrets Broker TLS certificate validation.

.DESCRIPTION
The Secrets Broker can be instructed to ignore TLS certificate validation
when interacting with the Safeguard appliance.  This cmdlet gets the current
state of the TLS certificate validation.

.EXAMPLE
Get-SgDevOpsTlsValidation

#>
function Get-SgDevOpsTlsValidation
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    (-not (Invoke-SgDevOpsMethod GET Safeguard).IgnoreSsl)
}

<#
.SYNOPSIS
Enables TLS certificate validation for the Secrets Broker.

.DESCRIPTION
The Secrets Broker can be instructed to ignore TLS certificate validation
when interacting with the Safeguard appliance.  This cmdlet enables the Secrets
Broker for TLS certificate validation.

.EXAMPLE
Enable-SgDevOpsTlsValidation

#>
function Enable-SgDevOpsTlsValidation
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Sg = (Invoke-SgDevOpsMethod GET Safeguard)
    $local:Sg.IgnoreSsl = $false
    Invoke-SgDevOpsMethod PUT Safeguard -Body $local:Sg
}

<#
.SYNOPSIS
Disables TLS certificate validation for the Secrets Broker.

.DESCRIPTION
The Secrets Broker can be instructed to ignore TLS certificate validation
when interacting with the Safeguard appliance.  This cmdlet disables the Secrets
Broker for TLS certificate validation.

.EXAMPLE
Disable-SgDevOpsTlsValidation

#>
function Disable-SgDevOpsTlsValidation
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Sg = (Invoke-SgDevOpsMethod GET Safeguard)
    $local:Sg.IgnoreSsl = $true
    Invoke-SgDevOpsMethod PUT Safeguard -Body $local:Sg
}

<#
.SYNOPSIS
Log into Secrets Broker in this Powershell session for the purposes of using
the API.

.DESCRIPTION
Secrets Broker relies on Safeguard for authentication and authorization.  This
cmdlet will authenticate the caller against the associated Safeguard appliance
and use that to establish a login session for the .

The other cmdlets in this module require that you first establish a login
session using this cmdlet.

Before you can establish a login session the first immediately after deployment
you need to associate the Secrets Broker with a Safeguard Appliance.  This can
be done using Initialize-SgDevOps, which will walk you through the process.

.PARAMETER ServiceAddress
Network address (IP or DNS) of the Secrets Broker.  This value may also include
the port information delimited with a colon (e.g. ssbdevops.example.com:12345).

.PARAMETER ServicePort
Port information for connecting to the Secrets Broker. (default: 443)

.PARAMETER Gui
Display Safeguard login window in a browser. Supports 2FA.

.PARAMETER Insecure
Whether or not to validate the Secrets Broker's TLS server certificate.

.PARAMETER ServiceApiVersion
API version for the Secrets Broker. (default: 1)

.EXAMPLE
Connect-SgDevOps localhost -Insecure

.EXAMPLE
Connect-SgDevOps ssbdevops.example.com:12345
#>
function Connect-SgDevOps
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$ServiceAddress,
        [Parameter(Mandatory=$false)]
        [int]$ServicePort,
        [Parameter(Mandatory=$false)]
        [switch]$Gui,
        [Parameter(Mandatory=$false)]
        [switch]$Insecure,
        [Parameter(Mandatory=$false)]
        [int]$ServiceApiVersion = 1
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Resolved = (Resolve-ServiceAddressAndPort $ServiceAddress $ServicePort)
    $ServiceAddress = $local:Resolved.ServiceAddress
    $ServicePort = $local:Resolved.ServicePort
    try
    {
        $local:Status = (Get-SgDevOpsApplianceStatus $ServiceAddress $ServicePort -Insecure:$Insecure -ServiceApiVersion $ServiceApiVersion)
    }
    catch
    {
        Write-Host -ForegroundColor Yellow "WARNING: This Secrets Broker configuration has an error, usually this is because of failed TLS server validation."
        Write-Host -ForegroundColor Magenta "You must run Initialize-SgDevOps or Initialize-SgDevOpsAppliance with -Insecure option to correct TLS server validation."
        throw
    }

    if (-not $local:Status.ApplianceId)
    {
        Write-Host -ForegroundColor Magenta "Run Initialize-SgDevOps to assocate to a Safeguard appliance."
        throw "This Secrets Broker is not associated with a Safeguard appliance"
    }
    $local:Token = (Get-ApplianceToken $local:Status.ApplianceAddress -Gui:$Gui -Insecure:$Insecure -ApplianceApiVersion $local:Status.ApiVersion)
    $local:OldProgressPreference = $ProgressPreference
    try
    {
        Edit-SslVersionSupport
        if ($Insecure)
        {
            Disable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
        $local:HttpSession = $null
        $local:Url = "https://${ServiceAddress}:${ServicePort}/service/devops/v${ServiceApiVersion}/Safeguard/Logon"
        $ProgressPreference = "SilentlyContinue"
        $local:Response = (Invoke-WebRequest -Method GET -UseBasicParsing -Uri $local:Url -Headers @{
                "Accept" = "application/json";
                "Content-type" = "application/json";
                "Authorization" = "spp-token ${local:Token}"
            } -SessionVariable HttpSession)

        Write-Verbose "Setting up the SafeguardSession variable"

        Set-Variable -Name "SgDevOpsSession" -Scope Global -Value @{
            "ServiceAddress" = $ServiceAddress;
            "ServicePort" = $ServicePort;
            "Insecure" = $Insecure;
            "AccessToken" = $local:Token;
            "Session" = $local:HttpSession;
            "Gui" = $Gui;
            "ServiceApiVersion" = $ServiceApiVersion
        }

        Write-Verbose $local:Response.Content

        Write-Host "Login Successful."
    }
    catch
    {
        Out-SgDevOpsExceptionIfPossible $_.Exception
    }
    finally
    {
        $ProgressPreference = $local:OldProgressPreference
        if ($Insecure)
        {
            Enable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
    }
}

<#
.SYNOPSIS
Call a method in the Secrets Broker API.

.DESCRIPTION
This utility is useful for calling the Secrets Broker API for testing or
scripting purposes. It provides a couple benefits over using curl.exe or
Invoke-RestMethod by handling authentication, composing the Url, parameters,
and body for the request.

This script is meant to be used with the Connect-SgDevOps cmdlet which
will create a login session so that it doesn't need to be passed to each call
to this cmdlet.  Call Disconnect-SgDevOps when finished.

.PARAMETER Method
HTTP method verb you would like to use: GET, PUT, POST, DELETE.

.PARAMETER RelativeUrl
Relative portion of the Url you would like to call starting after the version.

.PARAMETER Parameters
A hash table containing the HTTP query parameters to add to the Url.

.PARAMETER Body
A hash table containing an object to PUT or POST to the Url.

.PARAMETER JsonBody
A pre-formatted JSON string to PUT or Post to the URl.  If -Body is also
specified, this is ignored. It can sometimes be difficult to get arrays of
objects to behave properly with hashtables in Powershell.

.PARAMETER ExtraHeaders
A hash table containing additional headers to add to the request.

.EXAMPLE
Invoke-SgDevOpsMethod GET Safeguard/A2ARegistration/RetrievableAccounts

.EXAMPLE
Invoke-SgDevOpsMethod PUT Plugins/HashiCorpVault/Accounts -Body $accounts

.EXAMPLE
Invoke-SgDevOpsMethod DELETE Plugins/HashiCorpVault/Accounts -Parameters @{ removeAll = true }
#>
function Invoke-SgDevOpsMethod
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [ValidateSet("Get","Put","Post","Delete",IgnoreCase=$true)]
        [string]$Method,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$RelativeUrl,
        [Parameter(Mandatory=$false)]
        [HashTable]$Parameters,
        [Parameter(Mandatory=$false)]
        [object]$Body,
        [Parameter(Mandatory=$false)]
        [string]$JsonBody,
        [Parameter(Mandatory=$false)]
        [HashTable]$ExtraHeaders
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if (-not $SgDevOpsSession)
    {
        Write-Host -ForegroundColor Magenta "Run Connect-SgDevOps to initialize a session."
        throw "This cmdlet requires a connect session with the Secrets Broker"
    }

    try
    {
        Edit-SslVersionSupport
        if ($SgDevOpsSession.Insecure)
        {
            Disable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }

        $local:Url = "https://$($SgDevOpsSession.ServiceAddress):$($SgDevOpsSession.ServicePort)/service/devops/v$($SgDevOpsSession.ServiceApiVersion)/${RelativeUrl}"
        $local:Headers = @{
            "Accept" = "application/json";
            "Content-type" = "application/json";
        }
        foreach ($local:Key in $ExtraHeaders.Keys)
        {
            $local:Headers[$local:Key] = $ExtraHeaders[$local:Key]
        }

        Write-Verbose "---Request---"
        Write-Verbose "Headers=$(ConvertTo-Json -InputObject $local:Headers)"

        $local:Headers["Authorization"] = "spp-token $($SgDevOpsSession.AccessToken)"

        Invoke-Internal $SgDevOpsSession.Session $Method $local:Url $local:Headers -Parameters $Parameters -Body $Body -JsonBody $JsonBody
    }
    finally
    {
        if ($SgDevOpsSession.Insecure)
        {
            Enable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
    }
}

<#
.SYNOPSIS
Log out of a Secrets Broker in this Powershell session when finished using the
API.

.DESCRIPTION
This utility will end your login session and remove the PowerShell session
variable that was created by the Connect-SgDevOps cmdlet.

.EXAMPLE
Disconnect-SgDevOps
#>
function Disconnect-SgDevOps
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    try
    {
        if (-not $SgDevOpsSession)
        {
            Write-Host "Not logged in."
        }
        else
        {
            Invoke-SgDevOpsMethod GET "Safeguard/Logoff"
        }
        Write-Host "Log out Successful."
    }
    finally
    {
        Write-Host "Session variable removed."
        Set-Variable -Name "SgDevOpsSession" -Scope Global -Value $null
    }
}

<#
.SYNOPSIS
Associate this Secrets Broker with a Safeguard Appliance.

.DESCRIPTION
Associating a Secrets Broker with Safeguard allows it to be used for
authentication. Secrets Broker can then be configured to use the Safeguard A2A
API to synchronize secrets with target secret stores via plugins.

Before you can establish a login session the first immediately after deployment
you need to associate the Secrets Broker with a Safeguard Appliance.  The best
way to do that is to call Initialize-SgDevOps, which will walk you through the
process interactively.  Initialize-SgDevOps calls this cmdlet.

If a Safeguard login session has already been established using
Connect-Safeguard, then this cmdlet will reuse that connection, otherwise this
cmdlet will authenticate to the Safeguard appliance interactively.

.PARAMETER ServiceAddress
Network address (IP or DNS) of the Secrets Broker.  This value may also include
the port information delimited with a colon (e.g. ssbdevops.example.com:12345).

.PARAMETER ServicePort
Port information for connecting to the Secrets Broker. (default: 443)

.PARAMETER Appliance
Network address (IP or DNS) of the Safeguard appliance.

.PARAMETER Gui
Display Safeguard login window in a browser. Supports 2FA.

.PARAMETER Insecure
Whether or not to store the Insecure flag in the Safeguard Appliance connection
to ignore TLS server certificate validation. (IMPORTANT! This is not the same
as the -Insecure option in other cmdlets--this is communications between Secrets
Broker and Safeguard Appliance). It can be changed later using the
Enable-SgDevOpsTlsValidation cmdlet.

.PARAMETER Force
This option will force Secrets Broker to associate with another Safeguard
appliance even if it has already been associated.  This option wil also remove
any other prompts that might occur.

.PARAMETER ApplianceApiVersion
API version for the Safeguard Appliance. (default: 3)

.PARAMETER ServiceApiVersion
API version for the Secrets Broker. (default: 1)

.EXAMPLE
Initialize-SgDevOpsAppliance localhost -Insecure

.EXAMPLE
Initialize-SgDevOpsAppliance ssbdevops.example.com:12345 -Gui
#>
function Initialize-SgDevOpsAppliance
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

    $local:Resolved = (Resolve-ServiceAddressAndPort $ServiceAddress $ServicePort)
    $ServiceAddress = $local:Resolved.ServiceAddress
    $ServicePort = $local:Resolved.ServicePort

    try
    {
        # Initial check to status is always done with Insecure flag... out of box Secrets Broker uses a self-signed certificate
        $local:Status = (Get-SgDevOpsApplianceStatus $ServiceAddress $ServicePort -Insecure -ServiceApiVersion $ServiceApiVersion)
        if ($local:Status.ApplianceId)
        {
            Write-Host -ForegroundColor Yellow "WARNING: This Secrets Broker is currently associated with $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
            if ($Force)
            {
                $local:Confirmed = $true
            }
            else
            {
                $local:Confirmed = (Get-Confirmation "Initialize Secrets Broker" "Do you want to associate with a different Safeguard appliance?" `
                                                     "Initialize." "Cancels this operation.")
                Write-Host ""
                Write-Host ""
            }
        }
        else
        {
            # not associated yet
            $local:Confirmed = $true
        }
    }
    catch
    {
        Write-Host -ForegroundColor Yellow "WARNING: This Secrets Broker configuration has an error."
        Write-Host -ForegroundColor Magenta $_
        $local:Confirmed = (Get-Confirmation "Initialize Secrets Broker" "Do you want to associate with a different Safeguard appliance?" `
                                             "Initialize." "Cancels this operation.")
    }

    if ($local:Confirmed)
    {
        if ($Force -or (Confirm-TlsCertificateInformation "Secrets Broker" $ServiceAddress $ServicePort))
        {
            if (-not $Appliance)
            {
                if ($SafeguardSession -and $SafeguardSession.Appliance)
                {
                    $Appliance = $SafeguardSession.Appliance
                }
                else
                {
                    $Appliance = (Read-Host "Appliance")
                }
            }
            if ($Force -or (Confirm-TlsCertificateInformation "Safeguard Appliance" $Appliance 443))
            {
                $local:Token = (Get-ApplianceToken $Appliance -Gui:$Gui -Insecure:$Insecure -ApplianceApiVersion $ApplianceApiVersion)
                try
                {
                    Edit-SslVersionSupport
                    if ($Insecure)
                    {
                        Disable-SslVerification
                        if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
                    }

                    $local:Url = "https://${ServiceAddress}:${ServicePort}/service/devops/v${ServiceApiVersion}/Safeguard"
                    $local:Status = (Invoke-RestMethod -Method PUT -Uri $local:Url -Headers @{
                                            "Accept" = "application/json";
                                            "Content-type" = "application/json";
                                            "Authorization" = "spp-token ${local:Token}" } -Body @"
{
    "ApplianceAddress": "$Appliance",
    "ApiVersion": "$ApplianceApiVersion",
    "IgnoreSsl": $(([string]([bool]$Insecure)).ToLower())
}
"@)
                    $local:Status
                }
                finally
                {
                    if ($Insecure)
                    {
                        Enable-SslVerification
                        if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
                    }
                }
            }
            else
            {
                Write-Host -ForegroundColor Yellow "Operation canceled."
            }
        }
        else
        {
            Write-Host -ForegroundColor Yellow "Operation canceled."
        }
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

<#
.SYNOPSIS
Clear this Secrets Broker's association with a Safeguard Appliance.

.DESCRIPTION
Associating a Secrets Broker with Safeguard allows it to be used for
authentication. Secrets Broker can then be configured to use the Safeguard A2A
API to synchronize secrets with target secret stores via plugins.

Removing this configuration using this cmdlet will most likely break your
Secrets Broker.  To completely reset, use Clear-SgDevOps instead.

.PARAMETER Force
This option will force clearing the association without confirmation.

.EXAMPLE
Clear-SgDevOpsAppliance
#>
function Clear-SgDevOpsAppliance
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false)]
        [switch]$Force
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Status = (Get-SgDevOpsApplianceStatus)
    if ($local:Status.ApplianceId)
    {
        Write-Host -ForegroundColor Yellow "WARNING: This Secrets Broker is currently associated with $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
        if ($Force)
        {
            $local:Confirmed = $true
        }
        else
        {
            $local:Confirmed = (Get-Confirmation "Clear Secrets Broker" "Do you want to clear the association with this Safeguard appliance?" `
                                                 "Clear." "Cancels this operation.")
        }
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod DELETE "Safeguard" -Parameters @{ confirm = "yes" }
        Write-Host "Appliance information has been cleared."
        Write-Host "The Secrets Broker will restart, you must reinitialize using Initialize-SgDevOpsAppliance."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

<#
.SYNOPSIS
Request a restart of the Secrets Broker.

.DESCRIPTION
Restarting the Secrets Broker is sometimes necessary to ensure that plugins and
certificates are initialized properly after a configuration change.

.PARAMETER Force
This option will force restarting the Secrets Broker without confirmation.

.EXAMPLE
Restart-SgDevOps
#>
function Restart-SgDevOps
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false)]
        [switch]$Force
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host -ForegroundColor Yellow "WARNING: Restarting this Secrets Broker will require you to log in again."
    if ($Force)
    {
        $local:Confirmed = $true
    }
    else
    {
        $local:Confirmed = (Get-Confirmation "Restart Secrets Broker" "Do you want to restart this Secrets Broker?" `
                                             "Restart." "Cancels this operation.")
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod POST "Safeguard/Restart" -Parameters @{ confirm = "yes" }
        Write-Host "Secrets Broker has restarted, you must reconnect using Connect-SgDevOps."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}
