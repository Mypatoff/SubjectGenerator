namespace SubjectGenerator.Core;

public sealed class Teacher
{
    private readonly HashSet<int> _daysOff = new();

    internal Teacher(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>
    /// Days (0-based, like in <see cref="WeekLayout.DayNames"/>) when the teacher has no lessons.
    /// Change them through <see cref="School.SetDayOff"/>, which checks the day.
    /// </summary>
    public IReadOnlyCollection<int> DaysOff => _daysOff;

    public bool IsDayOff(int day) => _daysOff.Contains(day);

    internal void SetDayOff(int day, bool isOff)
    {
        if (isOff)
            _daysOff.Add(day);
        else
            _daysOff.Remove(day);
    }

    public override string ToString() => Name;
}
