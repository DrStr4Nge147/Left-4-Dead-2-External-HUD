using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
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
    private readonly Action<AppConfig, int, bool, bool>? _startLivePreview;
    private readonly Action<bool>? _endLivePreview;
    private readonly Action<bool>? _toggleDebug;
    private AppConfig _draft;
    private bool _ready;
    private bool _saved;
    private bool _capturingConsistentKey;
    private Rect _windowedBounds;
    private string? _statePath;
    private readonly DispatcherTimer _gameStatusTimer;

    public SettingsWindow(AppConfig config, Action apply,
                          Action<AppConfig, int, bool, bool>? startLivePreview = null,
                          Action<bool>? endLivePreview = null,
                          Action<bool>? toggleDebug = null)
    {
        InitializeComponent();

        FitToScreen();

        _live = config;
        _draft = config.Clone();
        _apply = apply;
        _startLivePreview = startLivePreview;
        _endLivePreview = endLivePreview;
        _toggleDebug = toggleDebug;

        Title = AppIdentity.Name;
        Icon = AppIcon.ForWindow();
        VersionText.Text = $"v{DisplayVersion()}";
        AuthorText.Text = $"by {AppIdentity.Author}";
        ReleasesLink.NavigateUri = new Uri(AppIdentity.ReleasesUrl);
        ReleasesLinkText.Text = AppIdentity.ReleasesUrl;
        _gameStatusTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _gameStatusTimer.Tick += (_, _) =>
        {
            UpdateGameStatus();
            UpdateVersionBanner();
        };
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
        ControlScroller.SizeChanged += (_, _) =>
        {
            RefreshScrollHint();
            // Also on Loaded: at construction the scroller has no width yet, so the first pass
        // measures zero and leaves the cards on the markup's default column count. That is
        // the "it only lays out properly after I resize the window" bug - the first real
        // measurement was arriving from the user's own resize.
        Loaded += (_, _) => RefreshCardColumns();
        SizeChanged += (_, _) => RefreshCardColumns();
        RefreshCardColumns();
        };
        RefreshCardColumns();
        RefreshScrollHint();

        LoadControls();
        _ready = true;
        ApplyPreviewMode();   // reopens on the remembered preview, live included
        UpdateGameStatus();
        UpdateVersionBanner();
        _gameStatusTimer.Start();
    }

    /// <summary>
    /// Cards reflow to the width available. Three across is right for the default window;
    /// narrower than that and the labels wrap into unreadable columns, wider and the rows
    /// stretch into long thin strips with the value floating an inch from its slider.
    /// </summary>
    /// <summary>
    /// Sizes the editor against the screen it is opening on, rather than trusting the
    /// designed size to fit.
    ///
    /// A fixed size is only ever right for one display. At 150% or 200% scaling the same
    /// numbers are half the usable desktop, and a window taller than the work area opens
    /// with its Save button under the taskbar - which is exactly where it is needed.
    /// The designed size is treated as a maximum, never a minimum.
    /// </summary>
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;
        if (area.Width <= 0 || area.Height <= 0) return;

        // Never taller than the work area, whatever the window is later resized to.
        MaxHeight = area.Height;
        MaxWidth = area.Width;

        double width = Math.Min(Width, area.Width * 0.74);
        double height = Math.Min(Height, area.Height * 0.80);

        // A small screen wins over the minimums too: a window that cannot fit is worse
        // than one that has to scroll a little more.
        MinWidth = Math.Min(MinWidth, width);
        MinHeight = Math.Min(MinHeight, height);

        Width = width;
        Height = height;
    }

    private void RefreshCardColumns()
    {
        double width = ControlScroller.ActualWidth;
        if (width <= 0) return;

        int columns = width switch
        {
            < 760 => 1,
            < 1080 => 2,
            < 1560 => 3,
            _ => 4
        };

        foreach (var grid in FindCardGrids(ControlScroller))
        {
            if (grid.Columns != columns) grid.Columns = columns;
        }
    }

    private static IEnumerable<System.Windows.Controls.Primitives.UniformGrid> FindCardGrids(
        DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.Primitives.UniformGrid grid)
            {
                yield return grid;
                continue;   // card grids do not nest
            }

            foreach (var nested in FindCardGrids(child)) yield return nested;
        }
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

    /// <summary>
    /// Says out loud when the installed addon and this build are on different versions, and
    /// puts the download address somewhere it can actually be clicked - the in-game overlay
    /// is click-through, so its copy of this message cannot carry a link.
    /// </summary>
    private void UpdateVersionBanner()
    {
        // Located once. The lookup walks the Steam library folders, which is not something
        // to repeat every second for an answer that does not move while the editor is open.
        _statePath ??= string.IsNullOrWhiteSpace(_draft.StatePath)
            ? StateLocator.Locate()
            : _draft.StatePath;

        var check = VersionGate.Check(AddonProbe.Look(_statePath), null);
        if (!check.Mismatched)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
            return;
        }

        bool appBehind = check.Verdict == VersionVerdict.AppBehind;

        UpdateBannerTitle.Text = appBehind
            ? "Update the overlay app"
            : "Update the exporter addon";

        UpdateBannerBody.Text = appBehind
            ? $"The installed addon is v{check.AddonVersion} and this app is "
            + $"v{check.AppVersion}. Nothing is broken - the HUD keeps working - but the two "
            + "halves ship on one version, so the newer build is worth having."
            : $"This app is v{check.AppVersion} and the installed addon is "
            + $"v{check.AddonVersion}. Restart L4D2 to let Steam re-sync the Workshop addon, "
            + "or reinstall the pack by hand.";

        // Only the app half is downloaded from here; a stale addon comes from the Workshop.
        UpdateBannerLinkLine.Visibility = appBehind ? Visibility.Visible : Visibility.Collapsed;
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void OnReleasesNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLog.Write("update", $"could not open the releases page: {ex.Message}");
        }

        e.Handled = true;
    }

    private static string DisplayVersion() => AppIdentity.DisplayVersion;

    private void LoadControls()
    {
        ScaleSlider.Value = Math.Clamp(_draft.Scale, ScaleSlider.Minimum, ScaleSlider.Maximum);
        OpacitySlider.Value = Math.Clamp(_draft.Opacity, OpacitySlider.Minimum, OpacitySlider.Maximum);
        ConsistentScaleSlider.Value = Math.Clamp(_draft.ConsistentScale,
                                                 ConsistentScaleSlider.Minimum,
                                                 ConsistentScaleSlider.Maximum);
        ConsistentOpacitySlider.Value = Math.Clamp(_draft.ConsistentOpacity,
                                                   ConsistentOpacitySlider.Minimum,
                                                   ConsistentOpacitySlider.Maximum);
        ConsistentVerticalSlider.Value = Math.Clamp(_draft.ConsistentVerticalOffset,
                                                    ConsistentVerticalSlider.Minimum,
                                                    ConsistentVerticalSlider.Maximum);
        ConsistentHorizontalSpacingSlider.Value = Math.Clamp(_draft.ConsistentHorizontalSpacing,
                                                             ConsistentHorizontalSpacingSlider.Minimum,
                                                             ConsistentHorizontalSpacingSlider.Maximum);
        ConsistentVerticalSpacingSlider.Value = Math.Clamp(_draft.ConsistentVerticalSpacing,
                                                           ConsistentVerticalSpacingSlider.Minimum,
                                                           ConsistentVerticalSpacingSlider.Maximum);
        OffsetXSlider.Value = Math.Clamp(_draft.OffsetX, OffsetXSlider.Minimum, OffsetXSlider.Maximum);
        OffsetYSlider.Value = Math.Clamp(_draft.OffsetY, OffsetYSlider.Minimum, OffsetYSlider.Maximum);
        BottomReserveSlider.Value = Math.Clamp(_draft.BottomReserve,
                                                BottomReserveSlider.Minimum,
                                                BottomReserveSlider.Maximum);
        MaxColumnsSlider.Value = Math.Clamp(_draft.MaxColumns, 1, 2);
        ExitWhenGameClosesCheckBox.IsChecked = _draft.ExitWhenGameCloses;
        AlwaysShowCheckBox.IsChecked = _draft.AlwaysShow;
        ConsistentSeparateYouCheckBox.IsChecked = _draft.ConsistentSeparateYou;
        ConsistentShowHealthNumbersCheckBox.IsChecked = _draft.ConsistentShowHealthNumbers;
        ConsistentMonochromeCheckBox.IsChecked = _draft.ConsistentMonochrome;
        ConsistentShowWeaponsCheckBox.IsChecked = _draft.ConsistentShowWeapons;
        WeaponVerticalSlider.Value = WeaponPanelPolicy.ClampVerticalOffset(
            _draft.WeaponPanelVerticalOffset);
        WeaponScaleSlider.Value = WeaponPanelPolicy.ClampScale(_draft.WeaponPanelScale);
        SelectByTag(WeaponCornerCombo, WeaponPanelPolicy.ParseCorner(_draft.WeaponPanelCorner));
        SelectByTag(WeaponOrientationCombo,
                    WeaponPanelPolicy.ParseOrientation(_draft.WeaponPanelOrientation));
        ConsistentHotkeyBox.Text = HotkeyDisplay.Name(_draft.ConsistentKey);
        SelectConsistentTemplate(_draft.ConsistentTemplate);
        SelectConsistentDesign(_draft.ConsistentDesign);
        ShowStatusBadgeCheckBox.IsChecked = _draft.ShowStatusBadge;
        DebugCheckBox.IsChecked = _draft.Debug;
        DebugCheckBox.IsEnabled = _toggleDebug != null;

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
        RosterExtrasRadio.IsChecked = mode == RosterMode.Extras;
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

    private bool IsConsistentTab => SettingsTabs.SelectedIndex == 1;

    /// <summary>
    /// The visible strip drives the TabControl, which remains the one place the selected
    /// tab actually lives. A negative index is the ListBox rebuilding its containers, and
    /// must never be forwarded: it would leave the editor with no tab selected at all.
    /// </summary>
    private void OnTabStripChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != TabStrip) return;

        if (TabStrip.SelectedIndex < 0)
        {
            TabStrip.SelectedIndex = SettingsTabs?.SelectedIndex ?? 0;
            return;
        }

        if (SettingsTabs != null && SettingsTabs.SelectedIndex != TabStrip.SelectedIndex)
        {
            SettingsTabs.SelectedIndex = TabStrip.SelectedIndex;
        }
    }

    private void OnSettingsTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.Source != SettingsTabs) return;

        if (TabStrip != null && TabStrip.SelectedIndex != SettingsTabs.SelectedIndex)
        {
            TabStrip.SelectedIndex = SettingsTabs.SelectedIndex;
        }

        // A TabControl builds a tab's contents the first time that tab is shown, so the
        // card grids on the tab being opened have never been measured and are still on the
        // column count written in the markup. Re-run the fit once they exist - at Loaded
        // priority, because right now they do not.
        //
        // This is why the second tab opened three-across and only corrected itself when the
        // window was resized: the resize was the first event that reached the new grids.
        Dispatcher.BeginInvoke(new Action(RefreshCardColumns), DispatcherPriority.Loaded);

        RefreshScrollHint();
        RefreshPreview();
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

                // Deliberately NOT SizeToContent.Height. That made sense while the settings
                // list was capped at 300px and the window could shrink-wrap what was left;
                // now the list fills the space the preview gave up, so asking the window to
                // fit its content means asking it to be as tall as every card in both tabs
                // - which is the whole screen. The list scrolls; the window stays put.
                SizeToContent = SizeToContent.Manual;
                DockClearOfPanel();
            }

            PushLivePreview();
            return;
        }

        if (PreviewHost.Visibility != Visibility.Visible)
        {
            SizeToContent = SizeToContent.Manual;
            PreviewHost.Visibility = Visibility.Visible;
            PreviewRow.Height = new GridLength(3, GridUnitType.Star);
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

        // Half the work area is the budget: enough to keep the editor clear of the panel
        // it is adjusting, without shrinking a window that already fits inside that.
        Width = Math.Min(Width, Math.Max(MinWidth, work.Width * 0.50));
        Height = Math.Min(Height, work.Height - 48);
        Left = Math.Max(work.Left, work.Right - Width - 24);
        Top = work.Top + 24;
    }

    private void PushLivePreview()
    {
        if (!IsLivePreview) return;

        _startLivePreview?.Invoke(_draft, (int)Math.Round(PreviewCountSlider.Value),
                                  !IsConsistentTab && ShowScoreboardCheckBox.IsChecked == true,
                                  IsConsistentTab);
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

    /// <summary>
    /// The console is a diagnostic, not a layout value: it takes effect the moment it is
    /// ticked and is remembered straight away, the same as the preview mode. Waiting for
    /// Save would be wrong for a tool someone reaches for when something is already broken.
    /// </summary>
    private void OnDebugChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        bool on = DebugCheckBox.IsChecked == true;

        _draft.Debug = on;
        if (_live.Debug != on)
        {
            _live.Debug = on;
            _live.TrySave(out _);
        }

        _toggleDebug?.Invoke(on);
    }

    /// <summary>Keeps the box honest when the console is closed from its own title bar.</summary>
    public void SetDebugChecked(bool on)
    {
        _draft.Debug = on;

        bool wasReady = _ready;
        _ready = false;
        DebugCheckBox.IsChecked = on;
        _ready = wasReady;
    }

    /// <summary>Keeps the consistent-HUD checkbox in sync when its global hotkey is used.</summary>
    public void SetConsistentHudChecked(bool on)
    {
        _draft.AlwaysShow = on;

        bool wasReady = _ready;
        _ready = false;
        AlwaysShowCheckBox.IsChecked = on;
        _ready = wasReady;
    }

    private void SelectConsistentTemplate(string? value)
    {
        string wanted = ConsistentHudPolicy.Parse(value);
        ConsistentTemplateCombo.SelectedItem = ConsistentTemplateCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, wanted,
                                                   StringComparison.OrdinalIgnoreCase))
            ?? ConsistentTemplateCombo.Items[0];
    }

    /// <summary>Selects the item whose Tag matches, falling back to the first.</summary>
    private static void SelectByTag(System.Windows.Controls.ComboBox combo, string wanted)
    {
        combo.SelectedItem = combo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, wanted,
                                                   StringComparison.OrdinalIgnoreCase))
            ?? combo.Items[0];
    }

    /// <summary>
    /// The simulated weapon panel. It uses the same corner, orientation, and height the
    /// live overlay would, at the consistent HUD's scale times the weapon panel's own
    /// size multiplier, so the editor is showing the real relationship between the two
    /// panels rather than a decorative stand-in.
    /// </summary>
    private void RefreshWeaponPreview(double scale)
    {
        if (_draft.ConsistentShowWeapons != true)
        {
            PreviewWeaponPanel.Visibility = Visibility.Collapsed;
            PreviewWeaponSlots.ItemsSource = null;
            PreviewWeaponItems.ItemsSource = null;
            return;
        }

        bool horizontal = WeaponPanelPolicy.IsHorizontal(_draft.WeaponPanelOrientation);
        var sample = SampleRoster.WeaponSurvivor();
        var slots = SurvivorCard.WeaponChip.SlotsFor(sample, horizontal);

        PreviewWeaponSlots.ItemsPanel = WeaponSlotPanel(horizontal);
        PreviewWeaponSlots.ItemsSource = slots;
        PreviewWeaponItems.ItemsSource = SurvivorCard.ItemChip.SlotsFor(sample);
        PreviewWeaponItems.HorizontalAlignment =
            WeaponPanelPolicy.IsLeft(_draft.WeaponPanelCorner)
                ? System.Windows.HorizontalAlignment.Left
                : System.Windows.HorizontalAlignment.Right;
        PreviewWeaponPanel.Opacity = Math.Clamp(_draft.ConsistentOpacity, 0.1, 1.0);

        // Same relationship the live overlay uses: the consistent HUD's scale times the
        // weapon panel's own multiplier.
        scale *= WeaponPanelPolicy.ClampScale(_draft.WeaponPanelScale);
        PreviewWeaponPanelScale.ScaleX = PreviewWeaponPanelScale.ScaleY = scale;

        Size natural = LayoutMeasurement.NaturalSize(PreviewWeaponPanel);
        double width = natural.Width * scale;
        double height = natural.Height * scale;
        double insetX = PreviewWidth * WeaponPanelPolicy.HorizontalInset;
        double insetY = PreviewHeight
            * WeaponPanelPolicy.ClampVerticalOffset(_draft.WeaponPanelVerticalOffset);

        Canvas.SetLeft(PreviewWeaponPanel,
                       WeaponPanelPolicy.IsLeft(_draft.WeaponPanelCorner)
                           ? insetX
                           : PreviewWidth - insetX - width);
        Canvas.SetTop(PreviewWeaponPanel, PreviewHeight - insetY - height);
        PreviewWeaponPanel.Visibility = Visibility.Visible;
    }

    private static ItemsPanelTemplate WeaponSlotPanel(bool horizontal)
    {
        var factory = new FrameworkElementFactory(typeof(StackPanel));
        factory.SetValue(StackPanel.OrientationProperty,
                         horizontal
                             ? System.Windows.Controls.Orientation.Horizontal
                             : System.Windows.Controls.Orientation.Vertical);

        var template = new ItemsPanelTemplate(factory);
        template.Seal();
        return template;
    }

    private void OnWeaponPanelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || (e.Source != WeaponCornerCombo && e.Source != WeaponOrientationCombo))
            return;

        ReadControls();
        RefreshPreview();
        SaveStatus.Text = "Unsaved changes";
    }

    private void SelectConsistentDesign(string? value)
    {
        string wanted = ConsistentHudPolicy.ParseDesign(value);
        ConsistentDesignCombo.SelectedItem = ConsistentDesignCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, wanted,
                                                   StringComparison.OrdinalIgnoreCase))
            ?? ConsistentDesignCombo.Items[0];
    }

    private void OnConsistentTemplateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.Source != ConsistentTemplateCombo) return;

        ReadControls();
        RefreshPreview();
        SaveStatus.Text = "Unsaved changes";
    }

    private void OnConsistentDesignChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || e.Source != ConsistentDesignCombo) return;

        ApplyConsistentDesignDefaultsIfUnchanged();
        ReadControls();
        RefreshPreview();
        SaveStatus.Text = "Unsaved changes";
    }

    /// <summary>
    /// Treats the design selector as a preset only while the controls still carry the
    /// previous design's defaults. Once a value has been customized, changing designs
    /// preserves that choice instead of silently resetting it.
    /// </summary>
    private void ApplyConsistentDesignDefaultsIfUnchanged()
    {
        if (ConsistentDesignCombo.SelectedItem is not ComboBoxItem selected) return;

        string previousDesign = ConsistentHudPolicy.ParseDesign(_draft.ConsistentDesign);
        string nextDesign = ConsistentHudPolicy.ParseDesign(selected.Tag as string);
        if (previousDesign == nextDesign) return;

        var previous = ConsistentHudPolicy.DefaultsFor(previousDesign);
        var next = ConsistentHudPolicy.DefaultsFor(nextDesign);

        if (NearlyEqual(ConsistentScaleSlider.Value, previous.Scale))
            ConsistentScaleSlider.Value = next.Scale;
        if (NearlyEqual(ConsistentOpacitySlider.Value, previous.Opacity))
            ConsistentOpacitySlider.Value = next.Opacity;
        if (NearlyEqual(ConsistentVerticalSlider.Value, previous.VerticalPosition))
            ConsistentVerticalSlider.Value = next.VerticalPosition;
        if (NearlyEqual(ConsistentHorizontalSpacingSlider.Value, previous.HorizontalSpacing))
            ConsistentHorizontalSpacingSlider.Value = next.HorizontalSpacing;
        if (NearlyEqual(ConsistentVerticalSpacingSlider.Value, previous.VerticalSpacing))
            ConsistentVerticalSpacingSlider.Value = next.VerticalSpacing;
        if (ConsistentMonochromeCheckBox.IsChecked == previous.Monochrome)
            ConsistentMonochromeCheckBox.IsChecked = next.Monochrome;
    }

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.0001;

    private void OnConsistentHotkeySetClick(object sender, RoutedEventArgs e)
    {
        _capturingConsistentKey = true;
        ConsistentHotkeyBox.Text = "Press a key...";
        ConsistentHotkeyBox.Focus();
        Keyboard.Focus(ConsistentHotkeyBox);
        SaveStatus.Text = "Press the key to use for the consistent HUD";
    }

    private void OnConsistentHotkeyClearClick(object sender, RoutedEventArgs e)
    {
        _capturingConsistentKey = false;
        _draft.ConsistentKey = 0;
        ConsistentHotkeyBox.Text = HotkeyDisplay.Name(0);
        SaveStatus.Text = "Unsaved changes";
    }

    private void OnConsistentHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturingConsistentKey) return;

        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _capturingConsistentKey = false;
            ConsistentHotkeyBox.Text = HotkeyDisplay.Name(_draft.ConsistentKey);
            SaveStatus.Text = "Hotkey capture cancelled";
            return;
        }

        int virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0 || virtualKey == _draft.HoldKey || virtualKey == _draft.EditorKey)
        {
            SaveStatus.Text = "Choose a key other than the scoreboard or editor key";
            return;
        }

        _draft.ConsistentKey = virtualKey;
        _capturingConsistentKey = false;
        ConsistentHotkeyBox.Text = HotkeyDisplay.Name(virtualKey);
        SaveStatus.Text = "Unsaved changes";
    }

    private RosterMode SelectedRosterMode()
    {
        if (RosterExtrasRadio.IsChecked == true) return RosterMode.Extras;
        if (RosterSoldiersRadio.IsChecked == true) return RosterMode.SoldiersAndFollowers;
        if (RosterFollowersRadio.IsChecked == true) return RosterMode.Followers;

        return RosterMode.All;
    }

    private void ReadControls()
    {
        _draft.Scale = ScaleSlider.Value;
        _draft.Opacity = OpacitySlider.Value;
        _draft.ConsistentScale = ConsistentScaleSlider.Value;
        _draft.ConsistentOpacity = ConsistentOpacitySlider.Value;
        _draft.ConsistentVerticalOffset = ConsistentVerticalSlider.Value;
        _draft.ConsistentHorizontalSpacing = ConsistentHorizontalSpacingSlider.Value;
        _draft.ConsistentVerticalSpacing = ConsistentVerticalSpacingSlider.Value;
        _draft.ConsistentSeparateYou = ConsistentSeparateYouCheckBox.IsChecked == true;
        _draft.ConsistentShowHealthNumbers =
            ConsistentShowHealthNumbersCheckBox.IsChecked == true;
        _draft.ConsistentMonochrome = ConsistentMonochromeCheckBox.IsChecked == true;
        _draft.ConsistentShowWeapons = ConsistentShowWeaponsCheckBox.IsChecked == true;
        _draft.WeaponPanelVerticalOffset = WeaponVerticalSlider.Value;
        _draft.WeaponPanelScale = WeaponScaleSlider.Value;
        if (WeaponCornerCombo.SelectedItem is ComboBoxItem corner)
            _draft.WeaponPanelCorner = WeaponPanelPolicy.ParseCorner(corner.Tag as string);
        if (WeaponOrientationCombo.SelectedItem is ComboBoxItem orientation)
            _draft.WeaponPanelOrientation =
                WeaponPanelPolicy.ParseOrientation(orientation.Tag as string);
        _draft.OffsetUnits = "percent";
        _draft.OffsetX = OffsetXSlider.Value;
        _draft.OffsetY = OffsetYSlider.Value;
        _draft.BottomReserve = BottomReserveSlider.Value;
        _draft.MaxColumns = (int)Math.Round(MaxColumnsSlider.Value);
        _draft.ExitWhenGameCloses = ExitWhenGameClosesCheckBox.IsChecked == true;
        _draft.AlwaysShow = AlwaysShowCheckBox.IsChecked == true;
        _draft.ShowStatusBadge = ShowStatusBadgeCheckBox.IsChecked == true;
        _draft.RosterFilter = RosterPolicy.ToConfigValue(SelectedRosterMode());
        if (ConsistentTemplateCombo.SelectedItem is ComboBoxItem item)
            _draft.ConsistentTemplate = ConsistentHudPolicy.Parse(item.Tag as string);
        if (ConsistentDesignCombo.SelectedItem is ComboBoxItem design)
            _draft.ConsistentDesign = ConsistentHudPolicy.ParseDesign(design.Tag as string);
    }

    private void RefreshLabels()
    {
        ScaleValue.Text = $"{ScaleSlider.Value:0.00}x";
        OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        ConsistentScaleValue.Text = $"{ConsistentScaleSlider.Value:0.00}x";
        ConsistentOpacityValue.Text = $"{ConsistentOpacitySlider.Value:P0}";
        ConsistentVerticalValue.Text = $"{ConsistentVerticalSlider.Value:P1} from bottom";
        ConsistentHorizontalSpacingValue.Text = $"{ConsistentHorizontalSpacingSlider.Value:0} px";
        ConsistentVerticalSpacingValue.Text = $"{ConsistentVerticalSpacingSlider.Value:0} px";
        WeaponVerticalValue.Text = $"{WeaponVerticalSlider.Value:P1} from bottom";
        WeaponScaleValue.Text = $"{WeaponScaleSlider.Value:0.00}x";
        OffsetXValue.Text = $"{OffsetXSlider.Value:P1}";
        OffsetYValue.Text = $"{OffsetYSlider.Value:P0}";
        BottomReserveValue.Text = $"{BottomReserveSlider.Value:P0}";
        MaxColumnsValue.Text = ((int)Math.Round(MaxColumnsSlider.Value)).ToString();
        PreviewCountValue.Text = ((int)Math.Round(PreviewCountSlider.Value)).ToString();
    }

    private void RefreshPreview()
    {
        ConsistentHudDesign.SetDesign(PreviewSurface, _draft.ConsistentDesign);

        // In live mode the real overlay is the preview, so every refresh becomes a push.
        if (IsLivePreview)
        {
            PushLivePreview();
            return;
        }

        var mode = RosterPolicy.Parse(_draft.RosterFilter);
        bool consistent = IsConsistentTab;
        var cards = SampleRoster.Cards(
            (int)Math.Round(PreviewCountSlider.Value),
            mode != RosterMode.Followers,
            monochrome: consistent && _draft.ConsistentMonochrome,
            showHealthNumbers: !consistent || _draft.ConsistentShowHealthNumbers);
        var youCards = new List<SurvivorCard>();
        if (consistent && _draft.ConsistentSeparateYou && cards.Count > 0)
        {
            youCards.Add(cards[0]);
            cards.RemoveAt(0);
        }

        bool vertical = ConsistentHudPolicy.IsVertical(_draft.ConsistentTemplate);
        PreviewSidebar.Visibility = consistent ? Visibility.Collapsed : Visibility.Visible;
        PreviewScoreboardContent.Visibility = consistent ? Visibility.Collapsed : Visibility.Visible;
        PreviewConsistentRows.Visibility = consistent && !vertical
            ? Visibility.Visible
            : Visibility.Collapsed;
        PreviewConsistentVerticalCards.Visibility = consistent && vertical
            ? Visibility.Visible
            : Visibility.Collapsed;
        PreviewScoreboard.Visibility = consistent ? Visibility.Collapsed : Visibility.Visible;
        PreviewBottomReserve.Visibility = consistent ? Visibility.Collapsed : Visibility.Visible;
        PreviewYouPanel.Visibility = Visibility.Collapsed;
        PreviewConsistentYouCards.ItemsSource = null;
        PreviewWeaponPanel.Visibility = Visibility.Collapsed;
        PreviewWeaponSlots.ItemsSource = null;

        if (consistent)
        {
            PreviewPanel.Padding = new Thickness(0);
            PreviewPanel.Background = Brushes.Transparent;
            PreviewPanel.BorderBrush = Brushes.Transparent;
            PreviewPanel.BorderThickness = new Thickness(0);
            PreviewPanel.Opacity = Math.Clamp(_draft.ConsistentOpacity, 0.1, 1.0);
            if (vertical)
            {
                PreviewConsistentRows.ItemsSource = null;
                PreviewConsistentVerticalCards.ItemsSource = cards;
            }
            else
            {
                PreviewConsistentVerticalCards.ItemsSource = null;
                int minimumColumns = youCards.Count > 0
                    ? ConsistentHudPolicy.SeparateRosterColumns
                    : ConsistentHudPolicy.MinimumColumns;
                PreviewConsistentRows.ItemsSource = ConsistentHudPolicy.SplitRows(
                    cards, minimumColumns);
            }
            PreviewColumns.ItemsSource = null;

            var cardMargin = ConsistentHudSpacing.CardMargin(_draft.ConsistentHorizontalSpacing,
                                                             _draft.ConsistentVerticalSpacing);
            PreviewConsistentRows.Tag = cardMargin;
            PreviewConsistentVerticalCards.Tag = cardMargin;
            PreviewConsistentYouCards.ItemsSource = youCards;
            PreviewYouPanel.Opacity = Math.Clamp(_draft.ConsistentOpacity, 0.1, 1.0);

            var placement = ConsistentHudPolicy.For(_draft.ConsistentTemplate);
            var template = ConsistentHudPolicy.Parse(_draft.ConsistentTemplate);
            bool rosterOnLeft = youCards.Count > 0
                && template == ConsistentHudPolicy.VanillaBottomCenter;
            string rosterAnchor = rosterOnLeft ? "BottomLeft" : placement.Anchor;
            double insetX = PreviewWidth * placement.HorizontalInset;
            double insetY = PreviewHeight
                * Math.Clamp(_draft.ConsistentVerticalOffset, 0.0, 0.90);
            double hudBaseScale = (PreviewHeight / PreviewBaselineHeight)
                                  * Math.Clamp(_draft.ConsistentScale, 0.1, 1.0);

            PreviewPanelScale.ScaleX = PreviewPanelScale.ScaleY = hudBaseScale;
            Size hudNatural = LayoutMeasurement.NaturalSize(PreviewPanel);
            double hudRenderedWidth = hudNatural.Width * hudBaseScale;
            double hudRenderedHeight = hudNatural.Height * hudBaseScale;
            double hudRoomWidth;
            double hudWidthForFit = hudRenderedWidth;
            if (youCards.Count > 0)
            {
                Size youNaturalForFit = LayoutMeasurement.NaturalSize(PreviewYouPanel);
                hudWidthForFit += youNaturalForFit.Width * hudBaseScale;
                hudRoomWidth = Math.Max(1, PreviewWidth - insetX * 2
                    - PreviewWidth * ConsistentHudPolicy.SeparateYouGapFraction);
            }
            else
            {
                hudRoomWidth = Math.Max(1, PreviewWidth
                    - (rosterAnchor.Contains("Center", StringComparison.OrdinalIgnoreCase)
                        ? insetX * 2
                        : insetX));
            }
            double hudRoomHeight = Math.Max(1, PreviewHeight - insetY);
            double hudAdjustment = Math.Min(hudRoomWidth / Math.Max(1, hudWidthForFit),
                                            hudRoomHeight / Math.Max(1, hudRenderedHeight));
            double hudMinFit = Math.Clamp(_draft.MinScale, 0.1, 1.0);
            double hudMaxFit = Math.Max(hudMinFit, LayoutPolicy.MaxFitScale);
            double hudFit = Math.Clamp(hudAdjustment, hudMinFit, hudMaxFit);

            double finalWidth = hudRenderedWidth * hudFit;
            double finalHeight = hudRenderedHeight * hudFit;
            PreviewPanelScale.ScaleX = PreviewPanelScale.ScaleY = hudBaseScale * hudFit;
            double finalLeft = rosterAnchor.Contains("Right", StringComparison.OrdinalIgnoreCase)
                ? PreviewWidth - insetX - finalWidth
                : rosterAnchor.Contains("Center", StringComparison.OrdinalIgnoreCase)
                    ? (PreviewWidth - finalWidth) / 2
                    : insetX;
            Canvas.SetLeft(PreviewPanel, finalLeft);
            Canvas.SetTop(PreviewPanel, rosterAnchor.StartsWith("Bottom", StringComparison.OrdinalIgnoreCase)
                ? PreviewHeight - insetY - finalHeight
                : insetY);

            if (youCards.Count > 0)
            {
                PreviewYouPanelScale.ScaleX = PreviewYouPanelScale.ScaleY = hudBaseScale * hudFit;
                Size youNatural = LayoutMeasurement.NaturalSize(PreviewYouPanel);
                double youRenderedWidth = youNatural.Width * hudBaseScale * hudFit;
                double youRenderedHeight = youNatural.Height * hudBaseScale * hudFit;
                bool youOnLeft = template == ConsistentHudPolicy.LowerRightVertical;
                Canvas.SetLeft(PreviewYouPanel, youOnLeft
                    ? insetX
                    : PreviewWidth - insetX - youRenderedWidth);
                Canvas.SetTop(PreviewYouPanel, PreviewHeight - insetY - youRenderedHeight);
                PreviewYouPanel.Visibility = Visibility.Visible;
            }

            RefreshWeaponPreview(hudBaseScale * hudFit);
            return;
        }

        // Scoreboard preview: this is the old framed panel beside the simulated vanilla
        // scoreboard, with the editor's sidebar geometry and column fitting intact.
        PreviewPanel.Padding = new Thickness(10, 8, 10, 8);
        PreviewPanel.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0x05, 0x07, 0x0A));
        PreviewPanel.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        PreviewPanel.BorderThickness = new Thickness(1);
        PreviewPanel.Opacity = Math.Clamp(_draft.Opacity, 0.1, 1.0);
        PreviewHeader.Text = $"{RosterPolicy.Header(RosterPolicy.Parse(_draft.RosterFilter))}  {cards.Count}";
        PreviewConsistentRows.ItemsSource = null;
        PreviewConsistentVerticalCards.ItemsSource = null;
        PreviewConsistentYouCards.ItemsSource = null;
        PreviewYouPanel.Visibility = Visibility.Collapsed;

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

    /// <summary>
    /// Puts one slider back to the value a fresh install would have, leaving every other
    /// setting alone. The consistent-HUD sliders answer to the selected HUD design, because
    /// that is where their defaults come from - resetting HUD size under Minimalist has to
    /// give Minimalist's 1.00x, not Basic's 0.65x.
    /// </summary>
    private void OnResetSliderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string name }) return;
        if (FindName(name) is not Slider slider) return;

        var fresh = new AppConfig();
        var design = ConsistentHudPolicy.DefaultsFor(
            ConsistentDesignCombo.SelectedItem is ComboBoxItem item
                ? item.Tag as string
                : _draft.ConsistentDesign);

        double? value = name switch
        {
            "ScaleSlider" => fresh.Scale,
            "OpacitySlider" => fresh.Opacity,
            "OffsetXSlider" => fresh.OffsetX,
            "OffsetYSlider" => fresh.OffsetY,
            "BottomReserveSlider" => fresh.BottomReserve,
            "MaxColumnsSlider" => fresh.MaxColumns,
            "PreviewCountSlider" => 6,   // same stand-in roster Reset UI restores
            "ConsistentScaleSlider" => design.Scale,
            "ConsistentOpacitySlider" => design.Opacity,
            "ConsistentVerticalSlider" => design.VerticalPosition,
            "ConsistentHorizontalSpacingSlider" => design.HorizontalSpacing,
            "ConsistentVerticalSpacingSlider" => design.VerticalSpacing,
            "WeaponVerticalSlider" => fresh.WeaponPanelVerticalOffset,
            "WeaponScaleSlider" => fresh.WeaponPanelScale,
            _ => null
        };

        if (value == null) return;

        // Clamped for the same reason the loader clamps: a default that falls outside a
        // slider's range would silently land on the nearest end instead of erroring, and
        // this is the one place that would go unnoticed.
        slider.Value = Math.Clamp(value.Value, slider.Minimum, slider.Maximum);
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

        // Written by the overlay while this window was open, and never by this window.
        // Saving a draft cloned before the first export would put the app back to claiming
        // the addon might be missing.
        _draft.ExporterProven |= _live.ExporterProven;

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
        _live.ConsistentKey = _draft.ConsistentKey;
        _live.PreviewMode = _draft.PreviewMode;
        _live.PreviewScoreboard = _draft.PreviewScoreboard;
        _live.Debug = _draft.Debug;
        _apply();

        // The values on screen are now the saved ones, so live preview must not roll the
        // overlay back to the pre-edit baseline when this window closes.
        _saved = true;
        Close();
    }
}
