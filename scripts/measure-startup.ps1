param([string]$TargetsPath='artifacts/performance/targets.json',[string]$ReportDirectory='artifacts/performance/startup',[ValidateRange(5,100)][int]$Rounds=15,[ValidateRange(1,10)][int]$WarmupRounds=2)
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$taskTargets=@((Get-Content $TargetsPath -Raw|ConvertFrom-Json).targets);$taskReport=Get-PerformanceOutputPath $ReportDirectory;New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskResults=@();$taskStarted=[DateTimeOffset]::UtcNow;$taskRestart=$false;$taskPassed=$false;$taskInstalled='D:\CURSORIUM\FrostWhiteComet.exe';$taskProcess=$null
function Launch([string]$Exe,[string[]]$Arguments,[ref]$CreationMs){$start=[Diagnostics.ProcessStartInfo]::new($Exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;foreach($argument in $Arguments){$start.ArgumentList.Add($argument)};$call=[Diagnostics.Stopwatch]::StartNew();$child=[Diagnostics.Process]::Start($start);if($CreationMs){$CreationMs.Value=$call.Elapsed.TotalMilliseconds};return $child}
try{
 $existing=@(Get-CimInstance Win32_Process -Filter "Name='FrostWhiteComet.exe' OR Name='CursoriumPerformanceHost.exe'")
 if(@($existing|Where-Object ExecutablePath -ne $taskInstalled).Count){throw 'Finish other performance/desktop tests before exclusive startup observations'}
 if($existing.Count){$taskRestart=$true;$stop=Launch $taskInstalled @('--shutdown');if(-not $stop.WaitForExit(10000)){throw 'Production shutdown timed out'};Start-Sleep -Milliseconds 300}
foreach($round in (-$WarmupRounds)..($Rounds-1)){
 foreach($offset in 0..($taskTargets.Count-1)){
  $phase=if($round -lt 0){'warmup'}else{'stable'}
  $index=(($offset+$round)%$taskTargets.Count+$taskTargets.Count)%$taskTargets.Count
  $target=$taskTargets[$index];$output=Join-Path $taskReport ('{0}-{1:D2}-{2}' -f $phase,[Math]::Abs($round),$target.id);if(Test-Path $output){throw 'Keep prior startup evidence; use a new output directory'};New-Item -ItemType Directory -Path $output|Out-Null
  $creation=0d;$timer=[Diagnostics.Stopwatch]::StartNew();$taskProcess=Launch (Join-Path $target.directory 'FrostWhiteComet.exe') @('--state-probe','--isolated-test','--test-data-root',(Join-Path $output 'data'),'--report',(Join-Path $output 'state.json')) ([ref]$creation)
  if(-not $taskProcess.WaitForExit(15000)-or $taskProcess.ExitCode -ne 0 -or -not(Test-Path (Join-Path $output 'state.json'))){throw 'Original executable state probe failed'}
  $exitMs=$timer.Elapsed.TotalMilliseconds
  $state=Get-Content (Join-Path $output 'state.json') -Raw|ConvertFrom-Json
  if($state.state.SchemaVersion -ne 1 -or @($state.themes).Count -lt 8){throw 'State probe did not read the actual expected catalog/state'}
  $hostCreation=0d;$timer.Restart();$taskProcess=Launch (Join-Path $target.directory 'CursoriumPerformanceHost.exe') @('--mode','state','--warmup','1','--seconds','5','--data',(Join-Path $output 'host-data'),'--report',(Join-Path $output 'state-host')) ([ref]$hostCreation)
  if(-not $taskProcess.WaitForExit(15000)-or $taskProcess.ExitCode -ne 0){throw 'Common-host state-constructor observation failed'}
  $hostExitMs=$timer.Elapsed.TotalMilliseconds;$hostState=Get-Content (Join-Path $output 'state-host/state.json') -Raw|ConvertFrom-Json
  if(-not $hostState.managedMainEpochExplicit){throw 'Prepare an updated diagnostic host; a deferred clock epoch cannot measure managed-entry state completion'}
  $taskResults+=@{round=$round;phase=$phase;target=$target.id;processCreationCallMs=$creation;stateProbeProcessExitMs=$exitMs;hostProcessCreationCallMs=$hostCreation;hostStateProcessExitMs=$hostExitMs;hostManagedEntryAssemblyLoadedMs=$hostState.markers.mainAssemblyLoadedMs;hostManagedEntryStateLoadedMs=$hostState.markers.stateLoadedMs;applicationSHA256=$target.applicationSHA256;hostSHA256=$target.hostSHA256}
  $taskProcess=$null
 }
}
 $taskPassed=$true
}finally{
 if($taskProcess -and -not $taskProcess.HasExited){$taskProcess.Kill();$taskProcess.WaitForExit(10000)|Out-Null}
 if($taskRestart){Start-Process -FilePath $taskInstalled -ArgumentList '--tray' -WindowStyle Hidden}
 @{schemaVersion=1;passed=$taskPassed;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;rounds=$Rounds;warmupRounds=$WarmupRounds;measurements=$taskResults;notes='Unchanged original release executables, isolated empty data, exclusive production instance. Explicit warmup trials retained and alternating stable order with warm OS filesystem caches, not cold boot. Original process creation/complete probe exit and separate common-host managed-entry state-constructor completion are distinct. None is first-painted UI or human interactivity. Tray/show/input-dispatcher milestones are measured separately.'}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $taskReport 'startup.json') -Encoding utf8
}
Write-Host ('Actual state probes completed: '+$taskResults.Count)
