namespace SubjectGenerator.Core;

/// <summary>
/// "This teacher teaches this subject to this class, so many times a week."
/// A class gets one teacher per subject. Together, a class's assignments are its weekly plan.
/// </summary>
public sealed class TeachingAssignment
{
    internal TeachingAssignment(Teacher teacher, Subject subject, SchoolClass schoolClass, int lessonsPerWeek)
    {
        Teacher = teacher;
        Subject = subject;
        Class = schoolClass;
        LessonsPerWeek = lessonsPerWeek;
    }

    public Teacher Teacher { get; }

    public Subject Subject { get; }

    public SchoolClass Class { get; }

    /// <summary>
    /// How many lessons of the subject this class gets in a week. Can differ between classes
    /// (Ingliz tili: 10 in one group, 2 in another). Change it through <see cref="School.SetLessonsPerWeek(TeachingAssignment, int)"/>.
    /// </summary>
    public int LessonsPerWeek { get; internal set; }

    public override string ToString() => $"{Class.Name}: {Subject.Name} ({Teacher.Name}) x{LessonsPerWeek}";
}
