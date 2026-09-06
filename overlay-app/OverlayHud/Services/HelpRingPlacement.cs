namespace OverlayHud.Services;

/// <summary>
/// Placement and geometry for the reinforcement ring - one dial, drawn for the host player
/// only, saying whether <c>help!</c> can be called and how long until it can.
///
/// It follows the weapon HUD's placement rules rather than the roster's: a single fixed
/// element whose only real questions are which side it sits on, how far up from the bottom
/// edge, and how big. It defaults to the corner the separated You card is in and stands on
/// top of it, because it is the player's own state and belongs beside their own health
/// rather than off in the roster's half of the screen.
/// </summary>
internal static class HelpRingPlacement
{
    public const string LowerLeft = "lower-left";
    public const string LowerRight = "lower-right";

    /// <summary>Fraction of the surface width kept between the ring and its corner.</summary>
    public const double HorizontalInset = 0.02;

    /// <summary>As the weapon HUD: short of 1.0, so it cannot be pushed off the top edge.</summary>
    public const double MaximumVerticalOffset = 0.92;

    /// <summary>
    /// The ring's own size range, and it is deliberately wider than the weapon HUD's. This is
    /// one dial carrying one number, sized independently of the HUD size slider, so the useful
    /// range runs from a small marker in the corner to something readable across the room.
    /// </summary>
    public const double MinimumScale = 0.40;
    public const double MaximumScale = 3.00;

    /// <summary>
    /// Gap in layout pixels between the ring and the card it stands on, when the two share a
    /// corner. Small enough to read as one stack, wide enough not to touch.
    /// </summary>
    public const double StackGap = 6.0;

    /// <summary>
    /// Ring size in layout pixels, before any scale. Sized against the weapon HUD's slot
    /// boxes so the two read as one set of panels rather than as an overlay on an overlay.
    /// </summary>
    public const double Diameter = 46.0;

    /// <summary>Ring thickness. Wide enough to carry colour at the scales the HUD is used at.</summary>
    public const double Thickness = 5.0;

    /// <summary>
    /// Text sizes inside the ring. A countdown is at most three digits and gets the largest;
    /// a word has to clear the stroke on both sides, and a six-letter one comes down again.
    /// </summary>
    public const double CountFontSize = 15.0;
    public const double WordFontSize = 9.0;
    public const double LongWordFontSize = 8.0;

    /// <summary>Longest word drawn at <see cref="WordFontSize"/> before it has to shrink.</summary>
    public const int ShortWordLength = 5;

    /// <summary>
    /// What size to draw a caption at, so the live ring, the editor's stand-in, and the review
    /// shot cannot drift apart on it.
    /// </summary>
    public static double FontSizeFor(string caption, bool isWord)
    {
        if (!isWord) return CountFontSize;
        return caption.Length > ShortWordLength ? LongWordFontSize : WordFontSize;
    }

    public static string ParseCorner(string? value) => value?.ToLowerInvariant() switch
    {
        LowerLeft => LowerLeft,
        _ => LowerRight
    };

    public static bool IsLeft(string? corner) => ParseCorner(corner) == LowerLeft;

    public static double ClampScale(double value) =>
        Math.Clamp(value, MinimumScale, MaximumScale);

    public static double ClampVerticalOffset(double value) =>
        Math.Clamp(value, 0.0, MaximumVerticalOffset);
}
