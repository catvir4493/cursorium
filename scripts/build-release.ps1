param([switch]$SkipVerification)
$ErrorActionPreference = 'Stop'
$frostRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $frostRoot
function Invoke-Dotnet { param([string[]]$Arguments) & dotnet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
Invoke-Dotnet @('run','--project','tools/Frost.CursorGen','-c','Release','--','assets/cursors')
Invoke-Dotnet @('run','--project','tools/Frost.CursorGen','-c','Release','--','assets/themes/obsidian','--obsidian')
Invoke-Dotnet @('build','Frost.sln','-c','Release','--no-incremental')
if (-not $SkipVerification) {
    Invoke-Dotnet @('run','--project','tests/Frost.Verify','-c','Release','--','assets/cursors')
    Invoke-Dotnet @('run','--project','tests/Frost.Verify','-c','Release','--','assets/themes/obsidian')
}
New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
Invoke-Dotnet @('publish','src/Frost.App','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false','-o','artifacts/cursorium-release/portable')
if ((Get-ChildItem -LiteralPath 'artifacts/cursorium-release/portable/Theme' -Filter '*.cur').Count -ne 15 -or (Get-ChildItem -LiteralPath 'artifacts/cursorium-release/portable/Themes/obsidian' -Filter '*.cur').Count -ne 15 -or -not (Test-Path -LiteralPath 'artifacts/cursorium-release/portable/Catalog/effects.json')) { throw 'Published CURSORIUM theme/effect resources are incomplete.' }
Copy-Item -LiteralPath 'README.md','LICENSE','VALIDATION.md' -Destination 'artifacts/cursorium-release/portable'
Copy-Item -LiteralPath 'docs' -Destination 'artifacts/cursorium-release/portable' -Recurse -Force
Compress-Archive -Path 'artifacts/cursorium-release/portable/*' -DestinationPath 'artifacts/CURSORIUM-1.0.0-win-x64.zip' -Force
Copy-Item -LiteralPath 'artifacts/CURSORIUM-1.0.0-win-x64.zip' -Destination 'artifacts/installer-payload.zip' -Force
Invoke-Dotnet @('publish','installer/Frost.Setup','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o','artifacts/cursorium-release/setup')
Copy-Item -LiteralPath 'artifacts/cursorium-release/setup/FrostWhiteCometSetup.exe' -Destination 'artifacts/CURSORIUM-Setup-1.0.0-Release.exe' -Force
Compress-Archive -Path 'assets/cursors','assets/themes','assets/catalog' -DestinationPath 'artifacts/CURSORIUM-Themes-1.0.0.zip' -Force
& (Join-Path $PSScriptRoot 'package-source.ps1')
Get-FileHash -Algorithm SHA256 -LiteralPath 'artifacts/CURSORIUM-Setup-1.0.0-Release.exe','artifacts/CURSORIUM-1.0.0-win-x64.zip','artifacts/CURSORIUM-Themes-1.0.0.zip','artifacts/CURSORIUM-Source-1.0.0.zip' | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath 'artifacts/CURSORIUM-SHA256SUMS.txt'
Write-Host "Release outputs: $frostRoot\artifacts"
