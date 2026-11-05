@echo off
setlocal
set "taskcodeFramework=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "taskcodeRoot=%~dp0.."
if not exist "%taskcodeFramework%\csc.exe" (
  echo The .NET Framework 4.x x64 compiler is required.
  exit /b 1
)
if not exist "%taskcodeRoot%\out\probe" mkdir "%taskcodeRoot%\out\probe"
"%taskcodeFramework%\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /warnaserror+ /out:"%taskcodeRoot%\out\probe\Taskcode.Probe.exe" /win32manifest:"%taskcodeRoot%\src\Taskcode.Probe\app.manifest" /reference:"%taskcodeFramework%\System.dll" /reference:"%taskcodeFramework%\System.Core.dll" /reference:"%taskcodeFramework%\System.Drawing.dll" /reference:"%taskcodeFramework%\System.Windows.Forms.dll" /reference:"%taskcodeFramework%\System.Xml.dll" /reference:"%taskcodeFramework%\WPF\UIAutomationClient.dll" /reference:"%taskcodeFramework%\WPF\UIAutomationTypes.dll" /reference:"%taskcodeFramework%\WPF\WindowsBase.dll" "%taskcodeRoot%\src\Taskcode.Probe\*.cs"
if errorlevel 1 exit /b 1
echo Built: out\probe\Taskcode.Probe.exe
if /i "%~1"=="--test" (
  start "" /wait "%taskcodeRoot%\out\probe\Taskcode.Probe.exe" --self-test
  if errorlevel 1 exit /b 1
  type "%taskcodeRoot%\out\probe\self-test.txt"
)
