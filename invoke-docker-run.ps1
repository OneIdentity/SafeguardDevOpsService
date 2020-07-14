[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false,Position=0)]
    [string]$ImageType = "alpine",
    [Parameter(Mandatory=$false)]
    [int]$Port = 443,
    [Parameter(Mandatory=$false)]
    [switch]$DebugContainer
)

if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

Import-Module -Name "$PSScriptRoot\docker\docker-include.psm1" -Scope Local -Force

$ImageType = $ImageType.ToLower()
Get-SafeguardDockerFile $ImageType # Make sure the ImageType exists

if (-not (Get-Command "docker" -EA SilentlyContinue))
{
    throw "Unable to find docker command. Is docker installed on this machine?"
}

$ImageName = "oneidentity/safeguard-devops:$ImageType"
$ContainerName = "safeguard-devops-runtime"

Write-Host "Rebuilding the image: $ImageName ..."
& "$PSScriptRoot/invoke-docker-build.ps1" $ImageType

Write-Host "Clean up any old container named $ContainerName ..."
$Exists = (& docker ps -a -f name=$ContainerName -q)
if ($Exists)
{
    docker rm $ContainerName
}

if ($DebugContainer)
{
    $AlternateCommand = "/bin/bash"
}

Write-Host "Running interactive container ($ContainerName) for $ImageName on port $Port ..."
& docker run --name "$ContainerName" -p "$Port:4443" --cap-add=NET_ADMIN -it "$ImageName" "$AlternateCommand"
