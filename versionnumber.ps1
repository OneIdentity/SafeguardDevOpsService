[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$SourceDir,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$SemanticVersion,
    [Parameter(Mandatory=$true, Position=2)]
    [string]$BuildId
)

Write-Host "SemanticVersion = $SemanticVersion"
Write-Host "BuildId = $BuildId"

$local:BuildNumber = ($BuildId % 65535) # max value for version part on Windows is 65534
Write-Host "BuildNumber = $($local:BuildNumber)"

$local:VersionString = "${SemanticVersion}.$($local:BuildNumber)"
$local:TemplateVersion = "255.255.65534.65534"
Write-Host "VersionString = $($local:VersionString)"
Write-Host "TemplateVersion = $($local:TemplateVersion)"

Write-Host "Searching for AssemblyInfo.cs files in '$SourceDir'"
(Get-ChildItem -Recurse -Filter "AssemblyInfo.cs") | ForEach-Object {
    $local:Path = $_.FullName
    Write-Host "Replacing version information in '$($local:Path)'"
    (Get-Content $local:Path -Raw).replace($local:TemplateVersion, $local:VersionString) | Set-Content $local:Path

    Write-Output "*****"
    Get-Content $local:Path
    Write-Output "*****"
}

Write-Output "##vso[task.setvariable variable=VersionString;]$($local:VersionString)"
