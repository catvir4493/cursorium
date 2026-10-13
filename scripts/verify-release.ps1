param([switch]$IsolatedUpgrade,[switch]$CrossVolume,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.3.0')
$ErrorActionPreference = 'Stop'
$frostRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $frostRoot
if($IsolatedUpgrade){
    $taskBase=if($CrossVolume){Join-Path $frostRoot ('artifacts/CursoriumUpgradeTemp-'+[guid]::NewGuid().ToString('N'))}else{[IO.Path]::GetTempPath()}
    if($CrossVolume){New-Item -ItemType Directory -Path $taskBase | Out-Null;[IO.File]::WriteAllText((Join-Path $taskBase 'cursorium-test-base.marker'),'CURSORIUM isolated upgrade tests');if([IO.Path]::GetPathRoot($taskBase) -eq [IO.Path]::GetPathRoot([IO.Path]::GetTempPath())){throw 'Cross-volume verification needs the project and Windows TEMP on different volumes.'}}
    $taskSandbox=Join-Path $taskBase ('CursoriumUpgradeTest-'+[guid]::NewGuid().ToString('N'))
    $taskProgram=Join-Path $taskSandbox 'Program'
    $taskData=Join-Path $taskSandbox 'Data'
    $taskOutput=Join-Path $frostRoot 'artifacts/cute-v1.2/upgrade-isolated'
    New-Item -ItemType Directory -Force -Path $taskSandbox,$taskOutput | Out-Null
    $taskRealTarget=Join-Path $env:LOCALAPPDATA 'Programs/FrostWhiteComet'
    $taskRegistration=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet' -ErrorAction SilentlyContinue
    if($taskRegistration.InstallLocation){$taskRealTarget=$taskRegistration.InstallLocation}
    if(-not (Test-Path -LiteralPath (Join-Path $taskRealTarget 'frost-install.marker'))){throw 'A marked existing installation is required for this upgrade test.'}
    Copy-Item -LiteralPath $taskRealTarget -Destination $taskProgram -Recurse
    Copy-Item -LiteralPath (Join-Path $env:LOCALAPPDATA 'FrostWhiteComet') -Destination $taskData -Recurse
    # All private copies stay in the sandbox. They are never release/source assets.
    function Get-UpgradeHashes([string]$Root){
        $result=@{}
        foreach($item in Get-ChildItem -LiteralPath $Root -File -Recurse){
            $relative=$item.FullName.Substring($Root.Length+1)
            if($relative -notin @('setup-result.json','frost.log','frost.log.old')){$result[$relative]=(Get-FileHash -LiteralPath $item.FullName).Hash}
        }
        return $result
    }
    $taskBefore=Get-UpgradeHashes $taskData
    $taskKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors')
    try{$taskNativeBefore=@($taskKey.GetValueNames()|Sort-Object|ForEach-Object{@{name=$_;kind=$taskKey.GetValueKind($_).ToString();value=$taskKey.GetValue($_,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}})|ConvertTo-Json -Depth 8 -Compress}finally{$taskKey.Dispose()}
    function Run-UpgradeProcess([string]$Exe,[string]$Arguments){
        $start=[Diagnostics.ProcessStartInfo]::new($Exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.Arguments=$Arguments
        $process=[Diagnostics.Process]::Start($start)
        if(-not $process.WaitForExit(60000)-or $process.ExitCode -ne 0){throw 'Isolated upgrade process failed.'}
    }
    $taskKeyName='Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet-'+[IO.Path]::GetFileName($taskSandbox)
    try{
        $taskBaseArgument=if($CrossVolume){' --test-install-base "'+$taskBase+'"'}else{''}
        Run-UpgradeProcess (Join-Path $frostRoot "artifacts/CURSORIUM-Setup-$Version-Release.exe") ('--silent --no-launch --isolated-test --test-install-root "'+$taskSandbox+'"'+$taskBaseArgument)
        $taskAfter=Get-UpgradeHashes $taskData
        if($taskAfter.Count -ne $taskBefore.Count){throw 'Isolated upgrade changed protected data count.'}
        foreach($name in $taskBefore.Keys){if($taskAfter[$name] -ne $taskBefore[$name]){throw 'Isolated upgrade altered a protected resource.'}}
        $taskPortable=Join-Path $frostRoot 'artifacts/cursorium-release/portable';$taskMatches=0
        foreach($item in Get-ChildItem -LiteralPath $taskPortable -File -Recurse){
            $relative=$item.FullName.Substring($taskPortable.Length+1);$installed=Join-Path $taskProgram $relative
            if(-not (Test-Path -LiteralPath $installed)-or (Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $item.FullName).Hash){throw 'Isolated installed payload mismatch.'};$taskMatches++
        }
        Run-UpgradeProcess (Join-Path $taskProgram 'FrostWhiteComet.exe') ('--state-probe --isolated-test --test-data-root "'+$taskData+'" --report "'+(Join-Path $taskSandbox 'state.json')+'"')
        $state=Get-Content -LiteralPath (Join-Path $taskSandbox 'state.json') -Raw | ConvertFrom-Json
        if(@($state.themes | Where-Object {$_ -like 'cute-*'}).Count -ne 5){throw 'Installed catalog lacks Cute Collection.'}
        $taskRegistry=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($taskKeyName)
        try{if($taskRegistry.GetValue('DisplayVersion') -ne $Version){throw 'Isolated registration version mismatch.'}}finally{$taskRegistry.Dispose()}
        $taskKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors')
        try{$taskNativeAfter=@($taskKey.GetValueNames()|Sort-Object|ForEach-Object{@{name=$_;kind=$taskKey.GetValueKind($_).ToString();value=$taskKey.GetValue($_,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}})|ConvertTo-Json -Depth 8 -Compress}finally{$taskKey.Dispose()}
        if($taskNativeAfter -ne $taskNativeBefore){throw 'Isolated upgrade changed real native cursor state.'}
        @{passed=$true;version=$Version;protectedFiles=$taskBefore.Count;payloadFiles=$taskMatches;cuteThemes=5;nativeUnchanged=$true;crossVolume=[bool]$CrossVolume;privateSandbox=$taskSandbox;notes='Actual existing installation/data copy upgraded in a bounded marked sandbox; own shortcut/registry namespace, no production IPC/startup writes. Cross-volume flag uses D: while Windows TEMP stays C:. Uninstall not exercised.'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskOutput 'upgrade-report.json') -Encoding utf8
        Write-Host 'Isolated installed upgrade passed.'
    }finally{[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($taskKeyName,$false)}
    return
}
$frostTarget = Join-Path $env:LOCALAPPDATA 'Programs\FrostWhiteComet'
$frostRegistration=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet' -ErrorAction SilentlyContinue
if($frostRegistration.InstallLocation){$frostTarget=$frostRegistration.InstallLocation}
$frostData = Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
$frostShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'CURSORIUM.lnk'
$frostReportDir = Join-Path $frostRoot 'artifacts\verification'
if (Test-Path -LiteralPath $frostTarget) { throw 'Release verification requires no existing installation. Preserve your installation and test on another user account, or uninstall it deliberately first.' }
if (Test-Path -LiteralPath (Join-Path $frostData 'original-theme.json')) { throw 'Release verification refuses an active theme backup. Restore your original theme first.' }
New-Item -ItemType Directory -Path $frostReportDir -Force | Out-Null
$frostChecks = [Collections.Generic.List[object]]::new()
function Assert-Frost([bool]$Condition, [string]$Name) {
    $frostChecks.Add([pscustomobject]@{ name=$Name; passed=$Condition })
    if (-not $Condition) { throw $Name }
}
function Run-Frost([string]$Exe, [string[]]$Arguments, [int]$Timeout=60000) {
    $frostProcess = Start-Process -FilePath $Exe -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if (-not $frostProcess.WaitForExit($Timeout)) { throw "Timed out: $Exe" }
    Assert-Frost ($frostProcess.ExitCode -eq 0) "Exit code: $([IO.Path]::GetFileName($Exe)) $Arguments"
}
try {
    Run-Frost (Join-Path $frostRoot 'artifacts\CURSORIUM-Setup-1.0.0-Release.exe') @('--silent','--no-launch')
    Assert-Frost (Test-Path -LiteralPath (Join-Path $frostTarget 'FrostWhiteComet.exe')) 'Installed executable exists'
    Assert-Frost ((Get-ChildItem -LiteralPath (Join-Path $frostTarget 'Theme') -Filter '*.cur').Count -eq 15) 'All 15 external CUR resources installed'
    Assert-Frost ((Get-ChildItem -LiteralPath (Join-Path $frostTarget 'Theme') -Filter '*.ani').Count -eq 2) 'Both ANI resources installed'
    Assert-Frost (Test-Path -LiteralPath (Join-Path $frostTarget 'frost.ico')) 'External tray icon installed'
    Assert-Frost (Test-Path -LiteralPath $frostShortcut) 'Start menu shortcut installed'
    Assert-Frost (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet') 'Windows uninstall registration exists'
    Assert-Frost (-not (Test-Path -LiteralPath (Join-Path $frostData 'original-theme.json'))) 'Installer does not automatically change cursor theme'
    & dotnet run --project tests/Frost.Verify -c Release -- assets/cursors --install-roundtrip --engine-exe (Join-Path $frostTarget 'FrostWhiteComet.exe') 2>&1 | Tee-Object -FilePath (Join-Path $frostReportDir 'native-report.txt')
    Assert-Frost ($LASTEXITCODE -eq 0) 'Native cursor, install, restore, and theme-after-engine-exit verification'
    Run-Frost (Join-Path $frostTarget 'FrostWhiteComet.exe') @('--self-test','--isolated-test','--report',('"'+$frostReportDir+'"'))
    $frostDesktop = Get-Content -LiteralPath (Join-Path $frostReportDir 'desktop-report.json') -Raw | ConvertFrom-Json
    Assert-Frost ($frostDesktop.passed -eq $true) 'Installed Release desktop verification passed'
    Run-Frost (Join-Path $frostTarget 'FrostWhiteComet.exe') @('--benchmark','--isolated-test','--report',('"'+$frostReportDir+'"'))
    Assert-Frost (Test-Path -LiteralPath (Join-Path $frostReportDir 'performance-report.json')) 'Installed Release benchmark recorded'
    & dotnet run --project tests/Frost.Verify -c Release -- assets/cursors --install-only 2>&1 | Tee-Object -FilePath (Join-Path $frostReportDir 'uninstall-theme-install.txt')
    Assert-Frost ($LASTEXITCODE -eq 0) 'Installed active native theme before uninstall'
    Copy-Item -LiteralPath (Join-Path $frostData 'original-theme.json') -Destination (Join-Path $frostReportDir 'uninstall-original-snapshot.json')
    $frostRunning = Start-Process -FilePath (Join-Path $frostTarget 'FrostWhiteComet.exe') -ArgumentList '--tray' -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    Assert-Frost (-not $frostRunning.HasExited) 'App running in tray before uninstall'
    Run-Frost (Join-Path $frostTarget 'Uninstall.exe') @('--uninstall','--silent')
    for ($frostAttempt=0; $frostAttempt -lt 100 -and (Test-Path -LiteralPath $frostTarget); $frostAttempt++) { Start-Sleep -Milliseconds 100 }
    Assert-Frost (-not (Test-Path -LiteralPath $frostTarget)) 'Uninstaller helper actually removed owned installation directory'
    Assert-Frost (-not (Test-Path -LiteralPath $frostShortcut)) 'Start menu shortcut removed'
    Assert-Frost (-not (Test-Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet')) 'Uninstall registration removed'
    Assert-Frost (-not (Test-Path -LiteralPath (Join-Path $frostData 'original-theme.json'))) 'Original theme restored and backup archived'
    Assert-Frost (@(Get-Process FrostWhiteComet -ErrorAction SilentlyContinue).Count -eq 0) 'No remaining app or overlay process'
    & dotnet run --project tests/Frost.Verify -c Release -- assets/cursors --compare-backup (Join-Path $frostReportDir 'uninstall-original-snapshot.json') 2>&1 | Tee-Object -FilePath (Join-Path $frostReportDir 'uninstall-theme-restore.txt')
    Assert-Frost ($LASTEXITCODE -eq 0) 'Uninstaller restored exact original native theme'
}
finally {
    $frostChecks | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $frostReportDir 'installation-report.json') -Encoding utf8
}
Write-Host "Release verification passed. Reports: $frostReportDir"
