using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Glyph.Data;

/// The open windows a taskbar would show (bar module "tasks"): read at most twice a second, icons kept by window.
internal static class Tasks
{
    public sealed record Win(IntPtr Handle, string Title, string App, bool Active, bool Minimised);

    static List<Win> list = new();
    static long readAt = -10_000;
    static readonly Dictionary<IntPtr, Bitmap?> icons = new();
    static readonly object gate = new();

    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] static extern IntPtr GetClassLongPtr(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;

    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
    };

    /// The windows now (cached for 500 ms).
    public static List<Win> Now()
    {
        long now = Environment.TickCount64;
        lock (gate)
        {
            if (now - readAt < 500) return list;
            readAt = now;
            list = Read();
            // forget icons of windows that closed
            foreach (var h in icons.Keys.Where(h => !list.Any(w => w.Handle == h)).ToList()) { icons[h]?.Dispose(); icons.Remove(h); }
            return list;
        }
    }

    static List<Win> Read()
    {
        var res = new List<Win>();
        var fg = Native.GetForegroundWindow();
        int self = Environment.ProcessId;
        var names = new Dictionary<uint, string>();
        Native.EnumWindows((h, _) =>
        {
            if (!Native.IsWindowVisible(h) || GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
            int ex = Native.GetWindowLong(h, GWL_EXSTYLE);
            if ((ex & WS_EX_TOOLWINDOW) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
            if ((ex & WS_EX_NOACTIVATE) != 0 && (ex & WS_EX_APPWINDOW) == 0) return true;
            if (Native.DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out int cloaked, 4) == 0 && cloaked != 0) return true;
            if (Skip.Contains(Native.ClassOf(h))) return true;
            var sb = new StringBuilder(256);
            Native.GetWindowText(h, sb, 256);
            var title = sb.ToString();
            if (title.Length == 0) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == self) return true;
            if (!names.TryGetValue(pid, out var app)) names[pid] = app = AppName(pid);
            res.Add(new Win(h, title, app, h == fg, Native.IsIconic(h)));
            return true;
        }, IntPtr.Zero);
        // Windows lists windows front to back, so using one would move it to the front of the bar: instead each
        // window keeps the place it got when it first appeared (new ones go on the end, like Windows' taskbar).
        // Windows already open when Glyph starts are placed back to front (roughly oldest first).
        for (int i = res.Count - 1; i >= 0; i--)
            if (!order.ContainsKey(res[i].Handle)) order[res[i].Handle] = nextPlace++;
        foreach (var gone in order.Keys.Where(k => !res.Any(w => w.Handle == k)).ToList()) order.Remove(gone);
        return res.OrderBy(w => order[w.Handle]).ToList();
    }

    static readonly Dictionary<IntPtr, long> order = new();
    static long nextPlace;

    // a program's name only changes when the process does: kept per process (with its start time, so a
    // recycled process id isn't mistaken for the old program); looking it up reads the exe's version info
    static readonly Dictionary<uint, (DateTime started, string name)> names = new();

    static string AppName(uint pid)
    {
        lock (names)
        {
            DateTime started = default;
            try { using var pp = Process.GetProcessById((int)pid); started = pp.StartTime; } catch { }
            if (names.TryGetValue(pid, out var hit) && hit.started == started) return hit.name;
            var name = LookUpName(pid);
            if (names.Count > 300) names.Clear();
            names[pid] = (started, name);
            return name;
        }
    }

    static string LookUpName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            try
            {
                var desc = p.MainModule?.FileVersionInfo.FileDescription;
                if (!string.IsNullOrWhiteSpace(desc)) return desc.Trim();
            }
            catch { }
            return p.ProcessName;
        }
        catch { return ""; }
    }

    static uint fgPid; static string fgApp = "";

    /// The name of the program whose window is in front ("" for the desktop and taskbar).
    public static string ForegroundApp()
    {
        if (Stats.ActiveWindowTitle().Length == 0) return "";
        GetWindowThreadProcessId(Native.GetForegroundWindow(), out uint pid);
        if (pid == fgPid) return fgApp;
        fgPid = pid; fgApp = pid == Environment.ProcessId ? "" : AppName(pid);
        return fgApp;
    }

    /// The window's icon (small), or null. Made once per window.
    public static Bitmap? Icon(IntPtr h)
    {
        lock (gate)
        {
            if (icons.TryGetValue(h, out var b)) return b;
            icons[h] = b = ReadIcon(h);
            return b;
        }
    }

    static Bitmap? ReadIcon(IntPtr h)
    {
        IntPtr hi = IntPtr.Zero;
        foreach (var kind in new[] { 2 /* ICON_SMALL2 */, 0 /* ICON_SMALL */, 1 /* ICON_BIG */ })
        {
            Native.SendMessageTimeout(h, 0x007F /* WM_GETICON */, (IntPtr)kind, IntPtr.Zero, 0x2 /* SMTO_ABORTIFHUNG */, 100, out hi);
            if (hi != IntPtr.Zero) break;
        }
        if (hi == IntPtr.Zero) hi = GetClassLongPtr(h, -34 /* GCLP_HICONSM */);
        if (hi == IntPtr.Zero) hi = GetClassLongPtr(h, -14 /* GCLP_HICON */);
        try
        {
            if (hi != IntPtr.Zero) { using var ic = System.Drawing.Icon.FromHandle(hi); return new Bitmap(ic.ToBitmap()); }
            GetWindowThreadProcessId(h, out uint pid);
            using var p = Process.GetProcessById((int)pid);
            var path = p.MainModule?.FileName;
            if (path != null) { using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path); if (ic != null) return new Bitmap(ic.ToBitmap()); }
        }
        catch { }
        return null;
    }

    /// Clicking a task (one window, or a program's windows grouped together): brings it to the front, or
    /// minimises it if it already is; with several windows, the next one comes to the front.
    public static void Toggle(List<IntPtr> list)
    {
        list = list.Where(Native.IsWindow).ToList();
        if (list.Count == 0) return;
        var fg = Native.GetForegroundWindow();
        int at = list.IndexOf(fg);
        if (at >= 0 && list.Count > 1) { Front(list[(at + 1) % list.Count]); return; }
        var h = list[0];
        if (fg == h && !Native.IsIconic(h)) { Native.ShowWindow(h, 6 /* SW_MINIMIZE */); return; }
        Front(h);
    }

    /// Right-click actions on a task: close, minimise, maximise.
    public static void Do(string what, List<IntPtr> list)
    {
        foreach (var h in list.Where(Native.IsWindow))
            switch (what)
            {
                case "close": Native.PostMessage(h, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero); break;
                case "minimise": case "minimize": Native.ShowWindow(h, 6); break;
                case "maximise": case "maximize": Native.ShowWindow(h, Native.IsZoomed(h) ? 9 : 3); break;
            }
        lock (gate) readAt = -10_000;
    }

    static void Front(IntPtr h)
    {
        if (Native.IsIconic(h)) Native.ShowWindow(h, 9 /* SW_RESTORE */);
        if (!SetForegroundWindow(h))
        {
            // Windows only lets the program that has the keyboard pick the front window; a tap of Alt counts as input
            keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero);
            SetForegroundWindow(h);
        }
        lock (gate) readAt = -10_000;
    }
}
