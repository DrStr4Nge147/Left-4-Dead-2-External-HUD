using System.Diagnostics;

namespace OverlayHud.Services;

internal static class GameProcessProbe
{
    public static bool IsRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        Process[] processes;
        try { processes = Process.GetProcessesByName(processName); }
        catch { return false; }

        try { return processes.Length > 0; }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }
}
