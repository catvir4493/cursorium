param([string]$ApplicationDirectory='artifacts/cursorium-release/portable',[string]$ReportDirectory='artifacts/v1.5-final',[switch]$SelfContainedVerifier)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
New-Item -ItemType Directory -Force -Path $ReportDirectory | Out-Null
$taskSteps=@()
function Step([string]$Name,[string]$Command,[scriptblock]$Action){
 $taskStart=(Get-Date).ToUniversalTime();$taskPassed=$false
 try {& $Action;$taskPassed=$true}finally{
  $script:taskSteps+=@{name=$Name;command=$Command;started=$taskStart.ToString('O');ended=(Get-Date).ToUniversalTime().ToString('O');passed=$taskPassed;environment='Windows 11 interactive desktop; isolated data'}
  $script:taskSteps | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $ReportDirectory 'execution.json') -Encoding utf8
 }
}
Step 'Release build' 'dotnet build Frost.sln -c Release' {& dotnet build Frost.sln -c Release;if($LASTEXITCODE -ne 0){throw 'Build failed'}}
$taskVerifier='dotnet';$taskVerifierPrefix=@('run','--project','tests/Frost.Verify','-c','Release','--');$taskVerifyParameters=@{}
if($SelfContainedVerifier){
 $taskVerifierDirectory=Join-Path $taskRoot (Join-Path $ReportDirectory 'verifier')
 $taskApplicationVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path ([IO.Path]::GetFullPath($ApplicationDirectory)) 'FrostWhiteComet.exe')).ProductVersion.Split('+')[0]
 if($taskApplicationVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'A versioned production application is required for the self-contained verifier'}
 Step 'Self-contained verifier' ('dotnet publish tests/Frost.Verify -c Release -r win-x64 --self-contained true -p:Version='+$taskApplicationVersion+' (10.0.13)') {& dotnet publish tests/Frost.Verify -c Release -r win-x64 --self-contained true "-p:Version=$taskApplicationVersion" -o $taskVerifierDirectory;if($LASTEXITCODE -ne 0){throw 'Self-contained verifier build failed'}}
 # Test the exact deployed Core bytes, not just another build of its sources.
 Step 'Deployed Core identity' 'Copy deployed Frost.Core.dll into the isolated verifier; require identical SHA256' {
  Copy-Item -LiteralPath (Join-Path ([IO.Path]::GetFullPath($ApplicationDirectory)) 'Frost.Core.dll') -Destination (Join-Path $taskVerifierDirectory 'Frost.Core.dll') -Force
  $taskCorePdb=Join-Path ([IO.Path]::GetFullPath($ApplicationDirectory)) 'Frost.Core.pdb'
  if(Test-Path -LiteralPath $taskCorePdb){Copy-Item -LiteralPath $taskCorePdb -Destination (Join-Path $taskVerifierDirectory 'Frost.Core.pdb') -Force}
  if((Get-FileHash -LiteralPath (Join-Path $taskVerifierDirectory 'Frost.Core.dll')).Hash -ne (Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetFullPath($ApplicationDirectory)) 'Frost.Core.dll')).Hash){throw 'The verifier must load exactly the deployed Core assembly'}
 }
 $taskVerifier=Join-Path $taskVerifierDirectory 'Frost.Verify.exe';$taskVerifierPrefix=@();$taskVerifyParameters=@{VerifierExecutable=$taskVerifier}
}
Step 'Retained and recovery regression' ('scripts/verify-cursorium.ps1 -Discover -SkipBenchmark'+$(if($SelfContainedVerifier){' -VerifierExecutable <self-contained-10.0.13-verifier>'}else{''})) {& $PSScriptRoot/verify-cursorium.ps1 -ApplicationDirectory $ApplicationDirectory -ReportDirectory (Join-Path $ReportDirectory 'regression') -Discover -SkipBenchmark @taskVerifyParameters}
Step 'Native FROST resources' ($taskVerifier+' -- assets/cursors') {& $taskVerifier @taskVerifierPrefix assets/cursors;if($LASTEXITCODE -ne 0){throw 'FROST resource validation failed'}}
Step 'Native Obsidian resources' ($taskVerifier+' -- assets/themes/obsidian') {& $taskVerifier @taskVerifierPrefix assets/themes/obsidian;if($LASTEXITCODE -ne 0){throw 'Obsidian resource validation failed'}}
# Reuse the retained signed-stage fixtures when present; never weaken or delete them.
$taskStage=Join-Path $taskRoot 'artifacts/library-1.1.0-stage2';$taskPrevious=Join-Path $taskRoot 'artifacts/library-previous/index.json'
if((Test-Path -LiteralPath $taskStage)-and(Test-Path -LiteralPath $taskPrevious)){
 Step 'Curation and immutable update regression' ($taskVerifier+' --curation-tests <retained signed stage> <previous catalog>') {& $taskVerifier @taskVerifierPrefix --curation-tests ([IO.Path]::GetFullPath($ApplicationDirectory)) $taskStage $taskPrevious ([IO.Path]::GetFullPath((Join-Path $ReportDirectory 'curation')));if($LASTEXITCODE -ne 0){throw 'Curation failed'}}
} else {throw 'Retained curation fixtures are unavailable; restore them before full release verification.'}
Write-Host 'All recorded v1.5 regression steps passed.'
