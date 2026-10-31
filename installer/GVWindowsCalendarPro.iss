; Instalador de GV Windows Calendar Pro (Inno Setup 6/7).
; Se compila con build.sh, que pasa /DAppVersion y /DSourceDir.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\src\GVCalendar\bin\Release\net48"
#endif

#define AppName "GV Windows Calendar Pro"
#define AppExe "GVWindowsCalendarPro.exe"
#define RunValue "GVWindowsCalendarPro"

[Setup]
AppId={{6C1B7E52-3F4A-4E0B-9C2D-8A7F1E5D3B90}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=GV
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=GVWindowsCalendarPro-Setup-{#AppVersion}
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
CloseApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.AutoStart=Iniciar automáticamente con Windows
english.AutoStart=Start automatically with Windows
spanish.OptionsGroup=Opciones:
english.OptionsGroup=Options:
spanish.NeedNet48=Esta aplicación necesita .NET Framework 4.8, que se incluye con Windows 10 (versión 1903 o posterior) y Windows 11.%n%nInstálelo desde https://dotnet.microsoft.com/download/dotnet-framework/net48 y vuelva a ejecutar el instalador.
english.NeedNet48=This application requires .NET Framework 4.8, included with Windows 10 (version 1903 or later) and Windows 11.%n%nInstall it from https://dotnet.microsoft.com/download/dotnet-framework/net48 and run setup again.

[Tasks]
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:OptionsGroup}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "/open"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Parameters: "/open"; Tasks: desktopicon

[Registry]
; Mismo valor que usa la opción "Iniciar con Windows" del menú de la aplicación.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#RunValue}"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\GVWindowsCalendarPro"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "/open"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
            and (Release >= 528040);
end;

procedure StopRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup: Boolean;
begin
  Result := IsNet48Installed;
  if not Result then
    SuppressibleMsgBox(CustomMessage('NeedNet48'), mbCriticalError, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp;
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Si se desmarca la tarea al actualizar, quitar el arranque automático anterior.
  if (CurStep = ssPostInstall) and not WizardIsTaskSelected('autostart') then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValue}');
end;

function InitializeUninstall: Boolean;
begin
  StopRunningApp;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // El usuario puede haber activado el inicio automático desde el menú de la aplicación.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValue}');
end;
