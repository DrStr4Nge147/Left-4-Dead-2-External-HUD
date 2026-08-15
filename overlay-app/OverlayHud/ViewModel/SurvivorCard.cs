using System.IO;
using System.Windows;
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
    public const double MinimalistBarWidth = 260;
    public const int MinimalistSegmentCount = 5;
    public const double MinimalistSegmentGap = 3.5;
    public const double MinimalistSegmentHeight = 4;
    public const double MinimalistRowHeight = 6;
    public const double MinimalistSegmentWidth =
        (MinimalistBarWidth - MinimalistSegmentCount * MinimalistSegmentGap)
        / MinimalistSegmentCount;

    public string Name { get; init; } = "";
    public string HealthText { get; init; } = "";
    public bool ShowHealthNumbers { get; init; } = true;
    public bool IsMonochrome { get; init; }

    public double PermanentWidth { get; init; }
    public double TotalWidth { get; init; }
    public IReadOnlyList<MinimalistHealthSegment> MinimalistSegments { get; init; } =
        Array.Empty<MinimalistHealthSegment>();

    public Brush HealthBrush { get; init; } = Brushes.LimeGreen;
    public Brush FollowerBrush { get; init; } = Brushes.LightSkyBlue;

    /// <summary>
    /// The temp-health segment: the game's scratched-up overlay in the health bar's own
    /// colour, not a colour of its own. See <see cref="Grunge"/>.
    /// </summary>
    public Brush TempBrush { get; init; } = Brushes.White;

    public string StateText { get; init; } = "";
    public Brush StateBrush { get; init; } = Brushes.Transparent;
    public bool HasState => StateText.Length > 0;

    /// <summary>Marks a Finale Soldiers follower. Only set when the roster is mixed.</summary>
    public bool IsFollower { get; init; }

    /// <summary>
    /// On the last strike and still up. Drives the card's pulsing outline - the one state
    /// worth catching out of the corner of an eye, because the next hit is the last one.
    /// A downed survivor does not get it: the card already says DOWN in red.
    /// </summary>
    public bool IsBlackAndWhite { get; init; }

    public double CardOpacity { get; init; } = 1.0;

    public ItemChip Kit { get; init; } = ItemChip.Empty;
    public ItemChip Pill { get; init; } = ItemChip.Empty;
    public ItemChip Throwable { get; init; } = ItemChip.Empty;

    /// <summary>L4D2 HUD slot order: throwable, kit/ammo pack, pills/adrenaline.</summary>
    public IReadOnlyList<ItemChip> ItemSlots => new[] { Throwable, Kit, Pill };

    private static readonly SolidColorBrush Green = Frozen(0x4C, 0xC0, 0x4C);
    private static readonly SolidColorBrush Amber = Frozen(0xE0, 0xA8, 0x30);
    private static readonly SolidColorBrush Red   = Frozen(0xC8, 0x3C, 0x3C);
    private static readonly SolidColorBrush Bone  = Frozen(0xD8, 0xD8, 0xD0);
    private static readonly SolidColorBrush Grey  = Frozen(0x6A, 0x6A, 0x6A);
    private static readonly SolidColorBrush Mono  = Frozen(0xF2, 0xF2, 0xF2);
    private static readonly SolidColorBrush FollowerBlue = Frozen(0x6F, 0xB4, 0xFF);

    public static SurvivorCard From(Survivor s, bool markFollower = false,
                                    bool monochrome = false, bool showHealthNumbers = true)
    {
        int max = s.MaxHp > 0 ? s.MaxHp : 100;

        int hp   = Math.Clamp(s.Hp, 0, max);
        int temp = Math.Max(0, s.Temp);

        // Temp health stacks on top of permanent health and the pair is capped at max,
        // which is how the in-game bar reads.
        int total = Math.Clamp(hp + temp, 0, max);

        bool dead  = s.State == "dead";
        bool down  = s.State is "incap" or "ledge" or "dying";

        // Black and white keeps its own grey rather than a health colour: the bar is saying
        // "one more hit", not "this much health left", and the game separates the two the
        // same way. The temp overlay below follows whichever of these wins, so a survivor on
        // his last strike who drinks pills gets a grey overlay, not a green one.
        SolidColorBrush fill = monochrome
            ? dead                 ? Grey  : Mono
            : dead                 ? Grey  :
              down                 ? Red   :
              s.BlackAndWhite      ? Bone  :
              hp >= max * 0.40     ? Green :
              hp >= max * 0.25     ? Amber :
                                     Red;

        string stateText =
            dead                ? "DEAD"    :
            s.State == "ledge"  ? "HANGING" :
            s.State == "incap"  ? "DOWN"    :
            s.State == "dying"  ? "DYING"   :
            s.BlackAndWhite     ? "B&W"     :
                                  "";

        Brush stateBrush = monochrome
            ? dead ? Grey : Mono
            : dead ? Grey : s.BlackAndWhite && !down ? Bone : Red;

        string healthText = dead
            ? "--"
            : temp > 0 ? $"{hp} + {temp}" : hp.ToString();

        Brush healthBrush = down ? Grunge(fill) : fill;
        Brush tempBrush = Grunge(fill);

        return new SurvivorCard
        {
            Name           = s.Name,
            HealthText     = healthText,
            ShowHealthNumbers = showHealthNumbers,
            IsMonochrome   = monochrome,
            PermanentWidth = dead ? 0 : BarWidth * hp / max,
            TotalWidth     = dead ? 0 : BarWidth * total / max,
            MinimalistSegments = BuildMinimalistSegments(hp, total, max,
                                                          healthBrush, tempBrush),
            // Down draws the whole bar scratched, not flat: incap health in the game's own
            // panels is a hatched red bar, and a solid block was the obvious difference when
            // the two were put side by side. Everyone else keeps a solid bar with the
            // scratched overlay only on the buffer past current health.
            HealthBrush    = healthBrush,
            FollowerBrush  = monochrome ? Mono : FollowerBlue,
            TempBrush      = tempBrush,
            StateText      = stateText,
            StateBrush     = stateBrush,
            IsFollower     = markFollower,
            IsBlackAndWhite = s.BlackAndWhite && !dead && !down,
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

    private static IReadOnlyList<MinimalistHealthSegment> BuildMinimalistSegments(
        int permanent, int total, int max, Brush permanentFill, Brush tempFill)
    {
        double totalSegments = max > 0
            ? MinimalistSegmentCount * Math.Clamp(total / (double)max, 0.0, 1.0)
            : 0.0;
        double permanentSegments = max > 0
            ? MinimalistSegmentCount * Math.Clamp(permanent / (double)max, 0.0, 1.0)
            : 0.0;

        return Enumerable.Range(0, MinimalistSegmentCount)
            .Select(index => new MinimalistHealthSegment
            {
                TotalFillWidth = MinimalistSegmentWidth
                    * Math.Clamp(totalSegments - index, 0.0, 1.0),
                PermanentFillWidth = MinimalistSegmentWidth
                    * Math.Clamp(permanentSegments - index, 0.0, 1.0),
                TotalFill = tempFill,
                PermanentFill = permanentFill
            })
            .ToArray();
    }

    public sealed class MinimalistHealthSegment
    {
        public double TotalFillWidth { get; init; }
        public double PermanentFillWidth { get; init; }
        public Brush TotalFill { get; init; } = Brushes.Transparent;
        public Brush PermanentFill { get; init; } = Brushes.Transparent;
    }

    /// <summary>
    /// One grunge brush per health colour, built once and reused. Cards are rebuilt on every
    /// poll, and a fresh tiled DrawingBrush five times a second is not something to hand the
    /// render thread.
    /// </summary>
    private static readonly Dictionary<Color, Brush> GrungeCache = new();

    /// <summary>
    /// The temp-health overlay: the bar's own colour, knocked back and scratched through, the
    /// way the game draws the buffer portion. It is deliberately not a colour of its own - a
    /// fixed tint here read as plain grey next to the coloured bar, which is what this
    /// replaces, and it hid the fact that a black-and-white survivor's buffer should be grey
    /// because his BAR is grey, not because buffers are grey.
    ///
    /// Drawn under the permanent bar, so only the part past current health is ever visible.
    /// </summary>
    private static Brush Grunge(SolidColorBrush source)
    {
        Color colour = source.Color;

        lock (GrungeCache)
        {
            if (GrungeCache.TryGetValue(colour, out var cached)) return cached;

            // Dimmed base, so the buffer segment reads as the same colour continuing rather
            // than as more full health.
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Shade(colour, 0.74, 0xE6)),
                null,
                new RectangleGeometry(new Rect(0, 0, 12, 16))));

            // The scratches. Slanted, uneven, and darker than the base - regular stripes look
            // like a progress bar, which is the one thing this must not look like.
            var scratch = new SolidColorBrush(Shade(colour, 0.40, 0xC0));
            scratch.Freeze();

            foreach (var (x, width) in new[] { (1.0, 1.6), (5.0, 0.9), (7.5, 2.1) })
            {
                var streak = new PathGeometry();
                var figure = new PathFigure { StartPoint = new Point(x, 16), IsClosed = true };
                figure.Segments.Add(new LineSegment(new Point(x + 3, 0), true));
                figure.Segments.Add(new LineSegment(new Point(x + 3 + width, 0), true));
                figure.Segments.Add(new LineSegment(new Point(x + width, 16), true));
                streak.Figures.Add(figure);

                group.Children.Add(new GeometryDrawing(scratch, null, streak));
            }

            // Both boxes are given in absolute units and match the drawing, so one tile is
            // one 12x16 device-independent block however wide the bar ends up.
            var brush = new DrawingBrush(group)
            {
                TileMode = TileMode.Tile,
                Viewbox = new Rect(0, 0, 12, 16),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 12, 16),
                ViewportUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            brush.Freeze();

            GrungeCache[colour] = brush;
            return brush;
        }
    }

    /// <summary>The same colour at a fraction of its brightness, with an explicit alpha.</summary>
    private static Color Shade(Color colour, double factor, byte alpha) => Color.FromArgb(
        alpha,
        (byte)(colour.R * factor),
        (byte)(colour.G * factor),
        (byte)(colour.B * factor));

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
