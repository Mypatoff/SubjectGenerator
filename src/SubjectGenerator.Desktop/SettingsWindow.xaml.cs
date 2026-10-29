using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SubjectGenerator.Core;

namespace SubjectGenerator.Desktop;

/// <summary>
/// Where the user enters classes, subjects (and how often each is taught), teachers,
/// and which teacher teaches which subject in which classes. Every change is saved right away.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly School _school;

    public SettingsWindow(School school)
    {
        InitializeComponent();
        _school = school;

        RefreshClasses();
        RefreshSubjects();
        RefreshTeachers();
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

    private bool Confirm(string question) =>
        MessageBox.Show(this, question, "O'chirish", MessageBoxButton.YesNo, MessageBoxImage.Question)
        == MessageBoxResult.Yes;

    /// <summary>The item a row button belongs to (each row button has its item in Tag).</summary>
    private static T ItemOf<T>(object sender) => (T)((FrameworkElement)sender).Tag;

    // ----- Classes -----

    private void RefreshClasses()
    {
        ClassesList.ItemsSource = null;
        ClassesList.ItemsSource = _school.Classes;
    }

    private void OnAddClass(object sender, RoutedEventArgs e)
    {
        if (!TryChange(() => _school.AddClass(NewClassName.Text)))
            return;

        NewClassName.Clear();
        NewClassName.Focus();
        RefreshClasses();
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
            SubjectsList.Items.Refresh();
    }

    private void OnRemoveSubject(object sender, RoutedEventArgs e)
    {
        var subject = ItemOf<Subject>(sender);
        if (!Confirm($"\"{subject.Name}\" fani o'chirilsinmi? Uni kim qaysi sinfda o'qitishi ham o'chadi."))
            return;

        TryChange(() => _school.RemoveSubject(subject));
        RefreshSubjects();
        RefreshTeacherSubjects();
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
        if (!Confirm($"\"{teacher.Name}\" o'chirilsinmi? U o'qitadigan darslar o'qituvchisiz qoladi."))
            return;

        TryChange(() => _school.RemoveTeacher(teacher));
        RefreshTeachers();
    }

    private void OnTeacherSelected(object sender, SelectionChangedEventArgs e) => RefreshTeacherDetails();

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

    /// <summary>Redraws the right side: one check box per class for the chosen subject, and a summary.</summary>
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
            ClassCheckboxesHint.Text = $"{subject.Name} fanini qaysi sinflarda o'qitadi:";
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
        };
        return box;
    }

    /// <summary>"Matematika: 5-A, 5-B" - one line per subject the teacher teaches.</summary>
    private void RefreshTeacherSummary(Teacher teacher)
    {
        TeacherSummary.Children.Clear();

        var bySubject = _school.AssignmentsOf(teacher).GroupBy(a => a.Subject).ToList();
        if (bySubject.Count == 0)
        {
            TeacherSummary.Children.Add(new TextBlock { Text = "Hali hech narsa belgilanmagan.", Foreground = System.Windows.Media.Brushes.Gray });
            return;
        }

        foreach (var group in bySubject)
        {
            var line = new TextBlock { Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap };
            line.Inlines.Add(new System.Windows.Documents.Run(group.Key.Name + ": ") { FontWeight = FontWeights.SemiBold });
            line.Inlines.Add(new System.Windows.Documents.Run(string.Join(", ", group.Select(a => a.Class.Name))));
            TeacherSummary.Children.Add(line);
        }
    }
}
