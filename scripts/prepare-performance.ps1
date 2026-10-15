param([Parameter(Mandatory)][string]$PreviousCheckout,[string]$OutputDirectory='artifacts/performance',[string]$CandidateDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$taskOutput=Get-PerformanceOutputPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
if(Test-Path -LiteralPath (Join-Path $taskOutput 'targets.json')){throw 'Keep frozen targets and evidence; choose a new output directory'}
function Build-Host([string]$Framework,[string]$Runtime,[string]$Name){
 $taskHost=Join-Path $taskOutput $Name
 & dotnet publish tools/Frost.PerformanceHost -c Release -r win-x64 --self-contained true "-p:PerformanceTargetFramework=$Framework" "-p:RuntimeFrameworkVersion=$Runtime" -o $taskHost | Out-Host
 if($LASTEXITCODE -ne 0){throw 'Performance host build failed'}
 return $taskHost
}
$taskEight=Build-Host net8.0-windows 8.0.24 host8
$taskTen=Build-Host net10.0-windows 10.0.13 host10
# PowerShell collects command output too: only the final returned path is used.
$taskEight=@($taskEight)[-1];$taskTen=@($taskTen)[-1]
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Expand-Baseline([string]$Name,[string]$Archive){
 $checksums=Join-Path ([IO.Path]::GetDirectoryName($Archive)) 'CURSORIUM-SHA256SUMS.txt'
 $archiveName=[IO.Path]::GetFileName($Archive)
 if(-not(Test-Path -LiteralPath $checksums)){throw 'Retain the official public SHA256 manifest beside each release archive'}
 $expected=@(Get-Content -LiteralPath $checksums|Where-Object {$_ -match ('^[A-Fa-f0-9]{64}\s+'+[regex]::Escape($archiveName)+'$')})
 if($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $Archive).Hash -ne ($expected[0] -split '\s+')[0]){throw 'Released performance baseline does not match its retained public checksum'}
 $destination=Join-Path $taskOutput ('released-'+$Name)
 if(Test-Path -LiteralPath $destination){throw 'Do not reuse an unknown baseline extraction'}
 [IO.Compression.ZipFile]::ExtractToDirectory($Archive,$destination)
 return $destination
}
$taskFour=Expand-Baseline 'v1.4.0' (Join-Path $PreviousCheckout 'artifacts/remote-v1.4.0/CURSORIUM-1.4.0-win-x64.zip')
$taskFive=Expand-Baseline 'v1.5.0' (Join-Path $PreviousCheckout 'artifacts/remote-v1.5.0/CURSORIUM-1.5.0-win-x64.zip')
$taskFiveOne=Expand-Baseline 'v1.5.1' (Join-Path $PreviousCheckout 'artifacts/remote-v1.5.1/CURSORIUM-1.5.1-win-x64.zip')
$taskEntries=@(
 @{id='v1.4.0';source=$taskFour;host=$taskEight;expected='1.4.0+6886ea6'},
 @{id='v1.5.0';source=$taskFive;host=$taskTen;expected='1.5.0+c59bd26'},
 @{id='v1.5.1';source=$taskFiveOne;host=$taskTen;expected='1.5.1+8da97e5'}
)
if($CandidateDirectory){$taskEntries+=@{id='v1.5.2';source=[IO.Path]::GetFullPath($CandidateDirectory);host=$taskTen;expected='1.5.2+'}}
$taskTargets=@()
foreach($taskEntry in $taskEntries){
 $taskVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskEntry.source 'FrostWhiteComet.exe')).ProductVersion
 if(-not $taskVersion.StartsWith($taskEntry.expected)){throw 'Unexpected released binary baseline'}
 $taskDestination=Join-Path $taskOutput ('targets/'+$taskEntry.id)
 if(Test-Path -LiteralPath $taskDestination){throw 'Do not overwrite an existing measurement target'}
 New-Item -ItemType Directory -Path $taskDestination | Out-Null
 Get-ChildItem -LiteralPath $taskEntry.source | Where-Object {$_.Name -notin @('Uninstall.exe','docs')} | Copy-Item -Destination $taskDestination -Recurse
 $taskBefore=(Get-FileHash -LiteralPath (Join-Path $taskDestination 'FrostWhiteComet.dll')).Hash
 Get-ChildItem -LiteralPath $taskEntry.host | Copy-Item -Destination $taskDestination -Recurse -Force
 if((Get-FileHash -LiteralPath (Join-Path $taskDestination 'FrostWhiteComet.dll')).Hash -ne $taskBefore){throw 'The measurement host must not modify the released application assembly'}
 $taskTargets+=@{id=$taskEntry.id;directory=$taskDestination;version=$taskVersion;applicationSHA256=$taskBefore;coreSHA256=(Get-FileHash (Join-Path $taskDestination 'Frost.Core.dll')).Hash;hostSHA256=(Get-FileHash (Join-Path $taskDestination 'CursoriumPerformanceHost.dll')).Hash}
}
@{schemaVersion=1;targets=$taskTargets;hostSourceSHA256=(Get-FileHash (Join-Path $taskRoot 'tools/Frost.PerformanceHost/Program.cs')).Hash;prepared=[DateTimeOffset]::UtcNow;notes='Production application/Core assemblies copied from retained release archives (candidate explicitly supplied). No dependency on the currently installed version. Identical reflection-host source, compiled for the exact 8.0.24 / 10.0.13 self-contained runtimes. Program and writable test data are isolated.'}|ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'targets.json') -Encoding utf8
