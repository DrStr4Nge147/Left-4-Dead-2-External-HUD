using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OverlayHud.Model;

public sealed class AppConfig
{
    /// <summary>Full path to overlay_hud_state.json. Empty = auto-locate.</summary>
    [JsonPropertyName("statePath")] public string StatePath { get; set; } = "";

    /// <summary>Process name of the game, without .exe.</summary>
    [JsonPropertyName("gameProcess")] public string GameProcess { get; set; } = "left4dead2";

    /// <summary>Virtual-key code to hold. 0x09 = Tab.</summary>
    [JsonPropertyName("holdKey")] public int HoldKey { get; set; } = 0x09;

    /// <summary>Virtual-key code used with holdKey to open the editor. 0x2D = Insert; 0 disables.</summary>
    [JsonPropertyName("editorKey")] public int EditorKey { get; set; } = 0x2D;

    /// <summary>
    /// Start the consistent HUD enabled, ignoring the scoreboard hold key. The configured
    /// consistentKey toggles this value while the game is in the foreground.
    /// </summary>
    [JsonPropertyName("alwaysShow")] public bool AlwaysShow { get; set; }

    /// <summary>Virtual-key code that toggles the consistent HUD. 0x76 = F7; 0 disables.</summary>
    [JsonPropertyName("consistentKey")] public int ConsistentKey { get; set; } = 0x76;

    /// <summary>Draw even when L4D2 is not the foreground window. Debug aid.</summary>
    [JsonPropertyName("ignoreForeground")] public bool IgnoreForeground { get; set; }

    /// <summary>Exit after an observed L4D2 process closes. Does not exit while waiting for startup.</summary>
    [JsonPropertyName("exitWhenGameCloses")] public bool ExitWhenGameCloses { get; set; } = true;

    /// <summary>TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight.</summary>
    [JsonPropertyName("anchor")] public string Anchor { get; set; } = "TopLeft";

    /// <summary>"percent" of the game window (default) or "pixels".</summary>
    [JsonPropertyName("offsetUnits")] public string OffsetUnits { get; set; } = "percent";

    /// <summary>Default 0.02 / 0.59 puts the panel just under the vanilla scoreboard.</summary>
    [JsonPropertyName("offsetX")] public double OffsetX { get; set; } = 0.02;
    [JsonPropertyName("offsetY")] public double OffsetY { get; set; } = 0.59;

    /// <summary>Scale the panel with the game window height so it looks the same at any resolution.</summary>
    [JsonPropertyName("autoScale")] public bool AutoScale { get; set; } = true;

    /// <summary>Window height at which scale 1.0 means 1.0.</summary>
    [JsonPropertyName("baselineHeight")] public double BaselineHeight { get; set; } = 1080;

    /// <summary>Overall UI scale, on top of autoScale.</summary>
    [JsonPropertyName("scale")] public double Scale { get; set; } = 1.0;

    /// <summary>Scale used by the consistent HUD, independently of the scoreboard panel.</summary>
    [JsonPropertyName("consistentScale")] public double ConsistentScale { get; set; } = 0.65;

    /// <summary>Opacity used by the consistent HUD, independently of the scoreboard panel.</summary>
    [JsonPropertyName("consistentOpacity")] public double ConsistentOpacity { get; set; } = 0.90;

    /// <summary>Card design used only by the consistent HUD: "basic" or "minimalist".</summary>
    [JsonPropertyName("consistentDesign")] public string ConsistentDesign { get; set; } = "basic";

    /// <summary>Whether the consistent HUD prints numeric health values.</summary>
    [JsonPropertyName("consistentShowHealthNumbers")]
    public bool ConsistentShowHealthNumbers { get; set; } = true;

    /// <summary>Render the consistent HUD using grayscale colors instead of health-state colors.</summary>
    [JsonPropertyName("consistentMonochrome")]
    public bool ConsistentMonochrome { get; set; }

    /// <summary>
    /// Fraction of the game window kept below the consistent HUD. Zero touches the bottom
    /// edge; higher values move the HUD upward. This is independent of the scoreboard offset.
    /// </summary>
    [JsonPropertyName("consistentVerticalOffset")] public double ConsistentVerticalOffset { get; set; } = 0.03;

    /// <summary>Extra horizontal gap between consistent-HUD cards, in layout pixels.</summary>
    [JsonPropertyName("consistentHorizontalSpacing")] public double ConsistentHorizontalSpacing { get; set; } = 10;

    /// <summary>Extra vertical gap between consistent-HUD cards, in layout pixels.</summary>
    [JsonPropertyName("consistentVerticalSpacing")] public double ConsistentVerticalSpacing { get; set; }

    /// <summary>
    /// Moves the local listen-server host's card out of the shared consistent-HUD roster
    /// and places it at the lower-right edge. The scoreboard view never uses this option.
    /// </summary>
    [JsonPropertyName("consistentSeparateYou")] public bool ConsistentSeparateYou { get; set; } = true;

    /// <summary>Smallest fraction of the resolution-scaled size used by overflow fitting.</summary>
    [JsonPropertyName("minScale")] public double MinScale { get; set; } = 0.35;

    /// <summary>
    /// Optional fraction of the window height kept clear at the bottom. The default is
    /// zero because the vanilla survivor HUD is hidden while the Tab scoreboard is open.
    /// </summary>
    [JsonPropertyName("bottomReserve")] public double BottomReserve { get; set; }

