$ErrorActionPreference = 'Stop'
$frostRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $frostRoot
$frostTarget = Join-Path $env:LOCALAPPDATA 'Programs\FrostWhiteComet'
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
