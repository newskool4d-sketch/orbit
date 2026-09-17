param([switch]$NativeUi,[switch]$Credential,[switch]$SourceOnly)
. "$PSScriptRoot/common.ps1"
Push-Location $OrbitRepoRoot
try {
    Invoke-OrbitNative 'node' @('--test','tests/web.test.cjs','tests/platform.test.cjs','tests/windows-ui.test.cjs','tests/dday-integration.test.cjs')
    if([Environment]::Version.Major -ge 10){ & "$PSScriptRoot/test-dday-store.ps1" }
    elseif($SourceOnly){throw 'Source-only C# verification requires PowerShell on .NET 10.'}
    if($SourceOnly){return}
    if(-not (Test-Path -LiteralPath $OrbitExe)){throw 'Native binary missing. Run Windows/scripts/build.ps1 first.'}
    $binaryTime=(Get-Item -LiteralPath $OrbitExe).LastWriteTimeUtc
    $nativeSource=Get-ChildItem -LiteralPath (Join-Path $OrbitWindowsRoot 'src/Orbit.Windows') -File | Where-Object {$_.Extension -in '.cs','.csproj'}
    $resources=Get-ChildItem -LiteralPath (Join-Path $OrbitRepoRoot 'Resources') -File
    if($nativeSource | Where-Object {$_.LastWriteTimeUtc -gt $binaryTime}){
        throw 'Native binary predates current sources. Rebuild before native/UI tests; old-binary results are not current validation.'
    }
    $deployedResources=Join-Path (Split-Path $OrbitExe -Parent) 'Resources'
    foreach($resource in $resources){
        $deployed=Join-Path $deployedResources $resource.Name
        if(-not (Test-Path -LiteralPath $deployed) -or (Get-FileHash -LiteralPath $resource.FullName -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $deployed -Algorithm SHA256).Hash){
            throw "Deployed resource is missing or stale: $($resource.Name). Rebuild before native/UI tests."
        }
    }
    Invoke-OrbitNative $OrbitExe @('--self-test')
    Invoke-OrbitNative $OrbitExe @('--sqlite-test')
    if($Credential){Invoke-OrbitNative $OrbitExe @('--credential-test')}
    if($NativeUi){
        $env:ORBIT_QA_DIR=Join-Path $OrbitWindowsRoot 'artifacts/qa'
        try{Invoke-OrbitNative $OrbitExe @('--ui-smoke')}finally{Remove-Item Env:ORBIT_QA_DIR}
        & "$PSScriptRoot/test-restart-persistence.ps1"
    }
} finally {Pop-Location}
