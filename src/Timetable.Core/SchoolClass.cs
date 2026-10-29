namespace Timetable.Core;

public sealed class SchoolClass
{
    public SchoolClass(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A class needs a name.", nameof(name));

        Name = name.Trim();
    }

    public string Name { get; }

    public override string ToString() => Name;
}
