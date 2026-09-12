; Inno Setup script for VertiRPC.
; Build it through build.ps1, which publishes the app first and passes the
; version in; compiling this file on its own expects publish\ to already exist.

#define AppName "VertiRPC"
#define AppPublisher "vertigism"
#define AppUrl "https://github.com/vertigism/VertiRPC"
#define AppExe "VertiRPC.exe"

#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
; Never change AppId: it is what makes an install an upgrade rather than a
; second copy alongside the first.
AppId={{1E330AE1-28F4-4E36-B51E-60E3711C8CB5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#AppVersion}

; A tray app needs no elevation, and a per-user install keeps it that way: no
; UAC prompt, and the startup entry it writes is per-user too.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=Output
OutputBaseFilename={#AppName}-{#AppVersion}-Setup
SetupIconFile=..\src\VertiRPC\Assets\icon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Lets the installer notice a running copy and offer to close it, instead of
; failing on a locked executable. The name matches the mutex App.xaml.cs takes.
AppMutex=VertiRPC_SingleInstance_Mutex,Global\VertiRPC_SingleInstance_Mutex
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Written by the app next to its own executable, never by the installer.
Type: files; Name: "{app}\*.log"

[UninstallRun]
; Stop the running copy before its files go away.
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden skipifdoesntexist

[Registry]
; The app owns this value through its "Run on Startup" checkbox; uninstalling
; has to take it away, or Windows keeps trying to launch a deleted exe.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; ValueType: none; Flags: deletevalue uninsdeletevalue

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0/runtime';

{ VertiRPC is published framework-dependent, so the desktop runtime has to be
  present. The shared framework folder is the simplest reliable marker. }
function IsDesktopRuntimeInstalled: Boolean;
var
  SharedRoot: String;
  FindRec: TFindRec;
begin
  Result := False;
  SharedRoot := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');

  if FindFirst(SharedRoot + '\*', FindRec) then
  try
    repeat
      if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        if Pos('10.', FindRec.Name) = 1 then
        begin
          Result := True;
          Exit;
        end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if IsDesktopRuntimeInstalled then
    Exit;

  if MsgBox('VertiRPC needs the .NET 10 Desktop Runtime, which is not installed.' + #13#10#13#10 +
            'Open the download page now?', mbConfirmation, MB_YESNO) = IDYES then
    ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);

  Result := False;
end;
