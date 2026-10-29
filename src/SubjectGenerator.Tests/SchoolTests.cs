using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class SchoolTests
{
    private static School NewSchool() => new(TestWeek.SixBySeven);

    [Fact]
    public void NewSchool_IsEmpty()
    {
        var school = NewSchool();

        Assert.Empty(school.Classes);
        Assert.Empty(school.Subjects);
        Assert.Empty(school.Teachers);
        Assert.Empty(school.Assignments);
    }

    [Fact]
    public void AddClass_TrimsTheName()
    {
        var school = NewSchool();

        var schoolClass = school.AddClass("  5-A ");

        Assert.Equal("5-A", schoolClass.Name);
        Assert.Same(schoolClass, Assert.Single(school.Classes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyNames_AreRejected(string name)
    {
        var school = NewSchool();

        Assert.Throws<SchoolRuleException>(() => school.AddClass(name));
        Assert.Throws<SchoolRuleException>(() => school.AddSubject(name, 2));
        Assert.Throws<SchoolRuleException>(() => school.AddTeacher(name));
    }

    [Fact]
    public void DuplicateNames_AreRejected_IgnoringCase()
    {
        var school = NewSchool();
        school.AddClass("5-A");
        school.AddSubject("Matematika", 5);
        school.AddTeacher("Karimov");

        Assert.Throws<SchoolRuleException>(() => school.AddClass("5-a"));
        Assert.Throws<SchoolRuleException>(() => school.AddSubject("MATEMATIKA", 3));
        Assert.Throws<SchoolRuleException>(() => school.AddTeacher("karimov"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(43)] // the test week has 6 x 7 = 42 slots
    public void LessonsPerWeek_MustFitInTheWeek(int lessons)
    {
        var school = NewSchool();
        var subject = school.AddSubject("Fizika", 2);

        Assert.Throws<SchoolRuleException>(() => school.AddSubject("Kimyo", lessons));
        Assert.Throws<SchoolRuleException>(() => school.SetLessonsPerWeek(subject, lessons));
        Assert.Equal(2, subject.LessonsPerWeek);
    }

    [Fact]
    public void SetLessonsPerWeek_ChangesTheNumber()
    {
        var school = NewSchool();
        var subject = school.AddSubject("Fizika", 2);

        school.SetLessonsPerWeek(subject, 4);

        Assert.Equal(4, subject.LessonsPerWeek);
    }

    [Fact]
    public void Assign_UsesTheSubjectsUsualNumber_UnlessGivenOne()
    {
        var school = NewSchool();
        var english = school.AddSubject("Ingliz tili", 10);
        var teacher = school.AddTeacher("Qashiboev");

        var usual = school.Assign(teacher, english, school.AddClass("40"));
        var own = school.Assign(teacher, english, school.AddClass("43"), lessonsPerWeek: 2);

        Assert.Equal(10, usual.LessonsPerWeek);
        Assert.Equal(2, own.LessonsPerWeek);
        Assert.Equal(12, school.LessonsPerWeekOf(teacher));
    }

    [Fact]
    public void SetLessonsPerWeek_ForOneClass_ChangesOnlyThatClass()
    {
        var school = NewSchool();
        var english = school.AddSubject("Ingliz tili", 10);
        var teacher = school.AddTeacher("Qashiboev");
        var a = school.Assign(teacher, english, school.AddClass("40"));
        var b = school.Assign(teacher, english, school.AddClass("41"));

        school.SetLessonsPerWeek(b, 3);

        Assert.Equal(10, a.LessonsPerWeek);
        Assert.Equal(3, b.LessonsPerWeek);
        Assert.Throws<SchoolRuleException>(() => school.SetLessonsPerWeek(b, 0));
        Assert.Throws<SchoolRuleException>(() => school.SetLessonsPerWeek(b, 43));
    }

    [Fact]
    public void ChangingTheUsualNumber_UpdatesClassesThatHadIt_ButNotOwnNumbers()
    {
        var school = NewSchool();
        var english = school.AddSubject("Ingliz tili", 10);
        var teacher = school.AddTeacher("Qashiboev");
        var usual = school.Assign(teacher, english, school.AddClass("40"));
        var own = school.Assign(teacher, english, school.AddClass("43"), lessonsPerWeek: 2);

        school.SetLessonsPerWeek(english, 8);

        Assert.Equal(8, usual.LessonsPerWeek);
        Assert.Equal(2, own.LessonsPerWeek);
    }

    [Fact]
    public void LessonsPerWeekOf_Class_AddsUpItsPlan()
    {
        var school = NewSchool();
        var group = school.AddClass("44");
        school.Assign(school.AddTeacher("A"), school.AddSubject("Ingliz tili", 10), group);
        school.Assign(school.AddTeacher("B"), school.AddSubject("Matematika", 2), group);

        Assert.Equal(12, school.LessonsPerWeekOf(group));
        Assert.Equal(2, school.AssignmentsOf(group).Count());
    }

    [Fact]
    public void ChangeWeek_ChangesDaysAndSlots_AndEmptiesTimetables()
    {
        var school = NewSchool();
        var group = school.AddClass("5-A");
        school.TimetableFor(group).Set(0, 0, new Lesson("Tarix", "Rahimov"));

        school.ChangeWeek(new WeekLayout(WeekLayout.AllDayNames.Take(5).ToArray(), 8));

        Assert.Equal(5, school.Layout.DayCount);
        Assert.Equal(8, school.Layout.SlotsPerDay);
        Assert.Equal(40, school.MaxLessonsPerWeek);
        Assert.Null(school.TimetableFor(group).Get(0, 0));
        Assert.Equal(8, school.TimetableFor(group).Layout.SlotsPerDay);
    }

    [Fact]
    public void HasAnyLessons_IsTrueOnlyWhenSomethingIsScheduled()
    {
        var school = NewSchool();
        var group = school.AddClass("5-A");
        Assert.False(school.HasAnyLessons());
        school.TimetableFor(group); // an empty timetable does not count
        Assert.False(school.HasAnyLessons());

        school.TimetableFor(group).Set(3, 2, new Lesson("Tarix", "Rahimov"));

        Assert.True(school.HasAnyLessons());
    }

    [Fact]
    public void ChangeWeek_KeepsDaysOffByDayName()
    {
        var school = NewSchool(); // Monday..Saturday
        var teacher = school.AddTeacher("Karimov");
        school.SetDayOff(teacher, 1, true); // Seshanba
        school.SetDayOff(teacher, 5, true); // Shanba

        // Drop Monday and Saturday: Seshanba becomes day 0, Shanba is gone.
        school.ChangeWeek(new WeekLayout(new[] { "Seshanba", "Chorshanba", "Payshanba", "Juma" }, 7));

        Assert.Equal(new[] { 0 }, teacher.DaysOff);
    }

    [Fact]
    public void ChangeWeek_RefusesAWeekTooShortForAClass()
    {
        var school = NewSchool();
        var assignment = school.Assign(school.AddTeacher("A"), school.AddSubject("Ingliz tili", 20), school.AddClass("40"));

        Assert.Throws<SchoolRuleException>(() =>
            school.ChangeWeek(new WeekLayout(WeekLayout.AllDayNames.Take(3).ToArray(), 6))); // 18 slots

        Assert.Equal(6, school.Layout.DayCount);
        Assert.Equal(20, assignment.LessonsPerWeek);
    }

    [Fact]
    public void SetDayOff_TurnsOnAndOff()
    {
        var school = NewSchool();
        var teacher = school.AddTeacher("Karimov");

        school.SetDayOff(teacher, 2, true);
        Assert.True(teacher.IsDayOff(2));
        Assert.False(teacher.IsDayOff(3));

        school.SetDayOff(teacher, 2, false);
        Assert.Empty(teacher.DaysOff);
    }

    [Fact]
    public void SetDayOff_TeacherMustWorkAtLeastOneDay()
    {
        var school = NewSchool();
        var teacher = school.AddTeacher("Karimov");
        for (var day = 0; day < 5; day++)
            school.SetDayOff(teacher, day, true);

        Assert.Throws<SchoolRuleException>(() => school.SetDayOff(teacher, 5, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => school.SetDayOff(teacher, 6, true));
    }

    [Fact]
    public void Teacher_CanTeachManyClassesAndSubjects()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        var physics = school.AddSubject("Fizika", 2);
        var karimov = school.AddTeacher("Karimov");

        school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);
        school.Assign(karimov, physics, a);

        Assert.Equal(3, school.AssignmentsOf(karimov).Count());
        Assert.Equal(karimov, school.TeacherOf(math, b));
        Assert.Null(school.TeacherOf(physics, b));
    }

    [Fact]
    public void Assign_SameThingTwice_DoesNotDuplicate()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var math = school.AddSubject("Matematika", 5);
        var karimov = school.AddTeacher("Karimov");

        school.Assign(karimov, math, a);
        school.Assign(karimov, math, a);

        Assert.Single(school.Assignments);
    }

    [Fact]
    public void Assign_SecondTeacherForSameSubjectAndClass_IsRejected()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var math = school.AddSubject("Matematika", 5);
        var karimov = school.AddTeacher("Karimov");
        var aliyev = school.AddTeacher("Aliyev");
        school.Assign(karimov, math, a);

        Assert.Throws<SchoolRuleException>(() => school.Assign(aliyev, math, a));
        Assert.Equal(karimov, school.TeacherOf(math, a));
    }

    [Fact]
    public void Assign_SomethingFromAnotherSchool_Throws()
    {
        var school = NewSchool();
        var math = school.AddSubject("Matematika", 5);
        var karimov = school.AddTeacher("Karimov");

        Assert.Throws<ArgumentException>(() => school.Assign(karimov, math, new SchoolClass("9-Z")));
    }

    [Fact]
    public void Removing_AnythingAlsoRemovesItsAssignments()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        var physics = school.AddSubject("Fizika", 2);
        var karimov = school.AddTeacher("Karimov");
        var aliyev = school.AddTeacher("Aliyev");
        school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);
        school.Assign(aliyev, physics, a);
        school.Assign(aliyev, physics, b);

        school.RemoveClass(b);
        Assert.Equal(2, school.Assignments.Count);

        school.RemoveSubject(physics);
        Assert.Equal(karimov, Assert.Single(school.Assignments).Teacher);

        school.RemoveTeacher(karimov);
        Assert.Empty(school.Assignments);
        Assert.Equal(new[] { aliyev }, school.Teachers);
    }

    [Fact]
    public void Unassign_RemovesOnlyThatAssignment()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 5);
        var karimov = school.AddTeacher("Karimov");
        var first = school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);

        school.Unassign(first);

        Assert.Null(school.TeacherOf(math, a));
        Assert.Equal(karimov, school.TeacherOf(math, b));
    }

    [Fact]
    public void TimetableFor_ReturnsTheSameTimetableEachTime()
    {
        var school = NewSchool();
        var schoolClass = school.AddClass("5-A");

        var first = school.TimetableFor(schoolClass);
        first.Set(0, 0, new Lesson("Ona tili", "Yusupova"));

        Assert.Same(first, school.TimetableFor(schoolClass));
        Assert.NotNull(school.TimetableFor(schoolClass).Get(0, 0));
    }

    [Fact]
    public void TimetableFor_KeepsClassesApart()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");

        school.TimetableFor(a).Set(0, 0, new Lesson("Tarix", "Rahimov"));

        Assert.Null(school.TimetableFor(b).Get(0, 0));
    }

    [Fact]
    public void TimetableFor_UnknownOrRemovedClass_Throws()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        school.RemoveClass(a);

        Assert.Throws<ArgumentException>(() => school.TimetableFor(new SchoolClass("99-Z")));
        Assert.Throws<ArgumentException>(() => school.TimetableFor(a));
    }

    [Fact]
    public void Layout_RejectsEmptyWeek()
    {
        Assert.Throws<ArgumentException>(() => new WeekLayout(Array.Empty<string>(), 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WeekLayout(new[] { "Dushanba" }, 0));
    }

    // ----- A timetable never contradicts the plan -----

    /// <summary>5-A has Matematika (Karimov) twice on Monday and once on Tuesday; Fizika (Aliyev) on Monday; 5-B has Matematika (Karimov) on Monday too.</summary>
    private static (School School, SchoolClass A, SchoolClass B, Subject Math, Subject Physics, Teacher Karimov, Teacher Aliyev) ScheduledSchool()
    {
        var school = NewSchool();
        var a = school.AddClass("5-A");
        var b = school.AddClass("5-B");
        var math = school.AddSubject("Matematika", 3);
        var physics = school.AddSubject("Fizika", 1);
        var karimov = school.AddTeacher("Karimov");
        var aliyev = school.AddTeacher("Aliyev");
        school.Assign(karimov, math, a);
        school.Assign(karimov, math, b);
        school.Assign(aliyev, physics, a);

        school.TimetableFor(a).Set(0, 0, new Lesson("Matematika", "Karimov"));
        school.TimetableFor(a).Set(0, 1, new Lesson("Matematika", "Karimov"));
        school.TimetableFor(a).Set(1, 0, new Lesson("Matematika", "Karimov"));
        school.TimetableFor(a).Set(0, 2, new Lesson("Fizika", "Aliyev"));
        school.TimetableFor(b).Set(0, 3, new Lesson("Matematika", "Karimov"));
        return (school, a, b, math, physics, karimov, aliyev);
    }

    [Fact]
    public void RemoveTeacher_RemovesTheirLessonsFromEveryTimetable()
    {
        var (school, a, b, _, _, karimov, _) = ScheduledSchool();

        school.RemoveTeacher(karimov);

        Assert.Equal(1, school.LessonCount());
        Assert.Equal("Fizika", school.TimetableFor(a).Get(0, 2)?.Subject);
        Assert.Null(school.TimetableFor(b).Get(0, 3));
    }

    [Fact]
    public void RemovingATeacher_AndAddingTheSameNameAgain_DoesNotBringTheOldLessonsBack()
    {
        var (school, a, _, _, _, karimov, _) = ScheduledSchool();

        school.RemoveTeacher(karimov);
        school.AddTeacher("Karimov");

        Assert.Null(school.TimetableFor(a).Get(0, 0));
    }

    [Fact]
    public void RemoveSubject_RemovesItsLessonsFromEveryTimetable()
    {
        var (school, a, b, math, _, _, _) = ScheduledSchool();

        school.RemoveSubject(math);

        Assert.Equal(1, school.LessonCount());
        Assert.Null(school.TimetableFor(b).Get(0, 3));
        Assert.Equal("Fizika", school.TimetableFor(a).Get(0, 2)?.Subject);
    }

    [Fact]
    public void Unassign_RemovesOnlyThatClassesLessonsOfThatSubject()
    {
        var (school, a, b, math, _, karimov, _) = ScheduledSchool();

        school.Unassign(school.Assignments.First(x => x.Class == a && x.Subject == math));

        Assert.Equal(2, school.LessonCount());
        Assert.Equal("Matematika", school.TimetableFor(b).Get(0, 3)?.Subject);
        Assert.Equal("Fizika", school.TimetableFor(a).Get(0, 2)?.Subject);
    }

    [Fact]
    public void SetDayOff_RemovesThatTeachersLessonsOnThatDayOnly()
    {
        var (school, a, b, _, _, karimov, _) = ScheduledSchool();

        school.SetDayOff(karimov, 0, true);

        Assert.Equal("Matematika", school.TimetableFor(a).Get(1, 0)?.Subject); // Tuesday stays
        Assert.Equal("Fizika", school.TimetableFor(a).Get(0, 2)?.Subject);     // another teacher stays
        Assert.Null(school.TimetableFor(a).Get(0, 0));
        Assert.Null(school.TimetableFor(a).Get(0, 1));
        Assert.Null(school.TimetableFor(b).Get(0, 3));
    }

    [Fact]
    public void SetDayOff_BackToWorking_RemovesNothing()
    {
        var (school, _, _, _, _, karimov, _) = ScheduledSchool();
        school.SetDayOff(karimov, 3, true); // no lessons on Thursday

        school.SetDayOff(karimov, 3, false);

        Assert.Equal(5, school.LessonCount());
    }

    [Fact]
    public void SetLessonsPerWeek_ForOneClass_RemovesThatClassesLessonsOfThatSubject()
    {
        var (school, a, b, math, _, _, _) = ScheduledSchool();

        school.SetLessonsPerWeek(school.Assignments.First(x => x.Class == a && x.Subject == math), 2);

        Assert.Equal(2, school.LessonCount());
        Assert.Equal("Matematika", school.TimetableFor(b).Get(0, 3)?.Subject);
    }

    [Fact]
    public void SetLessonsPerWeek_ToTheSameNumber_RemovesNothing()
    {
        var (school, a, _, math, _, _, _) = ScheduledSchool();

        school.SetLessonsPerWeek(school.Assignments.First(x => x.Class == a && x.Subject == math), 3);

        Assert.Equal(5, school.LessonCount());
    }

    [Fact]
    public void SetLessonsPerWeek_ForTheSubject_RemovesLessonsOfClassesThatFollowedIt()
    {
        var (school, a, b, math, _, _, _) = ScheduledSchool();
        school.SetLessonsPerWeek(school.Assignments.First(x => x.Class == b && x.Subject == math), 2); // 5-B has its own number

        school.SetLessonsPerWeek(math, 4); // 5-A followed the usual 3, so it changes; 5-B keeps 2

        Assert.Null(school.TimetableFor(a).Get(0, 0));
        Assert.Equal("Fizika", school.TimetableFor(a).Get(0, 2)?.Subject);
    }

    [Fact]
    public void Assign_ExistingWithAnotherNumber_RemovesItsLessons()
    {
        var (school, a, _, math, _, karimov, _) = ScheduledSchool();

        school.Assign(karimov, math, a, lessonsPerWeek: 1);

        Assert.Null(school.TimetableFor(a).Get(0, 0));
        Assert.Equal(2, school.LessonCount());
    }

    [Fact]
    public void LessonCount_AddsUpAllTimetables()
    {
        var (school, _, _, _, _, _, _) = ScheduledSchool();

        Assert.Equal(5, school.LessonCount());
        Assert.Equal(0, NewSchool().LessonCount());
    }
}
