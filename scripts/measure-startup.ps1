param([string]$TargetsPath='artifacts/performance/targets.json',[string]$ReportDirectory='artifacts/performance/startup',[ValidateRange(5,100)][int]$Rounds=15)
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
$taskTargets=@((Get-Content $TargetsPath -Raw|ConvertFrom-Json).targets);$taskReport=[IO.Path]::GetFullPath((Join-Path $taskRoot $ReportDirectory));New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskResults=@();$taskStarted=[DateTimeOffset]::UtcNow
foreach($round in 0..($Rounds-1)){
 foreach($offset in 0..($taskTargets.Count-1)){
  $target=$taskTargets[($offset+$round)%$taskTargets.Count];$output=Join-Path $taskReport ('{0:D2}-{1}' -f $round,$target.id);if(Test-Path $output){throw 'Keep prior startup evidence; use a new output directory'};New-Item -ItemType Directory -Path $output|Out-Null
  $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $target.directory 'FrostWhiteComet.exe'));$start.UseShellExecute=$false;$start.CreateNoWindow=$true
  foreach($argument in @('--state-probe','--isolated-test','--test-data-root',(Join-Path $output 'data'),'--report',(Join-Path $output 'state.json'))){$start.ArgumentList.Add($argument)}
  $timer=[Diagnostics.Stopwatch]::StartNew();$process=[Diagnostics.Process]::Start($start);$creation=$timer.Elapsed.TotalMilliseconds
  if(-not $process.WaitForExit(15000)-or $process.ExitCode -ne 0 -or -not(Test-Path (Join-Path $output 'state.json'))){throw 'Original executable state probe failed'}
  $taskResults+=@{round=$round;target=$target.id;processCreationCallMs=$creation;stateProbeProcessExitMs=$timer.Elapsed.TotalMilliseconds;applicationSHA256=$target.applicationSHA256}
 }
}
@{schemaVersion=1;passed=$true;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;rounds=$Rounds;measurements=$taskResults;notes='Actual unchanged release executables with isolated empty data. Alternating order, warm OS filesystem caches. Process creation API duration and complete state-probe process lifetime are distinct; neither is a first-painted-window timing. Tray/show/input-dispatcher milestones are measured by the common host separately.'}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $taskReport 'startup.json') -Encoding utf8
Write-Host ('Actual state probes completed: '+$taskResults.Count)
