using System.ComponentModel;
using System.Runtime.InteropServices;
using Glyph.Options;

namespace Glyph.Desktop;

/// One display. Number = Windows' own display number (\\.\DISPLAY2 = 2), as in Settings > System > Display.
/// Scale = Windows' display scaling for it (1 = 100%, 1.5 = 150%).
internal sealed record ScreenInfo(int Number, Rectangle Bounds, Rectangle Work, bool Primary, IntPtr Handle, float Scale = 1);

internal static class Screens
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX { public int cbSize; public Native.RECT rcMonitor, rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFOEX mi);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr mon, int type, out uint x, out uint y);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Native.POINT p, uint flags);

    /// Windows' scaling for a display (1 = 100%).
    public static float ScaleOf(IntPtr mon) => TestScale ?? (GetDpiForMonitor(mon, 0 /* effective */, out var x, out _) == 0 && x > 0 ? x / 96f : 1f);
    /// GLYPH_SCALE=1.5 pretends every display is scaled that much (for testing scaling on a 100% screen).
    static readonly float? TestScale = float.TryParse(Environment.GetEnvironmentVariable("GLYPH_SCALE"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) && t > 0.2f ? t : null;
    public static float ScaleAt(int x, int y) => ScaleOf(MonitorFromPoint(new Native.POINT { X = x, Y = y }, 2));

    /// Whether Glyph follows Windows' scaling (glyph.toml [monitors] scaling). Set when settings load.
    public static volatile bool Scaling = true;
    /// The scale to draw at on a display, or 1 when scaling is off.
    public static float Use(float scale) => Scaling ? scale : 1f;

    /// Every display, by number. Safe on any thread.
    public static List<ScreenInfo> All()
    {
        var list = new List<ScreenInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr _, ref Native.RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(m, ref mi))
            {
                var digits = new string((mi.szDevice ?? "").Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                int n = int.TryParse(digits, out var k) ? k : list.Count + 1;
                while (list.Any(s => s.Number == n)) n++;
                list.Add(new ScreenInfo(n, R(mi.rcMonitor), R(mi.rcWork), (mi.dwFlags & 1) != 0, m, ScaleOf(m)));
            }
            return true;
        }, IntPtr.Zero);
        return list.OrderBy(s => s.Number).ToList();
    }

    /// The screen's usable area right now (without bars and the taskbar that reserve space).
    public static Rectangle WorkOf(ScreenInfo s)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(s.Handle, ref mi) ? R(mi.rcWork) : s.Work;
    }

    static Rectangle R(Native.RECT r) => Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);

    /// Changes when a display is added, removed, moved or resized (not when the work area changes).
    public static string Key() => string.Join(";", All().Select(s => $"{s.Number}:{s.Bounds}:{s.Scale}"));

    /// The screen a setting names: "primary" or a number (falls back to the primary display).
    public static ScreenInfo Pick(List<ScreenInfo> all, string which)
    {
        if (int.TryParse(which, out var n) && all.FirstOrDefault(s => s.Number == n) is { } byNum) return byNum;
        return all.FirstOrDefault(s => s.Primary) ?? all.First();
    }

    public static string Describe(ScreenInfo s) => $"{s.Number}: {s.Bounds.Width}×{s.Bounds.Height} at {s.Scale * 100:0}%{(s.Primary ? ", main display" : "")}";

    /// A font made bigger for a scaled display (characters, the bar's font).
    public static FontOptions Scaled(FontOptions f, float s) => Math.Abs(s - 1) < 0.01f ? f : new FontOptions
    {
        Font = f.Font, FontSize = f.FontSize * s, FontWeight = f.FontWeight, LineSpacing = f.LineSpacing,
        CellWidth = (int)MathF.Round(f.CellWidth * s), CellHeight = (int)MathF.Round(f.CellHeight * s),
    };
}

