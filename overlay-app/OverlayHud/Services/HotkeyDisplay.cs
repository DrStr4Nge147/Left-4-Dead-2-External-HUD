using System.Windows.Input;

namespace OverlayHud.Services;

internal static class HotkeyDisplay
{
    public static string Name(int virtualKey)
    {
        if (virtualKey == 0) return "Not set";

        try
        {
            Key key = KeyInterop.KeyFromVirtualKey(virtualKey);
            return key == Key.None ? $"0x{virtualKey:X2}" : key.ToString();
        }
        catch
        {
            return $"0x{virtualKey:X2}";
        }
    }
}
