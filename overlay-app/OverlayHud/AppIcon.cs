using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OverlayHud;

/// <summary>
/// The application icon: three survivor health bars on the panel's own dark plate, in the
/// panel's own colours. One embedded .ico serves the executable, the tray, and the editor
/// window, so they cannot drift apart.
///
/// Built by tools/Build-AppIcon.ps1. Frames up to 128px are BMP/DIB rather than PNG
/// because <see cref="System.Drawing.Icon"/> cannot decode PNG-compressed frames, which is
/// how the tray loads it.
/// </summary>
public static class AppIcon
{
    private const string ResourceName = "OverlayHud.Assets.OverlayHud.ico";

    private static ImageSource? _window;

    /// <summary>Sized for the notification area, which asks for a small icon.</summary>
    public static System.Drawing.Icon ForTray()
    {
        var size = System.Windows.Forms.SystemInformation.SmallIconSize;

        using Stream stream = Open();
        return new System.Drawing.Icon(stream, size);
    }

    /// <summary>Title bar, alt-tab, and taskbar for the editor window.</summary>
    public static ImageSource ForWindow()
    {
        if (_window != null) return _window;

        using Stream stream = Open();

        // OnLoad, because the stream is closed as soon as this returns.
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None,
                                           BitmapCacheOption.OnLoad);

        // The largest frame: WPF downscales it per surface, and picking the first frame
        // lands on whichever size the encoder happened to write first.
        ImageSource best = decoder.Frames
            .OrderByDescending(frame => frame.PixelWidth)
            .First();

        best.Freeze();
        _window = best;

        return best;
    }

    private static Stream Open() =>
        typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Missing embedded icon: {ResourceName}");
}
