using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Glyph.Options;

namespace Glyph.Tiling;

/// glyph.toml [tiling]
internal sealed class TilingOptions
{
    [DisplayName("Tiling on"), Description("Arrange windows side by side automatically, like i3 or Hyprland. Win+Ctrl+Alt+T turns it on and off (Hotkeys page).")]
    public bool Enabled { get; set; }
    [Description("master = one big window on the left, the rest stacked on the right. dwindle = each new window splits the last one in half. columns / rows = equal strips. monocle = every window full size (switch between them with the focus keys)."),
     Choices("dwindle", "master", "columns", "rows", "monocle"), TypeConverter(typeof(ChoicesConverter))]
    public string Layout { get; set; } = "dwindle";
    [DisplayName("Main window share"), Description("How much of the screen the first window gets (master and dwindle). Win+Ctrl+Alt+minus / equals change it, and so does dragging the first window's edge."), Range(0.1, 0.9)]
    public float Ratio { get; set; } = 0.5f;
    [Description("Space between windows, in pixels."), Range(0, 200)] public int Gap { get; set; } = 10;
    [DisplayName("Outer gap"), Description("Space between the windows and the edge of the screen, in pixels."), Range(0, 400)] public int OuterGap { get; set; } = 10;
    [DisplayName("New windows"), Description("Where a new window goes: after_focused = next to the window you're using, end = after all the others, start = becomes the main window."),
     Choices("after_focused", "end", "start"), TypeConverter(typeof(ChoicesConverter))]
    public string NewWindows { get; set; } = "after_focused";
    [DisplayName("Active border"), Description("Border colour of the window you're using (Windows 11). none = Windows' own."), Color, TypeConverter(typeof(ColorNameConverter))]
    public string ActiveBorder { get; set; } = "accent";
    [DisplayName("Inactive border"), Description("Border colour of the other tiled windows. none = Windows' own."), Color, TypeConverter(typeof(ColorNameConverter))]
    public string InactiveBorder { get; set; } = "none";
    [Description("Window corners while tiling: keep = Windows' rounding, square, round, small."), Choices("keep", "square", "round", "small"), TypeConverter(typeof(ChoicesConverter))]
    public string Corners { get; set; } = "keep";
    [DisplayName("Float these"), Description("Windows that stay where you put them instead of being tiled. Each line matches class:Name, process:name.exe, title:Text, or just text in the title. * matches anything, e.g. title:*Picture-in-Picture*.")]
    public string[] Float { get; set; } = { "class:#32770", "class:OperationStatusWindow", "title:*Picture-in-Picture*", "title:*Picture in picture*", "process:PowerToys*" };
    [DisplayName("Ignore these"), Description("Windows tiling never touches at all (no border either). Same patterns as Float these.")]
    public string[] Ignore { get; set; } = Array.Empty<string>();
}

