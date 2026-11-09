; ShakeShake installer (Inno Setup 7)
;
; Build with:  powershell -ExecutionPolicy Bypass -File .\build.ps1 -Installer
; The app folder must have been published first (build.ps1 does that).
;
; The wizard lets the user pick any install folder (per-user install, no UAC).

#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif

#define AppName "晃晃 ShakeShake"
#define AppShortName "ShakeShake"
#define AppExe "ShakeShake.exe"
#define PublishDir "..\dist\app"
#define AppUrl "https://github.com/qingke04/shakeshake"

[Setup]
AppId={{C28D8BC2-0589-4344-8635-E132B7B9CD6C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=ShakeShake
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} 安装程序
DefaultDirName={localappdata}\Programs\{#AppShortName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Show the "Select Destination Location" page so the path can be customised.
DisableDirPage=no
DirExistsWarning=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename={#AppShortName}-{#AppVersion}-Setup
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic windows11
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage
AllowNoIcons=yes

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autoprograms}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--settings"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
{ The app lives in the tray; ask it to exit before files are replaced or removed. }
procedure KillRunningApp();
var
  ResultCode: Integer;
  TaskKill: String;
begin
  TaskKill := ExpandConstant('{sys}\taskkill.exe');
  if FileExists(TaskKill) then
    Exec(TaskKill, '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
end;

function InitializeSetup(): Boolean;
begin
  KillRunningApp();
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  KillRunningApp();
  Result := True;
end;
