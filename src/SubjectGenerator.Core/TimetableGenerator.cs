using System.Diagnostics;

namespace SubjectGenerator.Core;

/// <summary>One generated lesson: which class has which subject with which teacher, and when.</summary>
public sealed record Placement(SchoolClass Class, int Day, int Slot, Subject Subject, Teacher Teacher);

/// <summary>What <see cref="TimetableGenerator.Generate"/> made, plus anything the user should know.</summary>
public sealed class GenerationResult
{
    internal GenerationResult(IReadOnlyList<Placement> placements, int unplacedLessons, IReadOnlyList<string> problems)
    {
        Placements = placements;
        UnplacedLessons = unplacedLessons;
        Problems = problems;
    }

    public IReadOnlyList<Placement> Placements { get; }

    /// <summary>Lessons that should be in the week but did not fit anywhere.</summary>
    public int UnplacedLessons { get; }

    /// <summary>Messages for the user (in Uzbek): missing teachers, overloaded teachers, lessons that did not fit...</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>Replaces every class's timetable with the generated one.</summary>
    public void ApplyTo(School school)
    {
        school.ClearTimetables();
        foreach (var p in Placements)
            school.TimetableFor(p.Class).Set(p.Day, p.Slot, new Lesson(p.Subject.Name, p.Teacher.Name));
    }
}

/// <summary>
/// Makes timetables for all classes at once. The rules it never breaks:
/// - a class has at most one lesson in a slot;
/// - a teacher is never in two classes in the same slot;
/// - a teacher has no lessons on their days off.
/// What it tries for: every lesson placed, no empty slots between a class's lessons (each day
/// starts at the 1st lesson), lessons spread evenly over the week, and the same subject not
/// repeated in one day more than needed.
///
/// How: lessons wait in a queue. Each step takes the waiting lesson with the fewest free places
/// ("most constrained first") and puts it in its best free place. If it has no free place, it takes
/// the place that disturbs the fewest other lessons anyway, and those lessons go back into the queue.
/// This repeats until the queue is empty (or a step limit is reached; then the best state seen is kept).
/// </summary>
public static class TimetableGenerator
{
    private const int MaxStepsPerMode = 200_000;
    private static readonly TimeSpan MaxTimePerMode = TimeSpan.FromSeconds(4);

    public static GenerationResult Generate(School school, int? seed = null)
    {
        var problems = new List<string>();
        var random = seed is null ? new Random() : new Random(seed.Value);
        var search = new Search(school, random);

        ReportMissingTeachers(school, problems);
        ReportOverloads(school, problems);

        // First try: compact days (no gaps) with subjects spread out.
        var best = search.Run(compact: true);
        if (best.Count < search.TotalLessons)
        {
            // Second try: any slot is allowed, so a class may get an empty slot between two lessons.
            var relaxed = search.Run(compact: false);
            if (relaxed.Count > best.Count)
            {
                best = relaxed;
                problems.Add("Bo'sh soatlarsiz jadval tuzib bo'lmadi, shuning uchun ba'zi sinflarda darslar orasida bo'sh soat bor.");
            }
        }

        if (best.Count < search.TotalLessons)
            problems.AddRange(search.DescribeUnplaced(best));

        return new GenerationResult(search.ToPlacements(best), search.TotalLessons - best.Count, problems);
    }

    private static void ReportMissingTeachers(School school, List<string> problems)
    {
        if (school.Assignments.Count == 0)
        {
            problems.Add("Hech bir sinfga o'qituvchi biriktirilmagan. Sozlamalar > O'qituvchilar bo'limida belgilang.");
            return;
        }

        foreach (var subject in school.Subjects)
        {
            var classesWithoutTeacher = school.Classes.Where(c => school.TeacherOf(subject, c) is null).ToList();
            if (classesWithoutTeacher.Count > 0)
                problems.Add($"{subject.Name} fanining o'qituvchisi yo'q: {string.Join(", ", classesWithoutTeacher.Select(c => c.Name))}.");
        }
    }

