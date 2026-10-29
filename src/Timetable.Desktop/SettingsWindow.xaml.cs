using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Timetable.Core;

namespace Timetable.Desktop;

/// <summary>
/// Where the user enters classes (and each class's weekly plan), subjects, teachers, which teacher
/// teaches which subject in which classes, and the shape of the week. Every change is saved right away.
/// </summary>
public partial class SettingsWindow : Window
{
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xB5, 0x47, 0x08));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x36, 0x4D));

    private readonly School _school;
    private int _lessonsPerDay; // what the "Hafta" tab shows; applied only by "Haftani saqlash"

    public SettingsWindow(School school)
    {
        InitializeComponent();
        _school = school;

        RefreshClasses();
        RefreshSubjects();
        RefreshTeachers();
        RefreshWeek();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Runs a change to the school, then saves. If the change breaks a rule (for example a duplicate name),
    /// the user sees why and nothing is saved. Returns true if the change went through.
    /// </summary>
    private bool TryChange(Action change)
    {
        try
        {
            change();
        }
        catch (SchoolRuleException ex)
        {
            MessageBox.Show(this, ex.Message, "Sozlamalar", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        try
        {
            SchoolStorage.Save(_school, SchoolStorage.DefaultPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Ma'lumotlarni saqlab bo'lmadi:\n{ex.Message}", "Sozlamalar",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return true;
    }

    private bool Confirm(string question, string title = "O'chirish") =>
        MessageBox.Show(this, question, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    /// <summary>The item a row button belongs to (each row button has its item in Tag).</summary>
    private static T ItemOf<T>(object sender) => (T)((FrameworkElement)sender).Tag;

    /// <summary>A class's plan or a teacher's lessons changed: redraw both places that show them.</summary>
    private void RefreshPlans()
    {
        RefreshClassPlan();
        RefreshTeacherDetails();
    }

    /// <summary>
    /// One line of a plan: a label, then "− 3 +" to change how many lessons a week this class gets,
    /// then "✕" to take the subject away from the class.
    /// </summary>
    private DockPanel CreatePlanRow(string label, string detail, TeachingAssignment assignment)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var remove = new Button { Content = "✕", Style = (Style)FindResource("RowButton"), Margin = new Thickness(16, 0, 0, 0), ToolTip = "Olib tashlash" };
        remove.Click += (_, _) =>
        {
            if (Confirm($"{assignment.Class.Name} sinfidan {assignment.Subject.Name} fani olib tashlansinmi? Uning jadvaldagi darslari ham o'chadi."))
            {
                TryChange(() => _school.Unassign(assignment));
                RefreshPlans();
            }
        };

        var more = new Button { Content = "+", Style = (Style)FindResource("RowButton") };
        more.Click += (_, _) =>
        {
            if (TryChange(() => _school.SetLessonsPerWeek(assignment, assignment.LessonsPerWeek + 1)))
                RefreshPlans();
        };
        var fewer = new Button { Content = "−", Style = (Style)FindResource("RowButton") };
        fewer.Click += (_, _) =>
        {
            if (TryChange(() => _school.SetLessonsPerWeek(assignment, assignment.LessonsPerWeek - 1)))
                RefreshPlans();
        };
        var count = new TextBlock
        {
            Text = assignment.LessonsPerWeek.ToString(), Width = 34, TextAlignment = TextAlignment.Center,
            FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center
        };

        foreach (var element in new UIElement[] { remove, more, count, fewer })
        {
            DockPanel.SetDock(element, Dock.Right);
            row.Children.Add(element);
        }

        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        text.Inlines.Add(new Run(label) { FontWeight = FontWeights.SemiBold });
        text.Inlines.Add(new Run("  " + detail) { Foreground = Brushes.Gray });
        row.Children.Add(text);
        return row;
    }

    // ----- Classes -----

    private SchoolClass? SelectedClass => ClassesList.SelectedItem as SchoolClass;

    private void RefreshClasses()
    {
        var selected = SelectedClass;
        ClassesList.ItemsSource = null;
        ClassesList.ItemsSource = _school.Classes;
        ClassesList.SelectedItem = selected;
        RefreshClassPlan();
    }

    private void OnAddClass(object sender, RoutedEventArgs e)
    {
        SchoolClass? added = null;
        if (!TryChange(() => added = _school.AddClass(NewClassName.Text)))
            return;

        NewClassName.Clear();
        NewClassName.Focus();
        RefreshClasses();
        ClassesList.SelectedItem = added;
        RefreshTeacherDetails();
    }

    private void OnAddClassKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnAddClass(sender, e);
    }

    private void OnRemoveClass(object sender, RoutedEventArgs e)
    {
        var schoolClass = ItemOf<SchoolClass>(sender);
        if (!Confirm($"\"{schoolClass.Name}\" sinfi o'chirilsinmi? Uning jadvali va o'qituvchilari ham o'chadi."))
            return;

        TryChange(() => _school.RemoveClass(schoolClass));
        RefreshClasses();
        RefreshTeacherDetails();
    }

    private void OnClassSelected(object sender, SelectionChangedEventArgs e) => RefreshClassPlan();

    /// <summary>The right side of "Sinflar": every subject of the class, its teacher and lessons a week.</summary>
    private void RefreshClassPlan()
    {
        var schoolClass = SelectedClass;
        NoClassSelected.Visibility = schoolClass is null ? Visibility.Visible : Visibility.Collapsed;
        ClassDetails.Visibility = schoolClass is null ? Visibility.Collapsed : Visibility.Visible;
        if (schoolClass is null)
            return;

        ClassPlanTitle.Text = $"{schoolClass.Name} - haftalik reja";
        var total = _school.LessonsPerWeekOf(schoolClass);
        var slots = _school.Layout.SlotCount;
        ClassPlanTotal.Text = total > slots
            ? $"Jami: {total} ta dars / haftada {slots} ta soat - sig'maydi! Darslarni kamaytiring yoki \"Hafta\" bo'limida kunlik darslarni ko'paytiring."
            : $"Jami: {total} ta dars / haftada {slots} ta soat";
        ClassPlanTotal.Foreground = total > slots ? WarningBrush : TextBrush;
        ClassPlanTotal.TextWrapping = TextWrapping.Wrap;

        ClassPlanRows.Children.Clear();
        var plan = _school.AssignmentsOf(schoolClass).ToList();
        if (plan.Count == 0)
            ClassPlanRows.Children.Add(new TextBlock { Text = "Bu sinfga hali fan biriktirilmagan.", Foreground = Brushes.Gray });
        foreach (var assignment in plan)
            ClassPlanRows.Children.Add(CreatePlanRow(assignment.Subject.Name, assignment.Teacher.Name, assignment));
    }

    // ----- Subjects -----

    private void RefreshSubjects()
    {
        SubjectsList.ItemsSource = null;
        SubjectsList.ItemsSource = _school.Subjects;
    }

    private void OnAddSubject(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(NewSubjectLessons.Text.Trim(), out var lessons))
        {
            MessageBox.Show(this, "Haftalik darslar sonini raqam bilan yozing.", "Sozlamalar",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!TryChange(() => _school.AddSubject(NewSubjectName.Text, lessons)))
            return;

        NewSubjectName.Clear();
        NewSubjectName.Focus();
        RefreshSubjects();
        RefreshTeacherSubjects();
    }

    private void OnAddSubjectKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnAddSubject(sender, e);
    }

    private void OnMoreLessons(object sender, RoutedEventArgs e) => ChangeLessons(ItemOf<Subject>(sender), +1);

    private void OnFewerLessons(object sender, RoutedEventArgs e) => ChangeLessons(ItemOf<Subject>(sender), -1);

    private void ChangeLessons(Subject subject, int change)
    {
        if (TryChange(() => _school.SetLessonsPerWeek(subject, subject.LessonsPerWeek + change)))
        {
            SubjectsList.Items.Refresh();
            RefreshPlans(); // classes that use the usual number follow it
        }
    }

    private void OnRemoveSubject(object sender, RoutedEventArgs e)
    {
        var subject = ItemOf<Subject>(sender);
        if (!Confirm($"\"{subject.Name}\" fani o'chirilsinmi? Uni kim qaysi sinfda o'qitishi va jadvaldagi darslari ham o'chadi."))
            return;

        TryChange(() => _school.RemoveSubject(subject));
        RefreshSubjects();
        RefreshTeacherSubjects();
        RefreshClassPlan();
    }

    // ----- Teachers -----

    private Teacher? SelectedTeacher => TeachersList.SelectedItem as Teacher;

    private Subject? SelectedTeacherSubject => TeacherSubjectPicker.SelectedItem as Subject;

    private void RefreshTeachers()
    {
        var selected = SelectedTeacher;
        TeachersList.ItemsSource = null;
        TeachersList.ItemsSource = _school.Teachers;
        TeachersList.SelectedItem = selected;
        RefreshTeacherSubjects();
    }

    private void OnAddTeacher(object sender, RoutedEventArgs e)
    {
        Teacher? added = null;
        if (!TryChange(() => added = _school.AddTeacher(NewTeacherName.Text)))
            return;

        NewTeacherName.Clear();
        RefreshTeachers();
        TeachersList.SelectedItem = added;
    }

    private void OnAddTeacherKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnAddTeacher(sender, e);
    }

    private void OnRemoveTeacher(object sender, RoutedEventArgs e)
    {
        var teacher = ItemOf<Teacher>(sender);
        if (!Confirm($"\"{teacher.Name}\" o'chirilsinmi? U o'qitadigan fanlar sinflardan olib tashlanadi va jadvaldagi darslari ham o'chadi."))
            return;

        TryChange(() => _school.RemoveTeacher(teacher));
        RefreshTeachers();
        RefreshClassPlan();
    }

    /// <summary>
    /// Shows the new teacher. The subject box jumps to a subject they teach, so their classes are ticked
    /// right away (instead of a subject someone else teaches, with every box greyed out).
    /// </summary>
    private void OnTeacherSelected(object sender, SelectionChangedEventArgs e)
    {
        var teacher = SelectedTeacher;
        var taughtSubject = teacher is null ? null : _school.AssignmentsOf(teacher).Select(a => a.Subject).FirstOrDefault();
        if (taughtSubject is not null && SelectedTeacherSubject != taughtSubject)
            TeacherSubjectPicker.SelectedItem = taughtSubject; // this redraws the details through OnTeacherSubjectSelected
        else
            RefreshTeacherDetails();
    }

    private void OnTeacherSubjectSelected(object sender, SelectionChangedEventArgs e) => RefreshTeacherDetails();

    /// <summary>Fills the subject drop-down again (after subjects were added or removed).</summary>
    private void RefreshTeacherSubjects()
    {
        var selected = SelectedTeacherSubject;
        TeacherSubjectPicker.ItemsSource = null;
        TeacherSubjectPicker.ItemsSource = _school.Subjects;
        TeacherSubjectPicker.SelectedItem = selected is not null && _school.Subjects.Contains(selected)
            ? selected
            : _school.Subjects.FirstOrDefault();
        RefreshTeacherDetails();
    }

    /// <summary>Redraws the right side: days off, one check box per class for the chosen subject, and a summary.</summary>
    private void RefreshTeacherDetails()
    {
        var teacher = SelectedTeacher;
        NoTeacherSelected.Visibility = teacher is null ? Visibility.Visible : Visibility.Collapsed;
        TeacherDetails.Visibility = teacher is null ? Visibility.Collapsed : Visibility.Visible;
        if (teacher is null)
            return;

        TeacherTitle.Text = teacher.Name;

        DayOffCheckboxes.Children.Clear();
        for (var day = 0; day < _school.Layout.DayCount; day++)
            DayOffCheckboxes.Children.Add(CreateDayOffCheckBox(teacher, day));

        ClassCheckboxes.Children.Clear();

        var subject = SelectedTeacherSubject;
        if (subject is null)
            ClassCheckboxesHint.Text = "Avval \"Fanlar\" bo'limida fan qo'shing.";
        else if (_school.Classes.Count == 0)
            ClassCheckboxesHint.Text = "Avval \"Sinflar\" bo'limida sinf qo'shing.";
        else
        {
            ClassCheckboxesHint.Text = $"{subject.Name} fanini qaysi sinflarda o'qitadi (odatda haftasiga {subject.LessonsPerWeek} ta):";
            foreach (var schoolClass in _school.Classes)
                ClassCheckboxes.Children.Add(CreateClassCheckBox(teacher, subject, schoolClass));
        }

        RefreshTeacherSummary(teacher);
    }

    /// <summary>Ticked = the teacher does not work that day.</summary>
    private CheckBox CreateDayOffCheckBox(Teacher teacher, int day)
    {
        var box = new CheckBox
        {
            Content = _school.Layout.DayNames[day],
            IsChecked = teacher.IsDayOff(day),
            Margin = new Thickness(0, 0, 18, 8),
            Cursor = Cursors.Hand
        };
        box.Click += (_, _) =>
        {
            if (!TryChange(() => _school.SetDayOff(teacher, day, box.IsChecked == true)))
                box.IsChecked = teacher.IsDayOff(day); // the change was refused: show the real state again
            RefreshTeacherSummary(teacher);
        };
        return box;
    }

    /// <summary>
    /// Ticked = this teacher teaches the subject in that class. A class where another teacher
    /// already has the subject is greyed out, with that teacher's name shown.
    /// </summary>
    private CheckBox CreateClassCheckBox(Teacher teacher, Subject subject, SchoolClass schoolClass)
    {
        var current = _school.TeacherOf(subject, schoolClass);
        var box = new CheckBox
        {
            Content = schoolClass.Name,
            IsChecked = current == teacher,
            Margin = new Thickness(0, 0, 18, 8),
            MinWidth = 60,
            Cursor = Cursors.Hand
        };

        if (current is not null && current != teacher)
        {
            box.IsEnabled = false;
            box.Content = $"{schoolClass.Name} ({current.Name})";
            ToolTipService.SetShowOnDisabled(box, true);
            box.ToolTip = $"{schoolClass.Name} sinfida {subject.Name} fanini {current.Name} o'qitadi.";
        }

        box.Click += (_, _) =>
        {
            if (box.IsChecked == true)
                TryChange(() => _school.Assign(teacher, subject, schoolClass));
            else
            {
                var assignment = _school.Assignments.First(a => a.Subject == subject && a.Class == schoolClass);
                TryChange(() => _school.Unassign(assignment));
            }
            RefreshTeacherSummary(teacher);
            RefreshClassPlan();
        };
        return box;
    }

    /// <summary>Everything the teacher teaches, with lessons a week per class, and their weekly total.</summary>
    private void RefreshTeacherSummary(Teacher teacher)
    {
        var total = _school.LessonsPerWeekOf(teacher);
        var slots = (_school.Layout.DayCount - teacher.DaysOff.Count) * _school.Layout.SlotsPerDay;
        TeacherLoadTitle.Text = total > slots
            ? $"Hozir o'qitadi: jami {total} ta dars - ish kunlarida faqat {slots} ta soat bor!"
            : $"Hozir o'qitadi: jami haftasiga {total} ta dars";
        TeacherLoadTitle.Foreground = total > slots ? WarningBrush : TextBrush;

        TeacherSummary.Children.Clear();
        var assignments = _school.AssignmentsOf(teacher).OrderBy(a => a.Subject.Name).ToList();
        if (assignments.Count == 0)
            TeacherSummary.Children.Add(new TextBlock { Text = "Hali hech narsa belgilanmagan.", Foreground = Brushes.Gray });
        foreach (var assignment in assignments)
            TeacherSummary.Children.Add(CreatePlanRow(assignment.Subject.Name, assignment.Class.Name, assignment));
    }

    // ----- Week -----

    /// <summary>Shows the school's current week in the "Hafta" tab.</summary>
    private void RefreshWeek()
    {
        _lessonsPerDay = _school.Layout.SlotsPerDay;
        WeekDayCheckboxes.Children.Clear();
        foreach (var day in WeekLayout.AllDayNames)
        {
            var box = new CheckBox
            {
                Content = day,
                IsChecked = _school.Layout.DayNames.Contains(day),
                Margin = new Thickness(0, 0, 18, 8),
                Cursor = Cursors.Hand
            };
            box.Click += (_, _) => RefreshWeekTotals();
            WeekDayCheckboxes.Children.Add(box);
        }
        RefreshWeekTotals();
    }

    private string[] CheckedWeekDays() => WeekDayCheckboxes.Children.OfType<CheckBox>()
        .Where(b => b.IsChecked == true)
        .Select(b => (string)b.Content)
        .ToArray();

    /// <summary>"6 kun x 8 ta dars = 48 ta soat", and a warning for classes or teachers that would not fit.</summary>
    private void RefreshWeekTotals()
    {
        LessonsPerDayText.Text = _lessonsPerDay.ToString();
        var days = CheckedWeekDays().Length;
        var slots = days * _lessonsPerDay;
        WeekTotalText.Text = $"Haftada: {days} kun x {_lessonsPerDay} ta dars = {slots} ta soat";

        var tooBig = _school.Classes.Where(c => _school.LessonsPerWeekOf(c) > slots)
            .Select(c => $"{c.Name} ({_school.LessonsPerWeekOf(c)})")
            .ToList();
        WeekWarning.Text = tooBig.Count == 0
            ? ""
            : $"Bu sinflarning darslari bunday haftaga sig'maydi: {string.Join(", ", tooBig)}.";
    }

    private void OnMoreLessonsPerDay(object sender, RoutedEventArgs e)
    {
        _lessonsPerDay = Math.Min(_lessonsPerDay + 1, WeekLayout.MaxSlotsPerDay);
        RefreshWeekTotals();
    }

    private void OnFewerLessonsPerDay(object sender, RoutedEventArgs e)
    {
        _lessonsPerDay = Math.Max(_lessonsPerDay - 1, 1);
        RefreshWeekTotals();
    }

    private void OnSaveWeek(object sender, RoutedEventArgs e)
    {
        var days = CheckedWeekDays();
        if (days.Length == 0)
        {
            MessageBox.Show(this, "Kamida bitta o'qish kunini belgilang.", "Hafta", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_school.HasAnyLessons()
            && !Confirm("Hafta o'zgarsa, hozirgi jadval o'chiriladi va uni qayta tuzish kerak bo'ladi. Davom etilsinmi?", "Hafta"))
            return;

        if (!TryChange(() => _school.ChangeWeek(new WeekLayout(days, _lessonsPerDay))))
            return;

        RefreshWeek();
        RefreshSubjects();
        RefreshPlans();
        MessageBox.Show(this, $"Hafta saqlandi: {days.Length} kun, kuniga {_lessonsPerDay} ta dars.", "Hafta",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
