param([Parameter(Mandatory=$true)][string]$Executable, [ValidateSet('SelfTest','TrayTest')][string]$Check)
$ErrorActionPreference = 'Stop'
$taskcodeArgument = if ($Check -eq 'SelfTest') { '--self-test' } else { '--tray-test' }
$taskcodeReportName = if ($Check -eq 'SelfTest') { 'self-test.txt' } else { 'tray-test.txt' }
$taskcodeStarted = Get-Date
$taskcodeProcess = New-Object System.Diagnostics.Process
$taskcodeProcess.StartInfo.FileName = [System.IO.Path]::GetFullPath($Executable)
$taskcodeProcess.StartInfo.Arguments = $taskcodeArgument
$taskcodeProcess.StartInfo.UseShellExecute = $false
[void]$taskcodeProcess.Start()
if (-not $taskcodeProcess.WaitForExit(60000)) { $taskcodeProcess.Kill(); throw "$Check timed out." }
$taskcodeExitCode = $taskcodeProcess.ExitCode
$taskcodeProcess.Dispose()
$taskcodeReport = Get-Item -LiteralPath (Join-Path (Split-Path -Parent $Executable) $taskcodeReportName)
$taskcodeText = Get-Content -LiteralPath $taskcodeReport.FullName -Raw
Write-Output $taskcodeText
if ($taskcodeExitCode -ne 0 -or $taskcodeReport.LastWriteTime -lt $taskcodeStarted.AddSeconds(-2) -or $taskcodeText -match '(?m)^FAIL:') { throw "$Check failed." }
