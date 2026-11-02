#ifndef AppVersion
  #define AppVersion "0.3.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

[Setup]
AppId={{B51D02E8-2292-464D-B2CD-1486E5315750}
AppName=Shortcut Manager
AppVersion={#AppVersion}
AppPublisher=Shortcut Manager
AppPublisherURL=https://github.com/Soki0909/windows-shortcut-manager
AppSupportURL=https://github.com/Soki0909/windows-shortcut-manager/issues
AppUpdatesURL=https://github.com/Soki0909/windows-shortcut-manager/releases
LicenseFile=..\LICENSE
DefaultDirName={localappdata}\Programs\Shortcut Manager
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableProgramGroupPage=yes
DisableDirPage=yes
UninstallDisplayIcon={app}\ShortcutManager.exe
SetupIconFile=..\src\ShortcutManager.App\Assets\shortcut_manager_logo.ico
OutputDir=..\artifacts
OutputBaseFilename=ShortcutManager-Setup-win-x64-v{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"; InfoBeforeFile: "info-ja.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "info-en.txt"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Shortcut Manager"; Filename: "{app}\ShortcutManager.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Shortcut Manager (監査のみ)"; Filename: "{app}\ShortcutManager.exe"; Parameters: "--audit-only"; WorkingDir: "{app}"; Comment: "今回の自動修復を保留し、保存済み設定とリンクの差分を確認します。"
Name: "{userdesktop}\Shortcut Manager"; Filename: "{app}\ShortcutManager.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ShortcutManager.exe"; Description: "Shortcut Managerを起動する"; Flags: nowait postinstall skipifsilent
