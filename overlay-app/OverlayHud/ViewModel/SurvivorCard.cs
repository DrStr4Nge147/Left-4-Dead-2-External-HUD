using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverlayHud.Model;

namespace OverlayHud.ViewModel;

/// <summary>
/// One survivor turned into ready-to-bind values. All the arithmetic and colour choice
/// happens here so the XAML stays free of converters.
/// </summary>
public sealed class SurvivorCard
{
    public const double BarWidth = 190;

    public string Name { get; init; } = "";
    public string HealthText { get; init; } = "";

    public double PermanentWidth { get; init; }
    public double TotalWidth { get; init; }

    public Brush HealthBrush { get; init; } = Brushes.LimeGreen;
    public Brush TempBrush { get; init; } = Brushes.White;

    public string StateText { get; init; } = "";
    public Brush StateBrush { get; init; } = Brushes.Transparent;
    public bool HasState => StateText.Length > 0;

    /// <summary>Marks a Finale Soldiers follower. Only set when the roster is mixed.</summary>
    public bool IsFollower { get; init; }

    public double CardOpacity { get; init; } = 1.0;

    public ItemChip Kit { get; init; } = ItemChip.Empty;
    public ItemChip Pill { get; init; } = ItemChip.Empty;
    public ItemChip Throwable { get; init; } = ItemChip.Empty;

    /// <summary>L4D2 HUD slot order: throwable, kit/ammo pack, pills/adrenaline.</summary>
    public IReadOnlyList<ItemChip> ItemSlots => new[] { Throwable, Kit, Pill };

    private static readonly Brush Green   = Frozen(0x4C, 0xC0, 0x4C);
    private static readonly Brush Amber   = Frozen(0xE0, 0xA8, 0x30);
    private static readonly Brush Red     = Frozen(0xC8, 0x3C, 0x3C);
    private static readonly Brush Bone    = Frozen(0xD8, 0xD8, 0xD0);
    private static readonly Brush Grey    = Frozen(0x6A, 0x6A, 0x6A);
    private static readonly Brush TempFill = Frozen(0xBF, 0xE4, 0xFF);

    public static SurvivorCard From(Survivor s, bool markFollower = false)
    {
        int max = s.MaxHp > 0 ? s.MaxHp : 100;

        int hp   = Math.Clamp(s.Hp, 0, max);
        int temp = Math.Max(0, s.Temp);

        // Temp health stacks on top of permanent health and the pair is capped at max,
        // which is how the in-game bar reads.
        int total = Math.Clamp(hp + temp, 0, max);

        bool dead  = s.State == "dead";
        bool down  = s.State is "incap" or "ledge" or "dying";

        Brush fill =
            dead                 ? Grey   :
            down                 ? Red    :
            s.BlackAndWhite      ? Bone   :
            hp > max * 0.40      ? Green  :
            hp > max * 0.20      ? Amber  :
                                   Red;

        string stateText =
            dead                ? "DEAD"    :
            s.State == "ledge"  ? "HANGING" :
            s.State == "incap"  ? "DOWN"    :
            s.State == "dying"  ? "DYING"   :
            s.BlackAndWhite     ? "B&W"     :
                                  "";

        Brush stateBrush = dead ? Grey : s.BlackAndWhite && !down ? Bone : Red;

        string healthText = dead
            ? "--"
            : temp > 0 ? $"{hp} + {temp}" : hp.ToString();

        return new SurvivorCard
        {
            Name           = s.Name,
            HealthText     = healthText,
            PermanentWidth = dead ? 0 : BarWidth * hp / max,
            TotalWidth     = dead ? 0 : BarWidth * total / max,
            HealthBrush    = fill,
            TempBrush      = TempFill,
            StateText      = stateText,
            StateBrush     = stateBrush,
            IsFollower     = markFollower,
            CardOpacity    = dead ? 0.45 : 1.0,
            Kit            = ItemChip.For(s.Kit),
            Pill           = ItemChip.For(s.Pill),
            Throwable      = ItemChip.For(s.Throwable)
        };
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public sealed class ItemChip
    {
        public string Label { get; init; } = "";
        public ImageSource? Icon { get; init; }
        public Brush Background { get; init; } = Brushes.Transparent;
        public double Opacity { get; init; } = 0.25;

        public static readonly ItemChip Empty = new();

        public static ItemChip For(string id) => id switch
        {
            "medkit"          => Make("Medkit", "medkit"),
            "defib"           => Make("Defibrillator", "defib"),
            "explosive_ammo"  => Make("Explosive ammo", "explosive_ammo"),
            "incendiary_ammo" => Make("Incendiary ammo", "incendiary_ammo"),
            "pills"           => Make("Pain pills", "pills"),
            "adrenaline"      => Make("Adrenaline", "adrenaline"),
            "molotov"         => Make("Molotov", "molotov"),
            "pipebomb"        => Make("Pipe bomb", "pipebomb"),
            "bile"            => Make("Bile bomb", "bile"),
            _                 => Empty
        };

        private static ItemChip Make(string label, string asset)
        {
            return new ItemChip
            {
                Label = label,
                Icon = LoadIcon(asset),
                Background = Brushes.Black,
                Opacity = 1.0
            };
        }

        private static BitmapImage LoadIcon(string asset)
        {
            string name = $"OverlayHud.Assets.ItemIcons.{asset}.png";
            using Stream stream = typeof(SurvivorCard).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing embedded item icon: {asset}");

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
