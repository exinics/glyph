using System.Collections.Concurrent;
using System.Diagnostics;
using Glyph.Bar;
using Glyph.Desktop;
using Glyph.Render;

namespace Glyph;

/// The render thread. Draws two surfaces: the desktop (behind the icons) and the bar.
/// Each only redraws when something on it changes or moves, so still themes cost almost nothing.
/// The desktop rests while it can't be seen; the bar keeps going (it's on top of windows).
internal sealed class Engine
{
    readonly DesktopHost host;
    readonly Thread thread;
    volatile bool stop, reload = true;
    public volatile string PauseReason = "";
    public volatile bool UserPaused;
    public volatile int FpsCap;
    public volatile AppSettings CurrentSettings = new();
    public volatile BarOptions? CurrentBar;
    public volatile Theme? CurrentTheme;
    public event Action? Reloaded;
    /// Raised on the render thread when a shortcut should open (the app runs it on the UI thread).
    public event Action<Shortcut>? Launch;
    /// Raised on the render thread for click actions (run on the UI thread by the app).
    public event Action<string>? DoAction;
    /// Where the bar window is (set by the app; null = no bar).
    public volatile Func<(IntPtr hwnd, int gen, int w, int h)>? BarSource;
    /// The launcher (set by the app).
    public volatile Glyph.Launcher.LauncherHost? Launcher;
    readonly AutoResetEvent wakeEvent = new(false);
    /// Draw now (e.g. a key was typed in the launcher) instead of at the next frame.
    public void Wake() => wakeEvent.Set();

    readonly ConcurrentQueue<(int x, int y, bool down)> clicks = new();
    readonly ConcurrentQueue<(int x, int y, int delta)> wheels = new();
    /// The mouse wheel over the desktop, in screen pixels.
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

    public void PostWheel(int x, int y, int delta) => wheels.Enqueue((x, y, delta));
    readonly ConcurrentQueue<(int kind, int x, int y, int delta)> barMouse = new();

    // stats (read by the tray tooltip)
    public volatile float FrameMs, Fps;

    public Engine(DesktopHost host)
    {
        this.host = host;
        thread = new Thread(Run) { IsBackground = true, Name = "Glyph render", Priority = ThreadPriority.BelowNormal };
    }

    public void Start() => thread.Start();
    public void RequestReload() => reload = true;
    public void Stop() { stop = true; thread.Join(3000); }

    /// A left-button press or release over the desktop, in screen pixels.
    public void PostClick(int x, int y, bool down) => clicks.Enqueue((x, y, down));
    /// Mouse events from the bar window (see BarWindow.Mouse).
    public void PostBarMouse(int kind, int x, int y, int delta) => barMouse.Enqueue((kind, x, y, delta));

    static readonly HashSet<string> DesktopClasses = new(StringComparer.Ordinal) { "SysListView32", "SHELLDLL_DefView", "WorkerW", "Progman" };

    public static bool DesktopAt(int x, int y)
    {
        var h = Native.WindowFromPoint(new Native.POINT { X = x, Y = y });
        if (h == IntPtr.Zero) return false;
        var c = Native.ClassOf(h);
        return DesktopClasses.Contains(c) || Native.GetWindowTextOf(h) == "Glyph wallpaper";
    }

    // ---------------- state of the desktop surface ----------------
    Renderer? r;
    int gen = -1, compGen = -1;
    string fontKey = "", lastSig = "";
    int lastW, lastH; Native.POINT lastCur; bool forceDraw = true; RectangleF lastArea;
    double last, time, trimAt = -1, pausedAt = -1, lastDrawAt;
    bool pauseTrimmed, idleTrimmed;

    // ---------------- state of the bar surface ----------------
    Renderer? barR;
    readonly Grid barGrid = new();
    int barGen = -1, barCompGen = -1, barMouseX = -1, barMouseY = -1, barDownItem = -1;
    string barFontKey = "", lastBarSig = "";
    bool barForce = true;

    // ---------------- state of the launcher surface ----------------
    Renderer? lr;
    readonly Grid lGrid = new();
    int lGen = -1, lCompGen = -1;
    string lFontKey = "", lastLSig = "";

