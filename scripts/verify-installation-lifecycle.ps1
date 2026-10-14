param([string]$Version='1.5.1',[string]$ReportDirectory='artifacts/v1.5-installation')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskOutput=[IO.Path]::GetFullPath((Join-Path $taskRoot $ReportDirectory));New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskSandbox=Join-Path ([IO.Path]::GetTempPath()) ('CursoriumUpgradeTest-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskSandbox | Out-Null
$taskProgram=Join-Path $taskSandbox 'Program';$taskData=Join-Path $taskSandbox 'Data'
New-Item -ItemType Directory -Path $taskData | Out-Null
$taskFixture=Join-Path $taskData 'private-import.cur';Copy-Item -LiteralPath (Join-Path $taskRoot 'assets/cursors/Normal.cur') -Destination $taskFixture
$taskFixtureHash=(Get-FileHash -LiteralPath $taskFixture).Hash
$taskKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FrostWhiteComet-'+[IO.Path]::GetFileName($taskSandbox)
$taskChecks=@();$taskStarted=(Get-Date).ToUniversalTime().ToString('O')
function Check([bool]$Value,[string]$Name){$script:taskChecks+=@{name=$Name;passed=$Value};if(-not $Value){throw $Name}}
function Run-Setup([bool]$Remove=$false,[int]$Expected=0){
 $taskExecutable=if($Remove){Join-Path $taskProgram 'Uninstall.exe'}else{Join-Path $taskRoot "artifacts/CURSORIUM-Setup-$Version-Release.exe"}
 $taskStart=[Diagnostics.ProcessStartInfo]::new($taskExecutable);$taskStart.UseShellExecute=$false;$taskStart.CreateNoWindow=$true
 $taskStart.Arguments='--silent --no-launch --isolated-test --test-install-root "'+$taskSandbox+'"'+$(if($Remove){' --uninstall'}else{''})
 $taskProcess=[Diagnostics.Process]::Start($taskStart);if(-not $taskProcess.WaitForExit(60000)){throw 'Isolated installer timed out'}
 Check ($taskProcess.ExitCode -eq $Expected) "Installer expected exit $Expected (remove=$Remove)"
}
function Native-State {
 $taskNative=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Control Panel\Cursors')
 try {@($taskNative.GetValueNames() | Sort-Object | ForEach-Object {@{name=$_;kind=$taskNative.GetValueKind($_).ToString();value=$taskNative.GetValue($_,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}}) | ConvertTo-Json -Depth 8 -Compress}finally{$taskNative.Dispose()}
}
$taskNativeBefore=Native-State
try {
 Run-Setup;Check (Test-Path -LiteralPath (Join-Path $taskProgram 'frost-install.marker')) 'Fresh isolated install creates marked payload'
 Check ((Get-ItemProperty -LiteralPath $taskKey).DisplayVersion -eq $Version) 'Isolated registration records requested version'
 Run-Setup;Check ((Get-FileHash -LiteralPath $taskFixture).Hash -eq $taskFixtureHash) 'Repeated installation preserves imported cursor'
 # Both resolved move targets are fixed children of this generated test sandbox.
 $taskPrevious=$taskProgram+'.previous'
 if([IO.Path]::GetDirectoryName($taskProgram) -ne $taskSandbox -or [IO.Path]::GetDirectoryName($taskPrevious) -ne $taskSandbox -or (Test-Path -LiteralPath $taskPrevious)){throw 'Unsafe interruption simulation paths'}
 [IO.Directory]::Move($taskProgram,$taskPrevious)
 Run-Setup;Check ((Test-Path -LiteralPath $taskProgram)-and -not(Test-Path -LiteralPath $taskPrevious)) 'Interrupted rename is recovered before retry installation'
 $taskForeign=Join-Path $taskSandbox 'Foreign';New-Item -ItemType Directory -Path $taskForeign | Out-Null
 $taskForeignFile=Join-Path $taskForeign 'keep.txt';[IO.File]::WriteAllText($taskForeignFile,'preserve external data')
 $taskLink=Join-Path $taskProgram 'linked-data';New-Item -ItemType Junction -Path $taskLink -Target $taskForeign | Out-Null
 Run-Setup $true 1;Check ((Test-Path -LiteralPath $taskProgram)-and (Test-Path -LiteralPath $taskForeignFile)) 'Linked program tree blocks uninstall without touching target'
 # Remove only the test junction entry, never recurse into its target.
 Remove-Item -LiteralPath $taskLink -Force
 $taskBackup=Join-Path $taskData 'original-theme.json';[IO.File]::WriteAllText($taskBackup,'{"SchemaVersion":99}')
 Run-Setup $true 1;Check ((Test-Path -LiteralPath $taskProgram)-and (Test-Path -LiteralPath $taskBackup)-and (Test-Path -LiteralPath $taskKey)) 'Recovery failure blocks program and registration deletion'
 Remove-Item -LiteralPath $taskBackup
 Run-Setup $true
 for($taskAttempt=0;$taskAttempt -lt 100 -and (Test-Path -LiteralPath $taskProgram);$taskAttempt++){Start-Sleep -Milliseconds 100}
 Check (-not(Test-Path -LiteralPath $taskProgram)) 'Installed uninstaller helper actually deletes only owned program files'
 Check ((Get-FileHash -LiteralPath $taskFixture).Hash -eq $taskFixtureHash) 'Uninstall retains user imported resource bytes'
 Check (-not(Test-Path -LiteralPath $taskKey)) 'Isolated uninstall removes its own registration'
 Check ((Native-State) -eq $taskNativeBefore) 'Whole lifecycle preserves actual Windows cursor values and types'
} finally {
 @{passed=(@($taskChecks | Where-Object {-not $_.passed}).Count -eq 0);started=$taskStarted;ended=(Get-Date).ToUniversalTime().ToString('O');checks=$taskChecks;note='Actual Windows installer with isolated Program/Data/shortcut/registration. No production uninstall, restart, physical drive removal, or power interruption.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'lifecycle-report.json') -Encoding utf8
}
