# Timetable

Weekly class timetable manager/generator for a school (20 classes, teachers, subjects). Windows desktop
app, UI language Uzbek. Meant to be sold later, so keep it clean and reliable.

## Stack
C# .NET 8, WPF. xUnit tests. Installer: Inno Setup, built by GitHub Actions (`Timetable-Setup.exe`
plus a single-file `Timetable.exe`, both on the "latest" release).

## Projects
- Timetable.Core: school data and timetable logic (plain .NET, builds anywhere)
- Timetable.Desktop: WPF app (Windows only)
- Timetable.Tests: tests for Core

## Rules
- Keep code simple and readable; the owner is learning.
- Timetable/generation logic lives in Core and must have tests.
- WPF can only be compiled on Windows: after changing Desktop, push and check the GitHub Actions run
  before saying it works. A passing Core test run does not prove the XAML compiles.
- Any future database model change must ship with its migration in the same change.

## Dev environment
Owner's Windows PC (E:\projects\SubjectGenerator (the folder itself was not renamed)) has the .NET 8 SDK, so `dotnet build Timetable.sln`
compiles the WPF app there too. Linux box: `dotnet test src/Timetable.Tests` works. `dotnet build` of the whole solution does not
(WPF needs Windows), and `dotnet sln add` refuses the Desktop project - edit the .sln by hand.

## Product decisions so far
- Week: chosen in Settings > Hafta (school days + one fixed number of lessons a day; default Mon-Sat x 8).
  Classes may have fewer lessons on some days (lessons are spread evenly, no gaps).
- User enters classes, subjects (usual lessons per week), teachers, and which teacher teaches which subject
  in which class. Lessons per week can differ per class (stored on `TeachingAssignment`), because real
  data (a college workload plan) has e.g. Ingliz tili 10/week in one group and 2 in another.
- A class's subjects are exactly its assignments: not every class has every subject.
- No rooms, no special cases (double lessons, split groups) for now. Manual editing of generated
  timetables is wanted.

## Progress
- [x] Main screen: searchable class drop-down on top, week grid for the selected class, Settings button
- [x] Settings window: classes, subjects (lessons per week), teachers, which teacher teaches which
      subject in which classes (one teacher per subject per class; a teacher can have many)
- [x] Saving data: JSON at %AppData%\Timetable\school.json (`SchoolStorage`, format version 3
      with v1->v2->v3 migrations, saved after every change). Includes teachers' days off and timetables.
      Safety: a save is flushed to disk and the previous one is kept as school.json.bak; a damaged file is put
      aside as school.json.broken-* and the .bak is used; a file that is merely locked or from a newer app is
      never touched and the app closes instead of starting empty; an old-format file is copied to
      school.json.vN.bak before migrating; only one copy of the app can run (mutex in `App`).
- [x] Timetables never contradict the plan: removing a teacher/subject/assignment, changing lessons per week
      or adding a day off removes the affected lessons (`School.RemoveLessons`). Generating asks before
      replacing a timetable with a worse one.
- [x] Renamed from SubjectGenerator to Timetable (repo Mypatoff/timetable, Timetable.exe, Timetable-Setup.exe).
      The installer AppId is unchanged on purpose so it upgrades old installs (and removes the old exe/shortcuts);
      on first start `SchoolStorage.CopyLegacyData` copies %AppData%\SubjectGenerator to %AppData%\Timetable
      (the old folder is left untouched). Do not change the AppId.
- [x] Teacher days off (Settings > O'qituvchilar)
- [x] Per-class lessons per week (Settings > Sinflar shows each class's plan and total)
- [x] Week settings (Settings > Hafta)
- [x] Timetable generation ("Jadval tuzish" button, `TimetableGenerator`): all classes at once; never a
      teacher in two classes at once or on a day off; tries for no gaps and subjects spread over the week
- [ ] Manual editing of slots
- [x] Update button (main screen, "Yangilash"): reads the "latest" release from the GitHub API, compares its
      version (release name "Latest build 1.0.N") with the version stamped into the app, downloads
      Timetable-Setup.exe and runs it with `/SILENT` (a small progress window, no wizard pages; Windows asks permission
      only for an all-users install); the installer reopens the app. The download is checked against the release's
      SHA256SUMS.txt (made by the build workflow) and deleted if it does not match; a release without that file is refused.
      Parsing/comparing is `UpdateChecker` in Core (tested); download/launch is `AppUpdater` in Desktop.
      The single-file Timetable.exe (no unins000.exe next to it) only opens the release page.
      The installer is not code-signed yet: consider signing before selling.