    private static void ReportOverloads(School school, List<string> problems)
    {
        var layout = school.Layout;

        foreach (var schoolClass in school.Classes)
        {
            var lessons = school.Assignments.Where(a => a.Class == schoolClass).Sum(a => a.Subject.LessonsPerWeek);
            var slots = layout.DayCount * layout.SlotsPerDay;
            if (lessons > slots)
                problems.Add($"{schoolClass.Name}: haftasiga {lessons} ta dars, lekin haftada faqat {slots} ta soat bor.");
        }

        foreach (var teacher in school.Teachers)
        {
            var lessons = school.AssignmentsOf(teacher).Sum(a => a.Subject.LessonsPerWeek);
            var slots = (layout.DayCount - teacher.DaysOff.Count) * layout.SlotsPerDay;
            if (lessons > slots)
                problems.Add($"{teacher.Name}: haftasiga {lessons} ta dars, lekin ish kunlarida faqat {slots} ta soat bor.");
        }
    }

    /// <summary>
    /// The search itself. Everything is stored in plain arrays indexed by numbers
    /// (class number, teacher number, day, slot) because it is checked very many times.
    /// A "group" is all lessons of one subject in one class (for example 5-A Matematika, 5 lessons).
    /// </summary>
    private sealed class Search
    {
        private const int Empty = -1;

        private readonly School _school;
        private readonly Random _random;
        private readonly int _classes;
        private readonly int _days;
        private readonly int _slots;

        private readonly TeachingAssignment[] _groups;
        private readonly int[] _groupClass;
        private readonly int[] _groupTeacher;
        private readonly int[] _groupSize;
        private readonly bool[,] _teacherOff;    // [teacher, day]

        // Rules of the current mode.
        private bool _compact;
        private readonly bool[,,] _allowed;     // [class, day, slot]: may this slot be used at all?
        private readonly int[] _maxPerDay;      // [group]: most lessons of the group in one day

        // State: who is where.
        private readonly int[,,] _classSlot;    // [class, day, slot] -> group, or Empty
        private readonly int[,,] _teacherSlot;  // [teacher, day, slot] -> group, or Empty
        private readonly int[,,] _placedAtStep; // [class, day, slot] -> step when that lesson was put there
        private readonly int[,] _groupPerDay;   // [group, day]
        private readonly int[] _waiting;        // [group]: lessons not placed yet
        private int _placedCount;
        private int _step;

        public Search(School school, Random random)
        {
            _school = school;
            _random = random;
            _classes = school.Classes.Count;
            _days = school.Layout.DayCount;
            _slots = school.Layout.SlotsPerDay;

            _groups = school.Assignments.ToArray();
            _groupClass = _groups.Select(a => IndexOf(school.Classes, a.Class)).ToArray();
            _groupTeacher = _groups.Select(a => IndexOf(school.Teachers, a.Teacher)).ToArray();
            _groupSize = _groups.Select(a => a.Subject.LessonsPerWeek).ToArray();
            TotalLessons = _groupSize.Sum();

            _teacherOff = new bool[school.Teachers.Count, _days];
            for (var t = 0; t < school.Teachers.Count; t++)
                for (var d = 0; d < _days; d++)
                    _teacherOff[t, d] = school.Teachers[t].IsDayOff(d);

            _allowed = new bool[_classes, _days, _slots];
            _maxPerDay = new int[_groups.Length];
            _classSlot = new int[_classes, _days, _slots];
            _teacherSlot = new int[school.Teachers.Count, _days, _slots];
            _placedAtStep = new int[_classes, _days, _slots];
            _groupPerDay = new int[_groups.Length, _days];
            _waiting = new int[_groups.Length];
        }

        public int TotalLessons { get; }

        /// <summary>Runs the search in one mode and returns the best set of placed lessons it saw.</summary>
        public List<Spot> Run(bool compact)
        {
            SetUpMode(compact);
            Reset();

            var best = new List<Spot>();
            var clock = Stopwatch.StartNew();
            for (_step = 1; _step <= MaxStepsPerMode && _placedCount < TotalLessons; _step++)
            {
                if (_step % 1000 == 0 && clock.Elapsed > MaxTimePerMode)
                    break;

                var group = MostConstrainedWaitingGroup();
                if (group == Empty)
                    break; // nothing waiting can go anywhere at all (for example a teacher off every allowed day)
                PlaceForcing(group);

                if (_placedCount > best.Count)
                    best = Snapshot();
            }
            if (_placedCount > best.Count)
                best = Snapshot();
            return best;
        }

        public IEnumerable<string> DescribeUnplaced(List<Spot> placed)
        {
            for (var g = 0; g < _groups.Length; g++)
            {
                var missing = _groupSize[g] - placed.Count(s => s.Group == g);
                if (missing > 0)
                {
                    var a = _groups[g];
                    yield return $"{a.Class.Name}: {a.Subject.Name} ({a.Teacher.Name}) - {missing} ta dars joylashmadi.";
                }
            }
        }

