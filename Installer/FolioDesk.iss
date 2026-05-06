#define MyAppName "FolioDesk"
#define MyAppVersion GetStringParam("AppVersion", "1.0.0")
#define MyAppPublisher "FolioDesk"
#define MyAppExeName "FolioDesk.exe"

#define MyAppSourceDir GetStringParam("PublishDir", ".")
#define MyOutputDir GetStringParam("OutputDir", ".")
#define MyRuntime GetStringParam("RuntimeIdentifier", "win-x64")

#define MyAppIcon "ICO.ico"

[Setup]
AppId={40DF4033-CCC5-4A03-AB80-C13323DF6D0E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}

OutputDir={#MyOutputDir}
OutputBaseFilename=FolioDesk_{#MyRuntime}

SetupIconFile={#MyAppIcon}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