    [JsonIgnore]
    public bool OffsetsArePercent =>
        !string.Equals(OffsetUnits, "pixels", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Show the top-right name/version badge while L4D2 is focused and no round is
    /// exporting. It means "the exporter is not writing" — useful while setting the addon
    /// up, noise once it is known to work.
    /// </summary>
    [JsonPropertyName("showStatusBadge")] public bool ShowStatusBadge { get; set; } = true;

    /// <summary>Panel opacity, 0..1.</summary>
    [JsonPropertyName("opacity")] public double Opacity { get; set; } = 0.92;

    /// <summary>
    /// Which part of the roster the panel draws: "all" (every mortal survivor, including
    /// the four vanilla slots), "extras" (the previous All behavior), "soldiers" (mortal
    /// soldiers and followers), or "followers". Immortal team-4 holdout soldiers are
    /// excluded by all four.
    /// </summary>
    [JsonPropertyName("rosterFilter")] public string RosterFilter { get; set; } = "all";

    /// <summary>
    /// Which preview the editor opens with: "live" (the real overlay, the default) or
    /// "simulated" (its own canvas). Editor state, not overlay behavior - live preview only
    /// ever draws while the editor is open. Live is the default because it shows the actual
    /// panel at actual size over the actual game; the simulated canvas is the fallback for
    /// laying out with L4D2 closed.
    /// </summary>
    [JsonPropertyName("previewMode")] public string PreviewMode { get; set; } = "live";

    /// <summary>Blocks out the vanilla scoreboard region in both previews.</summary>
    [JsonPropertyName("previewScoreboard")] public bool PreviewScoreboard { get; set; } = true;

    /// <summary>
    /// Placement template for the persistent HUD: "vanilla-bottom-center",
    /// legacy "vanilla-vertical", or "lower-right-vertical". Retired "bottom-right" and
    /// "top-vertical" values migrate to "vanilla-bottom-center".
    /// </summary>
    [JsonPropertyName("consistentTemplate")] public string ConsistentTemplate { get; set; } = "vanilla-bottom-center";

    /// <summary>Cards per column before wrapping. 0 = work it out from the space available.</summary>
    [JsonPropertyName("cardsPerColumn")] public int CardsPerColumn { get; set; }

    /// <summary>Maximum horizontal columns. Extra cards make the panel shrink, not widen.</summary>
    [JsonPropertyName("maxColumns")] public int MaxColumns { get; set; } = 2;

    /// <summary>Seconds without a new seq before the panel is marked stale.</summary>
    [JsonPropertyName("staleAfterSeconds")] public double StaleAfterSeconds { get; set; } = 2.0;

    /// <summary>
    /// Set by the app the first time it sees this install export, and never cleared. It is
    /// what stops the `NO EXPORT` help from reappearing at every menu on every launch: that
    /// message is for a setup that has never worked, and once the addon has been seen
    /// working the app has no business claiming it might be missing.
    /// </summary>
    [JsonPropertyName("exporterProven")] public bool ExporterProven { get; set; }

    /// <summary>Open the debug console at startup. Turned on and off from the editor.</summary>
    [JsonPropertyName("debug")] public bool Debug { get; set; }

    public static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // A malformed config must not stop the overlay from starting - defaults are
            // always usable, and the status line will show whether state was found.
        }

        return new AppConfig();
    }

    public AppConfig Clone() => (AppConfig)MemberwiseClone();

    /// <summary>Copies settings exposed by the UI editor without touching transport/debug setup.</summary>
    public void CopyUiFrom(AppConfig source)
    {
        Anchor = source.Anchor;
        OffsetUnits = source.OffsetUnits;
        OffsetX = source.OffsetX;
        OffsetY = source.OffsetY;
        AutoScale = source.AutoScale;
        BaselineHeight = source.BaselineHeight;
        Scale = source.Scale;
        ConsistentScale = source.ConsistentScale;
        ConsistentOpacity = source.ConsistentOpacity;
        ConsistentDesign = source.ConsistentDesign;
        ConsistentShowHealthNumbers = source.ConsistentShowHealthNumbers;
        ConsistentMonochrome = source.ConsistentMonochrome;
        ConsistentVerticalOffset = source.ConsistentVerticalOffset;
        ConsistentHorizontalSpacing = source.ConsistentHorizontalSpacing;
        ConsistentVerticalSpacing = source.ConsistentVerticalSpacing;
        ConsistentSeparateYou = source.ConsistentSeparateYou;
        MinScale = source.MinScale;
        BottomReserve = source.BottomReserve;
        Opacity = source.Opacity;
        RosterFilter = source.RosterFilter;
        ConsistentTemplate = source.ConsistentTemplate;
        ShowStatusBadge = source.ShowStatusBadge;
        CardsPerColumn = source.CardsPerColumn;
        MaxColumns = source.MaxColumns;
    }

    /// <summary>Saves through a sibling temporary file so a failed write cannot corrupt config.</summary>
    public bool TrySave(out string error)
    {
        string temporary = ConfigPath + ".tmp";

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, options));
            File.Move(temporary, ConfigPath, true);
            error = "";
            return true;
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            error = "Could not save config. Check that the app folder is writable.";
            return false;
        }
    }
}
