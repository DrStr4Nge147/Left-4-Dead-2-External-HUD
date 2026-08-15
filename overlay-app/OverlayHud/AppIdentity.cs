namespace OverlayHud;

public static class AppIdentity
{
    public const string Name = "Left 4 Dead 2 Customized Overlay HUD - External";

    /// <summary>Shown in the editor and the tray menu, and matched by build metadata.</summary>
    public const string Author = "DrStr4nge";

    /// <summary>
    /// Where a newer build comes from. The addon half updates itself through Steam and the
    /// app half does not, so this is the one address that closes the gap.
    /// </summary>
    public const string ReleasesUrl =
        "https://github.com/DrStr4Nge147/Left-4-Dead-2-External-HUD/releases";

    /// <summary>
    /// This build's version. The addon ships on the same number, which is what makes a
    /// comparison against the installed pack meaningful - see <c>Services/VersionGate</c>.
    /// </summary>
    public static Version Version =>
        typeof(AppIdentity).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>x.y.z, matching how the addon states its own version.</summary>
    public static string DisplayVersion =>
        $"{Version.Major}.{Version.Minor}.{Math.Max(0, Version.Build)}";
}
