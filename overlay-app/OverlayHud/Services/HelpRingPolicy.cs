using OverlayHud.Model;

namespace OverlayHud.Services;

/// <summary>What the host's <c>help!</c> call is doing right now.</summary>
public enum HelpPhase
{
    /// <summary>No exporter field, or a status this build does not know. The ring is not drawn.</summary>
    Unknown,

    /// <summary>The call goes through right now.</summary>
    Ready,

    /// <summary>Called; the squad is on its way but not on the map yet.</summary>
    Calling,

    /// <summary>The squad is here, and this is what is left of their stay.</summary>
    Active,

    /// <summary>The squad is done and the cooldown is running.</summary>
    Cooling
}

/// <summary>One frame of the ring: how full it is, what it says, and which colour it is.</summary>
public readonly record struct HelpRing(HelpPhase Phase, double Fraction, string Caption, bool Available)
{
    /// <summary>Nothing to draw - no exporter field, or a status this build does not know.</summary>
    public static readonly HelpRing Hidden =
        new(HelpPhase.Unknown, 0.0, "", false);

    public bool IsVisible => Phase != HelpPhase.Unknown;
}

/// <summary>
/// Turns the exporter's <c>help</c> object into the reinforcement ring.
///
/// Two things happen here that cannot happen in the exporter.
///
/// The first is the clock. State arrives at 5 Hz and the ring is redrawn far more often
/// than that, so a ring driven by the samples alone steps in fifths of a second. Each
/// sample is stamped on arrival and the remaining time is counted down against the wall
/// clock from there, which makes the sweep continuous and leaves the exporter as the
/// authority on what the number actually is. The countdown never runs past the end of its
/// own window: a game that pauses stops advancing <c>seq</c>, and a ring that kept draining
/// through a pause menu would come back wrong.
///
/// The second is colour, which is a smaller question than the four phases suggest. Green
/// means help is with you or on its way; grey means you are waiting and the call will be
/// refused. So <see cref="HelpPhase.Ready"/>, <see cref="HelpPhase.Calling"/> and
/// <see cref="HelpPhase.Active"/> are all green, and only <see cref="HelpPhase.Cooling"/>
/// is grey.
/// </summary>
public sealed class HelpRingPolicy
{
    public const string StatusReady   = "ready";
    public const string StatusCalling = "calling";
    public const string StatusActive  = "active";
    public const string StatusCooling = "cooling";

    /// <summary>
    /// How long a sample is trusted for. The exporter writes at 5 Hz, so anything much past
    /// that is a paused, closed, or between-maps game; the ring holds its last frame instead
    /// of draining to zero on its own and claiming a readiness nothing has confirmed.
    /// </summary>
    public static readonly TimeSpan SampleLifetime = TimeSpan.FromSeconds(2);

    private readonly Func<DateTime> _now;

    private HelpState? _sample;
    private DateTime _sampledAt;

    public HelpRingPolicy() : this(() => DateTime.UtcNow) { }

    /// <summary>Test seam: lets the layout checks drive the countdown without sleeping.</summary>
    public HelpRingPolicy(Func<DateTime> now)
    {
        _now = now;
        _sampledAt = now();
    }

    /// <summary>Takes the newest exported value, or null when the exporter sends none.</summary>
    public void Observe(HelpState? state)
    {
        _sample = state;
        _sampledAt = _now();
    }

    /// <summary>The ring as it should be drawn at this instant.</summary>
    public HelpRing Current()
    {
        var sample = _sample;
        if (sample == null) return HelpRing.Hidden;

        var phase = ParsePhase(sample.Status);
        if (phase == HelpPhase.Unknown) return HelpRing.Hidden;

        double left = Remaining(sample);
        bool available = phase is HelpPhase.Ready or HelpPhase.Calling or HelpPhase.Active;

        if (phase == HelpPhase.Ready || left <= 0.0)
        {
            // A window that has run out where the game has stopped exporting is not the same
            // as a call the exporter has confirmed is ready, but it is what the player is
            // about to be told either way, and the alternative is a ring stuck at zero.
            return new HelpRing(phase == HelpPhase.Ready ? HelpPhase.Ready : phase,
                                1.0, "", available);
        }

        return new HelpRing(phase, Fraction(left, sample.Total), Caption(left), available);
    }

    /// <summary>
    /// Seconds left, counted from the sample against the wall clock and never past the end
    /// of the window the sample described.
    /// </summary>
    private double Remaining(HelpState sample)
    {
        double left = Math.Max(0.0, sample.Left);
        if (left <= 0.0) return 0.0;

        double elapsed = (_now() - _sampledAt).TotalSeconds;
        if (elapsed < 0.0) elapsed = 0.0;
        if (elapsed > SampleLifetime.TotalSeconds) elapsed = SampleLifetime.TotalSeconds;

        return Math.Max(0.0, left - elapsed);
    }

    /// <summary>
    /// How much of the ring is drawn: the share of the window still to run, so a countdown
    /// empties the ring and a cooldown that is nearly over is nearly empty. A total the
    /// exporter could not give - an older Finale Soldiers build, a setting changed mid-round -
    /// is drawn full rather than divided by zero.
    /// </summary>
    public static double Fraction(double left, double total)
    {
        if (total <= 0.0) return 1.0;
        return Math.Clamp(left / total, 0.0, 1.0);
    }

    /// <summary>
    /// The number inside the ring. Whole seconds, rounded up, so the last second is shown as
    /// "1" for its whole length and the ring reaches zero and the caption disappears together.
    /// </summary>
    public static string Caption(double left) =>
        left <= 0.0 ? "" : Math.Ceiling(left).ToString("0");

    public static HelpPhase ParsePhase(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        StatusReady   => HelpPhase.Ready,
        StatusCalling => HelpPhase.Calling,
        StatusActive  => HelpPhase.Active,
        StatusCooling => HelpPhase.Cooling,
        _             => HelpPhase.Unknown
    };
}
