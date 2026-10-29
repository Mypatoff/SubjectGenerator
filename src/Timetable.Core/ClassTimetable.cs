namespace Timetable.Core;

/// <summary>One class's week: a grid of slots (day x lesson number), each empty or holding a lesson.</summary>
public sealed class ClassTimetable
{
    private readonly Lesson?[,] _slots;

    public ClassTimetable(SchoolClass schoolClass, WeekLayout layout)
    {
        Class = schoolClass;
        Layout = layout;
        _slots = new Lesson?[layout.DayCount, layout.SlotsPerDay];
    }

    public SchoolClass Class { get; }

    public WeekLayout Layout { get; }

    /// <summary>The lesson in a slot, or null if the slot is empty. Day and slot are 0-based.</summary>
    public Lesson? Get(int day, int slot)
    {
        CheckPosition(day, slot);
        return _slots[day, slot];
    }

    /// <summary>Puts a lesson in a slot (null empties it). Day and slot are 0-based.</summary>
    public void Set(int day, int slot, Lesson? lesson)
    {
        CheckPosition(day, slot);
        _slots[day, slot] = lesson;
    }

    private void CheckPosition(int day, int slot)
    {
        if (day < 0 || day >= Layout.DayCount)
            throw new ArgumentOutOfRangeException(nameof(day));
        if (slot < 0 || slot >= Layout.SlotsPerDay)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
