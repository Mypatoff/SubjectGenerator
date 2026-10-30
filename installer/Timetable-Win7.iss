; Timetable setup for Windows 7 (the .NET Framework build). Build it with
;   ISCC.exe /DAppVersion=1.0.0 installer\Timetable-Win7.iss
; after building src\Timetable.Desktop.Win7 into publish\win7 (the GitHub build does both).
; It is a separate program from the normal Timetable (own AppId, folder and shortcut), but uses the same data in %AppData%\Timetable.
; Needs Windows 7 SP1 and .NET Framework 4.8 on the PC (Windows 10 and 11 have it already).

#define AppName "Timetable Win7"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppDir
  #define AppDir "..\publish\win7"
#endif

[Setup]
AppId={{5B0D6C52-3F0E-4E7B-A4C9-1D2E8F7A6B34}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Timetable
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=Timetable-Win7-Setup
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\Timetable-Win7.exe
SetupIconFile=..\src\Timetable.Desktop\Assets\Timetable.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=6.1sp1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#AppDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Timetable-Win7.exe"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Timetable-Win7.exe"; Tasks: desktopicon

; Also runs after a silent install: the in-app Update button relies on this to reopen the app.
[Run]
Filename: "{app}\Timetable-Win7.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall runasoriginaluser
