$ErrorActionPreference='Stop'
$OrbitWindowsRoot=Split-Path $PSScriptRoot -Parent
$OrbitRepoRoot=Split-Path $OrbitWindowsRoot -Parent
$OrbitDevRoot=Join-Path $env:LOCALAPPDATA 'OrbitDevelopment'
$OrbitDotnet=Join-Path $OrbitDevRoot 'dotnet-10.0.400/dotnet.exe'
if(-not (Test-Path -LiteralPath $OrbitDotnet)){ $OrbitDotnet=(Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_CLI_HOME=Join-Path $OrbitDevRoot 'cli'
$env:NUGET_PACKAGES=Join-Path $OrbitDevRoot 'packages'
$OrbitProject=Join-Path $OrbitWindowsRoot 'src/Orbit.Windows/Orbit.Windows.csproj'
$OrbitExe=Join-Path $OrbitWindowsRoot 'src/Orbit.Windows/bin/Release/net10.0-windows/win-x64/Orbit.Windows.exe'
function Invoke-OrbitNative {
    param([string]$File,[string[]]$Arguments)
    & $File @Arguments | Out-Host
    if($LASTEXITCODE -ne 0){throw "Native command failed (exit $LASTEXITCODE)"}
}
