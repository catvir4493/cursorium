param([Parameter(Mandatory)][string]$PreviousCheckout,[string]$OutputDirectory='artifacts/performance')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskRoot
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $OutputDirectory))
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
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
$taskFive=Join-Path $taskOutput 'released-v1.5.0'
if(-not(Test-Path -LiteralPath $taskFive)){[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $PreviousCheckout 'artifacts/remote-v1.5.0/CURSORIUM-1.5.0-win-x64.zip'),$taskFive)}
$taskEntries=@(
 @{id='v1.4.0';source=(Join-Path $PreviousCheckout 'artifacts/v1.5-net8-idle-baseline');host=$taskEight;expected='1.4.0+6886ea6'},
 @{id='v1.5.0';source=$taskFive;host=$taskTen;expected='1.5.0+c59bd26'},
 @{id='v1.5.1';source='D:\CURSORIUM';host=$taskTen;expected='1.5.1+8da97e5'}
)
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
@{schemaVersion=1;targets=$taskTargets;notes='Exact released production application/Core assemblies. Identical reflection-host source, compiled for the exact 8.0.24 / 10.0.13 self-contained runtimes. Program and writable test data are isolated.'}|ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'targets.json') -Encoding utf8
