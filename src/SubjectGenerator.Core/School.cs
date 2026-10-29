namespace SubjectGenerator.Core;

/// <summary>
/// Everything the school has: classes, subjects, teachers, who teaches what to which class,
/// the shape of the week, and each class's timetable.
/// All changes go through the methods here, so the data always follows the rules
/// (unique names, no assignment pointing at something that was deleted).
/// </summary>
public sealed class School
{
    private readonly List<SchoolClass> _classes = new();
    private readonly List<Subject> _subjects = new();
    private readonly List<Teacher> _teachers = new();
    private readonly List<TeachingAssignment> _assignments = new();
    private readonly Dictionary<SchoolClass, ClassTimetable> _timetables = new();

    public School(WeekLayout layout)
    {
        Layout = layout;
    }

    public WeekLayout Layout { get; }

    public IReadOnlyList<SchoolClass> Classes => _classes;

    public IReadOnlyList<Subject> Subjects => _subjects;

    public IReadOnlyList<Teacher> Teachers => _teachers;

    public IReadOnlyList<TeachingAssignment> Assignments => _assignments;

    /// <summary>The most lessons a subject can have in a week: every slot of the week.</summary>
    public int MaxLessonsPerWeek => Layout.DayCount * Layout.SlotsPerDay;

    // ----- Classes -----

    public SchoolClass AddClass(string name)
    {
        name = CleanName(name, "Sinf nomini kiriting.");
        if (_classes.Any(c => SameName(c.Name, name)))
            throw new SchoolRuleException($"\"{name}\" sinfi allaqachon bor.");

        var schoolClass = new SchoolClass(name);
        _classes.Add(schoolClass);
        return schoolClass;
    }

    /// <summary>Removes the class together with its timetable and everything taught to it.</summary>
    public void RemoveClass(SchoolClass schoolClass)
    {
        _classes.Remove(schoolClass);
        _timetables.Remove(schoolClass);
        _assignments.RemoveAll(a => a.Class == schoolClass);
    }

    // ----- Subjects -----

    public Subject AddSubject(string name, int lessonsPerWeek)
    {
        name = CleanName(name, "Fan nomini kiriting.");
        if (_subjects.Any(s => SameName(s.Name, name)))
            throw new SchoolRuleException($"\"{name}\" fani allaqachon bor.");
        CheckLessonsPerWeek(lessonsPerWeek);

        var subject = new Subject(name, lessonsPerWeek);
        _subjects.Add(subject);
        return subject;
    }

    public void SetLessonsPerWeek(Subject subject, int lessonsPerWeek)
    {
        CheckLessonsPerWeek(lessonsPerWeek);
        subject.LessonsPerWeek = lessonsPerWeek;
    }

    /// <summary>Removes the subject and every class's assignment for it.</summary>
    public void RemoveSubject(Subject subject)
    {
        _subjects.Remove(subject);
        _assignments.RemoveAll(a => a.Subject == subject);
    }

    // ----- Teachers -----

    public Teacher AddTeacher(string name)
    {
        name = CleanName(name, "O'qituvchi ismini kiriting.");
        if (_teachers.Any(t => SameName(t.Name, name)))
            throw new SchoolRuleException($"\"{name}\" o'qituvchisi allaqachon bor.");

        var teacher = new Teacher(name);
        _teachers.Add(teacher);
        return teacher;
    }

    /// <summary>Removes the teacher and everything they were teaching.</summary>
    public void RemoveTeacher(Teacher teacher)
    {
        _teachers.Remove(teacher);
        _assignments.RemoveAll(a => a.Teacher == teacher);
    }

    // ----- Who teaches what -----

    /// <summary>
    /// The teacher will teach the subject to the class. A class has one teacher per subject,
    /// so if another teacher already has it, that is an error (remove theirs first).
    /// </summary>
    public TeachingAssignment Assign(Teacher teacher, Subject subject, SchoolClass schoolClass)
    {
        if (!_teachers.Contains(teacher) || !_subjects.Contains(subject) || !_classes.Contains(schoolClass))
            throw new ArgumentException("The teacher, subject and class must all belong to this school.");

        var existing = TeacherOf(subject, schoolClass);
        if (existing == teacher)
            return _assignments.First(a => a.Subject == subject && a.Class == schoolClass);
        if (existing is not null)
            throw new SchoolRuleException(
                $"{schoolClass.Name} sinfida {subject.Name} fanini {existing.Name} o'qitadi.");

        var assignment = new TeachingAssignment(teacher, subject, schoolClass);
        _assignments.Add(assignment);
        return assignment;
    }

    public void Unassign(TeachingAssignment assignment) => _assignments.Remove(assignment);

    /// <summary>Who teaches the subject in the class, or null if nobody has been chosen yet.</summary>
    public Teacher? TeacherOf(Subject subject, SchoolClass schoolClass) =>
        _assignments.FirstOrDefault(a => a.Subject == subject && a.Class == schoolClass)?.Teacher;

    public IEnumerable<TeachingAssignment> AssignmentsOf(Teacher teacher) =>
        _assignments.Where(a => a.Teacher == teacher);

    // ----- Timetables -----

    /// <summary>The class's timetable; every class starts with an empty week.</summary>
    public ClassTimetable TimetableFor(SchoolClass schoolClass)
    {
        if (!_classes.Contains(schoolClass))
            throw new ArgumentException("This class does not belong to the school.", nameof(schoolClass));

        if (!_timetables.TryGetValue(schoolClass, out var timetable))
        {
            timetable = new ClassTimetable(schoolClass, Layout);
            _timetables[schoolClass] = timetable;
        }
        return timetable;
    }

    // ----- Helpers -----

    private void CheckLessonsPerWeek(int lessonsPerWeek)
    {
        if (lessonsPerWeek < 1 || lessonsPerWeek > MaxLessonsPerWeek)
            throw new SchoolRuleException($"Haftalik darslar soni 1 dan {MaxLessonsPerWeek} gacha bo'lishi kerak.");
    }

    private static string CleanName(string? name, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new SchoolRuleException(emptyMessage);
        return name.Trim();
    }

    /// <summary>"5-a" and "5-A" count as the same name.</summary>
    private static bool SameName(string a, string b) =>
        string.Equals(a, b, StringComparison.CurrentCultureIgnoreCase);
}
