<#
.SYNOPSIS
Get the available cmdlets from the safeguard-devops module.

.DESCRIPTION
This cmdlet can be used to determine what cmdlets are available from safeguard-devops.
To make it easier to find cmdlets you may specify up to three strings as matching criteria.

.PARAMETER Criteria1
A string to match against the name of the cmdlet.

.PARAMETER Criteria2
A string to match against the name of the cmdlet.

.PARAMETER Criteria3
A string to match against the name of the cmdlet.

.EXAMPLE
Get-SgDevOpsCommand

.EXAMPLE
Get-SgDevOpsCommand Get Account

.EXAMPLE
Get-SgDevOpsCommand plugin
#>
function Get-SgDevOpsCommand
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Criteria1,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$Criteria2,
        [Parameter(Mandatory=$false, Position=2)]
        [string]$Criteria3
    )

    $local:Commands = (Get-Command -Module 'safeguard-devops')
    if ($Criteria1) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria1 }) }
    if ($Criteria2) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria2 }) }
    if ($Criteria3) { $local:Commands = ($local:Commands | Where-Object { $_.Name -match $Criteria3 }) }
    $local:Commands
}
