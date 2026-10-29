namespace SubjectGenerator.Core;

/// <summary>The shape of a school week: which days there are and how many lesson slots each day has.</summary>
public sealed class WeekLayout
{
    public WeekLayout(IReadOnlyList<string> dayNames, int slotsPerDay)
    {
        if (dayNames.Count == 0)
            throw new ArgumentException("A week needs at least one day.", nameof(dayNames));
        if (slotsPerDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(slotsPerDay), "A day needs at least one slot.");

        DayNames = dayNames;
        SlotsPerDay = slotsPerDay;
    }

    public IReadOnlyList<string> DayNames { get; }

    public int SlotsPerDay { get; }

    public int DayCount => DayNames.Count;

    /// <summary>Monday to Saturday, 7 lessons a day. Change the numbers here to change every timetable.</summary>
    public static WeekLayout Default { get; } = new(
        new[] { "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba" },
        slotsPerDay: 7);
}
