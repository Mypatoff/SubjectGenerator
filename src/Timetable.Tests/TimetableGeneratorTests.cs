using System.Diagnostics;
using Timetable.Core;
using Xunit;

namespace Timetable.Tests;

public class TimetableGeneratorTests
{
    /// <summary>
    /// A school like the real one: 20 classes, 12 subjects (33 lessons a week per class),
    /// each subject split between a few teachers, and some teachers with a day off.
    /// </summary>
    private static School RealisticSchool()
    {
        var school = new School(TestWeek.SixBySeven);
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
            Assert.Equal(a.LessonsPerWeek, result.Placements.Count(p => p.Class == a.Class && p.Subject == a.Subject));
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
        var school = new School(TestWeek.SixBySeven);
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
        var school = new School(TestWeek.SixBySeven);
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
    public void OverloadedClass_DoesNotStopOtherClassesFromBeingPlaced()
    {
        // 5-A needs 45 lessons in a 42-slot week. That must not keep 5-B (and its shared teacher) from being placed.
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var english = school.AddSubject("Ingliz tili", 30);
        var russian = school.AddSubject("Rus tili", 27);
        var math = school.AddSubject("Matematika", 15);
        var karimov = school.AddTeacher("Karimov");
        school.Assign(school.AddTeacher("Aliyev"), english, a);
        school.Assign(karimov, math, a);
        school.Assign(school.AddTeacher("Rahimov"), russian, b);
        school.Assign(karimov, math, b);

        var watch = Stopwatch.StartNew();
        var result = TimetableGenerator.Generate(school, seed: 12);

        Assert.Equal(42, result.Placements.Count(p => p.Class == b));
        Assert.Equal(42, result.Placements.Count(p => p.Class == a));
        Assert.Equal(3, result.UnplacedLessons);
        Assert.Contains(result.Problems, p => p.Contains("5-A") && p.Contains("42"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"Took {watch.Elapsed}");
    }

    [Fact]
    public void SubjectOnlySomeClassesHave_IsNotAProblem()
    {
        // Like a college: Fizika is only in 5-A's plan. 5-B simply does not have it.
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        var physics = school.AddSubject("Fizika", 2);
        school.Assign(school.AddTeacher("Karimov"), math, a);
        school.Assign(school.AddTeacher("Rahimov"), math, b);
        school.Assign(school.AddTeacher("Aliyev"), physics, a);

        var result = TimetableGenerator.Generate(school, seed: 9);

        Assert.Equal(12, result.Placements.Count);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void ClassWithNoSubjects_IsReported()
    {
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("5-A");
        school.AddClass("5-B");
        school.Assign(school.AddTeacher("Karimov"), school.AddSubject("Matematika", 5), a);

        var result = TimetableGenerator.Generate(school, seed: 9);

        Assert.Contains(result.Problems, p => p.Contains("5-B"));
    }

    [Fact]
    public void EachClass_GetsItsOwnNumberOfLessons()
    {
        var school = new School(TestWeek.SixBySeven);
        var a = school.AddClass("40");
        var b = school.AddClass("43");
        var english = school.AddSubject("Ingliz tili", 10);
        var qashiboev = school.AddTeacher("Qashiboev");
        school.Assign(qashiboev, english, a);
        school.Assign(qashiboev, english, b, lessonsPerWeek: 2);

        var result = TimetableGenerator.Generate(school, seed: 13);

        Assert.Equal(10, result.Placements.Count(p => p.Class == a));
        Assert.Equal(2, result.Placements.Count(p => p.Class == b));
    }

    [Fact]
    public void LongerDays_FitMoreLessons()
    {
        // 47 lessons do not fit in 6 x 7 = 42, but do in 6 x 8 = 48.
        var eightADay = new WeekLayout(WeekLayout.AllDayNames.Take(6).ToArray(), 8);
        var school = new School(eightADay);
        var group = school.AddClass("44");
        school.Assign(school.AddTeacher("Qashiboev"), school.AddSubject("Ingliz tili", 10), group);
        school.Assign(school.AddTeacher("Muxtorov"), school.AddSubject("Qishloq xo'jaligi mashinalari", 37), group);

        var result = TimetableGenerator.Generate(school, seed: 14);

        Assert.Equal(0, result.UnplacedLessons);
        Assert.Equal(47, result.Placements.Count);
        // 47 over 6 days: some days have 8 lessons, others fewer; never more than 8.
        Assert.All(result.Placements, p => Assert.InRange(p.Slot, 0, 7));
    }

    [Fact]
    public void NothingAssigned_IsReported()
    {
        var school = new School(TestWeek.SixBySeven);
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
