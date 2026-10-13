param([string]$LibraryPath='D:\codex-workspaces\cursorium-library',[string]$ExportPath,[string]$Version='1.0.0',[switch]$Prepared)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(-not $ExportPath){$ExportPath=Join-Path $projectRoot 'artifacts/library-export'}
$catalogFile=Join-Path $ExportPath 'catalog/v1/index.json'
$catalog=Get-Content -LiteralPath $catalogFile -Raw | ConvertFrom-Json
if($catalog.SchemaVersion -ne 1 -or $catalog.CatalogVersion -ne $Version){throw 'Catalog version mismatch.'}
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
if(git -C $LibraryPath ls-remote origin "refs/tags/library-v$Version"){throw 'This library version is already published. Use a new immutable resource release version.'}
if(Test-Path -LiteralPath (Join-Path $LibraryPath 'catalog/v1/index.json')){$existing=Get-Content -LiteralPath (Join-Path $LibraryPath 'catalog/v1/index.json') -Raw|ConvertFrom-Json;if(-not $Prepared -and [version]$catalog.CatalogVersion -le [version]$existing.CatalogVersion){throw 'The new signed catalog must advance CatalogVersion.'}}
if(-not $Prepared){
Copy-Item -LiteralPath (Join-Path $ExportPath 'catalog') -Destination $LibraryPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $ExportPath 'catalog-public-key.pem') -Destination $LibraryPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/CURSORIUM-v1.2-SOURCES.md') -Destination (Join-Path $LibraryPath 'SOURCES.md') -Force
[IO.File]::WriteAllText((Join-Path $LibraryPath '.nojekyll'),'')
[IO.File]::WriteAllText((Join-Path $LibraryPath '.gitignore'),"packages/`nwork/`n*.private.pem`n")
[IO.File]::WriteAllText((Join-Path $LibraryPath '.gitattributes'),"catalog/** -text`n*.pem text eol=lf`n")
[IO.File]::WriteAllText((Join-Path $LibraryPath 'README.md'),@'
# CURSORIUM Library

Official public, data-only catalog. Index: https://catvir4493.github.io/cursorium-library/catalog/v1/index.json

Resources are versioned GitHub Release ZIPs, not executable plugins. Existing five Cute Collection IDs remain stable and bundled installations are recognized without another download. Cool Cat - Superstar uses Bobcat's original bonus ANI as Normal; no pixels, timing or hotspots were changed. Four original numeric motion presets use CURSORIUM's Comet, Neon and Stardust renderers.

Authors and licenses are in SOURCES.md and each package NOTICE.txt. Original third-party sprites remain Public Domain; inherited FROST roles and original presets follow the application's MIT license. License statements were rechecked on 2026-10-13, and fresh original ZIP SHA256 values matched the reviewed source archive records.

The exact UTF-8 index.json bytes have a detached 3072-bit RSA-PSS/SHA256 signature in index.json.sig. The client pins catalog-public-key.pem. The signing private key is maintained outside both repositories and all release assets. SHA256 verifies package bytes against signed metadata. HTTPS URLs and every redirect are restricted to this Pages site, this repository's Release downloads and GitHub's release asset CDN. A compromised signing key can authorize malicious data; native cursor validation remains required. Key rotation requires a reviewed client update. The first installation does not have an independent catalog expiry/transparency service.

To add a resource: verify redistribution permission, validate every CUR/ANI and all 15 roles, retain provenance/NOTICE, build a data-only ZIP, create a new immutable library Release, calculate size and SHA256, add metadata and an actual resource-derived PNG cover, increase CatalogVersion, validate the runtime schema, sign the exact index bytes with the protected key, and commit/push the catalog. Never change an existing release asset, stable ID, or tag. New resources need no desktop release when compatible with cursorium-1.

The application repository contains scripts/publish-library.ps1 and the --isolated-test --export-library publishing tool. Do not put packages, local staging, private keys, user settings or registry backups in this repository.
'@)
Invoke-LibraryGit @('add','catalog','catalog-public-key.pem','SOURCES.md','README.md','.gitignore','.gitattributes','.nojekyll')
Invoke-LibraryGit @('commit','-m',"feat: publish signed resource catalog $Version")
}
Invoke-LibraryGit @('push','--set-upstream','origin','main')
$commit=(git -C $LibraryPath rev-parse HEAD).Trim();$remote=(git -C $LibraryPath ls-remote origin refs/heads/main) -split '\s+'
if($remote[0] -ne $commit){throw 'Library remote commit mismatch.'}
$tag="library-v$Version";Invoke-LibraryGit @('tag','-a',$tag,'-m',"CURSORIUM resource library $Version");Invoke-LibraryGit @('push','origin',"refs/tags/$tag")
$notes="Ten verified data-only resources: five original Cute themes, Bobcat Superstar role-mapping variant and four original motion presets. See SOURCES.md and package NOTICE.txt. The desktop never automatically applies a download."
$body=@{tag_name=$tag;target_commitish=$commit;name="CURSORIUM Library $Version";body=$notes;draft=$true}|ConvertTo-Json
$release=Invoke-RestMethod -Method Post -Uri "$api/releases" -Headers $headers -ContentType 'application/json' -Body $body
$sums=@()
foreach($resource in $catalog.Resources){$file=Get-Item -LiteralPath (Join-Path $ExportPath ('packages/'+$resource.Id+'.zip'));$hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant();if($hash -ne $resource.SHA256 -or $file.Length -ne $resource.PackageSize){throw 'Package checksum mismatch.'};$sums+="$hash  $($file.Name)"}
$sumsPath=Join-Path $ExportPath 'SHA256SUMS.txt';[IO.File]::WriteAllLines($sumsPath,$sums)
foreach($file in @((Get-ChildItem -LiteralPath (Join-Path $ExportPath 'packages') -File -Filter '*.zip'))+(Get-Item -LiteralPath $sumsPath)){
 $uri=($release.upload_url -replace '\{.*\}$','')+'?name='+[Uri]::EscapeDataString($file.Name)
 $asset=Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -InFile $file.FullName -ContentType 'application/octet-stream' -TimeoutSec 120
 $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
 if($asset.size -ne $file.Length -or $asset.digest -ne ('sha256:'+$hash)){throw 'Uploaded library digest mismatch.'}
}
$release=Invoke-RestMethod -Method Patch -Uri "$api/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false}'
try{$pages=Invoke-RestMethod "$api/pages" -Headers $headers}catch{if([int]$_.Exception.Response.StatusCode -ne 404){throw};$pages=Invoke-RestMethod -Method Post -Uri "$api/pages" -Headers $headers -ContentType 'application/json' -Body '{"source":{"branch":"main","path":"/"}}'}
[ordered]@{commit=$commit;repository=$repo.html_url;release=$release.html_url;pages=$pages.html_url;assets=$release.assets.Count} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ExportPath 'publication.json') -Encoding UTF8
Write-Host "Library published: $($repo.html_url); release: $($release.html_url); Pages deployment pending verification."
