namespace SubjectGenerator.Core;

/// <summary>
/// Everything the school has: classes, subjects, teachers, who teaches what to which class (and how
/// often), the shape of the week, and each class's timetable.
/// All changes go through the methods here, so the data always follows the rules
/// (unique names, no assignment pointing at something that was deleted, and no generated lesson
/// that contradicts the plan: when a change makes a lesson impossible, that lesson leaves the timetable).
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

    /// <summary>The school's week. Change it with <see cref="ChangeWeek"/>.</summary>
    public WeekLayout Layout { get; private set; }

    public IReadOnlyList<SchoolClass> Classes => _classes;

    public IReadOnlyList<Subject> Subjects => _subjects;

    public IReadOnlyList<Teacher> Teachers => _teachers;

    public IReadOnlyList<TeachingAssignment> Assignments => _assignments;

    /// <summary>The most lessons a subject can have in a week: every slot of the week.</summary>
    public int MaxLessonsPerWeek => Layout.SlotCount;

    // ----- Week -----

    /// <summary>
    /// Changes the school days and the number of lessons a day. Timetables are emptied (they no longer
    /// fit the new week). Teachers keep their days off on the days that are still in the week.
    /// </summary>
    public void ChangeWeek(WeekLayout layout)
    {
        var tooMany = _assignments.FirstOrDefault(a => a.LessonsPerWeek > layout.SlotCount);
        if (tooMany is not null)
            throw new SchoolRuleException(
                $"{tooMany.Class.Name} sinfida {tooMany.Subject.Name} haftasiga {tooMany.LessonsPerWeek} ta, " +
                $"lekin yangi haftada faqat {layout.SlotCount} ta soat bor.");

        foreach (var teacher in _teachers)
        {
            var dayOffNames = teacher.DaysOff.Select(d => Layout.DayNames[d]).ToList();
            foreach (var day in teacher.DaysOff.ToList())
                teacher.SetDayOff(day, false);

            var newDaysOff = dayOffNames.Select(name => IndexOf(layout.DayNames, name)).Where(d => d >= 0).ToList();
            if (newDaysOff.Count < layout.DayCount) // a teacher must keep at least one working day
                foreach (var day in newDaysOff)
                    teacher.SetDayOff(day, true);
        }

        foreach (var subject in _subjects)
            subject.LessonsPerWeek = Math.Min(subject.LessonsPerWeek, layout.SlotCount);

        Layout = layout;
        _timetables.Clear();
    }

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

    /// <summary>The class's weekly plan: every subject it has, with teacher and lessons a week.</summary>
    public IEnumerable<TeachingAssignment> AssignmentsOf(SchoolClass schoolClass) =>
        _assignments.Where(a => a.Class == schoolClass);

    /// <summary>All lessons the class has in a week.</summary>
    public int LessonsPerWeekOf(SchoolClass schoolClass) => AssignmentsOf(schoolClass).Sum(a => a.LessonsPerWeek);

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

    /// <summary>
    /// Changes the subject's usual number of lessons a week. Classes that had the old usual number
    /// get the new one too; classes that were given their own number keep it.
    /// </summary>
    public void SetLessonsPerWeek(Subject subject, int lessonsPerWeek)
    {
        CheckLessonsPerWeek(lessonsPerWeek);
        foreach (var assignment in _assignments.Where(a => a.Subject == subject && a.LessonsPerWeek == subject.LessonsPerWeek).ToList())
        {
            if (assignment.LessonsPerWeek != lessonsPerWeek)
                RemoveLessonsOf(assignment);
            assignment.LessonsPerWeek = lessonsPerWeek;
        }
        subject.LessonsPerWeek = lessonsPerWeek;
    }

    /// <summary>Removes the subject and every class's assignment for it.</summary>
    public void RemoveSubject(Subject subject)
    {
        _subjects.Remove(subject);
        _assignments.RemoveAll(a => a.Subject == subject);
        RemoveLessons((_, _, lesson) => lesson.Subject == subject.Name);
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

    /// <summary>Marks a day (0-based) as the teacher's day off, or as a working day again.</summary>
    public void SetDayOff(Teacher teacher, int day, bool isOff)
    {
        if (day < 0 || day >= Layout.DayCount)
            throw new ArgumentOutOfRangeException(nameof(day));
        if (isOff && teacher.DaysOff.Count == Layout.DayCount - 1 && !teacher.IsDayOff(day))
            throw new SchoolRuleException($"{teacher.Name} haftada kamida bir kun ishlashi kerak.");

        teacher.SetDayOff(day, isOff);
        if (isOff)
            RemoveLessons((_, lessonDay, lesson) => lessonDay == day && lesson.Teacher == teacher.Name);
    }

    /// <summary>Removes the teacher and everything they were teaching.</summary>
    public void RemoveTeacher(Teacher teacher)
    {
        _teachers.Remove(teacher);
        _assignments.RemoveAll(a => a.Teacher == teacher);
        RemoveLessons((_, _, lesson) => lesson.Teacher == teacher.Name);
    }

    public IEnumerable<TeachingAssignment> AssignmentsOf(Teacher teacher) =>
        _assignments.Where(a => a.Teacher == teacher);

    /// <summary>All lessons the teacher has in a week.</summary>
    public int LessonsPerWeekOf(Teacher teacher) => AssignmentsOf(teacher).Sum(a => a.LessonsPerWeek);

    // ----- Who teaches what -----

    /// <summary>
    /// The teacher will teach the subject to the class, <paramref name="lessonsPerWeek"/> times a week
    /// (the subject's usual number if not given). A class has one teacher per subject,
    /// so if another teacher already has it, that is an error (remove theirs first).
    /// </summary>
    public TeachingAssignment Assign(Teacher teacher, Subject subject, SchoolClass schoolClass, int? lessonsPerWeek = null)
    {
        if (!_teachers.Contains(teacher) || !_subjects.Contains(subject) || !_classes.Contains(schoolClass))
            throw new ArgumentException("The teacher, subject and class must all belong to this school.");

        var lessons = lessonsPerWeek ?? subject.LessonsPerWeek;
        CheckLessonsPerWeek(lessons);

        var existing = _assignments.FirstOrDefault(a => a.Subject == subject && a.Class == schoolClass);
        if (existing is not null && existing.Teacher == teacher)
        {
            if (lessonsPerWeek is not null)
                SetLessonsPerWeek(existing, lessons);
            return existing;
        }
        if (existing is not null)
            throw new SchoolRuleException(
                $"{schoolClass.Name} sinfida {subject.Name} fanini {existing.Teacher.Name} o'qitadi.");

        var assignment = new TeachingAssignment(teacher, subject, schoolClass, lessons);
        _assignments.Add(assignment);
        return assignment;
    }

    /// <summary>Changes how many lessons a week this one class gets of the subject.</summary>
    public void SetLessonsPerWeek(TeachingAssignment assignment, int lessonsPerWeek)
    {
        CheckLessonsPerWeek(lessonsPerWeek);
        if (assignment.LessonsPerWeek != lessonsPerWeek)
            RemoveLessonsOf(assignment);
        assignment.LessonsPerWeek = lessonsPerWeek;
    }

    public void Unassign(TeachingAssignment assignment)
    {
        _assignments.Remove(assignment);
        RemoveLessonsOf(assignment);
    }

    /// <summary>Who teaches the subject in the class, or null if nobody has been chosen yet.</summary>
    public Teacher? TeacherOf(Subject subject, SchoolClass schoolClass) =>
        _assignments.FirstOrDefault(a => a.Subject == subject && a.Class == schoolClass)?.Teacher;

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

    /// <summary>Empties every class's timetable.</summary>
    public void ClearTimetables() => _timetables.Clear();

    /// <summary>True if any class has at least one lesson in its timetable.</summary>
    public bool HasAnyLessons() => LessonCount() > 0;

    /// <summary>How many lessons all the timetables hold together.</summary>
    public int LessonCount()
    {
        var count = 0;
        foreach (var timetable in _timetables.Values)
            for (var day = 0; day < Layout.DayCount; day++)
                for (var slot = 0; slot < Layout.SlotsPerDay; slot++)
                    if (timetable.Get(day, slot) is not null)
                        count++;
        return count;
    }

    // ----- Helpers -----

    /// <summary>
    /// Empties every slot whose lesson <paramref name="matches"/> (given the class, the day and the lesson).
    /// Lessons only remember names, so this is how a change to the plan keeps the timetables honest.
    /// </summary>
    private void RemoveLessons(Func<SchoolClass, int, Lesson, bool> matches)
    {
        foreach (var (schoolClass, timetable) in _timetables)
            for (var day = 0; day < Layout.DayCount; day++)
                for (var slot = 0; slot < Layout.SlotsPerDay; slot++)
                    if (timetable.Get(day, slot) is { } lesson && matches(schoolClass, day, lesson))
                        timetable.Set(day, slot, null);
    }

    private void RemoveLessonsOf(TeachingAssignment assignment) => RemoveLessons((schoolClass, _, lesson) =>
        schoolClass == assignment.Class && lesson.Subject == assignment.Subject.Name && lesson.Teacher == assignment.Teacher.Name);

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

    /// <summary>"5-a" and "5-A" count as the same name. Ordinal, so a file means the same on every PC (no Turkish-i surprises).</summary>
    private static bool SameName(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(IReadOnlyList<string> list, string item)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == item)
                return i;
        return -1;
    }
}
