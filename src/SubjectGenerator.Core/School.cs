namespace SubjectGenerator.Core;

/// <summary>Everything the school has: its classes, the shape of the week, and each class's timetable.</summary>
public sealed class School
{
    private readonly Dictionary<SchoolClass, ClassTimetable> _timetables = new();

    public School(IEnumerable<SchoolClass> classes, WeekLayout layout)
    {
        Classes = classes.ToList();
        Layout = layout;
    }

    public IReadOnlyList<SchoolClass> Classes { get; }

    public WeekLayout Layout { get; }

    /// <summary>The class's timetable; every class starts with an empty week.</summary>
    public ClassTimetable TimetableFor(SchoolClass schoolClass)
    {
        if (!Classes.Contains(schoolClass))
            throw new ArgumentException("This class does not belong to the school.", nameof(schoolClass));

        if (!_timetables.TryGetValue(schoolClass, out var timetable))
        {
            timetable = new ClassTimetable(schoolClass, Layout);
            _timetables[schoolClass] = timetable;
        }
        return timetable;
    }

    /// <summary>
    /// 20 placeholder classes (1-A, 1-B ... 10-A, 10-B) so the main screen has something to show.
    /// Temporary: replaced by the classes the user enters once the "classes" screen exists.
    /// </summary>
    public static School CreateSample()
    {
        var classes = new List<SchoolClass>();
        for (var grade = 1; grade <= 10; grade++)
        {
            classes.Add(new SchoolClass($"{grade}-A"));
            classes.Add(new SchoolClass($"{grade}-B"));
        }
        return new School(classes, WeekLayout.Default);
    }
}
