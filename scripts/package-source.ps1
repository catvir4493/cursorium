param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.5.2',[string]$OutputDirectory='artifacts')
$ErrorActionPreference='Stop'
$frostRoot=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'performance-storage.ps1')
$frostOutputDirectory=Get-PerformanceOutputPath $OutputDirectory
New-Item -ItemType Directory -Path $frostOutputDirectory -Force|Out-Null
$frostOutput=Join-Path $frostOutputDirectory "CURSORIUM-Source-$Version.zip"
$frostTemporary=$frostOutput+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
$frostComplete=$false
try {
    # Archive the committed objects, independent of checkout CRLF conversion.
    # Untracked/private scratch files cannot enter the public source delivery.
    if(git -C $frostRoot status --porcelain --untracked-files=no){throw 'Commit tracked changes before packaging source for a versioned delivery.'}
    $frostPaths=@('src','tools','tests','installer','scripts','assets','docs','Directory.Build.props','Frost.sln','README.md','VALIDATION.md','LICENSE','AGENTS.md','.gitignore','.gitattributes')
    & git -C $frostRoot archive --format=zip "--output=$frostTemporary" HEAD -- @frostPaths
    if($LASTEXITCODE -ne 0){throw 'Committed source archive creation failed.'}
    $frostComplete=$true
} finally { if(-not $frostComplete -and (Test-Path -LiteralPath $frostTemporary)){Remove-Item -LiteralPath $frostTemporary} }
if([IO.File]::Exists($frostOutput)){[IO.File]::Replace($frostTemporary,$frostOutput,[NullString]::Value)}else{[IO.File]::Move($frostTemporary,$frostOutput)}
Write-Host "Source package: $frostOutput"
