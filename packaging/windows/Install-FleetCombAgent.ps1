param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$resolved = (Resolve-Path $ExecutablePath).Path
New-Service `
    -Name "FleetCombAgent" `
    -BinaryPathName "`"$resolved`"" `
    -DisplayName "FleetComb Agent" `
    -Description "Secure FleetComb Asset connectivity service." `
    -StartupType Automatic
Start-Service -Name "FleetCombAgent"
