using System.Runtime.InteropServices;

namespace AutoHDR.UI;

internal static class DpiBootstrap
{
    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            // Must happen before ApplicationConfiguration.Initialize() and before
            // any HWND/control is created. Windows may otherwise lock the process
            // into the DPI context observed during early sign-in.
            SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }
        catch
        {
            // The application manifest / WinForms configuration remains the fallback.
        }
    }
}
