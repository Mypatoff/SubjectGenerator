namespace SubjectGenerator.Core;

/// <summary>
/// The school file was saved by a newer version of the app. It is not damaged, so it must never be
/// moved aside or overwritten: the user needs to update the app instead.
/// </summary>
public sealed class SchoolFileTooNewException : Exception
{
    public SchoolFileTooNewException(int version)
        : base($"The school file was made by a newer version of the app (format {version}).")
    {
    }
}
