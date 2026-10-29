# SubjectGenerator

Dars jadvali (timetable) manager for a school: classes, subjects and teachers' time, with a generated
one-week schedule for each class.

Status: working app. Classes, subjects, teachers and who teaches what are entered in Settings; "Jadval tuzish" makes a weekly timetable for all classes at once. Manual editing of slots is next.

## Download
Every push to `main` builds on GitHub: see the **latest** release for `SubjectGenerator-Setup.exe`
(installer) and `SubjectGenerator.exe` (single file, no install).

## Develop
`dotnet test src/SubjectGenerator.Tests` runs the Core tests anywhere. The WPF app (`src/SubjectGenerator.Desktop`)
builds on Windows only; GitHub Actions builds it.

## Your data
Saved automatically in `%AppData%\SubjectGenerator\`:
- `school.json` - the current data
- `school.json.bak` - the save before the last one
- `school.json.daily-YYYYMMDD.bak` - one copy per day, the newest 14 are kept
If `school.json` is damaged, the app puts it aside as `school.json.broken-*` and restores the newest usable copy.
