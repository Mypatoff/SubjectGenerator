namespace Timetable.Core;

/// <summary>The shape of a school week: which days there are and how many lesson slots each day has.</summary>
public sealed class WeekLayout
{
    /// <summary>Every day a school week can use, in order. The user picks which of these the school works.</summary>
    public static IReadOnlyList<string> AllDayNames { get; } =
        new[] { "Dushanba", "Seshanba", "Chorshanba", "Payshanba", "Juma", "Shanba", "Yakshanba" };

    /// <summary>The most lessons a day can be set to.</summary>
    public const int MaxSlotsPerDay = 12;

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

    /// <summary>Lessons in one day. Every day has the same number of slots; a class may still use fewer of them.</summary>
    public int SlotsPerDay { get; }

    public int DayCount => DayNames.Count;

    public int SlotCount => DayCount * SlotsPerDay;

    /// <summary>Monday to Saturday, 8 lessons a day. The user can change it in Settings > Hafta.</summary>
    public static WeekLayout Default { get; } = new(AllDayNames.Take(6).ToArray(), slotsPerDay: 8);
}
