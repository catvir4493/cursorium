param([switch]$SkipVerification)
$ErrorActionPreference = 'Stop'
$frostRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $frostRoot
function Invoke-Dotnet { param([string[]]$Arguments) & dotnet @Arguments; if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $Arguments" } }
Invoke-Dotnet @('run','--project','tools/Frost.CursorGen','-c','Release','--','assets/cursors')
Invoke-Dotnet @('build','Frost.sln','-c','Release','--no-incremental')
if (-not $SkipVerification) { Invoke-Dotnet @('run','--project','tests/Frost.Verify','-c','Release','--','assets/cursors') }
New-Item -ItemType Directory -Path 'artifacts' -Force | Out-Null
Invoke-Dotnet @('publish','src/Frost.App','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=false','-o','artifacts/publish/portable')
if ((Get-ChildItem -LiteralPath 'artifacts/publish/portable/Theme' -Filter '*.cur').Count -ne 15 -or (Get-ChildItem -LiteralPath 'artifacts/publish/portable/Theme' -Filter '*.ani').Count -ne 2 -or -not (Test-Path -LiteralPath 'artifacts/publish/portable/frost.ico')) { throw 'Published native cursor/icon resources are incomplete.' }
Copy-Item -LiteralPath 'README.md','LICENSE' -Destination 'artifacts/publish/portable'
if (Test-Path -LiteralPath 'VALIDATION.md') { Copy-Item -LiteralPath 'VALIDATION.md' -Destination 'artifacts/publish/portable' }
if (Test-Path -LiteralPath 'docs') { Copy-Item -LiteralPath 'docs' -Destination 'artifacts/publish/portable' -Recurse -Force }
Compress-Archive -Path 'artifacts/publish/portable/*' -DestinationPath 'artifacts/FrostWhiteComet-1.0.0-win-x64.zip' -Force
Copy-Item -LiteralPath 'artifacts/FrostWhiteComet-1.0.0-win-x64.zip' -Destination 'artifacts/installer-payload.zip' -Force
Invoke-Dotnet @('publish','installer/Frost.Setup','-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeNativeLibrariesForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-o','artifacts/publish/setup')
Copy-Item -LiteralPath 'artifacts/publish/setup/FrostWhiteCometSetup.exe' -Destination 'artifacts/FrostWhiteComet-Setup-1.0.0-Release.exe' -Force
Compress-Archive -Path 'assets/cursors/*' -DestinationPath 'artifacts/FrostWhiteComet-CursorTheme-1.0.0.zip' -Force
& (Join-Path $PSScriptRoot 'package-source.ps1')
Get-FileHash -Algorithm SHA256 -LiteralPath 'artifacts/FrostWhiteComet-Setup-1.0.0-Release.exe','artifacts/FrostWhiteComet-1.0.0-win-x64.zip','artifacts/FrostWhiteComet-CursorTheme-1.0.0.zip','artifacts/FrostWhiteComet-Source-1.0.0.zip' | ForEach-Object { "$($_.Hash)  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath 'artifacts/SHA256SUMS.txt'
Write-Host "Release outputs: $frostRoot\artifacts"
