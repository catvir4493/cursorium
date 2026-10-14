param([string]$LibraryPath='D:\codex-workspaces\cursorium-library',[string]$ExportPath,[string]$Version='1.0.0',[switch]$Prepared,[string]$PreviousCatalog,[switch]$Resume,[switch]$MetadataOnly)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not $ExportPath){$ExportPath=Join-Path $projectRoot 'artifacts/library-export'}
$catalogFile=Join-Path $ExportPath 'catalog/v1/index.json'
$catalog=Get-Content -LiteralPath $catalogFile -Raw | ConvertFrom-Json
if($catalog.SchemaVersion -ne 1 -or $catalog.CatalogVersion -ne $Version){throw 'Catalog version mismatch.'}
if(-not $PreviousCatalog){throw 'Supply the previous signed catalog captured before staging.'}
$ExportPath=[IO.Path]::GetFullPath($ExportPath)
$start=[Diagnostics.ProcessStartInfo]::new((Join-Path $projectRoot 'src/Frost.App/bin/Release/net10.0-windows/FrostWhiteComet.exe'))
$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('--isolated-test','--verify-library',$ExportPath,[IO.Path]::GetFullPath($PreviousCatalog),'--test-data-root',(Join-Path $ExportPath 'publisher-data'))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start)
if(-not $process.WaitForExit(60000) -or $process.ExitCode -ne 0){throw 'Preflight failed; previous public catalog is untouched. Inspect publisher-data/frost.log.'}
$preflight=Get-Content -LiteralPath (Join-Path $ExportPath 'preflight.json') -Raw|ConvertFrom-Json
if($MetadataOnly){
 if(-not $Prepared -or -not $preflight.metadataOnly -or (git -C $LibraryPath status --porcelain) -or (git -C $LibraryPath remote get-url origin).Trim() -ne 'https://github.com/catvir4493/cursorium-library.git'){throw 'A clean reviewed metadata-only checkout with the expected remote is required.'}
 foreach($file in Get-ChildItem -LiteralPath (Join-Path $ExportPath 'catalog') -File -Recurse){$target=Join-Path $LibraryPath ([IO.Path]::GetRelativePath($ExportPath,$file.FullName));if(-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw 'Prepared metadata/media differs from signed staging.'}}
 # Reuse the production anonymous downloader to prove unchanged package availability
 # before promoting metadata. No release assets or published tags are modified.
 $old=Get-Content -LiteralPath $PreviousCatalog -Raw|ConvertFrom-Json
 $check=[Diagnostics.ProcessStartInfo]::new($start.FileName);$check.UseShellExecute=$false;$check.CreateNoWindow=$true
 foreach($arg in @('--isolated-test','--verify-library-public',(Join-Path $ExportPath 'previous-public-verification'),$old.CatalogVersion,'--test-data-root',(Join-Path $ExportPath 'public-verifier-data'))){$check.ArgumentList.Add($arg)}
 $running=[Diagnostics.Process]::Start($check);if(-not $running.WaitForExit(60000) -or $running.ExitCode -ne 0){throw 'Previous public resources could not be verified; signed catalog retained.'}
 & git -C $LibraryPath push origin HEAD:refs/heads/main;if($LASTEXITCODE -ne 0){throw 'Metadata push failed; history will not be rewritten.'}
 $commit=(git -C $LibraryPath rev-parse HEAD).Trim();$remote=(git -C $LibraryPath ls-remote origin refs/heads/main) -split '\s+';if($remote[0] -ne $commit){throw 'Remote metadata commit mismatch.'}
 [ordered]@{commit=$commit;metadataOnly=$true;catalogVersion=$Version;packagesChanged=0;resources=$catalog.Resources.Count}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $ExportPath 'publication.json') -Encoding utf8
 return
}
if($preflight.metadataOnly){throw 'Use -MetadataOnly for a catalog/media-only update; no empty resource Release will be created.'}
# Credentials are only held in memory, never written to the library or application.
$credentialLines=(@('protocol=https','host=github.com','') -join "`n" | git -c credential.interactive=false credential fill 2>$null)
if($LASTEXITCODE -ne 0){throw 'GitHub credential unavailable.'}
$credentials=@{}
foreach($line in $credentialLines){$pair=$line -split '=',2;if($pair.Count -eq 2){$credentials[$pair[0]]=$pair[1]}}
if(-not $credentials.password){throw 'No GitHub credential.'}
$headers=@{Authorization='Bearer '+$credentials.password;Accept='application/vnd.github+json';'X-GitHub-Api-Version'='2022-11-28';'User-Agent'='CURSORIUM-library-publisher'}
$repository='catvir4493/cursorium-library';$api="https://api.github.com/repos/$repository"
try{$repo=Invoke-RestMethod $api -Headers $headers}catch{if([int]$_.Exception.Response.StatusCode -ne 404){throw};$body=@{name='cursorium-library';description='Official signed CURSORIUM catalog and reviewed data-only resources';private=$false;auto_init=$false}|ConvertTo-Json;$repo=Invoke-RestMethod -Method Post 'https://api.github.com/user/repos' -Headers $headers -ContentType 'application/json' -Body $body}
if(-not $repo.permissions.push){throw 'No library push permission.'}
function Invoke-LibraryGit([string[]]$Arguments){& git -C $LibraryPath @Arguments;if($LASTEXITCODE -ne 0){throw "Library git failed: $($Arguments[0])"}}
if(-not (Test-Path -LiteralPath (Join-Path $LibraryPath '.git'))){if(Test-Path -LiteralPath $LibraryPath){throw 'Library path exists without Git; preserve it and choose an empty path.'};New-Item -ItemType Directory -Path $LibraryPath | Out-Null;Invoke-LibraryGit @('init','-b','main');Invoke-LibraryGit @('remote','add','origin',"https://github.com/$repository.git");Invoke-LibraryGit @('config','user.name',(git -C $projectRoot config user.name).Trim());Invoke-LibraryGit @('config','user.email',(git -C $projectRoot config user.email).Trim())}
if((git -C $LibraryPath remote get-url origin).Trim() -ne "https://github.com/$repository.git"){throw 'Unexpected library remote.'}
if(git -C $LibraryPath status --porcelain){throw 'Library checkout has uncommitted changes; preserve them before export.'}
if((git -C $LibraryPath ls-remote origin "refs/tags/library-v$Version") -and -not $Resume){throw 'This library version is already published. Use a new immutable resource release version.'}
if(Test-Path -LiteralPath (Join-Path $LibraryPath 'catalog/v1/index.json')){$existing=Get-Content -LiteralPath (Join-Path $LibraryPath 'catalog/v1/index.json') -Raw|ConvertFrom-Json;if(-not $Prepared -and [version]$catalog.CatalogVersion -le [version]$existing.CatalogVersion){throw 'The new signed catalog must advance CatalogVersion.'}}
if(-not $Prepared){throw 'Commit reviewed catalog, media and attribution first, then use -Prepared.'}
foreach($relative in @('catalog/v1/index.json','catalog/v1/index.json.sig','catalog/v1/collections/index.json','catalog/v1/collections/index.json.sig')){if((Get-FileHash -LiteralPath (Join-Path $LibraryPath $relative)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $ExportPath $relative)).Hash){throw 'Prepared checkout differs from the verified stage.'}}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $ExportPath 'catalog') -File -Recurse){$relative=[IO.Path]::GetRelativePath($ExportPath,$file.FullName);$preparedFile=Join-Path $LibraryPath $relative;if(-not (Test-Path -LiteralPath $preparedFile) -or (Get-FileHash -LiteralPath $preparedFile).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw 'Prepared media differs from verified staging; nothing will be published.'}}
$commit=(git -C $LibraryPath rev-parse HEAD).Trim();$remote=(git -C $LibraryPath ls-remote origin refs/heads/main) -split '\s+'
& git -C $LibraryPath merge-base --is-ancestor $remote[0] $commit
if($LASTEXITCODE -ne 0){throw 'Prepared commit must fast-forward published main.'}
$tag="library-v$Version"
if(-not (git -C $LibraryPath tag --list $tag)){Invoke-LibraryGit @('tag','-a',$tag,'-m',"CURSORIUM resource library $Version")}
if((git -C $LibraryPath rev-parse "$tag^{}").Trim() -ne $commit){throw 'Release tag differs from reviewed commit; it will not be moved.'}
Invoke-LibraryGit @('push','origin',"refs/tags/$tag")
$batch=@($catalog.Resources|Where-Object {$_.PackageUrl.StartsWith("https://github.com/$repository/releases/download/$tag/",[StringComparison]::Ordinal)})
$notes="$($batch.Count) new reviewed data-only resources; $($catalog.Resources.Count) catalog resources. See SOURCES.md and package NOTICE.txt. Download never applies Windows pointers."
$release=$null
try{$release=Invoke-RestMethod "$api/releases/tags/$tag" -Headers $headers}catch{if([int]$_.Exception.Response.StatusCode -ne 404){throw}}
if($release -and -not $Resume){throw 'Release exists; inspect it before using -Resume.'}
if(-not $release){$body=@{tag_name=$tag;target_commitish=$commit;name="CURSORIUM Library $Version";body=$notes;draft=$true}|ConvertTo-Json;$release=Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json' -Body $body}
$sums=@()
foreach($resource in $batch){$file=Get-Item -LiteralPath (Join-Path $ExportPath ('packages/'+$resource.Id+'.zip'));$hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();if($hash -ne $resource.SHA256 -or $file.Length -ne $resource.PackageSize){throw 'Package checksum mismatch.'};$sums+="$hash  $($file.Name)"}
$sumsPath=Join-Path $ExportPath 'SHA256SUMS.txt';[IO.File]::WriteAllLines($sumsPath,$sums)
foreach($file in @((Get-ChildItem -LiteralPath (Join-Path $ExportPath 'packages') -File -Filter '*.zip'))+(Get-Item -LiteralPath $sumsPath)){
 $uri=($release.upload_url -replace '\{.*\}$','')+'?name='+[Uri]::EscapeDataString($file.Name)
 $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
 $existing=@($release.assets|Where-Object name -eq $file.Name)
 if($existing.Count){if($existing.Count -ne 1 -or $existing[0].size -ne $file.Length -or $existing[0].digest -ne ('sha256:'+$hash)){throw 'Existing immutable asset differs; it will not be replaced.'};continue}
 if(-not $release.draft){throw 'Published release cannot be modified.'}
 $asset=Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -InFile $file.FullName -ContentType 'application/octet-stream' -TimeoutSec 120
 $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
 if($asset.size -ne $file.Length -or $asset.digest -ne ('sha256:'+$hash)){throw 'Uploaded library digest mismatch.'}
}
if($release.draft){$release=Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false}'}
# Packages must be anonymously downloadable BEFORE promoting the signed Pages index.
$publicDownloads=Join-Path $ExportPath 'public-downloads';New-Item -ItemType Directory -Path $publicDownloads -Force|Out-Null
foreach($file in @((Get-ChildItem -LiteralPath (Join-Path $ExportPath 'packages') -File -Filter '*.zip'))+(Get-Item -LiteralPath $sumsPath)){
 $target=Join-Path $publicDownloads $file.Name
 Invoke-WebRequest "https://github.com/$repository/releases/download/$tag/$([Uri]::EscapeDataString($file.Name))" -OutFile $target -TimeoutSec 120
 if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw 'Public package download mismatch; previous catalog retained.'}
}
Invoke-LibraryGit @('push','origin','HEAD:refs/heads/main')
$remote=(git -C $LibraryPath ls-remote origin refs/heads/main) -split '\s+'
if($remote[0] -ne $commit){throw 'Library remote commit mismatch.'}
try{$pages=Invoke-RestMethod "$api/pages" -Headers $headers}catch{if([int]$_.Exception.Response.StatusCode -ne 404){throw};$pages=Invoke-RestMethod -Method Post -Uri "$api/pages" -Headers $headers -ContentType 'application/json' -Body '{"source":{"branch":"main","path":"/"}}'}
[ordered]@{commit=$commit;repository=$repo.html_url;release=$release.html_url;pages=$pages.html_url;newPackages=$batch.Count;resources=$catalog.Resources.Count;publicDownloadsVerified=$sums.Count+1} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ExportPath 'publication.json') -Encoding UTF8
Write-Host "Library published: $($repo.html_url); release: $($release.html_url); Pages deployment pending verification."
