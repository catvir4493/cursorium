param([string]$BaselineDirectory='artifacts/v1.5-baseline',[string]$ReportDirectory='artifacts/v1.5-protection')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskBaseline=Join-Path $taskRoot $BaselineDirectory;$taskOutput=Join-Path $taskRoot $ReportDirectory
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskData=Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
$taskOriginal=Get-Content -LiteralPath (Join-Path $taskBaseline 'private-hashes.json') -Raw | ConvertFrom-Json
$taskChecks=@()
foreach($taskEntry in $taskOriginal | Where-Object area -eq 'data'){
 if($taskEntry.path -in @('frost.log','frost.log.old','setup-result.json')){continue}
 $taskPath=Join-Path $taskData $taskEntry.path
 $taskSame=(Test-Path -LiteralPath $taskPath)-and (Get-FileHash -LiteralPath $taskPath).Hash -eq $taskEntry.sha256
 $taskChecks+=@{category=if($taskEntry.path -match '^Online[\\/]Staging[\\/]'){'ephemeral'}elseif($taskEntry.path -match '^Online[\\/](catalog|collections)-cache.json$'){'signed-cache'}else{'permanent'};unchanged=$taskSame}
}
$taskCritical=@()
foreach($taskName in @('settings.json','studio.json','profiles.json','original-theme.json','theme-transaction.json','interface.json','theme-ownership.json')){
 $taskOld=Join-Path (Join-Path $taskBaseline 'user-data') $taskName;$taskNew=Join-Path $taskData $taskName
 $taskOldExists=Test-Path -LiteralPath $taskOld;$taskNewExists=Test-Path -LiteralPath $taskNew
 $taskSame=$taskOldExists -eq $taskNewExists
 if($taskSame -and $taskOldExists){$taskSame=(Get-FileHash -LiteralPath $taskOld).Hash -eq (Get-FileHash -LiteralPath $taskNew).Hash}
 $taskCritical+=@{name=$taskName;existedBefore=$taskOldExists;existsAfter=$taskNewExists;unchanged=$taskSame}
}
$taskReg=Join-Path $taskOutput 'private-current-cursors.reg'
& reg.exe export 'HKCU\Control Panel\Cursors' $taskReg /y | Out-Null;if($LASTEXITCODE -ne 0){throw 'Current registry snapshot failed'}
function Normalize-Registry([string]$Path){
 $taskSection='';$taskPending='';$taskEntries=@()
 foreach($taskLine in Get-Content -LiteralPath $Path){
  if($taskLine.StartsWith('[')){$taskSection=$taskLine;continue}
  if(-not $taskLine.Trim()-or $taskLine.StartsWith('Windows Registry')){continue}
  $taskPending+=$taskLine.Trim()
  if($taskPending.EndsWith('\')){$taskPending=$taskPending.Substring(0,$taskPending.Length-1);continue}
  $taskEntries+=($taskSection+$taskPending);$taskPending=''
 }
 return ($taskEntries | Sort-Object) -join "`n"
}
$taskNativeSame=(Normalize-Registry (Join-Path $taskBaseline 'private-cursors.reg')) -ceq (Normalize-Registry $taskReg)
$taskPassed=(@($taskCritical | Where-Object {-not $_.unchanged}).Count -eq 0)-and (@($taskChecks | Where-Object {$_.category -eq 'permanent'-and -not $_.unchanged}).Count -eq 0)-and $taskNativeSame
@{passed=$taskPassed;at=(Get-Date).ToUniversalTime().ToString('O');critical=$taskCritical;permanentFiles=@($taskChecks | Where-Object category -eq 'permanent').Count;permanentChanged=@($taskChecks | Where-Object {$_.category -eq 'permanent'-and -not $_.unchanged}).Count;ephemeralOrCacheChanges=@($taskChecks | Where-Object {$_.category -ne 'permanent'-and -not $_.unchanged}).Count;nativeValuesAndTypesUnchanged=$taskNativeSame;notes='Private registry snapshots and relative file hashes remain local. Log/setup-result and mutable cache/staging are distinguished from permanent resources.'} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $taskOutput 'protection-report.json') -Encoding utf8
if(-not $taskPassed){throw 'Permanent data or native state changed; inspect private evidence before proceeding'}
Write-Host 'Permanent user data and native state match the original baseline.'
