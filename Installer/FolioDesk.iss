#define MyAppName "FolioDesk"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "FolioDesk"
#define MyAppExeName "FolioDesk.exe"
#define MyAppSourceDir "D:\Projects\DOKA\FolioDesk\FolioDesk\bin\Release\net10.0-windows\publish\win-x64"
#define MyAppIcon "D:\Projects\DOKA\FolioDesk\Installer\ICO.ico"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}
OutputDir=D:\Projects\DOKA\FolioDesk\Installer
OutputBaseFilename=FolioDesk_Setup
SetupIconFile={#MyAppIcon}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{#MyAppIcon}"
Name: "{group}\{#MyAppName} 제거"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "FolioDesk 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
