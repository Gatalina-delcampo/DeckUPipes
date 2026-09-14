; DeckUPipes installer script (Inno Setup 6).
; Built by CI on tags (v*). Publish output is expected in dist/publish.

#define MyAppName "DeckUPipes"
#ifndef MyAppVersion
#define MyAppVersion "0.1.2"
#endif
#define MyAppPublisher "gatacampestre"
#define MyAppExeName "DeckUPipes.App.exe"
#define MyAppId "{{4B2E1C6A-9D3F-4E7B-8A51-2C6D9E0F1A3B}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\DeckUPipes
DefaultGroupName=DeckUPipes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts
OutputBaseFilename=DeckUPipes-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\DeckUPipes.App\Assets\deckupipes.ico
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
Name: "{group}\DeckUPipes"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall DeckUPipes"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DeckUPipes"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,DeckUPipes}"; Flags: nowait postinstall skipifsilent


