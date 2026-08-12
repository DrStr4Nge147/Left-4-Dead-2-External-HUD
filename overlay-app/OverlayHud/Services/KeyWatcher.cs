using OverlayHud.Interop;

namespace OverlayHud.Services;

/// <summary>
/// Watches the panel key and editor chord globally while L4D2 has focus.
///
/// The hold key is always forwarded, so the game's scoreboard behaves exactly as before.
/// The shortcut key is swallowed only for a chord that begins while the supplied gate is
/// enabled. Nothing is injected into the game process.
/// </summary>
public sealed class KeyWatcher : IDisposable
{
    private readonly KeyboardChordState _state;
    private readonly Func<bool> _shortcutEnabled;

    // The delegate must be held in a field: if it is collected the hook fires into freed
    // memory and the process dies.
    private readonly Native.LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    public event Action<bool>? HeldChanged;
    public event Action? ShortcutPressed;

    public bool IsHeld => _state.IsHeld;

    public KeyWatcher(int holdKey, int shortcutKey, Func<bool> shortcutEnabled)
    {
        _state = new KeyboardChordState(holdKey, shortcutKey);
        _shortcutEnabled = shortcutEnabled;
        _proc = HookProc;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;

        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc,
                                        Native.GetModuleHandle(null), 0);
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = System.Runtime.InteropServices.Marshal
                             .PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);

            int msg = wParam.ToInt32();
            bool isDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
            bool isUp = msg == Native.WM_KEYUP || msg == Native.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                bool enabled;
                try { enabled = _shortcutEnabled(); }
                catch { enabled = false; }

                var decision = _state.Process((int)data.vkCode, isDown, isUp, enabled);
                if (decision.HeldChanged is bool held) HeldChanged?.Invoke(held);
                if (decision.TriggerShortcut) ShortcutPressed?.Invoke();

                if (decision.Consume) return (IntPtr)1;
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;

        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
