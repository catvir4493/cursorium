$ErrorActionPreference='Stop'
$frostRoot=Split-Path -Parent $PSScriptRoot
$frostOutput=Join-Path $frostRoot 'artifacts\FrostWhiteComet-Source-1.0.0.zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $frostOutput) { Remove-Item -LiteralPath $frostOutput -Force }
$frostArchive=[IO.Compression.ZipFile]::Open($frostOutput,[IO.Compression.ZipArchiveMode]::Create)
try {
    $frostFiles=@()
    foreach($frostFolder in @('src','tools','tests','installer','scripts','assets','docs')) {
        $frostFiles+=Get-ChildItem -LiteralPath (Join-Path $frostRoot $frostFolder) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' }
    }
    foreach($frostName in @('Directory.Build.props','Frost.sln','README.md','VALIDATION.md','LICENSE','.gitignore','.gitattributes')) { $frostFiles+=Get-Item -LiteralPath (Join-Path $frostRoot $frostName) }
    foreach($frostFile in $frostFiles) {
        $frostRelative=$frostFile.FullName.Substring($frostRoot.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($frostArchive,$frostFile.FullName,$frostRelative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $frostArchive.Dispose() }
Write-Host "Source package: $frostOutput"
