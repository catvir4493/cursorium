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
    # git archive honors *.ps1 eol=crlf from .gitattributes even when reading HEAD.
    # Restore canonical Git text bytes so source identity is checkout-independent.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $frostArchive=[IO.Compression.ZipFile]::Open($frostTemporary,[IO.Compression.ZipArchiveMode]::Update)
    try {
        foreach($frostEntry in @($frostArchive.Entries | Where-Object {$_.FullName.EndsWith('.ps1',[StringComparison]::OrdinalIgnoreCase)})){
            $frostName=$frostEntry.FullName;$frostTime=$frostEntry.LastWriteTime;$frostAttributes=$frostEntry.ExternalAttributes
            $frostStream=$frostEntry.Open();$frostMemory=[IO.MemoryStream]::new()
            try {$frostStream.CopyTo($frostMemory);$frostBytes=$frostMemory.ToArray()} finally {$frostStream.Dispose();$frostMemory.Dispose()}
            $frostEntry.Delete();$frostNew=$frostArchive.CreateEntry($frostName);$frostNew.LastWriteTime=$frostTime;$frostNew.ExternalAttributes=$frostAttributes
            $frostStream=$frostNew.Open()
            try {for($frostIndex=0;$frostIndex -lt $frostBytes.Length;$frostIndex++){
                if($frostBytes[$frostIndex] -eq 13 -and $frostIndex+1 -lt $frostBytes.Length -and $frostBytes[$frostIndex+1] -eq 10){continue}
                $frostStream.WriteByte($frostBytes[$frostIndex])
            }} finally {$frostStream.Dispose()}
        }
    } finally {$frostArchive.Dispose()}
    $frostComplete=$true
} finally { if(-not $frostComplete -and (Test-Path -LiteralPath $frostTemporary)){Remove-Item -LiteralPath $frostTemporary} }
if([IO.File]::Exists($frostOutput)){[IO.File]::Replace($frostTemporary,$frostOutput,[NullString]::Value)}else{[IO.File]::Move($frostTemporary,$frostOutput)}
Write-Host "Source package: $frostOutput"
