param([Parameter(Mandatory)][string]$ReviewPath,[Parameter(Mandatory)][string]$SourcesDirectory,[Parameter(Mandatory)][string]$PreviousCatalog,[Parameter(Mandatory)][string]$ReportDirectory,[string]$PrivateKeyPath=(Join-Path $env:LOCALAPPDATA 'CURSORIUM-Publishing/catalog-signing-key.pem'))
$ErrorActionPreference='Stop';$projectRoot=Split-Path -Parent $PSScriptRoot
$ReportDirectory=[IO.Path]::GetFullPath($ReportDirectory)
if(Test-Path -LiteralPath $ReportDirectory){throw 'Use a fresh pipeline verification directory.'}
New-Item -ItemType Directory -Path $ReportDirectory|Out-Null
$review=Get-Content -LiteralPath $ReviewPath -Raw|ConvertFrom-Json -AsHashtable
$previous=Get-Content -LiteralPath $PreviousCatalog -Raw|ConvertFrom-Json
$results=@()
function Run-Preparation([hashtable]$Value,[string]$Name,[bool]$Success,[string]$Key=$PrivateKeyPath){
 $log=Join-Path $ReportDirectory 'isolated-data/frost.log';$before=if(Test-Path -LiteralPath $log){(Get-Content -LiteralPath $log -Raw).Length}else{0}
 $path=Join-Path $ReportDirectory ($Name+'.review.json');$output=Join-Path $ReportDirectory $Name
 $Value|ConvertTo-Json -Depth 30|Set-Content -LiteralPath $path -Encoding utf8
 $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $projectRoot 'src/Frost.App/bin/Release/net10.0-windows/FrostWhiteComet.exe'));$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=$projectRoot
 foreach($a in @('--isolated-test','--prepare-library',$path,[IO.Path]::GetFullPath($SourcesDirectory),[IO.Path]::GetFullPath($PreviousCatalog),$output,$Key,'--test-data-root',(Join-Path $ReportDirectory 'isolated-data'))){$start.ArgumentList.Add($a)}
 $process=[Diagnostics.Process]::Start($start);if(-not $process.WaitForExit(30000)){throw 'Pipeline test timed out; inspect before retry.'}
 if(($process.ExitCode -eq 0) -ne $Success){throw "Unexpected preparation result: $Name"}
 $reason=switch($Name){'missing-key'{'absent-private-key.pem'}'unapproved-license'{'Incomplete/duplicate/unapproved'}'source-hash-mismatch'{'Reviewed source changed'}'duplicate-id'{'Duplicate batch resource ID'}'duplicate-artwork'{'Duplicate cursor artwork'}'missing-role-file'{'Role mapping is missing or ambiguous'}'invalid-renderer'{'Invalid trail preset'}'duplicate-catalog-version'{'Catalog version must advance'}'duplicate-package-version'{'Existing resource identity or immutable version conflict'}default{''}}
 if(-not $Success -and -not (Get-Content -LiteralPath $log -Raw).Substring($before).Contains($reason)){throw "Failure did not exercise the intended boundary: $Name"}
 $script:results+=@{name=$Name;passed=$true;exitCode=$process.ExitCode;expectedSuccess=$Success;verifiedFailureBoundary=$reason}
}
function Copy-Review {$value=$review|ConvertTo-Json -Depth 30|ConvertFrom-Json -AsHashtable;$value.Version='1.1.2';foreach($c in $value.Cursors){$c.Id='pipeline-'+$c.Id};foreach($t in $value.Trails){$t.Id='pipeline-'+$t.Id};return $value}
$v=Copy-Review;Run-Preparation $v 'missing-key' $false (Join-Path $ReportDirectory 'absent-private-key.pem')
$v=Copy-Review;$v.Cursors[0].Approved=$false;Run-Preparation $v 'unapproved-license' $false
$v=Copy-Review;$v.Cursors[0].ArchiveSHA256=('0'*64);Run-Preparation $v 'source-hash-mismatch' $false
$v=Copy-Review;$v.Cursors+=@($v.Cursors[0]);Run-Preparation $v 'duplicate-id' $false
$v=Copy-Review;$copy=$v.Cursors[0]|ConvertTo-Json -Depth 20|ConvertFrom-Json -AsHashtable;$copy.Id='duplicate-artwork';$v.Cursors+=@($copy);Run-Preparation $v 'duplicate-artwork' $false
$v=Copy-Review;$v.Cursors[0].Roles.Normal='missing-normal.cur';Run-Preparation $v 'missing-role-file' $false
$v=Copy-Review;$v.Trails[0].RendererType='untrusted-code';Run-Preparation $v 'invalid-renderer' $false
$v=Copy-Review;$v.Version=$previous.CatalogVersion;Run-Preparation $v 'duplicate-catalog-version' $false
# A legitimate local package update proves the same pipeline supports advancing
# one stable ID. Its signed fixture is private and is never uploaded or applied.
$preset=$review.Trails|Where-Object Id -eq 'moonlight'|Select-Object -First 1
$preset.Version='1.0.1';$preset.Length=90;$preset.Description='Isolated update validation fixture; not a published release.'
$update=@{SchemaVersion=1;Version='1.1.2';Cursors=@();Trails=@($preset);Collections=$review.Collections}
Run-Preparation $update 'valid-immutable-update' $true
$bad=$update|ConvertTo-Json -Depth 30|ConvertFrom-Json -AsHashtable;$bad.Trails[0].Version='1.0.0';Run-Preparation $bad 'duplicate-package-version' $false
@{passed=$true;checks=$results;notes='Real protected-key/pinned-public-key publisher, isolated staging only. No remote mutation and no Windows Apply. Signing key bytes are never logged or copied.'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $ReportDirectory 'pipeline-report.json') -Encoding utf8
