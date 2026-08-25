using System.Text.Json.Serialization;

namespace OverlayHud.Model;

/// <summary>
/// One snapshot written by the exporter addon. Mirrors docs/STATE_FORMAT.md.
/// </summary>
public sealed class HudState
{
    [JsonPropertyName("v")]         public string Version { get; set; } = "";
    [JsonPropertyName("seq")]       public long Seq { get; set; }
    [JsonPropertyName("time")]      public double Time { get; set; }
    [JsonPropertyName("count")]     public int Count { get; set; }

    /// <summary>
    /// 1 while the game is running a cinematic over the player - the finale outro above
    /// all - and has therefore hidden its own survivor HUD. Absent from exporters that
    /// predate the field, which leaves it 0 and keeps the original always-drawn behavior.
    /// </summary>
    [JsonPropertyName("cine")]      public int Cinematic { get; set; }

    /// <summary>
    /// The engine's hidden-HUD bitfield for the host survivor, or -1 when the exporter
    /// found no route to it. Diagnostic only: the verdict is <see cref="Cinematic"/>.
    /// </summary>
    [JsonPropertyName("hud")]       public int HideHudBits { get; set; } = -1;

    /// <summary>
    /// 1 while the host's view is bound to a camera entity, 0 when it is their own eyes,
    /// -1 when unreadable. Diagnostic only, as with <see cref="HideHudBits"/>.
    /// </summary>
    [JsonPropertyName("view")]      public int ViewCamera { get; set; } = -1;

    /// <summary>
    /// 1 while the server has the host player frozen for a scene it is running, -1 when
    /// unreadable. Diagnostic, and the one read that answers at the chapter end.
    /// </summary>
    [JsonPropertyName("frz")]       public int Frozen { get; set; } = -1;

    /// <summary>
    /// 1 once the finale has been won, -1 when unreadable. Diagnostic, and the one read that
    /// answers for the end credits: the game hands control back at that point, so every
    /// player-side read is honestly reporting ordinary play while the credits roll.
    /// </summary>
    [JsonPropertyName("won")]       public int FinaleWon { get; set; } = -1;
    [JsonPropertyName("survivors")] public List<Survivor> Survivors { get; set; } = new();
}

public sealed class Survivor
{
    [JsonPropertyName("uid")]     public int Uid { get; set; }
    [JsonPropertyName("name")]    public string Name { get; set; } = "";
    [JsonPropertyName("team")]    public int Team { get; set; }
    [JsonPropertyName("char")]    public int Character { get; set; }

    /// <summary>
    /// True for the listen-server host's survivor. Older exporters omit this field and
    /// therefore leave it false, preserving the original all-in-roster behavior.
    /// </summary>
    [JsonPropertyName("local")]   public bool IsLocal { get; set; }

    /// <summary>
    /// survivor / soldier / follower / holdout. Empty from an exporter older than 0.6.5,
    /// which is treated as "survivor" - see RosterPolicy.
    /// </summary>
    [JsonPropertyName("cls")]     public string Cls { get; set; } = "";

    [JsonPropertyName("bot")]     public bool? Bot { get; set; }
    [JsonPropertyName("hp")]      public int Hp { get; set; }
    [JsonPropertyName("maxhp")]   public int MaxHp { get; set; }
    [JsonPropertyName("temp")]    public int Temp { get; set; }
    [JsonPropertyName("state")]   public string State { get; set; } = "alive";
    [JsonPropertyName("revives")] public int Revives { get; set; }
    [JsonPropertyName("bw")]      public bool BlackAndWhite { get; set; }
    [JsonPropertyName("kit")]     public string Kit { get; set; } = "";
    [JsonPropertyName("pill")]    public string Pill { get; set; } = "";
    [JsonPropertyName("throw")]   public string Throwable { get; set; } = "";

    /// <summary>
    /// Slot-0 weapon id, e.g. "rifle_ak47". Empty when the survivor carries none, and
    /// always empty from an exporter older than 2.0.0.
    /// </summary>
    [JsonPropertyName("pri")]     public string Primary { get; set; } = "";

    /// <summary>Rounds in the primary's magazine. -1 when the install exposed no route.</summary>
    [JsonPropertyName("priclip")] public int PrimaryClip { get; set; } = -1;

    /// <summary>Primary rounds in reserve. -1 when unreadable.</summary>
    [JsonPropertyName("priammo")] public int PrimaryReserve { get; set; } = -1;

    /// <summary>
    /// What kind of rounds are loaded in the primary: 0 normal, 1 incendiary, 2 explosive.
    /// Upgraded rounds live only in the magazine, so this returns to 0 by being fired.
    /// </summary>
    [JsonPropertyName("priupg")]  public int PrimaryAmmoKind { get; set; }

    /// <summary>
    /// Upgraded rounds left to fire. The upgrade's own pool, which survives a reload where
    /// the magazine does not, so it is what the HUD counts down while one is loaded. Zero
    /// whenever the kind is 0.
    /// </summary>
    [JsonPropertyName("priupgn")] public int PrimaryUpgradedLeft { get; set; }

    /// <summary>
    /// Slot-1 weapon id: "pistol", "pistol_magnum", "chainsaw", or a melee script name
    /// such as "katana". Empty from an exporter older than 2.0.0.
    /// </summary>
    [JsonPropertyName("sec")]     public string Secondary { get; set; } = "";

    /// <summary>Rounds in the secondary's magazine, or chainsaw fuel. -1 for melee.</summary>
    [JsonPropertyName("secclip")] public int SecondaryClip { get; set; } = -1;

    [JsonPropertyName("weapon")]  public string Weapon { get; set; } = "";

    /// <summary>
    /// Which of the survivor's own slots is in their hands: "primary", "secondary",
    /// "throwable", "kit", "pills", or empty for anything the exporter's tables do not
    /// know. Empty from an exporter older than 2.0.0, which simply highlights nothing.
    /// </summary>
    [JsonPropertyName("slot")]    public string ActiveSlot { get; set; } = "";
}
