@echo off
setlocal
set "taskcodeFramework=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "taskcodeRoot=%~dp0.."
if not exist "%taskcodeFramework%\csc.exe" (
  echo The .NET Framework 4.x x64 compiler is required.
  exit /b 1
)
if not exist "%taskcodeRoot%\out\tools" mkdir "%taskcodeRoot%\out\tools"
"%taskcodeFramework%\csc.exe" /nologo /target:exe /warnaserror+ /out:"%taskcodeRoot%\out\tools\BrandingAssets.exe" /reference:"%taskcodeFramework%\System.Drawing.dll" "%taskcodeRoot%\scripts\BrandingAssets.cs"
if errorlevel 1 exit /b 1
"%taskcodeRoot%\out\tools\BrandingAssets.exe" "%taskcodeRoot%\assets\branding\taskcode-source.png" "%taskcodeRoot%\assets\branding"
if errorlevel 1 exit /b 1
if not exist "%taskcodeRoot%\out\probe" mkdir "%taskcodeRoot%\out\probe"
"%taskcodeFramework%\csc.exe" /nologo /target:winexe /main:Taskcode.Probe.Program /platform:x64 /optimize+ /warnaserror+ /out:"%taskcodeRoot%\out\probe\Taskcode.Probe.exe" /win32icon:"%taskcodeRoot%\assets\branding\taskcode.ico" /resource:"%taskcodeRoot%\assets\branding\taskcode.ico",Taskcode.AppIcon.ico /win32manifest:"%taskcodeRoot%\src\Taskcode.Probe\app.manifest" /reference:"%taskcodeFramework%\System.dll" /reference:"%taskcodeFramework%\System.Core.dll" /reference:"%taskcodeFramework%\System.Web.Extensions.dll" /reference:"%taskcodeFramework%\System.Drawing.dll" /reference:"%taskcodeFramework%\System.Windows.Forms.dll" /reference:"%taskcodeFramework%\System.Xml.dll" /reference:"%taskcodeFramework%\WPF\UIAutomationClient.dll" /reference:"%taskcodeFramework%\WPF\UIAutomationTypes.dll" /reference:"%taskcodeFramework%\WPF\WindowsBase.dll" /reference:"%taskcodeFramework%\WPF\PresentationCore.dll" /resource:"%taskcodeRoot%\assets\providers\claude.svg",Taskcode.Claude.svg "%taskcodeRoot%\src\Taskcode.Probe\*.cs"
if errorlevel 1 exit /b 1
"%taskcodeFramework%\csc.exe" /nologo /target:exe /main:Taskcode.Probe.ClaudeBridge /platform:x64 /optimize+ /warnaserror+ /out:"%taskcodeRoot%\out\probe\Taskcode.ClaudeBridge.exe" /reference:"%taskcodeFramework%\System.dll" /reference:"%taskcodeFramework%\System.Core.dll" /reference:"%taskcodeFramework%\System.Web.Extensions.dll" "%taskcodeRoot%\src\Taskcode.Probe\AccountProviders.cs" "%taskcodeRoot%\src\Taskcode.Probe\AccountSignIn.cs" "%taskcodeRoot%\src\Taskcode.Probe\ClaudeBridge.cs" "%taskcodeRoot%\src\Taskcode.Probe\QuotaPeriod.cs"
if errorlevel 1 exit /b 1
copy /y "%taskcodeRoot%\src\Taskcode.Probe\app.config" "%taskcodeRoot%\out\probe\Taskcode.Probe.exe.config" >nul
if errorlevel 1 exit /b 1
echo Built: out\probe\Taskcode.Probe.exe
if /i "%~1"=="--test" (
  start "" /wait "%taskcodeRoot%\out\probe\Taskcode.Probe.exe" --self-test
  if errorlevel 1 exit /b 1
  type "%taskcodeRoot%\out\probe\self-test.txt"
  start "" /wait "%taskcodeRoot%\out\probe\Taskcode.Probe.exe" --tray-test
  if errorlevel 1 exit /b 1
  type "%taskcodeRoot%\out\probe\tray-test.txt"
)
