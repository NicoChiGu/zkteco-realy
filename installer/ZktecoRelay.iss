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
Source: "{#DllSourceDir}\*"; DestDir: "{app}\dll"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\.env.example"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"
Name: "{group}\卸载 ZKTeco Relay"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: desktopicon
Name: "{userstartup}\ZKTeco Relay 管理器"; Filename: "{app}\manager\{#ManagerExe}"; WorkingDir: "{app}\manager"; Tasks: autostart

[Run]
Filename: "{code:GetRegsvr32X64}"; Parameters: "/s ""{app}\dll\x64\zkemkeeper.dll"""; WorkingDir: "{app}\dll\x64"; Check: HasDllX64; StatusMsg: "正在注册 ZKTeco x64 COM SDK (zkemkeeper.dll)..."; Flags: runhidden
Filename: "{code:GetRegsvr32X86}"; Parameters: "/s ""{app}\dll\x86\zkemkeeper.dll"""; WorkingDir: "{app}\dll\x86"; Check: HasDllX86; StatusMsg: "正在注册 ZKTeco x86 COM SDK (zkemkeeper.dll)..."; Flags: runhidden
Filename: "{sys}\regsvr32.exe"; Parameters: "/s ""{app}\dll\zkemkeeper.dll"""; WorkingDir: "{app}\dll"; Check: HasDllRoot; StatusMsg: "正在注册 ZKTeco COM SDK (zkemkeeper.dll)..."; Flags: runhidden
Filename: "{app}\manager\{#ManagerExe}"; Description: "启动 ZKTeco Relay 管理器"; Flags: nowait postinstall skipifsilent

[Code]
function HasDllX64: Boolean;
begin
  Result := IsWin64 and FileExists(ExpandConstant('{app}\dll\x64\zkemkeeper.dll'));
end;

function HasDllX86: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\dll\x86\zkemkeeper.dll'));
end;

function HasDllRoot: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\dll\zkemkeeper.dll')) and 
            not FileExists(ExpandConstant('{app}\dll\x64\zkemkeeper.dll')) and 
            not FileExists(ExpandConstant('{app}\dll\x86\zkemkeeper.dll'));
end;

function GetRegsvr32X64(Param: String): String;
begin
  if Is64BitInstallMode then
    Result := ExpandConstant('{sys}\regsvr32.exe')
  else
    Result := ExpandConstant('{sysnative}\regsvr32.exe');
end;

function GetRegsvr32X86(Param: String): String;
begin
  if Is64BitInstallMode then
    Result := ExpandConstant('{syswow64}\regsvr32.exe')
  else
    Result := ExpandConstant('{sys}\regsvr32.exe');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Res: Integer;
  Regsvr32Path: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    if HasDllX64 or HasDllX86 or HasDllRoot then
    begin
      Res := MsgBox(
        '是否取消注册 (注销) ZKTeco COM SDK (zkemkeeper.dll) 组件？' + #13#10 + #13#10 +
        '取消注册后，系统中的 zkemkeeper COM 组件将失效。' + #13#10 +
        '若此机器上有其他软件正在使用该 DLL，建议选择“否”保留注册。' + #13#10 + #13#10 +
        '是否仍要取消注册？',
        mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2
      );

      if Res = IDYES then
      begin
        if HasDllX64 then
        begin
          Regsvr32Path := GetRegsvr32X64('');
          Exec(Regsvr32Path, '/u /s "' + ExpandConstant('{app}\dll\x64\zkemkeeper.dll') + '"', ExpandConstant('{app}\dll\x64'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;

        if HasDllX86 then
        begin
          Regsvr32Path := GetRegsvr32X86('');
          Exec(Regsvr32Path, '/u /s "' + ExpandConstant('{app}\dll\x86\zkemkeeper.dll') + '"', ExpandConstant('{app}\dll\x86'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;

        if HasDllRoot then
        begin
          Regsvr32Path := ExpandConstant('{sys}\regsvr32.exe');
          Exec(Regsvr32Path, '/u /s "' + ExpandConstant('{app}\dll\zkemkeeper.dll') + '"', ExpandConstant('{app}\dll'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
end;
