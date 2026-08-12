using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using OverlayHud.Interop;
using OverlayHud.Model;
using OverlayHud.Services;
using OverlayHud.ViewModel;

namespace OverlayHud;

public partial class MainWindow : Window
{
    private readonly AppConfig _cfg = AppConfig.Load();
    private readonly GameLifetimeState _gameLifetime = new();

    private StateReader _reader = null!;
    private KeyWatcher _keys = null!;
    private DispatcherTimer _geometry = null!;
    private System.Windows.Forms.NotifyIcon? _tray;
    private SettingsWindow? _settings;

    private Native.RECT _lastRect;
    private bool _gameForeground;
    private volatile bool _settingsActive;
    private bool _dirty = true;
    private int _lastCardCount = -1;

    // Live preview. The baseline is what the running overlay looked like before the editor
    // started pushing draft values at it, so Cancel can put it back without a save.
    private AppConfig? _livePreviewBaseline;
    private int _livePreviewSampleCount;
    private bool _livePreviewScoreboard;
    private readonly ScoreboardHold _scoreboard;
    private bool LivePreview => _livePreviewBaseline != null;

    // Layout is derived from the game window, so it has to survive a resolution change at
    // runtime rather than being computed once at startup.
    private double _surfaceWidth = SystemParameters.PrimaryScreenWidth;
    private double _surfaceHeight = SystemParameters.PrimaryScreenHeight;
    private double _fitScale = 1.0;

