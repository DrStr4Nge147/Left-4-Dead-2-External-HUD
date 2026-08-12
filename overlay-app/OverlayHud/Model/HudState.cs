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
    [JsonPropertyName("survivors")] public List<Survivor> Survivors { get; set; } = new();
}

public sealed class Survivor
{
    [JsonPropertyName("uid")]     public int Uid { get; set; }
    [JsonPropertyName("name")]    public string Name { get; set; } = "";
    [JsonPropertyName("team")]    public int Team { get; set; }
    [JsonPropertyName("char")]    public int Character { get; set; }

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
    [JsonPropertyName("weapon")]  public string Weapon { get; set; } = "";
}
