namespace OverlayHud.Services;

/// <summary>
/// Calibrated limits that describe L4D2's scoreboard geometry, not user preferences.
/// Keeping them out of config prevents the editor from suggesting that it can resize the
/// vanilla sidebar or that overflow fitting is optional.
/// </summary>
internal static class LayoutPolicy
{
    public const double MinUserScale = 0.6;
    public const double MaxUserScale = 1.0;
    public const double MaxFitScale = 1.4;

    /// <summary>
    /// Absolute right boundary of the vanilla scoreboard region, as a fraction of window
    /// width. Measured from a 1920x1080 in-game capture: the scoreboard's survivor rows
    /// end at x=722, so the panel may use everything up to 0.376 rather than stopping
    /// 25 px short of it as the earlier 0.36 estimate did.
    /// </summary>
    public const double SidebarWidthFraction = 0.376;
}
