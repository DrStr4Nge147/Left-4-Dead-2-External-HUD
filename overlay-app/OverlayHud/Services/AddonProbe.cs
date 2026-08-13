using System.IO;

namespace OverlayHud.Services;

/// <summary>One installed copy of the exporter, wherever it came from.</summary>
internal readonly record struct AddonPack(string Path, bool FromWorkshop, bool Enabled)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>What is actually installed, rather than what can be inferred from a quiet file.</summary>
internal readonly record struct AddonPresence(bool Located, IReadOnlyList<AddonPack>? PackList,
                                              string AddonsPath)
{
    /// <summary>Never null: the default value of this struct is "nothing known yet".</summary>
    public IReadOnlyList<AddonPack> Packs => PackList ?? Array.Empty<AddonPack>();

    public int Count => Packs.Count;

    public bool Missing => Located && Packs.Count == 0;

    /// <summary>Two copies mount, one wins, and which one is not predictable.</summary>
    public bool Duplicated => Located && Packs.Count > 1;

    /// <summary>Installed, but switched off in the game's Add-ons screen, so it never runs.</summary>
    public bool Disabled => Located && Packs.Count > 0 && Packs.All(pack => !pack.Enabled);

    public bool Installed => Located && Packs.Count == 1 && Packs[0].Enabled;
}

