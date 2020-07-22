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
