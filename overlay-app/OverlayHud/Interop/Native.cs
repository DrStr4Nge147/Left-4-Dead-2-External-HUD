using System.Runtime.InteropServices;

namespace OverlayHud.Interop;

internal static class Native
{
    // --- window styles -----------------------------------------------------

    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_TRANSPARENT = 0x00000020;  // clicks fall through to the game
    public const int WS_EX_NOACTIVATE  = 0x08000000;  // never steals focus
    public const int WS_EX_TOOLWINDOW  = 0x00000080;  // no alt-tab entry
    public const int WS_EX_LAYERED     = 0x00080000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // --- foreground / geometry --------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width  => Right - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // --- z-order -----------------------------------------------------------

    public static readonly IntPtr HWND_TOPMOST = new(-1);

    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                           int X, int Y, int cx, int cy, uint uFlags);

    // --- low level keyboard hook ------------------------------------------

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN     = 0x0100;
    public const int WM_KEYUP       = 0x0101;
    public const int WM_SYSKEYDOWN  = 0x0104;
    public const int WM_SYSKEYUP    = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
                                                 IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>High bit is set while the key is physically down, hook or no hook.</summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    // --- cursor ------------------------------------------------------------
    //
    // How the app knows L4D2's pause menu or console is open. Both are drawn inside the game
    // window by the game's own UI, so there is no window, class, or title to look for, and
    // nothing the exporter can poll knows about either: a listen server does not pause when
    // the menu opens, so the export loop keeps running and the state file keeps advancing.
    //
    // What does change is the cursor. L4D2 hides it during play and shows the arrow for any
    // of its menus, and cursor visibility is global state any process can read.

    public const int CURSOR_SHOWING = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorInfo(ref CURSORINFO pci);

    // No input synthesis lives here. v1.0.0 held the scoreboard key with SendInput and it
    // did nothing visible in game - input goes to the focused window, which is the editor.
    // v1.0.1 asks the addon to run the console command instead; see ScoreboardHold.
}
