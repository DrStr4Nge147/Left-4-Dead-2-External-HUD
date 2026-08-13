using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using OverlayHud.Services;

namespace OverlayHud;

/// <summary>
/// Shows what the app is doing right now, and what it has done. Opened from the editor's
/// debug checkbox, and closable on its own - closing it is the same as unticking the box,
/// which is what <see cref="Window.Closed"/> is wired to in MainWindow.
/// </summary>
public partial class DebugWindow : Window
{
    private readonly Func<string> _summary;
    private readonly DispatcherTimer _refresh;

    public DebugWindow(Func<string> summary)
    {
        InitializeComponent();

        _summary = summary;
        Title = $"{AppIdentity.Name} - debug console";
        Icon = AppIcon.ForWindow();

        LogText.Text = string.Join(Environment.NewLine, DebugLog.Snapshot());
        ScrollToEnd();

        DebugLog.LineAdded += OnLineAdded;

        _refresh = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _refresh.Tick += (_, _) => SummaryText.Text = _summary();
        _refresh.Start();
        SummaryText.Text = _summary();

        Closed += (_, _) =>
        {
            DebugLog.LineAdded -= OnLineAdded;
            _refresh.Stop();
        };
    }

    /// <summary>
    /// Lines can arrive from any thread that logged one, so every append is marshalled.
    /// </summary>
    private void OnLineAdded(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnLineAdded(line));
            return;
        }

        LogText.AppendText(LogText.Text.Length == 0 ? line : Environment.NewLine + line);

        if (FollowCheckBox.IsChecked == true) ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        LogText.CaretIndex = LogText.Text.Length;
        LogText.ScrollToEnd();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Fully qualified: GlobalUsings pulls in WinForms for the tray icon, and both
            // assemblies define a Clipboard.
            System.Windows.Clipboard.SetText(LogText.Text);
            SaveStatus.Text = "Copied";
        }
        catch (Exception ex)
        {
            // The clipboard is shared and can be locked by another process mid-copy.
            SaveStatus.Text = $"Could not copy: {ex.Message}";
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory,
                                $"debug-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        try
        {
            var contents = new StringBuilder()
                .AppendLine(_summary())
                .AppendLine()
                .AppendLine(LogText.Text)
                .ToString();

            File.WriteAllText(path, contents);
            SaveStatus.Text = $"Saved to {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            SaveStatus.Text = $"Could not save: {ex.Message}";
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        DebugLog.Clear();
        LogText.Clear();
        SaveStatus.Text = "";
    }
}
