using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OverlayHud.Model;
using OverlayHud.Services;
using OverlayHud.ViewModel;

namespace OverlayHud;

public partial class SettingsWindow : Window
{
    private const double PreviewWidth = 720;
    private const double PreviewHeight = 405;
    private const double PreviewBaselineHeight = 1080;

    private readonly AppConfig _live;
    private readonly Action _apply;
    private readonly Action<AppConfig, int, bool>? _startLivePreview;
    private readonly Action<bool>? _endLivePreview;
    private AppConfig _draft;
    private bool _ready;
    private bool _saved;
    private Rect _windowedBounds;
    private readonly DispatcherTimer _gameStatusTimer;

    public SettingsWindow(AppConfig config, Action apply,
                          Action<AppConfig, int, bool>? startLivePreview = null,
                          Action<bool>? endLivePreview = null)
    {
        InitializeComponent();

        _live = config;
        _draft = config.Clone();
        _apply = apply;
        _startLivePreview = startLivePreview;
        _endLivePreview = endLivePreview;

        Title = AppIdentity.Name;
        Icon = AppIcon.ForWindow();
        VersionText.Text = $"v{DisplayVersion()}";
        AuthorText.Text = $"by {AppIdentity.Author}";
        _gameStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _gameStatusTimer.Tick += (_, _) => UpdateGameStatus();
        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _gameStatusTimer.Stop();
            _endLivePreview?.Invoke(_saved);
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitToWorkArea();
        _windowedBounds = new Rect(Left, Top, Width, Height);

        // Says out loud what the scrollbar implies, and disappears once everything fits.
        ControlScroller.ScrollChanged += (_, _) => RefreshScrollHint();
        ControlScroller.SizeChanged += (_, _) => RefreshScrollHint();
        RefreshScrollHint();

        LoadControls();
        _ready = true;
        ApplyPreviewMode();   // reopens on the remembered preview, live included
        UpdateGameStatus();
        _gameStatusTimer.Start();
    }

    private void RefreshScrollHint()
    {
        bool scrollable = ControlScroller.ScrollableHeight > 1
                          && ControlScroller.VerticalOffset < ControlScroller.ScrollableHeight - 1;

        ScrollHint.Visibility = scrollable ? Visibility.Visible : Visibility.Collapsed;
        ScrollFade.Visibility = scrollable ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// The editor asks for a large preview. On a display that cannot give it that much
    /// room, shrink to fit rather than opening with controls off-screen.
    /// </summary>
    private void FitToWorkArea()
    {
        var work = SystemParameters.WorkArea;

        Width = Math.Max(MinWidth, Math.Min(Width, work.Width - 40));
        Height = Math.Max(MinHeight, Math.Min(Height, work.Height - 40));
        Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
        Top = work.Top + Math.Max(0, (work.Height - Height) / 2);
    }

    private void UpdateGameStatus()
    {
        bool running = GameProcessProbe.IsRunning(_draft.GameProcess);
        GameStatusText.Text = running ? "L4D2: RUNNING" : "L4D2: NOT RUNNING";
        GameStatusText.Foreground = running
            ? new SolidColorBrush(Color.FromRgb(0x62, 0xD2, 0x7B))
            : new SolidColorBrush(Color.FromRgb(0xF1, 0xB8, 0x5B));
    }

    private static string DisplayVersion()
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version;
        return version == null
            ? "unknown"
            : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private void LoadControls()
    {
        ScaleSlider.Value = Math.Clamp(_draft.Scale, ScaleSlider.Minimum, ScaleSlider.Maximum);
        OpacitySlider.Value = Math.Clamp(_draft.Opacity, OpacitySlider.Minimum, OpacitySlider.Maximum);
        OffsetXSlider.Value = Math.Clamp(_draft.OffsetX, OffsetXSlider.Minimum, OffsetXSlider.Maximum);
        OffsetYSlider.Value = Math.Clamp(_draft.OffsetY, OffsetYSlider.Minimum, OffsetYSlider.Maximum);
        BottomReserveSlider.Value = Math.Clamp(_draft.BottomReserve,
                                                BottomReserveSlider.Minimum,
                                                BottomReserveSlider.Maximum);
        MaxColumnsSlider.Value = Math.Clamp(_draft.MaxColumns, 1, 2);
        ExitWhenGameClosesCheckBox.IsChecked = _draft.ExitWhenGameCloses;
        AlwaysShowCheckBox.IsChecked = _draft.AlwaysShow;
        ShowStatusBadgeCheckBox.IsChecked = _draft.ShowStatusBadge;

        // The editor reopens on whichever preview was last used. This is safe to persist
        // because live preview only draws while this window is open.
        bool live = string.Equals(_draft.PreviewMode, "live", StringComparison.OrdinalIgnoreCase);
        LivePreviewRadio.IsChecked = live;
        SimulatedPreviewRadio.IsChecked = !live;
        ShowScoreboardCheckBox.IsChecked = _draft.PreviewScoreboard;

        LivePreviewRadio.IsEnabled = _startLivePreview != null;

        // Nothing for it to hold onto in the simulated preview, which has no real game.
        ShowScoreboardCheckBox.IsEnabled = live && _startLivePreview != null;

        var mode = RosterPolicy.Parse(_draft.RosterFilter);
        RosterAllRadio.IsChecked = mode == RosterMode.All;
        RosterSoldiersRadio.IsChecked = mode == RosterMode.SoldiersAndFollowers;
        RosterFollowersRadio.IsChecked = mode == RosterMode.Followers;

        if (PreviewCountSlider.Value < 1) PreviewCountSlider.Value = 6;

        RefreshLabels();
    }

    private void OnSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;

        ReadControls();
        RefreshLabels();
        RefreshPreview();
        SaveStatus.Text = "Unsaved changes";
    }

