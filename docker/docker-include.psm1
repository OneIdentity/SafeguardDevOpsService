function Get-SafeguardDockerFileName
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true,Position=0)]
        [string]$ImageType
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    switch ($ImageType)
    {
        # Ubuntu
        {$_ -ieq "ubuntu" -or $_ -ieq "ubuntu20.04"} {"Dockerfile_ubuntu20.04"}
        # Alpine
        {$_ -ieq "alpine" -or $_ -ieq "alpine3.12"} {"Dockerfile_alpine3.12"}
        default { throw "Invalid ImageType specified."}
    }
}

function Get-SafeguardDockerFile
{
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$false,Position=0)]
        [ValidateSet(
            "ubuntu","ubuntu20.04",
            "alpine","alpine3.12",
            IgnoreCase=$true)]
        [string]$ImageType = "alpine"
    )

    if (-not $PSBoundParameters.ContainsKey("ErrorAction")) { $ErrorActionPreference = "Stop" }
    if (-not $PSBoundParameters.ContainsKey("Verbose")) { $VerbosePreference = $PSCmdlet.GetVariableValue("VerbosePreference") }

    (Resolve-Path (Join-Path "docker" (Get-SafeguardDockerFileName $ImageType))).Path
}