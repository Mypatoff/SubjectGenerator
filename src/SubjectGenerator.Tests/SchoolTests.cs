using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class SchoolTests
{
    [Fact]
    public void Sample_HasTwentyUniquelyNamedClasses()
    {
        var school = School.CreateSample();

        Assert.Equal(20, school.Classes.Count);
        Assert.Equal(20, school.Classes.Select(c => c.Name).Distinct().Count());
    }

    [Fact]
    public void TimetableFor_ReturnsTheSameTimetableEachTime()
    {
        var school = School.CreateSample();
        var schoolClass = school.Classes[3];

        var first = school.TimetableFor(schoolClass);
        first.Set(0, 0, new Lesson("Ona tili", "Yusupova"));

        Assert.Same(first, school.TimetableFor(schoolClass));
        Assert.NotNull(school.TimetableFor(schoolClass).Get(0, 0));
    }

    [Fact]
    public void TimetableFor_KeepsClassesApart()
    {
        var school = School.CreateSample();

        school.TimetableFor(school.Classes[0]).Set(0, 0, new Lesson("Tarix", "Rahimov"));

        Assert.Null(school.TimetableFor(school.Classes[1]).Get(0, 0));
    }

    [Fact]
    public void TimetableFor_UnknownClass_Throws()
    {
        var school = School.CreateSample();

        Assert.Throws<ArgumentException>(() => school.TimetableFor(new SchoolClass("99-Z")));
    }

    [Fact]
    public void Layout_RejectsEmptyWeek()
    {
        Assert.Throws<ArgumentException>(() => new WeekLayout(Array.Empty<string>(), 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeekLayout(new[] { "Dushanba" }, 0));
    }
}
