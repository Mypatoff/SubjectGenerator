using SubjectGenerator.Core;
using Xunit;

namespace SubjectGenerator.Tests;

public class ClassTimetableTests
{
    private static ClassTimetable NewTimetable() =>
        new(new SchoolClass("5-A"), TestWeek.SixBySeven);

    [Fact]
    public void NewTimetable_HasEveryScheduledSlotEmpty()
    {
        var timetable = NewTimetable();

        for (var day = 0; day < timetable.Layout.DayCount; day++)
            for (var slot = 0; slot < timetable.Layout.SlotsPerDay; slot++)
                Assert.Null(timetable.Get(day, slot));
    }

    [Fact]
    public void Set_StoresLessonOnlyInThatSlot()
    {
        var timetable = NewTimetable();
        var math = new Lesson("Matematika", "Karimov");

        timetable.Set(1, 2, math);

        Assert.Equal(math, timetable.Get(1, 2));
        Assert.Null(timetable.Get(1, 3));
        Assert.Null(timetable.Get(2, 2));
    }

    [Fact]
    public void Set_Null_EmptiesTheSlot()
    {
        var timetable = NewTimetable();
        timetable.Set(0, 0, new Lesson("Fizika", "Aliyev"));

        timetable.Set(0, 0, null);

        Assert.Null(timetable.Get(0, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(6, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 7)]
    public void OutOfRangePosition_Throws(int day, int slot)
    {
        var timetable = NewTimetable();

        Assert.Throws<ArgumentOutOfRangeException>(() => timetable.Get(day, slot));
        Assert.Throws<ArgumentOutOfRangeException>(() => timetable.Set(day, slot, null));
    }
}
