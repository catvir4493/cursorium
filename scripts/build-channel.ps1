param([ValidateSet('Development','Community','Steam')][string]$Channel='Development',[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.6.0',[string]$OutputDirectory,[string]$RightsDirectory)
$ErrorActionPreference='Stop';$taskRoot=Split-Path -Parent $PSScriptRoot;Set-Location -LiteralPath $taskRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
if(-not $OutputDirectory){$OutputDirectory='artifacts/channel-'+$Channel}
$taskOutput=Get-PerformanceOutputPath $OutputDirectory
if(Test-Path -LiteralPath $taskOutput){throw 'Use a fresh channel output; existing payloads are preserved.'}
if($Channel -eq 'Steam' -and -not $RightsDirectory){throw 'An actual signed commercial-rights staging directory is required for a Steam experimental payload.'}
& dotnet publish src/Frost.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:Version=$Version" "-p:CursoriumChannel=$Channel" -o $taskOutput
if($LASTEXITCODE -ne 0){throw 'Channel payload build failed'}
$probe=Join-Path $taskRoot ('artifacts/channel-probe-'+$Channel+'-'+[guid]::NewGuid().ToString('N'))
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $taskOutput 'FrostWhiteComet.exe'));$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('--foundation-probe','--isolated-test','--test-data-root',$probe)){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);if(-not $process.WaitForExit(30000)-or $process.ExitCode -ne 0){throw 'Non-input channel probe failed'}
$identity=Get-Content -LiteralPath (Join-Path $probe 'foundation-probe.json') -Raw|ConvertFrom-Json
if($identity.channel -ne $Channel -or $identity.steamIntegrated -or $identity.cloudIntegrated -or $identity.steamAppId){throw 'Built channel identity does not match its manifest'}
Copy-Item -LiteralPath LICENSE,THIRD-PARTY-NOTICES.md -Destination $taskOutput
$nugetRoot=(@(& dotnet nuget locals global-packages --list)[-1] -split ': ',2)[-1].Trim()
if($LASTEXITCODE -ne 0 -or -not(Test-Path -LiteralPath $nugetRoot)){throw 'Exact runtime license package cache is unavailable'}
$noticeDirectory=Join-Path $taskOutput 'licenses';New-Item -ItemType Directory -Path $noticeDirectory|Out-Null
foreach($item in @(
 @{source='microsoft.netcore.app.runtime.win-x64/10.0.13/LICENSE.TXT';target='dotnet-runtime-LICENSE.TXT'},
 @{source='microsoft.netcore.app.runtime.win-x64/10.0.13/THIRD-PARTY-NOTICES.TXT';target='dotnet-runtime-THIRD-PARTY-NOTICES.TXT'},
 @{source='microsoft.windowsdesktop.app.runtime.win-x64/10.0.13/LICENSE';target='windowsdesktop-LICENSE.txt'},
 @{source='microsoft.netcore.app.host.win-x64/10.0.13/LICENSE.TXT';target='dotnet-apphost-LICENSE.TXT'},
 @{source='microsoft.netcore.app.host.win-x64/10.0.13/THIRD-PARTY-NOTICES.TXT';target='dotnet-apphost-THIRD-PARTY-NOTICES.TXT'}
)){
 $source=Join-Path $nugetRoot $item.source;if(-not(Test-Path -LiteralPath $source)){throw 'Required runtime supplier notice missing'}
 Copy-Item -LiteralPath $source -Destination (Join-Path $noticeDirectory $item.target)
}
if($Channel -eq 'Steam'){
 if(@(Get-ChildItem -LiteralPath (Join-Path $taskOutput 'Themes') -Directory|Where-Object Name -like 'cute-*').Count){throw 'Uncleared bundled themes leaked into Steam payload'}
 & dotnet run --project tools/Frost.Foundation -c Release -p:CursoriumChannel=Development -- verify-steam-payload (Join-Path $probe 'rights-verifier') ([IO.Path]::GetFullPath($RightsDirectory)) $taskOutput
 if($LASTEXITCODE -ne 0){throw 'Signed bundled-content approval does not match the Steam payload'}
 [IO.File]::WriteAllText((Join-Path $taskOutput 'CHANNEL-PREVIEW.txt'),'Experimental Steam foundation only. No AppID, Steamworks, SteamPipe or Cloud integration. Native cursor application disabled. Do not distribute as a finished Steam build.')
}else{
 Copy-Item -LiteralPath README.md,VALIDATION.md -Destination $taskOutput
 Copy-Item -LiteralPath docs -Destination $taskOutput -Recurse
}
$dataTemplate=switch($Channel){'Community'{'%LOCALAPPDATA%/FrostWhiteComet'};'Steam'{'%LOCALAPPDATA%/CURSORIUM/Steam'};default{'%LOCALAPPDATA%/CURSORIUM/Development'}}
@{schemaVersion=1;version=$Version;channel=$Channel;runtime='net10.0-windows / 10.0.13';entryPoint='FrostWhiteComet.exe';userData=$dataTemplate;programUpdateOwner=$identity.updateOwner;steamIntegrated=$false;steamAppId=$null;cloudIntegrated=$false;liveNativeWrites=$identity.liveNativeWrites;packageKind=$(if($Channel -eq 'Steam'){'ExperimentalSteamFoundation'}else{$Channel+'Preview'});files=@(Get-ChildItem -LiteralPath $taskOutput -Recurse -File|ForEach-Object {@{path=$_.FullName.Substring($taskOutput.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $taskOutput 'distribution-manifest.json') -Encoding utf8
Write-Host "Prepared $Channel payload; Steam integration remains unimplemented. No installation or deployment performed."
