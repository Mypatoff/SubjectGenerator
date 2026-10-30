# Timetable

Dars jadvali (timetable) manager for a school: classes, subjects and teachers' time, with a generated
one-week schedule for each class.

Status: working app. Classes, subjects, teachers and who teaches what are entered in Settings; "Jadval tuzish" makes a weekly timetable for all classes at once. Manual editing of slots is next.

## Download
Every push to `main` builds on GitHub: see the **latest** release for `Timetable-Setup.exe`
(installer) and `Timetable.exe` (single file, no install).
An installed app updates itself with the **Yangilash** button on the main screen.

### Windows 7
`Timetable-Win7-Setup.exe` (same release) is a separate build for Windows 7 SP1 and newer. It needs .NET Framework 4.8
on the PC (Windows 10/11 already have it; on Windows 7 install it first). It installs next to, not over, the normal
Timetable, uses the same data folder, and updates itself from `Timetable-Win7-Setup.exe`.

## Develop
`dotnet test src/Timetable.Tests` runs the Core tests anywhere. The WPF app (`src/Timetable.Desktop`)
builds on Windows only; GitHub Actions builds it.

## Your data
Saved automatically in `%AppData%\Timetable\`:
- `school.json` - the current data
- `school.json.bak` - the save before the last one
- `school.json.daily-YYYYMMDD.bak` - one copy per day, the newest 14 are kept
If `school.json` is damaged, the app puts it aside as `school.json.broken-*` and restores the newest usable copy.

The program used to be called SubjectGenerator. On first start it copies the data from `%AppData%\SubjectGenerator\` (the old folder is kept).
