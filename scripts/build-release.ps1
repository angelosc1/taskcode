param([string]$Version, [string]$InnoCompiler, [switch]$RunChecks)
$ErrorActionPreference = 'Stop'
$taskcodeRoot = Split-Path -Parent $PSScriptRoot
if (-not $Version) { $Version = (Get-Content -LiteralPath (Join-Path $taskcodeRoot 'VERSION') -Raw).Trim() }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must have three numeric components.' }
if ($RunChecks) {
    $taskcodeStarted = Get-Date
    & (Join-Path $PSScriptRoot 'build-probe.cmd') --test
    if ($LASTEXITCODE -ne 0) { throw 'Development checks failed.' }
    foreach ($taskcodeReportName in @('self-test.txt','tray-test.txt')) {
        $taskcodeReport = Get-Item -LiteralPath (Join-Path $taskcodeRoot ('out/probe/' + $taskcodeReportName))
        if ($taskcodeReport.LastWriteTime -lt $taskcodeStarted.AddSeconds(-2) -or (Get-Content -LiteralPath $taskcodeReport.FullName -Raw) -match '(?m)^FAIL:') { throw "Validation failed: $taskcodeReportName" }
    }
}
$taskcodeFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$taskcodeCompiler = Join-Path $taskcodeFramework 'csc.exe'
$taskcodeOutput = Join-Path $taskcodeRoot ('out/release/' + $Version)
$taskcodeApp = Join-Path $taskcodeOutput 'app'
New-Item -ItemType Directory -Path $taskcodeApp -Force | Out-Null
$taskcodeVersionFile = Join-Path $taskcodeOutput 'Version.cs'
& (Join-Path $PSScriptRoot 'write-version.ps1') -Version $Version -Output $taskcodeVersionFile
$taskcodeSource = Join-Path $taskcodeRoot 'src/Taskcode.Probe'
$taskcodeFiles = @(Get-ChildItem -LiteralPath $taskcodeSource -Filter '*.cs' | Where-Object { $_.Name -notmatch '(Checks|SelfTest)\.cs$' } | ForEach-Object FullName)
$taskcodeReferences = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Management.dll','System.Security.dll','System.Windows.Forms.dll','System.Xml.dll','WPF/UIAutomationClient.dll','WPF/UIAutomationTypes.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll') | ForEach-Object { '/reference:' + (Join-Path $taskcodeFramework $_) }
$taskcodeArguments = @('/nologo','/target:winexe','/main:Taskcode.Probe.Program','/platform:x64','/optimize+','/warnaserror+','/define:TASKCODE_RELEASE',('/out:' + (Join-Path $taskcodeApp 'Taskcode.exe')),('/win32icon:' + (Join-Path $taskcodeRoot 'assets/branding/taskcode.ico')),('/win32manifest:' + (Join-Path $taskcodeSource 'app.manifest')),('/resource:' + (Join-Path $taskcodeRoot 'assets/branding/taskcode.ico') + ',Taskcode.AppIcon.ico'),('/resource:' + (Join-Path $taskcodeRoot 'assets/providers/claude.svg') + ',Taskcode.Claude.svg'))
$taskcodeArguments += Get-ChildItem -LiteralPath (Join-Path $taskcodeRoot 'locales') -Filter '*.json' | ForEach-Object { '/resource:' + $_.FullName + ',Taskcode.Locales.' + $_.Name }
$taskcodeArguments += Get-ChildItem -LiteralPath (Join-Path $taskcodeSource 'Sidecars') -Filter '*.cjs' | ForEach-Object { '/resource:' + $_.FullName + ',Taskcode.' + $_.Name }
& $taskcodeCompiler @taskcodeArguments @taskcodeReferences @taskcodeFiles $taskcodeVersionFile
if ($LASTEXITCODE -ne 0) { throw 'Release compilation failed.' }
$taskcodeBridgeFiles = @('AccountProviders.cs','AccountSignIn.cs','ClaudeBridge.cs','QuotaPeriod.cs','AppInfo.cs') | ForEach-Object { Join-Path $taskcodeSource $_ }
$taskcodeBridgeReferences = @('System.dll','System.Core.dll','System.Web.Extensions.dll') | ForEach-Object { '/reference:' + (Join-Path $taskcodeFramework $_) }
& $taskcodeCompiler /nologo /target:exe /main:Taskcode.Probe.ClaudeBridge /platform:x64 /optimize+ /warnaserror+ ('/out:' + (Join-Path $taskcodeApp 'Taskcode.ClaudeBridge.exe')) @taskcodeBridgeReferences @taskcodeBridgeFiles $taskcodeVersionFile
if ($LASTEXITCODE -ne 0) { throw 'Bridge compilation failed.' }
Copy-Item -LiteralPath (Join-Path $taskcodeSource 'app.config') -Destination (Join-Path $taskcodeApp 'Taskcode.exe.config')
foreach ($taskcodeDocument in @('LICENSE','THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $taskcodeRoot $taskcodeDocument) -Destination $taskcodeApp }
Copy-Item -LiteralPath (Join-Path $taskcodeRoot 'docs/INSTALL.md') -Destination (Join-Path $taskcodeApp 'INSTALL.md')
$taskcodeExpected = @('Taskcode.exe','Taskcode.exe.config','Taskcode.ClaudeBridge.exe','LICENSE','THIRD-PARTY-NOTICES.md','INSTALL.md') | Sort-Object
$taskcodeActual = @(Get-ChildItem -LiteralPath $taskcodeApp -File | ForEach-Object Name | Sort-Object)
if (Compare-Object $taskcodeExpected $taskcodeActual) { throw 'Release staging contains an unexpected file. Settings, credentials and test files must never ship.' }
$taskcodeZip = Join-Path $taskcodeOutput ("Taskcode-$Version-win-x64.zip")
Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $taskcodeApp -File | ForEach-Object FullName) -DestinationPath $taskcodeZip -Force
if (-not $InnoCompiler) {
    $taskcodeCandidates = @((Join-Path $taskcodeRoot 'out/tools/InnoSetup/ISCC.exe'), 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 7\ISCC.exe')
    $InnoCompiler = $taskcodeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoCompiler) { throw 'Inno Setup 6.6+ or 7 is required to build the installer.' }
& $InnoCompiler ("/DAppVersion=$Version") ("/DSourceDir=$taskcodeApp") ("/DOutputDir=$taskcodeOutput") (Join-Path $taskcodeRoot 'installer/Taskcode.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$taskcodeSetup = Join-Path $taskcodeOutput ("Taskcode-Setup-$Version-win-x64.exe")
$taskcodeHashes = @($taskcodeSetup,$taskcodeZip) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + (Split-Path -Leaf $_) }
[System.IO.File]::WriteAllLines((Join-Path $taskcodeOutput 'SHA256SUMS'), $taskcodeHashes, (New-Object System.Text.UTF8Encoding $false))
Write-Output "Release $Version ready: $taskcodeOutput"
