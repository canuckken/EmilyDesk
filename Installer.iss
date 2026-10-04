#error This legacy hosts-file installer is disabled. Build Installer-UserMode.iss instead.
#define AppName "EmilyDesk"
#define AppVersion "2.10.27"
#define ServiceName "XWidgetWeatherBridge"

[Setup]
; Keep the previous AppId so Community Toolkit/Weather Bridge installs upgrade cleanly.
AppId={{30F03C20-E571-41B8-BE20-EFD5E6CB3B72}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName=EmilyDesk 2.10.27
AppPublisher=EmilyDesk Community Project
VersionInfoCompany=EmilyDesk Community Project
VersionInfoDescription=EmilyDesk Installer
VersionInfoProductName=EmilyDesk
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\EmilyDesk
UsePreviousAppDir=no
DisableDirPage=yes
DefaultGroupName={#AppName}
UsePreviousTasks=no
UsePreviousGroup=no
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=EmilyDeskInstaller
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
SetupIconFile=EmilyDeskIconV2.ico
UninstallDisplayIcon={app}\EmilyDesk.exe
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes
DisableFinishedPage=no
Uninstallable=yes

[Tasks]
Name: "desktopicon"; Description: "Create an EmilyDesk desktop shortcut"; GroupDescription: "Shortcuts:"

[Dirs]
Name: "{commonappdata}\EmilyDesk\DesignerLayouts"; Permissions: users-modify

[Files]
Source: "Stage\EmilyDesk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Engine.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Service.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.SetupHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Designer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Shared.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.WidgetSdk.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Weather.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Clock.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Calendar.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.RecycleBin.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\Widgets\*"; DestDir: "{app}\Widgets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Themes\ember-glow\*"; DestDir: "{app}\Themes\ember-glow"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Themes\copper-glow\*"; DestDir: "{app}\Themes\copper-glow"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\XWidgetReborn.WeatherCore.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\ProviderProfiles\*"; DestDir: "{app}\ProviderProfiles"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\EmilyDeskIconV2.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\Compatibility-Audit.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\Compatibility-Audit.cmd"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Remove shortcuts left by all previous development names.
Type: files; Name: "{app}\EmilyDesk.ico"
Type: files; Name: "{group}\EmilyDesk Designer.lnk"
Type: files; Name: "{autodesktop}\XWidget Community Toolkit.lnk"
Type: files; Name: "{autodesktop}\XWidget Weather Bridge Status.lnk"
; Remove only known obsolete WidgetWorks shortcut files. Do not remove the
; WidgetWorks Start Menu folder because it may contain unrelated user items.
Type: files; Name: "{userdesktop}\WidgetWorks.lnk"
Type: files; Name: "{commondesktop}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\EmilyDesk.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\EmilyDesk.lnk"
Type: filesandordirs; Name: "{commonprograms}\XWidget Community Toolkit"
Type: filesandordirs; Name: "{commonprograms}\XWidget Weather Bridge"
[Icons]
; This shortcut always targets an executable installed by this installer.
Name: "{group}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0
Name: "{group}\Run Compatibility Audit"; Filename: "{app}\Compatibility-Audit.cmd"; WorkingDir: "{app}"
Name: "{group}\Open Diagnostics"; Filename: "{sys}\explorer.exe"; Parameters: """{commonappdata}\XWidgetWeatherBridge"""; WorkingDir: "{commonappdata}\XWidgetWeatherBridge"
Name: "{userdesktop}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0; Tasks: desktopicon

Name: "{userstartup}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; Parameters: "--tray"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0

[Run]
; Remove the PowerShell prototype and stale scheduled task.
Filename: "{cmd}"; Parameters: "/c schtasks /End /TN ""XWidget Weather Bridge"" >nul 2>&1"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/c schtasks /Delete /TN ""XWidget Weather Bridge"" /F >nul 2>&1"; Flags: runhidden waituntilterminated

; Replace the prior service while preserving its stable service name.

Filename: "{cmd}"; Parameters: "/c netsh http delete urlacl url=http://api.accuweather.com:80/ >nul 2>&1"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/c netsh http add urlacl url=http://api.accuweather.com:80/ user=""NT AUTHORITY\SYSTEM"""; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/c if not exist ""{commonappdata}\XWidgetWeatherBridge"" mkdir ""{commonappdata}\XWidgetWeatherBridge"""; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/c if exist ""C:\XWidgetWeatherFix\locations.json"" if not exist ""{commonappdata}\XWidgetWeatherBridge\locations.json"" copy /Y ""C:\XWidgetWeatherFix\locations.json"" ""{commonappdata}\XWidgetWeatherBridge\locations.json"" >nul"; Flags: runhidden waituntilterminated


; Service installation is handled by the application itself, avoiding fragile nested quoting.
Filename: "{app}\EmilyDesk.SetupHelper.exe"; Parameters: "remove-service"; Flags: runhidden waituntilterminated
Filename: "{app}\EmilyDesk.SetupHelper.exe"; Parameters: "install-service ""{app}\EmilyDesk.Service.exe"""; Flags: runhidden waituntilterminated
Filename: "{app}\EmilyDesk.SetupHelper.exe"; Parameters: "start-and-verify"; StatusMsg: "Starting and verifying the EmilyDesk weather service..."; Flags: runhidden waituntilterminated

; Default finish action is the installed dashboard. XWidget launch is optional.

[UninstallRun]
Filename: "{app}\EmilyDesk.SetupHelper.exe"; Parameters: "remove-service"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/c netsh http delete urlacl url=http://api.accuweather.com:80/ >nul 2>&1"; Flags: runhidden waituntilterminated

[Code]

const
  EventModifyState = $0002;

function OpenEvent(
  DesiredAccess: LongWord;
  InheritHandle: Boolean;
  Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(EventHandle: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function RunHiddenAndWait(
  const FileName, Parameters: String;
  var ResultCode: Integer): Boolean;
begin
  Result := Exec(
    FileName,
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
end;

procedure SignalDashboardExit();
var
  ExitEvent: THandle;
begin
  ExitEvent := OpenEvent(
    EventModifyState,
    False,
    'Local\XWidgetRebornExitDashboard');
  if ExitEvent <> 0 then
  begin
    SetEvent(ExitEvent);
    CloseHandle(ExitEvent);
  end;
end;

procedure StopRebornForUpgrade();
var
  ResultCode: Integer;
begin
  { Give the Dashboard time to hide and dispose its NotifyIcon. }
  SignalDashboardExit();
  Sleep(2000);

  { Compatibility and unresponsive-process fallbacks only. }
  RunHiddenAndWait(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM "EmilyDesk.exe" /T /F',
    ResultCode);
  { Also close the pre-rebrand executable during an in-place upgrade. }
  RunHiddenAndWait(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM "XWidgetReborn.exe" /T /F',
    ResultCode);

  { Stop the weather service before replacing its executable and DLLs. }
  RunHiddenAndWait(
    ExpandConstant('{sys}\sc.exe'),
    'stop "XWidgetWeatherBridge"',
    ResultCode);

  { Allow Service Control Manager a brief moment to release the executable. }
  Sleep(1500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRebornForUpgrade();
  Result := '';
end;


function RequiredPayloadExists(): Boolean;
begin
  Result :=
    FileExists(ExpandConstant('{app}\EmilyDesk.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Engine.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Service.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.SetupHelper.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Designer.exe')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Shared.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.WidgetSdk.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Weather.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Clock.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Calendar.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.RecycleBin.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.WeatherCore.dll'));
end;

procedure VerifyInstalledPayload();
begin
  if not RequiredPayloadExists() then
  begin
    RaiseException(
      'One or more EmilyDesk 3.0 files are missing after extraction.' +
      Chr(13) + Chr(10) + Chr(13) + Chr(10) +
      'Security software may have quarantined an unsigned component. Review ' +
      'Malwarebytes Detection History and Windows Security Protection history. ' +
      'Do not disable protection globally. EmilyDesk Dashboard, the service, and setup ' +
      'helper are now separate files so the exact quarantined component can be identified.'
    );
  end;
end;

procedure InitializeWizard();
var
  InfoPage: TOutputMsgMemoWizardPage;
begin
  InfoPage := CreateOutputMsgMemoPage(
    wpWelcome,
    'How EmilyDesk works',
    'Local compatibility changes',
    'Please review these installation details before continuing:',
    'EmilyDesk restores legacy weather widgets by running one local Windows service. ' +
    'The installer adds a localhost hosts-file entry for api.accuweather.com and reserves ' +
    'a local HTTP listener. It does not redirect general web traffic.' + #13#10 + #13#10 +
    'Because service installation and hosts-file changes are also techniques used by some ' +
    'malware, security software may inspect or warn about this unsigned community build. ' +
    'Do not disable security software permanently. Review the source and build log, and ' +
    'submit the exact built installer to your security vendor when a false positive occurs.'
  );
end;

const
  HostsMarker = '# EmilyDesk Weather Compatibility';
  HostsLine = '127.0.0.1 api.accuweather.com';

function GetXWidgetExe(Param: String): String;
var
  Candidate: String;
begin
  Result := '';

  Candidate := ExpandConstant('{pf32}\XWidget\xwidget.exe');
  if FileExists(Candidate) then begin Result := Candidate; exit; end;

  Candidate := ExpandConstant('{pf}\XWidget\xwidget.exe');
  if FileExists(Candidate) then begin Result := Candidate; exit; end;

  Candidate := ExpandConstant('{localappdata}\XWidget\xwidget.exe');
  if FileExists(Candidate) then begin Result := Candidate; exit; end;

  Candidate := ExpandConstant('{userappdata}\XWidget\xwidget.exe');
  if FileExists(Candidate) then begin Result := Candidate; exit; end;

  if RegQueryStringValue(HKLM32,
       'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\xwidget.exe',
       '', Candidate) and FileExists(Candidate) then
  begin Result := Candidate; exit; end;

  if RegQueryStringValue(HKCU,
       'SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\xwidget.exe',
       '', Candidate) and FileExists(Candidate) then
  begin Result := Candidate; exit; end;
end;

function GetXWidgetDir(Param: String): String;
begin
  Result := ExtractFileDir(GetXWidgetExe(''));
end;

function XWidgetExists(): Boolean;
begin
  Result := GetXWidgetExe('') <> '';
end;

function HostsPath(): String;
begin
  Result := ExpandConstant('{sys}\drivers\etc\hosts');
end;

procedure RemoveBridgeHostsLines();
var
  Existing, Output: TArrayOfString;
  I, Count: Integer;
  S: String;
begin
  if not LoadStringsFromFile(HostsPath(), Existing) then exit;

  SetArrayLength(Output, GetArrayLength(Existing));
  Count := 0;

  for I := 0 to GetArrayLength(Existing) - 1 do
  begin
    S := Trim(Existing[I]);
    if (CompareText(S, '# XWidget Weather Bridge') <> 0) and
       (CompareText(S, HostsMarker) <> 0) and
       (Pos('api.accuweather.com', Lowercase(S)) = 0) then
    begin
      Output[Count] := Existing[I];
      Count := Count + 1;
    end;
  end;

  SetArrayLength(Output, Count);
  SaveStringsToFile(HostsPath(), Output, False);
end;

procedure AddBridgeHostsLines();
var
  Lines: TArrayOfString;
  Count, ResultCode: Integer;
begin
  RemoveBridgeHostsLines();

  if not LoadStringsFromFile(HostsPath(), Lines) then
    SetArrayLength(Lines, 0);

  Count := GetArrayLength(Lines);
  SetArrayLength(Lines, Count + 2);
  Lines[Count] := HostsMarker;
  Lines[Count + 1] := HostsLine;
  SaveStringsToFile(HostsPath(), Lines, False);

  Exec(ExpandConstant('{sys}\ipconfig.exe'), '/flushdns', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    AddBridgeHostsLines();

  if CurStep = ssPostInstall then
    VerifyInstalledPayload();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveBridgeHostsLines();
    Exec(ExpandConstant('{sys}\ipconfig.exe'), '/flushdns', '', SW_HIDE,
         ewWaitUntilTerminated, ResultCode);
  end;
end;


[UninstallDelete]
Type: files; Name: "{userdesktop}\EmilyDesk.lnk"
Type: files; Name: "{commondesktop}\EmilyDesk.lnk"
; Repeat exact obsolete WidgetWorks shortcut cleanup during uninstall.
Type: files; Name: "{userdesktop}\WidgetWorks.lnk"
Type: files; Name: "{commondesktop}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\EmilyDesk.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\EmilyDesk.lnk"
