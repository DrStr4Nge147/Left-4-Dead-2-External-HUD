using System.Threading;

namespace OverlayHud.Services;

/// <summary>Owns a per-Windows-session mutex for the lifetime of the primary process.</summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    public const string MutexName = @"Local\Left4Dead2CustomizedOverlayHudExternal";

    /// <summary>
    /// Shown to a duplicate launch. Until v0.6.6 a second copy exited silently, which is
    /// indistinguishable from the app failing to start at all.
    /// </summary>
    public const string AlreadyRunningMessage =
        "The overlay app is already running.\n\n" +
        "Look for its icon in the notification area, next to the clock. Right-click it for " +
        "Customize UI... or Exit.\n\n" +
        "Only one copy can run at a time, so this one will close.";

    private Mutex? _mutex;

    public bool IsPrimary { get; }

    public SingleInstanceGuard(string mutexName = MutexName)
    {
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        IsPrimary = createdNew;
    }

    public void Dispose()
    {
        if (_mutex == null) return;

        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }

        _mutex.Dispose();
        _mutex = null;
    }
}
