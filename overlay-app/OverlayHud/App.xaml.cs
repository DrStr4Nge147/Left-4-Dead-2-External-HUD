using System.Windows;
using OverlayHud.Services;

namespace OverlayHud;

public partial class App : Application
{
    private SingleInstanceGuard? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new SingleInstanceGuard();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.Dispose();
            _singleInstance = null;

            // The running copy has no window of its own, so a duplicate launch otherwise
            // looks like nothing happened. DefaultDesktopOnly keeps this readable when it is
            // triggered from over a fullscreen game, where there is no owner window to sit on.
            // Fully qualified: WinForms is referenced for NotifyIcon and brings its own
            // MessageBox into scope.
            System.Windows.MessageBox.Show(SingleInstanceGuard.AlreadyRunningMessage,
                                           AppIdentity.Name,
                                           MessageBoxButton.OK,
                                           MessageBoxImage.Information,
                                           MessageBoxResult.OK,
                                           System.Windows.MessageBoxOptions.DefaultDesktopOnly);

            Shutdown();
            return;
        }

        base.OnStartup(e);

        if (e.Args.Any(arg => string.Equals(arg, "--settings",
                                            StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is MainWindow main) main.ShowSettings();
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
