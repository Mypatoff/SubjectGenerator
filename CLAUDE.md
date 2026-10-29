# SubjectGenerator

Weekly class timetable manager/generator for a school (20 classes, teachers, subjects). Windows desktop
app, UI language Uzbek. Meant to be sold later, so keep it clean and reliable.

## Stack
C# .NET 8, WPF. xUnit tests. Installer: Inno Setup, built by GitHub Actions (`SubjectGenerator-Setup.exe`
plus a single-file `SubjectGenerator.exe`, both on the "latest" release).

## Projects
- SubjectGenerator.Core: school data and timetable logic (plain .NET, builds anywhere)
- SubjectGenerator.Desktop: WPF app (Windows only)
- SubjectGenerator.Tests: tests for Core

## Rules
- Keep code simple and readable; the owner is learning.
- Timetable/generation logic lives in Core and must have tests.
- WPF can only be compiled on Windows: after changing Desktop, push and check the GitHub Actions run
  before saying it works. A passing Core test run does not prove the XAML compiles.
- Any future database model change must ship with its migration in the same change.

## Dev environment
Owner's Windows PC (E:\projects\SubjectGenerator) has the .NET 8 SDK, so `dotnet build SubjectGenerator.sln`
compiles the WPF app there too. Linux box: `dotnet test src/SubjectGenerator.Tests` works. `dotnet build` of the whole solution does not
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
- [x] Saving data: JSON at %AppData%\SubjectGenerator\school.json (`SchoolStorage`, format version 3
      with v1->v2->v3 migrations, saved after every change). Includes teachers' days off and timetables.
      Safety: a save is flushed to disk and the previous one is kept as school.json.bak; a damaged file is put
      aside as school.json.broken-* and the .bak is used; a file that is merely locked or from a newer app is
      never touched and the app closes instead of starting empty; an old-format file is copied to
      school.json.vN.bak before migrating; only one copy of the app can run (mutex in `App`).
- [x] Timetables never contradict the plan: removing a teacher/subject/assignment, changing lessons per week
      or adding a day off removes the affected lessons (`School.RemoveLessons`). Generating asks before
      replacing a timetable with a worse one.
- [x] Teacher days off (Settings > O'qituvchilar)
- [x] Per-class lessons per week (Settings > Sinflar shows each class's plan and total)
- [x] Week settings (Settings > Hafta)
- [x] Timetable generation ("Jadval tuzish" button, `TimetableGenerator`): all classes at once; never a
      teacher in two classes at once or on a day off; tries for no gaps and subjects spread over the week
- [ ] Manual editing of slots
- [x] Update button (main screen, "Yangilash"): reads the "latest" release from the GitHub API, compares its
      version (release name "Latest build 1.0.N") with the version stamped into the app, downloads
      SubjectGenerator-Setup.exe and runs it silently (Windows asks permission only for an all-users install); the installer reopens the app.
      Parsing/comparing is `UpdateChecker` in Core (tested); download/launch is `AppUpdater` in Desktop.
      The single-file SubjectGenerator.exe (no unins000.exe next to it) only opens the release page.
      The installer is not code-signed yet: consider signing before selling.
