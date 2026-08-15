namespace OverlayHud.Services;

/// <summary>How the installed addon's version compares to this build's.</summary>
internal enum VersionVerdict
{
    /// <summary>No addon version could be read. Claims nothing and warns about nothing.</summary>
    Unknown,

    Matched,

    /// <summary>The addon is newer: this app is the half that needs downloading.</summary>
    AppBehind,

    /// <summary>The app is newer: the addon has not re-synced yet.</summary>
    AddonBehind
}

internal readonly record struct VersionCheck(VersionVerdict Verdict, string AppVersion,
                                             string AddonVersion)
{
    public bool Mismatched => Verdict is VersionVerdict.AppBehind or VersionVerdict.AddonBehind;
}

/// <summary>
/// Compares the exporter addon's version against this build's.
///
/// The two halves ship on one number but are updated by different routes: the addon comes
/// down from the Workshop on its own, and the app has to be fetched by hand. So the addon is
/// the half that runs ahead, and a mismatch is nearly always the app being stale. Nothing is
/// blocked by a mismatch - the HUD works across versions - it is only said out loud, because
/// otherwise the user is running an old app with no way to know it.
///
/// The addon's own addoninfo.txt is the source of truth: it is readable from the installed
/// pack at the main menu, before any map has loaded and before anything has been exported.
/// The version stamped into state.json is the fallback, for an install whose manifest could
/// not be read.
/// </summary>
internal static class VersionGate
{
    public static VersionCheck Check(AddonPresence addon, string? exportedVersion)
    {
        string app = AppIdentity.DisplayVersion;
        string? addonText = First(addon.Version, exportedVersion);

        if (addonText == null || !TryParse(addonText, out var addonVersion)
            || !TryParse(app, out var appVersion))
        {
            return new VersionCheck(VersionVerdict.Unknown, app, addonText ?? "");
        }

        var verdict = appVersion.CompareTo(addonVersion) switch
        {
            < 0 => VersionVerdict.AppBehind,
            > 0 => VersionVerdict.AddonBehind,
            _   => VersionVerdict.Matched
        };

        return new VersionCheck(verdict, app, Display(addonVersion));
    }

    private static string? First(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    /// <summary>
    /// Accepts what either half actually writes: "1.0.9", and a bare "1.0" or four-part
    /// assembly version if one ever appears. Anything else is unknown rather than a warning.
    /// </summary>
    private static bool TryParse(string text, out Version version)
    {
        version = new Version(0, 0, 0);

        var trimmed = text.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(trimmed, out var parsed)) return false;

        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));

        return true;
    }

    private static string Display(Version version) =>
        $"{version.Major}.{version.Minor}.{version.Build}";
}
