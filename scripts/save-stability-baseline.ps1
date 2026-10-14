param([string]$OutputDirectory='artifacts/v1.5-baseline')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $OutputDirectory))
if(Test-Path -LiteralPath $taskOutput){throw 'A baseline already exists; use a new output directory.'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
$taskData=Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
if(Test-Path -LiteralPath $taskData){Copy-Item -LiteralPath $taskData -Destination (Join-Path $taskOutput 'user-data') -Recurse}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory('D:\CURSORIUM',(Join-Path $taskOutput 'installation.zip'))
$taskHashes=@()
foreach($taskBase in @('D:\CURSORIUM',$taskData)){
 foreach($taskFile in Get-ChildItem -LiteralPath $taskBase -Recurse -File){
  $taskHashes+=@{area=if($taskBase -eq $taskData){'data'}else{'program'};path=$taskFile.FullName.Substring($taskBase.Length+1);sha256=(Get-FileHash -LiteralPath $taskFile.FullName).Hash}
 }
}
$taskHashes | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskOutput 'private-hashes.json') -Encoding utf8
& reg.exe export 'HKCU\Control Panel\Cursors' (Join-Path $taskOutput 'private-cursors.reg') /y | Out-Null
if($LASTEXITCODE -ne 0){throw 'Registry export failed.'}
$taskArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $taskOutput 'installation.zip'))
try {if($taskArchive.Entries.Count -lt 100){throw 'Incomplete installation backup.'}}finally{$taskArchive.Dispose()}
$taskCopied=Join-Path $taskOutput 'user-data'
foreach($taskEntry in $taskHashes | Where-Object area -eq 'data'){
 if((Get-FileHash -LiteralPath (Join-Path $taskCopied $taskEntry.path)).Hash -ne $taskEntry.sha256){throw 'Data backup hash mismatch.'}
}
@{commit=(git rev-parse HEAD);tag=(git rev-parse 'cursorium-v1.4.0^{}');at=(Get-Date).ToUniversalTime().ToString('O');files=$taskHashes.Count;archiveSha256=(Get-FileHash -LiteralPath (Join-Path $taskOutput 'installation.zip')).Hash;verified=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'baseline.json') -Encoding utf8
Write-Host "Verified private baseline: $taskOutput"
