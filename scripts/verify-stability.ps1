param([string]$ApplicationDirectory='artifacts/cursorium-release/portable',[ValidateRange(5,259200)][int]$DurationSeconds=60,[ValidateRange(1,1000)][int]$StartupCycles=3,[string]$ReportDirectory='artifacts/v1.5-stability')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskApp=[IO.Path]::GetFullPath((Join-Path $taskRoot $ApplicationDirectory))
$taskReport=[IO.Path]::GetFullPath((Join-Path $taskRoot $ReportDirectory))
New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskExe=Join-Path $taskApp 'FrostWhiteComet.exe'
function Invoke-StabilityProcess([string[]]$Arguments,[int]$Timeout){
 $taskStart=[Diagnostics.ProcessStartInfo]::new($taskExe);$taskStart.UseShellExecute=$false;$taskStart.CreateNoWindow=$true
 $taskStart.Arguments=($Arguments | ForEach-Object {'"'+$_.Replace('"','\"')+'"'}) -join ' '
 $taskClock=[Diagnostics.Stopwatch]::StartNew();$taskProcess=[Diagnostics.Process]::Start($taskStart)
 if(-not $taskProcess.WaitForExit($Timeout)){throw 'Diagnostic timed out; its isolated process is retained for inspection.'}
 if($taskProcess.ExitCode -ne 0){throw "Diagnostic failed: exit $($taskProcess.ExitCode)"}
 return $taskClock.Elapsed.TotalMilliseconds
}
$taskRuns=@()
for($taskCycle=0;$taskCycle -lt $StartupCycles;$taskCycle++){
 $taskData=Join-Path $taskReport ('startup-data-'+[guid]::NewGuid().ToString('N'))
 $taskState=Join-Path $taskData 'state-report.json'
 $taskMilliseconds=Invoke-StabilityProcess @('--state-probe','--isolated-test','--test-data-root',$taskData,'--report',$taskState) 30000
 if(-not (Test-Path -LiteralPath $taskState)){throw 'State probe did not execute.'}
 $taskRuns+=@{cycle=$taskCycle;milliseconds=$taskMilliseconds}
}
$taskStress=Join-Path $taskReport ('stress-'+[guid]::NewGuid().ToString('N'))
$null=Invoke-StabilityProcess @('--stability-review','--isolated-test','--test-data-root',(Join-Path $taskStress 'data'),'--report',$taskStress,'--duration-seconds',"$DurationSeconds") (($DurationSeconds+60)*1000)
$taskResult=Get-Content -LiteralPath (Join-Path $taskStress 'stability-report.json') -Raw | ConvertFrom-Json
if(-not $taskResult.passed){throw 'Stability report failed.'}
@{passed=$true;startupProbes=$taskRuns;durationSeconds=$taskResult.durationSeconds;cycles=$taskResult.cycles;report=(Join-Path $taskStress 'stability-report.json');note='Startup probes measure process + native catalog/state loading, not first painted UI. Stress workload is synthetic local preview with real WPF lifecycle; production tray may coexist.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskReport 'summary.json') -Encoding utf8
Write-Host "Stability verification passed: $taskReport"
