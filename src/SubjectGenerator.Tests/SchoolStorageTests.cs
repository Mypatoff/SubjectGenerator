using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public sealed class SchoolStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SubjectGeneratorTests-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_folder, "school.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
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
              "Lessons": [{ "Class": "5-A", "Day": 5, "Slot": 6, "Subject": "Matematika", "Teacher": "Karimov" }]
            }
            """);

        var school = SchoolStorage.Load(FilePath);

        Assert.Equal(6, school.Layout.DayCount);
        Assert.Equal(7, school.Layout.SlotsPerDay);
        Assert.All(school.Assignments, a => Assert.Equal(5, a.LessonsPerWeek));
        Assert.True(school.Teachers[0].IsDayOff(5));
        Assert.Equal("Matematika", school.TimetableFor(school.Classes[0]).Get(5, 6)?.Subject);
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

        Assert.Throws<InvalidDataException>(() => SchoolStorage.Load(FilePath));
    }
}
