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
        var school = SchoolStorage.Load(FilePath, WeekLayout.Default);

        Assert.Empty(school.Classes);
        Assert.Empty(school.Subjects);
    }

    [Fact]
    public void SaveThenLoad_KeepsEverything()
    {
        var school = new School(WeekLayout.Default);
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        school.AddSubject("Fizika", 2);
        var karimov = school.AddTeacher("Karimov");
        school.AddTeacher("Aliyev");
        school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);

        SchoolStorage.Save(school, FilePath);
        var loaded = SchoolStorage.Load(FilePath, WeekLayout.Default);

        Assert.Equal(new[] { "5-A", "5-B" }, loaded.Classes.Select(c => c.Name));
        Assert.Equal(new[] { "Matematika:5", "Fizika:2" }, loaded.Subjects.Select(s => $"{s.Name}:{s.LessonsPerWeek}"));
        Assert.Equal(new[] { "Karimov", "Aliyev" }, loaded.Teachers.Select(t => t.Name));
        Assert.Equal(
            new[] { "Karimov Matematika 5-A", "Karimov Matematika 5-B" },
            loaded.Assignments.Select(x => $"{x.Teacher.Name} {x.Subject.Name} {x.Class.Name}"));
    }

    [Fact]
    public void Save_Twice_OverwritesTheFile()
    {
        var school = new School(WeekLayout.Default);
        school.AddClass("5-A");
        SchoolStorage.Save(school, FilePath);

        school.AddClass("6-A");
        SchoolStorage.Save(school, FilePath);

        Assert.Equal(2, SchoolStorage.Load(FilePath, WeekLayout.Default).Classes.Count);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Load_FileFromNewerVersion_Throws()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{\"Version\": 999}");

        Assert.Throws<InvalidDataException>(() => SchoolStorage.Load(FilePath, WeekLayout.Default));
    }
}
