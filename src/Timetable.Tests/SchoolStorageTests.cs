using Timetable.Core;
using Xunit;

namespace Timetable.Tests;

public sealed class SchoolStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "TimetableTests-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_folder, "school.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void CopyLegacyData_CopiesAllFilesAndLeavesTheOldFolderAlone()
    {
        var legacy = Path.Combine(_folder, "old");
        var target = Path.Combine(_folder, "new");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "school.json"), "current");
        File.WriteAllText(Path.Combine(legacy, "school.json.bak"), "previous");

        SchoolStorage.CopyLegacyData(legacy, target);

        Assert.Equal("current", File.ReadAllText(Path.Combine(target, "school.json")));
        Assert.Equal("previous", File.ReadAllText(Path.Combine(target, "school.json.bak")));
        Assert.Equal("current", File.ReadAllText(Path.Combine(legacy, "school.json")));
        Assert.Equal("previous", File.ReadAllText(Path.Combine(legacy, "school.json.bak")));
    }

    [Fact]
    public void CopyLegacyData_NewFolderAlreadyHasData_IsNotOverwritten()
    {
        var legacy = Path.Combine(_folder, "old");
        var target = Path.Combine(_folder, "new");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(legacy, "school.json"), "old data");
        File.WriteAllText(Path.Combine(target, "school.json"), "new data");

        SchoolStorage.CopyLegacyData(legacy, target);

        Assert.Equal("new data", File.ReadAllText(Path.Combine(target, "school.json")));
    }

    [Fact]
    public void CopyLegacyData_NoOldFolder_DoesNothing()
    {
        var target = Path.Combine(_folder, "new");

        SchoolStorage.CopyLegacyData(Path.Combine(_folder, "missing"), target);

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void CopyLegacyData_CopiedFileLoadsAsTheSameSchool()
    {
        var legacy = Path.Combine(_folder, "old");
        var school = new School(TestWeek.SixBySeven);
        school.AddClass("5-A");
        SchoolStorage.Save(school, Path.Combine(legacy, "school.json"));

        SchoolStorage.CopyLegacyData(legacy, Path.Combine(_folder, "new"));

        Assert.Equal("5-A", SchoolStorage.Load(Path.Combine(_folder, "new", "school.json")).Classes.Single().Name);
    }

    [Fact]
    public void Load_MissingFile_GivesEmptySchool()
    {
        var school = SchoolStorage.Load(FilePath);

        Assert.Empty(school.Classes);
        Assert.Empty(school.Subjects);
    }

    [Fact]
    public void SaveThenLoad_KeepsEverything()
    {
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        school.AddSubject("Fizika", 2);
        var karimov = school.AddTeacher("Karimov");
        school.AddTeacher("Aliyev");
        school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);

        SchoolStorage.Save(school, FilePath);
        var loaded = SchoolStorage.Load(FilePath);

        Assert.Equal(new[] { "5-A", "5-B" }, loaded.Classes.Select(c => c.Name));
        Assert.Equal(new[] { "Matematika:5", "Fizika:2" }, loaded.Subjects.Select(s => $"{s.Name}:{s.LessonsPerWeek}"));
        Assert.Equal(new[] { "Karimov", "Aliyev" }, loaded.Teachers.Select(t => t.Name));
        Assert.Equal(
            new[] { "Karimov Matematika 5-A", "Karimov Matematika 5-B" },
            loaded.Assignments.Select(x => $"{x.Teacher.Name} {x.Subject.Name} {x.Class.Name}"));
    }

    [Fact]
    public void SaveThenLoad_KeepsDaysOffAndTimetables()
    {
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("5-A");
        var karimov = school.AddTeacher("Karimov");
        school.SetDayOff(karimov, 0, true);
        school.SetDayOff(karimov, 5, true);
        school.Assign(karimov, school.AddSubject("Matematika", 5), a);
        school.TimetableFor(a).Set(2, 3, new Lesson("Matematika", "Karimov"));

        SchoolStorage.Save(school, FilePath);
        var loaded = SchoolStorage.Load(FilePath);

        Assert.Equal(new[] { 0, 5 }, loaded.Teachers[0].DaysOff.OrderBy(d => d));
        Assert.Equal(new Lesson("Matematika", "Karimov"), loaded.TimetableFor(loaded.Classes[0]).Get(2, 3));
        Assert.Null(loaded.TimetableFor(loaded.Classes[0]).Get(2, 4));
    }

    [Fact]
    public void Load_Version1File_IsMigrated()
    {
        // A file saved by the first version of the app: teachers were plain names, no timetables.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """
            {
              "Version": 1,
              "Classes": ["5-A"],
              "Subjects": [{ "Name": "Matematika", "LessonsPerWeek": 5 }],
              "Teachers": ["Karimov", "Aliyev"],
              "Assignments": [{ "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-A" }]
            }
            """);

        var school = SchoolStorage.Load(FilePath);

        Assert.Equal(new[] { "Karimov", "Aliyev" }, school.Teachers.Select(t => t.Name));
        Assert.All(school.Teachers, t => Assert.Empty(t.DaysOff));
        Assert.Equal("Karimov", school.TeacherOf(school.Subjects[0], school.Classes[0])?.Name);
        Assert.Equal(5, school.Assignments[0].LessonsPerWeek);
        Assert.Equal(7, school.Layout.SlotsPerDay); // old files keep the old fixed week
    }

    [Fact]
    public void Load_MissingFile_UsesTheDefaultWeek()
    {
        Assert.Same(WeekLayout.Default, SchoolStorage.Load(FilePath).Layout);
    }

    [Fact]
    public void SaveThenLoad_KeepsTheWeekAndEachClassesLessonCount()
    {
        var school = new School(new WeekLayout(new[] { "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma" }, 8));
        var english = school.AddSubject("Ingliz tili", 10);
        var teacher = school.AddTeacher("Qashiboev");
        school.Assign(teacher, english, school.AddClass("40"));
        school.Assign(teacher, english, school.AddClass("43"), lessonsPerWeek: 2);

        SchoolStorage.Save(school, FilePath);
        var loaded = SchoolStorage.Load(FilePath);

        Assert.Equal(new[] { "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma" }, loaded.Layout.DayNames);
        Assert.Equal(8, loaded.Layout.SlotsPerDay);
        Assert.Equal(new[] { 10, 2 }, loaded.Assignments.Select(a => a.LessonsPerWeek));
    }

    [Fact]
    public void Load_Version2File_IsMigrated()
    {
        // Version 2: no week (it was always Monday-Saturday x 7), every class got the subject's number.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """
            {
              "Version": 2,
              "Classes": ["5-A", "5-B"],
              "Subjects": [{ "Name": "Matematika", "LessonsPerWeek": 5 }],
              "Teachers": [{ "Name": "Karimov", "DaysOff": [5] }],
              "Assignments": [
                { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-A" },
                { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-B" }
              ],
              "Lessons": [{ "Class": "5-A", "Day": 4, "Slot": 6, "Subject": "Matematika", "Teacher": "Karimov" }]
            }
            """);

        var school = SchoolStorage.Load(FilePath);

        Assert.Equal(6, school.Layout.DayCount);
        Assert.Equal(7, school.Layout.SlotsPerDay);
        Assert.All(school.Assignments, a => Assert.Equal(5, a.LessonsPerWeek));
        Assert.True(school.Teachers[0].IsDayOff(5));
        Assert.Equal("Matematika", school.TimetableFor(school.Classes[0]).Get(4, 6)?.Subject);
    }

    [Fact]
    public void Save_Twice_OverwritesTheFile()
    {
        var school = new School(TestWeek.SixBySeven);
        school.AddClass("5-A");
        SchoolStorage.Save(school, FilePath);

        school.AddClass("6-A");
        SchoolStorage.Save(school, FilePath);

        Assert.Equal(2, SchoolStorage.Load(FilePath).Classes.Count);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_FileFromNewerVersion_Throws()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{\"Version\": 999}");

        Assert.Throws<SchoolFileTooNewException>(() => SchoolStorage.Load(FilePath));
    }

    private School SchoolWith(params string[] classNames)
    {
        var school = new School(TestWeek.SixBySeven);
        foreach (var name in classNames)
            school.AddClass(name);
        return school;
    }

    [Fact]
    public void Save_KeepsThePreviousSaveAsBackup()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);
        SchoolStorage.Save(SchoolWith("5-A", "5-B"), FilePath);

        Assert.Equal(2, SchoolStorage.Load(FilePath).Classes.Count);
        Assert.Equal(1, SchoolStorage.Load(FilePath + ".bak").Classes.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"Version\": 3, \"Classes\": [\"5-A\"")] // cut off in the middle, as a power cut can leave it
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"Version\": \"three\"}")]
    [InlineData("{\"Version\": 3, \"Week\": {\"Days\": [], \"LessonsPerDay\": 8}}")]
    public void Load_DamagedFile_ThrowsInvalidData(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, content);

        Assert.Throws<InvalidDataException>(() => SchoolStorage.Load(FilePath));
    }

    [Fact]
    public void Load_UnreadableFile_IsNotReportedAsDamaged()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);

        using var lockedByAnotherProgram = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<IOException>(() => SchoolStorage.Load(FilePath));
    }

    [Fact]
    public void LoadWithRecovery_GoodFile_ChangesNothing()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);

        var result = SchoolStorage.LoadWithRecovery(FilePath);

        Assert.Equal(SchoolStorage.LoadOutcome.Loaded, result.Outcome);
        Assert.Null(result.BrokenFilePath);
        Assert.Equal("5-A", Assert.Single(result.School.Classes).Name);
    }

    [Fact]
    public void LoadWithRecovery_DamagedFile_RestoresTheBackup_AndKeepsTheDamagedFile()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);
        SchoolStorage.Save(SchoolWith("5-A", "5-B"), FilePath);
        File.WriteAllText(FilePath, ""); // what an unflushed save can leave after a power cut

        var result = SchoolStorage.LoadWithRecovery(FilePath);

        Assert.Equal(SchoolStorage.LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("5-A", Assert.Single(result.School.Classes).Name);
        Assert.True(File.Exists(result.BrokenFilePath));
        // The restored school is on disk again, so the next start does not find "no file" and begin empty.
        Assert.Equal("5-A", Assert.Single(SchoolStorage.Load(FilePath).Classes).Name);
    }

    [Fact]
    public void LoadWithRecovery_DamagedFile_NoBackup_StartsEmpty_AndKeepsTheDamagedFile()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "garbage");

        var result = SchoolStorage.LoadWithRecovery(FilePath);

        Assert.Equal(SchoolStorage.LoadOutcome.StartedEmpty, result.Outcome);
        Assert.Empty(result.School.Classes);
        Assert.Equal("garbage", File.ReadAllText(result.BrokenFilePath!));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void LoadWithRecovery_DamagedFileAndDamagedBackup_StartsEmpty()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "garbage");
        File.WriteAllText(FilePath + ".bak", "also garbage");

        var result = SchoolStorage.LoadWithRecovery(FilePath);

        Assert.Equal(SchoolStorage.LoadOutcome.StartedEmpty, result.Outcome);
    }

    [Fact]
    public void LoadWithRecovery_UnreadableFile_Throws_AndLeavesTheFileAlone()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);

        using (new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<IOException>(() => SchoolStorage.LoadWithRecovery(FilePath));

        Assert.Equal("5-A", Assert.Single(SchoolStorage.Load(FilePath).Classes).Name);
        Assert.Empty(Directory.GetFiles(_folder, "*.broken-*"));
    }

    [Fact]
    public void LoadWithRecovery_FileFromNewerVersion_Throws_AndLeavesTheFileAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{\"Version\": 999}");

        Assert.Throws<SchoolFileTooNewException>(() => SchoolStorage.LoadWithRecovery(FilePath));

        Assert.Equal("{\"Version\": 999}", File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(_folder, "*.broken-*"));
    }

    [Fact]
    public void Load_AssignmentPointingAtSomethingMissing_IsSkipped_NotTheWholeFile()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """
            {
              "Version": 3,
              "Week": { "Days": ["Dushanba", "Seshanba"], "LessonsPerDay": 6 },
              "Classes": ["5-A"],
              "Subjects": [{ "Name": "Matematika", "LessonsPerWeek": 5 }],
              "Teachers": [{ "Name": "Karimov", "DaysOff": [] }],
              "Assignments": [
                { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-A", "LessonsPerWeek": 5 },
                { "Teacher": "Nobody", "Subject": "Matematika", "Class": "5-A", "LessonsPerWeek": 5 },
                { "Teacher": "Karimov", "Subject": "Fizika", "Class": "5-A", "LessonsPerWeek": 2 },
                { "Teacher": "Karimov", "Subject": "Matematika", "Class": "9-Z", "LessonsPerWeek": 5 },
                { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-A", "LessonsPerWeek": 0 }
              ],
              "Lessons": []
            }
            """);

        var school = SchoolStorage.Load(FilePath);

        var assignment = Assert.Single(school.Assignments);
        Assert.Equal("Karimov", assignment.Teacher.Name);
    }

    [Fact]
    public void Load_OldFormat_KeepsACopyOfTheOldFile_AndDoesNotOverwriteIt()
    {
        Directory.CreateDirectory(_folder);
        const string oldFile = "{\"Version\": 2, \"Classes\": [\"5-A\"], \"Subjects\": [], \"Teachers\": [], \"Assignments\": [], \"Lessons\": []}";
        File.WriteAllText(FilePath, oldFile);

        SchoolStorage.Load(FilePath);
        SchoolStorage.Save(SchoolWith("changed"), FilePath);
        SchoolStorage.Load(FilePath); // current format now: no new copy

        Assert.Equal(oldFile, File.ReadAllText(FilePath + ".v2.bak"));
    }

    [Fact]
    public void Save_KeepsADailyCopyOfTheFileBeforeTheFirstSaveOfTheDay()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);
        SchoolStorage.Save(SchoolWith("5-A", "5-B"), FilePath);
        SchoolStorage.Save(SchoolWith("5-A", "5-B", "5-C"), FilePath);
        SchoolStorage.Save(SchoolWith("1"), FilePath); // .bak now holds 3 classes, but the daily copy still has 1

        var snapshot = Assert.Single(Directory.GetFiles(_folder, "school.json.daily-*.bak"));
        Assert.Equal("5-A", Assert.Single(SchoolStorage.Load(snapshot).Classes).Name);
    }

    [Fact]
    public void Save_KeepsOnlyTheNewestDailyCopies()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);
        for (var day = 1; day <= 20; day++)
            File.WriteAllText($"{FilePath}.daily-202001{day:00}.bak", "{}");

        SchoolStorage.Save(SchoolWith("5-B"), FilePath);

        var kept = Directory.GetFiles(_folder, "school.json.daily-*.bak");
        Assert.Equal(14, kept.Length);
        Assert.DoesNotContain(kept, f => f.EndsWith("20200101.bak"));
        Assert.Contains(kept, f => f.EndsWith("20200120.bak"));
    }

    [Fact]
    public void LoadWithRecovery_BackupDamaged_UsesADailyCopy()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);
        SchoolStorage.Save(SchoolWith("5-A", "5-B"), FilePath); // daily copy: 5-A
        File.WriteAllText(FilePath + ".bak", "garbage");
        File.WriteAllText(FilePath, "garbage");

        var result = SchoolStorage.LoadWithRecovery(FilePath);

        Assert.Equal(SchoolStorage.LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("5-A", Assert.Single(result.School.Classes).Name);
    }

    [Fact]
    public void LoadWithRecovery_DamagedFile_BackupFromNewerVersion_Throws_AndLeavesTheFileWhereItIs()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "garbage");
        File.WriteAllText(FilePath + ".bak", "{\"Version\": 999}");

        Assert.Throws<SchoolFileTooNewException>(() => SchoolStorage.LoadWithRecovery(FilePath));

        Assert.Equal("garbage", File.ReadAllText(FilePath));
        Assert.Empty(Directory.GetFiles(_folder, "*.broken-*"));
    }

    [Fact]
    public void LoadWithRecovery_TwoBreaksInTheSameSecond_DoNotCollide()
    {
        Directory.CreateDirectory(_folder);
        for (var i = 0; i < 2; i++)
        {
            File.WriteAllText(FilePath, "garbage " + i);
            SchoolStorage.LoadWithRecovery(FilePath);
        }

        Assert.Equal(2, Directory.GetFiles(_folder, "*.broken-*").Length);
    }

    private const string LessonFileStart = """
        {
          "Version": 3,
          "Week": { "Days": ["Dushanba", "Seshanba"], "LessonsPerDay": 3 },
          "Classes": ["5-A", "5-B"],
          "Subjects": [{ "Name": "Matematika", "LessonsPerWeek": 2 }],
          "Teachers": [{ "Name": "Karimov", "DaysOff": [1] }],
          "Assignments": [
            { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-A", "LessonsPerWeek": 2 },
            { "Teacher": "Karimov", "Subject": "Matematika", "Class": "5-B", "LessonsPerWeek": 2 }
          ],
        """;

    [Fact]
    public void Load_LessonsThatContradictThePlan_AreDropped_AndReported()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, LessonFileStart + """
              "Lessons": [
                { "Class": "5-A", "Day": 0, "Slot": 0, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-B", "Day": 0, "Slot": 0, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 1, "Slot": 0, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 0, "Slot": 0, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 0, "Slot": 1, "Subject": "Fizika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 0, "Slot": 5, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 0, "Slot": 1, "Subject": "Matematika", "Teacher": "Karimov" },
                { "Class": "5-A", "Day": 0, "Slot": 2, "Subject": "Matematika", "Teacher": "Karimov" }
              ]
            }
            """);

        var (school, warnings) = SchoolStorage.LoadDetailed(FilePath);

        // Kept: 5-A day 0 slots 0 and 1. Dropped: teacher already busy in 5-B, day off, the same slot twice,
        // no such assignment, slot outside the week, and a third lesson of a 2-lesson plan.
        var timetable = school.TimetableFor(school.Classes[0]);
        Assert.NotNull(timetable.Get(0, 0));
        Assert.NotNull(timetable.Get(0, 1));
        Assert.Null(timetable.Get(0, 2));
        Assert.Equal(2, school.LessonCount());
        Assert.Contains(warnings, w => w.StartsWith("6 ta dars"));
    }

    [Fact]
    public void Load_SkippedAssignment_IsReported()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, LessonFileStart.Replace("\"Class\": \"5-B\"", "\"Class\": \"9-Z\"") + "\"Lessons\": [] }");

        var (school, warnings) = SchoolStorage.LoadDetailed(FilePath);

        Assert.Single(school.Assignments);
        Assert.Contains(warnings, w => w.Contains("9-Z"));
    }

    [Fact]
    public void Load_CleanFile_HasNoWarnings()
    {
        SchoolStorage.Save(SchoolWith("5-A"), FilePath);

        Assert.Empty(SchoolStorage.LoadDetailed(FilePath).Warnings);
    }

    [Fact]
    public void Names_AreComparedTheSameWayOnEveryPc()
    {
        // Under a Turkish/Azeri culture "i".ToUpper() is "İ"; the school must not depend on that.
        var school = new School(TestWeek.SixBySeven);
        school.AddClass("5-i");

        var old = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Throws<SchoolRuleException>(() => school.AddClass("5-I"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = old;
        }
    }
}
