using OverlayHud.Model;

namespace OverlayHud.Services;

/// <summary>Which part of the roster the panel draws.</summary>
public enum RosterMode
{
    /// <summary>Everything the vanilla HUD does not already draw.</summary>
    All,

    /// <summary>Finale Soldiers mortal soldiers and followers only.</summary>
    SoldiersAndFollowers,

    /// <summary>Followers only.</summary>
    Followers
}

/// <summary>
/// Turns an exported roster into the cards the panel shows.
///
/// Two rules are applied in order, and both exist because the panel supplements the
/// vanilla HUD rather than replacing it:
///
/// 1. Immortal team-4 holdout soldiers are never drawn, in any mode. They are scenery
///    that cannot be hurt, so a health card for one is noise.
/// 2. Plain survivors keep the established positional rule - L4D2 already draws four
///    survivor slots, so only the fifth onward is a card. Soldiers and followers are
///    never subject to that skip; they are not what those four slots contain.
///
/// An exporter older than v0.6.5 sends no <c>cls</c>, so every entry classifies as a
/// plain survivor and <see cref="RosterMode.All"/> reproduces the previous behavior
/// exactly. The two soldier modes need the newer exporter to have anything to show.
/// </summary>
public static class RosterPolicy
{
    /// <summary>Survivor slots L4D2's own HUD already draws.</summary>
    public const int VanillaSurvivorSlots = 4;

    public const string ClassSurvivor = "survivor";
    public const string ClassSoldier  = "soldier";
    public const string ClassFollower = "follower";
    public const string ClassHoldout  = "holdout";

    public static RosterMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "soldiers"  => RosterMode.SoldiersAndFollowers,
        "followers" => RosterMode.Followers,
        _           => RosterMode.All
    };

    public static string ToConfigValue(RosterMode mode) => mode switch
    {
        RosterMode.SoldiersAndFollowers => "soldiers",
        RosterMode.Followers            => "followers",
        _                               => "all"
    };

    /// <summary>Panel header for the mode, without the count.</summary>
    public static string Header(RosterMode mode) => mode switch
    {
        RosterMode.SoldiersAndFollowers => "SOLDIERS + FOLLOWERS",
        RosterMode.Followers            => "FOLLOWERS",
        _                               => "EXTRA SURVIVORS"
    };

    public static List<Survivor> Apply(IEnumerable<Survivor> roster, RosterMode mode)
    {
        var result = new List<Survivor>();
        int plainSurvivorsSeen = 0;

        foreach (var survivor in roster)
        {
            string cls = Classify(survivor);

            if (cls == ClassHoldout) continue;

            if (cls == ClassSurvivor)
            {
                // Counted even when the mode discards it, so the fifth survivor is still
                // the fifth one after a mode change rather than the first one kept.
                plainSurvivorsSeen++;

                if (mode != RosterMode.All) continue;
                if (plainSurvivorsSeen <= VanillaSurvivorSlots) continue;
            }
            else if (cls == ClassSoldier && mode == RosterMode.Followers)
            {
                continue;
            }

            result.Add(survivor);
        }

        return result;
    }

    /// <summary>
    /// True when this entry should carry the follower marker. Followers-only mode marks
    /// nothing: every card would carry it, which says nothing.
    /// </summary>
    public static bool MarksFollower(Survivor survivor, RosterMode mode) =>
        mode != RosterMode.Followers && Classify(survivor) == ClassFollower;

    private static string Classify(Survivor survivor) =>
        survivor.Cls?.Trim().ToLowerInvariant() switch
        {
            ClassSoldier  => ClassSoldier,
            ClassFollower => ClassFollower,
            ClassHoldout  => ClassHoldout,
            _             => ClassSurvivor
        };
}
