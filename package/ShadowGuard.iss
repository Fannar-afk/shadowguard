#define MyAppName "ShadowGuard"
#define MyAppVersion GetEnv("SHADOWGUARD_VERSION") == "" ? "1.0.0" : GetEnv("SHADOWGUARD_VERSION")
#define MyAppPublisher "Fannar-afk"
#define MyAppExeName "ShadowGuard.exe"
#define SourceDir GetEnv("SHADOWGUARD_PUBLISH_DIR") == "" ? "..\\ShadowGuard\\bin\\Release\\net6.0-windows\\win-x64\\publish" : GetEnv("SHADOWGUARD_PUBLISH_DIR")
#define OutputDir GetEnv("SHADOWGUARD_OUTPUT_DIR") == "" ? "." : GetEnv("SHADOWGUARD_OUTPUT_DIR")

[Setup]
AppId={{7F2F3B8C-88D3-4A38-9B6C-2D385B2A5E1D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=ShadowGuard-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional tasks:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
