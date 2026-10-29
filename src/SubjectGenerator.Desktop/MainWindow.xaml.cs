using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SubjectGenerator.Core;

namespace SubjectGenerator.Desktop;

public partial class MainWindow : Window
{
    private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEC));
    private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF5));
    private static readonly Brush EmptySlotBrush = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFD));

    private readonly School _school = LoadSchool();
    private SchoolClass? _selectedClass;

    public MainWindow()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>
    /// Reads the saved school. If the file is broken, it is put aside (not deleted) and the app
    /// starts with an empty school, so the user is never stuck.
    /// </summary>
    private static School LoadSchool()
    {
        var path = SchoolStorage.DefaultPath;
        try
        {
            return SchoolStorage.Load(path, WeekLayout.Default);
        }
        catch (Exception ex)
        {
            var brokenPath = path + $".broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, brokenPath);
            MessageBox.Show(
                $"Saqlangan ma'lumotlarni o'qib bo'lmadi, dastur bo'sh holda ochildi.\n" +
                $"Eski fayl shu yerda saqlab qo'yildi:\n{brokenPath}\n\n{ex.Message}",
                "SubjectGenerator", MessageBoxButton.OK, MessageBoxImage.Warning);
            return new School(WeekLayout.Default);
        }
    }

    /// <summary>Shows the selected class again (or the first one), or the "no classes yet" message.</summary>
    private void Refresh()
    {
        if (_selectedClass is null || !_school.Classes.Contains(_selectedClass))
            _selectedClass = _school.Classes.FirstOrDefault();

        var hasClasses = _selectedClass is not null;
        ClassPickerButton.IsEnabled = hasClasses;
        WeekGridBox.Visibility = hasClasses ? Visibility.Visible : Visibility.Collapsed;
        NoClassesMessage.Visibility = hasClasses ? Visibility.Collapsed : Visibility.Visible;

        if (_selectedClass is null)
        {
            ClassPickerText.Text = "-";
            ClassTitle.Text = "";
            return;
        }

        ClassPickerText.Text = _selectedClass.Name;
        ShowTimetable(_school.TimetableFor(_selectedClass));
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        new SettingsWindow(_school) { Owner = this }.ShowDialog();
        Refresh();
    }

    // ----- Class picker (the drop-down list with a search box) -----

    private void OnClassPopupOpened(object? sender, EventArgs e)
    {
        // While the list is open, a click on the button should only close it (StaysOpen="False" does that),
        // not close and then open it again.
        ClassPickerButton.IsHitTestVisible = false;

        ClassSearch.Text = "";
        FillClassList();
        ClassSearch.Focus();
    }

    private void OnClassPopupClosed(object? sender, EventArgs e) => ClassPickerButton.IsHitTestVisible = true;

    private void OnClassSearchChanged(object sender, TextChangedEventArgs e)
    {
        ClassSearchHint.Visibility = ClassSearch.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        FillClassList();
    }

    /// <summary>Puts the classes whose name contains the search text into the list.</summary>
    private void FillClassList()
    {
        var search = ClassSearch.Text.Trim();
        var matches = _school.Classes
            .Where(c => c.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        ClassList.ItemsSource = matches;
        ClassList.SelectedItem = matches.Contains(_selectedClass!) ? _selectedClass : matches.FirstOrDefault();
        NoClassesFound.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Arrow keys move through the list while typing in the search box, Enter opens the class.</summary>
    private void OnClassSearchKeyDown(object sender, KeyEventArgs e)
    {
        var count = ClassList.Items.Count;
        switch (e.Key)
        {
            case Key.Down when count > 0:
                ClassList.SelectedIndex = Math.Min(ClassList.SelectedIndex + 1, count - 1);
                ClassList.ScrollIntoView(ClassList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up when count > 0:
                ClassList.SelectedIndex = Math.Max(ClassList.SelectedIndex - 1, 0);
                ClassList.ScrollIntoView(ClassList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter:
                ChooseClass(ClassList.SelectedItem as SchoolClass);
                e.Handled = true;
                break;
            case Key.Escape:
                ClassPickerButton.IsChecked = false;
                e.Handled = true;
                break;
        }
    }

    private void OnClassListClicked(object sender, MouseButtonEventArgs e) =>
        ChooseClass(ClassList.SelectedItem as SchoolClass);

    private void ChooseClass(SchoolClass? schoolClass)
    {
        if (schoolClass is null)
            return;

        _selectedClass = schoolClass;
        ClassPickerButton.IsChecked = false;
        Refresh();
    }

    // ----- Week grid -----

    /// <summary>Redraws the week grid: day names across the top, lesson numbers down the side.</summary>
    private void ShowTimetable(ClassTimetable timetable)
    {
        var layout = timetable.Layout;
        ClassTitle.Text = $"{timetable.Class.Name} sinf - haftalik jadval";

        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();

        // Column 0 and row 0 are the labels; the slots start at column 1, row 1.
        WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        for (var day = 0; day < layout.DayCount; day++)
            WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 110 });

        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var slot = 0; slot < layout.SlotsPerDay; slot++)
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 56 });

        AddCell(CreateLabelCell(""), row: 0, column: 0);
        for (var day = 0; day < layout.DayCount; day++)
            AddCell(CreateLabelCell(layout.DayNames[day]), row: 0, column: day + 1);

        for (var slot = 0; slot < layout.SlotsPerDay; slot++)
        {
            AddCell(CreateLabelCell($"{slot + 1}-dars"), row: slot + 1, column: 0);
            for (var day = 0; day < layout.DayCount; day++)
                AddCell(CreateSlotCell(timetable.Get(day, slot)), row: slot + 1, column: day + 1);
        }
    }

    private void AddCell(UIElement cell, int row, int column)
    {
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        WeekGrid.Children.Add(cell);
    }

    private static Border CreateLabelCell(string text) => new()
    {
        Background = HeaderBrush,
        BorderBrush = LineBrush,
        BorderThickness = new Thickness(0, 0, 1, 1),
        Padding = new Thickness(8, 8, 8, 8),
        Child = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x25, 0x36, 0x4D)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    /// <summary>One slot: the lesson's subject and teacher, or a blank cell if nothing is scheduled yet.</summary>
    private static Border CreateSlotCell(Lesson? lesson)
    {
        var cell = new Border
        {
            Background = lesson is null ? EmptySlotBrush : Brushes.White,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(8)
        };

        if (lesson is not null)
        {
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = lesson.Subject, FontWeight = FontWeights.SemiBold });
            text.Children.Add(new TextBlock { Text = lesson.Teacher, FontSize = 12, Foreground = Brushes.Gray });
            cell.Child = text;
        }
        return cell;
    }
}
