using System.Text.Json;
using System.Text.Json.Nodes;

namespace SubjectGenerator.Core;

/// <summary>
/// Saves the school (classes, subjects, teachers, assignments and timetables) to a JSON file,
/// and reads it back.
///
/// The file has a version number. If the format ever changes: bump <see cref="CurrentVersion"/>
/// and add a MigrateVnToVn+1 step in <see cref="Read"/> that turns an old file into the new shape.
///
/// Reading has two kinds of failure, and they must not be mixed up:
/// - the file cannot be read right now (locked, no access): an <see cref="IOException"/> or similar,
///   the file is fine and must stay where it is;
/// - the file is damaged: an <see cref="InvalidDataException"/>. <see cref="LoadWithRecovery"/> deals with it;
/// - the file is fine but from a newer app: a <see cref="SchoolFileTooNewException"/>, left alone.
/// </summary>
public static class SchoolStorage
{
    public const int CurrentVersion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>%AppData%\SubjectGenerator\school.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SubjectGenerator", "school.json");

    public static void Save(School school, string path)
    {
        var file = new SchoolFile
        {
            Version = CurrentVersion,
            Week = new WeekEntry { Days = school.Layout.DayNames.ToList(), LessonsPerDay = school.Layout.SlotsPerDay },
            Classes = school.Classes.Select(c => c.Name).ToList(),
            Subjects = school.Subjects.Select(s => new SubjectEntry { Name = s.Name, LessonsPerWeek = s.LessonsPerWeek }).ToList(),
            Teachers = school.Teachers
                .Select(t => new TeacherEntry { Name = t.Name, DaysOff = t.DaysOff.OrderBy(d => d).ToList() })
                .ToList(),
            Assignments = school.Assignments
                .Select(a => new AssignmentEntry
                {
                    Teacher = a.Teacher.Name, Subject = a.Subject.Name, Class = a.Class.Name, LessonsPerWeek = a.LessonsPerWeek
                })
                .ToList(),
            Lessons = LessonsOf(school).ToList()
        };

        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temporary file first, force it onto the disk, then swap it in: if the PC turns off
        // mid-save, the old file is still there instead of a half-written (or empty) one.
        // The file being replaced is kept as school.json.bak, so there is always the save before this one.
        var tempPath = path + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, file, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
            File.Replace(tempPath, path, BackupPath(path));
        else
            File.Move(tempPath, path);
    }

    private static string BackupPath(string path) => path + ".bak";

    /// <summary>
    /// Reads the school from the file. A missing file means a new, empty school with the default week.
    /// Throws <see cref="InvalidDataException"/> if the file is damaged; a file that cannot be opened
    /// throws an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> instead.
    /// </summary>
    public static School Load(string path)
    {
        if (!File.Exists(path))
            return new School(WeekLayout.Default);

        var text = File.ReadAllText(path);
        try
        {
            return Read(text, path);
        }
        catch (Exception ex) when (ex is not (InvalidDataException or SchoolFileTooNewException))
        {
            // Bad JSON, a missing field, a name that appears twice...: whatever it was, the file is no good.
            throw new InvalidDataException("The school file is damaged: " + ex.Message, ex);
        }
    }

    /// <summary>How <see cref="LoadWithRecovery"/> got the school.</summary>
    public enum LoadOutcome
    {
        /// <summary>The file was read as it is (or there was no file yet).</summary>
        Loaded,

        /// <summary>The file was damaged, so it was put aside and the previous save (school.json.bak) was used.</summary>
        RestoredFromBackup,

        /// <summary>The file was damaged and there was no usable backup, so the school is empty.</summary>
        StartedEmpty
    }

    /// <param name="BrokenFilePath">Where the damaged file was put aside, if it was damaged.</param>
    public sealed record LoadResult(School School, LoadOutcome Outcome, string? BrokenFilePath);

