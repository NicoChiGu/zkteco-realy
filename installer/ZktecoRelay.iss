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

#if DirExists(SourceDir + "\dll")
  #define DllSourceDir SourceDir + "\dll"
#elif DirExists(SourceDir + "\..\dll")
  #define DllSourceDir SourceDir + "\..\dll"
#else
  #define DllSourceDir "..\dll"
#endif

[Setup]
AppId={{9D98A71E-B56D-4F0F-8E97-11C40CF9D4A2}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\ZKTeco Relay
DefaultGroupName={#AppName}
OutputDir={#OutputDir}
OutputBaseFilename=zkteco-relay-win-{#Architecture}-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
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
Source: "{#DllSourceDir}\*.dll"; DestDir: "{app}\dll"; Flags: ignoreversion
Source: "{#DllSourceDir}\*.dll"; DestDir: "{sys}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\.env.example"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"
Name: "{group}\卸载 ZKTeco Relay"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: desktopicon
Name: "{userstartup}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: autostart

[Run]
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{sys}\zkemkeeper.dll"""; WorkingDir: "{sys}"; StatusMsg: "正在注册 ZKTeco COM SDK (zkemkeeper.dll)..."; Flags: runhidden
Filename: "{app}\manager\{#ManagerExe}"; Description: "启动 ZKTeco Relay 管理器"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Res: Integer;
  SysDir: String;
  Regsvr32Path: String;
  ResultCode: Integer;
  DllList: array of String;
  I: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    SysDir := ExpandConstant('{sys}');
    if FileExists(SysDir + '\zkemkeeper.dll') or FileExists(ExpandConstant('{app}\dll\zkemkeeper.dll')) then
    begin
      Res := MsgBox(
        '是否注销并删除系统目录 (' + SysDir + ') 中的 ZKTeco COM SDK DLL 组件？' + #13#10 + #13#10 +
        '注销并删除后，系统中的 zkemkeeper COM 组件将失效。' + #13#10 +
        '若此机器上有其他软件正在使用该 DLL，建议选择“否”保留注册。' + #13#10 + #13#10 +
        '是否仍要注销并删除组件？',
        mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2
      );

      if Res = IDYES then
      begin
        Regsvr32Path := SysDir + '\regsvr32.exe';
        if FileExists(SysDir + '\zkemkeeper.dll') then
        begin
          Exec(Regsvr32Path, '/u /s "' + SysDir + '\zkemkeeper.dll"', SysDir, SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;

        DllList := [
          'zkemkeeper.dll', 'zkemsdk.dll', 'zkemsdkutils.dll',
          'commpro.dll', 'comms.dll', 'tcpcomm.dll', 'usbcomm.dll', 'usbstd.dll',
          'rscagent.dll', 'rscomm.dll', 'plcommpro.dll', 'plcomms.dll',
          'plrscagent.dll', 'plrscomm.dll', 'pltcpcomm.dll', 'plusbcomm.dll',
          'plcommutils.dll', 'ZKCommuCryptoClient.dll', 'ZKEMCrypto.dll',
          'libareacode.dll', 'IOTCAPIs.dll', 'RDTAPIs.dll', 'p4p.dll', 'p4pcomm.dll'
        ];

        for I := 0 to GetArrayLength(DllList) - 1 do
        begin
          DeleteFile(SysDir + '\' + DllList[I]);
        end;
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
end;
