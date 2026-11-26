using System.Runtime.InteropServices;

namespace Glyph.Desktop;

/// Auto-hides the Windows taskbar while Glyph runs (if you choose), and puts it back on quit.
internal static class Taskbar
{
    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public Native.RECT rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    const uint ABM_GETSTATE = 4, ABM_SETSTATE = 10;
    const int ABS_AUTOHIDE = 1, ABS_ALWAYSONTOP = 2;
    const string Marker = "taskbar_autohide_by_glyph";

    static APPBARDATA Data() => new() { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = Native.FindWindow("Shell_TrayWnd", null) };

    static bool AutoHide
    {
        get { var d = Data(); return ((long)SHAppBarMessage(ABM_GETSTATE, ref d) & ABS_AUTOHIDE) != 0; }
        set { var d = Data(); d.lParam = (IntPtr)(value ? ABS_AUTOHIDE : ABS_ALWAYSONTOP); SHAppBarMessage(ABM_SETSTATE, ref d); }
    }

    static bool ChangedByUs
    {
        get => File.Exists(Paths.State) && File.ReadAllText(Paths.State).Contains(Marker);
        set
        {
            Directory.CreateDirectory(Paths.Logs);
            var lines = File.Exists(Paths.State) ? File.ReadAllLines(Paths.State).Where(l => l != Marker).ToList() : new List<string>();
            if (value) lines.Add(Marker);
            File.WriteAllLines(Paths.State, lines);
        }
    }

    const string HiddenMarker = "taskbar_hidden_by_glyph";
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

    static IEnumerable<IntPtr> Bars()
    {
        var main = Native.FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) yield return main;
        IntPtr h = IntPtr.Zero;
        while ((h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero) yield return h;
    }

    static bool Marked(string m) => File.Exists(Paths.State) && File.ReadAllLines(Paths.State).Contains(m);
    static void Mark(string m, bool on)
    {
        Directory.CreateDirectory(Paths.Logs);
        var lines = File.Exists(Paths.State) ? File.ReadAllLines(Paths.State).Where(l => l != m).ToList() : new List<string>();
        if (on) lines.Add(m);
        File.WriteAllLines(Paths.State, lines);
    }

    /// "hide": called every second, because Windows shows the taskbar again now and then (e.g. the Win key).
    public static void Enforce(string mode)
    {
        if (mode != "hide") return;
        foreach (var b in Bars()) if (Native.IsWindowVisible(b)) ShowWindow(b, 0);
    }

    public static void Apply(string mode)
    {
        try
        {
            if (mode == "hide")
            {
                if (!AutoHide) { AutoHide = true; ChangedByUs = true; }   // frees the space it reserves
                foreach (var b in Bars()) ShowWindow(b, 0);
                if (!Marked(HiddenMarker)) { Mark(HiddenMarker, true); Log.Write("taskbar hidden"); }
                return;
            }
            if (Marked(HiddenMarker)) { foreach (var b in Bars()) ShowWindow(b, 5); Mark(HiddenMarker, false); Log.Write("taskbar shown"); }
            bool want = mode == "autohide";
            if (want && !AutoHide) { AutoHide = true; ChangedByUs = true; Log.Write("taskbar set to auto-hide"); }
            else if (!want && AutoHide && ChangedByUs) { AutoHide = false; ChangedByUs = false; Log.Write("taskbar shown again"); }
        }
        catch (Exception e) { Log.Write("taskbar: " + e.Message); }
    }

    public static void Restore()
    {
        try
        {
            if (Marked(HiddenMarker)) { foreach (var b in Bars()) ShowWindow(b, 5); Mark(HiddenMarker, false); }
            if (ChangedByUs) { AutoHide = false; ChangedByUs = false; }
        }
        catch { }
    }
}
