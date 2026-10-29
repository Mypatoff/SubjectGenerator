# SubjectGenerator

Dars jadvali (timetable) manager for a school: classes, subjects and teachers' time, with a generated
one-week schedule for each class.

Status: main screen only (pick a class, see its empty week). Data entry, saving and generation come next.

## Download
Every push to `main` builds on GitHub: see the **latest** release for `SubjectGenerator-Setup.exe`
(installer) and `SubjectGenerator.exe` (single file, no install).

## Develop
`dotnet test src/SubjectGenerator.Tests` runs the Core tests anywhere. The WPF app (`src/SubjectGenerator.Desktop`)
builds on Windows only; GitHub Actions builds it.
