namespace OverlayHud.Services;

/// <summary>
/// Placement and geometry for the reinforcement ring - one dial, drawn for the host player
/// only, saying whether <c>help!</c> can be called and how long until it can.
///
/// It follows the weapon HUD's placement rules rather than the roster's: a single fixed
/// element whose only real questions are which side it sits on, how far up from the bottom
/// edge, and how big. The default corner is the opposite of the weapon HUD's, so a player
/// who has changed neither setting gets the two of them in different corners.
/// </summary>
internal static class HelpRingPlacement
{
    public const string LowerLeft = "lower-left";
    public const string LowerRight = "lower-right";

    /// <summary>Fraction of the surface width kept between the ring and its corner.</summary>
    public const double HorizontalInset = 0.02;

    /// <summary>As the weapon HUD: short of 1.0, so it cannot be pushed off the top edge.</summary>
    public const double MaximumVerticalOffset = 0.92;

    public const double MinimumScale = 0.50;
    public const double MaximumScale = 2.00;

    /// <summary>
    /// Ring size in layout pixels, before any scale. Sized against the weapon HUD's slot
    /// boxes so the two read as one set of panels rather than as an overlay on an overlay.
    /// </summary>
    public const double Diameter = 46.0;

    /// <summary>Ring thickness. Wide enough to carry colour at the scales the HUD is used at.</summary>
    public const double Thickness = 5.0;

    public static string ParseCorner(string? value) => value?.ToLowerInvariant() switch
    {
        LowerRight => LowerRight,
        _ => LowerLeft
    };

    public static bool IsLeft(string? corner) => ParseCorner(corner) == LowerLeft;

    public static double ClampScale(double value) =>
        Math.Clamp(value, MinimumScale, MaximumScale);

    public static double ClampVerticalOffset(double value) =>
        Math.Clamp(value, 0.0, MaximumVerticalOffset);
}
