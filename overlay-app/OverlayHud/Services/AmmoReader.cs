using System.IO;
using System.Windows.Threading;

namespace OverlayHud.Services;

/// <summary>
/// Polls the exporter's ammunition channel — the host player's magazine and reserve,
/// rewritten at 20 Hz beside the roster's own 5 Hz file.
///
/// It exists because the roster rate cannot count rounds: an Uzi empties a magazine faster
/// than five updates a second can follow, so the counter jumps in twos and threes. This
/// carries only the numbers that need to move that fast, so the roster export stays cheap.
///
/// The channel is optional in every direction. An exporter older than 1.3.0 never writes
/// the file, a torn read is discarded, and anything that has not advanced recently is
/// treated as absent — in all three cases the weapon HUD falls back to the numbers in
/// state.json, which are correct, just coarser.
/// </summary>
public sealed class AmmoReader : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly double _staleAfterSeconds;

    private long _lastSeq = -1;
    private DateTime _lastChangeUtc = DateTime.MinValue;

    /// <summary>
    /// True once the file has been seen to ADVANCE. Reading it is not proof of anything:
    /// last session's ammo.txt is still sitting there at the main menu, and without this it
    /// reads as live for a whole staleness window every time the app starts.
    /// </summary>
    private bool _hasAdvanced;

    public string? Path { get; private set; }

    public int PrimaryClip { get; private set; } = -1;
    public int PrimaryReserve { get; private set; } = -1;
    public int SecondaryClip { get; private set; } = -1;

    /// <summary>
    /// Which rounds are loaded in the primary: 0 normal, 1 incendiary, 2 explosive. Carried
    /// here rather than on the roster because an upgrade runs out by being fired, so the
    /// mark has to stop on the same round the counter does.
    /// </summary>
    public int PrimaryAmmoKind { get; private set; }

    /// <summary>Upgraded rounds left to fire; 0 when no upgrade is loaded.</summary>
    public int PrimaryUpgradedLeft { get; private set; }

    /// <summary>
    /// True while the file is present and advancing. False means "use state.json", which is
    /// the normal state at a menu, on an old addon, and for one tick after a map change.
    /// </summary>
    public bool IsFresh { get; private set; }

    public event Action? Updated;

    public AmmoReader(string? stateFilePath, TimeSpan interval, double staleAfterSeconds)
    {
        _staleAfterSeconds = staleAfterSeconds;
        Path = Locate(stateFilePath);

        // Same priority as StateReader: a layered window posts Render-priority work
        // continuously, and a Background timer beneath it never fires at all.
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start() => _timer.Start();

    /// <summary>
    /// The channel lives beside the state file. Builds up to v1.0.3 kept their state loose
    /// in ems/ under a different name and never wrote this file at all, so deriving the path
    /// from the current layout is enough: on those installs it simply will not exist.
    /// </summary>
    public static string? Locate(string? stateFilePath)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath)) return null;

        string? folder = System.IO.Path.GetDirectoryName(stateFilePath);
        return folder == null ? null : System.IO.Path.Combine(folder, "ammo.txt");
    }

    /// <summary>Re-derives the path once the state file has been located.</summary>
    public void Rebind(string? stateFilePath)
    {
        if (Path != null) return;
        Path = Locate(stateFilePath);
    }

    private void Poll()
    {
        try
        {
            PollCore();
        }
        finally
        {
            Updated?.Invoke();
        }
    }

    private void PollCore()
    {
        if (Path == null || !File.Exists(Path))
        {
            IsFresh = false;
            return;
        }

        string? text = TryRead(Path);
        if (text == null) return;   // locked mid-write; the next tick sees a whole one

        var parts = text.Trim('\0', ' ', '\r', '\n', '\t')
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // A torn read can produce a valid-looking prefix, so nothing is accepted until all
        // four fields parse. Half a line is not a smaller update, it is a wrong one.
        if (parts.Length < 4
            || !long.TryParse(parts[0], out long seq)
            || !int.TryParse(parts[1], out int clip)
            || !int.TryParse(parts[2], out int reserve)
            || !int.TryParse(parts[3], out int secondary))
        {
            return;
        }

        // The ammunition kind was added after the first four fields and is optional for
        // that reason: an exporter that does not write it leaves the mark at normal rather
        // than costing the whole line.
        int kind = parts.Length > 4 && int.TryParse(parts[4], out int parsed) ? parsed : 0;
        int upgradedLeft = parts.Length > 5 && int.TryParse(parts[5], out int left) ? left : 0;

        if (seq != _lastSeq)
        {
            bool firstSighting = _lastSeq < 0;
            _lastSeq = seq;
            _lastChangeUtc = DateTime.UtcNow;

            // A leftover file from the previous session is seen exactly once before it can
            // be told apart from a live one, so the first sighting is recorded and ignored.
            if (firstSighting) return;

            _hasAdvanced = true;
        }

        if (!_hasAdvanced)
        {
            IsFresh = false;
            return;
        }

        PrimaryClip = clip;
        PrimaryReserve = reserve;
        SecondaryClip = secondary;
        PrimaryAmmoKind = kind;
        PrimaryUpgradedLeft = upgradedLeft;
        IsFresh = (DateTime.UtcNow - _lastChangeUtc).TotalSeconds <= _staleAfterSeconds;
    }

    private static string? TryRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose() => _timer.Stop();
}
