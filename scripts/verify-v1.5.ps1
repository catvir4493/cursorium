param([string]$ApplicationDirectory='artifacts/cursorium-release/portable',[string]$ReportDirectory='artifacts/v1.5-final')
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
Step 'Retained and recovery regression' 'scripts/verify-cursorium.ps1 -Discover -SkipBenchmark' {& $PSScriptRoot/verify-cursorium.ps1 -ApplicationDirectory $ApplicationDirectory -ReportDirectory (Join-Path $ReportDirectory 'regression') -Discover -SkipBenchmark}
Step 'Native FROST resources' 'dotnet run --project tests/Frost.Verify -c Release -- assets/cursors' {& dotnet run --project tests/Frost.Verify -c Release -- assets/cursors;if($LASTEXITCODE -ne 0){throw 'FROST resource validation failed'}}
Step 'Native Obsidian resources' 'dotnet run --project tests/Frost.Verify -c Release -- assets/themes/obsidian' {& dotnet run --project tests/Frost.Verify -c Release -- assets/themes/obsidian;if($LASTEXITCODE -ne 0){throw 'Obsidian resource validation failed'}}
# Reuse the retained signed-stage fixtures when present; never weaken or delete them.
$taskStage=Join-Path $taskRoot 'artifacts/library-1.1.0-stage2';$taskPrevious=Join-Path $taskRoot 'artifacts/library-previous/index.json'
if((Test-Path -LiteralPath $taskStage)-and(Test-Path -LiteralPath $taskPrevious)){
 Step 'Curation and immutable update regression' 'dotnet run --project tests/Frost.Verify -c Release -- --curation-tests <retained signed stage> <previous catalog>' {& dotnet run --project tests/Frost.Verify -c Release -- --curation-tests ([IO.Path]::GetFullPath($ApplicationDirectory)) $taskStage $taskPrevious ([IO.Path]::GetFullPath((Join-Path $ReportDirectory 'curation')));if($LASTEXITCODE -ne 0){throw 'Curation failed'}}
} else {throw 'Retained curation fixtures are unavailable; restore them before full release verification.'}
Write-Host 'All recorded v1.5 regression steps passed.'
