param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
$taskcodeRoot = Split-Path -Parent $PSScriptRoot
$lines = Get-ChildItem -LiteralPath (Join-Path $taskcodeRoot 'locales') -Filter '*.json' | ForEach-Object {
    '/resource:"' + $_.FullName + '",Taskcode.Locales.' + $_.Name
}
[IO.File]::WriteAllLines($Output, [string[]]$lines)
