using System.Diagnostics;
using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class TimetableGeneratorTests
{
    /// <summary>
    /// A school like the real one: 20 classes, 12 subjects (33 lessons a week per class),
    /// each subject split between a few teachers, and some teachers with a day off.
    /// </summary>
    private static School RealisticSchool()
    {
        var school = new School(WeekLayout.Default);
        for (var grade = 1; grade <= 10; grade++)
        {
            school.AddClass($"{grade}-A");
            school.AddClass($"{grade}-B");
        }

        var subjects = new (string Name, int PerWeek, int Teachers)[]
        {
            ("Ona tili", 5, 4), ("Matematika", 6, 4), ("Adabiyot", 3, 3), ("Ingliz tili", 3, 3),
            ("Rus tili", 2, 2), ("Tarix", 2, 2), ("Geografiya", 2, 2), ("Biologiya", 2, 2),
            ("Fizika", 2, 2), ("Kimyo", 2, 2), ("Informatika", 2, 2), ("Jismoniy tarbiya", 2, 2)
        };

        foreach (var (name, perWeek, teacherCount) in subjects)
        {
            var subject = school.AddSubject(name, perWeek);
            var teachers = Enumerable.Range(1, teacherCount).Select(i => school.AddTeacher($"{name} {i}")).ToList();
            for (var c = 0; c < school.Classes.Count; c++)
                school.Assign(teachers[c % teacherCount], subject, school.Classes[c]);
        }

        school.SetDayOff(school.Teachers.First(t => t.Name == "Fizika 1"), 5, true);       // Saturday
        school.SetDayOff(school.Teachers.First(t => t.Name == "Tarix 2"), 0, true);        // Monday
        school.SetDayOff(school.Teachers.First(t => t.Name == "Ingliz tili 1"), 2, true);  // Wednesday
        return school;
    }

    [Fact]
    public void RealisticSchool_EveryLessonIsPlaced()
    {
        var school = RealisticSchool();

        var result = TimetableGenerator.Generate(school, seed: 1);

        Assert.Equal(0, result.UnplacedLessons);
        Assert.Empty(result.Problems);
        Assert.Equal(20 * 33, result.Placements.Count);
        foreach (var a in school.Assignments)
            Assert.Equal(a.Subject.LessonsPerWeek, result.Placements.Count(p => p.Class == a.Class && p.Subject == a.Subject));
    }

    [Fact]
    public void RealisticSchool_TeacherIsNeverInTwoClassesAtOnce()
    {
        var result = TimetableGenerator.Generate(RealisticSchool(), seed: 2);

        var clashes = result.Placements.GroupBy(p => (p.Teacher, p.Day, p.Slot)).Where(g => g.Count() > 1);
        Assert.Empty(clashes);
    }

    [Fact]
    public void RealisticSchool_ClassHasOneLessonPerSlot()
    {
        var result = TimetableGenerator.Generate(RealisticSchool(), seed: 3);

        Assert.Empty(result.Placements.GroupBy(p => (p.Class, p.Day, p.Slot)).Where(g => g.Count() > 1));
    }

    [Fact]
    public void RealisticSchool_DaysOffAreRespected()
    {
        var school = RealisticSchool();

        var result = TimetableGenerator.Generate(school, seed: 4);

        Assert.DoesNotContain(result.Placements, p => p.Teacher.IsDayOff(p.Day));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(5000)]
    [InlineData(50000)]
    public void RealisticSchool_DaysHaveNoGapsAndAreEven(int seed)
    {
        var result = TimetableGenerator.Generate(RealisticSchool(), seed);
        Assert.Equal(0, result.UnplacedLessons);

        foreach (var byClass in result.Placements.GroupBy(p => p.Class))
        {
            for (var day = 0; day < 6; day++)
            {
                var slots = byClass.Where(p => p.Day == day).Select(p => p.Slot).OrderBy(s => s).ToList();
                // Starts at the 1st lesson and has no holes: 0, 1, 2, ...
                Assert.Equal(Enumerable.Range(0, slots.Count), slots);
                // 33 lessons over 6 days: 5 or 6 a day.
                Assert.InRange(slots.Count, 5, 6);
            }
        }
    }

    [Fact]
    public void RealisticSchool_IsFast()
    {
        var watch = Stopwatch.StartNew();

        TimetableGenerator.Generate(RealisticSchool(), seed: 6);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Took {watch.Elapsed}");
    }

    [Fact]
    public void TeacherWithFullWeek_GetsEverySlotOnce()
    {
        // One teacher, 6 classes x 7 lessons = 42 = every slot of the week. Only one way to fit:
        // the teacher is busy every slot, and no two classes share one.
        var school = new School(WeekLayout.Default);
        var math = school.AddSubject("Matematika", 7);
        var teacher = school.AddTeacher("Karimov");
        for (var i = 1; i <= 6; i++)
            school.Assign(teacher, math, school.AddClass($"{i}-A"));

        var result = TimetableGenerator.Generate(school, seed: 7);

        Assert.Equal(0, result.UnplacedLessons);
        Assert.Equal(42, result.Placements.Select(p => (p.Day, p.Slot)).Distinct().Count());
    }

    [Fact]
    public void OverloadedTeacher_IsReported()
    {
        // 8 lessons a day are needed on 6 days, but a teacher with 2 days off has only 4 x 7 = 28 slots.
        var school = new School(WeekLayout.Default);
        var math = school.AddSubject("Matematika", 6);
        var teacher = school.AddTeacher("Karimov");
        school.SetDayOff(teacher, 0, true);
        school.SetDayOff(teacher, 1, true);
        for (var i = 1; i <= 5; i++)
            school.Assign(teacher, math, school.AddClass($"{i}-A"));

        var result = TimetableGenerator.Generate(school, seed: 8);

        Assert.Equal(30 - 28, result.UnplacedLessons);
        Assert.Contains(result.Problems, p => p.Contains("Karimov"));
        Assert.DoesNotContain(result.Placements, p => p.Day is 0 or 1);
    }

    [Fact]
    public void SubjectWithoutTeacher_IsReportedAndSkipped()
    {
        var school = new School(WeekLayout.Default);
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        school.AddSubject("Fizika", 2);
        school.Assign(school.AddTeacher("Karimov"), math, a);
        school.Assign(school.AddTeacher("Aliyev"), math, b);

        var result = TimetableGenerator.Generate(school, seed: 9);

        Assert.Equal(10, result.Placements.Count);
        Assert.Contains(result.Problems, p => p.Contains("Fizika") && p.Contains("5-A") && p.Contains("5-B"));
    }

    [Fact]
    public void NothingAssigned_IsReported()
    {
        var school = new School(WeekLayout.Default);
        school.AddClass("5-A");

        var result = TimetableGenerator.Generate(school, seed: 10);

        Assert.Empty(result.Placements);
        Assert.NotEmpty(result.Problems);
    }

    [Fact]
    public void ApplyTo_ReplacesOldTimetables()
    {
        var school = RealisticSchool();
        var firstClass = school.Classes[0];
        school.TimetableFor(firstClass).Set(5, 6, new Lesson("Eski", "Eski"));

        var result = TimetableGenerator.Generate(school, seed: 11);
        result.ApplyTo(school);

        var placement = result.Placements.First(p => p.Class == firstClass);
        Assert.Equal(new Lesson(placement.Subject.Name, placement.Teacher.Name),
            school.TimetableFor(firstClass).Get(placement.Day, placement.Slot));
        Assert.NotEqual("Eski", school.TimetableFor(firstClass).Get(5, 6)?.Subject);
    }
}
