param(
    [switch]$Release,
    [switch]$Prerelease,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.5.2',
    [string]$NotesPath
)
$ErrorActionPreference='Stop'
$cursoriumRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $cursoriumRoot
$cursoriumRepository='catvir4493/cursorium'
$cursoriumRemote=(git remote get-url origin).Trim()
if($LASTEXITCODE -ne 0 -or $cursoriumRemote -notin @('https://github.com/catvir4493/cursorium.git','git@github.com:catvir4493/cursorium.git')){throw 'origin must point to the CURSORIUM repository.'}
if((git branch --show-current).Trim() -ne 'main'){throw 'Switch the completed release work to main before publishing.'}
if(git status --porcelain){throw 'Commit or preserve working-tree changes before publishing.'}
function Invoke-CursoriumGit([string[]]$Arguments){
    & git @Arguments
    if($LASTEXITCODE -ne 0){throw "Git operation failed: $($Arguments[0])"}
}
Invoke-CursoriumGit @('push','--set-upstream','origin','main')
$cursoriumCommit=(git rev-parse HEAD).Trim()
$cursoriumRemoteHead=(git ls-remote origin refs/heads/main) -split '\s+'
if($LASTEXITCODE -ne 0 -or $cursoriumRemoteHead[0] -ne $cursoriumCommit){throw 'Remote main does not match the completed local commit.'}
Write-Host "Source uploaded and verified: https://github.com/$cursoriumRepository/commit/$cursoriumCommit"
if(-not $Release){return}

