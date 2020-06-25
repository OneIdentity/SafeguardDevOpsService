param (
   [Parameter(Mandatory=$true)][string]$sourcedir,
   [Parameter(Mandatory=$true)][string]$zipfilename
)

Write-Host $sourcedir
Write-Host $zipfilename

if (Test-Path $zipfilename) {
  Remove-Item $zipfilename
}
Copy-Item -Path .\Manifest.json $sourcedir
Add-Type -Assembly System.IO.Compression.FileSystem
$compressionLevel = [System.IO.Compression.CompressionLevel]::Optimal
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourcedir, $zipfilename, $compressionLevel, $false)