        public List<Placement> ToPlacements(List<Spot> spots) => spots
            .Select(s => new Placement(_groups[s.Group].Class, s.Day, s.Slot, _groups[s.Group].Subject, _groups[s.Group].Teacher))
            .ToList();

        /// <summary>
        /// Compact mode: each class gets a fixed number of lessons per day (spread evenly, the extra ones
        /// on the first days), always starting at the 1st slot, so there are no gaps. A subject is also
        /// limited per day (5 lessons a week over 6 days = at most 1 a day).
        /// Relaxed mode: any slot, no per-day limit (the score still prefers spreading out and no gaps).
        /// </summary>
        private void SetUpMode(bool compact)
        {
            _compact = compact;
            Array.Clear(_allowed);

            for (var c = 0; c < _classes; c++)
            {
                var lessons = 0;
                for (var g = 0; g < _groups.Length; g++)
                    if (_groupClass[g] == c)
                        lessons += _groupSize[g];

                for (var d = 0; d < _days; d++)
                {
                    var today = compact ? lessons / _days + (d < lessons % _days ? 1 : 0) : _slots;
                    for (var k = 0; k < Math.Min(today, _slots); k++)
                        _allowed[c, d, k] = true;
                }
            }

            for (var g = 0; g < _groups.Length; g++)
            {
                var workingDays = 0;
                for (var d = 0; d < _days; d++)
                    if (!_teacherOff[_groupTeacher[g], d])
                        workingDays++;
                _maxPerDay[g] = compact ? (_groupSize[g] + workingDays - 1) / Math.Max(workingDays, 1) : _slots;
            }
        }

        private void Reset()
        {
            Fill(_classSlot, Empty);
            Fill(_teacherSlot, Empty);
            Array.Clear(_groupPerDay);
            Array.Copy(_groupSize, _waiting, _groupSize.Length);
            _placedCount = 0;
        }

        /// <summary>The waiting group with the fewest free spots right now (ties broken at random).</summary>
        private int MostConstrainedWaitingGroup()
        {
            var best = Empty;
            var fewest = int.MaxValue;
            var ties = 0;
            for (var g = 0; g < _groups.Length; g++)
            {
                if (_waiting[g] == 0 || !HasAnyUsableSpot(g) || ClassIsFull(g) || TeacherIsFull(g))
                    continue;
                var count = CountFreeSpots(g, fewest);
                if (count < fewest)
                {
                    fewest = count;
                    best = g;
                    ties = 1;
                }
                else if (count == fewest && _random.Next(++ties) == 0)
                {
                    best = g;
                }
            }
            return best;
        }

        /// <summary>
        /// Puts one lesson of the group in the best spot. A free spot if there is one; otherwise the spot
        /// that pushes out the fewest (and least recently placed) lessons, which go back to waiting.
        /// </summary>
        private void PlaceForcing(int g)
        {
            var c = _groupClass[g];
            var t = _groupTeacher[g];
            var bestDay = -1;
            var bestSlot = -1;
            var bestCost = double.MaxValue;

            for (var d = 0; d < _days; d++)
            {
                if (_teacherOff[t, d] || _groupPerDay[g, d] >= _maxPerDay[g])
                    continue;
                for (var k = 0; k < _slots; k++)
                {
                    if (!_allowed[c, d, k])
                        continue;

                    var cost = Score(g, d, k) + _random.NextDouble();
                    cost += PushOutCost(_classSlot[c, d, k], c, d, k);
                    var teacherOther = _teacherSlot[t, d, k];
                    if (teacherOther != Empty && teacherOther != _classSlot[c, d, k])
                        cost += PushOutCost(teacherOther, _groupClass[teacherOther], d, k);

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestDay = d;
                        bestSlot = k;
                    }
                }
            }

            var inClass = _classSlot[c, bestDay, bestSlot];
            if (inClass != Empty)
                Remove(inClass, bestDay, bestSlot);
            var inTeacher = _teacherSlot[t, bestDay, bestSlot];
            if (inTeacher != Empty)
                Remove(inTeacher, bestDay, bestSlot);
            Put(g, bestDay, bestSlot);
        }

        /// <summary>
        /// How bad it is to push a lesson out of its spot: always costly, and very costly if it was put
        /// there only a few steps ago (otherwise two lessons could keep pushing each other out forever).
        /// </summary>
        private double PushOutCost(int group, int c, int d, int k)
        {
            if (group == Empty)
                return 0;
            var recent = _step - _placedAtStep[c, d, k] < 15;
            return recent ? 1000 : 100;
        }

