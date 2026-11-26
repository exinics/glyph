namespace Glyph.Desktop;

/// Hides or shows Windows' own desktop icons, the same way as right-click > View >
/// "Show desktop icons". Glyph remembers if it hid them, so it only ever puts back what it changed.
internal static class DesktopIcons
{
    static IntPtr DefView()
    {
        var progman = Native.FindWindow("Progman", null);
        var dv = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (dv != IntPtr.Zero) return dv;
        IntPtr found = IntPtr.Zero;
        Native.EnumWindows((top, _) =>
        {
            var d = Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (d != IntPtr.Zero) { found = d; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool Visible
    {
        get
        {
            var dv = DefView();
            var lv = Native.FindWindowEx(dv, IntPtr.Zero, "SysListView32", null);
            return lv != IntPtr.Zero && Native.IsWindowVisible(lv);
        }
    }

    const string Marker = "icons_hidden_by_glyph";
    static bool HiddenByUs
    {
        get => File.Exists(Paths.State) && File.ReadAllLines(Paths.State).Contains(Marker);
        set
        {
            Directory.CreateDirectory(Paths.Logs);
            var lines = File.Exists(Paths.State) ? File.ReadAllLines(Paths.State).Where(l => l != Marker).ToList() : new List<string>();
            if (value) lines.Add(Marker);
            File.WriteAllLines(Paths.State, lines);
        }
    }

    static void Toggle()
    {
        var dv = DefView();
        if (dv != IntPtr.Zero) Native.SendMessage(dv, Native.WM_COMMAND, (IntPtr)0x7402, IntPtr.Zero);
    }

    /// Makes the icons match the setting. Safe to call often.
    public static void Apply(bool hide)
    {
        try
        {
            bool visible = Visible;
            if (hide && visible) { Toggle(); HiddenByUs = true; Log.Write("desktop icons hidden"); }
            else if (!hide && !visible && HiddenByUs) { Toggle(); HiddenByUs = false; Log.Write("desktop icons shown again"); }
        }
        catch (Exception e) { Log.Write("desktop icons: " + e.Message); }
    }

    /// On quit: put the icons back if Glyph hid them.
    public static void Restore()
    {
        try
        {
            if (HiddenByUs && !Visible) Toggle();
            HiddenByUs = false;
        }
        catch (Exception e) { Log.Write("desktop icons: " + e.Message); }
    }
}
