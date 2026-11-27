; Glyph's installer (Inno Setup 6.5+). Built by release.ps1, or by hand:
;   ISCC.exe /DSrc=..\out\Glyph /DVersion=1.2.3 /O..\out installer\Glyph.iss
; Src is a folder laid out like the Glyph folder: bin\ (the program), config\README.txt, GUIDE.md, README.md.
;
; Installs for you only (no administrator needed), into a folder you choose (default
; %LOCALAPPDATA%\Programs\Glyph). Your settings live in <that folder>\config and are never touched by
; updates or by uninstalling.

#ifndef Src
  #define Src "..\out\Glyph"
#endif
#ifndef Version
  #define Version "0.0.0"
#endif

[Setup]
AppId={{6F1C8A52-3D4B-4E7A-9C21-6A7970680001}
AppName=Glyph
AppVersion={#Version}
AppVerName=Glyph {#Version}
AppPublisher=exinics
AppPublisherURL=https://github.com/exinics/glyph
AppSupportURL=https://github.com/exinics/glyph/issues
AppUpdatesURL=https://github.com/exinics/glyph/releases
DefaultDirName={userpf}\Glyph
DisableDirPage=no
UsePreviousAppDir=yes
DefaultGroupName=Glyph
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputBaseFilename=Glyph-Setup-{#Version}
SetupIconFile=..\src\Glyph\glyph.ico
UninstallDisplayIcon={app}\bin\Glyph.exe
UninstallDisplayName=Glyph
WizardStyle=modern dark
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=no

[Messages]
SelectDirLabel3=Glyph will be installed into this folder. Your settings and themes will live in its "config" folder.

[Tasks]
Name: "autostart"; Description: "Start Glyph when I sign in to Windows"
Name: "desktopicon"; Description: "Make a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#Src}\bin\*"; DestDir: "{app}\bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Src}\config\README.txt"; DestDir: "{app}\config"; Flags: onlyifdoesntexist uninsneveruninstall
Source: "{#Src}\GUIDE.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#Src}\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\Glyph"; Filename: "{app}\bin\Glyph.exe"; WorkingDir: "{app}\bin"
Name: "{group}\Glyph settings"; Filename: "{app}\bin\Glyph.exe"; Parameters: "--settings"; WorkingDir: "{app}\bin"
Name: "{group}\Glyph video converter"; Filename: "{app}\bin\Glyph.exe"; Parameters: "--convert-gui"; WorkingDir: "{app}\bin"
Name: "{autodesktop}\Glyph"; Filename: "{app}\bin\Glyph.exe"; WorkingDir: "{app}\bin"; Tasks: desktopicon

[Run]
Filename: "{app}\bin\Glyph.exe"; Description: "Start Glyph now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\bin\Glyph.exe"; Parameters: "--no-autostart"; Flags: runhidden waituntilterminated; RunOnceId: "GlyphAutostartOff"

[UninstallDelete]
; files the updater added since installing, and Glyph's logs; config\ (your settings) stays
Type: filesandordirs; Name: "{app}\bin"
Type: filesandordirs; Name: "{app}\logs"

[Code]
const
  EVENT_MODIFY_STATE = $0002;
  SYNCHRONIZE = $00100000;
  RuntimeUrl = 'https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe';

function OpenEvent(Access: DWORD; Inherit: BOOL; Name: string): THandle; external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(H: THandle): BOOL; external 'SetEvent@kernel32.dll stdcall';
function OpenMutex(Access: DWORD; Inherit: BOOL; Name: string): THandle; external 'OpenMutexW@kernel32.dll stdcall';
function CloseHandle(H: THandle): BOOL; external 'CloseHandle@kernel32.dll stdcall';

var
  DownloadPage: TDownloadWizardPage;

function GlyphRunning(): Boolean;
var H: THandle;
begin
  H := OpenMutex(SYNCHRONIZE, False, 'Local\Glyph.SingleInstance');
  Result := H <> 0;
  if Result then CloseHandle(H);
end;

{ Asks a running Glyph (any copy, any version) to quit properly, so Windows' icons and taskbar come back. }
procedure QuitGlyph();
var H: THandle; I: Integer;
begin
  if not GlyphRunning() then Exit;
  H := OpenEvent(EVENT_MODIFY_STATE, False, 'Local\Glyph.Quit');
  if H <> 0 then begin SetEvent(H); CloseHandle(H); end;
  for I := 1 to 75 do begin
    if not GlyphRunning() then Exit;
    Sleep(200);
  end;
end;

function HasDesktopRuntime(): Boolean;
var F: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\9.*'), F) then
  begin
    Result := True;
    FindClose(F);
  end;
end;

procedure InitializeWizard();
begin
  DownloadPage := CreateDownloadPage('Getting .NET', 'Glyph needs Microsoft''s .NET 9 Desktop Runtime, which isn''t on this PC yet.', nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Code: Integer;
begin
  Result := True;
  if (CurPageID <> wpReady) or HasDesktopRuntime() then Exit;
  DownloadPage.Clear;
  DownloadPage.Add(RuntimeUrl, 'windowsdesktop-runtime-9-x64.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      if not DownloadPage.AbortedByUser then
        MsgBox('Couldn''t download .NET: ' + GetExceptionMessage + #13#10#13#10 + 'You can get it from https://dotnet.microsoft.com/download/dotnet/9.0 (".NET Desktop Runtime", x64), then run this setup again.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  finally
    DownloadPage.Hide;
  end;
  { the runtime installs for the whole PC, so Windows asks for permission here }
  if not ShellExec('runas', ExpandConstant('{tmp}\windowsdesktop-runtime-9-x64.exe'), '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code) or not HasDesktopRuntime() then
  begin
    MsgBox('.NET didn''t install (' + IntToStr(Code) + '). Glyph won''t start without it. You can get it from https://dotnet.microsoft.com/download/dotnet/9.0 (".NET Desktop Runtime", x64).', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  QuitGlyph();
  if GlyphRunning() then
    Result := 'Glyph is still running. Quit it (tray icon > Quit) and click Back, then Next.'
  else
    Result := '';
end;

{ A Glyph used before (e.g. from a downloaded zip) left its folder in the registry: offer to bring its settings. }
procedure CopyOldSettings();
var Old, New: String; Code: Integer;
begin
  if not RegQueryStringValue(HKCU, 'Software\Glyph', 'Home', Old) then Exit;
  New := ExpandConstant('{app}');
  if CompareText(RemoveBackslashUnlessRoot(Old), RemoveBackslashUnlessRoot(New)) = 0 then Exit;
  if not DirExists(AddBackslash(Old) + 'config\themes') then Exit;
  if DirExists(AddBackslash(New) + 'config\themes') then Exit;
  if WizardSilent() then Exit;
  if MsgBox('You''ve used Glyph before, from:' + #13#10 + Old + #13#10#13#10 + 'Copy its settings, themes and shortcuts here?', mbConfirmation, MB_YESNO) <> IDYES then Exit;
  Exec(ExpandConstant('{sys}\robocopy.exe'), '"' + AddBackslash(Old) + 'config" "' + AddBackslash(New) + 'config" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP', '', SW_HIDE, ewWaitUntilTerminated, Code);
  if Code >= 8 then MsgBox('Some files couldn''t be copied (robocopy ' + IntToStr(Code) + '). Your old folder is unchanged.', mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Code: Integer; OnOff: String;
begin
  if CurStep <> ssPostInstall then Exit;
  CopyOldSettings();   { first: Glyph makes its starting theme the first time it runs }
  if WizardIsTaskSelected('autostart') then OnOff := 'on' else OnOff := 'off';
  Exec(ExpandConstant('{app}\bin\Glyph.exe'), '--start-with-windows ' + OnOff, ExpandConstant('{app}\bin'), SW_HIDE, ewWaitUntilTerminated, Code);
end;

function InitializeUninstall(): Boolean;
begin
  QuitGlyph();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Home: String;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  if RegQueryStringValue(HKCU, 'Software\Glyph', 'Home', Home) and (CompareText(RemoveBackslashUnlessRoot(Home), RemoveBackslashUnlessRoot(ExpandConstant('{app}'))) = 0) then
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Glyph');
  if DirExists(ExpandConstant('{app}\config')) then
    MsgBox('Your settings and themes were kept in:' + #13#10 + ExpandConstant('{app}\config') + #13#10#13#10 + 'Delete that folder if you don''t need them.', mbInformation, MB_OK);
end;
