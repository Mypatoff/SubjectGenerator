namespace SubjectGenerator.Core;

public sealed class Teacher
{
    internal Teacher(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public override string ToString() => Name;
}
