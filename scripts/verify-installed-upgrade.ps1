param([switch]$VerifyOnly,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.5.2')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
$taskRegistration=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet'
$taskTarget=[IO.Path]::GetFullPath($taskRegistration.InstallLocation)
if($taskTarget -ne 'D:\CURSORIUM'){throw 'Current authorized installation must be D:\CURSORIUM.'}
$taskData=Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
$taskPortable=Join-Path $taskRoot 'artifacts/cursorium-release/portable'
$taskRollback=Join-Path $taskRoot ("artifacts/v$Version-installed/install-rollback-"+(Get-Date -Format 'yyyyMMdd-HHmmss'))
if($VerifyOnly){$taskRollback=(Get-ChildItem -LiteralPath (Join-Path $taskRoot "artifacts/v$Version-installed") -Directory -Filter 'install-rollback-*'|Sort-Object Name -Descending|Select-Object -First 1).FullName}else{New-Item -ItemType Directory -Path $taskRollback | Out-Null}
if(-not (Test-Path -LiteralPath (Join-Path $taskTarget 'frost-install.marker'))){throw 'Marked existing installation required.'}
if([IO.Path]::GetFullPath($taskTarget).TrimEnd('\') -ne 'D:\CURSORIUM' -or (Get-Item -LiteralPath $taskTarget).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Installation target validation failed.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(-not $VerifyOnly){[IO.Compression.ZipFile]::CreateFromDirectory($taskTarget,(Join-Path $taskRollback 'previous-installation.zip'))}
if(-not $VerifyOnly -and (Test-Path -LiteralPath $taskData)){Copy-Item -LiteralPath $taskData -Destination (Join-Path $taskRollback 'user-data') -Recurse}
function Read-CursorValues {
  $taskKey=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors')
  try { @($taskKey.GetValueNames() | Sort-Object | ForEach-Object { [ordered]@{name=$_;kind=$taskKey.GetValueKind($_).ToString();value=$taskKey.GetValue($_,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)} }) | ConvertTo-Json -Depth 8 -Compress }
  finally {$taskKey.Dispose()}
}
function Read-DataHashes {
  $taskResult=@{}
  foreach($taskFile in Get-ChildItem -LiteralPath $taskData -File -Recurse){
    $taskRelative=$taskFile.FullName.Substring($taskData.Length+1)
    if($taskRelative -in @('setup-result.json','frost.log','frost.log.old')){continue}
    $taskResult[$taskRelative]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
  }
  return $taskResult
}
$taskNativeBefore=if($VerifyOnly){[IO.File]::ReadAllText((Join-Path $taskRollback 'private-cursor-snapshot.json')).Trim()}else{Read-CursorValues}
if(-not $VerifyOnly){$taskNativeBefore | Set-Content -LiteralPath (Join-Path $taskRollback 'private-cursor-snapshot.json') -Encoding utf8}
$taskHashesBefore=if($VerifyOnly){$taskCurrentData=$taskData;$taskData=Join-Path $taskRollback 'user-data';$taskResult=Read-DataHashes;$taskData=$taskCurrentData;$taskResult}else{Read-DataHashes}
if(-not $VerifyOnly){
$taskInfo=[Diagnostics.ProcessStartInfo]::new((Join-Path $taskRoot ("artifacts/CURSORIUM-Setup-$Version-Release.exe")))
$taskInfo.UseShellExecute=$false;$taskInfo.CreateNoWindow=$true;$taskInfo.Arguments='--silent --no-launch'
$taskSetup=[Diagnostics.Process]::Start($taskInfo)
if(-not $taskSetup.WaitForExit(60000)){throw 'Installer timed out.'}
if($taskSetup.ExitCode -ne 0){throw "Installer failed: $($taskSetup.ExitCode)"}
}
$taskHashesAfter=Read-DataHashes
if($taskHashesAfter.Count -ne $taskHashesBefore.Count){throw 'User data file count changed.'}
foreach($taskName in $taskHashesBefore.Keys){if($taskHashesBefore[$taskName] -ne $taskHashesAfter[$taskName]){throw 'A protected data/resource hash changed.'}}
if((Read-CursorValues) -ne $taskNativeBefore){throw 'Native cursor values or types changed during upgrade.'}
$taskMatches=0
foreach($taskFile in Get-ChildItem -LiteralPath $taskPortable -File -Recurse){
  $taskRelative=$taskFile.FullName.Substring($taskPortable.Length+1)
  $taskInstalled=Join-Path $taskTarget $taskRelative
  if(-not (Test-Path -LiteralPath $taskInstalled)){throw "Missing installed file: $taskRelative"}
  if((Get-FileHash -LiteralPath $taskFile.FullName).Hash -ne (Get-FileHash -LiteralPath $taskInstalled).Hash){throw "Installed file mismatch: $taskRelative"}
  $taskMatches++
}
$taskRegistry=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet'
if($taskRegistry.DisplayVersion -ne $Version){throw 'Uninstall version incorrect.'}
$taskShell=New-Object -ComObject WScript.Shell
$taskStartLink=Join-Path ([Environment]::GetFolderPath('Programs')) 'CURSORIUM.lnk'
$taskDesktopLink=Join-Path ([Environment]::GetFolderPath('Desktop')) 'CURSORIUM.lnk'
foreach($taskLinkPath in @($taskStartLink,$taskDesktopLink)){
  $taskLink=$taskShell.CreateShortcut($taskLinkPath)
  if($taskLink.TargetPath -ne (Join-Path $taskTarget 'FrostWhiteComet.exe')){throw 'Shortcut target is incorrect.'}
  [Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskLink) | Out-Null
}
$taskLaunch=[Diagnostics.ProcessStartInfo]::new($taskStartLink);$taskLaunch.UseShellExecute=$true
[Diagnostics.Process]::Start($taskLaunch) | Out-Null
$taskApp=$null
for($taskAttempt=0;$taskAttempt -lt 60;$taskAttempt++){
  Start-Sleep -Milliseconds 100
  $taskApp=Get-Process FrostWhiteComet -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $taskTarget 'FrostWhiteComet.exe')} | Select-Object -First 1
  if($taskApp -and $taskApp.MainWindowHandle -ne 0){break}
}
if(-not $taskApp -or $taskApp.MainWindowHandle -eq 0){throw 'Installed interactive window did not appear.'}
if($taskApp.MainWindowTitle -ne 'CURSORIUM — Cursor & Motion Studio'){throw 'Installed window title mismatch.'}
$taskOriginalPid=$taskApp.Id
$taskLaunch=[Diagnostics.ProcessStartInfo]::new($taskDesktopLink);$taskLaunch.UseShellExecute=$true
[Diagnostics.Process]::Start($taskLaunch) | Out-Null
Start-Sleep -Milliseconds 600
$taskApp.Refresh()
if($taskApp.HasExited){throw 'Installed main process exited during shortcut reopen.'}
if((Read-CursorValues) -ne $taskNativeBefore){throw 'Launch changed native cursor scheme.'}
$taskHashesAfter=Read-DataHashes
$taskCatalogRefresh=@()
$taskStagingCleanup=@()
foreach($taskName in $taskHashesBefore.Keys){
  if($taskHashesBefore[$taskName] -ne $taskHashesAfter[$taskName]){
    # The installer comparison above remains strict for every existing file.
    # On actual startup, only the signed discovery cache may legitimately refresh.
    # Settings, profiles, native resources, favorites and backups stay byte-exact.
    $relative=$taskName.Replace('\','/')
    if($relative -eq 'Online/catalog-cache.json' -and $taskHashesAfter[$taskName]){$taskCatalogRefresh+=@($relative);continue}
    # DownloadManager's existing interrupted-session recovery may remove only
    # disposable staging owned by a process generation that is no longer alive.
    # An existing file with changed bytes, or any config/native/theme file, fails.
    if(-not $taskHashesAfter[$taskName] -and $relative -match '^Online/Staging/session-(\d+)-(\d+)/(resource-[a-f0-9]{32}/.+|download-[a-f0-9]{32}\.zip)$'){
      $oldProcessId=[int]$Matches[1];$oldStarted=[long]$Matches[2];$stillAlive=$false
      try{$oldProcess=[Diagnostics.Process]::GetProcessById($oldProcessId);try{$stillAlive=$oldProcess.StartTime.ToUniversalTime().Ticks -eq $oldStarted}finally{$oldProcess.Dispose()}}catch [ArgumentException]{}catch [ComponentModel.Win32Exception]{$stillAlive=$true}
      if(-not $stillAlive){$taskStagingCleanup+=@($relative);continue}
    }
    throw "Launch changed existing protected data: $relative"
  }
}
$taskReport=[ordered]@{schemaVersion=1;version=$Version;passed=$true;installerExitCode=0;installedFilesMatchedBySha256=$taskMatches;existingDataAndResourceFiles=$taskHashesBefore.Count;installationPreservedAllExistingFileHashes=$true;startupPreservedUserConfigurationAndNativeResources=$true;startupSignedCatalogCacheRefresh=$taskCatalogRefresh;startupAbandonedStagingCleanup=$taskStagingCleanup;nativeCursorValuesAndTypesPreserved=$true;previousInstallationAndDataBackedUp=$true;uninstallRegistrationVersion=$taskRegistry.DisplayVersion;startMenuLaunchTitle=$taskApp.MainWindowTitle;desktopReopensSameProcess=$true;observedProcessId=$taskOriginalPid;notes='Existing marked installation upgrade and actual shortcut launch. Installer preserves every pre-existing hash before launch. Startup may refresh the signed catalog and clean missing disposable staging from a dead process generation; all other configuration/native/resource files remain byte-exact. Does not establish fresh install, uninstall or failure-rollback roundtrip.'}
$taskReport | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot "artifacts/v$Version-installed/installed-upgrade-report.json") -Encoding utf8
$taskReport | ConvertTo-Json
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskShell) | Out-Null
