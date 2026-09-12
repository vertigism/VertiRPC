; Inno Setup script for VertiRPC.
; Build it through build.ps1, which publishes the app first and passes the
; version in; compiling this file on its own expects publish\ to already exist.

#define AppName "VertiRPC"
#define AppPublisher "vertigism"
#define AppUrl "https://github.com/vertigism/VertiRPC"
#define AppExe "VertiRPC.exe"

; No fallback on purpose: the version lives in the csproj, and a default here
; would quietly stamp a stale number on a setup built any other way.
#ifndef AppVersion
  #error AppVersion was not passed in. Build through build.ps1, which reads it from the csproj.
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

; Restart Manager finds the running copy through the files it holds open and
; closes it, so an upgrade does not fail on a locked executable. Deliberately no
; AppMutex: that is checked while Setup initialises, before any of this runs, so
; it only ever blocked with "please close all instances" -- advice nobody can act
; on when the app's window is hidden and only its tray icon is left.
CloseApplications=yes
; The postinstall task offers to start it again, which is the user's choice to
; make; silently relaunching something they may have closed on purpose is not.
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The symbols are left out: nothing in the app surfaces a stack trace, so on a
; user's machine they are weight with no reader.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Written by the app next to its own executable, never by the installer.
Type: files; Name: "{app}\*.log"

[UninstallRun]
; Stop the running copy before its files go away. RunOnceId keeps it to a single
; execution: without one, an uninstall that repeats a step would run it again.
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExe}"; RunOnceId: "StopVertiRPC"; Flags: runhidden skipifdoesntexist

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
