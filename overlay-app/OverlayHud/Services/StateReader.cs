using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using OverlayHud.Model;

namespace OverlayHud.Services;

/// <summary>
/// Polls the exporter's JSON file and hands out the most recent state that parsed.
///
/// The addon rewrites the file in place with no atomic swap, so a read can catch a
/// half-written file. A failed parse is normal and expected: it keeps the previous state
/// and tries again on the next tick. It is never an error condition.
/// </summary>
public sealed class StateReader : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private long _lastSeq = -1;
    private long _parseFailures;
    private DateTime _lastSeqChangeUtc = DateTime.UtcNow;

    public string? Path { get; private set; }
    public HudState? Current { get; private set; }

    /// <summary>True when the file has not advanced its seq recently: game paused or gone.</summary>
    public bool IsStale { get; private set; } = true;

    /// <summary>Empty when all is well; otherwise describes why nothing is showing.</summary>
    public string Status { get; private set; } = "starting up";

    /// <summary>Resolved path or last failure. Shown when the panel has nothing to draw.</summary>
    public string Diagnostic { get; private set; } = "";

    /// <summary>Polls completed since start. Zero means the timer never fired.</summary>
    public long Polls { get; private set; }

    public event Action? Updated;

    public StateReader(string? configuredPath, TimeSpan interval, double staleAfterSeconds)
    {
        StaleAfterSeconds = staleAfterSeconds;
        Path = string.IsNullOrWhiteSpace(configuredPath) ? StateLocator.Locate() : configuredPath;

        // Normal, not Background. A layered window (AllowsTransparency) renders in
        // software and posts Render-priority work continuously, and Background sits below
        // Render in the dispatcher queue - a Background timer here never fires at all.
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = interval };
        _timer.Tick += (_, _) => Poll();
    }

    public double StaleAfterSeconds { get; }

    public void Start() => _timer.Start();

    private void Poll()
    {
        // Every exit path has to reach the UI. An early return that skips Updated makes a
        // permanent read failure look exactly like a dead timer, with the panel frozen on
        // whatever it last drew.
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
        Polls++;

        if (Path == null)
        {
            // The game may have been installed, or Steam started, after we launched.
            Path = StateLocator.Locate();

            if (Path == null)
            {
                Status = "Left 4 Dead 2 not found - set statePath in config.json";
                Diagnostic = "searched the Steam library folders";
                return;
            }
        }

        if (!File.Exists(Path))
        {
            Status = "waiting for the addon - no state file yet";
            Diagnostic = Path;
            MarkStale();
            return;
        }

        var text = TryRead(Path);
        if (text == null) return;   // locked mid-write, try again next tick

        HudState? state;
        try
        {
            state = JsonSerializer.Deserialize<HudState>(Clean(text), _json);
        }
        catch (JsonException ex)
        {
            // Usually a torn read caught mid-rewrite, which is normal and self-correcting.
            // Recorded rather than swallowed: a *permanent* parse failure looks identical
            // from the outside, and that difference cost a debugging session.
            _parseFailures++;
            Status = "state file did not parse";
            Diagnostic = $"{_parseFailures}x: {ex.Message}";
            return;
        }
        catch (Exception ex)
        {
            // Anything else would escape into the timer and kill polling for the rest of
            // the session.
            Status = "state file could not be read";
            Diagnostic = ex.Message;
            return;
        }

        if (state == null) return;

        Diagnostic = "";

        if (state.Seq != _lastSeq)
        {
            _lastSeq = state.Seq;
            _lastSeqChangeUtc = DateTime.UtcNow;
        }

        Current = state;
        IsStale = (DateTime.UtcNow - _lastSeqChangeUtc).TotalSeconds > StaleAfterSeconds;
        Status = IsStale ? "game paused or not running" : "";
    }

    /// <summary>
    /// VScript's StringToFile appends a NUL terminator. System.Text.Json does not accept
    /// NUL as trailing whitespace, so every single parse fails without this.
    /// </summary>
    private static string Clean(string text) => text.Trim('\0', ' ', '\r', '\n', '\t');

    private void MarkStale()
    {
        IsStale = true;
        Current = null;
    }

    private static string? TryRead(string path)
    {
        try
        {
            // The game holds the file open while writing; share everything.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
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