    /// <summary>
    /// <see cref="Load"/>, but a damaged file is put aside (never deleted) and the backup of the previous
    /// save is tried before giving up with an empty school. A file that merely cannot be read right now,
    /// or one from a newer app, throws and is left alone: starting empty would invite overwriting good data.
    /// </summary>
    public static LoadResult LoadWithRecovery(string path)
    {
        try
        {
            return new LoadResult(Load(path), LoadOutcome.Loaded, null);
        }
        catch (InvalidDataException)
        {
            var brokenPath = path + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, brokenPath);

            var backupPath = BackupPath(path);
            if (File.Exists(backupPath))
            {
                try
                {
                    var restored = Load(backupPath);
                    Save(restored, path); // without this the next start would find no file and begin empty
                    return new LoadResult(restored, LoadOutcome.RestoredFromBackup, brokenPath);
                }
                catch (InvalidDataException)
                {
                    // The backup is no good either.
                }
            }
            return new LoadResult(new School(WeekLayout.Default), LoadOutcome.StartedEmpty, brokenPath);
        }
    }

    private static School Read(string text, string path)
    {
        var json = JsonNode.Parse(text) as JsonObject
                   ?? throw new InvalidDataException("The school file is empty.");

        var version = json["Version"]?.GetValue<int>() ?? 1;
        if (version > CurrentVersion)
            throw new SchoolFileTooNewException(version);
        if (version < CurrentVersion)
            CopyBeforeMigration(path, version);
        if (version == 1)
            MigrateV1ToV2(json);
        if (version <= 2)
            MigrateV2ToV3(json);

        var file = json.Deserialize<SchoolFile>()!;
        var layout = new WeekLayout(file.Week.Days, file.Week.LessonsPerDay);
        var school = new School(layout);

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
            var teacher = school.Teachers.FirstOrDefault(t => t.Name == entry.Teacher);
            var subject = school.Subjects.FirstOrDefault(s => s.Name == entry.Subject);
            var schoolClass = school.Classes.FirstOrDefault(c => c.Name == entry.Class);
            if (teacher is null || subject is null || schoolClass is null)
                continue; // points at something that is not there: skip it instead of losing the whole school

            try
            {
                school.Assign(teacher, subject, schoolClass, entry.LessonsPerWeek);
            }
            catch (SchoolRuleException)
            {
                // A second teacher for the same subject and class, or a bad number of lessons: skip it too.
            }
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
    /// The next save writes the new format over the old file, so keep a copy of the old one first
    /// (school.json.v2.bak). Only the first copy is kept. If it cannot be made, loading goes on anyway.
    /// </summary>
    private static void CopyBeforeMigration(string path, int version)
    {
        try
        {
            File.Copy(path, $"{path}.v{version}.bak", overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Already copied by an earlier start, or the folder is read-only; not worth stopping for.
        }
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

    /// <summary>
    /// Version 3 adds "Week" (school days and lessons a day; before, the week was always Monday-Saturday
    /// with 7 lessons) and a "LessonsPerWeek" on every assignment (before, every class got the subject's number).
    /// </summary>
    private static void MigrateV2ToV3(JsonObject json)
    {
        var oldWeekDays = new JsonArray();
        foreach (var day in WeekLayout.AllDayNames.Take(6))
            oldWeekDays.Add(day);
        json["Week"] = new JsonObject { ["Days"] = oldWeekDays, ["LessonsPerDay"] = 7 };

        var subjectLessons = new Dictionary<string, int>();
        foreach (var subject in json["Subjects"] as JsonArray ?? new JsonArray())
            subjectLessons[subject!["Name"]!.GetValue<string>()] = subject["LessonsPerWeek"]!.GetValue<int>();

        foreach (var assignment in json["Assignments"] as JsonArray ?? new JsonArray())
            assignment!["LessonsPerWeek"] = subjectLessons[assignment["Subject"]!.GetValue<string>()];

        json["Version"] = 3;
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
        public WeekEntry Week { get; set; } = new();
        public List<string> Classes { get; set; } = new();
        public List<SubjectEntry> Subjects { get; set; } = new();
        public List<TeacherEntry> Teachers { get; set; } = new();
        public List<AssignmentEntry> Assignments { get; set; } = new();
        public List<LessonEntry> Lessons { get; set; } = new();
    }

    private sealed class WeekEntry
    {
        public List<string> Days { get; set; } = new();
        public int LessonsPerDay { get; set; }
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
        public int LessonsPerWeek { get; set; }
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
