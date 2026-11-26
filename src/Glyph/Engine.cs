using System.Collections.Concurrent;
using System.Diagnostics;
using Glyph.Bar;
using Glyph.Desktop;
using Glyph.Render;

namespace Glyph;

/// The render thread. Draws, for every screen Glyph uses, the desktop (behind the icons) and that screen's bar,
/// plus the launcher. Each surface only redraws when something on it changes or moves, so still themes cost
/// almost nothing. A screen's desktop rests while it can't be seen; bars keep going (they're on top of windows).
internal sealed class Engine
{
    readonly DesktopHost host;
    readonly Thread thread;
    volatile bool stop, reload = true;
    /// Why every screen rests (locked, battery saver), or empty.
    public volatile string PauseReason = "";
    /// Screens (by number) that can't be seen right now, and why (fullscreen app, maximised windows).
    public volatile IReadOnlyDictionary<int, string> Covered = new Dictionary<int, string>();
    public volatile bool UserPaused;
    public volatile int FpsCap;
    public volatile AppSettings CurrentSettings = new();
    /// The main screen's theme (theme sync, window frames, tiling and the launcher use it).
    public volatile Theme? CurrentTheme;
    /// What the app should set up after a reload: a wallpaper window on each of these screens, and a bar where set.
    public volatile IReadOnlyList<(ScreenInfo screen, BarOptions? bar)> Plan = Array.Empty<(ScreenInfo, BarOptions?)>();
    public event Action? Reloaded;
    /// Raised on the render thread when a shortcut should open (the app runs it on the UI thread).
    public event Action<Shortcut>? Launch;
    /// Raised on the render thread for click actions (run on the UI thread by the app).
    public event Action<string>? DoAction;
    /// Where each screen's bar window is (set by the app; by screen number).
    public volatile IReadOnlyDictionary<int, Func<(IntPtr hwnd, int gen, int w, int h)>> BarSources = new Dictionary<int, Func<(IntPtr, int, int, int)>>();
    /// The launcher (set by the app).
    public volatile Glyph.Launcher.LauncherHost? Launcher;
    readonly AutoResetEvent wakeEvent = new(false);
    /// Draw now (e.g. a key was typed in the launcher) instead of at the next frame.
    public void Wake() => wakeEvent.Set();

    readonly ConcurrentQueue<(int x, int y, bool down)> clicks = new();
    readonly ConcurrentQueue<(int x, int y, int delta)> wheels = new();
    double memT;

    /// GLYPH_DEBUG=1: where the memory goes (logged every 15 s).
    static void LogMemory()
    {
        var gi = GC.GetGCMemoryInfo();
        using var me = System.Diagnostics.Process.GetCurrentProcess();
        Log.Write($"memory: private {me.PrivateMemorySize64 >> 20} MB, ws {me.WorkingSet64 >> 20} MB, gc heap {gi.HeapSizeBytes >> 10} KB (committed {gi.TotalCommittedBytes >> 10} KB, frag {gi.FragmentedBytes >> 10} KB), " +
                  $"gen0 budget {gi.GenerationInfo[0].SizeAfterBytes >> 10} KB, jit {System.Runtime.JitInfo.GetCompiledMethodCount()} methods / {System.Runtime.JitInfo.GetCompiledILBytes() >> 10} KB IL, " +
                  $"threads {me.Threads.Count}, assemblies {AppDomain.CurrentDomain.GetAssemblies().Length}: {string.Join(", ", AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name))}");
    }

    /// The mouse wheel over the desktop, in screen pixels.
    public void PostWheel(int x, int y, int delta) => wheels.Enqueue((x, y, delta));

    // stats (read by the tray tooltip)
    public volatile float FrameMs, Fps;

    public Engine(DesktopHost host)
    {
        this.host = host;
        thread = new Thread(Run) { IsBackground = true, Name = "Glyph render", Priority = ThreadPriority.BelowNormal };
    }

    public void Start() => thread.Start();
    public void RequestReload() { reload = true; Wake(); }
    public void Stop() { stop = true; Wake(); thread.Join(3000); }

