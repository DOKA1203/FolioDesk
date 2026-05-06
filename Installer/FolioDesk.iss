; GitHub Actions에서 /D 옵션으로 주입받는 변수들 정의
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef RuntimeIdentifier
  #define RuntimeIdentifier "win-x64"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\installer"
#endif

#define MyAppName "FolioDesk"
#define MyAppPublisher "FolioDesk"
#define MyAppExeName "FolioDesk.exe"

[Setup]
AppId={{40DF4033-CCC5-4A03-AB80-C13323DF6D0E}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
; 사용자 권한으로 설치 (관리자 권한 불필요)
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#OutputDir}
OutputBaseFilename={#MyAppName}-{#AppVersion}-{#RuntimeIdentifier}-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

; --- 아키텍처 최적화 설정 ---
#if RuntimeIdentifier == "win-arm64"
  ArchitecturesAllowed=arm64
  ArchitecturesInstallIn64BitMode=arm64
#else
  ArchitecturesAllowed=x64
  ArchitecturesInstallIn64BitMode=x64
#endif

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; PublishDir의 모든 파일(EXE, DLL, Config 등)을 포함
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; 설치 완료 후 프로그램 실행 옵션
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"