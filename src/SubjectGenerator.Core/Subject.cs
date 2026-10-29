namespace SubjectGenerator.Core;

/// <summary>A subject (Matematika, Ona tili...) and how many of its lessons every class gets in a week.</summary>
public sealed class Subject
{
    internal Subject(string name, int lessonsPerWeek)
    {
        Name = name;
        LessonsPerWeek = lessonsPerWeek;
    }

    public string Name { get; }

    /// <summary>Change it through <see cref="School.SetLessonsPerWeek"/>, which checks the number.</summary>
    public int LessonsPerWeek { get; internal set; }

    public override string ToString() => Name;
}
