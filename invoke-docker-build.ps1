[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false,Position=0)]
    [string]$ImageType = "alpine",
    [Parameter(Mandatory=$false,Position=1)]
    [string]$Version
)

if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

Import-Module -Name "$PSScriptRoot\docker\docker-include.psm1" -Scope Local -Force

$ImageType = $ImageType.ToLower()
$SafeguardDockerFile = (Get-SafeguardDockerFile $ImageType)

Write-Host $SafeguardDockerFile

if (-not (Get-Command "docker" -EA SilentlyContinue))
{
    throw "Unable to find docker command. Is docker installed on this machine?"
}

if ($Version)
{
    $Version = "$Version-"
}

ImageName="oneidentity/safeguard-devops:$Version$ImageType"

if (Invoke-Expression "docker images -q oneidentity/safeguard-ps:$ImageType")
{
    Write-Host "Cleaning up the old image: $ImageName ..."
    & docker rmi --force "$ImageName"
}

Write-Host "Building a new image: $ImageName ..."
& docker build --no-cache -t "$ImageName" -f "$SafeguardDockerFile" "$PSScriptRoot"
