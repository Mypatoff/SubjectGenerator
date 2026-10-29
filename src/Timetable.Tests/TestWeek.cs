using Timetable.Core;

namespace Timetable.Tests;

/// <summary>Weeks used by the tests, so they do not change when the app's default week changes.</summary>
internal static class TestWeek
{
    /// <summary>Monday to Saturday, 7 lessons a day: 42 slots.</summary>
    public static WeekLayout SixBySeven { get; } = new(WeekLayout.AllDayNames.Take(6).ToArray(), 7);
}
