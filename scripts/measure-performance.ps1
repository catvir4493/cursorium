param([string]$TargetsPath='artifacts/performance/targets.json',[string]$ReportDirectory='artifacts/performance/alternating',[ValidateRange(1,20)][int]$Rounds=3,[ValidateRange(5,600)][int]$Seconds=20,[ValidateRange(1,120)][int]$WarmupSeconds=5,[string[]]$Modes=@('disabled','idle','comet','minimal','neon','stardust','target120','power30','opened-closed','fast-click','harness-only','harness-timer8'),[switch]$NoProductionRestart,[switch]$Resume,[string]$RepairPlanPath='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$taskReport=Get-PerformanceOutputPath $ReportDirectory;New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
$taskTargets=@((Get-Content -LiteralPath $TargetsPath -Raw|ConvertFrom-Json).targets)
if($taskTargets.Count -lt 1 -or @($taskTargets.id|Sort-Object -Unique).Count -ne $taskTargets.Count){throw 'Measurement targets must have unique stable identities'}
foreach($target in $taskTargets){
 if($target.id -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$' -or $target.id.Contains('..')){throw 'Measurement target identity cannot supply a path'}
 foreach($pair in @(@('FrostWhiteComet.dll','applicationSHA256'),@('Frost.Core.dll','coreSHA256'),@('CursoriumPerformanceHost.dll','hostSHA256'))){
  if((Get-FileHash -LiteralPath (Join-Path $target.directory $pair[0])).Hash -ne $target.($pair[1])){throw 'A frozen measurement binary changed; prepare a new target manifest'}
 }
}
$taskDriverExe=Join-Path $taskTargets[-1].directory 'CursoriumPerformanceHost.exe'
$taskRepair=@{};$taskPlanSHA=$null
if($RepairPlanPath){
 $taskPlan=Get-Content -LiteralPath $RepairPlanPath -Raw|ConvertFrom-Json
 if($taskPlan.schemaVersion -ne 1 -or @($taskPlan.entries).Count -eq 0){throw 'Repair observations need an explicit nonempty versioned plan derived from excluded/missing trials'}
 foreach($entry in $taskPlan.entries){
  $key=$entry.mode+'/'+$entry.target
  if($Modes -cnotcontains $entry.mode -or $taskTargets.id -cnotcontains $entry.target -or $taskRepair.ContainsKey($key) -or $entry.requiredRounds -lt 1 -or $entry.requiredRounds -gt $Rounds -or [int]$entry.requiredRounds -ne $entry.requiredRounds){throw 'Repair plan has an unknown, duplicate or invalid observation group'}
  $taskRepair[$key]=[int]$entry.requiredRounds
 }
 $taskPlanSHA=(Get-FileHash -LiteralPath $RepairPlanPath).Hash
}
$taskInstalled='D:\CURSORIUM\FrostWhiteComet.exe';$taskWasRunning=$false;$taskHost=$null;$taskDriver=$null;$taskRuns=@();$taskStarted=[DateTimeOffset]::UtcNow
if($Resume -and (Test-Path (Join-Path $taskReport 'execution.json'))){$previous=Get-Content (Join-Path $taskReport 'execution.json') -Raw|ConvertFrom-Json;if($previous.rounds -ne $Rounds -or $previous.seconds -ne $Seconds -or $previous.warmupSeconds -ne $WarmupSeconds){throw 'Resume must keep the original observation durations and rounds'};$taskRuns=@($previous.runs);$taskStarted=$previous.started}
function Save-Execution([object]$Value){
 $destination=Join-Path $taskReport 'execution.json';$temporary=$destination+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
 try{$Value|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $temporary -Encoding utf8;if([IO.File]::Exists($destination)){[IO.File]::Replace($temporary,$destination,[NullString]::Value)}else{[IO.File]::Move($temporary,$destination)}}finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}}
}
function Start-Measurement([string]$Exe,[string[]]$Arguments){$taskInfo=[Diagnostics.ProcessStartInfo]::new($Exe);$taskInfo.UseShellExecute=$false;$taskInfo.CreateNoWindow=$true;foreach($argument in $Arguments){$taskInfo.ArgumentList.Add($argument)};return [Diagnostics.Process]::Start($taskInfo)}
try{
 $taskExisting=@(Get-CimInstance Win32_Process -Filter "Name='FrostWhiteComet.exe' OR Name='CursoriumPerformanceHost.exe'")
 if(@($taskExisting | Where-Object ExecutablePath -ne $taskInstalled).Count){throw 'Finish other measurements or isolated desktop tests before exclusive performance work'}
 if($taskExisting.Count){$taskWasRunning=$true;$taskStop=Start-Measurement $taskInstalled @('--shutdown');if(-not $taskStop.WaitForExit(10000)){throw 'Production shutdown timed out'};Start-Sleep -Milliseconds 300}
 for($round=0;$round -lt $Rounds;$round++){
  for($modeIndex=0;$modeIndex -lt $Modes.Count;$modeIndex++){
   $mode=$Modes[$modeIndex]
   for($offset=0;$offset -lt $taskTargets.Count;$offset++){
    $target=$taskTargets[($offset+$round+$modeIndex)%$taskTargets.Count]
    if($RepairPlanPath){$key=$mode+'/'+$target.id;if(-not $taskRepair.ContainsKey($key)-or $round -ge $taskRepair[$key]){continue}}
    $name=('{0:D2}-{1}-{2}' -f $round,$mode,$target.id);$output=Join-Path $taskReport $name
    $completed=@($taskRuns|Where-Object {$_.round -eq $round -and $_.mode -eq $mode -and $_.target -eq $target.id})
    if($completed.Count){if(-not $Resume -or $completed.Count -ne 1 -or $completed[0].applicationSHA256 -ne $target.applicationSHA256 -or -not(Test-Path (Join-Path (Join-Path $taskReport $completed[0].relativeReport) 'metrics.json'))){throw 'Completed evidence is incompatible with this run'};continue}
    if(Test-Path -LiteralPath $output){if(-not $Resume){throw 'Use a new report directory; measurement evidence is immutable'};$name+='.retry-'+[guid]::NewGuid().ToString('N');$output=Join-Path $taskReport $name}
    New-Item -ItemType Directory -Path $output | Out-Null
    $controller=[Diagnostics.Process]::GetCurrentProcess();$controller.Refresh();$controllerCpu=$controller.TotalProcessorTime.TotalSeconds;$timer=[Diagnostics.Stopwatch]::StartNew()
    $exe=Join-Path $target.directory 'CursoriumPerformanceHost.exe';$data=Join-Path $taskReport ('data-'+[guid]::NewGuid().ToString('N'))
    $taskHost=Start-Measurement $exe @('--mode',$mode,'--warmup',"$WarmupSeconds",'--seconds',"$Seconds",'--data',$data,'--report',$output);$creationCallMs=$timer.Elapsed.TotalMilliseconds
    $ready=Join-Path $output 'ready.json'
    while(-not(Test-Path -LiteralPath $ready)){if($taskHost.HasExited -or $timer.Elapsed.TotalSeconds -gt 40){throw "Host did not become ready: $name"};Start-Sleep -Milliseconds 50}
    $readyData=Get-Content -LiteralPath $ready -Raw|ConvertFrom-Json
    $readinessMs=$timer.Elapsed.TotalMilliseconds
    if($readyData.pid -ne $taskHost.Id){throw 'Readiness process mismatch'}
    if($mode -notin @('disabled','idle','opened-closed','harness-only','harness-timer8')){
     $taskDriver=Start-Measurement $taskDriverExe @('--drive','--input',$mode,'--seconds',"$($Seconds+$WarmupSeconds+1)",'--report',$output)
    }
    if(-not $taskHost.WaitForExit(($Seconds+$WarmupSeconds+60)*1000)-or $taskHost.ExitCode -ne 0){throw "Measurement failed: $name"}
    if($taskDriver){if(-not $taskDriver.WaitForExit(10000)-or $taskDriver.ExitCode -ne 0){throw 'Input driver did not exit safely'};$driverData=Get-Content (Join-Path $output 'driver.json') -Raw|ConvertFrom-Json;if(-not $driverData.passed){throw 'Input driver was cancelled; this run is not valid'};if($driverData.observedMoves -lt 5 -or ($mode -eq 'fast-click' -and $driverData.observedClicks -lt 1)){throw 'Actual input did not reach the owned target; do not accept a locked/blocked desktop as an active workload'};$taskDriver=$null}
    $metrics=Get-Content (Join-Path $output 'metrics.json') -Raw|ConvertFrom-Json
    if(-not $metrics.passed -or @($metrics.samples|Where-Object phase -eq stable).Count -lt $Seconds-1){throw 'Insufficient stable observations'}
    $controller.Refresh();$taskRuns+=@{round=$round;mode=$mode;target=$target.id;relativeReport=$name;processCreationCallMs=$creationCallMs;readinessMs=$readinessMs;elapsedSeconds=$timer.Elapsed.TotalSeconds;controllerCPUOneCorePercent=($controller.TotalProcessorTime.TotalSeconds-$controllerCpu)/$timer.Elapsed.TotalSeconds*100;runtime=$readyData.runtime;applicationSHA256=$target.applicationSHA256;contaminatedByPointerMotion=($mode -in @('disabled','idle','harness-only','harness-timer8','opened-closed') -and @($metrics.samples|Where-Object {$_.phase -eq 'stable' -and $_.pointerChanged}).Count -gt 0)}
    Save-Execution @{schemaVersion=1;started=$taskStarted;updated=[DateTimeOffset]::UtcNow;completed=$false;rounds=$Rounds;seconds=$Seconds;warmupSeconds=$WarmupSeconds;runs=$taskRuns;repairPlanSHA256=$taskPlanSHA}
    Write-Host ("Completed {0}: {1:N1}s" -f $name,$timer.Elapsed.TotalSeconds);$taskHost=$null
   }
  }
 }
 Save-Execution @{schemaVersion=1;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;completed=$true;rounds=$Rounds;seconds=$Seconds;warmupSeconds=$WarmupSeconds;runs=$taskRuns;repairPlanSHA256=$taskPlanSHA;driverSHA256=(Get-FileHash (Join-Path $taskTargets[-1].directory 'CursoriumPerformanceHost.dll')).Hash;notes='Alternating target order across rounds and modes. Identical independent .NET 10 input-driver binary for all targets; its CPU is recorded separately. Repairs are selected from invalid/missing input conditions, never CPU values. No profiling attached during timed runs; machine is interactive, not a dedicated laboratory.'}
}finally{
 foreach($process in @($taskDriver,$taskHost)){if($process -and -not $process.HasExited){$process.Kill();$process.WaitForExit(10000)|Out-Null}}
 if($taskWasRunning -and -not $NoProductionRestart){Start-Process -FilePath $taskInstalled -ArgumentList '--tray' -WindowStyle Hidden}
}
