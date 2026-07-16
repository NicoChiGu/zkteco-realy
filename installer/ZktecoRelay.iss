#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif
#ifndef Architecture
  #define Architecture "x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

#define AppName "ZKTeco Relay"
#define Publisher "NicoChiGu"
#define ManagerExe "ZktecoRelay.Manager.exe"

[Setup]
AppId={{9D98A71E-B56D-4F0F-8E97-11C40CF9D4A2}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={localappdata}\Programs\ZKTeco Relay
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=zkteco-relay-win-{#Architecture}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallDisplayIcon={app}\manager\{#ManagerExe}
#if Architecture == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#else
ArchitecturesAllowed=x86compatible
#endif
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#Publisher}
VersionInfoDescription=ZKTeco Relay installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked
Name: "autostart"; Description: "登录 Windows 后自动启动管理器"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#SourceDir}\api\*"; DestDir: "{app}\api"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\manager\*"; DestDir: "{app}\manager"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\.env.example"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"
Name: "{group}\卸载 ZKTeco Relay"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: desktopicon
Name: "{userstartup}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: autostart

[Run]
Filename: "{app}\manager\{#ManagerExe}"; Description: "启动 ZKTeco Relay 管理器"; Flags: nowait postinstall skipifsilent

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
end;
