#define AppName "EmilyDesk"
#define AppVersion "2.10.31"

[Setup]
AppId={{5B60B22E-46E5-4EB9-8D7C-707B753BF54D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName=EmilyDesk 2.10.31 Community Release
AppPublisher=EmilyDesk Community Project
; EmilyDesk contains an auto-start LocalSystem weather service. Installing the
; executable payload beneath Program Files keeps that service and every DLL it
; loads outside user-writable locations. Personal state remains under
; LocalAppData and ProgramData, as selected by the applications themselves.
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
WizardImageFile=WizardImage.bmp
WizardSmallImageFile=WizardSmallImage.bmp
SetupLogging=yes
SetupIconFile=EmilyDeskIconV2.ico
UninstallDisplayIcon={app}\EmilyDesk.exe
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes

VersionInfoVersion=2.10.31.0
VersionInfoCompany=EmilyDesk Community Project
VersionInfoDescription=EmilyDesk Community Release Installer
VersionInfoProductName=EmilyDesk
VersionInfoProductVersion=2.10.31
VersionInfoCopyright=Copyright © 2026 EmilyDesk Community Project
[Tasks]
Name: "desktopicon"; Description: "Create an EmilyDesk desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "Stage\EmilyDesk.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Engine.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Service.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Shared.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.WidgetSdk.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Weather.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Clock.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.Calendar.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Widgets.RecycleBin.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\Widgets\*"; DestDir: "{app}\Widgets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\XWidgetReborn.WeatherCore.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.SetupHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDesk.Designer.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\EmilyDeskIconV2.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\ProviderProfiles\*"; DestDir: "{app}\ProviderProfiles"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Wallpapers\*"; DestDir: "{app}\Wallpapers"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
; Ember Glow ships as a ready-to-use installed theme. Fresh installations and
; upgrades therefore receive the corrected clock without a separate import.
; Personal DesignerLayouts and all other settings remain untouched.
Source: "Stage\Themes\ember-glow\*"; DestDir: "{localappdata}\EmilyDesk\Themes\ember-glow"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Stage\Compatibility-Audit.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stage\Compatibility-Audit.cmd"; DestDir: "{app}"; Flags: ignoreversion
; Inno writes this private service dependency set while already elevated.
Source: "Stage\EmilyDesk.Service.exe"; DestDir: "{app}\WeatherService"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.WeatherCore.dll"; DestDir: "{app}\WeatherService"; Flags: ignoreversion
Source: "Stage\XWidgetReborn.Shared.dll"; DestDir: "{app}\WeatherService"; Flags: ignoreversion
Source: "Stage\ProviderProfiles\*"; DestDir: "{app}\WeatherService\ProviderProfiles"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\EmilyDesk.ico"
Type: files; Name: "{group}\EmilyDesk Designer.lnk"
Type: files; Name: "{app}\XWidgetReborn.exe"
Type: files; Name: "{app}\XWidgetReborn.Dashboard.exe"
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
Type: files; Name: "{group}\Run Compatibility Audit.lnk"
Type: files; Name: "{group}\One-time Compatibility Bootstrap.lnk"
Type: files; Name: "{localappdata}\EmilyDesk\Compatibility-Bootstrap.cmd"
Type: files; Name: "{localappdata}\EmilyDesk\Compatibility-Bootstrap.ps1"
Type: files; Name: "{userdesktop}\XWidget Reborn.lnk"
Type: files; Name: "{group}\XWidget Reborn.lnk"
Type: files; Name: "{userstartup}\XWidget Reborn.lnk"

[UninstallDelete]
; Repeat the exact obsolete-shortcut cleanup during uninstall. No application
; directories, user data, or wildcard shortcut paths are removed here.
Type: files; Name: "{userdesktop}\WidgetWorks.lnk"
Type: files; Name: "{commondesktop}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\WidgetWorks.lnk"
Type: files; Name: "{userprograms}\WidgetWorks\EmilyDesk.lnk"
Type: files; Name: "{commonprograms}\WidgetWorks\EmilyDesk.lnk"

[Icons]
Name: "{group}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0; AppUserModelID: "EmilyDesk.App"
Name: "{group}\Run Compatibility Audit"; Filename: "{app}\Compatibility-Audit.cmd"; WorkingDir: "{app}"
Name: "{userdesktop}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0; Tasks: desktopicon; AppUserModelID: "EmilyDesk.App"
Name: "{userstartup}\EmilyDesk"; Filename: "{app}\EmilyDesk.exe"; Parameters: "--tray"; WorkingDir: "{app}"; IconFilename: "{app}\EmilyDeskIconV2.ico"; IconIndex: 0; AppUserModelID: "EmilyDesk.App"

[UninstallRun]
; Personal layouts/settings and ProgramData weather data are retained.
Filename: "{app}\EmilyDesk.SetupHelper.exe"; Parameters: "remove-service-deployment"; Flags: waituntilterminated runhidden; RunOnceId: "RemoveEmilyDeskWeatherService"

[Code]
const
  EventModifyState = $0002;
  SynchronizeAccess = $00100000;
  WaitObject0 = 0;

function OpenEvent(
  DesiredAccess: LongWord;
  InheritHandle: Boolean;
  Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(EventHandle: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function OpenMutex(
  DesiredAccess: LongWord;
  InheritHandle: Boolean;
  Name: String): THandle;
  external 'OpenMutexW@kernel32.dll stdcall';
function WaitForSingleObject(
  Handle: THandle;
  Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';

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

procedure StopEmilyDeskForUpgrade();
var
  ResultCode: Integer;
  DashboardMutex: THandle;
  DashboardExited: Boolean;
begin
  { Give the Dashboard time to hide and dispose its NotifyIcon. }
  SignalDashboardExit();
  DashboardExited := True;
  DashboardMutex := OpenMutex(
    SynchronizeAccess,
    False,
    'Local\XWidgetRebornDashboard');
  if DashboardMutex <> 0 then
  begin
    DashboardExited :=
      WaitForSingleObject(DashboardMutex, 12000) = WaitObject0;
    CloseHandle(DashboardMutex);
  end;

  { Compatibility and unresponsive-process fallbacks only. }
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM "XWidgetReborn.exe" /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM "EmilyDesk.Engine.exe" /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  if not DashboardExited then
    Exec(
      ExpandConstant('{sys}\taskkill.exe'),
      '/IM "EmilyDesk.exe" /T /F',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);
  Sleep(700);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopEmilyDeskForUpgrade();
  { Never execute the previous helper from the legacy user-writable install.
    The freshly installed protected helper replaces the service after copy. }
  Result := '';
end;

function RequiredPayloadExists(): Boolean;
begin
  Result :=
    FileExists(ExpandConstant('{app}\EmilyDesk.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Engine.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Service.exe')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.Designer.exe')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Shared.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.WidgetSdk.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Weather.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Clock.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.Calendar.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.Widgets.RecycleBin.dll')) and
    FileExists(ExpandConstant('{app}\XWidgetReborn.WeatherCore.dll')) and
    FileExists(ExpandConstant('{app}\EmilyDesk.SetupHelper.exe')) and
    FileExists(ExpandConstant('{app}\Compatibility-Audit.cmd')) and
    FileExists(ExpandConstant('{localappdata}\EmilyDesk\Themes\ember-glow\theme.json')) and
    FileExists(ExpandConstant('{localappdata}\EmilyDesk\Themes\ember-glow\layouts\ember-glow-clock.layout.json')) and
    FileExists(ExpandConstant('{app}\WeatherService\EmilyDesk.Service.exe')) and
    FileExists(ExpandConstant('{app}\WeatherService\XWidgetReborn.WeatherCore.dll')) and
    FileExists(ExpandConstant('{app}\WeatherService\XWidgetReborn.Shared.dll'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    { Setup is elevated and this helper has already been written beneath
      Program Files. A nonzero verification result aborts installation. }
    WizardForm.StatusLabel.Caption :=
      'Installing and verifying the EmilyDesk weather service...';
    if (not Exec(
        ExpandConstant('{app}\EmilyDesk.SetupHelper.exe'),
        'deploy-service-and-verify "' +
          ExpandConstant('{app}\WeatherService') + '" "' +
          ExpandConstant('{localappdata}\EmilyDesk') + '"',
        ExpandConstant('{app}'),
        SW_HIDE,
        ewWaitUntilTerminated,
        ResultCode)) or (ResultCode <> 0) then
    begin
      Exec(
        ExpandConstant('{app}\EmilyDesk.SetupHelper.exe'),
        'remove-service-deployment',
        ExpandConstant('{app}'),
        SW_HIDE,
        ewWaitUntilTerminated,
        ResultCode);
      RaiseException(
        'EmilyDesk could not install or verify its weather service.' +
        Chr(13) + Chr(10) + Chr(13) + Chr(10) +
        'The installer has stopped instead of leaving a partial setup. ' +
        'See the setup log and %ProgramData%\EmilyDesk\WeatherService\' +
        'setup-diagnostics.log for details.'
      );
    end;

    { Real-time scanners can accept extraction and quarantine an unsigned PE
      several seconds later. Do not report success or leave broken shortcuts
      until the installed EmilyDesk application has survived that scan window. }
    WizardForm.StatusLabel.Caption :=
      'Verifying installed application files...';
    Sleep(15000);

    if not RequiredPayloadExists() then
      RaiseException(
        'EmilyDesk.exe was installed but is no longer present. ' +
        'Endpoint security may have quarantined the unsigned EmilyDesk application. ' +
        'The installation cannot complete with a broken application shortcut.' +
        Chr(13) + Chr(10) + Chr(13) + Chr(10) +
        'Review quarantine history for EmilyDesk.exe. A production release ' +
        'must be trusted by the security product or Authenticode-signed.'
      );
  end;
end;
