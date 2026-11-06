#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif

[Setup]
AppId=angelosc1.Taskcode
AppName=Taskcode
AppVersion={#AppVersion}
AppPublisher=Taskcode contributors
AppPublisherURL=https://github.com/angelosc1/taskcode
AppSupportURL=https://github.com/angelosc1/taskcode/issues
AppUpdatesURL=https://github.com/angelosc1/taskcode/releases
DefaultDirName={localappdata}\Programs\Taskcode
DefaultGroupName=Taskcode
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
WizardStyle=modern dark
SetupIconFile=..\assets\branding\taskcode.ico
UninstallDisplayIcon={app}\Taskcode.exe
OutputDir={#OutputDir}
OutputBaseFilename=Taskcode-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
CloseApplicationsFilter=Taskcode.exe,Taskcode.ClaudeBridge.exe
RestartApplications=no
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=Taskcode
VersionInfoDescription=Taskcode Setup
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\Taskcode.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Taskcode.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Taskcode.ClaudeBridge.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Taskcode.ClaudeBridge.exe"; DestDir: "{localappdata}\Taskcode"; Flags: ignoreversion
Source: "{#SourceDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\INSTALL.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Taskcode"; Filename: "{app}\Taskcode.exe"
Name: "{autodesktop}\Taskcode"; Filename: "{app}\Taskcode.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Taskcode.exe"; Parameters: "--finish-install"; Flags: runhidden waituntilterminated
Filename: "{app}\Taskcode.exe"; Parameters: "{code:LaunchParameters}"; Flags: nowait runasoriginaluser; Check: ShouldLaunch

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\Taskcode.exe')) then begin
    if not Exec(ExpandConstant('{app}\Taskcode.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
      Result := 'Close Taskcode from its tray menu, then try again.';
      Exit;
    end;
    Sleep(1000);
  end;
end;

function InitializeSetup: Boolean;
var
  FrameworkRelease: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', FrameworkRelease) and (FrameworkRelease >= 528040);
  if not Result then SuppressibleMsgBox('Taskcode requires .NET Framework 4.8 or later, included with Windows 11.', mbError, MB_OK, IDOK);
end;

function ShouldLaunch: Boolean;
begin
  Result := ExpandConstant('{param:NOLAUNCH|0}') <> '1';
end;

function LaunchParameters(Param: String): String;
begin
  if ExpandConstant('{param:UPDATE|0}') = '1' then Result := '--tray' else Result := '';
end;

function InitializeUninstall: Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\Taskcode.exe')) then begin
    Result := Exec(ExpandConstant('{app}\Taskcode.exe'), '--prepare-uninstall', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
    if Result then Result := ExitCode = 0;
    if not Result then SuppressibleMsgBox('Taskcode could not restore its Claude Code integration. Disconnect Claude in Taskcode settings, then uninstall again.', mbError, MB_OK, IDOK);
    Sleep(1000);
  end;
end;
