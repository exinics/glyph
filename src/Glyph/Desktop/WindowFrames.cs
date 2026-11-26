using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Glyph.Options;
using Glyph.Render;

namespace Glyph.Desktop;

/// theme.toml [windows]: frames (from frames.toml) drawn around real windows.
internal sealed class WindowFrameOptions
{
    [Description("Which windows get a frame. off = none. focused = the window you're using. tiled = every window tiling arranges, and the one you're using."),
     Choices("off", "focused", "tiled"), TypeConverter(typeof(ChoicesConverter))]
    public string Mode { get; set; } = "off";
    [Description("The frame (Frames page) for the window you're using."), TypeConverter(typeof(FrameNameConverter))] public string Frame { get; set; } = "none";
    [DisplayName("Other windows' frame"), Description("tiled: the frame for the other windows. auto = the same frame, none = no frame on them."), TypeConverter(typeof(FrameNameConverter))] public string InactiveFrame { get; set; } = "auto";
    [Description("The frame's title (in a tab or strip, if the frame has one). {process} = the program (e.g. WindowsTerminal), {title} = the window's title, {n} = the window's number. Filters work, e.g. {process|map:WindowsTerminal=TTY|upper}.{n:00}")]
    public string Title { get; set; } = "{process|upper}";
    [Description("Pixels between the window and its frame."), Range(0, 50)] public int Gap { get; set; } = 2;
    [DisplayName("Hide window borders"), Description("Hide Windows' own thin border on framed windows.")] public bool HideBorder { get; set; } = true;
    [DisplayName("Square corners"), Description("Make framed windows square-cornered (Windows rounds them).")] public bool SquareCorners { get; set; } = true;
    [DisplayName("Room for frames when tiling"), Description("Tiling leaves space around each window for its frame (e.g. a title tab above it).")] public bool TilingRoom { get; set; } = true;
}

/// Draws a theme's window frames around windows: four thin click-through layered windows per framed window
/// (top, bottom, left, right), drawn in software and only redrawn when the window changes size or focus.
internal sealed class WindowFrames : IDisposable
{
    readonly Func<Theme?> theme;
    readonly Func<IReadOnlyCollection<IntPtr>> tiled;
    WindowFrameOptions o = new();
    Theme? t;
    readonly Dictionary<IntPtr, Set> sets = new();
    readonly Dictionary<IntPtr, int> numbers = new();
    readonly List<IntPtr> hooks = new();
    readonly WinEventProc proc;
    readonly System.Windows.Forms.Timer poll = new() { Interval = 200 };
    readonly System.Windows.Forms.Timer soon = new() { Interval = 15 };
    SoftCanvas? soft;
    IntPtr focused, moving;
    bool on, restack = true;
    static readonly uint ownPid = (uint)Environment.ProcessId;
    readonly uint uiThread = GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    /// Space tiling should leave around each window (left, top, right, bottom), or null.
    public static (int l, int t, int r, int b)? Room { get; private set; }

    /// Room on a given display (frames are drawn bigger on scaled-up screens), or null.
    public static (int l, int t, int r, int b)? RoomFor(IntPtr monitor)
    {
        if (Room is not { } r) return null;
        float s = Screens.Use(Screens.ScaleOf(monitor));
        return ((int)MathF.Ceiling(r.l * s), (int)MathF.Ceiling(r.t * s), (int)MathF.Ceiling(r.r * s), (int)MathF.Ceiling(r.b * s));
    }

    sealed class Set
    {
        public readonly Strip[] Strips = { new(), new(), new(), new() };
        public Rectangle Win;           // the window's visible frame last drawn for
        public string Key = "";         // what was drawn (size, focus, title)
        public bool Shown, Ordered;
    }

