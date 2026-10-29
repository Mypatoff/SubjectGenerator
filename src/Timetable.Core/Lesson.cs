namespace Timetable.Core;

/// <summary>One subject taught by one teacher; this is what fills a slot in a timetable.</summary>
public sealed record Lesson(string Subject, string Teacher);
