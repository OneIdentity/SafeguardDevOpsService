
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
        $local:NewCertificate = (Invoke-SgDevOpsMethod POST "Safeguard/WebServerCertificate" -Body @{
                Base64CertificateData = "$($local:CertificateContents)";
                Passphrase = "$($local:PasswordPlainText)"
            })
    }
    else
    {
        $local:NewCertificate = (Invoke-SgDevOpsMethod POST "Safeguard/WebServerCertificate" -Body @{
                Base64CertificateData = "$($local:CertificateContents)"
            })
    }

    $local:NewCertificate
}

function Clear-SgDevOpsSslCertificate
{
    [CmdletBinding()]
    Param(
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    Invoke-SgDevOpsMethod DELETE "Safeguard/WebServerCertificate"
}