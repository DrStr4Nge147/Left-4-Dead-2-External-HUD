using OverlayHud.Model;

namespace OverlayHud.Services;

/// <summary>Independent roster categories, with aliases for saved legacy presets.</summary>
[Flags]
public enum RosterMode
{
    None = 0,
    Survivors = 1,
    ExtraSurvivors = 2,
    MortalSoldiers = 4,
    ManualFollowers = 8,
    Reinforcements = 16,
    Followers = ManualFollowers | Reinforcements,
    SoldiersAndFollowers = MortalSoldiers | Followers,
    Extras = ExtraSurvivors | SoldiersAndFollowers,
    All = Survivors | Extras
}

/// <summary>The badge a card carries, if any.</summary>
public enum CardMarker
{
    None,

    /// <summary>A soldier told to follow by hand.</summary>
    Follower,

    /// <summary>A soldier called in with <c>help!</c>.</summary>
    Reinforcement
}

/// <summary>Filters independent roster groups while always excluding holdouts.</summary>
public static class RosterPolicy
{
    /// <summary>Survivor slots L4D2's own HUD already draws.</summary>
    public const int VanillaSurvivorSlots = 4;

    public const string ClassSurvivor = "survivor";
    public const string ClassSoldier  = "soldier";
    public const string ClassFollower = "follower";
    public const string ClassHoldout  = "holdout";

    /// <summary>
    /// A soldier called in with <c>help!</c>. Finale Soldiers tags it with
    /// <c>cf_soldier_help_temp</c> before the follow toggle runs, so the exporter can tell it
    /// apart from a hand-picked follower even though both end up following. Sent by exporter
    /// v2.1.3 and later; an older one reports these as <see cref="ClassFollower"/>.
    /// </summary>
    public const string ClassReinforcement = "reinforcement";

    // Legacy scoreboard "all" historically meant Extras. Explicit selections can now
    // include the original four; use a prefix so no checkbox combination is ambiguous.
    public static RosterMode ParseScoreboard(string? value) =>
        value?.Trim().StartsWith("selected:", StringComparison.OrdinalIgnoreCase) == true
            ? Parse(value)
            : Parse(value) == RosterMode.All ? RosterMode.Extras : Parse(value);

    private static readonly (string Name, RosterMode Mode)[] Categories =
    {
        ("survivors", RosterMode.Survivors),
        ("extra-survivors", RosterMode.ExtraSurvivors),
        ("mortal-soldiers", RosterMode.MortalSoldiers),
        ("followers", RosterMode.ManualFollowers),
        ("reinforcements", RosterMode.Reinforcements)
    };

    public static RosterMode Parse(string? value)
    {
        value = value?.Trim().ToLowerInvariant();
        if (value?.StartsWith("selected:") == true)
        {
            var selected = value[9..].Split(',', StringSplitOptions.TrimEntries);
            return Categories.Where(category => selected.Contains(category.Name))
                .Aggregate(RosterMode.None, (mode, category) => mode | category.Mode);
        }
        return value switch
        {
            "extras" => RosterMode.Extras,
            "soldiers" => RosterMode.SoldiersAndFollowers,
            "followers" => RosterMode.Followers,
            _ => RosterMode.All
        };
    }

    public static string ToConfigValue(RosterMode mode) => "selected:" + string.Join(",",
        Categories.Where(category => mode.HasFlag(category.Mode)).Select(category => category.Name));

    /// <summary>Panel header for the mode, without the count.</summary>
    public static string Header(RosterMode mode) => mode switch
    {
        RosterMode.Extras              => "EXTRA SURVIVORS",
        RosterMode.SoldiersAndFollowers => "SOLDIERS + FOLLOWERS",
        RosterMode.Followers            => "FOLLOWERS",
        RosterMode.All                  => "ALL SURVIVORS",
        RosterMode.None                 => "NO SURVIVORS SELECTED",
        _                               => "SELECTED SURVIVORS"
    };

    public static List<Survivor> Apply(IEnumerable<Survivor> roster, RosterMode mode)
    {
        var result = new List<Survivor>();
        int plainSurvivorsSeen = 0;

        foreach (var survivor in roster)
        {
            string cls = Classify(survivor);

            if (cls == ClassHoldout) continue;

            var category = cls switch
            {
                ClassSurvivor => ++plainSurvivorsSeen <= VanillaSurvivorSlots
                    ? RosterMode.Survivors : RosterMode.ExtraSurvivors,
                ClassSoldier => RosterMode.MortalSoldiers,
                ClassFollower => RosterMode.ManualFollowers,
                ClassReinforcement => RosterMode.Reinforcements,
                _ => RosterMode.None
            };
            if ((mode & category) == 0) continue;

            result.Add(survivor);
        }

        return result;
    }

    /// <summary>
    /// The badge this entry carries. A reinforcement is badged everywhere, including
    /// followers-only, where it is the one thing separating it from a hand-picked follower.
    /// The plain follower badge is dropped in that mode: with every card following, only the
    /// reinforcements need saying.
    /// </summary>
    public static CardMarker Marker(Survivor survivor, RosterMode mode) => Classify(survivor) switch
    {
        ClassReinforcement => CardMarker.Reinforcement,
        ClassFollower      => (mode & ~RosterMode.Followers) == 0 ? CardMarker.None : CardMarker.Follower,
        _                  => CardMarker.None
    };

    private static string Classify(Survivor survivor) =>
        survivor.Cls?.Trim().ToLowerInvariant() switch
        {
            ClassSoldier  => ClassSoldier,
            ClassReinforcement => ClassReinforcement,
            ClassFollower => ClassFollower,
            ClassHoldout  => ClassHoldout,
            _             => ClassSurvivor
        };
}
