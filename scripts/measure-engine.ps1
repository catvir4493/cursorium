param([string]$ApplicationDirectory='artifacts/v1.5-candidate-current',[string]$ReportDirectory='artifacts/v1.5-performance',[switch]$Extended,[switch]$Exclusive)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskApp=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($ApplicationDirectory)){$ApplicationDirectory}else{Join-Path $taskRoot $ApplicationDirectory}));$taskReport=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($ReportDirectory)){$ReportDirectory}else{Join-Path $taskRoot $ReportDirectory}))
New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskInstalled='D:\CURSORIUM\FrostWhiteComet.exe';$taskRestart=$false
function Launch([string]$Exe,[string]$Arguments,[int]$Timeout){
 $taskStart=[Diagnostics.ProcessStartInfo]::new($Exe);$taskStart.UseShellExecute=$false;$taskStart.CreateNoWindow=$true;$taskStart.Arguments=$Arguments
 $taskTimer=[Diagnostics.Stopwatch]::StartNew();$taskProcess=[Diagnostics.Process]::Start($taskStart)
 if(-not $taskProcess.WaitForExit($Timeout)){throw 'Measurement timed out; preserve its isolated process for inspection'}
 if($taskProcess.ExitCode -ne 0){throw "Measurement failed: exit $($taskProcess.ExitCode)"};return $taskTimer.Elapsed.TotalMilliseconds
}
$taskStarted=(Get-Date).ToUniversalTime();$taskStartup=@()
try {
 if($Exclusive){
  $taskRunning=@(Get-CimInstance Win32_Process -Filter "Name='FrostWhiteComet.exe'")
  if(@($taskRunning | Where-Object {$_.ExecutablePath -ne $taskInstalled}).Count){throw 'Another test/portable process is active; finish it before exclusive measurement'}
  if($taskRunning.Count){$taskRestart=$true;$null=Launch $taskInstalled '--shutdown' 10000;Start-Sleep -Milliseconds 500}
 }
 for($taskCycle=0;$taskCycle -lt 5;$taskCycle++){
  $taskData=Join-Path $taskReport ('probe-'+[guid]::NewGuid().ToString('N'))
  $taskTime=Launch (Join-Path $taskApp 'FrostWhiteComet.exe') ('--state-probe --isolated-test --test-data-root "'+$taskData+'" --report "'+$taskData+'\state.json"') 10000
  $taskStartup+=@{cycle=$taskCycle;milliseconds=$taskTime}
 }
 $taskArguments='--benchmark --isolated-test --test-data-root "'+(Join-Path $taskReport 'data')+'" --report "'+$taskReport+'"'+$(if($Extended){' --extended-benchmark'}else{''})
 $null=Launch (Join-Path $taskApp 'FrostWhiteComet.exe') $taskArguments 100000
 $taskResult=Get-Content -LiteralPath (Join-Path $taskReport 'performance-report.json') -Raw | ConvertFrom-Json
 if($taskResult.measurements.Count -ne $(if($Extended){15}else{8})){throw 'Incomplete measurement modes'}
 @{passed=$true;started=$taskStarted.ToString('O');ended=(Get-Date).ToUniversalTime().ToString('O');exclusiveApplicationInstance=[bool]$Exclusive;startupStateProbes=$taskStartup;notes='State probe is process/native catalog readiness, not first painted frame. Benchmark CPU includes the input-driving harness. Other unrelated machine workload is uncontrolled.'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskReport 'measurement-run.json') -Encoding utf8
} finally {
 if($taskRestart){Start-Process -FilePath $taskInstalled -ArgumentList '--tray' -WindowStyle Hidden}
}
