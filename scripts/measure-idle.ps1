param([string]$CandidateDirectory='artifacts/v1.5-candidate-current',[string]$BaselineDirectory='D:\CURSORIUM',[ValidateRange(5,120)][int]$Seconds=15,[string]$ReportDirectory='artifacts/v1.5-idle')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskReport=Join-Path $taskRoot $ReportDirectory;New-Item -ItemType Directory -Force -Path $taskReport | Out-Null
if(-not(Test-Path -LiteralPath (Join-Path $taskRoot 'artifacts/v1.5-baseline/baseline.json'))){throw 'A verified private baseline is required first'}
$taskData=Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
if(Test-Path -LiteralPath (Join-Path $taskData 'theme-transaction.json')){throw 'Resolve the existing production transaction before measuring a normal launch'}
Add-Type -TypeDefinition @'
using System;using System.Runtime.InteropServices;
public static class IdleProbeNative {
 [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
 [DllImport("user32.dll")]public static extern bool GetPhysicalCursorPos(out Point p);
 [DllImport("user32.dll")]public static extern int GetGuiResources(IntPtr h,int kind);
}
'@
$taskInstalled='D:\CURSORIUM\FrostWhiteComet.exe'
$taskBaseline=if([IO.Path]::IsPathRooted($BaselineDirectory)){$BaselineDirectory}else{Join-Path $taskRoot $BaselineDirectory}
$taskRunning=@(Get-CimInstance Win32_Process -Filter "Name='FrostWhiteComet.exe'")
if(@($taskRunning | Where-Object ExecutablePath -ne $taskInstalled).Count){throw 'Finish other test/portable instances before idle measurement'}
function Launch([string]$Exe,[string]$Arguments){$taskStart=[Diagnostics.ProcessStartInfo]::new($Exe);$taskStart.UseShellExecute=$false;$taskStart.CreateNoWindow=$true;$taskStart.Arguments=$Arguments;return [Diagnostics.Process]::Start($taskStart)}
$taskRows=@();$taskProcess=$null
try {
 if($taskRunning.Count){$taskStop=Launch $taskInstalled '--shutdown';if(-not $taskStop.WaitForExit(10000)){throw 'Production shutdown timed out'};Start-Sleep -Milliseconds 500}
 foreach($taskEntry in @(@('baseline',(Join-Path $taskBaseline 'FrostWhiteComet.exe')),@('net10-candidate',(Join-Path (Join-Path $taskRoot $CandidateDirectory) 'FrostWhiteComet.exe')))){
  $taskProcess=Launch $taskEntry[1] '--tray';Start-Sleep -Seconds 5
  if($taskProcess.HasExited){throw 'Normal tray startup failed'}
  $taskProcess.Refresh();$taskCpu=$taskProcess.TotalProcessorTime;$taskClock=[Diagnostics.Stopwatch]::StartNew();$taskSamples=@();$taskPointer=[IdleProbeNative+Point]::new();[IdleProbeNative]::GetPhysicalCursorPos([ref]$taskPointer)|Out-Null;$taskMoves=0
  for($taskSample=0;$taskSample -lt $Seconds;$taskSample++){
   Start-Sleep -Seconds 1;$taskProcess.Refresh();$taskNow=[IdleProbeNative+Point]::new();[IdleProbeNative]::GetPhysicalCursorPos([ref]$taskNow)|Out-Null
   if($taskNow.X -ne $taskPointer.X -or $taskNow.Y -ne $taskPointer.Y){$taskMoves++};$taskPointer=$taskNow
   $taskSamples+=@{seconds=$taskClock.Elapsed.TotalSeconds;workingSet=$taskProcess.WorkingSet64;privateBytes=$taskProcess.PrivateMemorySize64;gdi=[IdleProbeNative]::GetGuiResources($taskProcess.Handle,0);user=[IdleProbeNative]::GetGuiResources($taskProcess.Handle,1);threads=$taskProcess.Threads.Count}
  }
  $taskRows+=@{mode=$taskEntry[0];version=[Diagnostics.FileVersionInfo]::GetVersionInfo($taskEntry[1]).ProductVersion;seconds=$taskClock.Elapsed.TotalSeconds;cpuOneCorePercent=($taskProcess.TotalProcessorTime-$taskCpu).TotalSeconds/$taskClock.Elapsed.TotalSeconds*100;cpuMachinePercent=($taskProcess.TotalProcessorTime-$taskCpu).TotalSeconds/$taskClock.Elapsed.TotalSeconds/([Environment]::ProcessorCount)*100;observedPointerChanges=$taskMoves;samples=$taskSamples}
  $taskStop=Launch $taskEntry[1] '--shutdown';if(-not $taskStop.WaitForExit(10000)-or -not $taskProcess.WaitForExit(10000)){throw 'Normal tray shutdown timed out'};$taskProcess=$null
 }
} finally {
 if($taskProcess -and -not $taskProcess.HasExited){$taskStop=Launch $taskProcess.MainModule.FileName '--shutdown';$null=$taskStop.WaitForExit(10000)}
 if($taskRunning.Count){Start-Process -FilePath $taskInstalled -ArgumentList '--tray' -WindowStyle Hidden}
}
& $PSScriptRoot/verify-stability-protection.ps1
@{measurements=$taskRows;notes='Actual normal tray processes using the same backed-up production configuration. No test Apply, synthetic movement or tight in-process driver loop. First 5 seconds excluded; this is a short idle observation, not a whole-machine energy or first-frame benchmark.'} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $taskReport 'idle-report.json') -Encoding utf8
Write-Host 'Protected normal-mode idle comparison completed.'
