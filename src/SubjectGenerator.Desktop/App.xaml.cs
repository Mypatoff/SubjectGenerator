using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SubjectGenerator.Core;

namespace SubjectGenerator.Desktop;

public partial class App : Application
{
    private Mutex? _singleInstance;

    public App()
    {
        // Without this a crash just closes the window with no explanation.
        DispatcherUnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Two copies would each keep their own picture of the school and overwrite each other's saves.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\SubjectGenerator-EF2346AF-2300-4CBB-8A05-0C707BA76CC8", out var isFirst);
        if (!isFirst)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            MessageBox.Show("SubjectGenerator allaqachon ochiq. Ochiq oynadan foydalaning.", "SubjectGenerator",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var school = LoadSchool();
        if (school is null)
        {
            Shutdown();
            return;
        }
        new MainWindow(school).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Reads the saved school. A damaged file is put aside (never deleted) and the backup of the previous save
    /// is used instead. If the file cannot be read for any other reason (another program has it open, it comes
    /// from a newer version of the app...), the file is left exactly as it is and null is returned, because
    /// starting with an empty school would make the next save overwrite good data.
    /// </summary>
    private static School? LoadSchool()
    {
        var path = SchoolStorage.DefaultPath;
        try
        {
            var result = SchoolStorage.LoadWithRecovery(path);
            switch (result.Outcome)
            {
                case SchoolStorage.LoadOutcome.RestoredFromBackup:
                    MessageBox.Show(
                        "Saqlangan ma'lumotlar fayli buzilgan ekan. Dastur oxirgi zaxira nusxadan tiklandi " +
                        "(eng so'nggi o'zgarishlar yo'qolgan bo'lishi mumkin).\n" +
                        $"Buzilgan fayl shu yerda saqlab qo'yildi:\n{result.BrokenFilePath}",
                        "SubjectGenerator", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                case SchoolStorage.LoadOutcome.StartedEmpty:
                    MessageBox.Show(
                        "Saqlangan ma'lumotlarni o'qib bo'lmadi, dastur bo'sh holda ochildi.\n" +
                        $"Eski fayl shu yerda saqlab qo'yildi:\n{result.BrokenFilePath}",
                        "SubjectGenerator", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
            return result.School;
        }
        catch (SchoolFileTooNewException)
        {
            MessageBox.Show(
                "Ma'lumotlar fayli dasturning yangiroq versiyasida saqlangan. Dasturni yangilang.\n" +
                "Fayl o'zgartirilmadi.",
                "SubjectGenerator", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Ma'lumotlar faylini ochib bo'lmadi (uni boshqa dastur band qilgan bo'lishi mumkin). " +
                "Fayl o'zgartirilmadi, dastur yopiladi. Keyinroq qayta urinib ko'ring.\n" +
                $"{path}\n\n{ex.Message}",
                "SubjectGenerator", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return null;
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "SubjectGenerator - xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
