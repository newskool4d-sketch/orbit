. "$PSScriptRoot/common.ps1"

$qaBase=[IO.Path]::GetFullPath((Join-Path $OrbitDevRoot 'persistence'))
$qaRoot=Join-Path $qaBase ([Guid]::NewGuid().ToString('N'))
$oldRoot=$env:ORBIT_QA_DATA_ROOT
$oldPhase=$env:ORBIT_QA_PHASE
try {
    New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
    $env:ORBIT_QA_DATA_ROOT=$qaRoot
    $env:ORBIT_QA_PHASE='seed'
    Invoke-OrbitNative $OrbitExe @('--ui-persistence-smoke')

    $settingsPath=Join-Path $qaRoot 'settings.json'
    $ddayPath=Join-Path $qaRoot 'ddays.json'
    if(-not (Test-Path -LiteralPath $settingsPath) -or -not (Test-Path -LiteralPath $ddayPath)){throw 'Persistence seed did not create both isolated data files.'}
    $settingsHash=(Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash
    $ddayHash=(Get-FileHash -LiteralPath $ddayPath -Algorithm SHA256).Hash

    $env:ORBIT_QA_PHASE='verify'
    Invoke-OrbitNative $OrbitExe @('--ui-persistence-smoke')
    if((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHash -or (Get-FileHash -LiteralPath $ddayPath -Algorithm SHA256).Hash -ne $ddayHash){throw 'Persistence verification unexpectedly modified the saved state.'}
    [pscustomobject]@{test='restart-persistence-process';pass=$true;processes=2;readOnlyVerify=$true} | ConvertTo-Json -Compress | Out-Host
} finally {
    if($null -eq $oldRoot){Remove-Item Env:ORBIT_QA_DATA_ROOT -ErrorAction SilentlyContinue}else{$env:ORBIT_QA_DATA_ROOT=$oldRoot}
    if($null -eq $oldPhase){Remove-Item Env:ORBIT_QA_PHASE -ErrorAction SilentlyContinue}else{$env:ORBIT_QA_PHASE=$oldPhase}
    $resolved=[IO.Path]::GetFullPath($qaRoot)
    if($resolved.StartsWith($qaBase+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
        for($attempt=0;$attempt -lt 5 -and (Test-Path -LiteralPath $resolved);$attempt++){
            try{Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop}catch{Start-Sleep -Milliseconds 200}
        }
    }
}
