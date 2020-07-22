# Helpers
function Install-CertificateViaApi
{
    Param(
        [Parameter(Mandatory=$true)]
        [string]$CertificateFile,
        [Parameter(Mandatory=$false)]
        [SecureString]$Password,
        [Parameter(Mandatory=$true)]
        [string]$Url
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }
    Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local

    $local:CertificateContents = (Get-CertificateFileContents $CertificateFile)
    if (-not $CertificateContents)
    {
        throw "No valid certificate to upload"
    }

    if (-not $Password)
    {
        Write-Host "For no password just press enter..."
        $Password = (Read-host "Password" -AsSecureString)
    }
    $local:PasswordPlainText = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password))

    Write-Host "Uploading Certificate..."
    if ($local:PasswordPlainText)
    {
        $local:NewCertificate = (Invoke-SgDevOpsMethod POST $Url -Body @{
                Base64CertificateData = "$($local:CertificateContents)";
                Passphrase = "$($local:PasswordPlainText)"
            })
    }
    else
    {
        $local:NewCertificate = (Invoke-SgDevOpsMethod POST $Url -Body @{
                Base64CertificateData = "$($local:CertificateContents)"
            })
    }

    $local:NewCertificate
}

function Get-SgDevOpsSslCertificate
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/WebServerCertificate"
}


function Install-SgDevOpsSslCertificate
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=0)]
        [string]$CertificateFile,
        [Parameter(Mandatory=$false,Position=1)]
        [SecureString]$Password
    )

    Install-CertificateViaApi -CertificateFile $CertificateFile -Password $Password -Url "Safeguard/WebServerCertificate"

    Write-Host "The DevOps service will restart, you must reconnect using Connect-SgDevOps."
}

function Clear-SgDevOpsSslCertificate
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod DELETE "Safeguard/WebServerCertificate"

    Write-Host "The DevOps service will restart, you must reconnect using Connect-SgDevOps."
}

function Get-SgDevOpsClientCertificate
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod GET "Safeguard/ClientCertificate"
}

function Install-SgDevOpsClientCertificate
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=0)]
        [string]$CertificateFile,
        [Parameter(Mandatory=$false,Position=1)]
        [SecureString]$Password
    )

    Install-CertificateViaApi -CertificateFile $CertificateFile -Password $Password -Url "Safeguard/ClientCertificate"
}

function Clear-SgDevOpsClientCertificate
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod DELETE "Safeguard/ClientCertificate"
}

function New-SgDevOpsCsr
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [ValidateSet('Ssl', 'Client', IgnoreCase=$true)]
        [string]$CertificateType,
        [Parameter(Mandatory=$true, Position=1)]
        [string]$Subject,
        [Parameter(Mandatory=$false)]
        [ValidateSet(1024, 2048, 3072, 4096)]
        [int]$KeyLength = 2048,
        [Parameter(Mandatory=$false)]
        [string[]]$IpAddresses = $null,
        [Parameter(Mandatory=$false)]
        [string[]]$DnsNames = $null,
        [Parameter(Mandatory=$false,Position=2)]
        [string]$OutFile = "$CertificateType.csr"
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    $local:Parameters = @{
        subjectName = $Subject;
        size = $KeyLength
    }

    switch ($CertificateType)
    {
        "ssl" { $local:Parameters.certType = "WebSsl"; break }
        "client" { $local:Parameters.certType = "A2AClient"; break }
    }

    if ($PSBoundParameters.ContainsKey("IpAddresses"))
    {
        Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local
        $IpAddresses | ForEach-Object {
            if (-not (Test-IpAddress $_))
            {
                throw "$_ is not an IP address"
            }
        }
        $local:Parameters.sanIp = ($IpAddresses -join ",")
    }
    if ($PSBoundParameters.ContainsKey("DnsNames")) { $local:Parameters.sanDns = ($DnsNames -join ",") }

    $local:Csr = (Invoke-SgDevOpsMethod GET "Safeguard/CSR" -Parameters $local:Parameters)
    $local:Csr

    $local:Csr | Out-File -Encoding ASCII -FilePath $OutFile -NoNewline -Force
    Write-Host "`nCSR saved to '$OutFile'"
}

function Get-SgDevOpsTrustedCertificate
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Thumbprint
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    if ($Thumbprint)
    {
        Invoke-SgDevOpsMethod GET "Safeguard/TrustedCertificates/$Thumbprint"
    }
    else
    {
        Invoke-SgDevOpsMethod GET "Safeguard/TrustedCertificates"
    }
}

function Install-SgDevOpsTrustedCertificate
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=0)]
        [string]$CertificateFile
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Import-Module -Name "$PSScriptRoot\ps-utilities.psm1" -Scope Local

    $local:CertificateContents = (Get-CertificateFileContents $CertificateFile)
    if (-not $CertificateContents)
    {
        throw "No valid certificate to upload"
    }

    Write-Host "Uploading Certificate..."
    Invoke-SgDevOpsMethod POST "Safeguard/TrustedCertificates" -Parameters @{ importFromSafeguard = $false } -Body @{
        Base64CertificateData = "$($local:CertificateContents)"
    }

}

function Remove-SgDevOpsTrustedCertificate
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$Thumbprint
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    # no support for remove all in PowerShell

    Invoke-SgDevOpsMethod DELETE "Safeguard/TrustedCertificates/$Thumbprint"
}
