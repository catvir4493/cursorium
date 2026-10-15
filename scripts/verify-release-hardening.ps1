param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.6.0',[string]$ReportDirectory='artifacts/release-hardening')
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$taskReport=Get-PerformanceOutputPath $ReportDirectory;New-Item -ItemType Directory -Force -Path $taskReport|Out-Null
$taskPortable=Get-PerformanceOutputPath 'artifacts/cursorium-release/portable'
$taskRuntime=Join-Path $taskPortable 'FrostWhiteComet.runtimeconfig.json'
$taskSource=Join-Path $taskRoot "artifacts/CURSORIUM-Source-$Version.zip"
$taskStarted=[DateTimeOffset]::UtcNow;$taskChecks=@();$taskComplete=$false
function Expect-Rejection([string]$Name,[string]$Expected,[scriptblock]$Action){
 $rejected=$false;try{& $Action}catch{if($_.Exception.Message -notlike ('*'+$Expected+'*')){throw};$rejected=$true}
 $script:taskChecks+=@{name=$Name;passed=$rejected};if(-not $rejected){throw ('Unsafe release bytes were accepted: '+$Name)}
}
function Assert-Restored([string]$Path,[byte[]]$Bytes){
 $algorithm=[Security.Cryptography.SHA256]::Create();try{$expected=[BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-','')}finally{$algorithm.Dispose()}
 if((Get-FileHash -LiteralPath $Path).Hash -ne $expected){throw 'An isolated release-fixture artifact was not restored byte-for-byte; publication must stop'}
}
try{
 & (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $Version -ReportPath (Join-Path $ReportDirectory 'initial-assets.json')
 Expect-Rejection 'Mismatched version/commit rejected before publishing' 'does not belong to the current version and commit' {& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version '99.99.99'}
 $runtimeBytes=[IO.File]::ReadAllBytes($taskRuntime)
 try{
  $runtime=Get-Content -LiteralPath $taskRuntime -Raw|ConvertFrom-Json;$runtime.runtimeOptions.includedFrameworks[0].version='99.0.0'
  $runtime|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $taskRuntime -Encoding utf8
  Expect-Rejection 'Wrong self-contained runtime rejected' 'must retain the verified .NET 10.0.13' {& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $Version}
 }finally{[IO.File]::WriteAllBytes($taskRuntime,$runtimeBytes);Assert-Restored $taskRuntime $runtimeBytes}
 $sourceBytes=[IO.File]::ReadAllBytes($taskSource)
 try{
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive=[IO.Compression.ZipFile]::Open($taskSource,[IO.Compression.ZipArchiveMode]::Update)
  try{
   $archive.GetEntry('README.md').Delete();$entry=$archive.CreateEntry('README.md');$writer=[IO.StreamWriter]::new($entry.Open())
   try{$writer.Write('Synthetic stale-source fixture; no user data.')}finally{$writer.Dispose()}
  }finally{$archive.Dispose()}
  Expect-Rejection 'Stale source ZIP content rejected' 'Source ZIP is stale' {& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $Version}
 }finally{[IO.File]::WriteAllBytes($taskSource,$sourceBytes);Assert-Restored $taskSource $sourceBytes}
 # Use another version filename; never replace the actual prepared source ZIP.
 $probeDirectory=Join-Path $ReportDirectory ('packaging-probe-'+[guid]::NewGuid().ToString('N'))
 $probeZip=Join-Path (Join-Path $taskRoot $probeDirectory) 'CURSORIUM-Source-0.0.0.zip'
 $probe=Join-Path $taskRoot ('tools/cursorium-untracked-packaging-probe-'+[guid]::NewGuid().ToString('N')+'.txt')
 $probeParent=[IO.Path]::GetDirectoryName($probe)
 for($ancestor=$probeParent;$ancestor;$ancestor=[IO.Path]::GetDirectoryName($ancestor)){if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked source workspace rejected before creating a synthetic packaging sentinel'}}
 try{
  [IO.File]::WriteAllText($probe,'Synthetic untracked packaging sentinel; no private information.')
  & (Join-Path $PSScriptRoot 'package-source.ps1') -Version '0.0.0' -OutputDirectory $probeDirectory
  $archive=[IO.Compression.ZipFile]::OpenRead($probeZip)
  try{
   $name='tools/'+[IO.Path]::GetFileName($probe)
   $excluded=$null -eq $archive.GetEntry($name);$taskChecks+=@{name='Untracked source-directory files excluded from source package';passed=$excluded}
   if(-not $excluded){throw 'Untracked files leaked into the source package'}
  }finally{$archive.Dispose()}
 }finally{if(Test-Path -LiteralPath $probe){Remove-Item -LiteralPath $probe}}
 & (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $Version -ReportPath (Join-Path $ReportDirectory 'final-assets.json')
 $taskComplete=$true
}finally{
 @{passed=$taskComplete;started=$taskStarted;ended=[DateTimeOffset]::UtcNow;checks=$taskChecks;notes='Negative fixtures touch only prepared artifacts and one uniquely named synthetic untracked workspace file. Runtime/source archive bytes restored and final whole-asset verification required. No installed application, user data, registry, published release or tag modified.'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $taskReport 'hardening-report.json') -Encoding utf8
}