/// glyph.toml [monitors]: what Glyph shows on each display.
internal sealed class MonitorOptions
{
    [Description("same = the theme on every screen. each = a different theme on each screen (Screen 1 theme… below). one = only one screen (Main screen); the others keep Windows' own wallpaper."),
     Choices("same", "each", "one"), TypeConverter(typeof(ChoicesConverter))]
    public string Mode { get; set; } = "same";
    [DisplayName("Main screen"), Description("one: the screen Glyph uses. Also where the bar and widgets go when they're on the main screen only. primary = Windows' main display, or a display number (as in Windows' Settings > System > Display)."),
     TypeConverter(typeof(ScreenNameConverter))]
    public string Main { get; set; } = "primary";
    [DisplayName("Bar on"), Description("every = each screen shows its theme's bar. main = only the main screen has a bar."), Choices("every", "main"), TypeConverter(typeof(ChoicesConverter))]
    public string Bar { get; set; } = "every";
    [DisplayName("Widgets on"), Description("every = each screen shows its theme's widgets. main = widgets only on the main screen (the others just show the wallpaper)."), Choices("every", "main"), TypeConverter(typeof(ChoicesConverter))]
    public string Widgets { get; set; } = "every";
    [DisplayName("Follow Windows scaling"), Description("Make everything bigger on screens Windows scales up (Settings > System > Display > Scale), each screen by its own amount. Off = sizes are exact pixels on every screen.")]
    public bool Scaling { get; set; } = true;
    [DisplayName("Screen 1 theme"), Description("each: the theme on display 1. auto = the theme in use (Themes page), none = leave this screen alone (Windows' wallpaper)."), TypeConverter(typeof(ThemeNameConverter))]
    public string Screen1 { get; set; } = "auto";
    [DisplayName("Screen 2 theme"), Description("each: the theme on display 2."), TypeConverter(typeof(ThemeNameConverter))] public string Screen2 { get; set; } = "auto";
    [DisplayName("Screen 3 theme"), Description("each: the theme on display 3."), TypeConverter(typeof(ThemeNameConverter))] public string Screen3 { get; set; } = "auto";
    [DisplayName("Screen 4 theme"), Description("each: the theme on display 4."), TypeConverter(typeof(ThemeNameConverter))] public string Screen4 { get; set; } = "auto";

    public string ThemeFor(int number) => number switch { 1 => Screen1, 2 => Screen2, 3 => Screen3, 4 => Screen4, _ => "auto" };

    /// One entry per screen Glyph draws on: which theme, and whether it gets widgets and a bar.
    public List<(ScreenInfo screen, string theme, bool widgets, bool bar)> Plan(string themeInUse)
    {
        var all = Screens.All();
        var res = new List<(ScreenInfo, string, bool, bool)>();
        if (all.Count == 0) return res;
        var main = Screens.Pick(all, Main);
        foreach (var s in all)
        {
            string? theme = Mode switch
            {
                "one" => s.Number == main.Number ? themeInUse : null,
                "each" => ThemeFor(s.Number) switch { "none" => null, "auto" or "" => themeInUse, var t => Directory.Exists(Paths.ThemeDir(t)) ? t : themeInUse },
                _ => themeInUse,
            };
            if (theme == null) continue;
            bool isMain = s.Number == main.Number;
            res.Add((s, theme, Widgets != "main" || isMain, Bar != "main" || isMain));
        }
        if (res.Count == 0) res.Add((main, themeInUse, true, true));   // every screen set to none: still use the main one
        return res;
    }
}

/// Drop-down of the themes in config\themes (plus auto and none).
internal sealed class ThemeNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c)
    {
        var names = Directory.Exists(Paths.Themes) ? Directory.GetDirectories(Paths.Themes).Where(d => File.Exists(Path.Combine(d, "theme.toml"))).Select(Path.GetFileName).OfType<string>() : Enumerable.Empty<string>();
        return new StandardValuesCollection(names.OrderBy(n => n).Prepend("none").Prepend("auto").ToArray());
    }
}

/// Drop-down of the displays connected now (primary, 1, 2…).
internal sealed class ScreenNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c) =>
        new(Screens.All().Select(s => s.Number.ToString()).Prepend("primary").ToArray());
}
