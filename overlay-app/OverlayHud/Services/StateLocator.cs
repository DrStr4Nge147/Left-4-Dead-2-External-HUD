using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OverlayHud.Services;

/// <summary>
/// Finds left4dead2/ems/overlay_hud_state.json without the user having to type a path.
/// The file itself may not exist yet - what is located is the game install.
/// </summary>
public static class StateLocator
{
    private const string RelativeState = @"left4dead2\ems\overlay_hud_state.json";
    private const string GameMarker    = @"left4dead2\gameinfo.txt";

    /// <summary>
    /// An install that already has a state file wins over one that merely exists. A
    /// machine can carry more than one L4D2 install (a stub on C:, the real one on
    /// another drive) and picking the first one found lands on the wrong install.
    /// </summary>
    public static string? Locate()
    {
        string? firstInstall = null;

        foreach (var lib in SteamLibraries())
        {
            var game = Path.Combine(lib, "steamapps", "common", "Left 4 Dead 2");

            if (!File.Exists(Path.Combine(game, GameMarker))) continue;

            var state = Path.Combine(game, RelativeState);

            if (File.Exists(state)) return state;

            firstInstall ??= state;
        }

        return firstInstall;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var path = ReadSteamPath(RegistryHive.CurrentUser, view, @"Software\Valve\Steam", "SteamPath")
                    ?? ReadSteamPath(RegistryHive.LocalMachine, view, @"SOFTWARE\Valve\Steam", "InstallPath");

            if (path != null && !roots.Contains(path)) roots.Add(path);
        }

        foreach (var fallback in new[]
                 {
                     @"C:\Program Files (x86)\Steam",
                     @"C:\Steam"
                 })
        {
            if (!roots.Contains(fallback)) roots.Add(fallback);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (seen.Add(root)) yield return root;

            // Additional library drives are listed in libraryfolders.vdf.
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            string text;
            try { text = File.ReadAllText(vdf); }
            catch { continue; }

            foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
            {
                var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                if (seen.Add(lib)) yield return lib;
            }
        }
    }

    private static string? ReadSteamPath(RegistryHive hive, RegistryView view, string subKey, string value)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(value) as string;
        }
        catch
        {
            return null;
        }
    }
}
