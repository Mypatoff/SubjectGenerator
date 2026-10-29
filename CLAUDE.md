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
- Week: Monday-Saturday, 7 slots a day (`WeekLayout.Default`, easy to change).
- User enters classes, subjects (lessons per week), teachers, and which teacher teaches which subject.
- No rooms, no special cases (double lessons, split groups) for now. Manual editing of generated
  timetables is wanted.

## Progress
- [x] Main screen: searchable class drop-down on top, week grid for the selected class, Settings button
- [x] Settings window: classes, subjects (lessons per week), teachers, which teacher teaches which
      subject in which classes (one teacher per subject per class; a teacher can have many)
- [x] Saving data: JSON at %AppData%\SubjectGenerator\school.json (`SchoolStorage`, format version 1,
      saved after every change). Timetables are not saved yet.
- [ ] Timetable generation
- [ ] Manual editing of slots
