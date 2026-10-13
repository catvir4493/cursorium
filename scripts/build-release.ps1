param([switch]$SkipVerification,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.2.1')
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
Compress-Archive -Path 'artifacts/cursorium-release/portable/*' -DestinationPath "artifacts/CURSORIUM-$Version-win-x64.zip" -Force
Copy-Item -LiteralPath "artifacts/CURSORIUM-$Version-win-x64.zip" -Destination 'artifacts/installer-payload.zip' -Force
Invoke-Dotnet @('publish','installer/Frost.Setup','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true',"-p:Version=$Version",'-o','artifacts/cursorium-release/setup')
Copy-Item -LiteralPath 'artifacts/cursorium-release/setup/FrostWhiteCometSetup.exe' -Destination "artifacts/CURSORIUM-Setup-$Version-Release.exe" -Force
Compress-Archive -Path 'assets/cursors','assets/themes','assets/catalog' -DestinationPath "artifacts/CURSORIUM-Themes-$Version.zip" -Force
& (Join-Path $PSScriptRoot 'package-source.ps1') -Version $Version
Get-FileHash -Algorithm SHA256 -LiteralPath "artifacts/CURSORIUM-Setup-$Version-Release.exe","artifacts/CURSORIUM-$Version-win-x64.zip","artifacts/CURSORIUM-Themes-$Version.zip","artifacts/CURSORIUM-Source-$Version.zip" | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath 'artifacts/CURSORIUM-SHA256SUMS.txt'
Write-Host "Release outputs: $frostRoot\artifacts"
