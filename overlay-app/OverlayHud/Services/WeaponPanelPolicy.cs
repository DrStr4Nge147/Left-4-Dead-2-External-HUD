namespace OverlayHud.Services;

/// <summary>
/// Placement rules for the weapon HUD - the local player's own primary and secondary
/// slots, drawn as their own panel rather than as part of a survivor card.
///
/// It gets two corners rather than the roster's template set. The roster is a list that
/// has to fit a variable number of cards; this is two fixed boxes, and the only real
/// question is which bottom corner they live in and how far up from the edge.
/// </summary>
internal static class WeaponPanelPolicy
{
    public const string LowerLeft = "lower-left";
    public const string LowerRight = "lower-right";

    public const string Vertical = "vertical";
    public const string Horizontal = "horizontal";

    /// <summary>Fraction of the surface width kept between the panel and its corner.</summary>
    public const double HorizontalInset = 0.02;

    /// <summary>
    /// Upper bound for the vertical slider. Stops just short of 1.0 so the panel cannot be
    /// pushed entirely off the top edge, where it would look like it had disappeared.
    /// </summary>
    public const double MaximumVerticalOffset = 0.92;

    /// <summary>
    /// Range for the weapon HUD's own size multiplier. It reaches above 1.0 because the
    /// panel is read mid-fight and the consistent HUD's own scale is tuned for a roster.
    /// </summary>
    public const double MinimumScale = 0.50;
    public const double MaximumScale = 2.00;

    public static string ParseCorner(string? value) => value?.ToLowerInvariant() switch
    {
        LowerLeft => LowerLeft,
        _ => LowerRight
    };

    public static string ParseOrientation(string? value) => value?.ToLowerInvariant() switch
    {
        Horizontal => Horizontal,
        _ => Vertical
    };

    public static bool IsLeft(string? corner) => ParseCorner(corner) == LowerLeft;

    public static bool IsHorizontal(string? orientation) =>
        ParseOrientation(orientation) == Horizontal;

    /// <summary>Clamps a saved size multiplier into the slider's range.</summary>
    public static double ClampScale(double value) =>
        Math.Clamp(value, MinimumScale, MaximumScale);

    /// <summary>Clamps a saved offset into the slider's range.</summary>
    public static double ClampVerticalOffset(double value) =>
        Math.Clamp(value, 0.0, MaximumVerticalOffset);
}
