[CmdletBinding()]
Param(
    [Parameter(Mandatory=$false,Position=0)]
    [string]$ImageType = "alpine",
    [Parameter(Mandatory=$false)]
    [switch]$Rebuild,
    [Parameter(Mandatory=$false)]
    [int]$Port = 443,
    [Parameter(Mandatory=$false)]
    [ValidateScript({[bool]($_ -as [IPAddress])})]
    [string]$IPAddress,
    [Parameter(Mandatory=$false)]
    [switch]$DebugContainer
)

if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

if (-not $IPAddress)
{
    Write-Host "Determining Host IP address..."
    $IPAddress = (Get-NetIPConfiguration | Where-Object {$_.IPv4DefaultGateway -ne $null -and $_.NetAdapter.status -ne "Disconnected"}).IPv4Address.IPAddress
    Write-Host -ForegroundColor Green $IPAddress
}

Import-Module -Name "$PSScriptRoot\docker\docker-include.psm1" -Scope Local -Force

$ImageType = $ImageType.ToLower()
Get-SafeguardDockerFile $ImageType # Make sure the ImageType exists

if (-not (Get-Command "docker" -EA SilentlyContinue))
{
    throw "Unable to find docker command. Is docker installed on this machine?"
}

$ImageName = "oneidentity/safeguard-devops:$ImageType"
$ContainerName = "safeguard-devops-runtime"

if (-not $(docker images $ImageName -q))
{
    Write-Host "Building the image: $ImageName ..."
    & "$PSScriptRoot/invoke-docker-build.ps1" $ImageType
}
elseif ($Rebuild)
{
    Write-Host "Rebuilding the image: $ImageName ..."
    & "$PSScriptRoot/invoke-docker-build.ps1" $ImageType -Rebuild:$Rebuild
}

Write-Host "Clean up any old container named $ContainerName ..."
$Exists = (& docker ps -a -f name=$ContainerName -q)
if ($Exists)
{
    docker rm $ContainerName
}

Write-Host "Using Docker host IP address: $IPAddress"
if ($DebugContainer)
{
    & docker run --name "$ContainerName" -p "${Port}:4443" --env DOCKER_HOST_IP=$IPAddress --cap-add=NET_ADMIN -it "$ImageName" "/bin/bash"
}
else
{
    Write-Host "Running interactive container ($ContainerName) for $ImageName on port $Port ..."
    & docker run --name "$ContainerName" -p "${Port}:4443" --env DOCKER_HOST_IP=$IPAddress --cap-add=NET_ADMIN -it "$ImageName"
}



