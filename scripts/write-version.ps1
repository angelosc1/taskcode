param([string]$VersionFile, [string]$Version, [Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
if (-not $Version) { $Version = (Get-Content -LiteralPath $VersionFile -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have three numeric components.' }
$text = @"
using System.Reflection;
[assembly: AssemblyTitle("Taskcode")]
[assembly: AssemblyDescription("Claude and Codex quota on the Windows taskbar")]
[assembly: AssemblyProduct("Taskcode")]
[assembly: AssemblyCompany("Taskcode contributors")]
[assembly: AssemblyVersion("$Version.0")]
[assembly: AssemblyFileVersion("$Version.0")]
[assembly: AssemblyInformationalVersion("$Version")]
"@
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Output), $text, (New-Object System.Text.UTF8Encoding $false))