    /// A left-button press or release over the desktop, in screen pixels.
    public void PostClick(int x, int y, bool down) => clicks.Enqueue((x, y, down));
    /// Mouse events from screen `screen`'s bar window (see BarWindow.Mouse).
    public void PostBarMouse(int screen, int kind, int x, int y, int delta)
    {
        var s = surfaces;
        foreach (var f in s) if (f.S.Number == screen) { f.BarMouse.Enqueue((kind, x, y, delta)); return; }
    }

    static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal) { "SysListView32", "SHELLDLL_DefView", "WorkerW", "Progman" };

    public static bool DesktopAt(int x, int y)
    {
        var h = Native.WindowFromPoint(new Native.POINT { X = x, Y = y });
        if (h == IntPtr.Zero) return false;
        var c = Native.ClassOf(h);
        return DesktopClasses.Contains(c) || Native.GetWindowTextOf(h) == "Glyph wallpaper";
    }

    /// One screen: its desktop and its bar.
    sealed class Surface : IDisposable
    {
        public ScreenInfo S;
        public string ThemeName = "";
        public bool Widgets = true, ShowBar = true;
        public float Scale = 1;
        public readonly Composer Comp = new();
        public readonly BarScene Bar = new();
        // desktop
        public Renderer? R;
        public int Gen = -1, CompGen = -1, LastW, LastH;
        public string FontKey = "", LastSig = "";
        public bool Force = true, PauseTrimmed, IdleTrimmed;
        public RectangleF LastArea;
        public double Last, Time, PausedAt = -1, LastDrawAt;
        // bar
        public Renderer? BarR;
        public readonly Grid BarGrid = new();
        public int BarGen = -1, BarCompGen = -1, BarMouseX = -1, BarMouseY = -1, BarDownItem = -1;
        public string BarFontKey = "", LastBarSig = "";
        public bool BarForce = true;
        public readonly ConcurrentQueue<(int kind, int x, int y, int delta)> BarMouse = new();
        public Surface(ScreenInfo s) { S = s; }
        public void Dispose() { R?.Dispose(); BarR?.Dispose(); R = null; BarR = null; Comp.Dispose(); }
    }

    volatile Surface[] surfaces = Array.Empty<Surface>();
    int mainScreen = -1;
    Native.POINT lastCur;
    double trimAt = -1;

    // ---------------- state of the launcher surface ----------------
    Renderer? lr;
    readonly Grid lGrid = new();
    int lGen = -1, lCompGen = -1;
    string lFontKey = "", lastLSig = "";

    Renderer? AnyRenderer() { foreach (var f in surfaces) { if (f.R != null) return f.R; if (f.BarR != null) return f.BarR; } return lr; }

    /// Works out which screens get which theme, and (re)loads every screen's theme.
    void LoadAll()
    {
        var app = AppSettings.Load(new List<string>());
        Screens.Scaling = app.Monitors.Scaling;
        var plan = app.Monitors.Plan(app.Theme);
        var old = surfaces.ToDictionary(f => f.S.Number);
        var next = new List<Surface>();
        foreach (var (screen, theme, widgets, bar) in plan)
        {
            if (!old.Remove(screen.Number, out var f)) { f = new Surface(screen); Hook(f.Comp); }
            if (f.S.Bounds != screen.Bounds) f.Force = f.BarForce = true;
            f.S = screen; f.ThemeName = theme; f.Widgets = widgets; f.ShowBar = bar;
            f.Scale = Screens.Use(screen.Scale);
            f.Comp.Load(theme, widgets);
            f.Comp.Scale = f.Scale;
            f.Bar.Load(f.Comp.Theme, f.Comp.Problems);
            f.Force = f.BarForce = true;
            next.Add(f);
        }
        foreach (var gone in old.Values) gone.Dispose();
        var main = next.FirstOrDefault(f => f.S.Number == Screens.Pick(next.Select(x => x.S).ToList(), app.Monitors.Main).Number) ?? next.FirstOrDefault();
        mainScreen = main?.S.Number ?? -1;
        // the main screen first: the launcher and the tray use its theme
        if (main != null) { next.Remove(main); next.Insert(0, main); }
        surfaces = next.ToArray();
        CurrentSettings = main?.Comp.App ?? app;
        CurrentTheme = main?.Comp.Theme;
        Plan = next.Select(f => (f.S, f.ShowBar && f.Bar.Enabled ? f.Bar.O : (BarOptions?)null)).ToList();
        if (Launcher is { } lh && main != null) { lh.Scene.Theme = main.Comp.Theme; lh.Scene.O = main.Comp.Theme.Options.Launcher; lastLSig = ""; }
        Log.Write($"screens: {string.Join(", ", next.Select(f => $"{f.S.Number} = {f.ThemeName}{(f.Widgets ? "" : " (no widgets)")}{(f.ShowBar && f.Bar.Enabled ? "" : " (no bar)")}"))} ({app.Monitors.Mode})");
    }

    void Hook(Composer c)
    {
        c.Env.Launch = s => Launch?.Invoke(s);
        c.Env.Do = a => DoAction?.Invoke(a);
        c.BackgroundAction = a => DoAction?.Invoke(a);
    }

    void Run()
    {
        var timer = Native.CreateWaitableTimerExW(IntPtr.Zero, null, Native.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, Native.TIMER_ALL_ACCESS);
        var clock = Stopwatch.StartNew();
        double statT = 0; int statFrames = 0;
        var screenClicks = new List<(int x, int y, bool down)>();
        var screenWheels = new List<(int x, int y, int delta)>();

        while (!stop)
        {
            double now = clock.Elapsed.TotalSeconds;
            int fps = 15;
            bool allResting = true, launcherOpen = false;
            try
            {
                if (reload)
                {
                    reload = false;
                    LoadAll();
                    Reloaded?.Invoke();
                    trimAt = now + 8;
                }

                // mouse: positions are screen pixels; each surface takes the ones on its screen
                screenClicks.Clear(); screenWheels.Clear();
                while (clicks.TryDequeue(out var c)) screenClicks.Add(c);
                while (wheels.TryDequeue(out var wh)) screenWheels.Add(wh);
                Native.GetCursorPos(out var cur);
                bool moved = cur.X != lastCur.X || cur.Y != lastCur.Y;
                lastCur = cur;
                bool overDesk = moved && DesktopAt(cur.X, cur.Y);

                var covered = Covered;
                foreach (var f in surfaces)
                {
                    bool locked = PauseReason.Length > 0;
                    bool desktopPaused = UserPaused || locked || covered.ContainsKey(f.S.Number);
                    bool barPaused = UserPaused || locked || (covered.TryGetValue(f.S.Number, out var why) && why == "fullscreen app");
                    if (!desktopPaused)
                    {
                        allResting = false;
                        var (drew, want) = DesktopStep(f, now, cur, moved, overDesk, screenClicks, screenWheels);
                        fps = Math.Max(fps, want);
                        if (drew) { statFrames++; f.LastDrawAt = now; f.IdleTrimmed = false; }
                        if (f.PauseTrimmed) { f.PauseTrimmed = false; f.Force = true; }
                        f.PausedAt = -1;
                    }
                    else
                    {
                        f.Last = now;
                        // hidden for a few seconds: give back the full-screen buffers and unused memory
                        if (f.PausedAt < 0) f.PausedAt = now;
                        else if (!f.PauseTrimmed && now - f.PausedAt > 3)
                        {
                            f.PauseTrimmed = true;
                            f.R?.Trim();
                            Native.TrimMemory();
                            Log.D($"trimmed memory (screen {f.S.Number} resting)");
                        }
                    }
                    if (!barPaused) { allResting = false; BarStep(f); }
                    else f.BarMouse.Clear();
                    if (!desktopPaused && !f.IdleTrimmed && now - f.LastDrawAt > 60) { f.IdleTrimmed = true; Native.TrimMemory(); Log.D("trimmed memory (idle)"); }
                }
                launcherOpen = surfaces.Length > 0 && LauncherStep(surfaces[0].Comp);

                if (trimAt > 0 && now >= trimAt && (!surfaces.Any(f => f.Comp.Widgets.Any(w => w.Animated)) || now >= trimAt + 20)) { trimAt = -1; Native.TrimMemory(); Log.D("trimmed memory"); }
                if (Log.Debug && now - memT >= 15) { memT = now; LogMemory(); }
                if (now - statT >= 2)
                {
                    Fps = (float)(statFrames / (now - statT)); FrameMs = 0;
                    statT = now; statFrames = 0;
                }
                if ((allResting || surfaces.Length == 0) && !launcherOpen) { wakeEvent.WaitOne(100); continue; }
            }
            catch (Exception e)
            {
                Log.Write("render error: " + e);
                foreach (var f in surfaces)
                {
                    try { f.R?.Dispose(); } catch { }
                    try { f.BarR?.Dispose(); } catch { }
                    f.R = null; f.BarR = null;
                }
                try { lr?.Dispose(); } catch { }
                lr = null;
                Thread.Sleep(1000);
                continue;
            }

            if (FpsCap > 0) fps = Math.Min(fps, FpsCap);
            double wait = now + 1.0 / Math.Max(1, fps) - clock.Elapsed.TotalSeconds;
            if (wait > 0.0005)
            {
                long due = -(long)(wait * 10_000_000);
                if (timer != IntPtr.Zero && Native.SetWaitableTimer(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                    WaitForMultipleObjects(2, new[] { timer, wakeEvent.SafeWaitHandle.DangerousGetHandle() }, false, 1000);
                else wakeEvent.WaitOne((int)(wait * 1000));
            }
        }
        foreach (var f in surfaces) f.Dispose();
        lr?.Dispose();
        if (timer != IntPtr.Zero) Native.CloseHandle(timer);
    }

    /// Returns (drew a frame, frames per second wanted).
    (bool, int) DesktopStep(Surface f, double now, Native.POINT cur, bool moved, bool overDesk, List<(int x, int y, bool down)> clickList, List<(int x, int y, int delta)> wheelList)
    {
        var comp = f.Comp;
        var (hwnd, g) = host.Current(f.S.Number);
        if (hwnd == IntPtr.Zero) return (false, 5);
        if (f.R == null || g != f.Gen)
        {
            f.R?.Dispose(); f.R = null;
            Native.GetClientRect(hwnd, out var rc0);
            f.R = new Renderer(hwnd, rc0.Width, rc0.Height, share: AnyRenderer());
            f.Gen = g; f.FontKey = ""; f.CompGen = -1; f.Force = true;
            if (surfaces.All(x => x == f || x.R == null || x.Gen == g)) host.Acknowledge(g);
        }
        var r = f.R;
        if (comp.Generation != f.CompGen) { r.Canvas.Reset(); f.CompGen = comp.Generation; f.Force = true; }
        var font = Screens.Scaled(comp.Font, f.Scale);
        if (font.Key != f.FontKey)
        {
            comp.RenderProblems.Clear();
            var fp = r.SetFont(font);
            if (fp != null) comp.RenderProblems.Add(fp);
            f.FontKey = font.Key;
            f.Force = true;
        }

        Native.GetClientRect(hwnd, out var rc);
        var o = host.Origin(f.S.Number);
        var wa = Screens.WorkOf(f.S);
        var area = new RectangleF(wa.Left - o.X, wa.Top - o.Y, wa.Width, wa.Height);
        bool resized = rc.Width != f.LastW || rc.Height != f.LastH || area != f.LastArea;
        f.LastW = rc.Width; f.LastH = rc.Height; f.LastArea = area;
        bool here = f.S.Bounds.Contains(cur.X, cur.Y);
        float mx = cur.X - o.X, my = cur.Y - o.Y;
        bool hoverChanged = moved && comp.UpdateHover(mx, my, here && overDesk);
        bool clicked = false;
        foreach (var c in clickList)
        {
            if (!f.S.Bounds.Contains(c.x, c.y)) continue;
            clicked = true;
            if (c.down) comp.MouseDown(c.x - o.X, c.y - o.Y); else comp.MouseUp(c.x - o.X, c.y - o.Y);
        }
        foreach (var wh in wheelList) if (f.S.Bounds.Contains(wh.x, wh.y)) comp.Wheel(wh.x - o.X, wh.y - o.Y, wh.delta);
        if (moved && here && overDesk) comp.AddTrail(mx, my);
        bool selChanged = moved && comp.DragTo(mx, my);
        bool animated = comp.Animated;
        comp.Env.Canvas = r.Canvas;
        var sig = comp.Signature();
        bool changed = sig != f.LastSig;
        f.LastSig = sig;
        bool draw = f.Force || resized || hoverChanged || clicked || animated || changed || selChanged || (moved && here && (comp.Scene.CursorOn || comp.Scene.Parallax));

        int fps = comp.Selecting ? 60 : animated || comp.Scene.CursorOn ? comp.Scene.Fps : 15;
        if (!draw) { f.Last = now; return (false, fps); }

        f.Force = false;
        float dt = (float)Math.Min(0.1, now - f.Last);
        f.Last = now; f.Time += dt;
        r.Resize(rc.Width, rc.Height);
        var atlas = r.Atlas!;
        comp.Compose(r.Width, r.Height, atlas.CellW, atlas.CellH, (float)f.Time, dt, mx, my, area);
        r.Draw(comp.Grid, comp.Effects, (float)f.Time, comp.ClearColor,
            comp.HasBackdrop ? comp.Scaled(comp.DrawBackdrop) : null, comp.HasVectorLayer ? comp.Scaled(comp.DrawWidgets) : null);
        if (!r.Present()) { Log.Write("graphics device lost; rebuilding"); r.Dispose(); f.R = null; }
        return (true, fps);
    }

    void BarStep(Surface f)
    {
        var bar = f.Bar; var comp = f.Comp;
        var src = f.ShowBar && bar.Enabled && BarSources.TryGetValue(f.S.Number, out var s) ? s : null;
        var (bh, bg, bw, bhh) = src != null ? src() : (IntPtr.Zero, 0, 0, 0);
        if (bh == IntPtr.Zero || bw <= 0 || bhh <= 0)
        {
            if (f.BarR != null) { f.BarR.Dispose(); f.BarR = null; }
            f.BarMouse.Clear();
            return;
        }
        if (f.BarR == null || bg != f.BarGen)
        {
            f.BarR?.Dispose(); f.BarR = null;
            f.BarR = new Renderer(bh, bw, bhh, composition: true, share: AnyRenderer());
            f.BarGen = bg; f.BarFontKey = ""; f.BarCompGen = -1; f.BarForce = true;
        }
        var barR = f.BarR;
        if (comp.Generation != f.BarCompGen) { barR.Canvas.Reset(); f.BarCompGen = comp.Generation; f.BarForce = true; }
        var bf = Screens.Scaled(bar.BarFont(), f.Scale);
        if (bf.Key != f.BarFontKey) { barR.SetFont(bf); f.BarFontKey = bf.Key; f.BarForce = true; }
        float vs = comp.Theme.Smooth ? f.Scale : 1;   // smooth bars are laid out in pixels ÷ scale and drawn scaled up
        if (barR.Width != bw || barR.Height != bhh) { barR.Resize(bw, bhh); f.BarForce = true; }
        var atlas = barR.Atlas!;

        while (f.BarMouse.TryDequeue(out var m))
        {
            if (m.kind == 0) { f.BarMouseX = m.x < 0 ? -1 : (int)(m.x / vs); f.BarMouseY = (int)(m.y / vs); continue; }
            int item = bar.HitTest(m.x / vs, m.y / vs);
            switch (m.kind)
            {
                case 1: f.BarDownItem = item; break;
                case 2: if (item >= 0 && item == f.BarDownItem) Fire(bar.ActionAt(item, "left")); f.BarDownItem = -1; break;
                case 3: if (item >= 0) Fire(bar.ActionAt(item, "right")); break;
                case 4: if (item >= 0) Fire(bar.ActionAt(item, m.delta > 0 ? "up" : "down")); break;
            }
        }
        int hover = f.BarMouseX < 0 ? -1 : bar.HitTest(f.BarMouseX, f.BarMouseY);
        if (hover != bar.Hover) { bar.Hover = hover; f.BarForce = true; }

        var sig = bar.Signature(atlas.CellW, atlas.CellH, barR.Canvas);
        if (sig == f.LastBarSig && !f.BarForce) return;
        f.LastBarSig = sig; f.BarForce = false;

        int cols = Math.Max(1, (bw + atlas.CellW - 1) / atlas.CellW), rows = Math.Max(1, (bhh + atlas.CellH - 1) / atlas.CellH);
        f.BarGrid.Resize(cols, rows);
        bar.Layout(vs == 1 ? bw : (int)MathF.Round(bw / vs), vs == 1 ? bhh : (int)MathF.Round(bhh / vs), atlas.CellW, atlas.CellH, barR.Canvas);
        bar.PaintCells(f.BarGrid);
        barR.Draw(f.BarGrid, BarEffects(bar, comp), (float)f.Time, bar.ClearColor, null, ScaledBy(vs, bar.PaintVector));
        if (!barR.Present()) { barR.Dispose(); f.BarR = null; }
    }

    /// Draws the launcher when it changed. Returns whether it's open (then the loop doesn't nap).
    bool LauncherStep(Composer comp)
    {
        var L = Launcher;
        if (L == null) return false;
        var (hwnd, g, w, h, want) = L.Surface();
        if (!want)
        {
            // closed a while: give the memory back
            if (lr != null && Environment.TickCount64 / 1000.0 - L.HiddenAt > 20)
            {
                lr.Dispose(); lr = null; lastLSig = "";
                Glyph.Launcher.IconCache.Clear();
                Native.TrimMemory();
            }
            return false;
        }
        if (lr == null || g != lGen)
        {
            if (lr == null) { lr = new Renderer(hwnd, w, h, composition: true, share: AnyRenderer()); lFontKey = ""; lCompGen = -1; }
            else lr.Resize(w, h);
            lGen = g; lastLSig = "";
        }
        if (comp.Generation != lCompGen) { lr.Canvas.Reset(); lCompGen = comp.Generation; lastLSig = ""; }
        float ls = L.Scale, lvs = comp.Theme.Smooth ? ls : 1;
        var font = Screens.Scaled(comp.Theme.Options.Font, ls);
        if (font.Key != lFontKey) { lr.SetFont(font); lFontKey = font.Key; lastLSig = ""; }
        var st = L.State;
        var sig = st.Signature + "|" + w + "x" + h;
        if (sig == lastLSig) return true;
        lastLSig = sig;
        var atlas = lr.Atlas!;
        var scene = L.Scene;
        lGrid.Resize(Math.Max(1, w / atlas.CellW), Math.Max(1, h / atlas.CellH));
        scene.PaintCells(lGrid, st, atlas.CellW, atlas.CellH);
        lr.Draw(lGrid, new EffectSettings { Enabled = false }, 0, new Rgba(0, 0, 0, 0), null, ScaledBy(lvs, c => scene.PaintVector(c, st, (int)MathF.Round(w / lvs))));
        if (!lr.Present()) { lr.Dispose(); lr = null; return true; }
        L.Drawn(scene.ContentHeight * lvs);
        return true;
    }

    static Action<Canvas> ScaledBy(float s, Action<Canvas> draw) => Math.Abs(s - 1) < 0.001f ? draw : c =>
    {
        var t0 = c.Dc.Transform;
        c.Dc.Transform = System.Numerics.Matrix3x2.CreateScale(s) * t0;
        try { draw(c); } finally { c.Dc.Transform = t0; }
    };

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint WaitForMultipleObjects(uint n, IntPtr[] handles, bool all, uint ms);

    static EffectSettings BarEffects(BarScene bar, Composer comp)
    {
        var t = comp.Effects;
        if (!bar.O.Effects || !t.Enabled) return new EffectSettings { Enabled = false };
        return new EffectSettings
        {
            Enabled = true, Bloom = t.Bloom, BloomThreshold = t.BloomThreshold, BloomRadius = t.BloomRadius, Scanlines = t.Scanlines,
            ScanlineSize = t.ScanlineSize, Flicker = 0, Grain = 0, Vignette = 0, Aberration = 0, Curvature = 0, Brightness = t.Brightness, Contrast = t.Contrast,
        };
    }

    void Fire(string action) { if (action.Length > 0) DoAction?.Invoke(action); }
}
