using OverlayHud.Model;

namespace OverlayHud.Services;

/// <summary>Which part of the roster the panel draws.</summary>
public enum RosterMode
{
    /// <summary>Every mortal survivor, including the four slots in the vanilla HUD.</summary>
    All,

    /// <summary>The previous All behavior: extra plain survivors plus soldiers and followers.</summary>
    Extras,

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
/// 2. Plain survivors are either all included, or keep the established positional rule -
///    L4D2 already draws four survivor slots, so only the fifth onward is a card in
///    <see cref="RosterMode.Extras"/>. Soldiers and followers are never subject to that
///    skip; they are not what those four slots contain.
///
/// An exporter older than v0.6.5 sends no <c>cls</c>, so every entry classifies as a
/// plain survivor. <see cref="RosterMode.All"/> then includes the complete exported roster,
/// while <see cref="RosterMode.Extras"/> reproduces the previous behavior exactly. The two
/// soldier modes need the newer exporter to have anything to show.
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
        "extras"    => RosterMode.Extras,
        "soldiers"  => RosterMode.SoldiersAndFollowers,
        "followers" => RosterMode.Followers,
        _           => RosterMode.All
    };

    public static string ToConfigValue(RosterMode mode) => mode switch
    {
        RosterMode.Extras              => "extras",
        RosterMode.SoldiersAndFollowers => "soldiers",
        RosterMode.Followers            => "followers",
        _                               => "all"
    };

    /// <summary>Panel header for the mode, without the count.</summary>
    public static string Header(RosterMode mode) => mode switch
    {
        RosterMode.Extras              => "EXTRA SURVIVORS",
        RosterMode.SoldiersAndFollowers => "SOLDIERS + FOLLOWERS",
        RosterMode.Followers            => "FOLLOWERS",
        _                               => "ALL SURVIVORS"
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
                plainSurvivorsSeen++;

                if (mode == RosterMode.SoldiersAndFollowers || mode == RosterMode.Followers)
                    continue;

                // Counted even when Extras discards it, so the fifth survivor is still the
                // fifth one after a mode change rather than the first one kept.
                if (mode == RosterMode.Extras && plainSurvivorsSeen <= VanillaSurvivorSlots)
                    continue;
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
