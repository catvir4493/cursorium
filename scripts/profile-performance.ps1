param([string]$TargetsPath='artifacts/performance/targets.json',[string]$ReportDirectory='artifacts/performance/profiles')
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$taskReport=Get-PerformanceOutputPath $ReportDirectory;New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskTargets=@((Get-Content $TargetsPath -Raw|ConvertFrom-Json).targets);$taskInstalled='D:\CURSORIUM\FrostWhiteComet.exe';$taskRestart=$false;$taskHost=$null;$taskDriver=$null
$taskDriverExe=Join-Path $taskTargets[-1].directory 'CursoriumPerformanceHost.exe';$taskStarted=[DateTimeOffset]::UtcNow;$taskRecords=@();$taskPassed=$false
if(-not(Test-Path -LiteralPath (Join-Path $taskRoot 'artifacts/diagnostic-tools/dotnet-trace.exe'))){throw 'Install the pinned dotnet-trace tool in artifacts/diagnostic-tools before profiling'}
function Launch([string]$Exe,[string[]]$Arguments){$start=[Diagnostics.ProcessStartInfo]::new($Exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;foreach($argument in $Arguments){$start.ArgumentList.Add($argument)};return [Diagnostics.Process]::Start($start)}
try{
 $existing=@(Get-CimInstance Win32_Process -Filter "Name='FrostWhiteComet.exe' OR Name='CursoriumPerformanceHost.exe'");if(@($existing|Where-Object ExecutablePath -ne $taskInstalled).Count){throw 'Other test process active'}
 if($existing.Count){$taskRestart=$true;$stop=Launch $taskInstalled @('--shutdown');if(-not $stop.WaitForExit(10000)){throw 'Shutdown timeout'};Start-Sleep -Milliseconds 300}
 $cases=@($taskTargets|ForEach-Object {@{target=$_;mode='comet'}})+@(@{target=$taskTargets[-1];mode='idle'},@{target=$taskTargets[-1];mode='harness-timer8'})
 foreach($case in $cases){
  $caseStarted=[DateTimeOffset]::UtcNow;$target=$case.target;$mode=$case.mode;$output=Join-Path $taskReport ($target.id+'-'+$mode);if(Test-Path -LiteralPath $output){throw 'Keep prior trace evidence; choose a new report directory'};New-Item -ItemType Directory -Path $output | Out-Null
  $exe=Join-Path $target.directory 'CursoriumPerformanceHost.exe';$taskHost=Launch $exe @('--mode',$mode,'--warmup','5','--seconds','25','--data',(Join-Path $taskReport ('data-'+[guid]::NewGuid().ToString('N'))),'--report',$output)
  $timer=[Diagnostics.Stopwatch]::StartNew();while(-not(Test-Path (Join-Path $output 'ready.json'))){if($taskHost.HasExited -or $timer.Elapsed.TotalSeconds -gt 40){throw 'Profile host readiness failed'};Start-Sleep -Milliseconds 50}
  if($mode -eq 'comet'){$taskDriver=Launch $taskDriverExe @('--drive','--input','comet','--seconds','31','--report',$output)}
  Start-Sleep -Seconds 5
  & ./artifacts/diagnostic-tools/dotnet-trace.exe collect --process-id $taskHost.Id --profile dotnet-sampled-thread-time,dotnet-common --duration 00:00:15 --output (Join-Path $output 'sample.nettrace') *> (Join-Path $output 'collection.txt')
  if($LASTEXITCODE -ne 0){throw 'EventPipe collection failed'}
  if(-not $taskHost.WaitForExit(45000)-or $taskHost.ExitCode -ne 0){throw 'Profiled host failed'};$taskHost=$null
  if($taskDriver){if(-not $taskDriver.WaitForExit(10000)-or $taskDriver.ExitCode -ne 0 -or -not(Get-Content (Join-Path $output 'driver.json') -Raw|ConvertFrom-Json).passed){throw 'Input target failed or was cancelled'};$taskDriver=$null}
  & ./artifacts/diagnostic-tools/dotnet-trace.exe report (Join-Path $output 'sample.nettrace') topN -n 40 *> (Join-Path $output 'walltime-top40.txt')
  if($LASTEXITCODE -ne 0){throw 'Trace report failed'}
  $taskRecords+=@{target=$target.id;mode=$mode;started=$caseStarted;ended=[DateTimeOffset]::UtcNow;applicationSHA256=$target.applicationSHA256;hostSHA256=$target.hostSHA256;command='dotnet-trace collect --profile dotnet-sampled-thread-time,dotnet-common --duration 00:00:15';report=($target.id+'-'+$mode);passed=$true}
  Write-Host ('Profile captured: '+$target.id+' / '+$mode)
 }
 $taskPassed=$true
}finally{
 foreach($process in @($taskDriver,$taskHost)){if($process -and -not $process.HasExited){$process.Kill();$process.WaitForExit(10000)|Out-Null}}
 if($taskRestart){Start-Process -FilePath $taskInstalled -ArgumentList '--tray' -WindowStyle Hidden}
 @{passed=$taskPassed;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;cases=$taskRecords;driverSHA256=(Get-FileHash (Join-Path $taskTargets[-1].directory 'CursoriumPerformanceHost.dll')).Hash;notes='EventPipe wall-time stack profiling, not kernel CPU sampling. Profiling perturbs execution; do not merge its CPU values into unprofiled timing results.'}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $taskReport 'execution.json') -Encoding utf8
}