    sealed class Strip : NativeWindow
    {
        public Rectangle R;
        public Strip()
        {
            CreateHandle(new CreateParams
            {
                Caption = "Glyph window frame", Style = unchecked((int)0x80000000) /* WS_POPUP */,
                ExStyle = 0x80000 | 0x20 | 0x80 | 0x08000000,   // layered, transparent (click-through), tool window, no activate
            });
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) { m.Result = (IntPtr)(-1); return; }   // WM_NCHITTEST: click-through
            base.WndProc(ref m);
        }
    }

    public WindowFrames(Func<Theme?> theme, Func<IReadOnlyCollection<IntPtr>> tiled)
    {
        this.theme = theme; this.tiled = tiled;
        proc = OnEvent;
        poll.Tick += (_, _) => Update();
        soon.Tick += (_, _) => { soon.Stop(); Update(); };
    }

    public void Apply(Theme? th)
    {
        t = th;
        o = th?.Options.Windows ?? new WindowFrameOptions();
        bool want = th != null && th.Smooth && o.Mode != "off" && th.Frame(o.Frame) != null;
        if (want && th != null)
        {
            var f = th.Frame(o.Frame)!;
            var s = Space(f);
            Room = o.TilingRoom ? (s.l, s.t, s.r, s.b) : null;
        }
        else Room = null;
        if (want != on)
        {
            on = want;
            if (on)
            {
                Hook(0x0003, 0x0003); Hook(0x000A, 0x000B); Hook(0x0016, 0x0017); Hook(0x8001, 0x8003); Hook(0x8017, 0x8018);
                poll.Start();
                soft ??= new SoftCanvas();
                Log.Write("window frames on (" + o.Mode + ")");
            }
            else
            {
                foreach (var h in hooks) UnhookWinEvent(h);
                hooks.Clear();
                poll.Stop(); soon.Stop();
                Clear();
                soft?.Dispose(); soft = null;
                Log.Write("window frames off");
            }
        }
        foreach (var s in sets.Values) s.Key = "";   // redraw with the new look
        if (on) Kick();
    }

    /// Something moved (e.g. tiling placed windows): catch up soon. (Never stop/start a WinForms timer for each
    /// event: each start makes a hidden window and each stop destroys it, which is itself a window event.)
    public void Kick() { if (on && !soon.Enabled) soon.Start(); }

    void Hook(uint min, uint max)
    {
        var h = SetWinEventHook(min, max, IntPtr.Zero, proc, 0, 0, 0);
        if (h != IntPtr.Zero) hooks.Add(h);
    }

    void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 || idChild != 0 || hwnd == IntPtr.Zero || thread == uiThread) return;   // not Glyph's own windows
        if (ev == 0x000A) { moving = hwnd; poll.Interval = 16; }
        else if (ev == 0x000B) { moving = IntPtr.Zero; poll.Interval = 200; }
        else if (ev == 0x0003 || ev == 0x0017) restack = true;   // focus changed or a window came back
        else if (ev >= 0x8001 && !sets.ContainsKey(hwnd) && hwnd != focused) return;   // shown / hidden / closed: only ours matter
        Kick();
    }

    // ───────────── which windows ─────────────

    static readonly HashSet<string> SystemClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "ApplicationManager_ImmersiveShellWindow", "ForegroundStaging", "TopLevelWindowForOverflowXamlIsland", "NotifyIconOverflowWindow",
        "MultitaskingViewFrame", "TaskSwitcherWnd", "Windows.Internal.Shell.TabProxyWindow", "LockScreenControllerProxyWindow",
    };

    static bool Normal(IntPtr h)
    {
        if (h == IntPtr.Zero || !Native.IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero || Native.IsCloaked(h)) return false;
        int style = Native.GetWindowLong(h, Native.GWL_STYLE), ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
        if ((style & 0x40000000) != 0 || (ex & Native.WS_EX_TOOLWINDOW) != 0 || (ex & Native.WS_EX_NOACTIVATE) != 0) return false;
        if ((style & 0x00C00000) != 0x00C00000) return false;
        if (SystemClasses.Contains(Native.ClassOf(h))) return false;
        if (Native.GetWindowTextOf(h).Length == 0) return false;
        GetWindowThreadProcessId(h, out uint pid);
        return pid != ownPid;
    }

    /// Visible, not minimised, not maximised or full screen (no room for a frame then).
    static bool Showable(IntPtr h, out Rectangle r)
    {
        r = default;
        if (!Native.IsWindow(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h) || Native.IsZoomed(h) || Native.IsCloaked(h)) return false;
        if (DwmGetWindowAttribute(h, 9, out Native.RECT fr, Marshal.SizeOf<Native.RECT>()) != 0 && !Native.GetWindowRect(h, out fr)) return false;
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (Native.GetMonitorInfo(Native.MonitorFromWindow(h, 2), ref mi))
        {
            var m = mi.rcMonitor;
            if (fr.Left <= m.Left && fr.Top <= m.Top && fr.Right >= m.Right && fr.Bottom >= m.Bottom) return false;
        }
        r = Rectangle.FromLTRB(fr.Left, fr.Top, fr.Right, fr.Bottom);
        return r.Width > 40 && r.Height > 30;
    }

    // ───────────── updating ─────────────

    string lastError = "";

    void Update()
    {
        try { UpdateNow(); }
        catch (Exception e) { var m = e.ToString(); if (m != lastError) { lastError = m; Log.Write("window frames: " + m); } }
    }

    void UpdateNow()
    {
        if (!on || t == null || soft == null) return;
        var fg = Native.GetForegroundWindow();
        if (Normal(fg)) focused = fg;
        else if (focused != IntPtr.Zero && !Native.IsWindow(focused)) focused = IntPtr.Zero;

        var want = new List<IntPtr>();
        if (o.Mode == "tiled") foreach (var h in tiled()) if (Native.IsWindow(h)) want.Add(h);
        if (focused != IntPtr.Zero && !want.Contains(focused)) want.Add(focused);

        foreach (var h in sets.Keys.Where(h => !want.Contains(h)).ToList()) Remove(h);
        foreach (var h in numbers.Keys.Where(h => !Native.IsWindow(h)).ToList()) numbers.Remove(h);

        bool order = restack; restack = false;
        var active = t.Frame(o.Frame);
        var idle = o.InactiveFrame == "auto" ? active : t.Frame(o.InactiveFrame);
        foreach (var h in want)
        {
            var f = h == focused ? active : idle;
            if (f == null || !Showable(h, out var win)) { if (sets.TryGetValue(h, out var gone)) HideSet(gone); continue; }
            if (!sets.TryGetValue(h, out var s)) { s = new Set(); sets[h] = s; Style(h, true); order = true; Log.D($"window frame on \"{Native.GetWindowTextOf(h)}\""); }
            if (!numbers.ContainsKey(h)) { int n = 1; while (numbers.ContainsValue(n)) n++; numbers[h] = n; }
            string title = TitleFor(h);
            float scale = Screens.Use(Screens.ScaleOf(Native.MonitorFromWindow(h, 2)));
            string key = $"{win.Width}x{win.Height}|{f.Name}|{title}|{scale}";
            if (key != s.Key) { Draw(s, f, win, title, scale); s.Key = key; s.Win = win; }
            else if (win.Location != s.Win.Location) { MoveSet(s, win.X - s.Win.X, win.Y - s.Win.Y); s.Win = win; }
            if (order || !s.Ordered) { Order(h, s); s.Ordered = true; }
        }
    }

    string TitleFor(IntPtr h)
    {
        GetWindowThreadProcessId(h, out uint pid);
        string procName = ProcessName(pid), title = Native.GetWindowTextOf(h);
        int n = numbers.TryGetValue(h, out var k) ? k : 1;
        return Glyph.Data.Fmt.Text(o.Title, (name, spec) => name switch
        {
            "process" => procName,
            "title" => Glyph.Data.Fmt.Trunc(title, spec),
            "n" => n.ToString(spec.Length > 0 ? spec : "0", System.Globalization.CultureInfo.InvariantCulture),
            _ => Glyph.Data.Stats.Value(name, spec),
        });
    }

    static readonly Dictionary<uint, string> procNames = new();
    static string ProcessName(uint pid)
    {
        if (procNames.TryGetValue(pid, out var n)) return n;
        try { using var p = Process.GetProcessById((int)pid); n = p.ProcessName; } catch { n = ""; }
        if (procNames.Count > 300) procNames.Clear();
        return procNames[pid] = n;
    }

    /// The space a frame takes around a window: its border, the gap, a tab or title above, brackets…
    (int l, int t, int r, int b) Space(FrameOptions f)
    {
        float bw = f.Border != "none" ? f.BorderWidth : 0;
        float above = FramePainter.Above(f, t!, t!.Options.Ui.FontSize, true);
        var outs = FramePainter.Outside(f);
        float g = o.Gap + bw;
        float bar = f.Header == "bar" ? FramePainter.StripH(f, t, t.Options.Ui.FontSize) : 0;
        return ((int)MathF.Ceiling(g + outs.L), (int)MathF.Ceiling(g + above + bar + outs.T), (int)MathF.Ceiling(g + outs.R), (int)MathF.Ceiling(g + outs.B));
    }

    /// Draws the frame in pixels ÷ `sc` (so it's sized like everything else on a scaled display), scaled up.
    void Draw(Set s, FrameOptions f, Rectangle win, string title, float sc = 1)
    {
        var winL = new RectangleF(win.X / sc, win.Y / sc, win.Width / sc, win.Height / sc);
        float bw = f.Border != "none" ? f.BorderWidth : 0;
        float above = FramePainter.Above(f, t!, t!.Options.Ui.FontSize, title.Length > 0);
        float bar = f.Header == "bar" && title.Length > 0 ? FramePainter.StripH(f, t, t.Options.Ui.FontSize) : 0;
        var outs = FramePainter.Outside(f);
        float g = o.Gap + bw;
        // the frame's box sits around the window (a title strip above it); the strips cover everything outside the window
        var box = RectangleF.FromLTRB(winL.Left - g, winL.Top - g - bar, winL.Right + g, winL.Bottom + g);
        var outer = RectangleF.FromLTRB(box.Left, box.Top - above, box.Right, box.Bottom);
        var all = Rectangle.FromLTRB((int)MathF.Floor((outer.Left - outs.L - 1) * sc), (int)MathF.Floor((outer.Top - outs.T - 1) * sc), (int)MathF.Ceiling((outer.Right + outs.R + 1) * sc), (int)MathF.Ceiling((outer.Bottom + outs.B + 1) * sc));
        var rects = new[]
        {
            Rectangle.FromLTRB(all.Left, all.Top, all.Right, win.Top),
            Rectangle.FromLTRB(all.Left, win.Bottom, all.Right, all.Bottom),
            Rectangle.FromLTRB(all.Left, win.Top, win.Left, win.Bottom),
            Rectangle.FromLTRB(win.Right, win.Top, all.Right, win.Bottom),
        };
        for (int i = 0; i < 4; i++)
        {
            var st = s.Strips[i]; var r = rects[i];
            st.R = r;
            if (r.Width <= 0 || r.Height <= 0) { Native.ShowWindow(st.Handle, 0); continue; }
            var c = soft!.Begin(r.Width, r.Height);
            c.Dc.Transform = Matrix3x2.CreateScale(sc) * Matrix3x2.CreateTranslation(-r.X, -r.Y);
            try { FramePainter.Draw(c, f, t, outer, title, "", t.Options.Ui.FontSize, false, fill: false); }
            catch (Exception e) { Log.D("window frame: " + e.Message); }
            soft.End();
            soft.UpdateLayered(st.Handle, r.X, r.Y);
            if (!s.Shown || !Native.IsWindowVisible(st.Handle)) Native.ShowWindow(st.Handle, 4 /* SW_SHOWNOACTIVATE */);
        }
        s.Shown = true;
    }

    void MoveSet(Set s, int dx, int dy)
    {
        foreach (var st in s.Strips)
        {
            if (st.R.Width <= 0 || st.R.Height <= 0) continue;
            st.R.Offset(dx, dy);
            SetWindowPos(st.Handle, IntPtr.Zero, st.R.X, st.R.Y, 0, 0, 0x0001 | 0x0004 | 0x0010 | 0x0200);   // NOSIZE NOZORDER NOACTIVATE NOOWNERZORDER
        }
    }

    /// Puts the strips just above their window, so windows on top of it also cover its frame. Done when focus
    /// changes, not on every check: hidden helper windows sit above many windows, and stacking under them again
    /// and again kept Windows busy.
    void Order(IntPtr h, Set s)
    {
        // the nearest visible window above it that isn't a frame strip
        var above = GetWindow(h, 3 /* GW_HWNDPREV */);
        while (above != IntPtr.Zero && (!Native.IsWindowVisible(above) || Native.ClassOf(above).Length == 0 || Native.GetWindowTextOf(above) == "Glyph window frame"))
            above = GetWindow(above, 3);
        var after = above == IntPtr.Zero || (Native.GetWindowLong(above, Native.GWL_EXSTYLE) & 0x8) != 0 ? IntPtr.Zero /* HWND_TOP */ : above;
        foreach (var st in s.Strips)
            SetWindowPos(st.Handle, after, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0200);   // NOSIZE NOMOVE NOACTIVATE NOOWNERZORDER
    }

    void HideSet(Set s)
    {
        if (!s.Shown) return;
        foreach (var st in s.Strips) Native.ShowWindow(st.Handle, 0);
        s.Shown = false; s.Key = "";
    }

    void Remove(IntPtr h)
    {
        if (!sets.TryGetValue(h, out var s)) return;
        foreach (var st in s.Strips) st.DestroyHandle();
        sets.Remove(h);
        if (Native.IsWindow(h)) Style(h, false);
    }

    void Style(IntPtr h, bool framed)
    {
        if (o.HideBorder || !framed) { int c = framed ? unchecked((int)0xFFFFFFFE) : unchecked((int)0xFFFFFFFF); DwmSetWindowAttribute(h, 34, ref c, 4); }
        if (o.SquareCorners || !framed) { int c = framed ? 1 : 0; DwmSetWindowAttribute(h, 33, ref c, 4); }
    }

    void Clear() { foreach (var h in sets.Keys.ToList()) Remove(h); }

    public void Dispose()
    {
        foreach (var h in hooks) UnhookWinEvent(h);
        hooks.Clear();
        poll.Dispose(); soon.Dispose();
        Clear();
        soft?.Dispose();
        Room = null;
    }

    delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out Native.RECT value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
}
