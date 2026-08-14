; EarClarinet installer script (Inno Setup 6).
; Built by CI on tags (v*). Publish output is expected in dist/publish.

#define MyAppName "EarClarinet"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "gatacampestre"
#define MyAppExeName "EarClarinet.App.exe"
#define MyAppId "4B2E1C6A-9D3F-4E7B-8A51-2C6D9E0F1A3B"

[Setup]
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\EarClarinet
DefaultGroupName=EarClarinet
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts
OutputBaseFilename=EarClarinet-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\EarClarinet.App\Assets\logowo.ico
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\EarClarinet"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall EarClarinet"; Filename: "{uninstallexe}"
Name: "{autodesktop}\EarClarinet"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,EarClarinet}"; Flags: nowait postinstall skipifsilent
