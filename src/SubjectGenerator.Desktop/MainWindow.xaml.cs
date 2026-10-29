using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SubjectGenerator.Core;

namespace SubjectGenerator.Desktop;

public partial class MainWindow : Window
{
    private static readonly Brush LineBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE3, 0xEC));
    private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xF1, 0xF5));
    private static readonly Brush EmptySlotBrush = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFD));

    private readonly School _school = School.CreateSample();

    public MainWindow()
    {
        InitializeComponent();

        ClassList.ItemsSource = _school.Classes;
        ClassList.SelectedIndex = 0;
    }

    private void OnClassSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ClassList.SelectedItem is SchoolClass schoolClass)
            ShowTimetable(_school.TimetableFor(schoolClass));
    }

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
