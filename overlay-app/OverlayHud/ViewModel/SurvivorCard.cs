using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OverlayHud.Model;
using OverlayHud.Services;

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
    public Brush BasicHealthNumberBrush { get; init; } = Brushes.White;
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

    /// <summary>The listen-server host's own card - the player looking at the HUD.</summary>
    public bool IsLocal { get; init; }

    /// <summary>
    /// What the consistent HUD's cards draw. Everyone keeps their items except the player
    /// themselves, whose three slots are drawn larger on the weapon HUD beside their own
    /// ammunition; carrying them twice would say the same thing in two places. The Tab
    /// scoreboard uses <see cref="ItemSlots"/> and shows everyone's, the weapon HUD being
    /// hidden while Tab is held.
    /// </summary>
    public IReadOnlyList<ItemChip> ConsistentItemSlots =>
        IsLocal ? Array.Empty<ItemChip>() : ItemSlots;


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
            BasicHealthNumberBrush = monochrome ? Brushes.Black : Brushes.White,
            FollowerBrush  = monochrome ? Mono : FollowerBlue,
            TempBrush      = tempBrush,
            StateText      = stateText,
            StateBrush     = stateBrush,
            IsFollower     = markFollower,
            IsBlackAndWhite = s.BlackAndWhite && !dead && !down,
            CardOpacity    = dead ? 0.45 : 1.0,
            IsLocal        = s.IsLocal,
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

        /// <summary>Gap to the next slot. Set by the weapon HUD, which spaces its own row.</summary>
        public Thickness SlotMargin { get; init; }

        /// <summary>Whether this is what the survivor currently has in their hands.</summary>
        public bool IsActive { get; init; }

        public static readonly ItemChip Empty = new();

        public bool HasItem => Icon != null;

        /// <summary>
        /// The weapon HUD's item row: throwable, kit, pills, in the same order the survivor
        /// cards used. Empty slots are kept rather than dropped - vanilla holds three fixed
        /// places, and a row that reflows as items are used loses the place-reading that
        /// makes it glanceable.
        /// </summary>
        public static IReadOnlyList<ItemChip> SlotsFor(Survivor s)
        {
            var slots = new[]
            {
                For(s.Throwable).HeldWhen(s.ActiveSlot, ActiveSlots.Throwable),
                For(s.Kit).HeldWhen(s.ActiveSlot, ActiveSlots.Kit),
                For(s.Pill).HeldWhen(s.ActiveSlot, ActiveSlots.Pills)
            };

            // The gap belongs to the slot for the same reason the weapon slots' does: an
            // ItemsControl margin would also trail after the last one.
            for (int i = 0; i < slots.Length - 1; i++)
                slots[i] = slots[i].With(margin: new Thickness(0, 0, 6, 0));

            return slots;
        }

        /// <summary>
        /// Marks the slot the exporter says is in the survivor's hands. An empty slot is
        /// never marked: the highlight answers "this is what you are holding", and an empty
        /// box cannot be it.
        /// </summary>
        private ItemChip HeldWhen(string? activeSlot, string thisSlot) =>
            HasItem && ActiveSlots.Is(activeSlot, thisSlot) ? With(active: true) : this;

        private ItemChip With(Thickness? margin = null, bool? active = null) => new()
        {
            Label = Label,
            Icon = Icon,
            Background = Background,
            Opacity = Opacity,
            SlotMargin = margin ?? SlotMargin,
            IsActive = active ?? IsActive
        };

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
            return LoadEmbeddedImage(name)
                ?? throw new InvalidOperationException($"Missing embedded item icon: {asset}");
        }
    }

    /// <summary>
    /// One slot of the weapon HUD: what the survivor is carrying in it, and how much
    /// ammunition is in it.
    ///
    /// Three ways to draw the weapon, in order. An embedded PNG wins if one exists for this
    /// exact id; otherwise a vector silhouette for the weapon's family, which is what
    /// normally draws; and if the app knows neither, the short text label, so a weapon from
    /// another addon still reads as something.
    /// </summary>
    public sealed class WeaponChip
    {
        /// <summary>Exporter id, e.g. "rifle_ak47". Empty for an unarmed slot.</summary>
        public string Id { get; init; } = "";

        /// <summary>Full name, used as the tooltip: "AK-47".</summary>
        public string Label { get; init; } = "";

        /// <summary>Last-resort label when there is neither art nor a silhouette.</summary>
        public string ShortText { get; init; } = "";

        public ImageSource? Icon { get; init; }

        /// <summary>Family silhouette from WeaponIcons.xaml. Null only if that lookup failed.</summary>
        public Geometry? Silhouette { get; init; }

        /// <summary>
        /// How much of the slot this weapon may fill. A pistol drawn as wide as a rifle
        /// loses the size relationship that is most of what the shape communicates, so
        /// nothing is stretched up to the slot width.
        /// </summary>
        public double ArtWidth { get; init; } = 78;

        /// <summary>Rounds in the magazine, as drawn. Empty when unreadable.</summary>
        public string ClipText { get; init; } = "";

        /// <summary>Rounds in reserve, drawn under the magazine. Empty when unreadable.</summary>
        public string ReserveText { get; init; } = "";

        /// <summary>
        /// The mark beside the magazine: a cartridge for normal rounds, a flame or a burst
        /// for an upgrade. Vanilla puts a single round next to the count, so what is loaded
        /// is read from the same place whether or not it has been upgraded.
        /// </summary>
        public Geometry? AmmoGlyph { get; init; }

        public Brush AmmoGlyphBrush { get; init; } = Brushes.Transparent;

        /// <summary>Gap to the next slot. Set by the panel, which knows its orientation.</summary>
        public Thickness SlotMargin { get; init; }

        /// <summary>Whether this is the weapon the survivor currently has in their hands.</summary>
        public bool IsActive { get; init; }

        public static readonly WeaponChip Empty = new();

        public bool HasWeapon => Id.Length > 0;
        public bool HasIcon => Icon != null;
        public bool ShowsSilhouette => HasWeapon && Icon == null && Silhouette != null;
        public bool ShowsText => HasWeapon && Icon == null && Silhouette == null;
        public bool HasAmmo => ClipText.Length > 0;
        public bool HasAmmoGlyph => HasAmmo && AmmoGlyph != null;
        public bool HasReserve => ReserveText.Length > 0;

        /// <summary>The panel's slots for one survivor: primary first, empty slots dropped.</summary>
        public static IReadOnlyList<WeaponChip> SlotsFor(Survivor s, bool horizontal = false)
        {
            var slots = new List<WeaponChip>(2);

            var primary = For(s.Primary, s.PrimaryClip, s.PrimaryReserve, s.PrimaryAmmoKind,
                              s.PrimaryUpgradedLeft)
                .With(active: ActiveSlots.Is(s.ActiveSlot, ActiveSlots.Primary));
            var secondary = For(s.Secondary, s.SecondaryClip, reserve: -1, ammoKind: NoAmmoMark)
                .FillingItsSlot()
                .With(active: ActiveSlots.Is(s.ActiveSlot, ActiveSlots.Secondary));

            if (primary.HasWeapon) slots.Add(primary);
            if (secondary.HasWeapon) slots.Add(secondary);

            // The gap belongs to the slot rather than the panel: an ItemsControl applies its
            // own margin to every child including the last, which leaves a trailing gap the
            // panel's corner inset then has to compensate for.
            for (int i = 0; i < slots.Count - 1; i++)
            {
                slots[i] = slots[i].WithMargin(horizontal
                    ? new Thickness(0, 0, 6, 0)
                    : new Thickness(0, 0, 0, 6));
            }

            return slots;
        }

        /// <summary>
        /// Passed as the ammunition kind for a slot that draws no mark at all. The mark
        /// answers "what kind of rounds are these", which is a question only the primary
        /// has: nothing upgrades a pistol, and a cartridge beside every pistol count is
        /// noise in a slot that already has one number.
        /// </summary>
        public const int NoAmmoMark = -1;

        public static WeaponChip For(string id, int clip, int reserve, int ammoKind = 0,
                                     int upgradedLeft = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) return Empty;

            var (label, shortText) = Names.TryGetValue(id, out var known)
                ? known
                : (Humanize(id), Humanize(id));

            var (familyKey, width) = Family(id);
            var icon = LoadEmbeddedImage($"OverlayHud.Assets.WeaponIcons.{id}.png");

            return new WeaponChip
            {
                Id = id,
                Label = label,
                ShortText = shortText,
                Icon = icon,
                Silhouette = icon == null ? LookupGeometry(familyKey) : null,
                ArtWidth = icon == null ? width : IconWidth(icon),
                // While an upgrade is loaded the count is the upgrade's own pool, not the
                // magazine. The magazine jumps back to full on a reload and says nothing
                // about how much fire is left; the pool keeps counting down across reloads
                // and reaching zero is exactly when the slot goes back to normal.
                ClipText = ClipTextFor(clip, ammoKind, upgradedLeft),

                // No reserve line while an upgrade is loaded. Upgraded rounds are their own
                // supply and the reserve behind them is ordinary ammunition, so printing it
                // under the count reads as "this many more of these", which is wrong. It
                // comes back on the round the upgrade runs out, which is also when the mark
                // beside the count returns to a plain cartridge.
                ReserveText = reserve < 0 || IsUpgraded(ammoKind) ? "" : reserve.ToString(),
                AmmoGlyph = ammoKind == NoAmmoMark ? null
                                                   : LookupGeometry(AmmoGlyphKey(ammoKind)),
                AmmoGlyphBrush = AmmoGlyphColour(ammoKind)
            };
        }

        /// <summary>
        /// The secondary slot is drawn without a width cap, so its weapon fills the box.
        ///
        /// Relative size is worth keeping between the long guns, which is what the primary
        /// slot holds and why it stays capped. The secondary holds a pistol, a Magnum, or a
        /// melee weapon - all small, never shown side by side, and nothing to compare
        /// against - so capping them only produced a small picture in an empty box. The
        /// slot's own height still governs the proportions.
        /// </summary>
        private WeaponChip FillingItsSlot() => With(artWidth: double.PositiveInfinity);

        private static bool IsUpgraded(int ammoKind) => ammoKind == 1 || ammoKind == 2;

        private static string ClipTextFor(int clip, int ammoKind, int upgradedLeft)
        {
            if (IsUpgraded(ammoKind))
                return upgradedLeft > 0 ? upgradedLeft.ToString() : clip < 0 ? "" : clip.ToString();

            return clip < 0 ? "" : clip.ToString();
        }

        /// <summary>
        /// 0 normal, 1 incendiary, 2 explosive - the exporter's own codes. Anything else is
        /// drawn as normal rounds rather than as nothing, since the count is still true.
        /// </summary>
        private static string AmmoGlyphKey(int ammoKind) => ammoKind switch
        {
            1 => "ammo_fire",
            2 => "ammo_boom",
            _ => "ammo"
        };

        private static Brush AmmoGlyphColour(int ammoKind) => ammoKind switch
        {
            1 => IncendiaryBrush,
            2 => ExplosiveBrush,
            _ => NormalAmmoBrush
        };

        private static readonly Brush NormalAmmoBrush = Frozen(0x9F, 0xB0, 0xC0);
        private static readonly Brush IncendiaryBrush = Frozen(0xFF, 0x8A, 0x3C);
        private static readonly Brush ExplosiveBrush = Frozen(0xFF, 0xD1, 0x4A);

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private WeaponChip WithMargin(Thickness margin) => With(margin: margin);

        private WeaponChip With(double? artWidth = null, Thickness? margin = null,
                                bool? active = null) => new()
        {
            Id = Id,
            Label = Label,
            ShortText = ShortText,
            Icon = Icon,
            Silhouette = Silhouette,
            ArtWidth = artWidth ?? ArtWidth,
            ClipText = ClipText,
            ReserveText = ReserveText,
            AmmoGlyph = AmmoGlyph,
            AmmoGlyphBrush = AmmoGlyphBrush,
            SlotMargin = margin ?? SlotMargin,
            IsActive = active ?? IsActive
        };

        /// <summary>
        /// The game's own icons are already in proportion to each other - a Magnum is 63
        /// pixels wide where an M60 is 256 - so they are scaled by a single factor rather
        /// than per weapon, and that proportion survives onto the HUD. The floor stops the
        /// smallest from becoming a smudge; the ceiling is what the slot can hold beside
        /// its ammunition column.
        /// </summary>
        private static double IconWidth(ImageSource icon) =>
            Math.Clamp(icon.Width * 0.45, 34, 86);

        /// <summary>
        /// Which silhouette a weapon with no icon uses, and how wide it may draw. One shape
        /// per family: at HUD size an AK and an SG552 are the same picture, and a melee
        /// weapon is an edge or a lump.
        /// </summary>
        private static (string Key, double Width) Family(string id) => id switch
        {
            "rifle" or "rifle_ak47" or "rifle_desert" or "rifle_sg552" => ("rifle", 78),
            "smg" or "smg_silenced" or "smg_mp5"                       => ("smg", 70),
            "pumpshotgun" or "shotgun_chrome" or "autoshotgun"
                or "shotgun_spas"                                      => ("shotgun", 78),
            "hunting_rifle" or "sniper_military" or "sniper_scout"
                or "sniper_awp"                                        => ("sniper", 82),
            "rifle_m60"                                                => ("machinegun", 80),
            "grenade_launcher"                                         => ("launcher", 74),
            "pistol" or "pistol_dual"                                  => ("pistol", 46),
            "pistol_magnum"                                            => ("magnum", 52),
            "chainsaw"                                                 => ("chainsaw", 72),
            "riotshield"                                               => ("shield", 40),
            "katana" or "machete" or "knife" or "fireaxe"
                or "pitchfork"                                         => ("blade", 70),
            "baseball_bat" or "cricket_bat" or "crowbar" or "golfclub"
                or "tonfa" or "shovel" or "frying_pan"
                or "electric_guitar" or "melee"                        => ("blunt", 66),
            _                                                          => ("unknown", 60)
        };

        /// <summary>
        /// Resolves a silhouette from the merged WeaponIcons dictionary once per weapon
        /// family. Cards are rebuilt five times a second, so this must not be a dictionary
        /// walk per slot per poll - and Application.Current is null in some test hosts,
        /// which is a missing silhouette, not a crash.
        /// </summary>
        private static Geometry? LookupGeometry(string familyKey)
        {
            lock (GeometryCache)
            {
                if (GeometryCache.TryGetValue(familyKey, out var cached)) return cached;

                Geometry? geometry = null;
                try
                {
                    geometry = Application.Current?
                        .TryFindResource($"WeaponIcon.{familyKey}") as Geometry;
                }
                catch (Exception)
                {
                    // A dictionary that failed to merge leaves the text label as the
                    // fallback, which is still readable.
                }

                geometry?.Freeze();
                GeometryCache[familyKey] = geometry;
                return geometry;
            }
        }

        private static readonly Dictionary<string, Geometry?> GeometryCache = new();

        /// <summary>An id no table knows - another addon's weapon, or a new melee.</summary>
        private static string Humanize(string id)
        {
            string spaced = id.Replace('_', ' ').Trim();
            return spaced.Length == 0 ? "" : char.ToUpperInvariant(spaced[0]) + spaced[1..];
        }

        /// <summary>
        /// Full name for the tooltip, and the short form drawn when no icon exists yet. The
        /// short form has to stay legible on a 0.65x consistent HUD, so it is an
        /// abbreviation rather than the full in-game name.
        /// </summary>
        private static readonly Dictionary<string, (string Label, string Short)> Names = new()
        {
            ["smg"]              = ("Uzi", "UZI"),
            ["smg_silenced"]     = ("Silenced SMG", "SMG-S"),
            ["smg_mp5"]          = ("MP5", "MP5"),
            ["pumpshotgun"]      = ("Pump shotgun", "PUMP"),
            ["shotgun_chrome"]   = ("Chrome shotgun", "CHROME"),
            ["autoshotgun"]      = ("Auto shotgun", "AUTO"),
            ["shotgun_spas"]     = ("Combat shotgun", "SPAS"),
            ["rifle"]            = ("M16 assault rifle", "M16"),
            ["rifle_ak47"]       = ("AK-47", "AK47"),
            ["rifle_desert"]     = ("Desert rifle", "SCAR"),
            ["rifle_sg552"]      = ("SG552", "SG552"),
            ["rifle_m60"]        = ("M60", "M60"),
            ["hunting_rifle"]    = ("Hunting rifle", "HUNT"),
            ["sniper_military"]  = ("Military sniper", "MIL"),
            ["sniper_scout"]     = ("Scout", "SCOUT"),
            ["sniper_awp"]       = ("AWP", "AWP"),
            ["grenade_launcher"] = ("Grenade launcher", "GL"),
            ["pistol"]           = ("Pistol", "P220"),
            ["pistol_dual"]      = ("Dual pistols", "P220x2"),
            ["pistol_magnum"]    = ("Magnum", "MAG"),
            ["chainsaw"]         = ("Chainsaw", "SAW"),
            ["melee"]            = ("Melee weapon", "MELEE"),
            ["fireaxe"]          = ("Fire axe", "AXE"),
            ["frying_pan"]       = ("Frying pan", "PAN"),
            ["machete"]          = ("Machete", "MACH"),
            ["baseball_bat"]     = ("Baseball bat", "BAT"),
            ["cricket_bat"]      = ("Cricket bat", "CRKT"),
            ["crowbar"]          = ("Crowbar", "CROW"),
            ["electric_guitar"]  = ("Electric guitar", "GTR"),
            ["golfclub"]         = ("Golf club", "GOLF"),
            ["katana"]           = ("Katana", "KTNA"),
            ["knife"]            = ("Combat knife", "KNIFE"),
            ["tonfa"]            = ("Nightstick", "TONFA"),
            ["pitchfork"]        = ("Pitchfork", "FORK"),
            ["shovel"]           = ("Shovel", "SHVL"),
            ["riotshield"]       = ("Riot shield", "SHLD")
        };
    }

    /// <summary>
    /// Loads an embedded PNG, or null when the app carries no such resource. Weapon art is
    /// optional and arrives one file at a time; item art is not, and its caller turns the
    /// null into a hard failure.
    /// </summary>
    private static BitmapImage? LoadEmbeddedImage(string resourceName)
    {
        // Cached, including the misses. The weapon slots are rebuilt on every ammunition
        // tick - twenty times a second - and decoding a PNG per slot per tick is not work
        // to hand the render thread for a picture that never changes.
        lock (ImageCache)
        {
            if (ImageCache.TryGetValue(resourceName, out var cached)) return cached;

            BitmapImage? image = null;
            using (Stream? stream = typeof(SurvivorCard).Assembly
                       .GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = stream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                }
            }

            ImageCache[resourceName] = image;
            return image;
        }
    }

    private static readonly Dictionary<string, BitmapImage?> ImageCache = new();
}
