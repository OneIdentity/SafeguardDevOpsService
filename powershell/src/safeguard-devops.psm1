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
    finally
    {
        if ($Insecure -or $SgDevOpsSession.Insecure)
        {
            Enable-SslVerification
            if ($global:PSDefaultParameterValues) { $PSDefaultParameterValues = $global:PSDefaultParameterValues.Clone() }
        }
    }
}

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
    $local:Status = (Get-SgDevOpsApplianceStatus $ServiceAddress $ServicePort -Insecure:$Insecure -ServiceApiVersion $ServiceApiVersion)
    if (-not $local:Status.ApplianceId)
    {
        Write-Host -ForegroundColor Magenta "Run Initialize-SgDevOps to assocate to a Safeguard appliance."
        throw "This Safeguard DevOps Service is not associated with a Safeguard appliance"
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
        $local:Response = (Invoke-WebRequest -Method GET -Uri $local:Url -Headers @{
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
        throw "This cmdlet requires a connect session with the Safeguard DevOps Service"
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

    # Initial check to status is always done with Insecure flag... out of box Safeguard DevOps Service uses a self-signed certificate
    $local:Resolved = (Resolve-ServiceAddressAndPort $ServiceAddress $ServicePort)
    $ServiceAddress = $local:Resolved.ServiceAddress
    $ServicePort = $local:Resolved.ServicePort
    $local:Status = (Get-SgDevOpsApplianceStatus $ServiceAddress $ServicePort -Insecure -ServiceApiVersion $ServiceApiVersion)
    if ($local:Status.ApplianceId)
    {
        Write-Host -ForegroundColor Yellow "WARNING: This Safeguard DevOps Service is currently associated with $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
        if ($Force)
        {
            $local:Confirmed = $true
        }
        else
        {
            $local:Confirmed = (Get-Confirmation "Initialize Safeguard DevOps Service" "Do you want to associate with a different Safeguard appliance?" `
                                                 "Initialize." "Cancels this operation.")
        }
    }
    else
    {
        # not associated yet
        $local:Confirmed = $true
    }

    if ($local:Confirmed)
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
                                    "Authorization" = "spp-token ${local:Token}"
                                } -Body @"
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
        Write-Host -ForegroundColor Yellow "WARNING: This Safeguard DevOps Service is currently associated with $($local:Status.ApplianceName) ($($local:Status.ApplianceAddress))."
        if ($Force)
        {
            $local:Confirmed = $true
        }
        else
        {
            $local:Confirmed = (Get-Confirmation "Clear Safeguard DevOps Service" "Do you want to clear the association with this Safeguard appliance?" `
                                                 "Clear." "Cancels this operation.")
        }
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod DELETE "Safeguard" -Parameters @{ confirm = "yes" }
        Write-Host "Appliance information has been cleared."
        Write-Host "The DevOps service will restart, you must reinitialize using Initialize-SgDevOpsAppliance."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

function Restart-SgDevOps
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false)]
        [switch]$Force
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Write-Host -ForegroundColor Yellow "WARNING: Restarting this Safeguard DevOps Service will require you to log in again."
    if ($Force)
    {
        $local:Confirmed = $true
    }
    else
    {
        $local:Confirmed = (Get-Confirmation "Restart Safeguard DevOps Service" "Do you want to restart this Safeguard DevOps Service?" `
                                             "Restart." "Cancels this operation.")
    }

    if ($local:Confirmed)
    {
        Invoke-SgDevOpsMethod POST "Safeguard/Restart" -Parameters @{ confirm = "yes" }
        Write-Host "Safeguard DevOps Service has restarted, you must reconnect using Connect-SgDevOps."
    }
    else
    {
        Write-Host -ForegroundColor Yellow "Operation canceled."
    }
}

function Get-SgDevOpsCommand
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Criteria1,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$Criteria2,
        [Parameter(Mandatory=$false, Position=2)]
        [string]$Criteria3
    )

    $local:Commands = (Get-Command -Module 'safeguard-devops')
    if ($Criteria1) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria1 }) }
    if ($Criteria2) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria2 }) }
    if ($Criteria3) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria3 }) }
    $local:Commands
}
