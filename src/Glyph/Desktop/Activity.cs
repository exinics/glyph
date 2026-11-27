namespace Glyph.Desktop;

/// Decides when the wallpaper can't be seen (so drawing it would waste power).
internal static class Activity
{
    static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "SHELLDLL_DefView", "",
    };

    public static volatile bool Locked;

    /// Why every screen should rest (locked, a power state set to pause), or "". `mode` = the preset for the
    /// power state now: highest, balanced, low or pause.
    public static string Reason(PerformanceOptions s, out string mode)
    {
        Native.GetSystemPowerStatus(out var power);
        bool saver = power.SystemStatusFlag == 1, onBattery = power.ACLineStatus == 0;
        mode = saver ? s.BatterySaver : onBattery ? s.OnBattery : s.PluggedIn;
        if (s.PauseWhenLocked && Locked) return "locked";
        if (mode == "pause") return saver ? "battery saver" : onBattery ? "on battery" : "plugged in";
        return "";
    }

    /// The screens (by number) that can't be seen: a fullscreen app or maximised windows on them.
    public static Dictionary<int, string> Covered(PerformanceOptions s)
    {
        var res = new Dictionary<int, string>();
        var screens = Screens.All();
        int NumberOf(IntPtr h) { var m = Native.MonitorFromWindow(h, 2); return screens.FirstOrDefault(x => x.Handle == m)?.Number ?? -1; }
        var fg = Native.GetForegroundWindow();
        bool fgIsShell = ShellClasses.Contains(fg == IntPtr.Zero ? "" : Native.ClassOf(fg));
        if (s.PauseWhenFullscreen && !fgIsShell && fg != IntPtr.Zero)
        {
            bool full = (Native.SHQueryUserNotificationState(out int q) == 0 && (q == 3 || q == 4)) || CoversMonitor(fg);
            if (full && NumberOf(fg) is int n and >= 0) res[n] = "fullscreen app";
        }
        if (s.PauseWhenMaximized)
            Native.EnumWindows((h, _) =>
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || !Native.IsZoomed(h) || Native.IsCloaked(h)) return true;
                if ((Native.GetWindowLong(h, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0) return true;
                int n = NumberOf(h);
                if (n >= 0) res.TryAdd(n, "maximised windows");
                return res.Count < screens.Count;
            }, IntPtr.Zero);
        return res;
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


}
