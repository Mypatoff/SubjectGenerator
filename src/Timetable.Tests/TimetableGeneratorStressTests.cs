using Timetable.Core;
using Xunit;

namespace Timetable.Tests;

/// <summary>
/// Many random schools, awkward ones included (more lessons than slots, teachers off most days,
/// fewer lessons than days). Whatever the school, the generator must not crash and must never break its rules.
/// </summary>
public class TimetableGeneratorStressTests
{
    private static School RandomSchool(Random random)
    {
        var dayCount = random.Next(1, 7);
        var slotsPerDay = random.Next(1, 9);
        var school = new School(new WeekLayout(WeekLayout.AllDayNames.Take(dayCount).ToArray(), slotsPerDay));

        var classCount = random.Next(0, 7);
        var teacherCount = random.Next(1, 8);
        var subjectCount = random.Next(1, 6);
        for (var c = 0; c < classCount; c++)
            school.AddClass($"C{c}");
        var teachers = Enumerable.Range(0, teacherCount).Select(t => school.AddTeacher($"T{t}")).ToList();
        var subjects = Enumerable.Range(0, subjectCount).Select(s => school.AddSubject($"S{s}", 1)).ToList();

        foreach (var teacher in teachers)
            for (var day = 0; day < dayCount; day++)
                if (random.Next(3) == 0 && teacher.DaysOff.Count < dayCount - 1)
                    school.SetDayOff(teacher, day, true);

        foreach (var schoolClass in school.Classes)
            foreach (var subject in subjects)
                if (random.Next(4) != 0)
                    // Up to the whole week, so overloaded classes happen too.
                    school.Assign(teachers[random.Next(teacherCount)], subject, schoolClass, random.Next(1, school.MaxLessonsPerWeek + 1));
        return school;
    }

    [Fact]
    public void RandomSchools_NeverCrash_AndNeverBreakTheRules()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var random = new Random(seed);
            var school = RandomSchool(random);

            GenerationResult result;
            try
            {
                result = TimetableGenerator.Generate(school, seed);
            }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"School {seed} crashed the generator: {ex}");
            }

            var where = $"school {seed}";
            Assert.Equal(school.Assignments.Sum(a => a.LessonsPerWeek) - result.UnplacedLessons, result.Placements.Count);

            Assert.Equal(result.Placements.Count,
                result.Placements.Select(p => (p.Class, p.Day, p.Slot)).Distinct().Count());
            Assert.True(result.Placements.Select(p => (p.Teacher, p.Day, p.Slot)).Distinct().Count() == result.Placements.Count,
                $"{where}: a teacher is in two classes at once");
            Assert.True(result.Placements.All(p => !p.Teacher.IsDayOff(p.Day)), $"{where}: a lesson on a day off");
            Assert.True(result.Placements.All(p => p.Day < school.Layout.DayCount && p.Slot < school.Layout.SlotsPerDay),
                $"{where}: a lesson outside the week");
            foreach (var a in school.Assignments)
                Assert.True(result.Placements.Count(p => p.Class == a.Class && p.Subject == a.Subject) <= a.LessonsPerWeek,
                    $"{where}: more lessons than the plan");

            // And applying it leaves a school that survives a save/load round trip with nothing dropped.
            result.ApplyTo(school);
            var path = Path.Combine(Path.GetTempPath(), "TimetableStress-" + Guid.NewGuid() + ".json");
            try
            {
                SchoolStorage.Save(school, path);
                var (loaded, warnings) = SchoolStorage.LoadDetailed(path);
                Assert.True(warnings.Count == 0, $"{where}: {string.Join("; ", warnings)}");
                Assert.Equal(school.LessonCount(), loaded.LessonCount());
            }
            finally
            {
                foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*"))
                    File.Delete(file);
            }
        }
    }
}
