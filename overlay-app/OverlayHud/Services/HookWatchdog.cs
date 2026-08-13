namespace OverlayHud.Services;

internal readonly record struct HookWatchdogDecision(bool Resync, bool Reinstall);

/// <summary>
/// Pure policy behind the keyboard hook's health check.
///
/// Windows silently removes a WH_KEYBOARD_LL hook whose callback overruns
/// LowLevelHooksTimeout, which a game load spike or an alt-tab stall is more than capable of
/// causing. There is no notification and the handle stays non-null, so the only honest
/// evidence is the tracked hold state disagreeing with the physical key.
///
/// A single disagreeing sample is not proof: a key pressed on the poll boundary can be seen
/// by GetAsyncKeyState before the callback has run. The tracked state is therefore corrected
/// straight away - cheap, and the overlay draws correctly again either way - while the far
/// more expensive reinstall waits for the disagreement to survive consecutive polls.
/// </summary>
internal sealed class HookWatchdog
{
    /// <summary>Consecutive disagreeing samples before the hook is presumed dead.</summary>
    public const int ReinstallAfterSamples = 2;

    private int _disagreements;

    public HookWatchdogDecision Observe(bool physicalDown, bool trackedDown)
    {
        if (physicalDown == trackedDown)
        {
            _disagreements = 0;
            return default;
        }

        _disagreements++;
        return new HookWatchdogDecision(true, _disagreements >= ReinstallAfterSamples);
    }

    /// <summary>Called once the hook has been replaced, so the next poll starts clean.</summary>
    public void Reset() => _disagreements = 0;
}
