using System.Threading;
using System.Windows;

namespace HeyComputer.Desktop;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Local\\HeyComputer.Desktop.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        _ownsMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "Hey Computer is already running in the notification area.",
                "Hey Computer",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window)
        {
            window.PrepareForSystemShutdown();
        }

        base.OnSessionEnding(e);
    }
}
