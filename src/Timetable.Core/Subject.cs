namespace Timetable.Core;

/// <summary>A subject (Matematika, Ona tili...) and how many of its lessons a class usually gets in a week.</summary>
public sealed class Subject
{
    internal Subject(string name, int lessonsPerWeek)
    {
        Name = name;
        LessonsPerWeek = lessonsPerWeek;
    }

    public string Name { get; }

    /// <summary>
    /// The usual number of lessons a week. A class that gets this subject starts with this number,
    /// and each class can then have its own (see <see cref="TeachingAssignment.LessonsPerWeek"/>).
    /// Change it through <see cref="School.SetLessonsPerWeek(Subject, int)"/>, which checks the number.
    /// </summary>
    public int LessonsPerWeek { get; internal set; }

    public override string ToString() => Name;
}
