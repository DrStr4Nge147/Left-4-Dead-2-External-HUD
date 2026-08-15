namespace OverlayHud.Services;

/// <summary>
/// The persistent HUD is deliberately a small set of named placements rather than a second
/// free-form coordinate editor. Each template describes where the persistent layout belongs, so
/// a user can change the role of the panel without recreating the scoreboard layout.
/// </summary>
internal static class ConsistentHudPolicy
{
    public const int MinimumColumns = 4;
    public const int SeparateRosterColumns = 3;
    public const int MaximumRows = 3;
    public const double SeparateYouGapFraction = 0.025;

    public const string VanillaBottomCenter = "vanilla-bottom-center";
    public const string VanillaVertical = "vanilla-vertical";
    public const string LowerRightVertical = "lower-right-vertical";
    public const string BasicDesign = "basic";
    public const string MinimalistDesign = "minimalist";

    public readonly record struct DesignDefaults(
        double Scale,
        double Opacity,
        double VerticalPosition,
        double HorizontalSpacing,
        double VerticalSpacing,
        bool Monochrome);

    // Retained only as migration aliases for configs created before the current template
    // set became the default. Neither retired value is exposed as an editor option.
    private const string LegacyBottomRight = "bottom-right";
    private const string LegacyTopHorizontal = "top-vertical";

    public readonly record struct Placement(string Anchor, double HorizontalInset, double VerticalInset);

    public static string Parse(string? value) => value?.ToLowerInvariant() switch
    {
        VanillaBottomCenter => VanillaBottomCenter,
        VanillaVertical => VanillaVertical,
        LowerRightVertical => LowerRightVertical,
        LegacyBottomRight => VanillaBottomCenter,
        LegacyTopHorizontal => VanillaBottomCenter,
        _ => VanillaBottomCenter
    };

    public static Placement For(string? value) => Parse(value) switch
    {
        VanillaVertical => new Placement("BottomLeft", 0.02, 0.035),
        VanillaBottomCenter => new Placement("BottomCenter", 0.02, 0.035),
        LowerRightVertical => new Placement("BottomRight", 0.02, 0.035),
        _ => new Placement("BottomCenter", 0.02, 0.035)
    };

    public static bool IsVertical(string? value)
    {
        string template = Parse(value);
        return template == VanillaVertical || template == LowerRightVertical;
    }

    public static string ParseDesign(string? value) => value?.ToLowerInvariant() switch
    {
        MinimalistDesign => MinimalistDesign,
        _ => BasicDesign
    };

    public static DesignDefaults DefaultsFor(string? value) => ParseDesign(value) switch
    {
        MinimalistDesign => new DesignDefaults(1.00, 0.90, 0.03, 10, 0, true),
        _ => new DesignDefaults(0.65, 0.90, 0.03, 10, 0, false)
    };

    /// <summary>
    /// Splits the consistent-HUD roster into horizontal rows. The normal layout starts at
    /// four cards across; a separated local-player card can request three columns so the
    /// remaining roster uses more of the space beside it. There are no more than three rows;
    /// larger rosters add columns so no cards are dropped.
    /// </summary>
    public static List<List<T>> SplitRows<T>(IReadOnlyList<T> items,
                                             int minimumColumns = MinimumColumns)
    {
        if (items.Count == 0) return new List<List<T>>();

        int columnCount = Math.Max(Math.Max(1, minimumColumns),
                                   (int)Math.Ceiling(items.Count / (double)MaximumRows));
        int rowCount = (int)Math.Ceiling(items.Count / (double)columnCount);
        var rows = new List<List<T>>(rowCount);

        for (int row = 0; row < rowCount; row++)
        {
            int start = row * columnCount;
            rows.Add(items.Skip(start).Take(Math.Min(columnCount, items.Count - start)).ToList());
        }

        return rows;
    }
}
