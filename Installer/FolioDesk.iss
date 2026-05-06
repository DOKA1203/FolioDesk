#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "."
#endif

#ifndef OutputDir
  #define OutputDir ".\installer"
#endif

#ifndef RuntimeIdentifier
  #define RuntimeIdentifier "win-x64"
#endif

#define MyAppName "FolioDesk"
#define MyAppPublisher "FolioDesk"
#define MyAppExeName "FolioDesk.exe"
#define MyAppIcon "ICO.ico"

[Setup]
AppId={{40DF4033-CCC5-4A03-AB80-C13323DF6D0E}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableDirPage=yes
DefaultGroupName={#MyAppName}

; 출력 경로 및 파일명 설정
OutputDir={#OutputDir}
OutputBaseFilename=FolioDesk_{#AppVersion}_{#RuntimeIdentifier}

SetupIconFile={#MyAppIcon}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

; --- 아키텍처 최적화 설정 추가 ---
; 이 설정이 있어야 ARM64에서 64비트 모드로 올바르게 작동합니다.
#if RuntimeIdentifier == "win-arm64"
  ArchitecturesAllowed=arm64
  ArchitecturesInstallIn64BitMode=arm64
#elif RuntimeIdentifier == "win-x64"
  ArchitecturesAllowed=x64
  ArchitecturesInstallIn64BitMode=x64
#endif

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Files]
; PublishDir 경로의 모든 파일을 포함
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "FolioDesk 실행"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
