; Script generated for DoubleTake
; Builds a self-contained, standalone Windows Setup Wizard (.exe)
; No MSIX, developer mode, or certificates required.

#define MyAppName "DoubleTake"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Saidi"
#define MyAppURL "https://github.com/SaidimM/DoubleTake"
#define MyAppExeName "DoubleTake.exe"
#define MySourceDir "publish-unpackaged"

[Setup]
; App Identity
AppId={{D32E5FF8-4BE6-4441-ADD7-CF25C862BC2A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation Paths
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=ReleaseOutput
OutputBaseFilename=DoubleTake-Setup-v{#MyAppVersion}-win-x64
SetupIconFile=src\DoubleTake\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Modern UI Style & Compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Close running DoubleTake before upgrade
CloseApplications=yes
CloseApplicationsFilter=DoubleTake.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start DoubleTake automatically when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DoubleTake"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