    private bool IsLivePreview => LivePreviewRadio.IsChecked == true && _startLivePreview != null;

    private void OnPreviewModeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        ApplyPreviewMode();
    }

    /// <summary>
    /// Live preview hands the whole preview job to the real overlay, so this window folds
    /// away its simulated canvas and moves off the sidebar the panel is drawn in.
    /// </summary>
    private void ApplyPreviewMode()
    {
        // Remembered immediately, on the live configuration as well as the draft: which
        // preview you were using is not one of the layout values Cancel throws away. The
        // live config is what reaches disk here, so an unsaved slider cannot ride along.
        string mode = LivePreviewRadio.IsChecked == true ? "live" : "simulated";
        bool scoreboard = ShowScoreboardCheckBox.IsChecked == true;

        _draft.PreviewMode = mode;
        _draft.PreviewScoreboard = scoreboard;
        ShowScoreboardCheckBox.IsEnabled = IsLivePreview;

        if (_live.PreviewMode != mode || _live.PreviewScoreboard != scoreboard)
        {
            _live.PreviewMode = mode;
            _live.PreviewScoreboard = scoreboard;
            _live.TrySave(out _);
        }

        if (IsLivePreview)
        {
            if (PreviewHost.Visibility == Visibility.Visible)
            {
                _windowedBounds = new Rect(Left, Top, Width, Height);
                PreviewHost.Visibility = Visibility.Collapsed;
                PreviewRow.Height = new GridLength(0);
                SizeToContent = SizeToContent.Height;
                DockClearOfPanel();
            }

            PushLivePreview();
            return;
        }

        if (PreviewHost.Visibility != Visibility.Visible)
        {
            SizeToContent = SizeToContent.Manual;
            PreviewHost.Visibility = Visibility.Visible;
            PreviewRow.Height = new GridLength(1, GridUnitType.Star);
            Left = _windowedBounds.Left;
            Top = _windowedBounds.Top;
            Width = _windowedBounds.Width;
            Height = _windowedBounds.Height;
        }

        _endLivePreview?.Invoke(false);
        RefreshPreview();
    }

    /// <summary>
    /// Keeps this window out of the top-left region the overlay draws in, so live preview
    /// is actually visible while it is being adjusted.
    /// </summary>
    private void DockClearOfPanel()
    {
        var work = SystemParameters.WorkArea;

        Width = Math.Min(Width, Math.Max(MinWidth, work.Width * 0.45));
        Left = Math.Max(work.Left, work.Right - Width - 24);
        Top = work.Top + 24;
    }

    private void PushLivePreview()
    {
        if (!IsLivePreview) return;

        _startLivePreview?.Invoke(_draft, (int)Math.Round(PreviewCountSlider.Value),
                                  ShowScoreboardCheckBox.IsChecked == true);
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        ReadControls();

        // The roster filter changes what the panel header says, so the preview has to
        // follow it as well as the sliders.
        RefreshPreview();
        SaveStatus.Text = "Unsaved changes";
    }

    private RosterMode SelectedRosterMode()
    {
        if (RosterSoldiersRadio.IsChecked == true) return RosterMode.SoldiersAndFollowers;
        if (RosterFollowersRadio.IsChecked == true) return RosterMode.Followers;

        return RosterMode.All;
    }

    private void ReadControls()
    {
        _draft.Scale = ScaleSlider.Value;
        _draft.Opacity = OpacitySlider.Value;
        _draft.OffsetUnits = "percent";
        _draft.OffsetX = OffsetXSlider.Value;
        _draft.OffsetY = OffsetYSlider.Value;
        _draft.BottomReserve = BottomReserveSlider.Value;
        _draft.MaxColumns = (int)Math.Round(MaxColumnsSlider.Value);
        _draft.ExitWhenGameCloses = ExitWhenGameClosesCheckBox.IsChecked == true;
        _draft.AlwaysShow = AlwaysShowCheckBox.IsChecked == true;
        _draft.ShowStatusBadge = ShowStatusBadgeCheckBox.IsChecked == true;
        _draft.RosterFilter = RosterPolicy.ToConfigValue(SelectedRosterMode());
    }

    private void RefreshLabels()
    {
        ScaleValue.Text = $"{ScaleSlider.Value:0.00}x";
        OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        OffsetXValue.Text = $"{OffsetXSlider.Value:P1}";
        OffsetYValue.Text = $"{OffsetYSlider.Value:P0}";
        BottomReserveValue.Text = $"{BottomReserveSlider.Value:P0}";
        MaxColumnsValue.Text = ((int)Math.Round(MaxColumnsSlider.Value)).ToString();
        PreviewCountValue.Text = ((int)Math.Round(PreviewCountSlider.Value)).ToString();
    }

    private void RefreshPreview()
    {
        // In live mode the real overlay is the preview, so every refresh becomes a push.
        if (IsLivePreview)
        {
            PushLivePreview();
            return;
        }

        var mode = RosterPolicy.Parse(_draft.RosterFilter);
        var cards = SampleRoster.Cards((int)Math.Round(PreviewCountSlider.Value),
                                       mode != RosterMode.Followers);

        // The simulated preview always draws its own scoreboard block - it is simulating the
        // game, and there is no real scoreboard for the checkbox to reach here.
        PreviewScoreboard.Visibility = Visibility.Visible;
        PreviewHeader.Text = $"{RosterPolicy.Header(RosterPolicy.Parse(_draft.RosterFilter))}  {cards.Count}";
        PreviewPanel.Opacity = Math.Clamp(_draft.Opacity, 0.1, 1.0);

        double sidebarEdge = PreviewWidth * LayoutPolicy.SidebarWidthFraction;
        double top = PreviewHeight * Math.Clamp(_draft.OffsetY, 0.0, 0.95);
        double left = PreviewWidth * Math.Clamp(_draft.OffsetX, 0.0, 0.5);
        double reserve = PreviewHeight * Math.Clamp(_draft.BottomReserve, 0.0, 0.9);
        double roomHeight = Math.Max(1, PreviewHeight - top - reserve);

        // This is the fixed vanilla boundary. Horizontal inset moves only PreviewPanel;
        // the remaining width budget shrinks by the same amount.
        PreviewSidebar.Width = sidebarEdge;
        PreviewScoreboard.Width = sidebarEdge;
        PreviewScoreboard.Height = Math.Max(1, top - 4);
        PreviewBottomReserve.Width = sidebarEdge;
        PreviewBottomReserve.Height = reserve;
        Canvas.SetTop(PreviewBottomReserve, PreviewHeight - reserve);

        double baseScale = (PreviewHeight / PreviewBaselineHeight) * _draft.Scale;

        var singleColumn = new List<List<SurvivorCard>> { cards };
        PreviewColumns.ItemsSource = singleColumn;
        PreviewPanelScale.ScaleX = PreviewPanelScale.ScaleY = baseScale;

        int maxColumns = Math.Max(1, _draft.MaxColumns);
        int columnCount;

        if (_draft.CardsPerColumn > 0)
        {
            int needed = (int)Math.Ceiling(cards.Count / (double)_draft.CardsPerColumn);
            columnCount = Math.Clamp(needed, 1, maxColumns);
        }
        else
        {
            double fullSizeHeight = LayoutMeasurement.NaturalSize(PreviewPanel).Height * baseScale;
            columnCount = fullSizeHeight <= roomHeight + 0.5
                ? 1
                : Math.Min(2, maxColumns);
        }

        PreviewColumns.ItemsSource = Split(cards, columnCount);

        Size natural = LayoutMeasurement.NaturalSize(PreviewPanel);
        double renderedWidth = natural.Width * baseScale;
        double renderedHeight = natural.Height * baseScale;
        double roomWidth = Math.Max(1, sidebarEdge - left);
        double adjustment = Math.Min(roomWidth / Math.Max(1, renderedWidth),
                                     roomHeight / Math.Max(1, renderedHeight));
        double minFit = Math.Clamp(_draft.MinScale, 0.1, 1.0);
        double maxFit = Math.Max(minFit, LayoutPolicy.MaxFitScale);
        double fit = Math.Clamp(adjustment, minFit, maxFit);

        PreviewPanelScale.ScaleX = PreviewPanelScale.ScaleY = baseScale * fit;
        Canvas.SetLeft(PreviewPanel, left);
        Canvas.SetTop(PreviewPanel, top);
    }

    private static List<List<SurvivorCard>> Split(List<SurvivorCard> cards, int columnCount)
    {
        int perColumn = (int)Math.Ceiling(cards.Count / (double)Math.Max(1, columnCount));
        var columns = new List<List<SurvivorCard>>(columnCount);

        for (int i = 0; i < cards.Count; i += perColumn)
        {
            columns.Add(cards.GetRange(i, Math.Min(perColumn, cards.Count - i)));
        }

        return columns;
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _draft.CopyUiFrom(new AppConfig());
        _ready = false;
        PreviewCountSlider.Value = 6;
        LoadControls();
        _ready = true;
        ReadControls();
        RefreshPreview();
        SaveStatus.Text = "Default UI loaded — save to apply";
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ReadControls();

        if (!_draft.TrySave(out string error))
        {
            SaveStatus.Text = error;
            return;
        }

        _live.CopyUiFrom(_draft);

        // Deliberately outside CopyUiFrom, like exitWhenGameCloses: Reset UI restores visual
        // layout, and neither of these is layout.
        _live.ExitWhenGameCloses = _draft.ExitWhenGameCloses;
        _live.AlwaysShow = _draft.AlwaysShow;
        _live.PreviewMode = _draft.PreviewMode;
        _live.PreviewScoreboard = _draft.PreviewScoreboard;
        _apply();

        // The values on screen are now the saved ones, so live preview must not roll the
        // overlay back to the pre-edit baseline when this window closes.
        _saved = true;
        Close();
    }
}
