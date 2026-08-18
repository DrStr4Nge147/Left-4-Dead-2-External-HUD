namespace OverlayHud.Services;

/// <summary>
/// The slot tokens the exporter writes in <c>slot</c>: which of a survivor's own slots is
/// in their hands. The weapon HUD highlights that one place.
///
/// A place rather than a weapon id, because the two cannot be told apart by name: a pair of
/// pistols is the same classname as one, and every melee weapon shares another. The
/// exporter compares entity handles and reports the answer; the app only has to know which
/// box to light up.
/// </summary>
internal static class ActiveSlots
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string Throwable = "throwable";
    public const string Kit = "kit";
    public const string Pills = "pills";

    /// <summary>
    /// Whether an exported token names this slot. An exporter older than 2.0.0 sends no
    /// token at all, so nothing is highlighted rather than the wrong thing.
    /// </summary>
    public static bool Is(string? exported, string slot) =>
        !string.IsNullOrEmpty(exported)
        && string.Equals(exported, slot, StringComparison.OrdinalIgnoreCase);
}
