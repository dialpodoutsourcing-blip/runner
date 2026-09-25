#define AppName "Ghost User Runner"
#define AppVersion "0.1.0"
#define SourceDir "..\artifacts\portable\GhostUserRunner"

[Setup]
AppId={{62E3416D-CDB6-47AB-B2DE-39E404CA341B}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\GhostUserRunner
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=GhostUserRunner-Setup
Compression=lzma2
SolidCompression=yes
Uninstallable=yes
WizardStyle=modern
SetupIconFile=..\assets\GhostUserRunner.ico
UninstallDisplayIcon={app}\GhostUserRunner.App.exe
ArchitecturesAllowed=x64compatible

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\GhostUserRunner.App.exe"
Name: "{userstartup}\{#AppName} Agent"; Filename: "{app}\GhostUserRunner.App.exe"; Parameters: "--background"; WorkingDir: "{app}"

[Run]
Filename: "{app}\GhostUserRunner.App.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
