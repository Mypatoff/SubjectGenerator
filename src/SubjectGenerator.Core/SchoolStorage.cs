using System.Text.Json;

namespace SubjectGenerator.Core;

/// <summary>
/// Saves the school's classes, subjects, teachers and assignments to a JSON file, and reads them back.
/// Timetables are not saved yet.
///
/// The file has a version number. If the format ever changes: bump <see cref="CurrentVersion"/>,
/// and in <see cref="Load"/> convert files with the old version to the new shape (a migration).
/// </summary>
public static class SchoolStorage
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>%AppData%\SubjectGenerator\school.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubjectGenerator", "school.json");

    public static void Save(School school, string path)
    {
        var file = new SchoolFile
        {
            Version = CurrentVersion,
            Classes = school.Classes.Select(c => c.Name).ToList(),
            Subjects = school.Subjects.Select(s => new SubjectEntry { Name = s.Name, LessonsPerWeek = s.LessonsPerWeek }).ToList(),
            Teachers = school.Teachers.Select(t => t.Name).ToList(),
            Assignments = school.Assignments
                .Select(a => new AssignmentEntry { Teacher = a.Teacher.Name, Subject = a.Subject.Name, Class = a.Class.Name })
                .ToList()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        // Write to a temporary file first, then swap it in: if the PC turns off mid-save,
        // the old file is still there instead of a half-written one.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(file, JsonOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>Reads the school from the file. A missing file means a new, empty school.</summary>
    public static School Load(string path, WeekLayout layout)
    {
        var school = new School(layout);
        if (!File.Exists(path))
            return school;

        var file = JsonSerializer.Deserialize<SchoolFile>(File.ReadAllText(path))
                   ?? throw new InvalidDataException("The school file is empty.");
        if (file.Version > CurrentVersion)
            throw new InvalidDataException(
                $"The school file was made by a newer version of the app (format {file.Version}).");

        foreach (var name in file.Classes)
            school.AddClass(name);
        foreach (var entry in file.Subjects)
            school.AddSubject(entry.Name, entry.LessonsPerWeek);
        foreach (var name in file.Teachers)
            school.AddTeacher(name);
        foreach (var entry in file.Assignments)
        {
            school.Assign(
                school.Teachers.First(t => t.Name == entry.Teacher),
                school.Subjects.First(s => s.Name == entry.Subject),
                school.Classes.First(c => c.Name == entry.Class));
        }
        return school;
    }

    // The shape of the JSON file. Kept separate from the School classes so that
    // changing the app's code does not silently change the file format.

    private sealed class SchoolFile
    {
        public int Version { get; set; }
        public List<string> Classes { get; set; } = new();
        public List<SubjectEntry> Subjects { get; set; } = new();
        public List<string> Teachers { get; set; } = new();
        public List<AssignmentEntry> Assignments { get; set; } = new();
    }

    private sealed class SubjectEntry
    {
        public string Name { get; set; } = "";
        public int LessonsPerWeek { get; set; }
    }

    private sealed class AssignmentEntry
    {
        public string Teacher { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Class { get; set; } = "";
    }
}
