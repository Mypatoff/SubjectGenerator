using System.Text.Json;
using System.Text.Json.Nodes;

namespace SubjectGenerator.Core;

/// <summary>
/// Saves the school (classes, subjects, teachers, assignments and timetables) to a JSON file,
/// and reads it back.
///
/// The file has a version number. If the format ever changes: bump <see cref="CurrentVersion"/>
/// and add a MigrateVnToVn+1 step in <see cref="Load"/> that turns an old file into the new shape.
/// </summary>
public static class SchoolStorage
{
    public const int CurrentVersion = 2;

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
            Teachers = school.Teachers
                .Select(t => new TeacherEntry { Name = t.Name, DaysOff = t.DaysOff.OrderBy(d => d).ToList() })
                .ToList(),
            Assignments = school.Assignments
                .Select(a => new AssignmentEntry { Teacher = a.Teacher.Name, Subject = a.Subject.Name, Class = a.Class.Name })
                .ToList(),
            Lessons = LessonsOf(school).ToList()
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

        var json = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                   ?? throw new InvalidDataException("The school file is empty.");

        var version = json["Version"]?.GetValue<int>() ?? 1;
        if (version > CurrentVersion)
            throw new InvalidDataException(
                $"The school file was made by a newer version of the app (format {version}).");
        if (version == 1)
            MigrateV1ToV2(json);

        var file = json.Deserialize<SchoolFile>()!;

        foreach (var name in file.Classes)
            school.AddClass(name);
        foreach (var entry in file.Subjects)
            school.AddSubject(entry.Name, entry.LessonsPerWeek);
        foreach (var entry in file.Teachers)
        {
            var teacher = school.AddTeacher(entry.Name);
            // A day that no longer exists (the week was made shorter) is simply dropped.
            foreach (var day in entry.DaysOff.Where(d => d >= 0 && d < layout.DayCount))
                school.SetDayOff(teacher, day, true);
        }
        foreach (var entry in file.Assignments)
        {
            school.Assign(
                school.Teachers.First(t => t.Name == entry.Teacher),
                school.Subjects.First(s => s.Name == entry.Subject),
                school.Classes.First(c => c.Name == entry.Class));
        }
        foreach (var entry in file.Lessons)
        {
            var schoolClass = school.Classes.FirstOrDefault(c => c.Name == entry.Class);
            var fits = entry.Day >= 0 && entry.Day < layout.DayCount && entry.Slot >= 0 && entry.Slot < layout.SlotsPerDay;
            if (schoolClass is not null && fits)
                school.TimetableFor(schoolClass).Set(entry.Day, entry.Slot, new Lesson(entry.Subject, entry.Teacher));
        }
        return school;
    }

    /// <summary>
    /// Version 1 stored teachers as plain names: "Teachers": ["Karimov"].
    /// Version 2 stores objects so a teacher can have days off: "Teachers": [{"Name": "Karimov", "DaysOff": []}],
    /// and adds "Lessons" (the timetables), which version 1 did not have.
    /// </summary>
    private static void MigrateV1ToV2(JsonObject json)
    {
        var oldTeachers = json["Teachers"] as JsonArray ?? new JsonArray();
        var newTeachers = new JsonArray();
        foreach (var name in oldTeachers)
            newTeachers.Add(new JsonObject { ["Name"] = name!.GetValue<string>(), ["DaysOff"] = new JsonArray() });

        json["Teachers"] = newTeachers;
        json["Lessons"] = new JsonArray();
        json["Version"] = 2;
    }

    private static IEnumerable<LessonEntry> LessonsOf(School school)
    {
        foreach (var schoolClass in school.Classes)
        {
            var timetable = school.TimetableFor(schoolClass);
            for (var day = 0; day < school.Layout.DayCount; day++)
                for (var slot = 0; slot < school.Layout.SlotsPerDay; slot++)
                    if (timetable.Get(day, slot) is { } lesson)
                        yield return new LessonEntry
                        {
                            Class = schoolClass.Name, Day = day, Slot = slot, Subject = lesson.Subject, Teacher = lesson.Teacher
                        };
        }
    }

    // The shape of the JSON file. Kept separate from the School classes so that
    // changing the app's code does not silently change the file format.

    private sealed class SchoolFile
    {
        public int Version { get; set; }
        public List<string> Classes { get; set; } = new();
        public List<SubjectEntry> Subjects { get; set; } = new();
        public List<TeacherEntry> Teachers { get; set; } = new();
        public List<AssignmentEntry> Assignments { get; set; } = new();
        public List<LessonEntry> Lessons { get; set; } = new();
    }

    private sealed class SubjectEntry
    {
        public string Name { get; set; } = "";
        public int LessonsPerWeek { get; set; }
    }

    private sealed class TeacherEntry
    {
        public string Name { get; set; } = "";
        public List<int> DaysOff { get; set; } = new();
    }

    private sealed class AssignmentEntry
    {
        public string Teacher { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Class { get; set; } = "";
    }

    private sealed class LessonEntry
    {
        public string Class { get; set; } = "";
        public int Day { get; set; }
        public int Slot { get; set; }
        public string Subject { get; set; } = "";
        public string Teacher { get; set; } = "";
    }
}
