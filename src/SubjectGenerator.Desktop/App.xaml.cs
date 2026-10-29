using System.Windows;
using System.Windows.Threading;

namespace SubjectGenerator.Desktop;

public partial class App : Application
{
    public App()
    {
        // Without this a crash just closes the window with no explanation.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "SubjectGenerator - xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
