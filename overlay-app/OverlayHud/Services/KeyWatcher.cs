using System.Windows.Threading;
using OverlayHud.Interop;

namespace OverlayHud.Services;

/// <summary>
/// Watches the panel key and editor chord globally while L4D2 has focus.
///
/// The hold key is always forwarded, so the game's scoreboard behaves exactly as before.
/// The shortcut key is swallowed only for a chord that begins while the supplied gate is
/// enabled. Nothing is injected into the game process.
///
/// The hook callback itself does the minimum: decide, post, return. Anything slower risks
/// LowLevelHooksTimeout, and a hook that overruns it is removed by Windows without warning -
/// which is exactly how the overlay used to stop responding to the hold key mid-session.
/// <see cref="Pulse"/> is the recovery path for when that happens anyway.
/// </summary>
public sealed class KeyWatcher : IDisposable
{
    private readonly KeyboardChordState _state;
    private readonly HookWatchdog _watchdog = new();
    private readonly Func<bool> _shortcutEnabled;
    private readonly Func<bool> _toggleEnabled;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly int _holdKey;

    // The delegate must be held in a field: if it is collected the hook fires into freed
    // memory and the process dies.
    private readonly Native.LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _disposed;

    public event Action<bool>? HeldChanged;
    public event Action? ShortcutPressed;
    public event Action? TogglePressed;

    public bool IsHeld => _state.IsHeld;

    /// <summary>Times the hook has been reinstalled after Windows removed it.</summary>
    public int Recoveries { get; private set; }

    public KeyWatcher(int holdKey, int shortcutKey, Func<bool> shortcutEnabled,
                      int toggleKey = 0, Func<bool>? toggleEnabled = null)
    {
        _holdKey = holdKey;
        _state = new KeyboardChordState(holdKey, shortcutKey, toggleKey);
        _shortcutEnabled = shortcutEnabled;
        _toggleEnabled = toggleEnabled ?? shortcutEnabled;
        _proc = HookProc;
    }

    public void SetToggleKey(int toggleKey) => _state.SetToggleKey(toggleKey);

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;

        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc,
                                        Native.GetModuleHandle(null), 0);

        DebugLog.Write("input", _hook != IntPtr.Zero
            ? $"keyboard hook installed for key 0x{_holdKey:X2}"
            : "keyboard hook could not be installed - the hold key will be polled instead");
    }

    /// <summary>
    /// Health check, driven by the geometry timer. Compares the tracked hold state against
    /// the physical key and repairs both the state and, when the disagreement persists, the
    /// hook. Polling also means the panel keeps working even if the reinstall fails; only
    /// the Tab+Insert suppression needs the hook.
    /// </summary>
    public void Pulse()
    {
        if (_disposed) return;

        bool physical = (Native.GetAsyncKeyState(_holdKey) & 0x8000) != 0;
        var decision = _watchdog.Observe(physical, _state.IsHeld);

        if (decision.Reinstall)
        {
            DebugLog.Write("input",
                "hold key state disagreed with the keyboard twice - Windows has dropped the "
                + "hook, reinstalling");
            Reinstall();
            _watchdog.Reset();
        }

        if (!decision.Resync) return;

        if (_state.Sync(physical) is bool held)
        {
            DebugLog.Write("input", $"hold key corrected to {(held ? "down" : "up")} from the "
                                    + "keyboard - the hook missed an event");
            HeldChanged?.Invoke(held);
        }
    }

    private void Reinstall()
    {
        if (_hook != IntPtr.Zero)
        {
            // A hook Windows already removed is gone; the unhook is best effort either way.
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        Start();

        if (_hook == IntPtr.Zero) return;

        Recoveries++;
        DebugLog.Write("input", $"keyboard hook recovered (reinstall #{Recoveries})");
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
                bool toggleEnabled;
                try { enabled = _shortcutEnabled(); }
                catch { enabled = false; }
                try { toggleEnabled = _toggleEnabled(); }
                catch { toggleEnabled = false; }

                var decision = _state.Process((int)data.vkCode, isDown, isUp,
                                               enabled, toggleEnabled);

                // Consume has to be decided here, but the subscribers must not be: a render
                // pass on this thread is charged against the hook timeout, and blowing that
                // budget is what gets the hook silently removed.
                if (decision.HeldChanged is bool held)
                    _dispatcher.BeginInvoke(() => HeldChanged?.Invoke(held));

                if (decision.TriggerShortcut)
                    _dispatcher.BeginInvoke(() => ShortcutPressed?.Invoke());

                if (decision.TriggerToggle)
                    _dispatcher.BeginInvoke(() => TogglePressed?.Invoke());

                if (decision.Consume) return (IntPtr)1;
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        _disposed = true;
        if (_hook == IntPtr.Zero) return;

        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