if(-not $NotesPath){$NotesPath="docs/releases/$Version.md"}
# File-provider note properties on Get-Content strings are serialized as an object
# by Windows PowerShell 5.1. The GitHub body must be a plain JSON string.
$cursoriumNotes=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $NotesPath).Path,[Text.Encoding]::UTF8)
$cursoriumNames=@("CURSORIUM-Setup-$Version-Release.exe","CURSORIUM-$Version-win-x64.zip","CURSORIUM-Themes-$Version.zip","CURSORIUM-Source-$Version.zip",'CURSORIUM-SHA256SUMS.txt')
$cursoriumAssets=@($cursoriumNames | ForEach-Object {Get-Item -LiteralPath (Join-Path $cursoriumRoot "artifacts/$_")})
$cursoriumChecksumLines=Get-Content -LiteralPath 'artifacts/CURSORIUM-SHA256SUMS.txt'
foreach($cursoriumAsset in $cursoriumAssets | Where-Object Extension -ne '.txt'){
    $cursoriumHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $cursoriumAsset.FullName).Hash
    if(-not ($cursoriumChecksumLines | Where-Object {$_ -eq "$cursoriumHash  $($cursoriumAsset.Name)"})){throw "Checksum mismatch: $($cursoriumAsset.Name)"}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($cursoriumAsset in $cursoriumAssets | Where-Object Extension -eq '.zip'){
    $cursoriumArchive=[IO.Compression.ZipFile]::OpenRead($cursoriumAsset.FullName)
    try{
        foreach($cursoriumEntry in $cursoriumArchive.Entries){
            $cursoriumEntryName=$cursoriumEntry.FullName.Replace('\','/')
            if($cursoriumEntryName -match '(^|/)(artifacts|test-data|\.git|\.env)(/|$)' -or $cursoriumEntryName -match '(^|/)(original-theme|theme-transaction|profiles|studio|setup-result)\.json$' -or $cursoriumEntryName -match '(^|/)frost\.log$'){
                throw "Private/runtime data must not be published: $cursoriumEntryName"
            }
        }
    }finally{$cursoriumArchive.Dispose()}
}
# Capture the credential entirely in memory. Never echo this result or write a token to disk.
$cursoriumCredentialOutput=(@('protocol=https','host=github.com','') -join "`n" | git -c credential.interactive=false credential fill 2>$null)
if($LASTEXITCODE -ne 0){throw 'Git Credential Manager has no usable GitHub credential.'}
$cursoriumCredential=@{}
foreach($cursoriumLine in $cursoriumCredentialOutput){$cursoriumPair=$cursoriumLine -split '=',2;if($cursoriumPair.Count -eq 2){$cursoriumCredential[$cursoriumPair[0]]=$cursoriumPair[1]}}
if(-not $cursoriumCredential.password){throw 'No GitHub authentication token was supplied.'}
$cursoriumHeaders=@{Authorization='Bearer '+$cursoriumCredential.password;Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28';'User-Agent'='CURSORIUM-release'}
$cursoriumTag="cursorium-v$Version"
$cursoriumTagRef="refs/tags/$cursoriumTag"
git show-ref --verify --quiet $cursoriumTagRef
if($LASTEXITCODE -eq 0){
    if((git rev-parse ($cursoriumTag+'^{commit}')).Trim() -ne $cursoriumCommit){throw 'This version tag already belongs to a different commit. Use a new version.'}
}else{Invoke-CursoriumGit @('tag','-a',$cursoriumTag,'-m',"CURSORIUM v$Version")}
Invoke-CursoriumGit @('push','origin',$cursoriumTagRef)
$cursoriumApi="https://api.github.com/repos/$cursoriumRepository"
$cursoriumReleases=Invoke-RestMethod -Uri "$cursoriumApi/releases?per_page=100" -Headers $cursoriumHeaders
$cursoriumRelease=$cursoriumReleases | Where-Object tag_name -eq $cursoriumTag | Select-Object -First 1
if(-not $cursoriumRelease){
    $cursoriumBody=@{tag_name=$cursoriumTag;target_commitish=$cursoriumCommit;name="CURSORIUM v$Version";body=$cursoriumNotes;draft=$true;prerelease=[bool]$Prerelease} | ConvertTo-Json
    $cursoriumRelease=Invoke-RestMethod -Method Post -Uri "$cursoriumApi/releases" -Headers $cursoriumHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($cursoriumBody))
}
foreach($cursoriumAsset in $cursoriumAssets){
    $cursoriumDigest='sha256:'+(Get-FileHash -Algorithm SHA256 -LiteralPath $cursoriumAsset.FullName).Hash.ToLowerInvariant()
    $cursoriumUploaded=$cursoriumRelease.assets | Where-Object name -eq $cursoriumAsset.Name | Select-Object -First 1
    if($cursoriumUploaded){
        if($cursoriumUploaded.size -ne $cursoriumAsset.Length -or $cursoriumUploaded.state -ne 'uploaded' -or $cursoriumUploaded.digest -ne $cursoriumDigest){throw "Existing release asset differs; it will not be replaced: $($cursoriumAsset.Name)"}
        Write-Host "Already uploaded and verified: $($cursoriumAsset.Name)"
        continue
    }
    if(-not $cursoriumRelease.draft){throw 'A published release is incomplete. Use a new version rather than changing a published release.'}
    $cursoriumUploadBase=$cursoriumRelease.upload_url -replace '\{.*\}$',''
    $cursoriumUploadUri=$cursoriumUploadBase+'?name='+[Uri]::EscapeDataString($cursoriumAsset.Name)
    $cursoriumType=if($cursoriumAsset.Extension -eq '.zip'){'application/zip'}elseif($cursoriumAsset.Extension -eq '.txt'){'text/plain'}else{'application/octet-stream'}
    Write-Host "Uploading $($cursoriumAsset.Name) ($([Math]::Round($cursoriumAsset.Length/1MB,1)) MiB)..."
    $cursoriumUploaded=Invoke-RestMethod -Method Post -Uri $cursoriumUploadUri -Headers $cursoriumHeaders -InFile $cursoriumAsset.FullName -ContentType $cursoriumType -TimeoutSec 1800
    if($cursoriumUploaded.state -ne 'uploaded' -or $cursoriumUploaded.size -ne $cursoriumAsset.Length -or $cursoriumUploaded.digest -ne $cursoriumDigest){throw "Uploaded asset digest or size did not match: $($cursoriumAsset.Name)"}
    Write-Host "Uploaded; SHA256 verified: $($cursoriumAsset.Name)"
}
$cursoriumRelease=Invoke-RestMethod -Uri "$cursoriumApi/releases/$($cursoriumRelease.id)" -Headers $cursoriumHeaders
if($cursoriumRelease.assets.Count -ne $cursoriumAssets.Count){throw 'Release asset list is incomplete or unexpected.'}
if($cursoriumRelease.draft){
    $cursoriumBody=@{draft=$false;prerelease=[bool]$Prerelease;make_latest=$(if($Prerelease){'false'}else{'true'})} | ConvertTo-Json
    $cursoriumRelease=Invoke-RestMethod -Method Patch -Uri "$cursoriumApi/releases/$($cursoriumRelease.id)" -Headers $cursoriumHeaders -ContentType 'application/json' -Body $cursoriumBody
}
if($cursoriumRelease.draft){throw 'Release remains a draft.'}
if([bool]$cursoriumRelease.prerelease -ne [bool]$Prerelease){throw 'Published release classification does not match the requested gate.'}
# Server digests alone do not exercise the public download route. Refetch every
# asset anonymously, bounded by the already verified local size, before completion.
$cursoriumPublicRoot=Join-Path $cursoriumRoot "artifacts/remote-v$Version"
New-Item -ItemType Directory -Force -Path $cursoriumPublicRoot | Out-Null
$cursoriumPublicChecks=@()
foreach($cursoriumAsset in $cursoriumRelease.assets){
    $cursoriumLocal=$cursoriumAssets | Where-Object Name -eq $cursoriumAsset.name | Select-Object -First 1
    $cursoriumUri=[Uri]$cursoriumAsset.browser_download_url
    if($cursoriumUri.Scheme -ne 'https' -or $cursoriumUri.Host -ne 'github.com' -or -not $cursoriumUri.AbsolutePath.StartsWith("/$cursoriumRepository/releases/download/$cursoriumTag/",[StringComparison]::Ordinal)){throw 'Unexpected public release download URL.'}
    $cursoriumDownload=Join-Path $cursoriumPublicRoot $cursoriumLocal.Name
    $cursoriumRequest=[Net.HttpWebRequest]::Create($cursoriumUri)
    $cursoriumRequest.UserAgent='CURSORIUM-public-release-verification'
    $cursoriumRequest.Timeout=600000;$cursoriumRequest.ReadWriteTimeout=600000
    $cursoriumResponse=$null;$cursoriumInput=$null;$cursoriumOutput=$null
    try{
        $cursoriumResponse=$cursoriumRequest.GetResponse();$cursoriumInput=$cursoriumResponse.GetResponseStream()
        $cursoriumOutput=[IO.FileStream]::new($cursoriumDownload,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
        $cursoriumBuffer=New-Object byte[] 65536;$cursoriumCount=0L
        while(($cursoriumRead=$cursoriumInput.Read($cursoriumBuffer,0,$cursoriumBuffer.Length)) -gt 0){
            $cursoriumCount+=$cursoriumRead;if($cursoriumCount -gt $cursoriumLocal.Length){throw 'Public download exceeded verified artifact size.'}
            $cursoriumOutput.Write($cursoriumBuffer,0,$cursoriumRead)
        }
        $cursoriumOutput.Flush($true)
        if($cursoriumCount -ne $cursoriumLocal.Length){throw 'Public download size mismatch.'}
    }finally{if($cursoriumOutput){$cursoriumOutput.Dispose()};if($cursoriumInput){$cursoriumInput.Dispose()};if($cursoriumResponse){$cursoriumResponse.Dispose()}}
    $cursoriumActual=(Get-FileHash -LiteralPath $cursoriumDownload -Algorithm SHA256).Hash
    if($cursoriumActual -ne (Get-FileHash -LiteralPath $cursoriumLocal.FullName -Algorithm SHA256).Hash){throw 'Public download SHA256 mismatch.'}
    $cursoriumPublicChecks+=@{name=$cursoriumLocal.Name;size=$cursoriumLocal.Length;sha256=$cursoriumActual;verified=$true}
    Write-Host "Public bytes downloaded and verified: $($cursoriumLocal.Name)"
}
@{passed=$true;verifiedAt=(Get-Date).ToUniversalTime().ToString('O');commit=$cursoriumCommit;release=$cursoriumRelease.html_url;assets=$cursoriumPublicChecks} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $cursoriumPublicRoot 'public-download-verification.json') -Encoding utf8
$cursoriumSummary=[ordered]@{repository=$cursoriumRepository;commit=$cursoriumCommit;tag=$cursoriumTag;published=$true;prerelease=$cursoriumRelease.prerelease;releaseUrl=$cursoriumRelease.html_url;assets=@($cursoriumRelease.assets | Select-Object name,size,digest,browser_download_url)}
$cursoriumSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath 'artifacts/github-publication.json' -Encoding utf8
Write-Host "Release published and verified: $($cursoriumRelease.html_url)"