    public MainWindow()
    {
        InitializeComponent();

        // The command file lives beside the state file: the reader's resolved path once it
        // exists, the configured path before that, and only then a fresh lookup.
        _scoreboard = new ScoreboardHold(() => _reader?.Path
            ?? (string.IsNullOrWhiteSpace(_cfg.StatePath) ? StateLocator.Locate() : _cfg.StatePath));

        Panel.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);
        MenuBadge.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);
        Title = AppIdentity.Name;
        MenuBadgeText.Text = $"{AppIdentity.Name} v{DisplayVersion()}";

        ApplyLayout();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    // -----------------------------------------------------------------------
    // startup
    // -----------------------------------------------------------------------

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        MakeClickThrough();
        CreateTrayIcon();

        _reader = new StateReader(_cfg.StatePath, TimeSpan.FromMilliseconds(100),
                                  _cfg.StaleAfterSeconds);
        _reader.Updated += () => { _dirty = true; Render(); };
        _reader.Start();

        _keys = new KeyWatcher(_cfg.HoldKey, _cfg.EditorKey,
                               () => _gameForeground || _settingsActive
                                                     || _cfg.IgnoreForeground);
        _keys.HeldChanged += _ => Render();
        _keys.ShortcutPressed += () => Dispatcher.BeginInvoke(ToggleSettings);
        _keys.Start();

        // Normal priority for the same reason as the state poll - see StateReader.
        _geometry = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _geometry.Tick += (_, _) => TrackGameWindow();
        _geometry.Start();

        TrackGameWindow();
        Render();
    }

    /// <summary>
    /// Clicks pass straight through to the game, the window never takes focus, and it does
    /// not appear in alt-tab. Without WS_EX_NOACTIVATE the overlay can steal focus from a
    /// fullscreen game and minimise it.
    /// </summary>
    private void MakeClickThrough()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);

        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE,
            ex | Native.WS_EX_LAYERED
               | Native.WS_EX_TRANSPARENT
               | Native.WS_EX_NOACTIVATE
               | Native.WS_EX_TOOLWINDOW);
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add($"{AppIdentity.Name}  -  by {AppIdentity.Author}").Enabled = false;
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Customize UI...", null,
            (_, _) => Dispatcher.BeginInvoke(OpenSettings));
        menu.Items.Add("Open config folder", null,
            (_, _) => Process.Start(new ProcessStartInfo(AppContext.BaseDirectory)
                                    { UseShellExecute = true }));
        menu.Items.Add("Exit", null, (_, _) => Close());

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon.ForTray(),
            Text = AppIdentity.Name,
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    private void OpenSettings()
    {
        if (_settings != null)
        {
            if (_settings.WindowState == WindowState.Minimized)
                _settings.WindowState = WindowState.Normal;

            _settings.Activate();
            return;
        }

        _settings = new SettingsWindow(_cfg, ApplyUiConfig, UpdateLivePreview, EndLivePreview);
        _settings.Activated += (_, _) => _settingsActive = true;
        _settings.Deactivated += (_, _) => _settingsActive = false;
        _settings.Closed += (_, _) =>
        {
            _settingsActive = false;
            _settings = null;

            // A window closed by anything other than Save or Cancel - alt+F4, the taskbar,
            // app shutdown - still has to hand the overlay back its own settings.
            EndLivePreview(false);
        };
        _settings.Show();
        _settings.Activate();
    }

    /// <summary>
    /// The keyboard chord is a true toggle. Closing the editor follows the same path as
    /// Cancel, so its private draft is discarded and the live configuration is untouched.
    /// </summary>
    private void ToggleSettings()
    {
        if (_settings != null)
        {
            _settings.Close();
            return;
        }

        OpenSettings();
    }

    public void ShowSettings() => OpenSettings();

    /// <summary>
    /// Draws the running overlay with the editor's unsaved draft, over the real game window
    /// at its real geometry. Nothing is written to disk: the first call snapshots the live
    /// configuration so <see cref="EndLivePreview"/> can restore it.
    /// </summary>
    public void UpdateLivePreview(AppConfig draft, int sampleCount, bool showScoreboard = false)
    {
        _livePreviewBaseline ??= _cfg.Clone();
        _livePreviewSampleCount = Math.Max(0, sampleCount);

        _livePreviewScoreboard = showScoreboard;

        _cfg.CopyUiFrom(draft);

        // Before the layout pass, not after: the guides decide whether to mark the
        // scoreboard region from whether the ask actually reached the addon.
        UpdateScoreboardHold();
        ApplyUiConfig();
    }

    /// <summary>
    /// Leaves live preview. <paramref name="keepDraftValues"/> is true when the editor
    /// already saved the draft, so the values currently on screen are the real ones.
    /// </summary>
    public void EndLivePreview(bool keepDraftValues)
    {
        var baseline = _livePreviewBaseline;
        if (baseline == null) return;

        _livePreviewBaseline = null;
        _livePreviewScoreboard = false;
        _scoreboard.Release();

        if (!keepDraftValues) _cfg.CopyUiFrom(baseline);

        ApplyUiConfig();
    }

    /// <summary>
    /// Asks the addon to hold the scoreboard, and keeps asking: each call is also the
    /// heartbeat that tells the addon this app is still here. Focus is irrelevant now - the
    /// game holds its own scoreboard, so it stays up while the editor is being used.
    /// </summary>
    private void UpdateScoreboardHold() =>
        _scoreboard.Update(LivePreview && _livePreviewScoreboard);

    private void ApplyUiConfig()
    {
        Panel.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);
        MenuBadge.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);

        _fitScale = 1.0;
        _lastCardCount = -1;
        ApplyLayout();

        _dirty = true;
        Render();
    }

    // -----------------------------------------------------------------------
    // follow the game window
    // -----------------------------------------------------------------------

    private void TrackGameWindow()
    {
        _gameForeground = IsGameWindow(Native.GetForegroundWindow());
        UpdateScoreboardHold();

        // Geometry is tracked by process, not by focus. Following only the foreground
        // window means a resolution change made while alt-tabbed is missed, and the panel
        // comes back at the old size.
        var hwnd = FindGameWindow(out bool gameProcessRunning);

        if (_gameLifetime.ShouldExit(gameProcessRunning, _cfg.ExitWhenGameCloses))
        {
            Close();
            return;
        }

        if (hwnd != IntPtr.Zero && Native.GetWindowRect(hwnd, out var rect))
        {
            if (rect.Left != _lastRect.Left || rect.Top != _lastRect.Top ||
                rect.Width != _lastRect.Width || rect.Height != _lastRect.Height)
            {
                _lastRect = rect;
                ApplyBounds(rect);
            }
        }
        else if (_cfg.IgnoreForeground || LivePreview)
        {
            // No game window to follow - debug mode, or laying out with L4D2 closed. Cover
            // the primary screen so the panel still lands where it would in-game.
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            SetSurface(Width, Height);
        }

        Render();
    }

    private IntPtr FindGameWindow(out bool processRunning)
    {
        processRunning = false;
        Process[] processes;

        try
        {
            processes = Process.GetProcessesByName(_cfg.GameProcess);
        }
        catch
        {
            // Process list can change underneath us; next tick will retry. Do not treat an
            // enumeration failure as proof that the game closed.
            processRunning = _gameLifetime.HasObservedGame;
            return IntPtr.Zero;
        }

        try
        {
            processRunning = processes.Length > 0;

            foreach (var proc in processes)
            {
                if (proc.MainWindowHandle != IntPtr.Zero) return proc.MainWindowHandle;
            }
        }
        catch
        {
            // A process can exit between enumeration and reading MainWindowHandle. The
            // following poll will establish whether the process itself is really gone.
        }
        finally
        {
            foreach (var proc in processes) proc.Dispose();
        }

        return IntPtr.Zero;
    }

    private bool IsGameWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;

            using var proc = Process.GetProcessById((int)pid);
            return string.Equals(proc.ProcessName, _cfg.GameProcess,
                                 StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;   // process exited between the two calls
        }
    }

    /// <summary>Match the game window exactly, in DIPs rather than raw pixels.</summary>
    private void ApplyBounds(Native.RECT rect)
    {
        var source = PresentationSource.FromVisual(this);
        double sx = 1, sy = 1;

        if (source?.CompositionTarget != null)
        {
            var m = source.CompositionTarget.TransformFromDevice;
            sx = m.M11;
            sy = m.M22;
        }

        Left   = rect.Left * sx;
        Top    = rect.Top * sy;
        Width  = rect.Width * sx;
        Height = rect.Height * sy;

        SetSurface(Width, Height);
    }

    private void SetSurface(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        if (Math.Abs(width - _surfaceWidth) < 1 && Math.Abs(height - _surfaceHeight) < 1) return;

        _surfaceWidth = width;
        _surfaceHeight = height;

        _fitScale = 1.0;   // re-earn any shrink against the new size
        ApplyLayout();

        _dirty = true;     // column count depends on the height
    }

    /// <summary>
    /// Recomputes everything that depends on the size of the surface being drawn over:
    /// panel scale, and the anchor margins when offsets are given as percentages. Called
    /// whenever the game window changes size, so a resolution change is handled live.
    /// </summary>
    private void ApplyLayout()
    {
        double baseScale = BaseScale;
        double minFit = Math.Clamp(_cfg.MinScale, 0.1, 1.0);
        double maxFit = Math.Max(minFit, LayoutPolicy.MaxFitScale);
        double scale = baseScale * Math.Clamp(_fitScale, minFit, maxFit);

        PanelScale.ScaleX = PanelScale.ScaleY = scale;
        MenuBadgeScale.ScaleX = MenuBadgeScale.ScaleY = baseScale;

        // The badge has its own fixed corner: roster anchor settings must not move it.
        MenuBadge.Margin = new Thickness(0, _surfaceHeight * 0.025,
                                         _surfaceWidth * 0.02, 0);

        ApplyAnchor();
        UpdateGuides();
    }

    /// <summary>
    /// Marks the boundaries the editor used to simulate - the vanilla sidebar edge, the
    /// vertical start, and any bottom clearance - directly on the surface being drawn over.
    /// </summary>
    private void UpdateGuides()
    {
        Guides.Visibility = LivePreview ? Visibility.Visible : Visibility.Collapsed;
        if (!LivePreview) return;

        double sidebarEdge = _surfaceWidth * LayoutPolicy.SidebarWidthFraction;
        double top = Math.Max(0, VerticalOffset());
        double reserve = Math.Clamp(_cfg.BottomReserve, 0.0, 0.9) * _surfaceHeight;

        GuideSidebar.X1 = GuideSidebar.X2 = sidebarEdge;
        GuideSidebar.Y1 = top;
        GuideSidebar.Y2 = _surfaceHeight;

        GuideTop.X1 = 0;
        GuideTop.X2 = sidebarEdge;
        GuideTop.Y1 = GuideTop.Y2 = top;

        GuideReserve.Width = sidebarEdge;
        GuideReserve.Height = reserve;
        Canvas.SetTop(GuideReserve, _surfaceHeight - reserve);

        // Fallback only. When the addon is holding the real scoreboard open, a block would
        // cover the very thing being looked at; the marker is for when the ask could not be
        // delivered - no located install, or the addon not loaded.
        bool blockOut = _livePreviewScoreboard && !_scoreboard.IsHeld;
        var scoreboard = blockOut ? Visibility.Visible : Visibility.Collapsed;
        GuideScoreboard.Visibility = scoreboard;
        GuideScoreboardLabel.Visibility = scoreboard;
        GuideScoreboard.Width = sidebarEdge;
        GuideScoreboard.Height = Math.Max(1, top);
        Canvas.SetLeft(GuideScoreboardLabel, 14);
        Canvas.SetTop(GuideScoreboardLabel, Math.Max(0, top - 34));

        Canvas.SetLeft(GuideSidebarLabel, Math.Max(0, sidebarEdge - 92));
        Canvas.SetTop(GuideSidebarLabel, top + 4);
        Canvas.SetLeft(GuideTopLabel, 6);
        Canvas.SetTop(GuideTopLabel, Math.Max(0, top - 16));
    }

    private double BaseScale
    {
        get
        {
            double baseline = _cfg.BaselineHeight > 0 ? _cfg.BaselineHeight : 1080;
            double user = Math.Clamp(_cfg.Scale,
                                     LayoutPolicy.MinUserScale,
                                     LayoutPolicy.MaxUserScale);
            double auto = _cfg.AutoScale ? _surfaceHeight / baseline : 1.0;

            return user * auto;
        }
    }

    private static string DisplayVersion()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version == null) return "unknown";

        return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private double EffectiveScale => PanelScale.ScaleX <= 0 ? 1.0 : PanelScale.ScaleX;

    private void ApplyAnchor()
    {
        var anchor = _cfg.Anchor ?? "TopLeft";

        // Fully qualified: Window exposes HorizontalAlignment/VerticalAlignment as
        // properties, so the bare names resolve to those instead of the enum types.
        var horizontal = anchor.Contains("Left", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.HorizontalAlignment.Left
            : anchor.Contains("Right", StringComparison.OrdinalIgnoreCase)
                ? System.Windows.HorizontalAlignment.Right
                : System.Windows.HorizontalAlignment.Center;

        var vertical = anchor.StartsWith("Top", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.VerticalAlignment.Top
            : anchor.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase)
                ? System.Windows.VerticalAlignment.Bottom
                : System.Windows.VerticalAlignment.Center;

        Panel.HorizontalAlignment = horizontal;
        Panel.VerticalAlignment = vertical;

        // Percent offsets are the default: a pixel offset that clears the scoreboard at
        // 1920x1080 lands in the middle of the screen at 1280x720.
        double x = HorizontalOffset();
        double y = VerticalOffset();

        // Only the anchored edge gets its offset. Applying x/y to all four edges also
        // consumes room on the opposite side and makes WPF measure a smaller surface than
        // the fit calculation expects. Centre anchors keep symmetric inset behaviour.
        Panel.Margin = new Thickness(
            horizontal == System.Windows.HorizontalAlignment.Right ? 0 : x,
            vertical == System.Windows.VerticalAlignment.Bottom ? 0 : y,
            horizontal == System.Windows.HorizontalAlignment.Left ? 0 : x,
            vertical == System.Windows.VerticalAlignment.Top ? 0 : y);
    }

    private double HorizontalOffset() => _cfg.OffsetsArePercent
        ? _cfg.OffsetX * _surfaceWidth
        : _cfg.OffsetX;

    private double VerticalOffset() => _cfg.OffsetsArePercent
        ? _cfg.OffsetY * _surfaceHeight
        : _cfg.OffsetY;

    // -----------------------------------------------------------------------
    // render
    // -----------------------------------------------------------------------

    private bool ShouldShow()
    {
        // Live preview is its own reason to draw: the editor holds input focus, so neither
        // the hold key nor the foreground gate can be satisfied while it is open.
        if (LivePreview) return true;

        bool wanted = _cfg.AlwaysShow || (_keys?.IsHeld ?? false);
        bool infocus = _cfg.IgnoreForeground || _gameForeground;

        return wanted && infocus;
    }

    private void Render()
    {
        // Fresh exports mean an active round. No/frozen exports mean main menu, lobby,
        // loading, or pause; the transport cannot distinguish those inactive states.
        bool showMenuBadge = _cfg.ShowStatusBadge
                             && _gameForeground && (_reader == null || _reader.IsStale)
                             && !LivePreview;
        MenuBadge.Visibility = showMenuBadge ? Visibility.Visible : Visibility.Collapsed;

        var state = _reader?.Current;
        var survivors = state?.Survivors ?? new List<Survivor>();

        // The exporter's observed roster order is preserved; RosterPolicy decides which of
        // it belongs on the panel for the configured filter.
        var mode = RosterPolicy.Parse(_cfg.RosterFilter);
        var extras = RosterPolicy.Apply(survivors, mode);

        bool hasActiveExtras = !(_reader?.IsStale ?? true) && extras.Count > 0;

        // Nothing being exported is not the same as nobody to show. An empty panel with a
        // reason on it is recoverable; drawing nothing at all is indistinguishable from the
        // overlay being broken, which is exactly how it was reported. A healthy export with
        // an empty roster still draws nothing - that case is by design, from v0.2.0.
        bool nothingExporting = _reader == null || _reader.IsStale;
        bool show = ShouldShow()
                    && (hasActiveExtras || _cfg.AlwaysShow || LivePreview || nothingExporting);

        Panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;

        // Live preview redraws on every slider move, and those do not touch the reader.
        if (!_dirty && !LivePreview) return;
        _dirty = false;

        var cards = extras
            .Select(s => SurvivorCard.From(s, RosterPolicy.MarksFollower(s, mode)))
            .ToList();

        // Nothing exporting - menu, lobby, or L4D2 not running at all. Stand-in cards keep
        // the panel measurable so layout can still be tuned.
        bool usingSampleCards = LivePreview && !hasActiveExtras;
        if (usingSampleCards)
            cards = SampleRoster.Cards(_livePreviewSampleCount, mode != RosterMode.Followers);

        // A wider roster may force the panel smaller. Re-earn that shrink when the number
        // of cards changes, without re-laying out at full scale on every 100 ms poll.
        if (cards.Count != _lastCardCount)
        {
            _lastCardCount = cards.Count;
            _fitScale = 1.0;
            ApplyLayout();
        }

        HeaderText.Text = $"{RosterPolicy.Header(mode)}  {cards.Count}";
        Columns.ItemsSource = LayoutColumns(cards);
        FitToSurface();

        // With survivors on screen the status line stays out of the way and only reports
        // staleness. With nothing to draw it has to explain why, or the panel is a blank
        // box with no way to tell a missing addon from a missing game.
        if (usingSampleCards)
        {
            StatusText.Text = "LIVE PREVIEW - SAMPLE ROSTER";
        }
        else if (LivePreview)
        {
            StatusText.Text = "LIVE PREVIEW";
        }
        else if (_reader == null)
        {
            StatusText.Text = "";
        }
        else if (cards.Count > 0)
        {
            StatusText.Text = _reader.IsStale ? "STALE" : "";
        }
        else
        {
            // The most common cause by far, and the one the raw status cannot express: the
            // addon is not loaded, or its VPK was replaced while the game was running.
            var reason = _reader.IsStale
                ? "NO EXPORT - IS THE ADDON LOADED? RESTART L4D2 AFTER UPDATING ITS VPK.  "
                : "";
            var detail = _reader.Diagnostic.Length > 0 ? $"  ({_reader.Diagnostic})" : "";
            StatusText.Text = reason
                + ($"{_reader.Status} [polls {_reader.Polls}]" + detail).ToUpperInvariant();
        }
    }

    /// <summary>
    /// Measures a full-size single column first, using every available vertical pixel.
    /// Only a real height overflow (or a manual cardsPerColumn preference) enables the
    /// second column. The final fit pass handles width and extreme roster sizes.
    /// </summary>
    private List<List<SurvivorCard>> LayoutColumns(List<SurvivorCard> cards)
    {
        if (cards.Count == 0) return new List<List<SurvivorCard>>();

        int maxColumns = Math.Max(1, _cfg.MaxColumns);
        int columnCount;

        if (_cfg.CardsPerColumn > 0)
        {
            int needed = (int)Math.Ceiling(cards.Count / (double)_cfg.CardsPerColumn);
            columnCount = Math.Clamp(needed, 1, maxColumns);
        }
        else
        {
            var singleColumn = new List<List<SurvivorCard>> { cards };
            Columns.ItemsSource = singleColumn;

            double fullSizeHeight = LayoutMeasurement.NaturalSize(Panel).Height * BaseScale;
            columnCount = fullSizeHeight <= AvailableHeight() + 0.5
                ? 1
                : Math.Min(2, maxColumns);
        }

        int perColumn = (int)Math.Ceiling(cards.Count / (double)columnCount);
        var columns = new List<List<SurvivorCard>>(columnCount);

        for (int i = 0; i < cards.Count; i += perColumn)
        {
            columns.Add(cards.GetRange(i, Math.Min(perColumn, cards.Count - i)));
        }

        return columns;
    }

    /// <summary>
    /// Vertical room for a top-anchored sidebar. By default the panel can use the full
    /// height below the scoreboard because the vanilla survivor HUD is hidden with Tab;
    /// bottomReserve remains available for custom HUDs that need an exclusion zone.
    /// </summary>
    private double AvailableHeight()
    {
        double offset = VerticalOffset();

        double reserve = Math.Clamp(_cfg.BottomReserve, 0.0, 0.9) * _surfaceHeight;

        // An optional exclusion zone is defined for the scoreboard sidebar's Top*
        // anchors. Other anchors retain their conventional symmetric-margin fitting.
        if ((_cfg.Anchor ?? "TopLeft")
            .StartsWith("Top", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(1, _surfaceHeight - Math.Max(0, offset) - reserve);
        }

        return Math.Max(1, _surfaceHeight - Math.Max(0, offset) * 2);
    }

    private double AvailableWidth()
    {
        double offset = Math.Max(0, HorizontalOffset());
        var anchor = _cfg.Anchor ?? "TopLeft";

        bool edgeAnchored = anchor.Contains("Left", StringComparison.OrdinalIgnoreCase)
            || anchor.Contains("Right", StringComparison.OrdinalIgnoreCase);

        double insetCount = edgeAnchored ? 1 : 2;
        double surfaceRoom = Math.Max(1, _surfaceWidth - offset * insetCount);

        // The vanilla sidebar edge is fixed. Moving the overlay inward must reduce its
        // usable width, not extend the scoreboard region by the same amount.
        double sidebarRoom = Math.Max(1,
            _surfaceWidth * LayoutPolicy.SidebarWidthFraction - offset * insetCount);

        return Math.Min(surfaceRoom, sidebarRoom);
    }

    /// <summary>
    /// Measures what was actually laid out and scales it to use the available sidebar.
    /// Spare width can enlarge a short roster; overflow shrinks large two-column rosters.
    /// </summary>
    private void FitToSurface()
    {
        Size natural = LayoutMeasurement.NaturalSize(Panel);
        double w = natural.Width * EffectiveScale;
        double h = natural.Height * EffectiveScale;
        if (w <= 0 || h <= 0) return;

        double roomW = AvailableWidth();
        double roomH = AvailableHeight();

        double adjustment = Math.Min(roomW / w, roomH / h);
        double maxFit = Math.Max(Math.Clamp(_cfg.MinScale, 0.1, 1.0),
                                 LayoutPolicy.MaxFitScale);
        double nextFit = Math.Min(maxFit, _fitScale * adjustment);

        if (Math.Abs(nextFit - _fitScale) < 0.005) return;

        // ApplyLayout enforces the readability floor; this upper bound prevents a tiny
        // roster from becoming comically large merely because the sidebar is empty.
        _fitScale = nextFit;
        ApplyLayout();
    }

    // -----------------------------------------------------------------------

    private void OnClosed(object? sender, EventArgs e)
    {
        // First, and unconditionally: a scoreboard key still down after the overlay exits
        // would leave the game stuck showing it.
        _scoreboard.Release();

        _settings?.Close();
        _keys?.Dispose();
        _reader?.Dispose();
        _geometry?.Stop();

        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }
}