/// <summary>
/// Looks for the exporter on disk.
///
/// This exists because the transport cannot tell "the addon is not installed" from "no map
/// is loaded yet" - both are a file that is not advancing - and the app used to guess. It
/// guessed wrong at every main menu. What is in the addons folder is the evidence that
/// separates them.
///
/// Packs are identified by content, never by filename: a Workshop subscription is stored as
/// <c>addons\workshop\&lt;publishedfileid&gt;.vpk</c>, and a manual install keeps whatever
/// name it was dragged in with.
/// </summary>
internal static class AddonProbe
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private static readonly object Gate = new();

    private static AddonPresence _last;
    private static DateTime _lastProbeUtc = DateTime.MinValue;
    private static string? _lastRoot;
    private static string _lastFingerprint = "";
    private static bool _running;

    /// <summary>
    /// The cached answer, refreshed in the background. A busy install can hold a hundred
    /// packs and each one has to be opened to be identified, which is far too much to do on
    /// the UI thread from a 250 ms timer. Until the first scan lands the answer is "not
    /// located", which reads as unknown and claims nothing.
    /// </summary>
    public static AddonPresence Look(string? statePath)
    {
        string? addons = AddonsFolder(statePath);

        lock (Gate)
        {
            bool due = addons != _lastRoot || DateTime.UtcNow - _lastProbeUtc >= Interval;
            if (!due || _running) return _last;

            _running = true;
        }

        Task.Run(() =>
        {
            try { Refresh(statePath); }
            finally { lock (Gate) _running = false; }
        });

        lock (Gate) return _last;
    }

    /// <summary>
    /// Scans now, on the calling thread, and returns what it found. This is the whole probe;
    /// <see cref="Look"/> is this plus caching and a background thread to run it on.
    /// </summary>
    public static AddonPresence Refresh(string? statePath)
    {
        string? addons = AddonsFolder(statePath);
        var files = ListPacks(addons);
        string fingerprint = Fingerprint(files, addons);

        lock (Gate)
        {
            _lastProbeUtc = DateTime.UtcNow;

            // Same folder, same files: the parsed answer cannot have changed.
            if (addons == _lastRoot && fingerprint == _lastFingerprint) return _last;
        }

        var presence = Probe(addons, files);

        lock (Gate)
        {
            _lastRoot = addons;
            _lastFingerprint = fingerprint;
            _last = presence;

            return _last;
        }
    }

    private static AddonPresence Probe(string? addonsFolder, List<FileInfo> files)
    {
        if (addonsFolder == null || !Directory.Exists(addonsFolder))
            return new AddonPresence(false, Array.Empty<AddonPack>(), addonsFolder ?? "");

        var disabled = DisabledEntries(addonsFolder);
        var packs = new List<AddonPack>();

        foreach (var file in files)
        {
            if (!VpkReader.ContainsExporter(file.FullName)) continue;

            bool workshop = string.Equals(file.Directory?.Name, "workshop",
                                          StringComparison.OrdinalIgnoreCase);

            // addonlist.txt keys are relative to the addons folder, with a backslash for a
            // Workshop entry: "workshop\123456789.vpk".
            string key = workshop ? $"workshop\\{file.Name}" : file.Name;

            packs.Add(new AddonPack(file.FullName, workshop, !disabled.Contains(key)));
        }

        return new AddonPresence(true, packs, addonsFolder);
    }

    private static List<FileInfo> ListPacks(string? addonsFolder)
    {
        var files = new List<FileInfo>();
        if (addonsFolder == null) return files;

        try
        {
            var root = new DirectoryInfo(addonsFolder);
            if (!root.Exists) return files;

            files.AddRange(root.GetFiles("*.vpk"));

            // Subscriptions live one level down, under names Steam chose.
            var workshop = new DirectoryInfo(Path.Combine(addonsFolder, "workshop"));
            if (workshop.Exists) files.AddRange(workshop.GetFiles("*.vpk"));
        }
        catch
        {
            // An unreadable folder is not evidence of anything.
        }

        return files;
    }

    /// <summary>
    /// What "nothing has changed" means. The pack list is only half of it: turning an addon
    /// on or off in the Add-ons screen rewrites addonlist.txt and touches no VPK at all, so
    /// a fingerprint built from the packs alone leaves the app insisting an addon is off
    /// long after it has been switched back on.
    ///
    /// The list is hashed rather than stamped because enabling one addon changes a single
    /// character - same length, and same write time as far as a coarse clock is concerned.
    /// </summary>
    private static string Fingerprint(List<FileInfo> files, string? addonsFolder)
    {
        var packs = string.Join("|",
            files.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
                 .Select(f => $"{f.FullName}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

        return $"{packs}#{AddonListHash(addonsFolder)}";
    }

    private static string AddonListHash(string? addonsFolder)
    {
        var list = AddonListPath(addonsFolder);
        if (list == null) return "none";

        try
        {
            if (!File.Exists(list)) return "absent";

            using var stream = File.OpenRead(list);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }
        catch
        {
            // Locked mid-write by the game. Treat as unchanged and try again next time.
            return "unreadable";
        }
    }

    private static string? AddonListPath(string? addonsFolder)
    {
        var game = Path.GetDirectoryName(addonsFolder);
        return game == null ? null : Path.Combine(game, "addonlist.txt");
    }

    /// <summary>
    /// Entries switched off in the Add-ons screen. The file is Valve KeyValues - one
    /// <c>"name" "0|1"</c> pair per line - and anything that cannot be read is treated as
    /// nothing being disabled, which is the safe direction.
    /// </summary>
    private static HashSet<string> DisabledEntries(string addonsFolder)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = AddonListPath(addonsFolder);

        try
        {
            if (list == null || !File.Exists(list)) return disabled;

            foreach (var line in File.ReadAllLines(list))
            {
                var parts = line.Split('"', StringSplitOptions.RemoveEmptyEntries)
                                .Select(part => part.Trim())
                                .Where(part => part.Length > 0)
                                .ToList();

                if (parts.Count < 2) continue;
                if (!parts[0].EndsWith(".vpk", StringComparison.OrdinalIgnoreCase)) continue;
                if (parts[1] == "0") disabled.Add(parts[0]);
            }
        }
        catch
        {
            // Locked or malformed: say nothing rather than claim the addon is off.
        }

        return disabled;
    }

    /// <summary>
    /// Walks up from the state file to the game folder. The state file may be in
    /// <c>ems\overlay_hud\</c> or, for an exporter older than v1.0.4, loose in <c>ems\</c>,
    /// so the anchor is the <c>left4dead2</c> folder itself rather than a fixed depth.
    /// </summary>
    private static string? AddonsFolder(string? statePath)
    {
        if (string.IsNullOrWhiteSpace(statePath)) return null;

        try
        {
            var folder = Directory.GetParent(statePath);

            while (folder != null)
            {
                if (string.Equals(folder.Name, "left4dead2", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(folder.FullName, "addons");

                folder = folder.Parent;
            }
        }
        catch
        {
            // A malformed configured path is a config problem, not an addon problem.
        }

        return null;
    }
}
