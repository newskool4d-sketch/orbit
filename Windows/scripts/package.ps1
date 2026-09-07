. "$PSScriptRoot/common.ps1"
& "$PSScriptRoot/build.ps1"
& "$PSScriptRoot/test.ps1" -NativeUi
$OrbitRelease=Join-Path $OrbitWindowsRoot ('artifacts/Orbit.Windows-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
Push-Location $OrbitWindowsRoot
try {
    Invoke-OrbitNative $OrbitDotnet @('publish',$OrbitProject,'-c','Release','-r','win-x64','--self-contained','true','--no-restore','-o',$OrbitRelease,'--nologo')
    $OrbitPublishedExe=Join-Path $OrbitRelease 'Orbit.Windows.exe'
    Invoke-OrbitNative $OrbitPublishedExe @('--self-test')
    Invoke-OrbitNative $OrbitPublishedExe @('--ui-smoke')
    Copy-Item -LiteralPath (Join-Path $OrbitWindowsRoot 'README.md') -Destination $OrbitRelease
    Copy-Item -LiteralPath (Join-Path $OrbitWindowsRoot 'THIRD-PARTY-NOTICES.md') -Destination $OrbitRelease
    Copy-Item -LiteralPath (Join-Path $OrbitWindowsRoot 'VERIFICATION.md') -Destination $OrbitRelease
    Copy-Item -LiteralPath (Join-Path $OrbitWindowsRoot 'licenses') -Destination $OrbitRelease -Recurse
    $OrbitArchive="$OrbitRelease.zip"
    Compress-Archive -LiteralPath $OrbitRelease -DestinationPath $OrbitArchive
    (Get-FileHash -LiteralPath $OrbitArchive -Algorithm SHA256).Hash | Set-Content -LiteralPath "$OrbitArchive.sha256" -Encoding utf8
    Write-Output "Package ready: $OrbitArchive"
} finally {Pop-Location}
