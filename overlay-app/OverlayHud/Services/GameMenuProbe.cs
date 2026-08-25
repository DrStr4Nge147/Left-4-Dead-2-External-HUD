using OverlayHud.Interop;

namespace OverlayHud.Services;

/// <summary>
/// Answers "is L4D2 showing one of its own menus" from outside the game process.
///
/// The pause menu and the developer console are VGUI panels drawn inside the game window.
/// They are not OS windows, so there is no handle, class, or title to find, and they are
/// invisible to the exporter as well: a listen server does not pause when the menu opens, so
/// the export loop keeps ticking and <see cref="StateReader"/> never goes stale. The first
/// attempt at this hid on stale exports and did nothing for exactly that reason.
///
/// The cursor is the observable difference. L4D2 hides it while the player is looking around
/// and shows the arrow for any menu it draws, and cursor visibility is global state that
/// <c>GetCursorInfo</c> reports to any process.
///
/// The check is deliberately paired with "L4D2 is the foreground window" at the call site: a
/// visible cursor means nothing when the player has alt-tabbed away, and that case is already
/// handled by the foreground gate.
/// </summary>
internal static class GameMenuProbe
{
    /// <summary>
    /// True while a cursor is being drawn anywhere on the desktop. False when the read fails,
    /// which keeps a failing API on the "draw the overlay" side rather than hiding it forever.
    /// </summary>
    public static bool CursorVisible()
    {
        var info = new Native.CURSORINFO();
        info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.CURSORINFO>();

        if (!Native.GetCursorInfo(ref info)) return false;

        // hCursor is null when the cursor is hidden, and the flag can lag behind it on some
        // drivers, so both have to agree before this claims a menu is up.
        return (info.flags & Native.CURSOR_SHOWING) != 0 && info.hCursor != IntPtr.Zero;
    }
}