        /// <summary>Lower is better: avoid the same subject twice a day, prefer early slots with no gap before them.</summary>
        private double Score(int g, int day, int slot)
        {
            var score = _groupPerDay[g, day] * 10.0;
            if (!_compact)
            {
                var c = _groupClass[g];
                score += slot * 0.5;
                if (slot > 0 && _classSlot[c, day, slot - 1] == Empty)
                    score += 3;
            }
            return score;
        }

        private bool IsFree(int g, int day, int slot)
        {
            var c = _groupClass[g];
            var t = _groupTeacher[g];
            return _allowed[c, day, slot]
                   && _classSlot[c, day, slot] == Empty
                   && _teacherSlot[t, day, slot] == Empty
                   && !_teacherOff[t, day]
                   && _groupPerDay[g, day] < _maxPerDay[g];
        }

        /// <summary>False if the group can never go anywhere in this mode, even by pushing others out.</summary>
        private bool HasAnyUsableSpot(int g)
        {
            for (var d = 0; d < _days; d++)
                if (!_teacherOff[_groupTeacher[g], d] && _groupPerDay[g, d] < _maxPerDay[g])
                    for (var k = 0; k < _slots; k++)
                        if (_allowed[_groupClass[g], d, k])
                            return true;
            return false;
        }

        /// <summary>
        /// Every usable slot of the group's class is taken. A waiting lesson could then only push out
        /// another lesson of the same class, which never helps: the class simply has more lessons than
        /// slots. Without this check such a class would keep pushing its own lessons around forever and
        /// the other classes would never get their turn.
        /// </summary>
        private bool ClassIsFull(int g)
        {
            var c = _groupClass[g];
            for (var d = 0; d < _days; d++)
                for (var k = 0; k < _slots; k++)
                    if (_allowed[c, d, k] && _classSlot[c, d, k] == Empty)
                        return false;
            return true;
        }

        /// <summary>Same idea for a teacher: every slot on their working days is taken (they have too many lessons).</summary>
        private bool TeacherIsFull(int g)
        {
            var t = _groupTeacher[g];
            for (var d = 0; d < _days; d++)
                if (!_teacherOff[t, d])
                    for (var k = 0; k < _slots; k++)
                        if (_teacherSlot[t, d, k] == Empty)
                            return false;
            return true;
        }

        /// <summary>Counts free spots, stopping early once there are more than <paramref name="stopAt"/>.</summary>
        private int CountFreeSpots(int g, int stopAt)
        {
            var count = 0;
            for (var d = 0; d < _days; d++)
                for (var k = 0; k < _slots; k++)
                    if (IsFree(g, d, k) && ++count > stopAt)
                        return count;
            return count;
        }

        private void Put(int g, int d, int k)
        {
            var c = _groupClass[g];
            _classSlot[c, d, k] = g;
            _teacherSlot[_groupTeacher[g], d, k] = g;
            _placedAtStep[c, d, k] = _step;
            _groupPerDay[g, d]++;
            _waiting[g]--;
            _placedCount++;
        }

        private void Remove(int g, int d, int k)
        {
            _classSlot[_groupClass[g], d, k] = Empty;
            _teacherSlot[_groupTeacher[g], d, k] = Empty;
            _groupPerDay[g, d]--;
            _waiting[g]++;
            _placedCount--;
        }

        private List<Spot> Snapshot()
        {
            var spots = new List<Spot>(_placedCount);
            for (var c = 0; c < _classes; c++)
                for (var d = 0; d < _days; d++)
                    for (var k = 0; k < _slots; k++)
                        if (_classSlot[c, d, k] != Empty)
                            spots.Add(new Spot(_classSlot[c, d, k], d, k));
            return spots;
        }

        private static void Fill(int[,,] array, int value)
        {
            foreach (var i in Enumerable.Range(0, array.GetLength(0)))
                for (var j = 0; j < array.GetLength(1); j++)
                    for (var k = 0; k < array.GetLength(2); k++)
                        array[i, j, k] = value;
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T item)
        {
            for (var i = 0; i < list.Count; i++)
                if (Equals(list[i], item))
                    return i;
            throw new ArgumentException("Item not found.");
        }
    }

    private readonly record struct Spot(int Group, int Day, int Slot);
}
