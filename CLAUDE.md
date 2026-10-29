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
Linux box: `dotnet test src/SubjectGenerator.Tests` works. `dotnet build` of the whole solution does not
(WPF needs Windows), and `dotnet sln add` refuses the Desktop project - edit the .sln by hand.

## Product decisions so far
- Week: Monday-Saturday, 7 slots a day (`WeekLayout.Default`, easy to change).
- User enters classes, subjects (lessons per week), teachers, and which teacher teaches which subject.
- No rooms, no special cases (double lessons, split groups) for now. Manual editing of generated
  timetables is wanted.

## Progress
- [x] Main screen: class chips on top, empty week grid for the selected class (sample 1-A..10-B classes)
- [ ] Screens to enter classes, subjects, teachers, teacher-subject pairs
- [ ] Saving data
- [ ] Timetable generation
- [ ] Manual editing of slots
