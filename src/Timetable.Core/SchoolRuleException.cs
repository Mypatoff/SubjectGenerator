namespace Timetable.Core;

/// <summary>
/// The user tried something the school data does not allow (a duplicate name, a missing teacher...).
/// The message is in Uzbek and is meant to be shown to the user as it is.
/// </summary>
public sealed class SchoolRuleException : Exception
{
    public SchoolRuleException(string message) : base(message)
    {
    }
}