/// Tiles windows per monitor. Event driven (WinEvent hooks), so it costs nothing while nothing changes.
/// Runs on the UI thread.
internal sealed class TileManager : IDisposable
{
    TilingOptions o = new();
    Func<Theme?> theme;
    bool enabled;
    readonly List<IntPtr> order = new();                      // tiling order (all monitors)
    readonly HashSet<IntPtr> floating = new();                // floated by the user
    readonly HashSet<IntPtr> failed = new();                  // couldn't be moved (e.g. run as administrator)
    readonly Dictionary<IntPtr, bool> zoomed = new();
    readonly Dictionary<IntPtr, Rectangle> placed = new();    // where each tiled window was put (visible frame)
    readonly Dictionary<IntPtr, string> layouts = new();      // per monitor
    readonly Dictionary<IntPtr, float> ratios = new();
    readonly HashSet<IntPtr> styled = new();                  // windows we changed the border/corners of
    readonly List<IntPtr> hooks = new();
    readonly WinEventProc proc;
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 40 };
    readonly System.Windows.Forms.Timer recheck = new() { Interval = 700 };   // after moving windows: did they all fit?
    bool movedAny;
    // maximise / restore: checked once a second (a hook for it would wake Glyph on every mouse move)
    readonly System.Windows.Forms.Timer zoomCheck = new() { Interval = 1000 };
    IntPtr lastFocus, dragging;
    long placedAt;
    static readonly uint ownPid = (uint)Environment.ProcessId;
    readonly uint uiThread = GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    public static string Status { get; private set; } = "off";
    public event Action? Changed;
    /// After windows were arranged.
    public event Action? Retiled;
    /// The windows tiling arranged last time.
    public IReadOnlyCollection<IntPtr> TiledWindows => enabled ? placed.Keys.ToList() : Array.Empty<IntPtr>();

    public TileManager(Func<Theme?> theme)
    {
        this.theme = theme;
        proc = OnEvent;
        debounce.Tick += (_, _) => { debounce.Stop(); Retile(); };
        recheck.Tick += (_, _) => { recheck.Stop(); Retile(); };
        zoomCheck.Tick += (_, _) => { foreach (var (h, z) in zoomed) if (z != Native.IsZoomed(h)) { Schedule(); break; } };
    }

    public bool Enabled => enabled;

    public void Apply(TilingOptions opts)
    {
        o = opts;
        if (opts.Enabled != enabled) SetEnabled(opts.Enabled);
        else if (enabled) { styled.ToList().ForEach(Unstyle); Schedule(); }
        UpdateStatus();
    }

    void SetEnabled(bool on)
    {
        enabled = on;
        if (on)
        {
            Hook(0x0003, 0x0003);   // foreground
            Hook(0x000A, 0x000B);   // move/size start, end
            Hook(0x0016, 0x0017);   // minimise start, end
            Hook(0x8001, 0x8003);   // destroy, show, hide
            Hook(0x8017, 0x8018);   // cloaked, uncloaked (virtual desktops, UWP apps)
            order.Clear(); placed.Clear(); zoomed.Clear(); failed.Clear();
            zoomCheck.Start();
            Log.Write("tiling on (" + o.Layout + ")");
            Retile();
        }
        else
        {
            foreach (var h in hooks) UnhookWinEvent(h);
            hooks.Clear();
            debounce.Stop(); zoomCheck.Stop(); recheck.Stop();
            foreach (var h in styled.ToList()) Unstyle(h);
            order.Clear(); placed.Clear(); floating.Clear();
            Log.Write("tiling off");
        }
        UpdateStatus();
    }

    void Hook(uint min, uint max)
    {
        var h = SetWinEventHook(min, max, IntPtr.Zero, proc, 0, 0, 0 /* WINEVENT_OUTOFCONTEXT */);
        if (h != IntPtr.Zero) hooks.Add(h);
    }

    void Schedule() { if (!enabled) return; debounce.Stop(); debounce.Start(); }

    void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero || thread == uiThread) return;   // Glyph's own windows (e.g. timers) don't count
        switch (ev)
        {
            case 0x0003: UpdateBorders(); if (!order.Contains(hwnd)) Schedule(); return;
            case 0x000B: dragging = IntPtr.Zero; OnDropped(hwnd); return;
            case 0x000A: dragging = hwnd; return;
            default:
                if (Native.GetAncestor(hwnd, 2 /* GA_ROOT */) != hwnd) return;
                Schedule();
                return;
        }
    }

    // ───────────── which windows ─────────────

    static readonly HashSet<string> SystemClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "ApplicationManager_ImmersiveShellWindow", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow",
        "MultitaskingViewFrame", "TaskSwitcherWnd", "Windows.Internal.Shell.TabProxyWindow", "LockScreenControllerProxyWindow",
    };

    enum Kind { No, Float, Tile }

    Kind Classify(IntPtr h)
    {
        if (!Native.IsWindowVisible(h) || GetWindow(h, 4 /* GW_OWNER */) != IntPtr.Zero) return Kind.No;
        if (Native.DwmGetWindowAttribute(h, Native.DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) return Kind.No;
        int style = Native.GetWindowLong(h, Native.GWL_STYLE), ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        if ((style & 0x40000000) != 0 /* WS_CHILD */ || (ex & Native.WS_EX_TOOLWINDOW) != 0 || (ex & Native.WS_EX_NOACTIVATE) != 0) return Kind.No;
        if ((style & 0x00C00000) != 0x00C00000 /* WS_CAPTION */) return Kind.No;
        var cls = Class(h);
        if (SystemClasses.Contains(cls)) return Kind.No;
        var title = Title(h);
        if (title.Length == 0) return Kind.No;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == ownPid) return Kind.No;
        string proc = ProcessName(pid);
        if (Matches(o.Ignore, cls, title, proc)) return Kind.No;
        if ((style & 0x00040000) == 0 /* WS_THICKFRAME: can't be resized */ || (ex & 0x8) != 0 /* topmost */) return Kind.Float;
        if (Matches(o.Float, cls, title, proc) || floating.Contains(h) || failed.Contains(h)) return Kind.Float;
        return Kind.Tile;
    }

    static readonly Dictionary<uint, string> procNames = new();
    static string ProcessName(uint pid)
    {
        if (procNames.TryGetValue(pid, out var n)) return n;
        try { using var p = Process.GetProcessById((int)pid); n = p.ProcessName + ".exe"; } catch { n = ""; }
        if (procNames.Count > 500) procNames.Clear();
        return procNames[pid] = n;
    }

    static bool Matches(string[] rules, string cls, string title, string proc)
    {
        foreach (var raw in rules)
        {
            var r = raw.Trim();
            if (r.Length == 0) continue;
            string what = title, pat = r;
            bool contains = true;
            int c = r.IndexOf(':');
            if (c > 0)
            {
                var k = r[..c].ToLowerInvariant();
                if (k is "class" or "process" or "title") { what = k == "class" ? cls : k == "process" ? proc : title; pat = r[(c + 1)..].Trim(); contains = false; }
            }
            if (contains && !pat.Contains('*')) { if (what.Contains(pat, StringComparison.OrdinalIgnoreCase)) return true; continue; }
            if (Wild(what, pat)) return true;
        }
        return false;
    }

    static bool Wild(string s, string p)
    {
        var re = "^" + System.Text.RegularExpressions.Regex.Escape(p).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(s, re, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    static string Class(IntPtr h) { var sb = new StringBuilder(256); Native.GetClassName(h, sb, 256); return sb.ToString(); }
    static string Title(IntPtr h) { var sb = new StringBuilder(256); Native.GetWindowText(h, sb, 256); return sb.ToString(); }

    // ───────────── laying out ─────────────

    public void Retile()
    {
        if (!enabled) return;
        var tile = new List<IntPtr>();
        var all = new List<IntPtr>();
        Native.EnumWindows((h, _) => { var k = Classify(h); if (k != Kind.No) all.Add(h); if (k == Kind.Tile) tile.Add(h); return true; }, IntPtr.Zero);

        // forget closed windows; keep minimised / other-desktop ones in their place for when they come back
        order.RemoveAll(h => !Native.IsWindow(h));
        floating.RemoveWhere(h => !Native.IsWindow(h));
        failed.RemoveWhere(h => !Native.IsWindow(h));
        var fg = Native.GetForegroundWindow();
        // new windows (EnumWindows gives the top one first: add from the bottom so the newest ends up last)
        for (int i = tile.Count - 1; i >= 0; i--)
        {
            var h = tile[i];
            if (order.Contains(h)) continue;
            int at = o.NewWindows switch
            {
                "start" => 0,
                "end" => order.Count,
                _ => lastFocus != IntPtr.Zero && order.IndexOf(lastFocus) is int fi and >= 0 ? fi + 1 : order.Count,
            };
            order.Insert(Math.Min(at, order.Count), h);
        }

        // a window that stayed bigger than its tile has a minimum size it can't go under: float it
        if (Environment.TickCount64 - placedAt > 400)
            foreach (var (h, want) in placed)
            {
                if (h == dragging || !Native.IsWindow(h) || Native.IsIconic(h) || Native.IsZoomed(h) || floating.Contains(h)) continue;
                if (DwmGetWindowAttribute(h, 9, out Native.RECT fr, Marshal.SizeOf<Native.RECT>()) == 0 && (fr.Width > want.Width + 24 || fr.Height > want.Height + 24))
                {
                    failed.Add(h);
                    Log.Write($"tiling: \"{Title(h)}\" can't get small enough for its tile - floating it");
                    tile.Remove(h);
                    var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
                    if (Native.GetMonitorInfo(Native.MonitorFromWindow(h, 2), ref mi))
                    {
                        var w = mi.rcWork;
                        int fw = Math.Min(fr.Width, w.Width), fh = Math.Min(fr.Height, w.Height);
                        Place(h, new Rectangle(w.Left + (w.Width - fw) / 2, w.Top + (w.Height - fh) / 2, fw, fh));   // in the middle, all of it on screen
                    }
                }
            }
        var tiled = order.Where(h => tile.Contains(h) && !Native.IsIconic(h) && !Native.IsZoomed(h)).ToList();
        zoomed.Clear();
        foreach (var h in order) zoomed[h] = Native.IsZoomed(h);
        placed.Clear();
        foreach (var mon in tiled.GroupBy(h => Native.MonitorFromWindow(h, 2)))
        {
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (!Native.GetMonitorInfo(mon.Key, ref mi)) continue;
            var w = mi.rcWork;
            var area = Rectangle.FromLTRB(w.Left + o.OuterGap, w.Top + o.OuterGap, w.Right - o.OuterGap, w.Bottom - o.OuterGap);
            var list = mon.ToList();
            var rects = Layout(LayoutOf(mon.Key), list.Count, area, RatioOf(mon.Key), o.Gap);
            if (Glyph.Desktop.WindowFrames.Room is { } room)   // leave space for window frames (a title tab above, a border…)
                rects = rects.Select(r => Rectangle.FromLTRB(r.Left + room.l, r.Top + room.t, r.Right - room.r, r.Bottom - room.b)).ToList();
            for (int i = 0; i < list.Count; i++) { Place(list[i], rects[i]); placed[list[i]] = rects[i]; }
            placedAt = Environment.TickCount64;
        }
        if (fg != IntPtr.Zero && order.Contains(fg)) lastFocus = fg;
        if (movedAny) { movedAny = false; recheck.Stop(); recheck.Start(); }
        UpdateBorders(all);
        UpdateStatus();
        Retiled?.Invoke();
    }

    string LayoutOf(IntPtr mon) => layouts.TryGetValue(mon, out var l) ? l : o.Layout;
    float RatioOf(IntPtr mon) => ratios.TryGetValue(mon, out var r) ? r : o.Ratio;

    /// The rectangles for n windows in `area`, with `gap` between them.
    public static List<Rectangle> Layout(string layout, int n, Rectangle area, float ratio, int gap)
    {
        var res = new List<RectangleF>();
        // work on the area grown by half a gap, then shrink every piece by half a gap: gaps come out even
        float g = gap / 2f;
        var a = new RectangleF(area.X - g, area.Y - g, area.Width + gap, area.Height + gap);
        ratio = Math.Clamp(ratio, 0.1f, 0.9f);
        switch (n == 1 ? "monocle" : layout)
        {
            case "monocle": for (int i = 0; i < n; i++) res.Add(a); break;
            case "columns": for (int i = 0; i < n; i++) res.Add(new RectangleF(a.X + a.Width * i / n, a.Y, a.Width / n, a.Height)); break;
            case "rows": for (int i = 0; i < n; i++) res.Add(new RectangleF(a.X, a.Y + a.Height * i / n, a.Width, a.Height / n)); break;
            case "master":
                {
                    float mw = a.Width * ratio;
                    res.Add(new RectangleF(a.X, a.Y, mw, a.Height));
                    int s = n - 1;
                    for (int i = 0; i < s; i++) res.Add(new RectangleF(a.X + mw, a.Y + a.Height * i / s, a.Width - mw, a.Height / s));
                    break;
                }
            default:   // dwindle
                {
                    var r = a;
                    for (int i = 0; i < n; i++)
                    {
                        if (i == n - 1) { res.Add(r); break; }
                        float f = i == 0 ? ratio : 0.5f;
                        if (r.Width >= r.Height)
                        {
                            res.Add(new RectangleF(r.X, r.Y, r.Width * f, r.Height));
                            r = new RectangleF(r.X + r.Width * f, r.Y, r.Width * (1 - f), r.Height);
                        }
                        else
                        {
                            res.Add(new RectangleF(r.X, r.Y, r.Width, r.Height * f));
                            r = new RectangleF(r.X, r.Y + r.Height * f, r.Width, r.Height * (1 - f));
                        }
                    }
                    break;
                }
        }
        return res.Select(r => Rectangle.FromLTRB((int)MathF.Round(r.Left + g), (int)MathF.Round(r.Top + g), (int)MathF.Round(r.Right - g), (int)MathF.Round(r.Bottom - g))).ToList();
    }

    /// Moves a window so its visible frame fills `r` (Windows 10/11 windows have an invisible resize
    /// border around them, so the window rectangle is a little bigger than what you see).
    void Place(IntPtr h, Rectangle r)
    {
        if (!Native.GetWindowRect(h, out var wr)) return;
        var fr = wr;
        if (DwmGetWindowAttribute(h, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out fr, Marshal.SizeOf<Native.RECT>()) != 0) fr = wr;
        int l = r.Left - (fr.Left - wr.Left), t = r.Top - (fr.Top - wr.Top), rr = r.Right + (wr.Right - fr.Right), b = r.Bottom + (wr.Bottom - fr.Bottom);
        if (wr.Left == l && wr.Top == t && wr.Right == rr && wr.Bottom == b) return;
        movedAny = true;
        if (!SetWindowPos(h, IntPtr.Zero, l, t, rr - l, b - t, 0x0004 | 0x0010 | 0x0200 | 0x4000 /* NOZORDER NOACTIVATE NOOWNERZORDER ASYNC */))
        {
            failed.Add(h);   // e.g. an app run as administrator: leave it floating
            Log.Write($"tiling: can't move \"{Title(h)}\" (run as administrator?) - leaving it floating");
        }
    }

    // ───────────── borders and corners ─────────────

    void UpdateBorders(List<IntPtr>? all = null)
    {
        if (!enabled) return;
        var t = theme();
        var fg = Native.GetForegroundWindow();
        bool doBorders = o.ActiveBorder != "none" || o.InactiveBorder != "none";
        int corner = o.Corners switch { "square" => 1, "round" => 2, "small" => 3, _ => -1 };
        foreach (var h in all ?? placed.Keys.Concat(floating).ToList())
        {
            if (!Native.IsWindow(h)) { styled.Remove(h); continue; }
            if (doBorders)
            {
                string name = h == fg ? o.ActiveBorder : o.InactiveBorder;
                int col = name == "none" || t == null || !t.TryResolve(name, out var c) ? unchecked((int)0xFFFFFFFF) /* DWMWA_COLOR_DEFAULT */ : (c.B << 16) | (c.G << 8) | c.R;
                DwmSetWindowAttribute(h, 34 /* DWMWA_BORDER_COLOR */, ref col, 4);
                styled.Add(h);
            }
            if (corner >= 0) { DwmSetWindowAttribute(h, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref corner, 4); styled.Add(h); }
        }
    }

    void Unstyle(IntPtr h)
    {
        if (Native.IsWindow(h))
        {
            int def = unchecked((int)0xFFFFFFFF), round = 0;
            DwmSetWindowAttribute(h, 34, ref def, 4);
            DwmSetWindowAttribute(h, 33, ref round, 4);
        }
        styled.Remove(h);
    }

    // ───────────── commands (hotkeys) ─────────────

    /// tile:toggle | tile:on | tile:off | tile:focus:left | tile:move:left | tile:master | tile:float |
    /// tile:layout:next | tile:layout:master | tile:ratio:+5 | tile:retile
    public bool Command(string arg, Action<bool> setEnabled)
    {
        var p = arg.ToLowerInvariant().Split(':', 2);
        string cmd = p[0], rest = p.Length > 1 ? p[1] : "";
        switch (cmd)
        {
            case "toggle": setEnabled(!enabled); return true;
            case "on": setEnabled(true); return true;
            case "off": setEnabled(false); return true;
        }
        if (!enabled) return false;
        var fg = Native.GetForegroundWindow();
        switch (cmd)
        {
            case "retile": Retile(); break;
            case "focus": if (Neighbour(fg, rest) is IntPtr f) Focus(f); break;
            case "move":
                {
                    if (Neighbour(fg, rest) is IntPtr n && order.IndexOf(fg) is int a and >= 0 && order.IndexOf(n) is int b and >= 0)
                    { (order[a], order[b]) = (order[b], order[a]); Retile(); }
                    break;
                }
            case "master":
                {
                    int i = order.IndexOf(fg);
                    var mon = Native.MonitorFromWindow(fg, 2);
                    int first = order.FindIndex(h => placed.ContainsKey(h) && Native.MonitorFromWindow(h, 2) == mon);
                    if (i >= 0 && first >= 0 && i != first) { (order[i], order[first]) = (order[first], order[i]); Retile(); }
                    break;
                }
            case "float":
                if (fg == IntPtr.Zero || Classify(fg) == Kind.No) break;
                if (floating.Remove(fg) | failed.Remove(fg)) { Retile(); break; }
                floating.Add(fg);
                {
                    // a floated window comes to the middle at 60% of the screen
                    var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
                    if (Native.GetMonitorInfo(Native.MonitorFromWindow(fg, 2), ref mi))
                    {
                        var w = mi.rcWork;
                        int ww = w.Width * 6 / 10, hh = w.Height * 6 / 10;
                        Place(fg, new Rectangle(w.Left + (w.Width - ww) / 2, w.Top + (w.Height - hh) / 2, ww, hh));
                    }
                }
                Retile();
                break;
            case "layout":
                {
                    var mon = Native.MonitorFromWindow(fg != IntPtr.Zero ? fg : Native.GetForegroundWindow(), 2);
                    string[] all = { "dwindle", "master", "columns", "rows", "monocle" };
                    string cur = LayoutOf(mon);
                    string next = rest is "" or "next" ? all[(Array.IndexOf(all, cur) + 1) % all.Length]
                        : rest == "prev" ? all[(Array.IndexOf(all, cur) + all.Length - 1) % all.Length]
                        : all.Contains(rest) ? rest : cur;
                    layouts[mon] = next;
                    Log.Write("tiling layout: " + next);
                    Retile();
                    break;
                }
            case "ratio":
                {
                    var mon = Native.MonitorFromWindow(fg, 2);
                    if (float.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                        ratios[mon] = Math.Clamp(RatioOf(mon) + d / 100f, 0.1f, 0.9f);
                    Retile();
                    break;
                }
            default: return false;
        }
        return true;
    }

    /// The tiled window next to `from` in a direction (left, right, up, down), by where they are on screen.
    IntPtr? Neighbour(IntPtr from, string dir)
    {
        if (!placed.TryGetValue(from, out var r)) { if (!Native.GetWindowRect(from, out var wr)) return placed.Keys.FirstOrDefault(); r = Rectangle.FromLTRB(wr.Left, wr.Top, wr.Right, wr.Bottom); }
        var c = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
        IntPtr? best = null; float bestD = float.MaxValue;
        bool monocle = LayoutOf(Native.MonitorFromWindow(from, 2)) == "monocle";
        if (monocle)
        {
            // every window has the same place: step through them in order instead
            var same = order.Where(placed.ContainsKey).ToList();
            int i = same.IndexOf(from);
            if (same.Count < 2) return null;
            int step = dir is "left" or "up" ? -1 : 1;
            return same[((i < 0 ? 0 : i) + step + same.Count) % same.Count];
        }
        foreach (var (h, q) in placed)
        {
            if (h == from) continue;
            var d = new PointF(q.X + q.Width / 2f - c.X, q.Y + q.Height / 2f - c.Y);
            bool ok = dir switch
            {
                "left" => q.Right <= r.Left + 2 && Overlap(q.Top, q.Bottom, r.Top, r.Bottom),
                "right" => q.Left >= r.Right - 2 && Overlap(q.Top, q.Bottom, r.Top, r.Bottom),
                "up" => q.Bottom <= r.Top + 2 && Overlap(q.Left, q.Right, r.Left, r.Right),
                "down" => q.Top >= r.Bottom - 2 && Overlap(q.Left, q.Right, r.Left, r.Right),
                _ => false,
            };
            if (!ok) continue;
            float dist = d.X * d.X + d.Y * d.Y;
            if (dist < bestD) { bestD = dist; best = h; }
        }
        return best;
    }

    static bool Overlap(int a0, int a1, int b0, int b1) => Math.Min(a1, b1) - Math.Max(a0, b0) > 0;

    static void Focus(IntPtr h)
    {
        if (Native.IsIconic(h)) ShowWindow(h, 9 /* SW_RESTORE */);
        if (!SetForegroundWindow(h))
        {
            // Windows only lets the app the user is using change focus; a tap of Alt counts as using Glyph
            keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero);
            SetForegroundWindow(h);
        }
        BringWindowToTop(h);
    }

    /// After a window was dragged: dropped on another tile = swap them; the first window's edge
    /// dragged = change the share; anything else snaps back.
    void OnDropped(IntPtr h)
    {
        if (!placed.TryGetValue(h, out var was)) { Schedule(); return; }
        Native.GetCursorPos(out var p);
        var mon = Native.MonitorFromWindow(h, 2);
        var target = placed.FirstOrDefault(kv => kv.Key != h && kv.Value.Contains(p.X, p.Y)).Key;
        if (target != IntPtr.Zero && order.IndexOf(h) is int a and >= 0 && order.IndexOf(target) is int b and >= 0)
            (order[a], order[b]) = (order[b], order[a]);
        else if (DwmGetWindowAttribute(h, 9, out Native.RECT now, Marshal.SizeOf<Native.RECT>()) == 0 && now.Width != was.Width && LayoutOf(mon) is "master" or "dwindle")
        {
            var first = order.FirstOrDefault(x => placed.ContainsKey(x) && Native.MonitorFromWindow(x, 2) == mon);
            var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
            if (first == h && Native.GetMonitorInfo(mon, ref mi))
            {
                float areaW = mi.rcWork.Width - 2 * o.OuterGap + o.Gap;
                ratios[mon] = Math.Clamp((now.Width + o.Gap) / areaW, 0.1f, 0.9f);
            }
        }
        Retile();
    }

    void UpdateStatus()
    {
        string s = !enabled ? "off" : LayoutOf(Native.MonitorFromWindow(Native.GetForegroundWindow(), 2));
        if (s != Status) { Status = s; Changed?.Invoke(); }
    }

    public void Dispose() { if (enabled) SetEnabled(false); debounce.Dispose(); recheck.Dispose(); zoomCheck.Dispose(); }

    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out Native.RECT r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
}
