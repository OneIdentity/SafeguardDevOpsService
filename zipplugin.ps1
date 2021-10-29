param (
    [Parameter(Mandatory=$true, Position=0)]
    [string]$projectdir,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$builddir,
    [Parameter(Mandatory=$true, Position=2)]
    [string]$outputPath,
    [Parameter(Mandatory=$false, Position=4)]
    [string]$buildId
)

Write-Host "projectdir=${projectdir}"
Write-Host "builddir=${builddir}"
Write-Host "outputPath=${outputPath}"
Write-Host "buildId=${buildId}"

New-Item -ItemType Directory -Force -Path $outputPath

Copy-Item -Path $projectdir\Manifest.json $builddir
$local:ManifestFile = (Join-Path $builddir "Manifest.json")

if ($buildId) {
    Write-Host "Replacing version marker in manifest file"
    $local:VersionMarker = ".9999"
    (Get-Content $local:ManifestFile -Raw).replace($local:VersionMarker, ".${buildId}") | Set-Content -Encoding UTF8 $local:ManifestFile
}


$local:manifestProperties = (Get-Content $local:ManifestFile -Raw) | ConvertFrom-Json
$local:zipfileName = $local:manifestProperties.Name
$local:version = $local:manifestProperties.Version
Write-Host "${local:ziplfileName} ${local:version}"

$local:zipfilePath = (Join-Path $outputPath "${local:zipfileName}-${local:version}.zip")
Write-Host "zip file output path=${local:zipfilePath}"

if (Test-Path $local:zipfilePath)
{
    Remove-Item $local:zipfilePath
}

Add-Type -Assembly System.IO.Compression.FileSystem
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
[System.IO.Compression.ZipFile]::CreateFromDirectory($builddir, $local:zipfilePath, $compressionLevel, $false)
