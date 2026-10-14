param([switch]$SkipVerification,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.5.1')
$ErrorActionPreference = 'Stop'
$frostRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $frostRoot
function Invoke-Dotnet { param([string[]]$Arguments) & dotnet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
Invoke-Dotnet @('run','--project','tools/Frost.CursorGen','-c','Release','--','assets/cursors')
Invoke-Dotnet @('run','--project','tools/Frost.CursorGen','-c','Release','--','assets/themes/obsidian','--obsidian')
Invoke-Dotnet @('build','Frost.sln','-c','Release','--no-incremental',"-p:Version=$Version")
if (-not $SkipVerification) {
    Invoke-Dotnet @('run','--project','tests/Frost.Verify','-c','Release','--','assets/cursors')
    Invoke-Dotnet @('run','--project','tests/Frost.Verify','-c','Release','--','assets/themes/obsidian')
}
New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
# Never mix files from an older self-contained runtime into a newer delivery.
foreach($taskOutputName in @('portable','setup')){
 $taskOutput=[IO.Path]::GetFullPath((Join-Path $frostRoot ('artifacts/cursorium-release/'+$taskOutputName)))
 $taskExpected=[IO.Path]::GetFullPath((Join-Path $frostRoot 'artifacts/cursorium-release'))
 if([IO.Path]::GetDirectoryName($taskOutput) -ne $taskExpected){throw 'Unsafe build output cleanup path'}
 if(Test-Path -LiteralPath $taskOutput){
  foreach($taskEntry in @(Get-Item -LiteralPath $taskOutput)+@(Get-ChildItem -LiteralPath $taskOutput -Recurse -Force)){
   if($taskEntry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build outputs must be inspected before cleanup'}
  }
  Remove-Item -LiteralPath $taskOutput -Recurse -Force
 }
}
Invoke-Dotnet @('publish','src/Frost.App','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false',"-p:Version=$Version",'-o','artifacts/cursorium-release/portable')
if ((Get-ChildItem -LiteralPath 'artifacts/cursorium-release/portable/Theme' -Filter '*.cur').Count -ne 15 -or (Get-ChildItem -LiteralPath 'artifacts/cursorium-release/portable/Themes/obsidian' -Filter '*.cur').Count -ne 15 -or -not (Test-Path -LiteralPath 'artifacts/cursorium-release/portable/Catalog/effects.json')) { throw 'Published CURSORIUM theme/effect resources are incomplete.' }
$cursoriumCute=@(Get-ChildItem -LiteralPath 'artifacts/cursorium-release/portable/Themes' -Directory | Where-Object {$_.Name -like 'cute-*'})
if($cursoriumCute.Count -ne 5){throw 'Published Cute Collection requires five real themes.'}
foreach($cursoriumTheme in $cursoriumCute){
    $cursoriumManifest=Get-Content -LiteralPath (Join-Path $cursoriumTheme.FullName 'theme.json') -Raw|ConvertFrom-Json
    if($cursoriumManifest.Collection -ne 'cute'-or $cursoriumManifest.License -notmatch 'Public Domain'-or @($cursoriumManifest.Roles.PSObject.Properties).Count -ne 15){throw 'Invalid redistributable Cute manifest.'}
    foreach($cursoriumSource in $cursoriumManifest.SourceFiles){
        if((Get-FileHash -LiteralPath (Join-Path $cursoriumTheme.FullName $cursoriumSource.Path)).Hash -ne $cursoriumSource.Sha256){throw 'Published Cute source checksum mismatch.'}
    }
}
Copy-Item -LiteralPath 'README.md','LICENSE','VALIDATION.md' -Destination 'artifacts/cursorium-release/portable'
Copy-Item -LiteralPath 'docs' -Destination 'artifacts/cursorium-release/portable' -Recurse -Force
$taskPortable=[IO.Path]::GetFullPath((Join-Path $frostRoot 'artifacts/cursorium-release/portable'))
@{schemaVersion=1;version=$Version;runtime='net10.0-windows / 10.0.13';entryPoint='FrostWhiteComet.exe';userData='%LOCALAPPDATA%/FrostWhiteComet';files=@(Get-ChildItem -LiteralPath $taskPortable -Recurse -File | ForEach-Object {@{path=$_.FullName.Substring($taskPortable.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskPortable 'distribution-manifest.json') -Encoding utf8
Compress-Archive -Path 'artifacts/cursorium-release/portable/*' -DestinationPath "artifacts/CURSORIUM-$Version-win-x64.zip" -Force
Copy-Item -LiteralPath "artifacts/CURSORIUM-$Version-win-x64.zip" -Destination 'artifacts/installer-payload.zip' -Force
Invoke-Dotnet @('publish','installer/Frost.Setup','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true',"-p:Version=$Version",'-o','artifacts/cursorium-release/setup')
Copy-Item -LiteralPath 'artifacts/cursorium-release/setup/FrostWhiteCometSetup.exe' -Destination "artifacts/CURSORIUM-Setup-$Version-Release.exe" -Force
Compress-Archive -Path 'assets/cursors','assets/themes','assets/catalog' -DestinationPath "artifacts/CURSORIUM-Themes-$Version.zip" -Force
& (Join-Path $PSScriptRoot 'package-source.ps1') -Version $Version
Get-FileHash -Algorithm SHA256 -LiteralPath "artifacts/CURSORIUM-Setup-$Version-Release.exe","artifacts/CURSORIUM-$Version-win-x64.zip","artifacts/CURSORIUM-Themes-$Version.zip","artifacts/CURSORIUM-Source-$Version.zip" | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath 'artifacts/CURSORIUM-SHA256SUMS.txt'
Write-Host "Release outputs: $frostRoot\artifacts"
