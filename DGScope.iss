; DGScope Installer Script for Inno Setup
; Download Inno Setup from: https://jrsoftware.org/isinfo.php

#define MyAppName "DGScope"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DGScope Team"
#define MyAppURL "https://github.com/yourusername/scope"
#define MyAppExeName "scope.exe"
#define MyAppDescription "Air Traffic Control Scope Display System"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{12345678-90AB-CDEF-1234-567890ABCDEF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Uncomment the following line if you have a license file
; LicenseFile=LICENSE
; Uncomment if you have a readme
; InfoBeforeFile=README.md
OutputDir=installer-output
OutputBaseFilename=DGScope_Setup_{#MyAppVersion}
; Uncomment the next line and provide the path to your icon file
; SetupIconFile=scope\Resources\AppIcon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Request admin privileges to install in Program Files
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main executable
Source: "build\Release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; All DLLs and configs
Source: "build\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "build\Release\*.config"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Include any additional resource folders if needed
; Source: "build\Release\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Check if .NET Framework 4.7.2 or later is installed
function IsDotNetInstalled(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
  begin
    // .NET Framework 4.7.2 = 461808
    if Release >= 461808 then
      Result := True;
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNetInstalled() then
  begin
    MsgBox('.NET Framework 4.7.2 or later is required to run DGScope.'#13#10#13#10
           'Please download and install it from:'#13#10
           'https://dotnet.microsoft.com/download/dotnet-framework/net472'#13#10#13#10
           'Setup will now exit.', mbError, MB_OK);
    Result := False;
  end;
end;
