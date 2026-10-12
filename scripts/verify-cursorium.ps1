param([string]$ApplicationDirectory='artifacts/cursorium-release/portable',[switch]$SkipBenchmark)
$ErrorActionPreference='Stop'
$cursoriumRoot=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $cursoriumRoot
$cursoriumApp=[IO.Path]::GetFullPath((Join-Path $cursoriumRoot $ApplicationDirectory))
$cursoriumExe=Join-Path $cursoriumApp 'FrostWhiteComet.exe'
if(-not (Test-Path -LiteralPath $cursoriumExe)){throw 'Build the Release package first.'}
$cursoriumReport=Join-Path $cursoriumRoot 'artifacts\cursorium-verification'
New-Item -ItemType Directory -Path $cursoriumReport -Force | Out-Null
# Preserve evidence that the user's existing backup/settings remain unchanged.
$cursoriumData=Join-Path $env:LOCALAPPDATA 'FrostWhiteComet'
$cursoriumProtected=@('original-theme.json','settings.json','studio.json','profiles.json')
$cursoriumBefore=@{}
foreach($cursoriumName in $cursoriumProtected){$cursoriumPath=Join-Path $cursoriumData $cursoriumName;$cursoriumBefore[$cursoriumName]=if(Test-Path -LiteralPath $cursoriumPath){(Get-FileHash -LiteralPath $cursoriumPath).Hash}else{$null}}
function Invoke-CursoriumTest([string[]]$Arguments){
    $cursoriumProcess=Start-Process -FilePath $cursoriumExe -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    if(-not $cursoriumProcess.WaitForExit(60000)){throw 'Desktop test timed out; inspect running test before retrying.'}
    if($cursoriumProcess.ExitCode -ne 0){throw "Desktop test failed: exit $($cursoriumProcess.ExitCode). See reports."}
}
& dotnet run --project tests/Frost.Verify -c Release -- --studio-tests $cursoriumApp $cursoriumReport --native-roundtrip 2>&1 | Tee-Object -FilePath (Join-Path $cursoriumReport 'core-console.txt')
if($LASTEXITCODE -ne 0){throw 'Core/native transaction verification failed.'}
$cursoriumTestingData=Join-Path $cursoriumReport ('desktop-data-'+[guid]::NewGuid().ToString('N'))
Invoke-CursoriumTest @('--self-test','--isolated-test','--test-data-root',('"'+$cursoriumTestingData+'"'),'--report',('"'+$cursoriumReport+'"'))
$cursoriumDesktop=Get-Content -LiteralPath (Join-Path $cursoriumReport 'desktop-report.json') -Raw | ConvertFrom-Json
if($cursoriumDesktop.passed -ne $true){throw 'Desktop report contains a failure.'}
if(-not $SkipBenchmark){Invoke-CursoriumTest @('--benchmark','--isolated-test','--report',('"'+$cursoriumReport+'"'))}
$cursoriumProtectionChecks=@()
foreach($cursoriumName in $cursoriumProtected){
    $cursoriumPath=Join-Path $cursoriumData $cursoriumName
    $cursoriumAfter=if(Test-Path -LiteralPath $cursoriumPath){(Get-FileHash -LiteralPath $cursoriumPath).Hash}else{$null}
    $cursoriumSame=$cursoriumAfter -eq $cursoriumBefore[$cursoriumName]
    $cursoriumProtectionChecks+=@{name=$cursoriumName;unchanged=$cursoriumSame}
    if(-not $cursoriumSame){throw "User data changed during tests: $cursoriumName"}
}
$cursoriumProtectionChecks | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $cursoriumReport 'user-data-protection.json') -Encoding utf8
Write-Host "CURSORIUM verification passed: $($cursoriumDesktop.checks.Count) desktop checks. Reports: $cursoriumReport"
