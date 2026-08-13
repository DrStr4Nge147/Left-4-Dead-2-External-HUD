namespace OverlayHud.Services;

internal readonly record struct KeyboardChordDecision(
    bool Consume,
    bool TriggerShortcut,
    bool? HeldChanged);

/// <summary>
/// Pure state machine behind the low-level hook. Keeping this separate makes the exact
/// pass-through/suppression contract testable without generating real keyboard input.
/// </summary>
internal sealed class KeyboardChordState
{
    private readonly int _holdKey;
    private readonly int _shortcutKey;
    private bool _consumeShortcutUntilUp;

    public KeyboardChordState(int holdKey, int shortcutKey)
    {
        _holdKey = holdKey;
        _shortcutKey = shortcutKey;
    }

    public bool IsHeld { get; private set; }

    /// <summary>
    /// Forces the tracked hold state to match the physical key. Used when the hook has
    /// missed an event - or has been removed by Windows - so the panel is not left waiting
    /// for a release that already happened. Returns the new value when it changed.
    /// </summary>
    public bool? Sync(bool held)
    {
        if (held == IsHeld) return null;

        IsHeld = held;
        return held;
    }

    public KeyboardChordDecision Process(int vkCode, bool isDown, bool isUp,
                                          bool shortcutEnabled)
    {
        if (vkCode == _holdKey)
        {
            bool held = isDown ? true : isUp ? false : IsHeld;
            bool? changed = held != IsHeld ? held : null;
            IsHeld = held;

            // The scoreboard key is observed only. L4D2 must still receive it.
            return new KeyboardChordDecision(false, false, changed);
        }

        if (_shortcutKey == 0 || _shortcutKey == _holdKey || vkCode != _shortcutKey)
            return default;

        // Once a chord begins, consume every repeat and its matching release even if the
        // player releases Tab first. L4D2 therefore sees no fragment of the Insert press.
        if (_consumeShortcutUntilUp)
        {
            if (isUp) _consumeShortcutUntilUp = false;
            return new KeyboardChordDecision(true, false, null);
        }

        if (isDown && IsHeld && shortcutEnabled)
        {
            _consumeShortcutUntilUp = true;
            return new KeyboardChordDecision(true, true, null);
        }

        return default;
    }
}
