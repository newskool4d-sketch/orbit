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
    $identityRoots=@((Join-Path $OrbitRepoRoot 'Resources'),(Join-Path $OrbitWindowsRoot 'src/Orbit.Windows'),(Join-Path $OrbitWindowsRoot 'scripts'))
    $identityLines=foreach($root in $identityRoots){
        Get-ChildItem -LiteralPath $root -File -Recurse |
            Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} |
            ForEach-Object{"$($_.FullName.Substring($OrbitRepoRoot.Length+1).Replace('\','/')) $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)"}
    }
    $identityText=($identityLines|Sort-Object)-join "`n"
    $identityHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($identityText)))
    $baseCommit=(& git -C $OrbitRepoRoot rev-parse HEAD).Trim()
    $dirty=[bool]((& git -C $OrbitRepoRoot status --porcelain).Length)
    [ordered]@{packageId=(Split-Path $OrbitRelease -Leaf);builtAtUtc=(Get-Date).ToUniversalTime().ToString('o');baseCommit=$baseCommit;workingTreeDirty=$dirty;sourceManifestSha256=$identityHash;runtime='win-x64';selfContained=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OrbitRelease 'BUILD-INFO.json') -Encoding utf8
    $OrbitArchive="$OrbitRelease.zip"
    Compress-Archive -LiteralPath $OrbitRelease -DestinationPath $OrbitArchive
    (Get-FileHash -LiteralPath $OrbitArchive -Algorithm SHA256).Hash | Set-Content -LiteralPath "$OrbitArchive.sha256" -Encoding utf8
    Write-Output "Package ready: $OrbitArchive"
} finally {Pop-Location}
