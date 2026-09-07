param([switch]$NativeUi,[switch]$Credential)
. "$PSScriptRoot/common.ps1"
Push-Location $OrbitRepoRoot
try {
    Invoke-OrbitNative 'node' @('--test','tests/web.test.cjs','tests/platform.test.cjs','tests/windows-ui.test.cjs')
    Invoke-OrbitNative $OrbitExe @('--self-test')
    Invoke-OrbitNative $OrbitExe @('--sqlite-test')
    if($Credential){Invoke-OrbitNative $OrbitExe @('--credential-test')}
    if($NativeUi){
        $env:ORBIT_QA_DIR=Join-Path $OrbitWindowsRoot 'artifacts/qa'
        try{Invoke-OrbitNative $OrbitExe @('--ui-smoke')}finally{Remove-Item Env:ORBIT_QA_DIR}
    }
} finally {Pop-Location}
