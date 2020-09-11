[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TargetDir,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$VersionString,
    [Parameter(Mandatory=$true, Position=2)]
    [bool]$IsPrerelease
)

if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }

if (-not (Test-Path $TargetDir))
{
    Write-Host "Creating $TargetDir"
    New-Item -Path $TargetDir -ItemType Container -Force | Out-Null
}
$ModuleName = "safeguard-devops"
$Module = (Join-Path $PSScriptRoot "src\$ModuleName.psd1")

$CodeVersion = "99999.99999.99999"
$BuildVersion = "$($VersionString.Split(".")[0..1] -join ".").$($VersionString.Split(".")[3])"
Write-Host "Replacing CodeVersion: $CodeVersion with BuildVersion: $BuildVersion"
(Get-Content $Module -Raw).replace($CodeVersion, $BuildVersion) | Set-Content $Module

if (-not $IsPrerelease)
{
    Write-Host "Removing the prerelease tag in the manifest"
    (Get-Content $Module -Raw).replace("Prerelease = '-pre'", "#Prerelease = '-pre'") | Set-Content $Module
}
else
{
    Write-Host "The module will be marked as prerelease"
}

$ModuleDef = (Invoke-Expression -Command (Get-Content $Module -Raw))
if ($ModuleDef["ModuleVersion"] -ne $BuildVersion)
{
    throw "Did not replace code version properly, ModuleVersion is '$($ModuleDef["ModuleVersion"])' BuildVersion is '$BuildVersion'"
}

Write-Host "Installing '$ModuleName' v$($ModuleDef["ModuleVersion"]) to '$TargetDir'"
$ModuleDir = (Join-Path $TargetDir $ModuleName)
if (-not (Test-Path $ModuleDir))
{
    New-Item -Path $ModuleDir -ItemType Container -Force | Out-Null
}
$VersionDir = (Join-Path $ModuleDir $ModuleDef["ModuleVersion"])
if (-not (Test-Path $VersionDir))
{
    New-Item -Path $VersionDir -ItemType Container -Force | Out-Null
}
Copy-Item -Recurse -Path (Join-Path $PSScriptRoot "src\*") -Destination $VersionDir

Get-ChildItem -Recurse $ModuleDir