    void Run()
    {
        var timer = Native.CreateWaitableTimerExW(IntPtr.Zero, null, Native.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, Native.TIMER_ALL_ACCESS);
        using var comp = new Composer();
        var bar = new BarScene();
        comp.Env.Launch = s => Launch?.Invoke(s);
        comp.Env.Do = a => DoAction?.Invoke(a);
        comp.BackgroundAction = a => DoAction?.Invoke(a);
        var clock = Stopwatch.StartNew();
        double statT = 0; int statFrames = 0;

        while (!stop)
        {
            double now = clock.Elapsed.TotalSeconds;
            int fps = 15;
            try
            {
                if (reload)
                {
                    reload = false;
                    comp.Load();
                    bar.Load(comp.Theme, comp.Problems);
                    CurrentSettings = comp.App;
                    CurrentBar = bar.Enabled ? bar.O : null;
                    CurrentTheme = comp.Theme;
                    if (Launcher is { } lh) { lh.Scene.Theme = comp.Theme; lh.Scene.O = comp.Theme.Options.Launcher; lastLSig = ""; }
                    Reloaded?.Invoke();
                    forceDraw = barForce = true;
                    trimAt = now + 8;
                }

                bool desktopPaused = UserPaused || PauseReason.Length > 0;
                bool barPaused = UserPaused || PauseReason is "locked" or "fullscreen app";

                if (!desktopPaused)
                {
                    var (drew, want) = DesktopStep(comp, now);
                    fps = want;
                    if (drew) { statFrames++; lastDrawAt = now; idleTrimmed = false; }
                    if (pauseTrimmed) { pauseTrimmed = false; forceDraw = true; }
                    pausedAt = -1;
                }
                else
                {
                    last = now; clicks.Clear(); wheels.Clear();
                    // hidden for a few seconds: give back the full-screen buffers and unused memory
                    if (pausedAt < 0) pausedAt = now;
                    else if (!pauseTrimmed && now - pausedAt > 3)
                    {
                        pauseTrimmed = true;
                        r?.Trim();
                        Native.TrimMemory();
                        Log.D("trimmed memory (desktop paused: " + PauseReason + ")");
                    }
                }

                if (!barPaused) BarStep(bar, comp);
                else barMouse.Clear();
                bool launcherOpen = LauncherStep(comp);

                if (trimAt > 0 && now >= trimAt && (!comp.Widgets.Any(w => w.Animated) || now >= trimAt + 20)) { trimAt = -1; Native.TrimMemory(); Log.D("trimmed memory"); }
                else if (!desktopPaused && !idleTrimmed && now - lastDrawAt > 60) { idleTrimmed = true; Native.TrimMemory(); Log.D("trimmed memory (idle)"); }
                if (Log.Debug && now - memT >= 15) { memT = now; LogMemory(); }
                if (now - statT >= 2)
                {
                    Fps = (float)(statFrames / (now - statT)); FrameMs = 0;
                    statT = now; statFrames = 0;
                }
                if (desktopPaused && barPaused && !launcherOpen) { wakeEvent.WaitOne(100); continue; }
            }
            catch (Exception e)
            {
                Log.Write("render error: " + e);
                try { r?.Dispose(); } catch { }
                try { barR?.Dispose(); } catch { }
                try { lr?.Dispose(); } catch { }
                r = null; barR = null; lr = null;
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
        r?.Dispose();
        barR?.Dispose();
        lr?.Dispose();
        if (timer != IntPtr.Zero) Native.CloseHandle(timer);
    }

    /// Returns (drew a frame, frames per second wanted).
    (bool, int) DesktopStep(Composer comp, double now)
    {
        var (hwnd, g) = host.Current;
        if (hwnd == IntPtr.Zero) return (false, 5);
        if (r == null || g != gen)
        {
            r?.Dispose(); r = null;
            Native.GetClientRect(hwnd, out var rc0);
            r = new Renderer(hwnd, rc0.Width, rc0.Height);
            gen = g; fontKey = ""; compGen = -1; forceDraw = true;
            host.Acknowledge(g);
        }
        if (comp.Generation != compGen) { r.Canvas.Reset(); compGen = comp.Generation; forceDraw = true; }
        if (comp.Font.Key != fontKey)
        {
            comp.RenderProblems.Clear();
            var fp = r.SetFont(comp.Font);
            if (fp != null) comp.RenderProblems.Add(fp);
            fontKey = comp.Font.Key;
            forceDraw = true;
        }

        Native.GetClientRect(hwnd, out var rc);
        var wa = Native.WorkArea();
        var org = host.Origin;
        var area = new RectangleF(wa.Left - org.X, wa.Top - org.Y, wa.Width, wa.Height);
        bool resized = rc.Width != lastW || rc.Height != lastH || area != lastArea;
        lastW = rc.Width; lastH = rc.Height; lastArea = area;
        Native.GetCursorPos(out var cur);
        bool moved = cur.X != lastCur.X || cur.Y != lastCur.Y;
        lastCur = cur;
        var o = host.Origin;
        float mx = cur.X - o.X, my = cur.Y - o.Y;
        bool overDesktop = !moved || DesktopAt(cur.X, cur.Y);
        bool hoverChanged = moved && comp.UpdateHover(mx, my, overDesktop);
        bool clicked = false;
        while (clicks.TryDequeue(out var c))
        {
            clicked = true;
            if (c.down) comp.MouseDown(c.x - o.X, c.y - o.Y); else comp.MouseUp(c.x - o.X, c.y - o.Y);
        }
        while (wheels.TryDequeue(out var wh)) comp.Wheel(wh.x - o.X, wh.y - o.Y, wh.delta);
        if (moved && overDesktop) comp.AddTrail(mx, my);
        bool animated = comp.Animated;
        comp.Env.Canvas = r.Canvas;
        var sig = comp.Signature();
        bool changed = sig != lastSig;
        lastSig = sig;
        bool draw = forceDraw || resized || hoverChanged || clicked || animated || changed || (moved && (comp.Scene.CursorOn || comp.Scene.Parallax));

        int fps = animated || comp.Scene.CursorOn ? comp.Scene.Fps : 15;
        if (!draw) { last = now; return (false, fps); }

        forceDraw = false;
        float dt = (float)Math.Min(0.1, now - last);
        last = now; time += dt;
        r.Resize(rc.Width, rc.Height);
        var atlas = r.Atlas!;
        comp.Compose(r.Width, r.Height, atlas.CellW, atlas.CellH, (float)time, dt, mx, my, area);
        r.Draw(comp.Grid, comp.Effects, (float)time, comp.ClearColor,
            comp.HasBackdrop ? comp.DrawBackdrop : null, comp.HasVectorLayer ? comp.DrawWidgets : null);
        if (!r.Present()) { Log.Write("graphics device lost; rebuilding"); r.Dispose(); r = null; }
        return (true, fps);
    }

    void BarStep(BarScene bar, Composer comp)
    {
        var src = BarSource;
        var (bh, bg, bw, bhh) = src != null && bar.Enabled ? src() : (IntPtr.Zero, 0, 0, 0);
        if (bh == IntPtr.Zero || bw <= 0 || bhh <= 0)
        {
            if (barR != null) { barR.Dispose(); barR = null; }
            barMouse.Clear();
            return;
        }
        if (barR == null || bg != barGen)
        {
            barR?.Dispose(); barR = null;
            barR = new Renderer(bh, bw, bhh, composition: true, share: r);
            barGen = bg; barFontKey = ""; barCompGen = -1; barForce = true;
        }
        if (comp.Generation != barCompGen) { barR.Canvas.Reset(); barCompGen = comp.Generation; barForce = true; }
        var bf = bar.BarFont();
        if (bf.Key != barFontKey) { barR.SetFont(bf); barFontKey = bf.Key; barForce = true; }
        if (barR.Width != bw || barR.Height != bhh) { barR.Resize(bw, bhh); barForce = true; }
        var atlas = barR.Atlas!;

        while (barMouse.TryDequeue(out var m))
        {
            if (m.kind == 0) { barMouseX = m.x; barMouseY = m.y; continue; }
            int item = bar.HitTest(m.x, m.y);
            switch (m.kind)
            {
                case 1: barDownItem = item; break;
                case 2: if (item >= 0 && item == barDownItem) Fire(bar.ActionAt(item, "left")); barDownItem = -1; break;
                case 3: if (item >= 0) Fire(bar.ActionAt(item, "right")); break;
                case 4: if (item >= 0) Fire(bar.ActionAt(item, m.delta > 0 ? "up" : "down")); break;
            }
        }
        int hover = barMouseX < 0 ? -1 : bar.HitTest(barMouseX, barMouseY);
        if (hover != bar.Hover) { bar.Hover = hover; barForce = true; }

        var sig = bar.Signature(atlas.CellW, atlas.CellH, barR.Canvas);
        if (sig == lastBarSig && !barForce) return;
        lastBarSig = sig; barForce = false;

        int cols = Math.Max(1, (bw + atlas.CellW - 1) / atlas.CellW), rows = Math.Max(1, (bhh + atlas.CellH - 1) / atlas.CellH);
        barGrid.Resize(cols, rows);
        bar.Layout(bw, bhh, atlas.CellW, atlas.CellH, barR.Canvas);
        bar.PaintCells(barGrid);
        barR.Draw(barGrid, BarEffects(bar, comp), (float)time, bar.ClearColor, null, bar.PaintVector);
        if (!barR.Present()) { barR.Dispose(); barR = null; }
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
            if (lr == null) { lr = new Renderer(hwnd, w, h, composition: true, share: r ?? barR); lFontKey = ""; lCompGen = -1; }
            else lr.Resize(w, h);
            lGen = g; lastLSig = "";
        }
        if (comp.Generation != lCompGen) { lr.Canvas.Reset(); lCompGen = comp.Generation; lastLSig = ""; }
        var font = comp.Theme.Options.Font;
        if (font.Key != lFontKey) { lr.SetFont(font); lFontKey = font.Key; lastLSig = ""; }
        var st = L.State;
        var sig = st.Signature + "|" + w + "x" + h;
        if (sig == lastLSig) return true;
        lastLSig = sig;
        var atlas = lr.Atlas!;
        var scene = L.Scene;
        lGrid.Resize(Math.Max(1, w / atlas.CellW), Math.Max(1, h / atlas.CellH));
        scene.PaintCells(lGrid, st, atlas.CellW, atlas.CellH);
        lr.Draw(lGrid, new EffectSettings { Enabled = false }, (float)time, new Rgba(0, 0, 0, 0), null, c => scene.PaintVector(c, st, w));
        if (!lr.Present()) { lr.Dispose(); lr = null; return true; }
        L.Drawn(scene.ContentHeight);
        return true;
    }

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
