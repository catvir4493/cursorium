param([Parameter(Mandatory)][string]$InputDirectory,[Parameter(Mandatory)][string]$OutputDirectory,[string]$ApplicationDirectory='src/Frost.App/bin/Release/net8.0-windows')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$inputRoot=[IO.Path]::GetFullPath($InputDirectory)
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
if(-not (Test-Path -LiteralPath $inputRoot -PathType Container)){throw 'Supply directories extracted and verified through the existing safe package workflow.'}
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $repo "$ApplicationDirectory/FrostWhiteComet.exe"))
$start.UseShellExecute=$false
$start.CreateNoWindow=$true
foreach($argument in @('--isolated-test','--export-covers',$inputRoot,$outputRoot,'--test-data-root',(Join-Path $repo 'artifacts/cover-generator-data'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start)
$process.WaitForExit()
if($process.ExitCode -ne 0){throw 'Real-resource cover generation failed.'}
$records=Get-ChildItem -LiteralPath $outputRoot -Filter *.png -File | Sort-Object Name | ForEach-Object { [pscustomobject]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} }
$records | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'cover-checksums.json') -Encoding utf8
Write-Host "Generated $($records.Count) static covers from validated native resources and the trusted painter. No package or Windows cursor settings changed."
