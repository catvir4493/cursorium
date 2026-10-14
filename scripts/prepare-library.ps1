param(
 [Parameter(Mandatory)][string]$ReviewPath,
 [Parameter(Mandatory)][string]$SourcesDirectory,
 [Parameter(Mandatory)][string]$PreviousCatalog,
 [Parameter(Mandatory)][string]$OutputDirectory,
 [string]$PrivateKeyPath=(Join-Path $env:LOCALAPPDATA 'CURSORIUM-Publishing/catalog-signing-key.pem')
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not (Test-Path -LiteralPath $PrivateKeyPath -PathType Leaf)){throw 'Protected signing key unavailable. Stop formal preparation; continue unsigned audit only.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh isolated output directory; existing evidence is preserved.'}
& dotnet build (Join-Path $projectRoot 'Frost.sln') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $projectRoot 'src/Frost.App/bin/Release/net10.0-windows/FrostWhiteComet.exe'))
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$projectRoot
foreach($argument in @('--isolated-test','--prepare-library',[IO.Path]::GetFullPath($ReviewPath),[IO.Path]::GetFullPath($SourcesDirectory),[IO.Path]::GetFullPath($PreviousCatalog),[IO.Path]::GetFullPath($OutputDirectory),[IO.Path]::GetFullPath($PrivateKeyPath),'--test-data-root',(Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'publisher-data'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start)
if(-not $process.WaitForExit(60000) -or $process.ExitCode -ne 0){throw 'Preparation failed. Preserve the staging evidence and inspect publisher-data/frost.log. No public state changed.'}
Write-Host 'Reviewed packages, real covers and signed catalog/collections prepared locally. Run publication preflight before uploading.'
