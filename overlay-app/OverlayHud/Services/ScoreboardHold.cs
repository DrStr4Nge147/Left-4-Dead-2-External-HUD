using System.IO;

namespace OverlayHud.Services;

/// <summary>
/// Asks the exporter addon to hold the game's own scoreboard open during live preview, by
/// writing one line into the same <c>ems</c> folder the state file comes out of.
///
/// This is the reverse direction of the transport: <c>"&lt;want&gt; &lt;seq&gt;"</c>, where
/// want is 1 or 0 and seq increments on every write. The addon acts on want and uses seq as
/// a heartbeat - a hold whose seq stops advancing is released by the addon on its own, so a
/// scoreboard cannot stay latched open because this app was killed while holding it.
///
/// Why not synthesise a keypress: L4D2 draws the scoreboard while the host's client holds
/// +showscores, and input goes to whatever window has focus. While the editor has focus,
/// that is the editor. v1.0.0 shipped that approach and it did nothing visible in game.
/// The addon is already inside the process that can simply run the command.
/// </summary>
public sealed class ScoreboardHold
{
    public const string CommandFileName = "overlay_hud_cmd.txt";

    private readonly Func<string?> _statePath;
    private readonly Action<string, string>? _write;

    private long _seq;
    private bool _requested;

    public ScoreboardHold(Func<string?> statePath) => _statePath = statePath;

    /// <summary>Test seam: the check records writes instead of touching the game folder.</summary>
    public ScoreboardHold(Func<string?> statePath, Action<string, string> write)
        : this(statePath) => _write = write;

    /// <summary>True while this app is asking for the scoreboard and can actually ask.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>
    /// Safe to call on every poll, and meant to be: each call while held rewrites the file
    /// with a new seq, which is the heartbeat the addon watches.
    /// </summary>
    public void Update(bool wanted)
    {
        string? path = CommandPath();

        if (path == null)
        {
            // No located install, so there is nothing to ask. Say so honestly rather than
            // reporting a hold the game will never see.
            IsHeld = false;
            return;
        }

        // Tracked separately from IsHeld so that a hold whose write failed is still
        // released explicitly later, rather than being left to the addon's timeout.
        if (!wanted && !_requested) return;

        _requested = wanted;

        // A failed write is not a hold. Reporting one would leave the panel hiding its own
        // fallback marker behind a scoreboard that is never going to appear.
        IsHeld = TryWrite(path, wanted ? "1" : "0") && wanted;
    }

    public void Release() => Update(false);

    private string? CommandPath()
    {
        string? state = _statePath();
        if (string.IsNullOrWhiteSpace(state)) return null;

        string? folder = Path.GetDirectoryName(state);

        return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, CommandFileName);
    }

    private bool TryWrite(string path, string want)
    {
        string line = $"{want} {++_seq}";

        try
        {
            if (_write != null)
            {
                _write(path, line);
                return true;
            }

            File.WriteAllText(path, line);
            return true;
        }
        catch
        {
            // A read-only or missing game folder must not take the editor down with it; the
            // panel falls back to marking the scoreboard region instead.
            return false;
        }
    }
}
