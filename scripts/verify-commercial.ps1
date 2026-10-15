param([Parameter(Mandatory)][string]$ApplicationDirectory,[Parameter(Mandatory)][string]$RetainedArtifacts,[string]$ReportDirectory='artifacts/v1.6-safe-verification')
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$report=Get-PerformanceOutputPath $ReportDirectory;if(Test-Path -LiteralPath $report){throw 'Preserve previous results; use a fresh verification directory'};New-Item -ItemType Directory -Path $report|Out-Null
$application=[IO.Path]::GetFullPath($ApplicationDirectory);$retained=[IO.Path]::GetFullPath($RetainedArtifacts);$verifier=Join-Path $report 'verifier/Frost.Verify.exe';$records=@()
function Step([string]$Name,[string]$Command,[scriptblock]$Action){$started=[DateTimeOffset]::UtcNow;$passed=$false;try{& $Action;$passed=$true}finally{$script:records+=@{name=$Name;command=$Command;started=$started;ended=[DateTimeOffset]::UtcNow;passed=$passed};@{scope='Non-input, non-Apply subset only';steps=$script:records;pending=@('Performance matrix','Natural input','Native CPU profiling','Full WPF/native regression','Windows 10','Physical dual/mixed DPI','24h/72h')}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $report 'execution.json') -Encoding utf8}}
function Verify([string[]]$Arguments){& $verifier @Arguments;if($LASTEXITCODE -ne 0){throw 'Retained verifier reported a failure'}}
Step 'Build' 'dotnet build Frost.sln -c Release' {& dotnet build Frost.sln -c Release;if($LASTEXITCODE -ne 0){throw 'Build failed'}}
Step 'Exact runtime verifier' 'dotnet publish tests/Frost.Verify -c Release -r win-x64 --self-contained true -p:CursoriumChannel=Community; copy deployed Core and compare SHA256' {
 & dotnet publish tests/Frost.Verify -c Release -r win-x64 --self-contained true -p:Version=1.6.0 -p:CursoriumChannel=Community -o (Split-Path -Parent $verifier);if($LASTEXITCODE -ne 0){throw 'Verifier publish failed'}
 Copy-Item -LiteralPath (Join-Path $application 'Frost.Core.dll') -Destination (Join-Path (Split-Path -Parent $verifier) 'Frost.Core.dll') -Force
 $sourceCore=Join-Path $application 'Frost.Core.dll';$deployedCore=Join-Path (Split-Path -Parent $verifier) 'Frost.Core.dll'
 if((Get-FileHash -LiteralPath $sourceCore).Hash -ne (Get-FileHash -LiteralPath $deployedCore).Hash){throw 'Deployed Core identity differs'}
}
Step 'Commercial foundation' 'Frost.Verify --foundation-tests <candidate> tests/fixtures/commercial <retained packages> <report>' {Verify @('--foundation-tests',$application,(Join-Path $taskRoot 'tests/fixtures/commercial'),(Join-Path $retained 'library-export/packages'),(Join-Path $retained 'library-1.1.0-stage2/packages'),(Join-Path $report 'foundation'))}
Step 'Retained core' 'Frost.Verify --studio-tests <candidate> <report> (without native roundtrip)' {Verify @('--studio-tests',$application,(Join-Path $report 'core'))}
Step 'Retained online security' 'Frost.Verify --online-tests <candidate> tests/fixtures/online <report>' {Verify @('--online-tests',$application,(Join-Path $taskRoot 'tests/fixtures/online'),(Join-Path $report 'online'))}
Step 'Retained curation/update' 'Frost.Verify --curation-tests <candidate> <retained signed stage> <previous catalog> <report>' {Verify @('--curation-tests',$application,(Join-Path $retained 'library-1.1.0-stage2'),(Join-Path $retained 'library-previous/index.json'),(Join-Path $report 'curation'))}
Step 'Actual public-network filesystem' 'Frost.Verify --download-filesystem-tests <candidate> <report> <fresh isolated encrypted data>' {Verify @('--download-filesystem-tests',$application,(Join-Path $report 'filesystem'),(Join-Path $env:LOCALAPPDATA ('CursoriumVerification-1.6-'+[guid]::NewGuid().ToString('N'))))}
Step 'FROST native file validation' 'Frost.Verify assets/cursors' {Verify @((Join-Path $taskRoot 'assets/cursors'))}
Step 'Obsidian native file validation' 'Frost.Verify assets/themes/obsidian' {Verify @((Join-Path $taskRoot 'assets/themes/obsidian'))}
Step 'Retained performance protocol tests' 'python -B -m unittest discover -s tests -p test_performance_summary.py (no benchmark/input)' {& python -B -m unittest discover -s tests -p test_performance_summary.py;if($LASTEXITCODE -ne 0){throw 'Protocol tests failed'}}
Step 'Unknown build channel rejected' 'dotnet build src/Frost.Core -p:CursoriumChannel=Unknown (expected failure)' {& dotnet build src/Frost.Core -c Release -p:CursoriumChannel=Unknown *> (Join-Path $report 'unknown-channel.txt');if($LASTEXITCODE -eq 0){throw 'Unknown channel accepted'}}
Step 'Actual migration junction rejected' 'Foundation migration CLI in a marked sandbox with an NTFS junction; no production paths' {
 $toolRoot=Join-Path $report 'junction-tool';& dotnet run --project tools/Frost.Foundation -c Release -- migration-create $toolRoot;if($LASTEXITCODE -ne 0){throw 'Sandbox creation failed'}
 $sandboxes=@(Get-ChildItem -LiteralPath $toolRoot -Directory -Filter 'CursoriumMigration-*');if($sandboxes.Count -ne 1){throw 'Unexpected sandbox scope'}
 $sandbox=$sandboxes[0].FullName;$foreign=Join-Path $report 'foreign';New-Item -ItemType Directory -Path $foreign|Out-Null;$keep=Join-Path $foreign 'keep.txt';[IO.File]::WriteAllText($keep,'isolated foreign bytes preserved');$hash=(Get-FileHash -LiteralPath $keep).Hash
 $junction=[IO.Path]::GetFullPath((Join-Path $sandbox 'legacy/junction'));if([IO.Path]::GetDirectoryName($junction) -ne (Join-Path $sandbox 'legacy')){throw 'Unsafe synthetic junction path'}
 New-Item -ItemType Junction -Path $junction -Target $foreign|Out-Null
 try{& dotnet run --project tools/Frost.Foundation -c Release -- migration-prepare (Join-Path $report 'junction-report') $sandbox;if($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath (Join-Path $sandbox 'cursorium'))-or(Get-FileHash -LiteralPath $keep).Hash -ne $hash){throw 'Linked migration was not safely rejected'}}finally{Remove-Item -LiteralPath $junction -Force}
}
Write-Host 'Safe commercial subset completed. Full WPF/native/input and hardware gates remain Pending.'
