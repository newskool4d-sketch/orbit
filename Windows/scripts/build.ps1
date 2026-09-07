. "$PSScriptRoot/common.ps1"
Push-Location $OrbitWindowsRoot
try {
    Invoke-OrbitNative $OrbitDotnet @('restore',$OrbitProject,'--locked-mode')
    Invoke-OrbitNative $OrbitDotnet @('build',$OrbitProject,'-c','Release','--no-restore','--nologo')
} finally {Pop-Location}
