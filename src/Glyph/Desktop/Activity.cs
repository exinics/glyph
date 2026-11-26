namespace Glyph.Desktop;

/// Decides when the wallpaper can't be seen (so drawing it would waste power).
internal static class Activity
{
    static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "SHELLDLL_DefView", "",
    };

    public static volatile bool Locked;

    public static string Reason(PerformanceOptions s, out int fpsCap)
    {
        fpsCap = 0;
        Native.GetSystemPowerStatus(out var power);
        bool onBattery = power.ACLineStatus == 0;
        if (onBattery && s.BatteryFps > 0) fpsCap = s.BatteryFps;
        if (s.PauseWhenLocked && Locked) return "locked";
        if (s.PauseOnBatterySaver && power.SystemStatusFlag == 1) return "battery saver";

        var fg = Native.GetForegroundWindow();
        var fgClass = fg == IntPtr.Zero ? "" : Native.ClassOf(fg);
        bool fgIsShell = ShellClasses.Contains(fgClass);

        if (s.PauseWhenFullscreen && !fgIsShell)
        {
            if (Native.SHQueryUserNotificationState(out int q) == 0 && (q == 3 || q == 4)) return "fullscreen app";
            if (CoversMonitor(fg)) return "fullscreen app";
        }
        if (s.PauseWhenMaximized && AllMonitorsCovered()) return "maximised windows";
        return "";
    }

    static bool CoversMonitor(IntPtr h)
    {
        if (h == IntPtr.Zero || !Native.IsWindowVisible(h) || Native.IsIconic(h)) return false;
        if (!Native.GetWindowRect(h, out var r)) return false;
        var mon = Native.MonitorFromWindow(h, 2);
        var mi = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(mon, ref mi)) return false;
        return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
    }

    static bool AllMonitorsCovered()
    {
        var monitors = new HashSet<IntPtr>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr _, ref Native.RECT _, IntPtr _) => { monitors.Add(m); return true; }, IntPtr.Zero);
        if (monitors.Count == 0) return false;
        var covered = new HashSet<IntPtr>();
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || !Native.IsZoomed(h) || Native.IsCloaked(h)) return true;
            if ((Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return true;
            covered.Add(Native.MonitorFromWindow(h, 2));
            return covered.Count < monitors.Count;
        }, IntPtr.Zero);
        return monitors.IsSubsetOf(covered);
    }
}
