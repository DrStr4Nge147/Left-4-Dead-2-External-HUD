using OverlayHud.Model;
using OverlayHud.Services;

namespace OverlayHud.ViewModel;

/// <summary>
/// Stand-in cards for layout work. Used by the editor's simulated preview and by live
/// preview when no round is exporting, so both show the same thing when there is no real
/// roster to draw.
/// </summary>
public static class SampleRoster
{
    private static readonly string[] Names =
    {
        "Sgt. Miller", "Pvt. Chambers", "Sgt. Hendricks", "Cpl. Ortiz",
        "Sgt. Reyes", "Pvt. Whitfield", "Cpl. Coleman", "Sgt. Walsh",
        "Pvt. Davis", "Cpl. Foster", "Sgt. Blake", "Pvt. Nguyen",
        "Cpl. Price", "Sgt. Ramirez", "Pvt. Mason", "Cpl. Harper"
    };

    /// <summary>
    /// <paramref name="markFollowers"/> tags a few of the samples as followers and a few as
    /// help! reinforcements, so both markers are visible while laying out, exactly as a mixed
    /// live roster would show them.
    /// The theme and number options mirror the live consistent-HUD renderer.
    /// </summary>
    /// <summary>
    /// The stand-in for the weapon HUD while nothing is exporting. A rifle mid-magazine and
    /// a pistol: the widest silhouette and the narrowest, so the panel can be positioned
    /// against its real size rather than a best case.
    /// </summary>
    public static Survivor WeaponSurvivor() => new()
    {
        Name = "You",
        Hp = 100,
        MaxHp = 100,
        IsLocal = true,
        Primary = "rifle_ak47",
        PrimaryClip = 30,
        PrimaryReserve = 172,
        Secondary = "pistol",
        SecondaryClip = 12,
        Throwable = "pipebomb",
        Kit = "medkit",
        Pill = "pills",
        ActiveSlot = ActiveSlots.Primary
    };

    public static List<SurvivorCard> Cards(int count, bool markFollowers = false,
                                           bool monochrome = false,
                                           bool showHealthNumbers = true, RosterMode? mode = null)
    {
        var result = new List<SurvivorCard>(Math.Max(0, count));

        // Generate a representative exported roster, then apply the same category policy
        // as the live renderer. Keep the slider as the maximum number of visible cards.
        var selected = mode.HasValue ? RosterPolicy.Apply(
            Enumerable.Range(0, Math.Max(0, count) * 4 + 4).Select(i => new Survivor
            {
                IsLocal = i == 0,
                Cls = i < 4 ? RosterPolicy.ClassSurvivor : ((i - 4) % 4) switch
                {
                    0 => RosterPolicy.ClassSurvivor,
                    1 => RosterPolicy.ClassSoldier,
                    2 => RosterPolicy.ClassFollower,
                    _ => RosterPolicy.ClassReinforcement
                }
            }), mode.Value).Take(Math.Max(0, count)).ToList() : null;

        for (int i = 0; i < (selected?.Count ?? count); i++)
        {
            var survivor = new Survivor
            {
                Name = Names[i % Names.Length],

                // The first card stands in for the player: the editor's Separate You card
                // is taken from the front of this list, and their own card is the one the
                // consistent HUD draws without items.
                IsLocal = selected?[i].IsLocal ?? i == 0,
                Cls = selected?[i].Cls ?? RosterPolicy.ClassSurvivor,
                Hp = i % 5 == 3 ? 28 : 100 - (i % 4) * 12,
                MaxHp = 100,
                Temp = i % 5 == 1 ? 18 : 0,
                State = i % 9 == 7 ? "incap" : "alive",
                BlackAndWhite = i % 9 == 5,
                Kit = new[] { "medkit", "defib", "explosive_ammo", "incendiary_ammo", "", "" }[i % 6],
                Pill = new[] { "", "pills", "", "adrenaline", "", "" }[i % 6],
                Throwable = new[] { "molotov", "pipebomb", "bile", "", "", "" }[i % 6],

                // Deliberately uneven: a full magazine, a nearly empty one, an unarmed
                // slot, and a melee secondary, so laying the HUD out shows the widest and
                // the narrowest weapon row the live game can produce.
                Primary = new[]
                {
                    "rifle_ak47", "autoshotgun", "sniper_military", "smg_silenced",
                    "rifle_m60", ""
                }[i % 6],
                PrimaryClip = new[] { 40, 7, 3, 50, 150, -1 }[i % 6],
                PrimaryReserve = new[] { 360, 56, 105, 650, 0, -1 }[i % 6],
                Secondary = new[]
                {
                    "pistol", "katana", "pistol_magnum", "", "chainsaw", "fireaxe"
                }[i % 6],
                SecondaryClip = new[] { 15, -1, 8, -1, 30, -1 }[i % 6]
            };

            result.Add(SurvivorCard.From(survivor,
                                         mode.HasValue ? RosterPolicy.Marker(survivor, mode.Value)
                                         : !markFollowers   ? CardMarker.None
                                             : i % 4 == 1 ? CardMarker.Reinforcement
                                             : i % 4 == 3 ? CardMarker.Follower
                                             : CardMarker.None,
                                         monochrome,
                                         showHealthNumbers));
        }

        return result;
    }
}
