param (
    [Parameter(Mandatory=$true)]
    [string]$projectdir,
    [Parameter(Mandatory=$true)]
    [string]$builddir,
    [Parameter(Mandatory=$true)]
    [string]$zipfilename
)

Write-Host "projectdir=${projectdir}"
Write-Host "builddir=${builddir}"
Write-Host "zipfilename=${zipfilename}"

if (Test-Path $zipfilename)
{
    Remove-Item $zipfilename
}

Copy-Item -Path $projectdir\Manifest.json $builddir
Add-Type -Assembly System.IO.Compression.FileSystem
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
[System.IO.Compression.ZipFile]::CreateFromDirectory($builddir, $zipfilename, $compressionLevel, $false)
