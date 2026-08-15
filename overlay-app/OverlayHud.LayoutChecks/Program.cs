using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OverlayHud;
using OverlayHud.Model;
using OverlayHud.Services;
using OverlayHud.ViewModel;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
    if (args.Length > 0 && string.Equals(args[0], "preview", StringComparison.OrdinalIgnoreCase))
        return RunPreviewCheck();
    if (args.Length > 0 && string.Equals(args[0], "icons", StringComparison.OrdinalIgnoreCase))
        return RunIconCheck();
    if (args.Length > 0 && string.Equals(args[0], "item-order", StringComparison.OrdinalIgnoreCase))
        return RunItemOrderCheck();
    if (args.Length > 0 && string.Equals(args[0], "shortcut", StringComparison.OrdinalIgnoreCase))
        return RunShortcutCheck();
    if (args.Length > 0 && string.Equals(args[0], "settings-toggle", StringComparison.OrdinalIgnoreCase))
        return RunSettingsToggleCheck();
    if (args.Length > 0 && string.Equals(args[0], "editor-controls", StringComparison.OrdinalIgnoreCase))
        return RunEditorControlsCheck();
    if (args.Length > 0 && string.Equals(args[0], "consistent-hud", StringComparison.OrdinalIgnoreCase))
        return RunConsistentHudCheck();
    if (args.Length > 0 && string.Equals(args[0], "no-export", StringComparison.OrdinalIgnoreCase))
        return RunNoExportCheck();
    if (args.Length > 0 && string.Equals(args[0], "scoreboard-hold", StringComparison.OrdinalIgnoreCase))
        return RunScoreboardHoldCheck();
    if (args.Length > 0 && string.Equals(args[0], "live-preview", StringComparison.OrdinalIgnoreCase))
        return RunLivePreviewCheck();
    if (args.Length > 0 && string.Equals(args[0], "roster-filter", StringComparison.OrdinalIgnoreCase))
        return RunRosterFilterCheck();
    if (args.Length > 0 && string.Equals(args[0], "app-name", StringComparison.OrdinalIgnoreCase))
        return RunAppNameCheck();
    if (args.Length > 0 && string.Equals(args[0], "health-colour", StringComparison.OrdinalIgnoreCase))
        return RunHealthColourCheck();
    if (args.Length > 0 && string.Equals(args[0], "game-lifecycle", StringComparison.OrdinalIgnoreCase))
        return RunGameLifecycleCheck();
    if (args.Length > 0 && string.Equals(args[0], "single-instance", StringComparison.OrdinalIgnoreCase))
        return RunSingleInstanceCheck();
    if (args.Length > 0 && string.Equals(args[0], "game-status", StringComparison.OrdinalIgnoreCase))
        return RunGameStatusCheck();
    if (args.Length > 0 && string.Equals(args[0], "hook-recovery", StringComparison.OrdinalIgnoreCase))
        return RunHookRecoveryCheck();
    if (args.Length > 0 && string.Equals(args[0], "menu-stale", StringComparison.OrdinalIgnoreCase))
        return RunMenuStaleCheck();
    if (args.Length > 0 && string.Equals(args[0], "debug-log", StringComparison.OrdinalIgnoreCase))
        return RunDebugLogCheck();
    if (args.Length > 0 && string.Equals(args[0], "version-gate", StringComparison.OrdinalIgnoreCase))
        return RunVersionGateCheck();

    int count = args.Length > 0 ? int.Parse(args[0]) : 11;
    double width = args.Length > 1 ? double.Parse(args[1]) : 1920;
    double height = args.Length > 2 ? double.Parse(args[2]) : 1080;
    int expectedColumns = args.Length > 3 ? int.Parse(args[3]) : 2;

    var app = new App();
    app.InitializeComponent();

    var window = new MainWindow { Width = width, Height = height };
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;

    var config = (AppConfig)GetField(window, "_cfg", flags);
    config.Anchor = "TopLeft";
    config.OffsetUnits = "percent";
    config.OffsetX = 0.02;
    config.OffsetY = 0.59;
    config.AutoScale = true;
    config.BaselineHeight = 1080;
    config.Scale = 1;
    config.BottomReserve = 0;
    config.CardsPerColumn = 0;
    config.MaxColumns = 2;

    Invoke(window, "SetSurface", flags, width, height);

    var root = (FrameworkElement)GetField(window, "Root", flags);
    var header = (TextBlock)GetField(window, "HeaderText", flags);
    var columns = (ItemsControl)GetField(window, "Columns", flags);
    var panel = (Border)GetField(window, "Panel", flags);

    panel.Visibility = Visibility.Visible;
    header.Text = $"EXTRA SURVIVORS  {count}";
    root.Measure(new Size(width, height));
    root.Arrange(new Rect(0, 0, width, height));
    root.UpdateLayout();

    var cards = Enumerable.Range(1, count)
        .Select(i => new SurvivorCard
        {
            Name = $"Survivor {i}",
            HealthText = "100",
            PermanentWidth = SurvivorCard.BarWidth,
            TotalWidth = SurvivorCard.BarWidth
        })
        .ToList();

    var layout = (IList)Invoke(window, "LayoutColumns", flags, cards);
    double availableHeight = (double)Invoke(window, "AvailableHeight", flags);
    double availableWidth = (double)Invoke(window, "AvailableWidth", flags);
    double baseScale = (double)(window.GetType().GetProperty("BaseScale", flags)?.GetValue(window)
        ?? throw new MissingMemberException(window.GetType().FullName, "BaseScale"));
    columns.ItemsSource = layout;
    Size cachedBeforeFit = panel.Child.DesiredSize;
    Invoke(window, "FitToSurface", flags);
    double storedFit = (double)GetField(window, "_fitScale", flags);
    root.Measure(new Size(width, height));
    root.Arrange(new Rect(0, 0, width, height));
    root.UpdateLayout();

    panel.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    double naturalWidth = panel.Child.DesiredSize.Width + panel.Padding.Left + panel.Padding.Right
        + panel.BorderThickness.Left + panel.BorderThickness.Right;
    double naturalHeight = panel.Child.DesiredSize.Height + panel.Padding.Top + panel.Padding.Bottom
        + panel.BorderThickness.Top + panel.BorderThickness.Bottom;
    double effectiveScale = (double)(window.GetType().GetProperty("EffectiveScale", flags)
        ?.GetValue(window) ?? throw new MissingMemberException(window.GetType().FullName,
                                                                "EffectiveScale"));
    double top = config.OffsetY * height;
    double left = config.OffsetX * width;
    double renderedRight = left + naturalWidth * effectiveScale;
    double renderedBottom = top + naturalHeight * effectiveScale;
    bool passed = layout.Count == expectedColumns
        && renderedRight <= left + availableWidth + 0.5
        && renderedBottom <= height + 0.5;

    Console.WriteLine(
        $"cards={count} columns={layout.Count} actualHeight={panel.ActualHeight:0.##} " +
        $"naturalSize={naturalWidth:0.##}x{naturalHeight:0.##} " +
        $"cachedBeforeFit={cachedBeforeFit.Width:0.##}x{cachedBeforeFit.Height:0.##} " +
        $"storedFit={storedFit:0.###} effectiveScale={effectiveScale:0.###} " +
        $"baseScale={baseScale:0.###} " +
        $"available={availableWidth:0.##}x{availableHeight:0.##} " +
        $"right={renderedRight:0.##} sidebarRight={left + availableWidth:0.##} " +
        $"bottom={renderedBottom:0.##} surfaceBottom={height:0.##}");
    Console.WriteLine(passed
        ? "PASS"
        : $"FAIL: expected {expectedColumns} column(s) contained inside the sidebar");

    window.Close();
    app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunPreviewCheck()
    {
        var app = new App();
        app.InitializeComponent();

        var config = new AppConfig();
        var settings = new SettingsWindow(config, () => { });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Invoke(settings, "LoadControls", flags);
        Invoke(settings, "RefreshPreview", flags);

        var surface = (Canvas)GetField(settings, "PreviewSurface", flags);
        var sidebar = (Border)GetField(settings, "PreviewSidebar", flags);
        var panel = (Border)GetField(settings, "PreviewPanel", flags);
        var scale = (ScaleTransform)GetField(settings, "PreviewPanelScale", flags);
        var value = (TextBlock)GetField(settings, "ScaleValue", flags);
        var valueRow = (DockPanel)value.Parent;

        settings.Measure(new Size(1080, 720));
        settings.Arrange(new Rect(0, 0, 1080, 720));
        settings.UpdateLayout();

        panel.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double naturalWidth = panel.Child.DesiredSize.Width + panel.Padding.Left + panel.Padding.Right
            + panel.BorderThickness.Left + panel.BorderThickness.Right;
        double panelRight = Canvas.GetLeft(panel) + naturalWidth * scale.ScaleX;
        double sidebarRight = Canvas.GetLeft(sidebar) + sidebar.Width;
        double fixedSidebarRight = 720 * LayoutPolicy.SidebarWidthFraction;

        double labelGap = value.Margin.Left;

        bool passed = panelRight <= sidebarRight + 0.5
            && Math.Abs(sidebarRight - fixedSidebarRight) < 0.5
            && !valueRow.LastChildFill
            && labelGap >= 8;

        Console.WriteLine(
            $"previewPanelRight={panelRight:0.##} previewSidebarRight={sidebarRight:0.##} " +
            $"fixedSidebarRight={fixedSidebarRight:0.##} " +
            $"titleValueGap={labelGap:0.##}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: preview must contain the panel and separate titles from values");

        settings.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunIconCheck()
    {
        string[] ids =
        {
            "medkit", "defib", "explosive_ammo", "incendiary_ammo", "pills",
            "adrenaline", "molotov", "pipebomb", "bile"
        };

        var chips = ids.Select(SurvivorCard.ItemChip.For).ToList();
        bool allMapped = chips.All(chip => chip.Icon != null && chip.Icon.Width > 0
                                                             && chip.Icon.Height > 0);
        bool allBlack = chips.All(chip => Equals(chip.Background, Brushes.Black));
        bool labelsPresent = chips.All(chip => !string.IsNullOrWhiteSpace(chip.Label));
        var app = new App();
        app.InitializeComponent();
        var template = (DataTemplate)app.Resources["ChipTemplate"];
        var presenter = new ContentPresenter
        {
            Content = chips[0],
            ContentTemplate = template
        };
        presenter.ApplyTemplate();
        presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();
        var image = FindVisualChild<Image>(presenter);
        bool enlargedInsideSlot = image is { Width: 26, Height: 20 }
            && presenter.DesiredSize.Width == 32
            && presenter.DesiredSize.Height == 22;

        bool passed = allMapped && allBlack && labelsPresent && enlargedInsideSlot;

        Console.WriteLine(
            $"icons={chips.Count} mapped={allMapped} " +
            $"pureBlackBackground={allBlack} labels={labelsPresent} " +
            $"image26x20In30x22Slot={enlargedInsideSlot}");
        app.Shutdown();
        Console.WriteLine(passed ? "PASS" : "FAIL: all nine item icons must be flat black and white");
        return passed ? 0 : 1;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;

            T? nested = FindVisualChild<T>(child);
            if (nested != null) return nested;
        }

        return null;
    }

    private static int RunItemOrderCheck()
    {
        var card = SurvivorCard.From(new Survivor
        {
            Name = "Order check",
            Hp = 100,
            MaxHp = 100,
            Throwable = "pipebomb",
            Kit = "medkit",
            Pill = "adrenaline"
        });

        string[] labels = card.ItemSlots.Select(slot => slot.Label).ToArray();
        string[] expected = { "Pipe bomb", "Medkit", "Adrenaline" };
        bool passed = labels.SequenceEqual(expected);

        Console.WriteLine("itemOrder=" + string.Join(" -> ", labels));
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: expected throwable -> kit/ammo -> pills/adrenaline");
        return passed ? 0 : 1;
    }

    private static int RunShortcutCheck()
    {
        const int tab = 0x09;
        const int insert = 0x2D;
        var state = new KeyboardChordState(tab, insert);

        var insertAloneDown = state.Process(insert, true, false, true);
        var insertAloneUp = state.Process(insert, false, true, true);
        var tabDown = state.Process(tab, true, false, true);
        var chordDown = state.Process(insert, true, false, true);
        var chordRepeat = state.Process(insert, true, false, true);
        var tabUpFirst = state.Process(tab, false, true, false);
        var chordUp = state.Process(insert, false, true, false);
        var insertAfterChord = state.Process(insert, true, false, true);
        state.Process(insert, false, true, true);

        var disabled = new KeyboardChordState(tab, insert);
        disabled.Process(tab, true, false, false);
        var disabledChord = disabled.Process(insert, true, false, false);

        var reversed = new KeyboardChordState(tab, insert);
        var insertFirst = reversed.Process(insert, true, false, true);
        reversed.Process(tab, true, false, true);
        var reversedUp = reversed.Process(insert, false, true, true);

        bool passed = !insertAloneDown.Consume && !insertAloneDown.TriggerShortcut
            && !insertAloneUp.Consume
            && !tabDown.Consume && tabDown.HeldChanged == true
            && chordDown.Consume && chordDown.TriggerShortcut
            && chordRepeat.Consume && !chordRepeat.TriggerShortcut
            && !tabUpFirst.Consume && tabUpFirst.HeldChanged == false
            && chordUp.Consume && !chordUp.TriggerShortcut
            && !insertAfterChord.Consume
            && !disabledChord.Consume && !disabledChord.TriggerShortcut
            && !insertFirst.Consume && !reversedUp.Consume;

        Console.WriteLine(
            $"insertAlonePasses={!insertAloneDown.Consume && !insertAloneUp.Consume} " +
            $"tabPasses={!tabDown.Consume && !tabUpFirst.Consume} " +
            $"chordTriggersOnce={chordDown.TriggerShortcut && !chordRepeat.TriggerShortcut} " +
            $"chordDownUpConsumed={chordDown.Consume && chordUp.Consume} " +
            $"foregroundGatePasses={!disabledChord.Consume} " +
            $"modifierMustBeFirst={!insertFirst.Consume && !reversedUp.Consume}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: Tab+Insert suppression contract changed");
        return passed ? 0 : 1;
    }

    private static int RunSettingsToggleCheck()
    {
        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        Invoke(window, "ToggleSettings", flags);
        var opened = window.GetType().GetField("_settings", flags)?.GetValue(window)
                     as SettingsWindow;
        bool openedAndActive = opened is { IsVisible: true };

        Invoke(window, "ToggleSettings", flags);
        var afterClose = window.GetType().GetField("_settings", flags)?.GetValue(window);
        bool cancelled = afterClose == null && opened is { IsVisible: false };
        bool passed = openedAndActive && cancelled;

        Console.WriteLine($"opened={openedAndActive} secondChordCancels={cancelled}");
        Console.WriteLine(passed ? "PASS" : "FAIL: editor shortcut must toggle open/cancel");

        window.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunEditorControlsCheck()
    {
        var app = new App();
        app.InitializeComponent();
        var settings = new SettingsWindow(new AppConfig(), () => { });

        bool enlargementRemoved = settings.FindName("MaxFitSlider") == null
            && typeof(AppConfig).GetProperty("MaxFitScale") == null;
        bool sidebarRemoved = settings.FindName("SidebarWidthSlider") == null
            && typeof(AppConfig).GetProperty("SidebarWidth") == null;
        bool policyRetained = LayoutPolicy.MaxFitScale == 1.4
            && LayoutPolicy.SidebarWidthFraction == 0.376;
        bool previewLimitIs27 = settings.FindName("PreviewCountSlider") is Slider
            { Maximum: 27 };
        bool userScaleRangeIsUseful = settings.FindName("ScaleSlider") is Slider
            { Minimum: 0.6, Maximum: 1.0 }
            && LayoutPolicy.MinUserScale == 0.6
            && LayoutPolicy.MaxUserScale == 1.0;
        string[] sliderNames =
        {
            "ScaleSlider", "OpacitySlider", "OffsetXSlider", "OffsetYSlider",
            "BottomReserveSlider", "MaxColumnsSlider", "PreviewCountSlider"
        };
        bool slidersJumpToClick = sliderNames.All(name =>
            settings.FindName(name) is Slider { IsMoveToPointEnabled: true });

        // The control list is taller than its cap, so the bar has to be permanently visible
        // and wide enough to read as a control rather than as a border.
        bool barAlwaysVisible = settings.FindName("ControlScroller") is ScrollViewer
        {
            VerticalScrollBarVisibility: ScrollBarVisibility.Visible
        };
        double barWidth = ((Style)settings.FindResource("VisibleScrollBar"))
            .Setters.OfType<Setter>()
            .Where(setter => setter.Property == FrameworkElement.WidthProperty)
            .Select(setter => Convert.ToDouble(setter.Value))
            .FirstOrDefault();
        bool hintExists = settings.FindName("ScrollHint") is TextBlock;

        // The settings sit in a well darker than the window, with a fade at its bottom
        // edge. Both exist so the list reads as a scrollable region rather than as the
        // end of the page.
        bool wellIsDarker = settings.FindName("ControlWell") is Border { Background: SolidColorBrush well }
            && settings.Background is SolidColorBrush page
            && Brightness(well.Color) < Brightness(page.Color)
            && settings.FindName("ScrollFade") is System.Windows.Shapes.Rectangle
            {
                Fill: LinearGradientBrush, IsHitTestVisible: false
            };

        bool obviousScrollBar = barAlwaysVisible && barWidth >= 14 && hintExists && wellIsDarker;

        bool tabAndScoreboardOptions =
            settings.FindName("AlwaysShowCheckBox") is CheckBox { Content: TextBlock
            {
                Text: "Show HUD consistently"
            } }
            && settings.FindName("ShowScoreboardCheckBox") is CheckBox { IsChecked: true };

        bool separateConsistentTab =
            settings.FindName("SettingsTabs") is TabControl { Items.Count: 2 }
            && settings.FindName("ConsistentTemplateCombo") is ComboBox template
            && template.Items.Count == 3
            && settings.FindName("ConsistentDesignCombo") is ComboBox design
            && design.Items.Count == 2
            && settings.FindName("ConsistentHotkeyBox") is TextBox
            && settings.FindName("ConsistentScaleSlider") is Slider { IsMoveToPointEnabled: true }
            && settings.FindName("ConsistentOpacitySlider") is Slider { IsMoveToPointEnabled: true }
            && settings.FindName("ConsistentVerticalSlider") is Slider { IsMoveToPointEnabled: true }
            && settings.FindName("ConsistentHorizontalSpacingSlider") is Slider
            {
                Minimum: -100, Maximum: 100, IsMoveToPointEnabled: true
            }
            && settings.FindName("ConsistentVerticalSpacingSlider") is Slider
            {
                Minimum: -20, Maximum: 40, IsMoveToPointEnabled: true
            }
            && settings.FindName("ConsistentSeparateYouCheckBox") is CheckBox
            {
                Content: TextBlock { Text: "Separate You" }
            }
            && settings.FindName("ConsistentShowHealthNumbersCheckBox") is CheckBox
            {
                Content: TextBlock { Text: "Show health numbers" }
            }
            && settings.FindName("ConsistentMonochromeCheckBox") is CheckBox
            {
                Content: TextBlock { Text: "Black & white theme" }
            }
            && new AppConfig().ConsistentKey == 0x76
            && new AppConfig().ConsistentDesign == ConsistentHudPolicy.BasicDesign
            && Math.Abs(new AppConfig().ConsistentScale - 0.65) < 0.0001
            && Math.Abs(new AppConfig().ConsistentOpacity - 0.90) < 0.0001
            && Math.Abs(new AppConfig().ConsistentVerticalOffset - 0.03) < 0.0001
            && Math.Abs(new AppConfig().ConsistentHorizontalSpacing - 10) < 0.0001
            && Math.Abs(new AppConfig().ConsistentVerticalSpacing) < 0.0001
            && new AppConfig().ConsistentSeparateYou
            && new AppConfig().ConsistentShowHealthNumbers
            && !new AppConfig().ConsistentMonochrome
            && new AppConfig().ConsistentTemplate == ConsistentHudPolicy.VanillaBottomCenter;

        bool templateNames = settings.FindName("ConsistentTemplateCombo") is ComboBox namedTemplate
            && namedTemplate.Items.OfType<ComboBoxItem>()
                .Select(item => item.Content?.ToString() ?? string.Empty)
                .SequenceEqual(new[]
                {
                    "Bottom - Horizontal Grid",
                    "Lower Left Vertical Grid",
                    "Lower Right Vertical Grid"
                })
            && namedTemplate.Items.OfType<ComboBoxItem>()
                .Select(item => item.Tag as string)
                .SequenceEqual(new[]
                {
                    ConsistentHudPolicy.VanillaBottomCenter,
                    ConsistentHudPolicy.VanillaVertical,
                    ConsistentHudPolicy.LowerRightVertical
                });

        bool designNames = settings.FindName("ConsistentDesignCombo") is ComboBox namedDesign
            && namedDesign.Items.OfType<ComboBoxItem>()
                .Select(item => item.Content?.ToString() ?? string.Empty)
                .SequenceEqual(new[] { "Basic", "Minimalist" })
            && namedDesign.Items.OfType<ComboBoxItem>()
                .Select(item => item.Tag as string)
                .SequenceEqual(new[]
                {
                    ConsistentHudPolicy.BasicDesign,
                    ConsistentHudPolicy.MinimalistDesign
                });

        bool sectionHeadingsReadable =
            settings.FindName("PreviewSectionHeading") is TextBlock previewHeading
            && previewHeading.Foreground is SolidColorBrush previewBrush
            && previewBrush.Color == Color.FromRgb(0xF0, 0xF2, 0xF5)
            && settings.FindName("WhoToShowSectionHeading") is TextBlock whoHeading
            && whoHeading.Foreground is SolidColorBrush whoBrush
            && whoBrush.Color == Color.FromRgb(0xF0, 0xF2, 0xF5)
            && settings.FindName("UiSizeSectionHeading") is TextBlock uiSizeHeading
            && uiSizeHeading.Foreground is SolidColorBrush uiSizeBrush
            && uiSizeBrush.Color == Color.FromRgb(0xF0, 0xF2, 0xF5);

        // First run opens on the live preview: the real panel over the real game is what
        // the editor is for, and the simulated canvas is the fallback for a closed game.
        var freshInstall = new SettingsWindow(new AppConfig(), () => { });
        Invoke(freshInstall, "LoadControls", BindingFlags.Instance | BindingFlags.NonPublic);
        bool livePreviewIsDefault = new AppConfig().PreviewMode == "live"
            && freshInstall.FindName("LivePreviewRadio") is RadioButton { IsChecked: true };
        freshInstall.Close();

        // The editor reopens on the preview last used, and the scoreboard block with it.
        var remembered = new SettingsWindow(
            new AppConfig { PreviewMode = "live", PreviewScoreboard = false }, () => { });
        Invoke(remembered, "LoadControls", BindingFlags.Instance | BindingFlags.NonPublic);
        // The two Finale Soldiers options are visually sectioned off, but all four choices
        // must stay in the same radio group - splitting the group would let two filters be
        // selected at once.
        bool rosterSectionIsCosmetic =
            settings.FindName("RosterSectionRule") is Border
            && settings.FindName("RosterSectionHeading") is TextBlock
            {
                Text: "FOR FINALE SOLDIERS MOD"
            }
            && settings.FindName("RosterAllRadio") is RadioButton allRadio
            && settings.FindName("RosterExtrasRadio") is RadioButton extrasRadio
            && settings.FindName("RosterSoldiersRadio") is RadioButton soldiersRadio
            && settings.FindName("RosterFollowersRadio") is RadioButton followersRadio
            && allRadio.GroupName == extrasRadio.GroupName
            && extrasRadio.GroupName == soldiersRadio.GroupName
            && soldiersRadio.GroupName == followersRadio.GroupName;

        bool previewChoiceRemembered =
            remembered.FindName("LivePreviewRadio") is RadioButton { IsChecked: true }
            && remembered.FindName("SimulatedPreviewRadio") is RadioButton { IsChecked: false }
            && remembered.FindName("ShowScoreboardCheckBox") is CheckBox { IsChecked: false };
        remembered.Close();

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Invoke(settings, "LoadControls", flags);
        var separateYouCheckBox = (CheckBox)GetField(settings, "ConsistentSeparateYouCheckBox", flags);
        var showHealthNumbersCheckBox =
            (CheckBox)GetField(settings, "ConsistentShowHealthNumbersCheckBox", flags);
        var monochromeCheckBox =
            (CheckBox)GetField(settings, "ConsistentMonochromeCheckBox", flags);
        var editorDraft = (AppConfig)GetField(settings, "_draft", flags);
        var designCombo = (ComboBox)GetField(settings, "ConsistentDesignCombo", flags);
        bool separateYouLoadsOn = separateYouCheckBox.IsChecked == true;
        bool themeOptionsLoadDefaults = showHealthNumbersCheckBox.IsChecked == true
            && monochromeCheckBox.IsChecked == false;

        var presetSettings = new SettingsWindow(new AppConfig(), () => { });
        Invoke(presetSettings, "LoadControls", flags);
        var presetDesignCombo = (ComboBox)GetField(presetSettings, "ConsistentDesignCombo", flags);
        var presetScaleSlider = (Slider)GetField(presetSettings, "ConsistentScaleSlider", flags);
        var presetOpacitySlider = (Slider)GetField(presetSettings, "ConsistentOpacitySlider", flags);
        var presetVerticalSlider = (Slider)GetField(presetSettings, "ConsistentVerticalSlider", flags);
        var presetHorizontalSlider =
            (Slider)GetField(presetSettings, "ConsistentHorizontalSpacingSlider", flags);
        var presetVerticalSpacingSlider =
            (Slider)GetField(presetSettings, "ConsistentVerticalSpacingSlider", flags);
        var presetMonochromeCheckBox =
            (CheckBox)GetField(presetSettings, "ConsistentMonochromeCheckBox", flags);
        bool basicDesignDefaults = Math.Abs(presetScaleSlider.Value - 0.65) < 0.0001
            && Math.Abs(presetOpacitySlider.Value - 0.90) < 0.0001
            && Math.Abs(presetVerticalSlider.Value - 0.03) < 0.0001
            && Math.Abs(presetHorizontalSlider.Value - 10) < 0.0001
            && Math.Abs(presetVerticalSpacingSlider.Value) < 0.0001
            && presetMonochromeCheckBox.IsChecked == false;
        presetDesignCombo.SelectedIndex = 1;
        Invoke(presetSettings, "ApplyConsistentDesignDefaultsIfUnchanged", flags);
        Invoke(presetSettings, "ReadControls", flags);
        bool minimalistDesignDefaults = Math.Abs(presetScaleSlider.Value - 1.0) < 0.0001
            && Math.Abs(presetOpacitySlider.Value - 0.90) < 0.0001
            && Math.Abs(presetVerticalSlider.Value - 0.03) < 0.0001
            && Math.Abs(presetHorizontalSlider.Value - 10) < 0.0001
            && Math.Abs(presetVerticalSpacingSlider.Value) < 0.0001
            && presetMonochromeCheckBox.IsChecked == true;
        presetDesignCombo.SelectedIndex = 0;
        Invoke(presetSettings, "ApplyConsistentDesignDefaultsIfUnchanged", flags);
        Invoke(presetSettings, "ReadControls", flags);
        bool basicDesignDefaultsRestore = Math.Abs(presetScaleSlider.Value - 0.65) < 0.0001
            && presetMonochromeCheckBox.IsChecked == false;
        presetSettings.Close();

        separateYouCheckBox.IsChecked = true;
        showHealthNumbersCheckBox.IsChecked = false;
        monochromeCheckBox.IsChecked = true;
        Invoke(settings, "ReadControls", flags);
        bool separateYouWrites = editorDraft.ConsistentSeparateYou;
        bool themeOptionsWrite = !editorDraft.ConsistentShowHealthNumbers
            && editorDraft.ConsistentMonochrome;
        bool designLoadsBasic = designCombo.SelectedItem is ComboBoxItem
        {
            Tag: ConsistentHudPolicy.BasicDesign
        };
        designCombo.SelectedIndex = 1;
        Invoke(settings, "ReadControls", flags);
        bool designWritesMinimalist = editorDraft.ConsistentDesign
            == ConsistentHudPolicy.MinimalistDesign;
        var designAppliedConfig = new AppConfig();
        designAppliedConfig.CopyUiFrom(editorDraft);
        bool designCopiesToLive = designAppliedConfig.ConsistentDesign
            == ConsistentHudPolicy.MinimalistDesign;
        bool themeOptionsCopyToLive = !designAppliedConfig.ConsistentShowHealthNumbers
            && designAppliedConfig.ConsistentMonochrome;
        designCombo.SelectedIndex = 0;
        Invoke(settings, "ReadControls", flags);
        separateYouCheckBox.IsChecked = false;
        showHealthNumbersCheckBox.IsChecked = true;
        monochromeCheckBox.IsChecked = false;
        Invoke(settings, "ReadControls", flags);
        var previewSlider = (Slider)GetField(settings, "PreviewCountSlider", flags);
        previewSlider.Value = 27;
        Invoke(settings, "OnResetClick", flags, settings, new RoutedEventArgs());
        bool resetRestoresSix = previewSlider.Value == 6;
        bool resetRestoresThemeDefaults =
            ((AppConfig)GetField(settings, "_draft", flags)).ConsistentShowHealthNumbers
            && !((AppConfig)GetField(settings, "_draft", flags)).ConsistentMonochrome;

        // Reset UI is a layout reset: it must not silently turn off the panel-without-Tab
        // or game-lifetime preferences.
        ((CheckBox)GetField(settings, "AlwaysShowCheckBox", flags)).IsChecked = true;
        Invoke(settings, "ReadControls", flags);
        Invoke(settings, "OnResetClick", flags, settings, new RoutedEventArgs());
        bool resetKeepsBehaviorOptions =
            ((AppConfig)GetField(settings, "_draft", flags)).AlwaysShow;

        bool passed = enlargementRemoved && sidebarRemoved && policyRetained
            && previewLimitIs27 && userScaleRangeIsUseful && slidersJumpToClick
            && resetRestoresSix && obviousScrollBar && tabAndScoreboardOptions
            && separateConsistentTab && templateNames && sectionHeadingsReadable
            && separateYouLoadsOn && separateYouWrites && themeOptionsLoadDefaults
            && themeOptionsWrite && themeOptionsCopyToLive
            && designNames && designLoadsBasic && designWritesMinimalist && designCopiesToLive
            && basicDesignDefaults && minimalistDesignDefaults && basicDesignDefaultsRestore
            && resetRestoresThemeDefaults
            && resetKeepsBehaviorOptions
            && previewChoiceRemembered && livePreviewIsDefault
            && rosterSectionIsCosmetic;

        Console.WriteLine(
            $"enlargementControlAndConfigRemoved={enlargementRemoved} " +
            $"sidebarControlAndConfigRemoved={sidebarRemoved} " +
            $"calibratedInternalPolicy={policyRetained} " +
            $"previewLimitIs27={previewLimitIs27} " +
            $"userScaleRangeIs0.60To1.00={userScaleRangeIsUseful} " +
            $"allSlidersJumpToClick={slidersJumpToClick} " +
            $"resetPreviewCountIs6={resetRestoresSix} " +
            $"alwaysVisibleScrollBar={barAlwaysVisible} barWidth={barWidth} " +
            $"scrollHint={hintExists} darkerWellWithFade={wellIsDarker} " +
            $"tabAndScoreboardOptions={tabAndScoreboardOptions} " +
            $"separateConsistentTab={separateConsistentTab} " +
            $"separateYouLoadsOn={separateYouLoadsOn} separateYouWrites={separateYouWrites} " +
            $"themeOptionsLoadDefaults={themeOptionsLoadDefaults} " +
            $"themeOptionsWrite={themeOptionsWrite} themeOptionsCopyToLive={themeOptionsCopyToLive} " +
            $"templateNames={templateNames} " +
            $"designNames={designNames} designLoadsBasic={designLoadsBasic} " +
            $"designWritesMinimalist={designWritesMinimalist} designCopiesToLive={designCopiesToLive} " +
            $"basicDesignDefaults={basicDesignDefaults} " +
            $"minimalistDesignDefaults={minimalistDesignDefaults} " +
            $"basicDesignDefaultsRestore={basicDesignDefaultsRestore} " +
            $"resetRestoresThemeDefaults={resetRestoresThemeDefaults} " +
            $"sectionHeadingsReadable={sectionHeadingsReadable} " +
            $"resetKeepsBehaviorOptions={resetKeepsBehaviorOptions} " +
            $"previewChoiceRemembered={previewChoiceRemembered} " +
            $"livePreviewIsDefault={livePreviewIsDefault} " +
            $"rosterSectionIsCosmetic={rosterSectionIsCosmetic}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: fixed layout policy must not be exposed as editor settings");

        settings.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunConsistentHudCheck()
    {
        var app = new App();
        app.InitializeComponent();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = new MainWindow { Width = 1920, Height = 1080 };
        var config = (AppConfig)GetField(window, "_cfg", flags);
        config.GameProcess = "OverlayHudConsistentCheckNoSuchGame";
        config.IgnoreForeground = true;
        config.AlwaysShow = false;
        config.ConsistentSeparateYou = false;
        config.ConsistentDesign = ConsistentHudPolicy.BasicDesign;
        config.ConsistentShowHealthNumbers = true;
        config.ConsistentMonochrome = false;
        config.ConsistentTemplate = ConsistentHudPolicy.VanillaBottomCenter;
        config.ConsistentVerticalOffset = 0.035;
        config.ConsistentHorizontalSpacing = 24;
        config.ConsistentVerticalSpacing = -8;

        var minimalistTempCard = SurvivorCard.From(new Survivor
        {
            Name = "Temporary health",
            Hp = 60,
            Temp = 20,
            MaxHp = 100
        });
        bool minimalistTempHealth = minimalistTempCard.TempBrush is DrawingBrush
            && minimalistTempCard.MinimalistSegments.Count == SurvivorCard.MinimalistSegmentCount
            && minimalistTempCard.MinimalistSegments.All(segment =>
                ReferenceEquals(segment.TotalFill, minimalistTempCard.TempBrush)
                && ReferenceEquals(segment.PermanentFill, minimalistTempCard.HealthBrush)
                && segment.TotalFillWidth >= segment.PermanentFillWidth)
            && minimalistTempCard.MinimalistSegments.Any(segment =>
                segment.TotalFillWidth > segment.PermanentFillWidth);
        bool minimalistFiveBars = SurvivorCard.MinimalistSegmentCount == 5
            && Math.Abs(SurvivorCard.MinimalistSegmentHeight - 4) < 0.001
            && Math.Abs(SurvivorCard.MinimalistRowHeight - 6) < 0.001
            && minimalistTempCard.MinimalistSegments.Count == 5;

        Invoke(window, "SetSurface", flags, 1920.0, 1080.0);
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);

        var panel = (Border)GetField(window, "Panel", flags);
        var scoreboard = (StackPanel)GetField(window, "ScoreboardContent", flags);
        var consistent = (StackPanel)GetField(window, "ConsistentContent", flags);
        var rows = (ItemsControl)GetField(window, "ConsistentRows", flags);
        var verticalCards = (ItemsControl)GetField(window, "ConsistentVerticalCards", flags);
        var youPanel = (Border)GetField(window, "ConsistentYouPanel", flags);
        var youCards = (ItemsControl)GetField(window, "ConsistentYouCards", flags);
        var panelScale = (ScaleTransform)GetField(window, "PanelScale", flags);
        var youPanelScale = (ScaleTransform)GetField(window, "ConsistentYouPanelScale", flags);
        var guideScoreboard = (System.Windows.Shapes.Rectangle)GetField(window, "GuideScoreboard", flags);

        double defaultBottomMargin = panel.Margin.Bottom;
        bool spacingApplied = rows.Tag is Thickness cardMargin
            && Math.Abs(cardMargin.Left - 12) < 0.001
            && Math.Abs(cardMargin.Right - 12) < 0.001
            && Math.Abs(cardMargin.Top + 3) < 0.001
            && Math.Abs(cardMargin.Bottom + 3) < 0.001;
        bool bottomCenterTemplate = panel.HorizontalAlignment == HorizontalAlignment.Center
            && panel.VerticalAlignment == VerticalAlignment.Bottom
            && panel.Margin.Bottom > 0;
        bool usesHudRenderer = panel.Visibility == Visibility.Visible
            && scoreboard.Visibility != Visibility.Visible
            && consistent.Visibility == Visibility.Visible
            && rows.ItemsSource is IEnumerable rowSource
            && rowSource.Cast<IEnumerable>()
                .Select(row => row.Cast<object>().Count())
                .SequenceEqual(new[] { 4, 2 })
            && panel.Background == Brushes.Transparent
            && guideScoreboard.Visibility != Visibility.Visible;
        Size basicNaturalSize = LayoutMeasurement.NaturalSize(panel);
        double basicNaturalWidth = basicNaturalSize.Width;

        config.ConsistentShowHealthNumbers = false;
        config.ConsistentMonochrome = true;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool themeRenderer = rows.ItemsSource is IEnumerable themedRowSource
            && themedRowSource.Cast<IEnumerable>()
                .SelectMany(row => row.Cast<object>())
                .OfType<SurvivorCard>()
                .FirstOrDefault() is { ShowHealthNumbers: false, IsMonochrome: true } themedCard
            && ((SolidColorBrush)themedCard.HealthBrush).Color
                == Color.FromRgb(0xF2, 0xF2, 0xF2)
            && ((SolidColorBrush)themedCard.BasicHealthNumberBrush).Color
                == Colors.Black
            && ((SolidColorBrush)themedCard.FollowerBrush).Color
                == Color.FromRgb(0xF2, 0xF2, 0xF2);

        config.ConsistentDesign = ConsistentHudPolicy.MinimalistDesign;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        Size minimalistNaturalSize = LayoutMeasurement.NaturalSize(panel);
        double minimalistNaturalWidth = minimalistNaturalSize.Width;
        bool minimalistRenderer = minimalistNaturalWidth < basicNaturalWidth
            && minimalistNaturalSize.Height < basicNaturalSize.Height
            && rows.ItemsSource is IEnumerable minimalistRowSource
            && minimalistRowSource.Cast<IEnumerable>()
                .Select(row => row.Cast<object>().Count())
                .SequenceEqual(new[] { 4, 2 });

        config.ConsistentDesign = ConsistentHudPolicy.BasicDesign;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);

        config.ConsistentSeparateYou = true;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool separateYouRenderer = youPanel.Visibility == Visibility.Visible
            && youCards.ItemsSource is IEnumerable youSource
            && youSource.Cast<object>().Count() == 1
            && youCards.Tag == null
            && rows.ItemsSource is IEnumerable separatedRowSource
                && separatedRowSource.Cast<IEnumerable>()
                .Select(row => row.Cast<object>().Count())
                .SequenceEqual(new[] { 3, 2 })
            && panel.HorizontalAlignment == HorizontalAlignment.Left
            && youPanel.HorizontalAlignment == HorizontalAlignment.Right
            && youPanel.Margin.Right > 0
            && youPanel.Margin.Bottom > 0;
        double rosterRenderedWidth = LayoutMeasurement.NaturalSize(panel).Width * panelScale.ScaleX;
        double youRenderedWidth = LayoutMeasurement.NaturalSize(youPanel).Width * youPanelScale.ScaleX;
        double youLeft = 1920 - youPanel.Margin.Right - youRenderedWidth;
        double separateGapPixels = youLeft - (panel.Margin.Left + rosterRenderedWidth);
        bool separateGroupGap = separateGapPixels
            >= 1920 * ConsistentHudPolicy.SeparateYouGapFraction - 1.5;

        config.ConsistentSeparateYou = false;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);

        config.ConsistentTemplate = ConsistentHudPolicy.VanillaVertical;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool vanillaVerticalTemplate = panel.HorizontalAlignment == HorizontalAlignment.Left
            && panel.VerticalAlignment == VerticalAlignment.Bottom;
        bool verticalRenderer = rows.Visibility == Visibility.Collapsed
            && verticalCards.Visibility == Visibility.Visible
            && verticalCards.ItemsSource is IEnumerable verticalSource
            && verticalSource.Cast<object>().Count() == 6;

        config.ConsistentTemplate = ConsistentHudPolicy.LowerRightVertical;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool lowerRightVerticalTemplate = panel.HorizontalAlignment == HorizontalAlignment.Right
            && panel.VerticalAlignment == VerticalAlignment.Bottom
            && rows.Visibility == Visibility.Collapsed
            && verticalCards.Visibility == Visibility.Visible
            && verticalCards.ItemsSource is IEnumerable lowerRightSource
            && lowerRightSource.Cast<object>().Count() == 6;

        config.ConsistentSeparateYou = true;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool lowerRightSeparatesToLeft = panel.HorizontalAlignment == HorizontalAlignment.Right
            && youPanel.HorizontalAlignment == HorizontalAlignment.Left
            && youPanel.Margin.Left > 0
            && Math.Abs(youPanel.Margin.Right) < 0.001;
        double lowerRightRosterWidth = LayoutMeasurement.NaturalSize(panel).Width
            * panelScale.ScaleX;
        double lowerRightYouWidth = LayoutMeasurement.NaturalSize(youPanel).Width
            * youPanelScale.ScaleX;
        double lowerRightRosterLeft = 1920 - panel.Margin.Right - lowerRightRosterWidth;
        double lowerRightYouRight = youPanel.Margin.Left + lowerRightYouWidth;
        bool lowerRightGroupGap = lowerRightRosterLeft - lowerRightYouRight
            >= 1920 * ConsistentHudPolicy.SeparateYouGapFraction - 1.0;

        config.ConsistentSeparateYou = false;

        config.ConsistentTemplate = ConsistentHudPolicy.VanillaBottomCenter;
        config.ConsistentVerticalOffset = 0.35;
        window.UpdateLivePreview(config.Clone(), 6, false, true);
        Invoke(window, "Render", flags);
        bool verticalPositionMoved = panel.HorizontalAlignment == HorizontalAlignment.Center
            && panel.VerticalAlignment == VerticalAlignment.Bottom
            && panel.Margin.Bottom > defaultBottomMargin + 100;

        var settings = new SettingsWindow(config.Clone(), () => { });
        Invoke(settings, "LoadControls", flags);
        var settingsTabs = (TabControl)GetField(settings, "SettingsTabs", flags);
        settingsTabs.SelectedIndex = 1;
        Invoke(settings, "RefreshPreview", flags);
        var previewPanel = (Border)GetField(settings, "PreviewPanel", flags);
        var previewScale = (ScaleTransform)GetField(settings, "PreviewPanelScale", flags);
        var previewRows = (ItemsControl)GetField(settings, "PreviewConsistentRows", flags);
        previewPanel.Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double previewNaturalWidth = previewPanel.Child.DesiredSize.Width
            + previewPanel.Padding.Left + previewPanel.Padding.Right
            + previewPanel.BorderThickness.Left + previewPanel.BorderThickness.Right;
        double previewRenderedWidth = previewNaturalWidth * previewScale.ScaleX;
        double previewNaturalHeight = previewPanel.Child.DesiredSize.Height
            + previewPanel.Padding.Top + previewPanel.Padding.Bottom
            + previewPanel.BorderThickness.Top + previewPanel.BorderThickness.Bottom;
        double previewRenderedHeight = previewNaturalHeight * previewScale.ScaleY;
        bool vanillaPreview = Math.Abs(Canvas.GetLeft(previewPanel)
                                       - (720 - previewRenderedWidth) / 2) < 0.5;
        bool previewVerticalPosition = Canvas.GetTop(previewPanel)
            < 405 - previewRenderedHeight - 100;
        bool previewSpacingApplied = previewRows.Tag is Thickness previewMargin
            && Math.Abs(previewMargin.Left - 12) < 0.001
            && Math.Abs(previewMargin.Top + 3) < 0.001;
        bool previewGrid = previewRows.ItemsSource is IEnumerable previewRowSource
            && previewRowSource.Cast<IEnumerable>()
                .Select(row => row.Cast<object>().Count())
                .SequenceEqual(new[] { 4, 2 });

        var separatePreviewConfig = config.Clone();
        separatePreviewConfig.ConsistentSeparateYou = true;
        var separatePreview = new SettingsWindow(separatePreviewConfig, () => { });
        Invoke(separatePreview, "LoadControls", flags);
        var separatePreviewTabs = (TabControl)GetField(separatePreview, "SettingsTabs", flags);
        separatePreviewTabs.SelectedIndex = 1;
        Invoke(separatePreview, "RefreshPreview", flags);
        var previewSeparatePanel = (Border)GetField(separatePreview, "PreviewPanel", flags);
        var previewSeparateScale = (ScaleTransform)GetField(separatePreview,
                                                               "PreviewPanelScale", flags);
        var previewYouPanel = (Border)GetField(separatePreview, "PreviewYouPanel", flags);
        var previewYouCards = (ItemsControl)GetField(separatePreview,
                                                       "PreviewConsistentYouCards", flags);
        var previewSeparateRows = (ItemsControl)GetField(separatePreview,
                                                          "PreviewConsistentRows", flags);
        bool separateYouPreview = previewYouPanel.Visibility == Visibility.Visible
            && previewYouCards.ItemsSource is IEnumerable previewYouSource
            && previewYouSource.Cast<object>().Count() == 1
            && previewYouCards.Tag == null
            && previewSeparateRows.ItemsSource is IEnumerable previewSeparateRowSource
            && previewSeparateRowSource.Cast<IEnumerable>()
                .Select(row => row.Cast<object>().Count())
                .SequenceEqual(new[] { 3, 2 })
            && Canvas.GetLeft(previewYouPanel) > 360;
        previewSeparatePanel.Child.Measure(new Size(double.PositiveInfinity,
                                                    double.PositiveInfinity));
        double separatePreviewNaturalWidth = previewSeparatePanel.Child.DesiredSize.Width
            + previewSeparatePanel.Padding.Left + previewSeparatePanel.Padding.Right
            + previewSeparatePanel.BorderThickness.Left + previewSeparatePanel.BorderThickness.Right;
        double separatePreviewRenderedWidth = separatePreviewNaturalWidth
            * previewSeparateScale.ScaleX;
        bool separatePreviewGap = Canvas.GetLeft(previewYouPanel)
            - (Canvas.GetLeft(previewSeparatePanel) + separatePreviewRenderedWidth)
            >= 720 * ConsistentHudPolicy.SeparateYouGapFraction - 1.0;

        var verticalSettingsConfig = config.Clone();
        verticalSettingsConfig.ConsistentTemplate = ConsistentHudPolicy.VanillaVertical;
        var verticalSettings = new SettingsWindow(verticalSettingsConfig, () => { });
        Invoke(verticalSettings, "LoadControls", flags);
        var verticalSettingsTabs = (TabControl)GetField(verticalSettings, "SettingsTabs", flags);
        verticalSettingsTabs.SelectedIndex = 1;
        Invoke(verticalSettings, "RefreshPreview", flags);
        var previewVerticalRows = (ItemsControl)GetField(verticalSettings,
                                                          "PreviewConsistentRows", flags);
        var previewVerticalCards = (ItemsControl)GetField(verticalSettings,
                                                           "PreviewConsistentVerticalCards", flags);
        bool previewVertical = previewVerticalRows.Visibility == Visibility.Collapsed
            && previewVerticalCards.Visibility == Visibility.Visible
            && previewVerticalCards.ItemsSource is IEnumerable previewVerticalSource
            && previewVerticalSource.Cast<object>().Count() == 6
            && previewVerticalCards.Tag is Thickness previewVerticalMargin
            && Math.Abs(previewVerticalMargin.Left - 12) < 0.001
            && Math.Abs(previewVerticalMargin.Top + 3) < 0.001;
        bool retiredBottomRightMigrates = ConsistentHudPolicy.Parse("bottom-right")
                == ConsistentHudPolicy.VanillaBottomCenter
            && ConsistentHudPolicy.For("bottom-right").Anchor == "BottomCenter";
        bool retiredTopHorizontalMigrates = ConsistentHudPolicy.Parse("top-vertical")
                == ConsistentHudPolicy.VanillaBottomCenter
            && ConsistentHudPolicy.For("top-vertical").Anchor == "BottomCenter";
        var rows27 = ConsistentHudPolicy.SplitRows(Enumerable.Range(0, 27).ToList());
        bool adaptiveGrid = rows27.Select(row => row.Count).SequenceEqual(new[] { 9, 9, 9 });
        var separateRows7 = ConsistentHudPolicy.SplitRows(
            Enumerable.Range(0, 7).ToList(), ConsistentHudPolicy.SeparateRosterColumns);
        bool separateAdaptiveGrid = separateRows7.Select(row => row.Count)
            .SequenceEqual(new[] { 3, 3, 1 });

        // Regression: a normal geometry/key render can arrive with _dirty=false. It must
        // leave the independent card on screen instead of clearing it before returning.
        window.EndLivePreview(true);
        config.AlwaysShow = true;
        config.ConsistentSeparateYou = true;
        config.ConsistentTemplate = ConsistentHudPolicy.VanillaBottomCenter;
        string noOpStatePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"OverlayHudConsistentNoOp-{Guid.NewGuid():N}.json");
        string NoOpSnapshot(long seq) =>
            $"{{\"v\":\"1.2.0\",\"seq\":{seq},\"time\":1,\"count\":1," +
            "\"survivors\":[{\"uid\":1,\"name\":\"Host\",\"local\":true," +
            "\"cls\":\"survivor\",\"hp\":100,\"maxhp\":100}]}";
        System.IO.File.WriteAllText(noOpStatePath, NoOpSnapshot(1));
        var noOpReader = new StateReader(noOpStatePath, TimeSpan.FromMilliseconds(100), 5.0);
        Invoke(noOpReader, "Poll", flags);
        System.IO.File.WriteAllText(noOpStatePath, NoOpSnapshot(2));
        Invoke(noOpReader, "Poll", flags);
        typeof(MainWindow).GetField("_reader", flags)!.SetValue(window, noOpReader);
        typeof(MainWindow).GetField("_gameForeground", flags)!.SetValue(window, true);
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
        Invoke(window, "ApplyLayout", flags);
        Invoke(window, "Render", flags);
        bool youVisibleBeforeNoOp = youPanel.Visibility == Visibility.Visible;
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, false);
        Invoke(window, "Render", flags);
        bool youPersistsAfterNoOpRender = youVisibleBeforeNoOp
            && youPanel.Visibility == Visibility.Visible
            && youCards.ItemsSource is IEnumerable noOpYouSource
            && noOpYouSource.Cast<object>().Count() == 1;
        noOpReader.Dispose();
        try { System.IO.File.Delete(noOpStatePath); } catch { }

        var toggle = new KeyboardChordState(0x09, 0x2D, 0x76);
        var toggleDown = toggle.Process(0x76, true, false, false, true);
        var toggleRepeat = toggle.Process(0x76, true, false, false, true);
        var toggleUp = toggle.Process(0x76, false, true, false, false);
        var disabled = new KeyboardChordState(0x09, 0x2D, 0x76)
            .Process(0x76, true, false, false, false);
        bool hotkeyState = toggleDown.Consume && toggleDown.TriggerToggle
            && toggleRepeat.Consume && !toggleRepeat.TriggerToggle
            && toggleUp.Consume && !toggleUp.TriggerToggle
            && !disabled.Consume && !disabled.TriggerToggle;

        bool passed = spacingApplied && bottomCenterTemplate && usesHudRenderer
            && minimalistRenderer && minimalistTempHealth && minimalistFiveBars
            && themeRenderer && separateYouRenderer
            && separateGroupGap
            && vanillaVerticalTemplate
            && verticalRenderer && lowerRightVerticalTemplate && lowerRightSeparatesToLeft
            && lowerRightGroupGap
            && verticalPositionMoved
            && vanillaPreview && previewVerticalPosition && previewSpacingApplied
            && previewGrid && separateYouPreview && separatePreviewGap && previewVertical
            && retiredBottomRightMigrates && retiredTopHorizontalMigrates
            && adaptiveGrid && separateAdaptiveGrid
            && youPersistsAfterNoOpRender && hotkeyState;
        Console.WriteLine(
            $"spacingApplied={spacingApplied} bottomCenterTemplate={bottomCenterTemplate} " +
            $"hudRenderer={usesHudRenderer} separateYouRenderer={separateYouRenderer} " +
            $"minimalistRenderer={minimalistRenderer} " +
            $"minimalistTempHealth={minimalistTempHealth} " +
            $"minimalistFiveBars={minimalistFiveBars} themeRenderer={themeRenderer} " +
            $"separateGroupGap={separateGroupGap}({separateGapPixels:0.0}px) " +
            $"vanillaVerticalTemplate={vanillaVerticalTemplate} " +
            $"verticalRenderer={verticalRenderer} " +
            $"lowerRightVerticalTemplate={lowerRightVerticalTemplate} " +
            $"lowerRightSeparatesToLeft={lowerRightSeparatesToLeft} " +
            $"lowerRightGroupGap={lowerRightGroupGap} " +
            $"verticalPositionMoved={verticalPositionMoved} " +
            $"vanillaPreviewCentered={vanillaPreview} previewVerticalPosition={previewVerticalPosition} " +
            $"previewSpacingApplied={previewSpacingApplied} " +
            $"previewGrid={previewGrid} separateYouPreview={separateYouPreview} " +
            $"separatePreviewGap={separatePreviewGap} " +
            $"previewVertical={previewVertical} " +
            $"retiredBottomRightMigrates={retiredBottomRightMigrates} " +
            $"retiredTopHorizontalMigrates={retiredTopHorizontalMigrates} " +
            $"adaptiveGrid={adaptiveGrid} " +
            $"separateAdaptiveGrid={separateAdaptiveGrid} " +
            $"youPersistsAfterNoOpRender={youPersistsAfterNoOpRender} " +
            $"toggleState={hotkeyState}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: consistent HUD renderer, templates, or toggle state machine changed");

        window.EndLivePreview(false);
        window.Close();
        settings.Close();
        separatePreview.Close();
        verticalSettings.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    /// <summary>
    /// What the overlay does when the addon is not writing: say so rather than draw nothing,
    /// and let the status badge be turned off once the setup is known to work.
    /// </summary>
    private static int RunNoExportCheck()
    {
        var app = new App();
        app.InitializeComponent();

        var window = new MainWindow { Width = 1920, Height = 1080 };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var config = (AppConfig)GetField(window, "_cfg", flags);
        config.GameProcess = "OverlayHudCheckNoSuchGame";
        config.AlwaysShow = true;          // stands in for the hold key being down
        config.IgnoreForeground = true;
        config.ShowStatusBadge = true;
        config.ExporterProven = false;     // a setup that has never worked is the case here

        Invoke(window, "SetSurface", flags, 1920.0, 1080.0);
        typeof(MainWindow).GetField("_gameForeground", flags)!.SetValue(window, true);

        var panel = (Border)GetField(window, "Panel", flags);
        var badge = (Border)GetField(window, "MenuBadge", flags);
        var notice = (Border)GetField(window, "Notice", flags);
        var noticeTitle = (TextBlock)GetField(window, "NoticeTitle", flags);
        var noticeBody = (TextBlock)GetField(window, "NoticeBody", flags);

        // A fake install, because the whole point of this check is that the app looks at
        // the addons folder instead of inferring from a file that is not moving.
        var install = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OverlayHudCheck",
                                             $"install-{Guid.NewGuid():N}");

        // Real VPKs, built the way the exporter's own pack is: identification is by content,
        // so a file with the right name and nothing in it must not count as installed.
        string Case(string name, int loose, int workshop, bool disabled = false,
                    bool decoy = false)
        {
            var game = System.IO.Path.Combine(install, name, "left4dead2");
            var addons = System.IO.Path.Combine(game, "addons");
            System.IO.Directory.CreateDirectory(addons);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(game, "ems", "overlay_hud"));

            var entries = new List<string>();

            for (int i = 0; i < loose; i++)
            {
                var file = $"overlay_hud_export_v1.0.{i}.vpk";
                WritePack(System.IO.Path.Combine(addons, file), withExporter: true);
                entries.Add(file);
            }

            for (int i = 0; i < workshop; i++)
            {
                // What Steam actually stores: a publishedfileid, no addon name anywhere.
                var folder = System.IO.Path.Combine(addons, "workshop");
                System.IO.Directory.CreateDirectory(folder);
                var file = $"31415926{i}.vpk";
                WritePack(System.IO.Path.Combine(folder, file), withExporter: true);
                entries.Add($"workshop\\{file}");
            }

            if (decoy)
            {
                // Right name, wrong contents. A name match would call this installed.
                WritePack(System.IO.Path.Combine(addons, "overlay_hud_export_fake.vpk"),
                          withExporter: false);
            }

            var list = new System.Text.StringBuilder("\"AddonList\"\n{\n");
            foreach (var entry in entries)
                list.Append($"\t\t\"{entry}\"\t\t\"{(disabled ? 0 : 1)}\"\n");
            list.Append("}\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(game, "addonlist.txt"),
                                        list.ToString());

            return System.IO.Path.Combine(game, "ems", "overlay_hud", "state.json");
        }

        void Watch(string statePath)
        {
            typeof(MainWindow).GetField("_reader", flags)!.SetValue(window,
                new StateReader(statePath, TimeSpan.FromMilliseconds(100), 2.0));

            // The overlay scans in the background so a hundred-pack install cannot stall
            // its UI thread; a check wants the answer settled before it looks.
            AddonProbe.Refresh(statePath);

            typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
            Invoke(window, "Render", flags);
        }

        // Nothing installed, plus a decoy named like the addon and empty inside: the one
        // case that really is a broken setup.
        Watch(Case("missing", loose: 0, workshop: 0, decoy: true));
        bool missingIsNamed = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("NOT INSTALLED", StringComparison.Ordinal)
            && noticeBody.Text.Contains("addons", StringComparison.OrdinalIgnoreCase);

        // A Workshop subscription: stored under a publishedfileid with the addon's name
        // nowhere in it. Matching on filename would report this as missing.
        Watch(Case("subscribed", loose: 0, workshop: 1));
        bool workshopIsFound = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("WAITING", StringComparison.Ordinal)
            && noticeBody.Text.Contains("Subscribed", StringComparison.OrdinalIgnoreCase);

        // Subscribed and dragged in by hand: both mount, one wins, unpredictably.
        Watch(Case("duplicated", loose: 1, workshop: 1));
        bool duplicatesAreNamed = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("MORE THAN ONE", StringComparison.Ordinal);

        // Installed and switched off in the Add-ons screen: present, and never runs.
        var toggled = Case("disabled", loose: 1, workshop: 0, disabled: true);
        Watch(toggled);
        bool disabledIsNamed = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("TURNED OFF", StringComparison.Ordinal);

        // ...and switched back on. Enabling an addon rewrites addonlist.txt and touches no
        // VPK, so a cache keyed on the packs alone kept insisting it was off.
        var list = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(
                System.IO.Path.GetDirectoryName(toggled)!)!)!, "addonlist.txt");
        System.IO.File.WriteAllText(list,
            System.IO.File.ReadAllText(list).Replace("\"0\"", "\"1\""));

        AddonProbe.Refresh(toggled);
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
        Invoke(window, "Render", flags);
        bool reEnablingIsSeen = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("WAITING", StringComparison.Ordinal);

        // Installed, never seen exporting: a main menu, not a fault. This is the case the
        // old NO EXPORT message got wrong on a perfectly good install.
        Watch(Case("installed", loose: 1, workshop: 0));
        bool waitingIsNotAnAccusation = notice.Visibility == Visibility.Visible
            && noticeTitle.Text.Contains("WAITING", StringComparison.Ordinal)
            && !noticeBody.Text.Contains("not writing", StringComparison.OrdinalIgnoreCase);

        // The panel itself carries no explanation any more - it draws a roster or nothing.
        bool panelStaysARoster = panel.Visibility == Visibility.Visible
            && !panel.IsAncestorOf(notice);

        bool badgeShownByDefault = badge.Visibility == Visibility.Visible;

        // Proven install with nothing exporting: the badge says it, and there is nothing
        // to add underneath.
        config.ExporterProven = true;
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
        Invoke(window, "Render", flags);
        bool quietOnceProven = notice.Visibility != Visibility.Visible
            && badge.Visibility == Visibility.Visible;

        // Badge off means the whole status corner is off, notice included.
        config.ExporterProven = false;
        config.ShowStatusBadge = false;
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
        Invoke(window, "Render", flags);
        bool badgeCanBeTurnedOff = badge.Visibility != Visibility.Visible
            && notice.Visibility != Visibility.Visible;

        bool passed = missingIsNamed && workshopIsFound && duplicatesAreNamed
            && disabledIsNamed && reEnablingIsSeen && waitingIsNotAnAccusation
            && panelStaysARoster && badgeShownByDefault && quietOnceProven
            && badgeCanBeTurnedOff;

        Console.WriteLine(
            $"missingIsNamed={missingIsNamed} workshopIsFound={workshopIsFound} " +
            $"duplicatesAreNamed={duplicatesAreNamed} disabledIsNamed={disabledIsNamed} " +
            $"reEnablingIsSeen={reEnablingIsSeen} " +
            $"waitingIsNotAnAccusation={waitingIsNotAnAccusation} " +
            $"panelStaysARoster={panelStaysARoster} badgeShownByDefault={badgeShownByDefault} " +
            $"quietOnceProven={quietOnceProven} badgeCanBeTurnedOff={badgeCanBeTurnedOff}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: the status corner must report what is on disk, not guess from a quiet file");

        window.Close();
        app.Shutdown();
        try { System.IO.Directory.Delete(install, true); } catch { }

        return passed ? 0 : 1;
    }

    /// <summary>
    /// The scoreboard key must be held only while the game is in front, and must always be
    /// released. A latched +showscores left behind in someone's game is the failure to
    /// prevent here, so every exit path is asserted.
    /// </summary>
    private static int RunScoreboardHoldCheck()
    {
        var writes = new List<string>();
        var paths = new List<string>();
        string state = @"C:\Games\Left 4 Dead 2\left4dead2\ems\overlay_hud\state.json";

        var hold = new ScoreboardHold(() => state, (path, line) =>
        {
            paths.Add(path);
            writes.Add(line);
        });

        hold.Update(false);
        bool silentUntilAsked = !hold.IsHeld && writes.Count == 0;

        hold.Update(true);
        bool asksForHold = hold.IsHeld && writes.SequenceEqual(new[] { "1 1" });
        bool writesBesideStateFile = paths[0] ==
            @"C:\Games\Left 4 Dead 2\left4dead2\ems\overlay_hud\" + ScoreboardHold.CommandFileName;

        // A new app can meet an exporter up to v1.0.3, which writes loose into ems/ and reads
        // its command file from there. Writing the current name beside that file would be a
        // hold nothing ever picks up.
        var legacyPaths = new List<string>();
        string legacyState = @"C:\Games\Left 4 Dead 2\left4dead2\ems\overlay_hud_state.json";
        var legacyHold = new ScoreboardHold(() => legacyState, (path, _) => legacyPaths.Add(path));
        legacyHold.Update(true);
        bool followsLegacyExporter = legacyPaths.SequenceEqual(new[]
        {
            @"C:\Games\Left 4 Dead 2\left4dead2\ems\" + ScoreboardHold.LegacyCommandFileName
        });

        // Every poll while held is the heartbeat: same want, advancing seq. Without it the
        // addon cannot tell a live hold from a process that died holding.
        hold.Update(true);
        hold.Update(true);
        bool heartbeats = writes.SequenceEqual(new[] { "1 1", "1 2", "1 3" });

        hold.Update(false);
        bool releases = !hold.IsHeld && writes.Last() == "0 4";

        hold.Update(false);
        hold.Release();
        bool releaseIsQuietOnceDone = writes.Count == 4;

        // A hold whose write failed still has to be released explicitly, rather than being
        // left to the addon's heartbeat timeout.
        var flaky = new List<string>();
        bool folderPresent = false;
        var recovering = new ScoreboardHold(() => state, (_, line) =>
        {
            if (!folderPresent) throw new System.IO.IOException("folder unavailable");
            flaky.Add(line);
        });
        recovering.Update(true);                      // write fails: not held
        bool notHeldOnFailedWrite = !recovering.IsHeld && flaky.Count == 0;
        folderPresent = true;
        recovering.Update(false);                     // release still reaches the addon
        bool releasesAfterFailedHold = flaky.Count == 1 && flaky[0].StartsWith("0 ");

        // No located install: the ask cannot be delivered, and must not be reported as held.
        state = "";
        var unreachable = new ScoreboardHold(() => null, (_, line) => writes.Add("unexpected"));
        unreachable.Update(true);
        bool honestWhenUnreachable = !unreachable.IsHeld && writes.Count == 4;

        bool passed = silentUntilAsked && asksForHold && writesBesideStateFile
            && followsLegacyExporter
            && heartbeats && releases && releaseIsQuietOnceDone && honestWhenUnreachable
            && notHeldOnFailedWrite && releasesAfterFailedHold;

        Console.WriteLine(
            $"silentUntilAsked={silentUntilAsked} asksForHold={asksForHold} " +
            $"writesBesideStateFile={writesBesideStateFile} " +
            $"followsLegacyExporter={followsLegacyExporter} heartbeats={heartbeats} " +
            $"releases={releases} releaseIsQuietOnceDone={releaseIsQuietOnceDone} " +
            $"honestWhenUnreachable={honestWhenUnreachable} " +
            $"notHeldOnFailedWrite={notHeldOnFailedWrite} " +
            $"releasesAfterFailedHold={releasesAfterFailedHold} " +
            $"writes=[{string.Join(" | ", writes)}]");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: the scoreboard command must heartbeat while held and release exactly once");

        return passed ? 0 : 1;
    }

    /// <summary>
    /// Live preview must draw without the hold key or game focus, mark the boundaries the
    /// simulated preview draws, and hand the overlay its own settings back on Cancel.
    /// </summary>
    private static int RunLivePreviewCheck()
    {
        var app = new App();
        app.InitializeComponent();

        var window = new MainWindow { Width = 1920, Height = 1080 };
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var config = (AppConfig)GetField(window, "_cfg", flags);
        config.Anchor = "TopLeft";
        config.OffsetUnits = "percent";
        config.OffsetX = 0.02;
        config.OffsetY = 0.59;
        config.AlwaysShow = false;
        config.IgnoreForeground = false;

        // A name no process can have, and a scratch folder instead of the real install.
        // A check must never reach into the machine it happens to be running on: the
        // v1.0.0 version of this one found a REAL running L4D2 and held its scoreboard key.
        config.GameProcess = "OverlayHudCheckNoSuchGame";

        string scratch = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                                "OverlayHudCheck", "ems");
        System.IO.Directory.CreateDirectory(scratch);

        // The addon's own folder is deliberately NOT created here: on a fresh install the
        // app asks for the scoreboard before a map has ever been loaded, and the hold has to
        // make the folder itself rather than fail on it.
        config.StatePath = System.IO.Path.Combine(scratch, "overlay_hud", "state.json");

        Invoke(window, "SetSurface", flags, 1920.0, 1080.0);

        var panel = (Border)GetField(window, "Panel", flags);
        var guides = (Canvas)GetField(window, "Guides", flags);
        var columns = (ItemsControl)GetField(window, "Columns", flags);
        var status = (TextBlock)GetField(window, "StatusText", flags);
        var sidebar = (System.Windows.Shapes.Line)GetField(window, "GuideSidebar", flags);

        // Neither the hold key nor L4D2 focus exists here, which is exactly the state the
        // editor puts the app in.
        Invoke(window, "Render", flags);
        bool hiddenBeforePreview = panel.Visibility != Visibility.Visible
            && guides.Visibility != Visibility.Visible;

        var draft = config.Clone();
        draft.OffsetY = 0.45;
        window.UpdateLivePreview(draft, 6, true);
        Invoke(window, "Render", flags);

        // The ask goes to the addon as a command file, and the drawn block stands down
        // because the real scoreboard is what will be on screen.
        var scoreboard = (System.Windows.Shapes.Rectangle)GetField(window, "GuideScoreboard", flags);
        var holdState = (ScoreboardHold)GetField(window, "_scoreboard", flags);
        string commandFile = System.IO.Path.Combine(scratch, "overlay_hud",
                                                    ScoreboardHold.CommandFileName);

        bool asksAddonForScoreboard = holdState.IsHeld
            && System.IO.File.Exists(commandFile)
            && System.IO.File.ReadAllText(commandFile).StartsWith("1 ", StringComparison.Ordinal)
            && scoreboard.Visibility != Visibility.Visible;

        // Undeliverable ask - no addon folder to write into. The block is the fallback, and
        // the hold must not claim to be held.
        config.StatePath = @"X:\OverlayHudCheckNoSuchFolder\ems\overlay_hud\state.json";
        window.UpdateLivePreview(draft, 6, true);
        Invoke(window, "Render", flags);
        bool marksRegionWhenUndeliverable = !holdState.IsHeld
            && scoreboard.Visibility == Visibility.Visible
            && Math.Abs(scoreboard.Height - 1080 * 0.45) < 0.5
            && Math.Abs(scoreboard.Width - 1920 * LayoutPolicy.SidebarWidthFraction) < 0.5;

        config.StatePath = System.IO.Path.Combine(scratch, "overlay_hud", "state.json");
        window.UpdateLivePreview(draft, 6, false);
        bool scoreboardOptional = scoreboard.Visibility != Visibility.Visible
            && System.IO.File.ReadAllText(commandFile).StartsWith("0 ", StringComparison.Ordinal);

        int cardCount = ((IEnumerable<object>?)columns.ItemsSource)?
            .SelectMany(column => ((IEnumerable<object>)column))
            .Count() ?? 0;

        bool drawsWhileEditing = panel.Visibility == Visibility.Visible
            && guides.Visibility == Visibility.Visible;
        bool tookDraftValue = Math.Abs(config.OffsetY - 0.45) < 0.0001;
        bool samplesShown = cardCount == 6
            && status.Text.Contains("LIVE PREVIEW", StringComparison.Ordinal);
        bool marksSidebarEdge = Math.Abs(sidebar.X1 - 1920 * LayoutPolicy.SidebarWidthFraction) < 0.5
            && Math.Abs(sidebar.Y1 - 1080 * 0.45) < 0.5;

        window.EndLivePreview(false);
        Invoke(window, "Render", flags);
        bool cancelRestores = Math.Abs(config.OffsetY - 0.59) < 0.0001
            && guides.Visibility != Visibility.Visible
            && panel.Visibility != Visibility.Visible;

        window.UpdateLivePreview(draft, 6, false);
        window.EndLivePreview(true);
        bool saveKeeps = Math.Abs(config.OffsetY - 0.45) < 0.0001;

        // The editor's own preview canvas has to survive as the other mode.
        var settings = new SettingsWindow(new AppConfig(), () => { });
        bool simulatedModeStillExists = settings.FindName("PreviewSurface") is Canvas
            && settings.FindName("SimulatedPreviewRadio") is RadioButton
            && settings.FindName("LivePreviewRadio") is RadioButton;

        bool passed = hiddenBeforePreview && drawsWhileEditing && tookDraftValue
            && samplesShown && marksSidebarEdge && cancelRestores && saveKeeps
            && simulatedModeStillExists && asksAddonForScoreboard && scoreboardOptional
            && marksRegionWhenUndeliverable;

        Console.WriteLine(
            $"hiddenBeforePreview={hiddenBeforePreview} " +
            $"drawsWithoutKeyOrFocus={drawsWhileEditing} draftApplied={tookDraftValue} " +
            $"sampleCards={cardCount} status=\"{status.Text}\" " +
            $"marksSidebarEdge={marksSidebarEdge} cancelRestores={cancelRestores} " +
            $"saveKeeps={saveKeeps} simulatedModeKept={simulatedModeStillExists} " +
            $"asksAddonForScoreboard={asksAddonForScoreboard} " +
            $"scoreboardOptional={scoreboardOptional} " +
            $"marksRegionWhenUndeliverable={marksRegionWhenUndeliverable}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: live preview must draw over the game and be reversible by Cancel");

        settings.Close();
        window.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    /// <summary>
    /// Locks the four roster filters against one roster that contains every class the
    /// exporter can report, plus a legacy roster with no class field at all.
    /// </summary>
    private static int RunRosterFilterCheck()
    {
        var roster = new List<Survivor>
        {
            Named("Host", RosterPolicy.ClassSurvivor),
            Named("Ellis", RosterPolicy.ClassSurvivor),
            Named("Coach", RosterPolicy.ClassSurvivor),
            Named("Rochelle", RosterPolicy.ClassSurvivor),
            Named("Extra bot", RosterPolicy.ClassSurvivor),
            Named("Cpl. Blake", RosterPolicy.ClassHoldout),
            Named("Cpl. Nguyen", RosterPolicy.ClassSoldier),
            Named("Pvt. Chambers", RosterPolicy.ClassFollower),
            Named("Cpl. Foster", RosterPolicy.ClassHoldout)
        };

        string[] all = Names(RosterPolicy.Apply(roster, RosterMode.All));
        string[] extras = Names(RosterPolicy.Apply(roster, RosterMode.Extras));
        string[] soldiers = Names(RosterPolicy.Apply(roster, RosterMode.SoldiersAndFollowers));
        string[] followers = Names(RosterPolicy.Apply(roster, RosterMode.Followers));

        // No cls at all: an exporter older than v0.6.5. Everything is a plain survivor, so
        // All includes the four vanilla entries and Extras keeps the previous skip.
        var legacy = new List<Survivor>
        {
            Named("Host", ""), Named("Ellis", ""), Named("Coach", ""),
            Named("Rochelle", ""), Named("Pvt. Chambers", "")
        };
        string[] legacyAll = Names(RosterPolicy.Apply(legacy, RosterMode.All));
        string[] legacyExtras = Names(RosterPolicy.Apply(legacy, RosterMode.Extras));

        bool noHoldoutAnywhere = !all.Concat(soldiers).Concat(followers)
            .Any(name => name is "Cpl. Blake" or "Cpl. Foster");
        bool allMode = all.SequenceEqual(
            new[] { "Host", "Ellis", "Coach", "Rochelle", "Extra bot",
                    "Cpl. Nguyen", "Pvt. Chambers" });
        bool extrasMode = extras.SequenceEqual(
            new[] { "Extra bot", "Cpl. Nguyen", "Pvt. Chambers" });
        bool soldierMode = soldiers.SequenceEqual(
            new[] { "Cpl. Nguyen", "Pvt. Chambers" });
        bool followerMode = followers.SequenceEqual(new[] { "Pvt. Chambers" });
        bool legacyAllIncludesVanilla = legacyAll.SequenceEqual(
            new[] { "Host", "Ellis", "Coach", "Rochelle", "Pvt. Chambers" });
        bool legacyExtrasUnchanged = legacyExtras.SequenceEqual(new[] { "Pvt. Chambers" });
        bool headersDiffer = RosterPolicy.Header(RosterMode.All) == "ALL SURVIVORS"
            && RosterPolicy.Header(RosterMode.Extras) == "EXTRA SURVIVORS"
            && RosterPolicy.Header(RosterMode.SoldiersAndFollowers) == "SOLDIERS + FOLLOWERS"
            && RosterPolicy.Header(RosterMode.Followers) == "FOLLOWERS";
        bool roundTrips = new[]
            {
                RosterMode.All, RosterMode.Extras, RosterMode.SoldiersAndFollowers,
                RosterMode.Followers
            }
            .All(mode => RosterPolicy.Parse(RosterPolicy.ToConfigValue(mode)) == mode)
            && RosterPolicy.Parse(null) == RosterMode.All
            && RosterPolicy.Parse("nonsense") == RosterMode.All;

        // The follower marker only means something on a mixed roster.
        var follower = Named("Pvt. Chambers", RosterPolicy.ClassFollower);
        var soldier = Named("Cpl. Nguyen", RosterPolicy.ClassSoldier);
        bool marksOnMixedRosters =
            RosterPolicy.MarksFollower(follower, RosterMode.All)
            && RosterPolicy.MarksFollower(follower, RosterMode.SoldiersAndFollowers)
            && !RosterPolicy.MarksFollower(follower, RosterMode.Followers)
            && !RosterPolicy.MarksFollower(soldier, RosterMode.All);

        // The editor is the only way to reach the setting, so all four controls are part
        // of the contract: they must load from config and write back to it.
        var app = new App();
        app.InitializeComponent();

        // Rendered, not just modelled: the marker has to survive the shared card template.
        bool markerRenders =
            FollowerMarkerVisible(app, SurvivorCard.From(follower, true))
            && !FollowerMarkerVisible(app, SurvivorCard.From(follower, false));
        var settings = new SettingsWindow(new AppConfig { RosterFilter = "extras" }, () => { });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Invoke(settings, "LoadControls", flags);

        bool editorLoadsSetting = settings.FindName("RosterExtrasRadio")
                is RadioButton { IsChecked: true }
            && settings.FindName("RosterAllRadio") is RadioButton { IsChecked: false }
            && settings.FindName("RosterSoldiersRadio") is RadioButton { IsChecked: false };

        ((RadioButton)GetField(settings, "RosterExtrasRadio", flags)).IsChecked = false;
        ((RadioButton)GetField(settings, "RosterAllRadio", flags)).IsChecked = true;
        ((RadioButton)GetField(settings, "RosterSoldiersRadio", flags)).IsChecked = false;
        ((RadioButton)GetField(settings, "RosterFollowersRadio", flags)).IsChecked = false;
        Invoke(settings, "ReadControls", flags);
        var draft = (AppConfig)GetField(settings, "_draft", flags);
        bool editorWritesAll = draft.RosterFilter == "all";

        ((RadioButton)GetField(settings, "RosterSoldiersRadio", flags)).IsChecked = false;
        ((RadioButton)GetField(settings, "RosterAllRadio", flags)).IsChecked = false;
        ((RadioButton)GetField(settings, "RosterExtrasRadio", flags)).IsChecked = true;
        Invoke(settings, "ReadControls", flags);
        bool editorWritesExtras = draft.RosterFilter == "extras";

        // Reset UI is a layout reset; it may return the filter to its default, but the
        // filter must survive Save & Apply through the same UI copy the sliders use.
        var live = new AppConfig { RosterFilter = "all" };
        ((RadioButton)GetField(settings, "RosterSoldiersRadio", flags)).IsChecked = true;
        ((RadioButton)GetField(settings, "RosterExtrasRadio", flags)).IsChecked = false;
        Invoke(settings, "ReadControls", flags);
        live.CopyUiFrom(draft);
        bool editorWritesSoldiers = draft.RosterFilter == "soldiers";
        bool appliedToLiveConfig = live.RosterFilter == "soldiers";

        settings.Close();
        app.Shutdown();

        bool passed = noHoldoutAnywhere && allMode && extrasMode && soldierMode && followerMode
            && legacyAllIncludesVanilla && legacyExtrasUnchanged && headersDiffer && roundTrips
            && editorLoadsSetting && editorWritesAll && editorWritesExtras && editorWritesSoldiers
            && appliedToLiveConfig
            && marksOnMixedRosters && markerRenders;

        Console.WriteLine(
            $"all=[{string.Join(", ", all)}] " +
            $"extras=[{string.Join(", ", extras)}] " +
            $"soldiers=[{string.Join(", ", soldiers)}] " +
            $"followers=[{string.Join(", ", followers)}] " +
            $"legacyAll=[{string.Join(", ", legacyAll)}] " +
            $"legacyExtras=[{string.Join(", ", legacyExtras)}] " +
            $"holdoutHiddenEverywhere={noHoldoutAnywhere} " +
            $"headers={headersDiffer} configRoundTrip={roundTrips} " +
            $"editorLoads={editorLoadsSetting} editorWritesAll={editorWritesAll} " +
            $"editorWritesExtras={editorWritesExtras} editorWritesSoldiers={editorWritesSoldiers} " +
            $"appliedToLiveConfig={appliedToLiveConfig} " +
            $"followerMarkedOnMixedOnly={marksOnMixedRosters} markerRenders={markerRenders}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: holdouts must never draw and each filter must select its own roster");

        return passed ? 0 : 1;
    }

    private static bool FollowerMarkerVisible(App app, SurvivorCard card)
    {
        var presenter = new ContentPresenter
        {
            Content = card,
            ContentTemplate = (DataTemplate)app.Resources["SurvivorTemplate"]
        };

        presenter.ApplyTemplate();
        presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        presenter.Arrange(new Rect(presenter.DesiredSize));
        presenter.UpdateLayout();

        return FindTextBlock(presenter, "FOLLOW") is { Visibility: Visibility.Visible };
    }

    private static TextBlock? FindTextBlock(DependencyObject parent, string text)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock block && block.Text == text) return block;

            TextBlock? nested = FindTextBlock(child, text);
            if (nested != null) return nested;
        }

        return null;
    }

    private static double Brightness(Color color) =>
        0.299 * color.R + 0.587 * color.G + 0.114 * color.B;

    private static Survivor Named(string name, string cls) =>
        new() { Name = name, Cls = cls, Hp = 100, MaxHp = 100 };

    private static string[] Names(IEnumerable<Survivor> survivors) =>
        survivors.Select(s => s.Name).ToArray();

    private static int RunAppNameCheck()
    {
        const string expected = "Left 4 Dead 2 Customized Overlay HUD - External";
        var app = new App();
        app.InitializeComponent();
        var main = new MainWindow();
        var settings = new SettingsWindow(new AppConfig(), () => { });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var badge = (TextBlock)GetField(main, "MenuBadgeText", flags);
        var mainConfig = (AppConfig)GetField(main, "_cfg", flags);
        mainConfig.AlwaysShow = true;
        mainConfig.ConsistentScale = 0.65;
        Invoke(main, "ApplyLayout", flags);
        var badgeScale = (ScaleTransform)GetField(main, "MenuBadgeScale", flags);
        bool badgeFixedAtNativeScale = Math.Abs(badgeScale.ScaleX - 1.0) < 0.0001
            && Math.Abs(badgeScale.ScaleY - 1.0) < 0.0001;

        var assembly = typeof(MainWindow).Assembly;
        string? product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        string? title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        string? company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        string? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        // One author string, carried by the runtime surfaces and by build metadata.
        const string author = "DrStr4nge";
        var authorText = (TextBlock)GetField(settings, "AuthorText", flags);
        bool authorShown = AppIdentity.Author == author
            && authorText.Text == $"by {author}"
            && company == author
            && copyright != null && copyright.Contains(author, StringComparison.Ordinal);

        // One embedded icon behind the executable, the tray, and the editor window. The
        // tray path is the fragile one: System.Drawing cannot read PNG-compressed frames,
        // so this fails loudly if the .ico is ever rebuilt as all-PNG.
        using var trayIcon = AppIcon.ForTray();
        var windowIcon = AppIcon.ForWindow();
        bool iconUsable = trayIcon.Width > 0 && trayIcon.Height > 0
            && windowIcon.Width >= 256
            && settings.Icon == windowIcon
            && trayIcon.ToBitmap().Width == trayIcon.Width;

        bool passed = AppIdentity.Name == expected
            && main.Title == expected
            && settings.Title == expected
            && badge.Text.StartsWith(expected + " v", StringComparison.Ordinal)
            && product == expected
            && title == expected
            && authorShown
            && badgeFixedAtNativeScale
            && iconUsable;

        Console.WriteLine(
            $"identityExact={AppIdentity.Name == expected} mainTitle={main.Title == expected} " +
            $"settingsTitle={settings.Title == expected} badge={badge.Text.StartsWith(expected + " v", StringComparison.Ordinal)} " +
            $"productMetadata={product == expected} titleMetadata={title == expected} " +
            $"author={authorShown} companyMetadata=\"{company}\" " +
            $"badgeFixedAtNativeScale={badgeFixedAtNativeScale} " +
            $"icon={iconUsable} trayIcon={trayIcon.Width}x{trayIcon.Height} " +
            $"windowIcon={windowIcon.Width:0}");
        Console.WriteLine(passed ? "PASS" : "FAIL: app name or author is inconsistent");

        settings.Close();
        main.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    /// <summary>
    /// The health-colour bands, at their boundaries. 40 and 25 are the cases that were
    /// wrong through v1.0.8: the comparisons were exclusive and the amber floor sat at
    /// 20% of max, so 40 HP drew amber and 24 HP drew amber instead of red.
    /// </summary>
    private static int RunHealthColourCheck()
    {
        var app = new App();
        app.InitializeComponent();

        Color Colour(int hp)
        {
            var card = SurvivorCard.From(new Survivor { Hp = hp, MaxHp = 100 });
            return ((SolidColorBrush)card.HealthBrush).Color;
        }

        var green = Color.FromRgb(0x4C, 0xC0, 0x4C);
        var amber = Color.FromRgb(0xE0, 0xA8, 0x30);
        var red   = Color.FromRgb(0xC8, 0x3C, 0x3C);

        (int Hp, Color Want, string Name)[] cases =
        {
            (100, green, "green"), (40, green, "green"),
            (39, amber, "amber"),  (25, amber, "amber"),
            (24, red,   "red"),    (1,  red,   "red")
        };

        bool passed = true;
        foreach (var (hp, want, name) in cases)
        {
            var got = Colour(hp);
            bool ok = got == want;
            passed &= ok;
            Console.WriteLine($"hp={hp} want={name} got={got} {(ok ? "ok" : "WRONG")}");
        }

        // A 50-max survivor bands off his own maximum, not off 100.
        var halfMax = SurvivorCard.From(new Survivor { Hp = 20, MaxHp = 50 });
        bool scales = ((SolidColorBrush)halfMax.HealthBrush).Color == green;
        passed &= scales;
        Console.WriteLine($"maxhp=50 hp=20 green={scales}");

        Console.WriteLine(passed ? "PASS" : "FAIL: health colour bands are off");
        app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunGameLifecycleCheck()
    {
        var automatic = new GameLifetimeState();
        bool waitsBeforeGame = !automatic.ShouldExit(false, true);
        bool staysWhileRunning = !automatic.ShouldExit(true, true);
        bool exitsAfterClose = automatic.ShouldExit(false, true);

        var retained = new GameLifetimeState();
        retained.ShouldExit(true, false);
        bool staysAfterCloseWhenDisabled = !retained.ShouldExit(false, false);

        var app = new App();
        app.InitializeComponent();
        var config = new AppConfig();
        var settings = new SettingsWindow(config, () => { });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Invoke(settings, "LoadControls", flags);
        var checkbox = (CheckBox)GetField(settings, "ExitWhenGameClosesCheckBox", flags);
        bool defaultChecked = config.ExitWhenGameCloses && checkbox.IsChecked == true;

        var label = (TextBlock)checkbox.Content;
        bool readableCheckbox = label.Foreground is SolidColorBrush brush
            && brush.Color == Color.FromRgb(0xF0, 0xF2, 0xF5);

        var main = new MainWindow();
        var mainConfig = (AppConfig)GetField(main, "_cfg", flags);
        mainConfig.GameProcess = $"ovlhud_absent_{Guid.NewGuid():N}";
        mainConfig.ExitWhenGameCloses = true;
        var lifetime = (GameLifetimeState)GetField(main, "_gameLifetime", flags);
        lifetime.ShouldExit(true, true);
        bool windowClosed = false;
        main.Closed += (_, _) => windowClosed = true;
        Invoke(main, "TrackGameWindow", flags);

        bool passed = waitsBeforeGame && staysWhileRunning && exitsAfterClose
            && staysAfterCloseWhenDisabled && defaultChecked && readableCheckbox
            && windowClosed;

        Console.WriteLine(
            $"waitsBeforeGame={waitsBeforeGame} staysWhileRunning={staysWhileRunning} " +
            $"exitsAfterClose={exitsAfterClose} retainOptionWorks={staysAfterCloseWhenDisabled} " +
            $"defaultChecked={defaultChecked} readableCheckbox={readableCheckbox} " +
            $"windowClosesAfterObservedProcess={windowClosed}");
        Console.WriteLine(passed ? "PASS" : "FAIL: game-close lifecycle contract changed");

        settings.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    private static int RunSingleInstanceCheck()
    {
        string testName = $@"Local\OverlayHudLayoutCheck_{Guid.NewGuid():N}";

        using var first = new SingleInstanceGuard(testName);
        using var second = new SingleInstanceGuard(testName);

        bool exactlyOneOwner = first.IsPrimary && !second.IsPrimary;

        // A duplicate launch has to say so. The dialog itself cannot be shown from a check,
        // so the text it uses is asserted here instead of the window.
        string message = SingleInstanceGuard.AlreadyRunningMessage;
        bool explainsItself = message.Contains("already running", StringComparison.OrdinalIgnoreCase)
            && message.Contains("notification area", StringComparison.OrdinalIgnoreCase);

        bool passed = exactlyOneOwner && explainsItself;

        Console.WriteLine(
            $"firstIsPrimary={first.IsPrimary} secondRejected={!second.IsPrimary} " +
            $"productionMutexIsStable={SingleInstanceGuard.MutexName == @"Local\Left4Dead2CustomizedOverlayHudExternal"} " +
            $"duplicateIsTold={explainsItself}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: two app instances can own the same session mutex, or a duplicate exits silently");
        return passed ? 0 : 1;
    }

    private static int RunGameStatusCheck()
    {
        var app = new App();
        app.InitializeComponent();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        var runningConfig = new AppConfig
        {
            GameProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName
        };
        var runningWindow = new SettingsWindow(runningConfig, () => { });
        Invoke(runningWindow, "UpdateGameStatus", flags);
        var runningText = (TextBlock)GetField(runningWindow, "GameStatusText", flags);

        var absentConfig = new AppConfig { GameProcess = $"absent_{Guid.NewGuid():N}" };
        var absentWindow = new SettingsWindow(absentConfig, () => { });
        Invoke(absentWindow, "UpdateGameStatus", flags);
        var absentText = (TextBlock)GetField(absentWindow, "GameStatusText", flags);

        bool passed = runningText.Text == "L4D2: RUNNING"
            && absentText.Text == "L4D2: NOT RUNNING"
            && runningText.Foreground is SolidColorBrush runningBrush
            && runningBrush.Color == Color.FromRgb(0x62, 0xD2, 0x7B)
            && absentText.Foreground is SolidColorBrush absentBrush
            && absentBrush.Color == Color.FromRgb(0xF1, 0xB8, 0x5B);

        Console.WriteLine(
            $"runningText={runningText.Text} absentText={absentText.Text} " +
            $"runningGreen={runningText.Foreground} absentAmber={absentText.Foreground}");
        Console.WriteLine(passed ? "PASS" : "FAIL: game status badge is inaccurate");

        runningWindow.Close();
        absentWindow.Close();
        app.Shutdown();
        return passed ? 0 : 1;
    }

    /// <summary>
    /// At the main menu the state file is last session's, sitting there unchanged. Holding
    /// Tab used to bring up a panel full of that dead roster. Reading a file is not proof of
    /// an exporter; only watching it advance is.
    /// </summary>
    private static int RunMenuStaleCheck()
    {
        var app = new App();
        app.InitializeComponent();

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                            "OverlayHudCheck", $"menu-stale-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(folder);
        var file = System.IO.Path.Combine(folder, "state.json");

        // Written with this build's own version: an exporter one version behind is a real
        // condition with its own notice, and it is not what this check is about.
        string Snapshot(long seq) =>
            $"{{\"v\":\"{AppIdentity.DisplayVersion}\",\"seq\":{seq},\"time\":1,\"count\":2," +
            "\"survivors\":[" +
            // Soldiers, so the vanilla-four skip does not decide the count for us.
            "{\"uid\":1,\"name\":\"Leftover A\",\"cls\":\"soldier\",\"hp\":100,\"maxhp\":100}," +
            "{\"uid\":2,\"name\":\"Leftover B\",\"cls\":\"soldier\",\"hp\":80,\"maxhp\":100}]}";

        System.IO.File.WriteAllText(file, Snapshot(5));

        var reader = new StateReader(file, TimeSpan.FromMilliseconds(100), 5.0);

        var window = new MainWindow { Width = 1920, Height = 1080 };
        var config = (AppConfig)GetField(window, "_cfg", flags);
        config.AlwaysShow = false;          // the hold key is the only reason to draw
        config.IgnoreForeground = true;
        config.RosterFilter = "all";

        // Stated rather than inherited. The window loads whatever config.json sits beside
        // the harness, and the overlay writes exporterProven into it the moment a check
        // drives a live export - so an earlier run must not decide this one.
        config.ExporterProven = false;
        Invoke(window, "SetSurface", flags, 1920.0, 1080.0);

        // Stand in for Tab being held, without a real hook.
        var keys = new KeyWatcher(0x09, 0x2D, () => true);
        ((KeyboardChordState)GetField(keys, "_state", flags)).Sync(true);
        typeof(MainWindow).GetField("_keys", flags)!.SetValue(window, keys);
        typeof(MainWindow).GetField("_reader", flags)!.SetValue(window, reader);
        typeof(MainWindow).GetField("_gameForeground", flags)!.SetValue(window, true);

        var panel = (Border)GetField(window, "Panel", flags);
        var columns = (ItemsControl)GetField(window, "Columns", flags);
        var notice = (Border)GetField(window, "Notice", flags);

        int CardCount() => ((IEnumerable?)columns.ItemsSource)?.Cast<IEnumerable>()
            .Sum(column => column.Cast<object>().Count()) ?? 0;

        void Draw()
        {
            typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
            Invoke(window, "Render", flags);
        }

        // A file that has only ever been read once is not an export, however recently the
        // game wrote it. The setup is unproven, so the panel still explains itself - but
        // with no roster on it.
        Invoke(reader, "Poll", flags);
        Invoke(reader, "Poll", flags);
        Draw();
        bool leftoverIsNotAnExport = !reader.HasExported && reader.IsStale;
        bool noDeadRoster = CardCount() == 0;

        // The panel is a roster and nothing else now. With no live export there is nothing
        // to draw, so it stays away entirely; the status corner carries the explanation.
        bool panelIsQuiet = panel.Visibility != Visibility.Visible;

        // The exporter starts writing: the file advances, and the roster is real.
        System.IO.File.WriteAllText(file, Snapshot(6));
        Invoke(reader, "Poll", flags);
        Draw();
        bool advanceIsAnExport = reader.HasExported && !reader.IsStale;
        bool drawsLiveRoster = panel.Visibility == Visibility.Visible && CardCount() == 2
            && notice.Visibility != Visibility.Visible;

        // Back to the menu: the file stops advancing. Nothing is owed now - the setup is
        // proven, and the top-right badge already reports the state.
        typeof(StateReader).GetField("_lastSeqChangeUtc", flags)!
            .SetValue(reader, DateTime.UtcNow.AddMinutes(-1));
        Invoke(reader, "Poll", flags);
        Draw();
        bool quietAtTheMenu = reader.IsStale && panel.Visibility != Visibility.Visible;

        // Seeing it work is remembered on disk, so the next launch does not open by
        // suggesting the addon might be missing. A fresh app run, a fresh reader that has
        // seen nothing, and a config that says this install has exported before.
        var proven = new StateReader(file, TimeSpan.FromMilliseconds(100), 5.0);
        var provenWindow = new MainWindow { Width = 1920, Height = 1080 };
        var provenConfig = (AppConfig)GetField(provenWindow, "_cfg", flags);
        provenConfig.AlwaysShow = false;
        provenConfig.IgnoreForeground = true;
        provenConfig.ExporterProven = true;

        var provenKeys = new KeyWatcher(0x09, 0x2D, () => true);
        ((KeyboardChordState)GetField(provenKeys, "_state", flags)).Sync(true);
        typeof(MainWindow).GetField("_keys", flags)!.SetValue(provenWindow, provenKeys);
        typeof(MainWindow).GetField("_reader", flags)!.SetValue(provenWindow, proven);
        typeof(MainWindow).GetField("_gameForeground", flags)!.SetValue(provenWindow, true);
        Invoke(proven, "Poll", flags);
        typeof(MainWindow).GetField("_dirty", flags)!.SetValue(provenWindow, true);
        Invoke(provenWindow, "Render", flags);

        var provenPanel = (Border)GetField(provenWindow, "Panel", flags);
        bool quietOnRelaunch = !proven.HasExported
            && provenPanel.Visibility != Visibility.Visible;

        bool passed = leftoverIsNotAnExport && noDeadRoster && panelIsQuiet
            && advanceIsAnExport && drawsLiveRoster && quietAtTheMenu && quietOnRelaunch;

        Console.WriteLine(
            $"leftoverIsNotAnExport={leftoverIsNotAnExport} noDeadRoster={noDeadRoster} " +
            $"panelIsQuiet={panelIsQuiet} advanceIsAnExport={advanceIsAnExport} " +
            $"drawsLiveRoster={drawsLiveRoster} quietAtTheMenu={quietAtTheMenu} " +
            $"quietOnRelaunch={quietOnRelaunch}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: a stale file must not draw a roster, and a proven setup must stay quiet");

        keys.Dispose();
        provenKeys.Dispose();
        reader.Dispose();
        proven.Dispose();
        window.Close();
        provenWindow.Close();
        app.Shutdown();
        try { System.IO.Directory.Delete(folder, true); } catch { }

        // Written by the overlay when this check drove its first live export. It belongs to
        // the harness folder, not to a user, and leaving it there changes what the next
        // check sees.
        try { System.IO.File.Delete(AppConfig.ConfigPath); } catch { }

        return passed ? 0 : 1;
    }

    /// <summary>
    /// The two halves ship on one version but arrive by different routes - the addon updates
    /// itself through Steam, the app does not - so the app is the half that goes stale
    /// silently. The version is read from the installed pack's own addoninfo.txt, which is
    /// answerable at a main menu, before any map has loaded and before anything has been
    /// exported.
    /// </summary>
    private static int RunVersionGateCheck()
    {
        var app = new App();
        app.InitializeComponent();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        // Stated against the running build, so this check does not need editing at every
        // version bump.
        var self = AppIdentity.Version;
        string same = AppIdentity.DisplayVersion;
        string newer = $"{self.Major}.{self.Minor}.{Math.Max(0, self.Build) + 1}";
        string older = self.Build > 0
            ? $"{self.Major}.{self.Minor}.{self.Build - 1}"
            : $"{Math.Max(0, self.Major - 1)}.{self.Minor}.0";

        // The manifest is hand-written Valve KeyValues, and quotes are optional in it.
        bool readsQuoted = VpkReader.AddonVersionFrom(
            "\"AddonInfo\"\n{\n\taddontitle\t\t\"x\"\n\taddonversion\t\t\"1.2.3\"\n}") == "1.2.3";
        bool readsBare = VpkReader.AddonVersionFrom("addonversion 4.5.6") == "4.5.6";
        bool absentIsNull = VpkReader.AddonVersionFrom("\"AddonInfo\"\n{\n\taddontitle\t\"x\"\n}")
                            == null;

        var install = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OverlayHudCheck",
                                             $"version-{Guid.NewGuid():N}");

        // One install per case: the probe caches per addons folder, and a shared folder
        // would have each case answering with the previous one's pack.
        string Case(string name, string? addonVersion, bool versionInPreload = true)
        {
            var game = System.IO.Path.Combine(install, name, "left4dead2");
            var addons = System.IO.Path.Combine(game, "addons");
            System.IO.Directory.CreateDirectory(addons);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(game, "ems", "overlay_hud"));

            var pack = "overlay_hud_export.vpk";
            WritePack(System.IO.Path.Combine(addons, pack), withExporter: true, addonVersion,
                      versionInPreload);

            System.IO.File.WriteAllText(System.IO.Path.Combine(game, "addonlist.txt"),
                $"\"AddonList\"\n{{\n\t\t\"{pack}\"\t\t\"1\"\n}}\n");

            return System.IO.Path.Combine(game, "ems", "overlay_hud", "state.json");
        }

        VersionCheck Verdict(string statePath, string? exported = null) =>
            VersionGate.Check(AddonProbe.Refresh(statePath), exported);

        // A small file can be stored inline in the directory tree or in the data section
        // after it. vpk.exe picks; both have to be readable.
        var behindPath = Case("app-behind", newer);
        var behind = Verdict(behindPath);
        bool appBehindIsSeen = behind.Verdict == VersionVerdict.AppBehind
            && behind.AddonVersion == newer && behind.AppVersion == same;

        var dataSection = Verdict(Case("data-section", newer, versionInPreload: false));
        bool dataSectionIsRead = dataSection.Verdict == VersionVerdict.AppBehind
            && dataSection.AddonVersion == newer;

        bool matchIsQuiet = Verdict(Case("matched", same)) is
            { Verdict: VersionVerdict.Matched, Mismatched: false };

        bool addonBehindIsSeen = Verdict(Case("addon-behind", older)).Verdict
            == VersionVerdict.AddonBehind;

        // No manifest to read: the exporter stamps its version into every state file, so a
        // running round still answers the question.
        var unreadable = Case("no-manifest", null);
        bool unknownIsSilent = !Verdict(unreadable).Mismatched
            && Verdict(unreadable).Verdict == VersionVerdict.Unknown;
        bool exportedVersionIsTheFallback = Verdict(unreadable, newer).Verdict
            == VersionVerdict.AppBehind;

        // A version that is not a version claims nothing rather than warning about nothing.
        bool junkIsIgnored = Verdict(Case("junk", "not-a-version")).Verdict
            == VersionVerdict.Unknown;

        // ...and the overlay says so, during a round as well as at the menu. Someone who
        // only ever holds Tab mid-round would never see a menu-only message.
        var window = new MainWindow { Width = 1920, Height = 1080 };
        var config = (AppConfig)GetField(window, "_cfg", flags);
        config.GameProcess = "OverlayHudCheckNoSuchGame";
        config.AlwaysShow = true;
        config.IgnoreForeground = true;
        config.ShowStatusBadge = true;
        config.ExporterProven = true;
        Invoke(window, "SetSurface", flags, 1920.0, 1080.0);
        typeof(MainWindow).GetField("_gameForeground", flags)!.SetValue(window, true);

        var badge = (Border)GetField(window, "MenuBadge", flags);
        var notice = (Border)GetField(window, "Notice", flags);
        var noticeTitle = (TextBlock)GetField(window, "NoticeTitle", flags);
        var noticeBody = (TextBlock)GetField(window, "NoticeBody", flags);

        // A round actually running: the file has to be seen advancing, or the reader is
        // stale and this proves nothing about the in-game case.
        void Export(string statePath, long seq) =>
            System.IO.File.WriteAllText(statePath,
                $"{{\"v\":\"{newer}\",\"seq\":{seq},\"time\":1,\"count\":1,\"survivors\":[" +
                "{\"uid\":1,\"name\":\"A\",\"cls\":\"soldier\",\"hp\":100,\"maxhp\":100}]}");

        StateReader Watch(string statePath)
        {
            var reader = new StateReader(statePath, TimeSpan.FromMilliseconds(100), 5.0);
            typeof(MainWindow).GetField("_reader", flags)!.SetValue(window, reader);

            AddonProbe.Refresh(statePath);

            Export(statePath, 1);
            Invoke(reader, "Poll", flags);
            Export(statePath, 2);
            Invoke(reader, "Poll", flags);

            typeof(MainWindow).GetField("_dirty", flags)!.SetValue(window, true);
            Invoke(window, "Render", flags);

            return reader;
        }

        var live = Watch(behindPath);
        bool roundIsLive = live.HasExported && !live.IsStale;
        bool warnsInGame = roundIsLive
            && badge.Visibility == Visibility.Visible
            && notice.Visibility == Visibility.Visible
            && noticeTitle.Text == "UPDATE THE OVERLAY APP"
            && noticeBody.Text.Contains(AppIdentity.ReleasesUrl, StringComparison.Ordinal)
            && noticeBody.Text.Contains($"v{newer}", StringComparison.Ordinal);
        live.Dispose();

        // Matched versions during a round: nothing to say, and nothing on screen.
        var matchedInstall = Case("matched-live", same);
        var quiet = Watch(matchedInstall);
        bool quietWhenMatched = badge.Visibility != Visibility.Visible
            && notice.Visibility != Visibility.Visible;
        quiet.Dispose();

        // The editor is where the link can be clicked - the overlay is click-through.
        AddonProbe.Refresh(behindPath);
        var editor = new SettingsWindow(new AppConfig { StatePath = behindPath }, () => { });
        Invoke(editor, "UpdateVersionBanner", flags);

        var bannerBorder = (Border)GetField(editor, "UpdateBanner", flags);
        var bannerTitle = (TextBlock)GetField(editor, "UpdateBannerTitle", flags);
        var bannerBody = (TextBlock)GetField(editor, "UpdateBannerBody", flags);
        var link = (System.Windows.Documents.Hyperlink)GetField(editor, "ReleasesLink", flags);
        var linkText = (System.Windows.Documents.Run)GetField(editor, "ReleasesLinkText", flags);

        bool editorOffersTheLink = bannerBorder.Visibility == Visibility.Visible
            && bannerTitle.Text == "Update the overlay app"
            && bannerBody.Text.Contains($"v{newer}", StringComparison.Ordinal)
            && link.NavigateUri?.AbsoluteUri == AppIdentity.ReleasesUrl
            && linkText.Text == AppIdentity.ReleasesUrl;

        AddonProbe.Refresh(matchedInstall);
        var quietEditor = new SettingsWindow(new AppConfig { StatePath = matchedInstall },
                                             () => { });
        Invoke(quietEditor, "UpdateVersionBanner", flags);
        bool editorIsQuietWhenMatched =
            ((Border)GetField(quietEditor, "UpdateBanner", flags)).Visibility
                != Visibility.Visible;

        bool passed = readsQuoted && readsBare && absentIsNull
            && appBehindIsSeen && dataSectionIsRead && matchIsQuiet && addonBehindIsSeen
            && unknownIsSilent && exportedVersionIsTheFallback && junkIsIgnored
            && warnsInGame && quietWhenMatched
            && editorOffersTheLink && editorIsQuietWhenMatched;

        Console.WriteLine(
            $"readsQuoted={readsQuoted} readsBare={readsBare} absentIsNull={absentIsNull}");
        Console.WriteLine(
            $"appBehind={appBehindIsSeen} dataSection={dataSectionIsRead} " +
            $"matched={matchIsQuiet} addonBehind={addonBehindIsSeen}");
        Console.WriteLine(
            $"unknownIsSilent={unknownIsSilent} exportedFallback={exportedVersionIsTheFallback} " +
            $"junkIgnored={junkIsIgnored}");
        Console.WriteLine(
            $"warnsInGame={warnsInGame} (round live={roundIsLive}) quietWhenMatched={quietWhenMatched}");
        Console.WriteLine(
            $"editorLink={editorOffersTheLink} editorQuiet={editorIsQuietWhenMatched}");
        Console.WriteLine(passed ? "PASS" : "FAIL: the version gate is not reporting honestly");

        editor.Close();
        quietEditor.Close();
        window.Close();
        app.Shutdown();

        try { System.IO.Directory.Delete(install, recursive: true); } catch { }

        return passed ? 0 : 1;
    }

    /// <summary>
    /// Writes a real VPK v2 whose directory tree either carries the exporter's script or
    /// does not. Fixtures have to be real packs: the app identifies an addon by opening it,
    /// because a Workshop subscription's filename is a publishedfileid and says nothing.
    ///
    /// With <paramref name="addonVersion"/> given the pack also carries an addoninfo.txt at
    /// its root, the way a built addon does. <paramref name="versionInPreload"/> chooses
    /// which of the two places vpk.exe can put a small file's bytes: inline in the directory
    /// tree, or in the data section after it. Both have to be readable.
    /// </summary>
    private static void WritePack(string path, bool withExporter, string? addonVersion = null,
                                  bool versionInPreload = true)
    {
        var info = addonVersion == null
            ? null
            : System.Text.Encoding.ASCII.GetBytes(
                $"\"AddonInfo\"\n{{\n\taddontitle\t\t\"Overlay HUD Export\"\n" +
                $"\taddonversion\t\t\"{addonVersion}\"\n}}\n");

        var tree = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(tree, System.Text.Encoding.ASCII, true))
        {
            void Text(string value)
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes(value));
                writer.Write((byte)0);
            }

            void Block(string extension, string directory, string name, byte[]? preload,
                       uint length)
            {
                Text(extension);
                Text(directory);
                Text(name);

                writer.Write((uint)0);                        // crc
                writer.Write((ushort)(preload?.Length ?? 0)); // preload bytes
                writer.Write((ushort)0x7FFF);                 // archive index
                writer.Write((uint)0);                        // entry offset
                writer.Write(length);                         // entry length
                writer.Write((ushort)0xFFFF);                 // terminator
                if (preload != null) writer.Write(preload);

                Text("");                                     // end of names
                Text("");                                     // end of directories
            }

            Block(withExporter ? "nut" : "txt",
                  withExporter ? "scripts/vscripts" : "materials/readme",
                  withExporter ? "overlay_hud_export" : "notes",
                  null, 0);

            // VPK spells the root directory " ", which is where addoninfo.txt lives.
            if (info != null)
            {
                Block("txt", " ", "addoninfo",
                      versionInPreload ? info : null,
                      versionInPreload ? 0u : (uint)info.Length);
            }

            Text("");                          // end of extensions
        }

        var bytes = tree.ToArray();

        using var file = System.IO.File.Create(path);
        using var header = new System.IO.BinaryWriter(file);
        header.Write(0x55AA1234u);             // signature
        header.Write(2u);                      // version
        header.Write((uint)bytes.Length);      // tree size
        header.Write(0u);                      // file data section
        header.Write(0u);                      // archive md5 section
        header.Write(0u);                      // other md5 section
        header.Write(0u);                      // signature section
        header.Write(bytes);

        // The data section starts immediately after the tree, which is what an entry offset
        // of zero points at.
        if (info != null && !versionInPreload) header.Write(info);
    }

    /// <summary>
    /// The debug console is driven by polls, so an honest log depends entirely on only
    /// recording changes. A console that reprints the same line four times a second is one
    /// nobody reads.
    /// </summary>
    private static int RunDebugLogCheck()
    {
        DebugLog.Clear();

        var seen = new List<string>();
        void Watch(string line) => seen.Add(line);
        DebugLog.LineAdded += Watch;

        DebugLog.Note("exporter", "state", "exporting");
        DebugLog.Note("exporter", "state", "exporting");
        DebugLog.Note("exporter", "state", "exporting");
        bool repeatsAreDropped = seen.Count == 1;

        DebugLog.Note("exporter", "state", "export stopped");
        DebugLog.Note("exporter", "state", "exporting");
        bool changesAreKept = seen.Count == 3;

        // Different keys do not mask each other, or one busy poll silences the rest.
        DebugLog.Note("panel", "render", "exporting");
        bool keysAreIndependent = seen.Count == 4;

        DebugLog.Write("input", "hook installed");
        DebugLog.Write("input", "hook installed");
        bool writeIsAlwaysKept = seen.Count == 6;

        bool timestamped = seen.All(line => line.Length > 13 && line[2] == ':' && line[5] == ':');

        // After a clear, the first line of a state is new information again.
        DebugLog.Clear();
        DebugLog.Note("exporter", "state", "exporting");
        bool clearForgetsNotes = seen.Count == 7 && DebugLog.Snapshot().Count == 1;

        DebugLog.LineAdded -= Watch;

        // The buffer is bounded, so a long session cannot grow it without limit.
        DebugLog.Clear();
        for (int i = 0; i < DebugLog.Capacity + 50; i++) DebugLog.Write("state", $"line {i}");
        var snapshot = DebugLog.Snapshot();
        bool bounded = snapshot.Count == DebugLog.Capacity
            && snapshot[^1].EndsWith($"line {DebugLog.Capacity + 49}", StringComparison.Ordinal);

        DebugLog.Clear();

        // The window itself: it must load, start from the buffer rather than empty, and
        // follow lines logged after it opened.
        var app = new App();
        app.InitializeComponent();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        DebugLog.Write("state", "before the console opened");
        var console = new DebugWindow(() => "summary");
        var logText = (TextBox)GetField(console, "LogText", flags);
        var summaryText = (TextBlock)GetField(console, "SummaryText", flags);

        bool opensWithHistory = logText.Text.Contains("before the console opened",
                                                      StringComparison.Ordinal)
            && summaryText.Text == "summary";

        DebugLog.Write("state", "after the console opened");
        bool followsNewLines = logText.Text.Contains("after the console opened",
                                                     StringComparison.Ordinal);

        console.Close();
        DebugLog.Write("state", "after the console closed");
        bool detachesOnClose = !logText.Text.Contains("after the console closed",
                                                      StringComparison.Ordinal);
        app.Shutdown();
        DebugLog.Clear();

        bool passed = repeatsAreDropped && changesAreKept && keysAreIndependent
            && writeIsAlwaysKept && timestamped && clearForgetsNotes && bounded
            && opensWithHistory && followsNewLines && detachesOnClose;

        Console.WriteLine(
            $"repeatsAreDropped={repeatsAreDropped} changesAreKept={changesAreKept} " +
            $"keysAreIndependent={keysAreIndependent} writeIsAlwaysKept={writeIsAlwaysKept} " +
            $"timestamped={timestamped} clearForgetsNotes={clearForgetsNotes} bounded={bounded} " +
            $"opensWithHistory={opensWithHistory} followsNewLines={followsNewLines} " +
            $"detachesOnClose={detachesOnClose}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: the debug log must record changes, not polls, and stay bounded");

        return passed ? 0 : 1;
    }

    /// <summary>
    /// The overlay stopping mid-session was a keyboard hook Windows had silently removed,
    /// leaving the hold state stuck at false with nothing to notice or repair it.
    /// </summary>
    private static int RunHookRecoveryCheck()
    {
        const int tab = 0x09;
        const int insert = 0x2D;

        // Agreement is the common case and must never churn the hook.
        var quiet = new HookWatchdog();
        var quietUp = quiet.Observe(false, false);
        var quietDown = quiet.Observe(true, true);

        // A single disagreeing sample is a poll-boundary race: fix the state, keep the hook.
        var boundary = new HookWatchdog();
        var firstMiss = boundary.Observe(true, false);
        var recovered = boundary.Observe(true, true);

        // A hook that is really gone keeps disagreeing.
        var dead = new HookWatchdog();
        dead.Observe(false, true);
        var deadSecond = dead.Observe(false, true);

        // ...and after the reinstall the count starts over, so one replacement per failure.
        dead.Reset();
        var afterReset = dead.Observe(false, true);

        // The stuck state itself has to be repairable, in both directions.
        var state = new KeyboardChordState(tab, insert);
        state.Process(tab, true, false, true);
        bool? releaseMissed = state.Sync(false);
        bool clearedHold = !state.IsHeld;
        bool? pressMissed = state.Sync(true);
        bool? noChange = state.Sync(true);

        bool passed = !quietUp.Resync && !quietUp.Reinstall
            && !quietDown.Resync && !quietDown.Reinstall
            && firstMiss.Resync && !firstMiss.Reinstall
            && !recovered.Resync && !recovered.Reinstall
            && deadSecond.Resync && deadSecond.Reinstall
            && !afterReset.Reinstall
            && releaseMissed == false && clearedHold
            && pressMissed == true && noChange == null;

        Console.WriteLine(
            $"agreementIsQuiet={!quietUp.Reinstall && !quietDown.Reinstall} " +
            $"singleMissResyncsOnly={firstMiss.Resync && !firstMiss.Reinstall} " +
            $"persistentMissReinstalls={deadSecond.Reinstall} " +
            $"reinstallCountResets={!afterReset.Reinstall} " +
            $"stuckHoldIsRepaired={releaseMissed == false && pressMissed == true} " +
            $"syncIsIdempotent={noChange == null}");
        Console.WriteLine(passed
            ? "PASS"
            : "FAIL: a dead keyboard hook is no longer detected or repaired");
        return passed ? 0 : 1;
    }

    private static object GetField(object instance, string name, BindingFlags flags) =>
        instance.GetType().GetField(name, flags)?.GetValue(instance)
        ?? throw new MissingFieldException(instance.GetType().FullName, name);

    private static object Invoke(object instance, string name, BindingFlags flags,
                                 params object[] arguments)
    {
        var method = instance.GetType().GetMethods(flags).SingleOrDefault(candidate =>
            candidate.Name == name && candidate.GetParameters().Length == arguments.Length);

        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, name);

        return method.Invoke(instance, arguments) ?? new object();
    }
}
