#ifndef AppVersion
  #error Build this template using tools/Build-Release.ps1 with an explicit edition.
#endif

[Setup]
AppId={#InstallerAppId}
AppName=radioMAT
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher={#Publisher}
VersionInfoCompany={#Publisher}
AppCopyright={#Publisher}
#if Len(PublisherUrl) > 0
AppPublisherURL={#PublisherUrl}
#endif
DefaultDirName={localappdata}\Programs\{#InstallDirectory}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDirectory}
OutputBaseFilename=radioMAT-Setup-{#AppVersion}-x64
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\radioMAT.exe
UninstallDisplayName=radioMAT
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
; The uninstall list is rewritten instead of appended, so paths of files from older versions do not
; stay in it forever. [InstallDelete] removes those files anyway (same as Whisper Automat, 11.11).
UninstallLogMode=overwrite

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"
#if PublicEdition == "1"
Name: "english"; MessagesFile: "compiler:Default.isl"
#endif

[CustomMessages]
polish.DesktopIcon=Utwórz skrót na pulpicie
polish.LaunchRadio=Uruchom radioMAT
#if PublicEdition == "1"
english.DesktopIcon=Create a desktop shortcut
english.LaunchRadio=Launch radioMAT
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[InstallDelete]
; An update does not remove files the new package no longer has, so leftovers of older versions
; would stay. Program files go before copying and [Files] puts the new version back. Settings live
; in %APPDATA% (outside {app}) and the uninstaller (unins*) is left alone. Silent: not in release notes.
Type: files; Name: "{app}\radioMAT.exe"
Type: files; Name: "{app}\createdump.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\licenses"
Type: filesandordirs; Name: "{app}\pl"

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#InstallDirectory}"; Filename: "{app}\radioMAT.exe"; WorkingDir: "{app}"; AppUserModelID: "{#AppUserModelId}"
Name: "{userdesktop}\{#InstallDirectory}"; Filename: "{app}\radioMAT.exe"; WorkingDir: "{app}"; AppUserModelID: "{#AppUserModelId}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#RunValueName}"; ValueData: """{app}\radioMAT.exe"" --autostart"; Flags: uninsdeletevalue; Check: HasAutostart

[Run]
Filename: "{app}\radioMAT.exe"; Description: "{cm:LaunchRadio}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function HasAutostart: Boolean;
begin
  Result := RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#RunValueName}');
end;
