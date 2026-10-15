# Performance evidence is private and must stay inside ignored workspace artifacts.
# This helper creates nothing: validate all ancestors before any report/target write.
function Get-PerformanceOutputPath([string]$Directory){
 $workspace=Split-Path -Parent $PSScriptRoot
 $allowed=[IO.Path]::GetFullPath((Join-Path $workspace 'artifacts')).TrimEnd([IO.Path]::DirectorySeparatorChar)
 $resolved=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($Directory)){$Directory}else{Join-Path $workspace $Directory})).TrimEnd([IO.Path]::DirectorySeparatorChar)
 if($resolved -ne $allowed -and -not $resolved.StartsWith($allowed+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Performance output must stay inside ignored workspace artifacts, never production installation or user data'}
 for($ancestor=$resolved;$ancestor;$ancestor=[IO.Path]::GetDirectoryName($ancestor)){
  if((Test-Path -LiteralPath $ancestor)-and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked performance output is not an isolated writable directory'}
 }
 return $resolved
}
