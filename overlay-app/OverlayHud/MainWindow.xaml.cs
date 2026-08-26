using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    /// <summary>The 20 Hz ammunition channel. Null until startup finishes.</summary>
    private AmmoReader? _ammo;
    private KeyWatcher _keys = null!;
    private DispatcherTimer _geometry = null!;
    private System.Windows.Forms.NotifyIcon? _tray;
    private SettingsWindow? _settings;
    private DebugWindow? _debug;
    private System.Windows.Forms.ToolStripMenuItem? _debugMenuItem;

    private Native.RECT _lastRect;
    private VersionCheck _version;
    private bool _gameForeground;

    /// <summary>
    /// Whether L4D2 is showing one of its own menus. A seam, not a preference: the answer
    /// comes from the desktop cursor, which is global state a check cannot arrange - the
    /// developer's own cursor is visible while the checks run, so a direct call made every
    /// roster-drawing check fail at once. Production keeps the real probe.
    /// </summary>
    private Func<bool> _menuProbe = GameMenuProbe.CursorVisible;
    private volatile bool _settingsActive;
    private bool _dirty = true;
    private int _lastCardCount = -1;
    private bool _separatedYouVisible;

    // Live preview. The baseline is what the running overlay looked like before the editor
    // started pushing draft values at it, so Cancel can put it back without a save.
    private AppConfig? _livePreviewBaseline;
    private int _livePreviewSampleCount;
    private bool _livePreviewScoreboard;
    private bool _livePreviewConsistent;
    private readonly ScoreboardHold _scoreboard;
    private bool LivePreview => _livePreviewBaseline != null;

    /// <summary>
    /// True while the hold key is down. Mirrored from the watcher so the presentation can
    /// be reasoned about - and tested - without a real keyboard hook.
    /// </summary>
    private bool _holdKeyDown;

    /// <summary>
    /// Whether the hold key is down, preferring the watcher's own state and falling back to
    /// the mirrored flag. Two sources because they fail differently: the flag is only as
    /// current as the last event delivered, and the watcher does not exist until startup
    /// finishes.
    /// </summary>
    private bool HoldKeyDown => (_keys?.IsHeld ?? false) || _holdKeyDown;

    /// <summary>
    /// The consistent HUD steps aside while the hold key is down.
    ///
    /// L4D2 hides its own survivor HUD and draws the scoreboard while Tab is held, and this
    /// follows it: holding Tab gives the scoreboard panel, releasing gives the persistent
    /// HUD back - if it was turned on at all. Without this the two draw over each other,
    /// which is the one moment the roster is being read carefully.
    /// </summary>
    private bool ConsistentMode => _livePreviewConsistent
        || (!LivePreview && _cfg.AlwaysShow && !HoldKeyDown);

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

        Panel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        ConsistentYouPanel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        StatusStack.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);
        Title = AppIdentity.Name;
        MenuBadgeText.Text = $"{AppIdentity.Name} v{DisplayVersion()}";

        ApplyLayout();
        StartBlackAndWhitePulse();

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    /// <summary>
    /// Runs the shared black-and-white outline colour for the life of the app.
    ///
    /// It has to be started here, on the resource, rather than from a storyboard inside the
    /// card template: cards are rebuilt from scratch on every poll, so a per-card animation
    /// is discarded and restarted about five times a second and never gets far enough from
    /// its start value to be seen. One brush, one clock, every marked card in step.
    ///
    /// The animation is on a shared resource, so it is deliberately never stopped.
    /// </summary>
    private void StartBlackAndWhitePulse()
    {
        if (Application.Current?.Resources["BwPulseBrush"] is not SolidColorBrush brush) return;
        if (brush.IsFrozen) return;

        var pulse = new ColorAnimation
        {
            From = Color.FromRgb(0xFF, 0x60, 0x60),
            To = Color.FromRgb(0x5A, 0x18, 0x18),
            Duration = TimeSpan.FromSeconds(0.55),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            // Linear reads as a strobe; easing makes it breathe, which is easier to have on
            // screen for a whole round.
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        brush.BeginAnimation(SolidColorBrush.ColorProperty, pulse);
    }

    // -----------------------------------------------------------------------
    // startup
    // -----------------------------------------------------------------------

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        MakeClickThrough();
        CreateTrayIcon();

        DebugLog.Write("startup", $"{AppIdentity.Name} v{DisplayVersion()}");
        DebugLog.Write("startup", $"config: {AppConfig.ConfigPath}");
        DebugLog.Write("startup",
            $"game process: {_cfg.GameProcess} | hold key: 0x{_cfg.HoldKey:X2} | " +
            $"editor key: 0x{_cfg.EditorKey:X2} | exporter proven before: {_cfg.ExporterProven}");

        _reader = new StateReader(_cfg.StatePath, TimeSpan.FromMilliseconds(100),
                                  _cfg.StaleAfterSeconds);
        _reader.Updated += () => { _dirty = true; Render(); };
        _reader.Start();

        // The ammunition channel runs four times faster than the roster and redraws nothing
        // but the two weapon slots. Half a second of staleness is generous for a 20 Hz
        // writer and keeps a brief hitch from dropping back to the coarse numbers.
        _ammo = new AmmoReader(_reader.Path, TimeSpan.FromMilliseconds(50), 0.5);
        _ammo.Updated += OnAmmoTick;
        _ammo.Start();

        DebugLog.Write("state", _reader.Path == null
            ? "no state file located - is L4D2 installed where Steam says?"
            : $"watching {_reader.Path}");

        if (_cfg.Debug) ShowDebugConsole(true);

        _keys = new KeyWatcher(_cfg.HoldKey, _cfg.EditorKey,
                               () => _gameForeground || _settingsActive
                                                     || _cfg.IgnoreForeground,
                               _cfg.ConsistentKey,
                               () => _gameForeground || _cfg.IgnoreForeground);
        // Both events are already marshalled onto this thread by the watcher, which keeps
        // the hook callback itself short enough to survive LowLevelHooksTimeout.
        _keys.HeldChanged += held =>
        {
            _holdKeyDown = held;

            // The whole presentation changes here - panel chrome, anchor, scale, and which
            // of the three HUD elements are drawn - so the layout has to be recomputed
            // rather than just redrawn. The fit scale is re-earned because the scoreboard
            // panel and the consistent grid are different sizes.
            _fitScale = 1.0;
            _dirty = true;
            ApplyLayout();
            Render();
        };
        _keys.ShortcutPressed += ToggleSettings;
        _keys.TogglePressed += ToggleConsistentHud;
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

    /// <summary>
    /// Puts the overlay back on top of the game. WS_EX_NOACTIVATE plus SWP_NOACTIVATE means
    /// this cannot pull focus away from the game while doing it.
    /// </summary>
    private void RaiseAboveGame()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add($"{AppIdentity.Name}  -  by {AppIdentity.Author}").Enabled = false;
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Customize UI...", null,
            (_, _) => Dispatcher.BeginInvoke(OpenSettings));

        // Reachable without the editor: the console is most wanted when nothing is drawing,
        // and the editor is one more thing that could be failing at that moment.
        var debugItem = new System.Windows.Forms.ToolStripMenuItem("Debug console")
        {
            CheckOnClick = true,
            Checked = _cfg.Debug
        };
        debugItem.CheckedChanged += (_, _) =>
            Dispatcher.BeginInvoke(() => ShowDebugConsole(debugItem.Checked));
        menu.Items.Add(debugItem);
        _debugMenuItem = debugItem;
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

        _settings = new SettingsWindow(_cfg, ApplyUiConfig, UpdateLivePreview, EndLivePreview,
                                       ShowDebugConsole);
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

    /// <summary>
    /// The persistent HUD hotkey changes the same preference exposed by the editor. Saving it
    /// means the user's choice survives a restart, while the next render changes immediately.
    /// The hook gate only enables this while the game is in front, so a key pressed in the
    /// editor cannot accidentally switch the live HUD behind it.
    /// </summary>
    private void ToggleConsistentHud()
    {
        _cfg.AlwaysShow = !_cfg.AlwaysShow;
        _cfg.TrySave(out string error);

        DebugLog.Write("input", _cfg.AlwaysShow
            ? "consistent HUD enabled by hotkey"
            : "consistent HUD disabled by hotkey");
        if (error.Length > 0) DebugLog.Write("config", error);

        _settings?.SetConsistentHudChecked(_cfg.AlwaysShow);
        _fitScale = 1.0;
        _lastCardCount = -1;
        ApplyLayout();
        _dirty = true;
        Render();
    }

    public void ShowSettings() => OpenSettings();

    /// <summary>
    /// Opens or closes the debug console. Every route in and out - the editor checkbox, the
    /// tray item, the window's own close button - lands here, so the two indicators and the
    /// saved setting cannot drift apart.
    /// </summary>
    private void ShowDebugConsole(bool on)
    {
        if (on == (_debug != null))
        {
            SyncDebugIndicators(on);
            return;
        }

        if (!on)
        {
            var closing = _debug;
            _debug = null;
            closing?.Close();
            SyncDebugIndicators(false);
            return;
        }

        _debug = new DebugWindow(DebugSummary);
        _debug.Closed += (_, _) =>
        {
            // Closed from its own title bar: that is the user turning it off.
            if (_debug == null) return;

            _debug = null;
            _cfg.Debug = false;
            _cfg.TrySave(out _);
            SyncDebugIndicators(false);
        };
        _debug.Show();
        SyncDebugIndicators(true);
    }

    private void SyncDebugIndicators(bool on)
    {
        if (_debugMenuItem != null && _debugMenuItem.Checked != on) _debugMenuItem.Checked = on;

        _settings?.SetDebugChecked(on);
    }

    /// <summary>
    /// The console's top block: what someone actually needs to answer "is this working".
    /// Read live rather than logged, because these are the current values.
    /// </summary>
    private string DebugSummary()
    {
        string exporting = _reader == null
            ? "no reader"
            : _reader.IsStale
                ? _reader.HasExported ? "stopped (menu, load, or paused)" : "nothing seen yet"
                : "live";

        string path = _reader?.Path ?? "not located";
        string status = string.IsNullOrEmpty(_reader?.Status) ? "ok" : _reader!.Status;
        string diagnostic = string.IsNullOrEmpty(_reader?.Diagnostic) ? "" : $"  |  {_reader!.Diagnostic}";
        int survivors = _reader?.Current?.Survivors.Count ?? 0;

        return string.Join(Environment.NewLine, new[]
        {
            $"exporter    {exporting}   status: {status}{diagnostic}",
            $"versions    app v{_version.AppVersion}   addon " +
                $"{(_version.AddonVersion.Length > 0 ? $"v{_version.AddonVersion}" : "unknown")}" +
                $"   {_version.Verdict}",
            $"state file  {path}",
            $"polls       {_reader?.Polls ?? 0}   survivors in last read: {survivors}   " +
                $"proven: {(_cfg.ExporterProven ? "yes" : "no")}",
            $"game        {_cfg.GameProcess}   foreground: {(_gameForeground ? "yes" : "no")}   " +
                $"window: {_lastRect.Width}x{_lastRect.Height}",
            $"input       hold key down: {(_keys?.IsHeld == true ? "yes" : "no")}   " +
                $"hook reinstalls: {_keys?.Recoveries ?? 0}",
            $"panel       {(Panel.Visibility == Visibility.Visible ? "drawing" : "hidden")}   " +
                $"scale: {PanelScale.ScaleX:0.000}   surface: {_surfaceWidth:0}x{_surfaceHeight:0}",

            // The two raw reads sit beside the verdict on purpose: if an outro ever fails
            // to hide the panel, which of them moved is the whole question.
            $"cinematic   {((_reader?.Current?.Cinematic ?? 0) == 1 ? "yes" : "no")}   " +
                $"hideHud bits: {_reader?.Current?.HideHudBits ?? -1}   " +
                $"view camera: {_reader?.Current?.ViewCamera ?? -1}   " +
                $"frozen: {_reader?.Current?.Frozen ?? -1}   " +
                $"finale won: {_reader?.Current?.FinaleWon ?? -1}   " +
                $"cursor: {(_menuProbe() ? "shown (menu)" : "hidden (play)")}   " +
                $"hiding: {(_cfg.HideDuringCinematics ? "on" : "off")}"
        });
    }

    /// <summary>
    /// Draws the running overlay with the editor's unsaved draft, over the real game window
    /// at its real geometry. Nothing is written to disk: the first call snapshots the live
    /// configuration so <see cref="EndLivePreview"/> can restore it.
    /// </summary>
    public void UpdateLivePreview(AppConfig draft, int sampleCount, bool showScoreboard = false,
                                  bool consistentHud = false)
    {
        _livePreviewBaseline ??= _cfg.Clone();
        _livePreviewSampleCount = Math.Max(0, sampleCount);

        _livePreviewScoreboard = showScoreboard;
        _livePreviewConsistent = consistentHud;

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
        _livePreviewConsistent = false;
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
        _keys?.SetToggleKey(_cfg.ConsistentKey);
        Panel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        ConsistentYouPanel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        StatusStack.Opacity = Math.Clamp(_cfg.Opacity, 0.1, 1.0);

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
        bool wasForeground = _gameForeground;
        _gameForeground = IsGameWindow(Native.GetForegroundWindow());
        UpdateScoreboardHold();

        // Windows can drop the keyboard hook without telling anyone; this is the only place
        // that finds out. See KeyWatcher.Pulse.
        _keys?.Pulse();

        // Alt-tabbing back into a fullscreen game puts its window above ours, and topmost
        // alone does not survive that. Re-assert on the way back in rather than every tick.
        if (_gameForeground && !wasForeground)
        {
            RaiseAboveGame();
            DebugLog.Write("focus", "game came forward - overlay re-asserted topmost");
        }
        else if (!_gameForeground && wasForeground)
        {
            DebugLog.Write("focus", "game lost focus");
        }

        // Geometry is tracked by process, not by focus. Following only the foreground
        // window means a resolution change made while alt-tabbed is missed, and the panel
        // comes back at the old size.
        var hwnd = FindGameWindow(out bool gameProcessRunning);

        DebugLog.Note("process", "game",
            gameProcessRunning ? $"{_cfg.GameProcess} is running" : $"{_cfg.GameProcess} not running");

        if (_gameLifetime.ShouldExit(gameProcessRunning, _cfg.ExitWhenGameCloses))
        {
            DebugLog.Write("game", "game closed and exitWhenGameCloses is on - exiting");
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

        DebugLog.Write("layout",
            $"surface {_surfaceWidth:0}x{_surfaceHeight:0} -> {width:0}x{height:0}");

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
        ApplyPanelMode();

        double baseScale = BaseScale;
        double minFit = Math.Clamp(_cfg.MinScale, 0.1, 1.0);
        double maxFit = Math.Max(minFit, LayoutPolicy.MaxFitScale);
        double scale = baseScale * Math.Clamp(_fitScale, minFit, maxFit);

        PanelScale.ScaleX = PanelScale.ScaleY = scale;
        // Keep the status badge and its notice at native size. The survivor panel can be
        // scaled for the scoreboard or Consistent HUD, but the setup/version message must
        // remain readable and must not change size with either layout.
        MenuBadgeScale.ScaleX = MenuBadgeScale.ScaleY = 1.0;
        ConsistentYouPanelScale.ScaleX = ConsistentYouPanelScale.ScaleY = scale;
        // The weapon HUD carries the consistent HUD's scale times its own multiplier: it
        // belongs to that presentation, but ammunition is read mid-fight and often wants
        // to be a different size from the roster beside it.
        double weaponScale = scale * WeaponPanelPolicy.ClampScale(_cfg.WeaponPanelScale);
        WeaponPanelScale.ScaleX = WeaponPanelScale.ScaleY = weaponScale;

        // The badge and its notice have their own fixed corner: roster anchor settings must
        // not move them.
        StatusStack.Margin = new Thickness(0, _surfaceHeight * 0.025,
                                           _surfaceWidth * 0.02, 0);

        ApplyAnchor();
        ApplyYouLayout();
        ApplyWeaponPanelLayout();
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

        if (_livePreviewConsistent)
        {
            GuideScoreboard.Visibility = Visibility.Collapsed;
            GuideScoreboardLabel.Visibility = Visibility.Collapsed;
            GuideReserve.Visibility = Visibility.Collapsed;
            GuideSidebar.Visibility = Visibility.Collapsed;
            GuideTop.Visibility = Visibility.Collapsed;
            GuideSidebarLabel.Visibility = Visibility.Collapsed;
            GuideTopLabel.Visibility = Visibility.Collapsed;
            return;
        }

        GuideReserve.Visibility = Visibility.Visible;
        GuideSidebar.Visibility = Visibility.Visible;
        GuideTop.Visibility = Visibility.Visible;
        GuideSidebarLabel.Visibility = Visibility.Visible;
        GuideTopLabel.Visibility = Visibility.Visible;

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
            double user = Math.Clamp(ConsistentMode ? _cfg.ConsistentScale : _cfg.Scale,
                                     LayoutPolicy.MinUserScale,
                                     LayoutPolicy.MaxUserScale);
            double auto = _cfg.AutoScale ? _surfaceHeight / baseline : 1.0;

            return user * auto;
        }
    }

    private static string DisplayVersion() => AppIdentity.DisplayVersion;

    private double EffectiveScale => PanelScale.ScaleX <= 0 ? 1.0 : PanelScale.ScaleX;

    /// <summary>
    /// Draws the local player's weapon slots, or hides the panel.
    ///
    /// Hidden rather than emptied when there is nothing to say: no local marker (a dedicated
    /// server, or an exporter older than 1.2.0), no weapon fields (an exporter older than
    /// 2.0.0), or genuinely empty hands. An empty bordered box in the corner of the screen
    /// looks like a bug in a way that an absent one does not.
    /// </summary>
    /// <summary>
    /// Redraws only the weapon slots, on the ammunition channel's own tick. The roster is
    /// not touched: this fires four times per roster update, and rebuilding every survivor
    /// card to move one number would be the expensive way to do it.
    /// </summary>
    private void OnAmmoTick()
    {
        if (_weaponSurvivor == null || WeaponPanel.Visibility != Visibility.Visible) return;

        RenderWeaponPanel(_weaponSurvivor);
    }

    private Survivor? _weaponSurvivor;

    private void RenderWeaponPanel(Survivor? survivor)
    {
        bool wanted = ConsistentMode && _cfg.ConsistentShowWeapons && survivor != null;

        _weaponSurvivor = wanted ? survivor : null;

        var slots = wanted
            ? SurvivorCard.WeaponChip.SlotsFor(
                WithLiveAmmo(survivor!),
                WeaponPanelPolicy.IsHorizontal(_cfg.WeaponPanelOrientation))
            : Array.Empty<SurvivorCard.WeaponChip>();

        var items = wanted
            ? SurvivorCard.ItemChip.SlotsFor(survivor!)
            : Array.Empty<SurvivorCard.ItemChip>();

        // Bare hands and empty pockets is the one case with nothing to say. Carrying only
        // pills still draws the panel: the item row is what the cards used to show.
        if (slots.Count == 0 && !items.Any(item => item.HasItem))
        {
            WeaponPanel.Visibility = Visibility.Collapsed;
            WeaponSlots.ItemsSource = null;
            WeaponItems.ItemsSource = null;
            return;
        }

        WeaponSlots.ItemsPanel = SlotPanelTemplate(
            WeaponPanelPolicy.IsHorizontal(_cfg.WeaponPanelOrientation));
        WeaponSlots.ItemsSource = slots;
        WeaponSlots.Visibility = slots.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        WeaponItems.ItemsSource = items;
        WeaponItems.Margin = new Thickness(0, slots.Count > 0 ? 6 : 0, 0, 0);
        WeaponPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// The roster snapshot with its ammunition counts replaced by the fast channel's, when
    /// that channel is live. Which weapons are being carried still comes from the roster:
    /// only the numbers move fast enough to need their own transport.
    ///
    /// Falls through untouched when the channel is absent, stale, or older than the addon
    /// that writes it, which is what makes this safe against an out-of-date exporter.
    /// </summary>
    private Survivor WithLiveAmmo(Survivor survivor)
    {
        if (_ammo == null || !_ammo.IsFresh) return survivor;

        return new Survivor
        {
            Uid = survivor.Uid,
            Name = survivor.Name,
            Team = survivor.Team,
            Character = survivor.Character,
            IsLocal = survivor.IsLocal,
            Cls = survivor.Cls,
            Bot = survivor.Bot,
            Hp = survivor.Hp,
            MaxHp = survivor.MaxHp,
            Temp = survivor.Temp,
            State = survivor.State,
            Revives = survivor.Revives,
            BlackAndWhite = survivor.BlackAndWhite,
            Kit = survivor.Kit,
            Pill = survivor.Pill,
            Throwable = survivor.Throwable,
            Primary = survivor.Primary,
            Secondary = survivor.Secondary,
            Weapon = survivor.Weapon,
            ActiveSlot = survivor.ActiveSlot,
            PrimaryAmmoKind = _ammo.PrimaryAmmoKind,
            PrimaryUpgradedLeft = _ammo.PrimaryUpgradedLeft,
            PrimaryClip = _ammo.PrimaryClip,
            PrimaryReserve = _ammo.PrimaryReserve,
            SecondaryClip = _ammo.SecondaryClip
        };
    }

    /// <summary>
    /// The slot arrangement. Built once per orientation and reused: assigning a fresh
    /// ItemsPanelTemplate throws away and rebuilds every container, and this runs on the
    /// same 100 ms tick as everything else.
    /// </summary>
    private static ItemsPanelTemplate SlotPanelTemplate(bool horizontal)
    {
        if (horizontal)
        {
            return _horizontalSlots ??=
                BuildSlotPanel(System.Windows.Controls.Orientation.Horizontal);
        }

        return _verticalSlots ??=
            BuildSlotPanel(System.Windows.Controls.Orientation.Vertical);
    }

    private static ItemsPanelTemplate? _verticalSlots;
    private static ItemsPanelTemplate? _horizontalSlots;

    private static ItemsPanelTemplate BuildSlotPanel(
        System.Windows.Controls.Orientation orientation)
    {
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty, orientation);

        var template = new ItemsPanelTemplate(factory);
        template.Seal();
        return template;
    }

    private double ActivePanelOpacity() => ConsistentMode ? _cfg.ConsistentOpacity : _cfg.Opacity;

    private void ApplyPanelMode()
    {
        bool consistent = ConsistentMode;
        ConsistentHudDesign.SetDesign(Root, _cfg.ConsistentDesign);
        Panel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        ScoreboardContent.Visibility = consistent ? Visibility.Collapsed : Visibility.Visible;
        ConsistentContent.Visibility = consistent ? Visibility.Visible : Visibility.Collapsed;
        ConsistentYouPanel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);
        WeaponPanel.Opacity = Math.Clamp(ActivePanelOpacity(), 0.1, 1.0);

        if (!consistent)
        {
            _separatedYouVisible = false;
            ConsistentYouPanel.Visibility = Visibility.Collapsed;
            ConsistentYouCards.ItemsSource = null;
            WeaponPanel.Visibility = Visibility.Collapsed;
            WeaponSlots.ItemsSource = null;
        }

        if (consistent)
        {
            Panel.Padding = new Thickness(0);
            Panel.Background = Brushes.Transparent;
            Panel.BorderBrush = Brushes.Transparent;
            Panel.BorderThickness = new Thickness(0);
        }
        else
        {
            Panel.Padding = new Thickness(10, 8, 10, 8);
            Panel.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0x05, 0x07, 0x0A));
            Panel.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            Panel.BorderThickness = new Thickness(1);
        }
    }

    private void ApplyAnchor()
    {
        var anchor = ConsistentMode ? ConsistentRosterAnchor() : (_cfg.Anchor ?? "TopLeft");

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

    /// <summary>
    /// With Separate You enabled, the bottom horizontal roster occupies the lower-left
    /// side so the independent local-player card has the lower-right side to itself.
    /// The other templates keep their named roster anchor.
    /// </summary>
    private string ConsistentRosterAnchor()
    {
        var template = ConsistentHudPolicy.Parse(_cfg.ConsistentTemplate);
        if (_separatedYouVisible && template == ConsistentHudPolicy.VanillaBottomCenter)
            return "BottomLeft";

        return ConsistentHudPolicy.For(template).Anchor;
    }

    /// <summary>
    /// The optional local-player card has its own root-level anchor so the selected roster
    /// template and its spacing cannot move it. Lower-right vertical mirrors it to the
    /// lower-left because the roster already owns the lower-right side.
    /// </summary>
    private void ApplyYouLayout()
    {
        if (!ConsistentMode)
        {
            ConsistentYouPanelScale.ScaleX = ConsistentYouPanelScale.ScaleY = 1.0;
            return;
        }

        var placement = ConsistentHudPolicy.For(_cfg.ConsistentTemplate);
        bool youOnLeft = _separatedYouVisible
            && ConsistentHudPolicy.Parse(_cfg.ConsistentTemplate)
                == ConsistentHudPolicy.LowerRightVertical;
        ConsistentYouPanel.HorizontalAlignment = youOnLeft
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
        ConsistentYouPanel.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
        ConsistentYouPanel.Margin = new Thickness(
            youOnLeft ? placement.HorizontalInset * _surfaceWidth : 0,
            0,
            youOnLeft ? 0 : placement.HorizontalInset * _surfaceWidth,
            Math.Clamp(_cfg.ConsistentVerticalOffset, 0.0, 0.90) * _surfaceHeight);
    }

    /// <summary>
    /// The weapon HUD's own corner. It shares the consistent HUD's scale and opacity - it
    /// is part of that presentation - but not its placement: the roster wants to be out of
    /// the way, and this wants to be where ammunition is normally read.
    /// </summary>
    private void ApplyWeaponPanelLayout()
    {
        if (!ConsistentMode)
        {
            WeaponPanelScale.ScaleX = WeaponPanelScale.ScaleY = 1.0;
            return;
        }

        bool left = WeaponPanelPolicy.IsLeft(_cfg.WeaponPanelCorner);
        double inset = WeaponPanelPolicy.HorizontalInset * _surfaceWidth;

        WeaponPanel.HorizontalAlignment = left
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
        // The item row is narrower than a horizontal pair of weapon slots, so it takes the
        // panel's own edge rather than floating in the middle of it.
        WeaponItems.HorizontalAlignment = left
            ? System.Windows.HorizontalAlignment.Left
            : System.Windows.HorizontalAlignment.Right;
        WeaponPanel.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
        WeaponPanel.Margin = new Thickness(
            left ? inset : 0,
            0,
            left ? 0 : inset,
            WeaponPanelPolicy.ClampVerticalOffset(_cfg.WeaponPanelVerticalOffset)
                * _surfaceHeight);
    }

    private double HorizontalOffset() => _cfg.OffsetsArePercent
        ? (ConsistentMode
            ? ConsistentHudPolicy.For(_cfg.ConsistentTemplate).HorizontalInset * _surfaceWidth
            : _cfg.OffsetX * _surfaceWidth)
        : (ConsistentMode
            ? ConsistentHudPolicy.For(_cfg.ConsistentTemplate).HorizontalInset * _surfaceWidth
            : _cfg.OffsetX);

    private double VerticalOffset() => _cfg.OffsetsArePercent
        ? (ConsistentMode
            ? Math.Clamp(_cfg.ConsistentVerticalOffset, 0.0, 0.90) * _surfaceHeight
            : _cfg.OffsetY * _surfaceHeight)
        : (ConsistentMode
            ? Math.Clamp(_cfg.ConsistentVerticalOffset, 0.0, 0.90) * _surfaceHeight
            : _cfg.OffsetY);

    // -----------------------------------------------------------------------
    // render
    // -----------------------------------------------------------------------

    /// <summary>
    /// True once this install has been seen exporting, now or in an earlier session. The
    /// remembered half is the point: a working setup should not be told it might be broken
    /// every time the app starts.
    /// </summary>
    private bool ExporterProven => _cfg.ExporterProven || (_reader?.HasExported ?? false);

    /// <summary>
    /// Records what the exporter is doing, and remembers the first time it is seen working.
    /// The write happens once per install rather than once per run, so the config file is
    /// not touched on every launch.
    /// </summary>
    private void TrackExporterHealth()
    {
        if (_reader == null) return;

        DebugLog.Note("exporter", "state", _reader.IsStale
            ? _reader.HasExported
                ? "export stopped - menu, loading, paused, or the map ended"
                : $"nothing exported yet - {(_reader.Status.Length > 0 ? _reader.Status : "waiting")}"
            : $"exporting - {_reader.Current?.Survivors.Count ?? 0} survivors, seq advancing");

        if (_reader.Diagnostic.Length > 0)
            DebugLog.Note("diagnostic", "state", _reader.Diagnostic);

        if (!_reader.HasExported || _cfg.ExporterProven) return;

        _cfg.ExporterProven = true;
        bool saved = _cfg.TrySave(out string error);

        DebugLog.Write("state", saved
            ? "addon confirmed working - the NO EXPORT help will not be shown again"
            : $"addon confirmed working, but the config could not be saved: {error}");
    }

    private bool ShouldShow()
    {
        // Live preview is its own reason to draw: the editor holds input focus, so neither
        // the hold key nor the foreground gate can be satisfied while it is open.
        if (LivePreview) return true;

        bool wanted = _cfg.AlwaysShow || HoldKeyDown;
        bool infocus = _cfg.IgnoreForeground || _gameForeground;

        return wanted && infocus;
    }

    private void Render()
    {
        TrackExporterHealth();

        // Probed only when something would show the answer. The scan is backgrounded and
        // cached, but it still opens every pack in addons\, and a proven setup running with
        // the status corner switched off has no reason to pay for it at all.
        var addon = _cfg.ShowStatusBadge || _debug != null
            ? AddonProbe.Look(_reader?.Path)
            : default;

        _version = VersionGate.Check(addon, _reader?.Current?.Version);

        // Fresh exports mean an active round. No/frozen exports mean main menu, lobby,
        // loading, or pause; the transport cannot distinguish those inactive states.
        //
        // A version mismatch is the one thing worth saying during a round as well: the
        // update is the whole point of the message, and someone who only ever holds Tab
        // mid-round would never see it if it were menu-only.
        bool showMenuBadge = _cfg.ShowStatusBadge
                             && _gameForeground && !LivePreview
                             && (_reader == null || _reader.IsStale || _version.Mismatched);
        MenuBadge.Visibility = showMenuBadge ? Visibility.Visible : Visibility.Collapsed;
        UpdateNotice(showMenuBadge, addon);

        // A stale read is last session's roster, or this session's before the map ended.
        // Drawing it is worse than drawing nothing: it is wrong, and it looks authoritative.
        var state = (_reader?.IsStale ?? true) ? null : _reader?.Current;
        var survivors = state?.Survivors ?? new List<Survivor>();

        // The exporter's observed roster order is preserved; RosterPolicy decides which of
        // it belongs on the panel for the configured filter.
        // Two independent rosters. The consistent HUD stands in for the vanilla survivor
        // HUD, which is hidden while it is up, so the original four are its to draw; the
        // scoreboard panel sits beside L4D2's own scoreboard, which lists them already.
        var mode = ConsistentMode
            ? RosterPolicy.Parse(_cfg.ConsistentRosterFilter)
            : RosterPolicy.ParseScoreboard(_cfg.RosterFilter);
        var selectedRoster = RosterPolicy.Apply(survivors, mode);
        bool separateYouRequested = ConsistentMode && _cfg.ConsistentSeparateYou;
        Survivor? localSurvivor = separateYouRequested
            ? selectedRoster.FirstOrDefault(survivor => survivor.IsLocal)
            : null;
        var rosterSurvivors = localSurvivor == null
            ? selectedRoster
            : selectedRoster.Where(survivor => !ReferenceEquals(survivor, localSurvivor)).ToList();

        bool hasActiveRoster = !(_reader?.IsStale ?? true) && selectedRoster.Count > 0;
        Survivor? weaponSurvivor = survivors.FirstOrDefault(survivor => survivor.IsLocal);

        // The panel draws a roster or it draws nothing. It used to carry the "no export"
        // explanation as well, which meant holding Tab at a main menu produced an alarm
        // about a missing addon on an install where the addon was sitting in addons\ and
        // working perfectly - the app cannot tell "not installed" from "no map loaded" from
        // the state file alone. That explanation now lives under the top-right badge, where
        // it is backed by an actual look at the addons folder. See UpdateNotice.
        // Deliberately read off the last state the reader parsed, not the fresh-only one
        // above. A finale outro is the last thing the exporter reports before the map ends
        // and the exports stop, so the verdict has to outlive its own staleness window -
        // otherwise the panel would come back for the report screen it is meant to sit out.
        // The next map's first export clears it.
        bool cinematic = _cfg.HideDuringCinematics && !LivePreview
                         && (_reader?.Current?.Cinematic ?? 0) == 1;

        // The pause menu and the console are drawn entirely client-side: nothing the exporter
        // can poll knows they are open, and L4D2 hides its own survivor HUD for them, leaving
        // this overlay as the only thing still drawn over the menu.
        //
        // Two separate reasons to be away, and the first version of this shipped only the
        // second, which is why ESC did nothing:
        //
        //   menu     L4D2 shows a mouse cursor for its own menus and hides it during play.
        //            A listen server does NOT pause when the menu opens - the export loop
        //            keeps running and the file keeps advancing - so staleness never sees it.
        //            Paired with the foreground gate, since a visible cursor over some other
        //            application says nothing about the game.
        //   stopped  exports have stopped advancing: loading screen, main menu, game exited.
        //            HasExported keeps an overlay launched before the game drawing its empty
        //            panel - never having exported is not the same as having stopped.
        bool menuOpen = _gameForeground && _menuProbe();
        bool stopped  = _reader is { HasExported: true, IsStale: true };

        bool paused = _cfg.HideWhenGamePaused && !LivePreview && (menuOpen || stopped);

        bool show = ShouldShow() && !cinematic && !paused
                    && (hasActiveRoster || _cfg.AlwaysShow || LivePreview);

        Panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show)
        {
            _separatedYouVisible = false;
            ConsistentYouPanel.Visibility = Visibility.Collapsed;
            ConsistentYouCards.ItemsSource = null;
            WeaponPanel.Visibility = Visibility.Collapsed;
            WeaponSlots.ItemsSource = null;
            _dirty = true;
        }

        // Why the panel is not on screen is the single most asked question about this app,
        // and the panel cannot answer it while it is the thing not being drawn.
        DebugLog.Note("panel", "render", show
            ? "drawing"
            : cinematic
                ? "hidden - the game is running a cinematic"
                : paused
                ? menuOpen
                    ? "hidden - L4D2 is showing a menu or the console"
                    : "hidden - exports stopped (loading, main menu, or game closed)"
                : !ShouldShow()
                ? _cfg.AlwaysShow || HoldKeyDown
                    ? "hidden - L4D2 is not the foreground window"
                    : "hidden - hold key is not down"
                : "hidden - nothing to draw for the current roster filter");

        if (!show) return;

        // Live preview redraws on every slider move, and those do not touch the reader.
        if (!_dirty && !LivePreview) return;
        _dirty = false;

        var cards = rosterSurvivors
            .Select(s => SurvivorCard.From(
                s,
                RosterPolicy.Marker(s, mode),
                monochrome: ConsistentMode && _cfg.ConsistentMonochrome,
                showHealthNumbers: !ConsistentMode || _cfg.ConsistentShowHealthNumbers))
            .ToList();
        var youCards = new List<SurvivorCard>();

        if (localSurvivor != null)
        {
            youCards.Add(SurvivorCard.From(localSurvivor,
                                           RosterPolicy.Marker(localSurvivor, mode),
                                           monochrome: ConsistentMode && _cfg.ConsistentMonochrome,
                                           showHealthNumbers: !ConsistentMode
                                               || _cfg.ConsistentShowHealthNumbers));
        }

        // Nothing exporting - menu, lobby, or L4D2 not running at all. Stand-in cards keep
        // the panel measurable so layout can still be tuned.
        bool usingSampleCards = LivePreview && !hasActiveRoster;
        if (usingSampleCards)
        {
            var samples = SampleRoster.Cards(
                _livePreviewSampleCount,
                mode != RosterMode.Followers,
                monochrome: ConsistentMode && _cfg.ConsistentMonochrome,
                showHealthNumbers: !ConsistentMode || _cfg.ConsistentShowHealthNumbers);
            if (separateYouRequested && samples.Count > 0)
            {
                youCards = new List<SurvivorCard> { samples[0] };
                samples.RemoveAt(0);
            }

            cards = samples;
        }

        bool separatedYouVisible = ConsistentMode && youCards.Count > 0;
        bool separationChanged = separatedYouVisible != _separatedYouVisible;
        _separatedYouVisible = separatedYouVisible;

        // A wider roster may force the panel smaller. Re-earn that shrink when the number
        // of cards changes, without re-laying out at full scale on every 100 ms poll.
        if (cards.Count != _lastCardCount || separationChanged)
        {
            _lastCardCount = cards.Count;
            _fitScale = 1.0;
            ApplyLayout();
        }

        RenderWeaponPanel(usingSampleCards ? SampleRoster.WeaponSurvivor() : weaponSurvivor);

        if (ConsistentMode)
        {
            HeaderText.Text = "";
            StatusText.Text = "";
            Columns.ItemsSource = null;
            var cardMargin = ConsistentHudSpacing.CardMargin(_cfg.ConsistentHorizontalSpacing,
                                                             _cfg.ConsistentVerticalSpacing);
            ConsistentRows.Tag = cardMargin;
            ConsistentVerticalCards.Tag = cardMargin;
            if (youCards.Count > 0)
            {
                ConsistentYouCards.ItemsSource = youCards;
                ConsistentYouPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ConsistentYouCards.ItemsSource = null;
                ConsistentYouPanel.Visibility = Visibility.Collapsed;
            }
            bool vertical = ConsistentHudPolicy.IsVertical(_cfg.ConsistentTemplate);
            ConsistentRows.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
            ConsistentVerticalCards.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
            if (vertical)
            {
                ConsistentRows.ItemsSource = null;
                ConsistentVerticalCards.ItemsSource = cards;
            }
            else
            {
                ConsistentVerticalCards.ItemsSource = null;
                int minimumColumns = _separatedYouVisible
                    ? ConsistentHudPolicy.SeparateRosterColumns
                    : ConsistentHudPolicy.MinimumColumns;
                ConsistentRows.ItemsSource = ConsistentHudPolicy.SplitRows(cards,
                                                                           minimumColumns);
            }
        }
        else
        {
            HeaderText.Text = $"{RosterPolicy.Header(mode)}  {cards.Count}";
            StatusText.Text = usingSampleCards
                ? "LIVE PREVIEW - SAMPLE ROSTER"
                : LivePreview ? "LIVE PREVIEW" : "";

            ConsistentRows.ItemsSource = null;
            ConsistentVerticalCards.ItemsSource = null;
            ConsistentYouCards.ItemsSource = null;
            ConsistentYouPanel.Visibility = Visibility.Collapsed;
            ConsistentRows.Visibility = Visibility.Visible;
            ConsistentVerticalCards.Visibility = Visibility.Collapsed;
            Columns.ItemsSource = LayoutColumns(cards);
        }
        FitToSurface();
    }

    /// <summary>
    /// The line under the status badge. It says only what can be shown from evidence: the
    /// addons folder for whether the addon is there, and the state file for whether it is
    /// writing. Nothing exporting on an install with the pack present is not a fault - it is
    /// a menu or a load - and saying otherwise is what made the old message untrustworthy.
    /// </summary>
    private void UpdateNotice(bool badgeShowing, AddonPresence addon)
    {
        if (!badgeShowing || _reader == null)
        {
            Notice.Visibility = Visibility.Collapsed;
            return;
        }

        string title;
        string body;

        if (_reader.Path == null)
        {
            title = "GAME NOT FOUND";
            body = "Left 4 Dead 2 was not located. Set statePath in config.json to the "
                 + "state file under left4dead2\\ems\\overlay_hud\\.";
        }
        else if (addon.Missing)
        {
            title = "ADDON NOT INSTALLED";
            body = $"No exporter pack in {addon.AddonsPath}, subscribed or dropped in. "
                 + "Install the addon and restart L4D2.";
        }
        else if (addon.Duplicated)
        {
            var where = string.Join(" and ", addon.Packs.Select(pack =>
                pack.FromWorkshop ? $"workshop\\{pack.Name}" : pack.Name));

            title = "MORE THAN ONE COPY";
            body = $"The exporter is installed twice ({where}). One of them mounts and "
                 + "which is not predictable. Keep one and restart L4D2.";
        }
        else if (addon.Disabled)
        {
            title = "ADDON TURNED OFF";
            body = $"{addon.Packs[0].Name} is installed but switched off in the game's "
                 + "Add-ons screen, so it never runs. Enable it and restart L4D2.";
        }
        else if (_version.Verdict == VersionVerdict.AppBehind)
        {
            // The addon updates itself through Steam and this app does not, so this is the
            // ordinary way the pair drifts apart.
            title = "UPDATE THE OVERLAY APP";
            body = $"The addon is v{_version.AddonVersion} and this app is "
                 + $"v{_version.AppVersion}. The HUD keeps working; get the matching build "
                 + $"from {AppIdentity.ReleasesUrl}";
        }
        else if (_version.Verdict == VersionVerdict.AddonBehind)
        {
            title = "UPDATE THE EXPORTER ADDON";
            body = $"This app is v{_version.AppVersion} and the addon is "
                 + $"v{_version.AddonVersion}. The HUD keeps working; restart L4D2 to let "
                 + "Steam re-sync the addon, or reinstall the pack.";
        }
        else if (!ExporterProven)
        {
            // Installed but never seen writing. Stated as waiting rather than as a fault,
            // because at a main menu that is exactly what it is.
            var source = addon.Count == 1 && addon.Packs[0].FromWorkshop
                ? "Subscribed addon found"
                : "The addon is installed";

            title = "WAITING FOR A ROUND";
            body = $"{source}. The overlay fills in once a map is running.";
        }
        else
        {
            // Proven install, not exporting: menu, lobby, loading, or between maps. The
            // badge alone says that, and it needs no sentence under it.
            Notice.Visibility = Visibility.Collapsed;
            return;
        }

        NoticeTitle.Text = title;
        NoticeBody.Text = body;
        Notice.Visibility = Visibility.Visible;

        DebugLog.Note("notice", "state", $"{title} - {body}");
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

        if (ConsistentMode)
            return Math.Max(1, _surfaceHeight - Math.Max(0, offset));

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
        var anchor = ConsistentMode
            ? ConsistentRosterAnchor()
            : (_cfg.Anchor ?? "TopLeft");

        if (ConsistentMode)
        {
            bool edge = anchor.Contains("Left", StringComparison.OrdinalIgnoreCase)
                        || anchor.Contains("Right", StringComparison.OrdinalIgnoreCase);
            double hudInsetCount = edge ? 1 : 2;
            double room = _surfaceWidth - offset * hudInsetCount;

            // Separate You is a sibling root element, so the roster's natural width can
            // otherwise run underneath it. Reserve both the rendered You card and a
            // deliberate inter-group gap before FitToSurface chooses the roster scale.
            if (_separatedYouVisible)
            {
                double youWidth = SeparateYouRenderedWidth();
                double gap = _surfaceWidth * ConsistentHudPolicy.SeparateYouGapFraction;
                room -= youWidth + gap + offset;
            }

            return Math.Max(1, room);
        }

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

    private double SeparateYouRenderedWidth()
    {
        if (!ConsistentMode || !_separatedYouVisible
            || ConsistentYouCards.ItemsSource == null)
            return 0;

        Size natural = LayoutMeasurement.NaturalSize(ConsistentYouPanel);
        return Math.Max(0, natural.Width * EffectiveScale);
    }

    /// <summary>
    /// Measures what was actually laid out and scales it to use the available sidebar.
    /// Spare width can enlarge a short roster; overflow shrinks large two-column rosters.
    /// </summary>
    private void FitToSurface()
    {
        // A separated You card changes the usable width after every scale adjustment, so
        // settle the fit in one render instead of leaving the sibling overlap to future
        // state ticks. The cap prevents a pathological layout from spinning forever.
        for (int pass = 0; pass < 4; pass++)
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

            if (Math.Abs(nextFit - _fitScale) < 0.001) return;

            // ApplyLayout enforces the readability floor; this upper bound prevents a tiny
            // roster from becoming comically large merely because the sidebar is empty.
            _fitScale = nextFit;
            ApplyLayout();
        }
    }

    // -----------------------------------------------------------------------

    private void OnClosed(object? sender, EventArgs e)
    {
        // First, and unconditionally: a scoreboard key still down after the overlay exits
        // would leave the game stuck showing it.
        _scoreboard.Release();

        var debug = _debug;
        _debug = null;      // this is shutdown, not the user turning the console off
        debug?.Close();

        _settings?.Close();
        _keys?.Dispose();
        _reader?.Dispose();
        _ammo?.Dispose();
        _geometry?.Stop();

        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
    }
}
