[CmdletBinding()]
Param(
)

if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

if (-not (Get-Module safeguard-ps)) { Import-Module safeguard-ps }
if (-not (Get-Module safeguard-ps))
{
    throw "This script requires safeguard-ps.  Please using Install-Module to install safeguard-ps."
}

if (-not $SafeguardSession)
{
    throw "This script requires a Safeguard session.  Please login using Connect-Safeguard."
}

$local:Filter = "PrimaryAuthenticationProviderName eq 'Certificate' and Description eq 'Safeguard User for Safeguard Secrets Broker for DevOps'"
$local:A2aUsers = (Find-SafeguardUser -QueryFilter $local:Filter -Fields Id,UserName,PrimaryAuthenticationIdentity)
Write-Host "Cleaning up A2A certificate users:"
Write-host ($local:A2aUsers | Format-Table | Out-String)
$local:A2aUsers | ForEach-Object {
    Remove-SafeguardUser $_
}

<#
$local:DevOps = (Invoke-SafeguardMethod Core GET DevOps/SecretsBrokers)
Write-Host "Cleaning up secrets broker entries:"
Write-Host ($local:DevOps | Format-Table Id,AssetId,AssetName | Out-String)
$local:DevOps | ForEach-Object {
    Invoke-SafeguardMethod Core DELETE "DevOps/SecretsBrokers/$($_.Id)"
}
#>

$local:Filter = "Name contains 'SafeguardDevOpsService'"
$local:Assets = (Find-SafeguardAsset -QueryFilter  $local:Filter -Fields Id,Name,NetworkAddress)
Write-Host "Cleaning up Secrets Broker Assets:"
Write-host ($local:Assets | Format-Table | Out-String)
$local:Assets | ForEach-Object {
    Remove-SafeguardAsset $_
}

$local:Partitions = ((Get-SafeguardAssetPartition) | Where-Object { $_.Name -match "SafeguardDevOpsService*" })
Write-Host "Cleaning up Secrets Broker Asset Partitions:"
Write-host ($local:Partitions | Format-Table Id,Name | Out-String)
$local:Partitions | ForEach-Object {
    Remove-SafeguardAssetPartition $_
}

$local:A2as = ((Get-SafeguardA2a) | Where-Object { $_.AppName -match "SafeguardDevOpsService*" })
Write-Host "Cleaning up A2A registrations:"
Write-host ($local:A2as | Format-Table Id,AppName | Out-String)
$local:A2as | ForEach-Object {
    Remove-SafeguardA2a $_
}
