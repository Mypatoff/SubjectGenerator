; Timetable setup. Build it with
;   ISCC.exe /DAppVersion=1.0.0 installer\Timetable.iss
; after publishing the app into publish\desktop (the GitHub build does both).
; The app is published self-contained, so nothing else (.NET) has to be installed on the PC.

#define AppName "Timetable"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppDir
  #define AppDir "..\publish\desktop"
#endif

[Setup]
AppId={{EF2346AF-2300-4CBB-8A05-0C707BA76CC8}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Timetable-Setup
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\Timetable.exe
SetupIconFile=..\src\Timetable.Desktop\Assets\Timetable.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

; The program used to be called SubjectGenerator. The AppId is unchanged, so this installer upgrades an old
; install in place; remove the old exe and shortcuts so nothing keeps starting the old program.
[InstallDelete]
Type: files; Name: "{app}\SubjectGenerator.exe"
Type: files; Name: "{autodesktop}\SubjectGenerator.lnk"
Type: filesandordirs; Name: "{autoprograms}\SubjectGenerator"

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Timetable.exe"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Timetable.exe"; Tasks: desktopicon

; Also runs after a silent install: the in-app Update button relies on this to reopen the app.
; runasoriginaluser keeps the app from running as administrator if the installer was elevated (it would look at another user's AppData).
[Run]
Filename: "{app}\Timetable.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall runasoriginaluser
