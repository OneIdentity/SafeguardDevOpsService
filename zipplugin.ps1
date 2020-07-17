param (
    [Parameter(Mandatory=$true, Position=0)]
    [string]$projectdir,
    [Parameter(Mandatory=$true, Position=1)]
    [string]$builddir,
    [Parameter(Mandatory=$true, Position=2)]
    [string]$zipfilename
)

Write-Host "projectdir=${projectdir}"
Write-Host "builddir=${builddir}"
Write-Host "zipfilename=${zipfilename}"

if (Test-Path $zipfilename)
{
    Remove-Item $zipfilename
}

$pluginbindir = (Split-Path -Path $zipfilename -Parent)
New-Item -ItemType Directory -Force -Path $pluginbindir

Copy-Item -Path $projectdir\Manifest.json $builddir
Add-Type -Assembly System.IO.Compression.FileSystem
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
[System.IO.Compression.ZipFile]::CreateFromDirectory($builddir, $zipfilename, $compressionLevel, $false)
