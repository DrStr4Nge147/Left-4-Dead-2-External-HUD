namespace OverlayHud.Services;

/// <summary>
/// Process-wide ring buffer behind the debug console.
///
/// Every interesting thing this app does is a poll - the state file every 100 ms, the game
/// window every 250 ms - so a log that recorded each one would be a wall of identical lines
/// with the one meaningful transition buried in it. <see cref="Note"/> is therefore the
/// normal way to write: it keeps only changes, per key. Nothing here is called from the
/// keyboard hook callback; see KeyWatcher for why that matters.
/// </summary>
internal static class DebugLog
{
    /// <summary>Lines kept. Enough to cover a map load and the session before it.</summary>
    public const int Capacity = 600;

    private static readonly object Gate = new();
    private static readonly Queue<string> Lines = new();
    private static readonly Dictionary<string, string> LastByKey = new(StringComparer.Ordinal);

    /// <summary>Raised on the calling thread. Subscribers marshal for themselves.</summary>
    public static event Action<string>? LineAdded;

    public static void Write(string category, string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss.fff}  {category,-9}  {message}";

        lock (Gate)
        {
            Lines.Enqueue(line);
            while (Lines.Count > Capacity) Lines.Dequeue();
        }

        LineAdded?.Invoke(line);
    }

    /// <summary>
    /// Writes only when this key's message has changed. For everything driven by a timer:
    /// the reader's status, the foreground window, whether the panel is drawing.
    /// </summary>
    public static void Note(string key, string category, string message)
    {
        lock (Gate)
        {
            if (LastByKey.TryGetValue(key, out var previous) && previous == message) return;

            LastByKey[key] = message;
        }

        Write(category, message);
    }

    public static IReadOnlyList<string> Snapshot()
    {
        lock (Gate) return Lines.ToList();
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Lines.Clear();

            // Cleared notes as well, or the first transition after a clear is swallowed as a
            // duplicate of something no longer on screen.
            LastByKey.Clear();
        }
    }
}
