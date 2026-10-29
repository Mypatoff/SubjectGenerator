namespace SubjectGenerator.Core;

/// <summary>"This teacher teaches this subject to this class." A class gets one teacher per subject.</summary>
public sealed record TeachingAssignment(Teacher Teacher, Subject Subject, SchoolClass Class);
