param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.5.2',[string]$ReportPath='artifacts/release-assets-verification.json')
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
$taskStarted=[DateTimeOffset]::UtcNow;$taskCommit=(git rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Git revision is unavailable'}
if(git status --porcelain --untracked-files=no){throw 'Commit tracked changes before verifying release/source identity'}
$taskPortable=Join-Path $taskRoot 'artifacts/cursorium-release/portable'
$taskProduct=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskPortable 'FrostWhiteComet.exe')).ProductVersion
if($taskProduct -ne "$Version+$taskCommit"){throw 'Portable build does not belong to the current version and commit; rebuild after committing.'}
$taskManifest=Get-Content -LiteralPath (Join-Path $taskPortable 'distribution-manifest.json') -Raw|ConvertFrom-Json
if($taskManifest.schemaVersion -ne 1 -or $taskManifest.version -ne $Version){throw 'Distribution manifest version is invalid'}
$taskRuntime=Get-Content -LiteralPath (Join-Path $taskPortable 'FrostWhiteComet.runtimeconfig.json') -Raw|ConvertFrom-Json
if($taskRuntime.runtimeOptions.tfm -ne 'net10.0' -or @($taskRuntime.runtimeOptions.includedFrameworks).Count -ne 2 -or @($taskRuntime.runtimeOptions.includedFrameworks|Where-Object {$_.name -notin @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') -or $_.version -ne '10.0.13'}).Count){throw 'Portable application must retain the verified .NET 10.0.13 self-contained desktop runtime'}
function Hash-Stream([IO.Stream]$Stream){$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($Stream)).Replace('-','')}finally{$algorithm.Dispose()}}
function Hash-GitBlob([IO.Stream]$Stream,[long]$Length,[string]$Format){
 $algorithm=if($Format -eq 'sha1'){[Security.Cryptography.SHA1]::Create()}elseif($Format -eq 'sha256'){[Security.Cryptography.SHA256]::Create()}else{throw 'Unsupported Git object hash format'}
 try{
  $prefix=[Text.Encoding]::UTF8.GetBytes('blob '+$Length+[char]0);$null=$algorithm.TransformBlock($prefix,0,$prefix.Length,$prefix,0)
  $buffer=[byte[]]::new(65536);while(($count=$Stream.Read($buffer,0,$buffer.Length)) -gt 0){$null=$algorithm.TransformBlock($buffer,0,$count,$buffer,0)}
  $null=$algorithm.TransformFinalBlock([byte[]]::new(0),0,0);return [BitConverter]::ToString($algorithm.Hash).Replace('-','')
 }finally{$algorithm.Dispose()}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskPayload=[IO.Compression.ZipFile]::OpenRead((Join-Path $taskRoot "artifacts/CURSORIUM-$Version-win-x64.zip"))
$taskPayloadEntries=@{};$taskPayloadChecks=0
try{
 foreach($entry in $taskPayload.Entries){$name=$entry.FullName.Replace('\','/');if($name.EndsWith('/')){continue};if($taskPayloadEntries.ContainsKey($name)){throw 'Duplicate portable ZIP entry'};$taskPayloadEntries[$name]=$entry}
 if($taskPayloadEntries.Count -ne @($taskManifest.files).Count+1){throw 'Portable ZIP file count differs from its manifest'}
 foreach($file in $taskManifest.files){
  $name=$file.path.Replace('\','/');if(-not $taskPayloadEntries.ContainsKey($name)){throw 'A manifest file is absent from the portable ZIP'}
  $entry=$taskPayloadEntries[$name];if($entry.Length -ne $file.size){throw 'Portable ZIP size differs from its manifest'}
  $stream=$entry.Open();try{if((Hash-Stream $stream) -ne $file.sha256){throw 'Portable ZIP hash differs from its manifest'}}finally{$stream.Dispose()}
  if((Get-FileHash -LiteralPath (Join-Path $taskPortable $name)).Hash -ne $file.sha256){throw 'Published directory differs from the portable ZIP'};$taskPayloadChecks++
 }
 $stream=$taskPayloadEntries['distribution-manifest.json'].Open();try{if((Hash-Stream $stream) -ne (Get-FileHash -LiteralPath (Join-Path $taskPortable 'distribution-manifest.json')).Hash){throw 'Portable manifest bytes do not match the prepared directory'}}finally{$stream.Dispose()}
}finally{$taskPayload.Dispose()}
$taskSource=[IO.Compression.ZipFile]::OpenRead((Join-Path $taskRoot "artifacts/CURSORIUM-Source-$Version.zip"));$taskSourceChecks=0
try{
 $objectFormat=(git rev-parse --show-object-format).Trim();if($LASTEXITCODE -ne 0){throw 'Git object format is unavailable'}
 $tracked=@(git -c core.quotepath=false ls-tree -r HEAD -- src tools tests installer scripts assets docs Directory.Build.props Frost.sln README.md VALIDATION.md LICENSE AGENTS.md .gitignore .gitattributes)
 if($LASTEXITCODE -ne 0){throw 'Committed source listing failed'}
 $expected=@{};foreach($file in $tracked){if($file -notmatch '^(100644|100755) blob ([A-Fa-f0-9]+)\t(.+)$'){throw 'Only regular committed files belong in source delivery'};$expected[$Matches[3]]=$Matches[2]};$seen=@{}
 if(@($taskSource.Entries|Where-Object {-not $_.FullName.EndsWith('/')} ).Count -ne $expected.Count){throw 'Source ZIP must contain exactly the committed delivery sources'}
 foreach($entry in $taskSource.Entries){$name=$entry.FullName.Replace('\','/');if($name.EndsWith('/')){continue};if(-not $expected.ContainsKey($name)-or $seen.ContainsKey($name)){throw 'Source ZIP has an unexpected or duplicate file'};$seen[$name]=$true
  $stream=$entry.Open();try{if((Hash-GitBlob $stream $entry.Length $objectFormat) -ne $expected[$name]){throw 'Source ZIP is stale; rebuild it from the completed source commit'}}finally{$stream.Dispose()};$taskSourceChecks++
 }
}finally{$taskSource.Dispose()}
$taskExpectedNames=@("CURSORIUM-Setup-$Version-Release.exe","CURSORIUM-$Version-win-x64.zip","CURSORIUM-Themes-$Version.zip","CURSORIUM-Source-$Version.zip")
$taskChecksums=@{};foreach($line in Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts/CURSORIUM-SHA256SUMS.txt')){
 if($line -notmatch '^([A-Fa-f0-9]{64})\s+([^/\\]+)$'){throw 'Malformed release checksum record'};$hash=$Matches[1];$name=$Matches[2]
 if($name -notin $taskExpectedNames -or $taskChecksums.ContainsKey($name)){throw 'Unexpected or duplicate checksum record'}
 if((Get-FileHash -LiteralPath (Join-Path $taskRoot ('artifacts/'+$name))).Hash -ne $hash){throw 'Release asset checksum mismatch'};$taskChecksums[$name]=$hash
}
if($taskChecksums.Count -ne 4){throw 'All four release asset hashes are required'}
$taskSetupVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskRoot "artifacts/CURSORIUM-Setup-$Version-Release.exe")).ProductVersion
if($taskSetupVersion -ne "$Version+$taskCommit"){throw 'Installer version or commit differs from the prepared application'}
@{passed=$true;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;version=$Version;commit=$taskCommit;portableFiles=$taskPayloadChecks;sourceFiles=$taskSourceChecks;assetChecksums=$taskChecksums;notes='Local release bytes, tracked source contents, distribution manifest and executable revision checked before publishing.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $taskRoot $ReportPath) -Encoding utf8
Write-Host 'Release bytes and source revision verified.'
