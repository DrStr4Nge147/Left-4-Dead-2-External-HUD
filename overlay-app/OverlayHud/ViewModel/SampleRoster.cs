using OverlayHud.Model;

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
    /// <paramref name="markFollowers"/> tags a few of the samples as followers so the
    /// marker is visible while laying out, exactly as a mixed live roster would show it.
    /// The theme and number options mirror the live consistent-HUD renderer.
    /// </summary>
    public static List<SurvivorCard> Cards(int count, bool markFollowers = false,
                                           bool monochrome = false,
                                           bool showHealthNumbers = true)
    {
        var result = new List<SurvivorCard>(Math.Max(0, count));

        for (int i = 0; i < count; i++)
        {
            var survivor = new Survivor
            {
                Name = Names[i % Names.Length],
                Hp = i % 5 == 3 ? 28 : 100 - (i % 4) * 12,
                MaxHp = 100,
                Temp = i % 5 == 1 ? 18 : 0,
                State = i % 9 == 7 ? "incap" : "alive",
                BlackAndWhite = i % 9 == 5,
                Kit = new[] { "medkit", "defib", "explosive_ammo", "incendiary_ammo", "", "" }[i % 6],
                Pill = new[] { "", "pills", "", "adrenaline", "", "" }[i % 6],
                Throwable = new[] { "molotov", "pipebomb", "bile", "", "", "" }[i % 6]
            };

            result.Add(SurvivorCard.From(survivor,
                                         markFollowers && i % 4 == 1,
                                         monochrome,
                                         showHealthNumbers));
        }

        return result;
    }
}
