using System.Windows;

namespace OverlayHud.Services;

/// <summary>
/// Converts the user-facing spacing controls into the margin shared by every consistent-HUD
/// survivor card. The existing one-pixel card breathing room remains at zero spacing, while
/// negative values are allowed to produce intentional overlap.
/// </summary>
internal static class ConsistentHudSpacing
{
    public const double DefaultHorizontal = 0.0;
    public const double DefaultVertical = 0.0;
    public const double MinimumHorizontal = -100.0;
    public const double MaximumHorizontal = 100.0;
    public const double MinimumVertical = -20.0;
    public const double MaximumVertical = 40.0;

    public static Thickness CardMargin(double horizontal, double vertical)
    {
        double h = Math.Clamp(horizontal, MinimumHorizontal, MaximumHorizontal) / 2.0;
        double v = 1.0 + Math.Clamp(vertical, MinimumVertical, MaximumVertical) / 2.0;
        return new Thickness(h, v, h, v);
    }
}
