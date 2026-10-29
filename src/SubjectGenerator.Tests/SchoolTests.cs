using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class SchoolTests
{
    private static School NewSchool() => new(WeekLayout.Default);

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
    [InlineData(43)] // the default week has 6 x 7 = 42 slots
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
}
