using System.Diagnostics;
using Glyph.Bar;
using Glyph.Render;

namespace Glyph.Ui;

/// Renders a theme off screen (desktop + bar, at the real screen size) on its own thread, for the editor.
/// Also reports where every widget and bar module is, so they can be clicked and dragged.
internal sealed class Preview : IDisposable
{
    public sealed class Info
    {
        public int W, H;
        public RectangleF Area;
        public List<(int fileIndex, RectangleF r, string label)> Widgets = new();
        public List<(int fileIndex, Glyph.Widgets.Part part)> Parts = new();
        public bool BarOn;
        public RectangleF BarRect;
        public List<(BarModuleOptions m, RectangleF r)> Modules = new();
        public List<string> Problems = new();
    }

    readonly Thread thread;
    volatile bool stop, reload = true, dirty = true, replay;
    volatile string theme = "";
    public volatile bool Animate;
    PointF mouse = new(-1e6f, -1e6f);
    readonly object gate = new();
    readonly AutoResetEvent wake = new(false);
    /// Raised on the preview thread with a finished frame. The bitmap stays valid until the next frame after the one after.
    public event Action<Bitmap, Info>? Frame;
    readonly Bitmap[] buffers = new Bitmap[2];
    int which;

    public Preview(string themeName)
    {
        theme = themeName;
        thread = new Thread(Run) { IsBackground = true, Name = "Glyph preview", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    public string Theme { get => theme; set { theme = value; Reload(); } }
    public void Reload() { reload = true; wake.Set(); }
    public void Redraw() { dirty = true; wake.Set(); }
    /// Plays the theme's appear / loop animations again.
    public void Replay() { replay = true; dirty = true; wake.Set(); }
    public void SetMouse(PointF p) { lock (gate) mouse = p; if (Animate) wake.Set(); }

    static RectangleF TaskbarCut(Rectangle screen)
    {
        var tb = Glyph.Data.Tray.ExplorerTray();
        if (tb == IntPtr.Zero || !Native.IsWindowVisible(tb) || !Native.GetWindowRect(tb, out var r)) return RectangleF.Empty;
        var rr = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        rr.Intersect(screen);
        return rr.Width > 0 && rr.Height > 0 && rr.Height < screen.Height / 3 ? rr : RectangleF.Empty;
    }

    void Run()
    {
        var screen = Screen.PrimaryScreen!.Bounds;
        int W = screen.Width, H = screen.Height;
        buffers[0] = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        buffers[1] = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        Renderer? desk = null, barR = null;
        Bitmap? barBmp = null;
        var comp = new Composer();
        var bar = new BarScene();
        var barGrid = new Grid();
        string fontKey = "", barFontKey = "";
        var clock = Stopwatch.StartNew();
        double last = 0, time = 0;
        try
        {
            desk = new Renderer(IntPtr.Zero, W, H);
            while (!stop)
            {
                if (reload)
                {
                    reload = false;
                    try { comp.Load(theme); bar.Load(comp.Theme, comp.Problems); }
                    catch (Exception e) { Log.Write("preview load: " + e.Message); }
                    desk.Canvas.Reset(); barR?.Canvas.Reset();
                    dirty = true;
                }
                if (replay) { replay = false; comp.ReplayAnimations(); }
                bool loading = comp.Widgets.Any(w => w.Animated) || comp.Anim.Running;   // e.g. shortcut icons still coming in, tweens playing
                if (!dirty && !loading && !Animate) { wake.WaitOne(500); continue; }
                dirty = false;

                if (comp.Font.Key != fontKey) { desk.SetFont(comp.Font); fontKey = comp.Font.Key; }
                double now = clock.Elapsed.TotalSeconds;
                float dt = (float)Math.Min(0.1, now - last); last = now; time += dt;

                // the usable area: screen minus the taskbar and the bar's strip
                var area = new RectangleF(0, 0, W, H);
                var tb = TaskbarCut(screen);
                if (!tb.IsEmpty) { if (tb.Y <= 0) { area.Y += tb.Height; area.Height -= tb.Height; } else area.Height -= tb.Height; }
                var info = new Info { W = W, H = H, Problems = comp.Problems.ToList() };
                var bo = bar.O;
                if (bar.Enabled)
                {
                    info.BarOn = true;
                    (info.BarRect, area) = bo.PlaceIn(area);
                }
                info.Area = area;

                PointF m; lock (gate) m = Animate ? mouse : new PointF(-1e6f, -1e6f);
                var atlas = desk.Atlas!;
                comp.Env.Canvas = desk.Canvas;
                comp.UpdateHover(m.X, m.Y, m.X > -1e5f);   // hover looks and hover tweens (only with Animate on)
                comp.Compose(W, H, atlas.CellW, atlas.CellH, (float)time, dt, m.X, m.Y, area);
                desk.Draw(comp.Grid, comp.Effects, (float)time, comp.ClearColor, comp.HasBackdrop ? comp.DrawBackdrop : null, comp.HasVectorLayer ? comp.DrawWidgets : null);
                var target = buffers[which];
                which ^= 1;
                lock (target) desk.ReadBackInto(target);
                foreach (var w in comp.Widgets)
                {
                    if (w.Bounds.IsEmpty) continue;
                    info.Widgets.Add((w.FileIndex, w.Bounds, w.GetType().Name.Replace("Widget", "").ToLowerInvariant()));
                    try { foreach (var pt in w.Parts()) if (!pt.Rect.IsEmpty) info.Parts.Add((w.FileIndex, pt)); } catch { }
                }

                if (info.BarOn)
                {
                    int bw = (int)info.BarRect.Width, bh = (int)info.BarRect.Height;
                    if (barR == null || barR.Width != bw || barR.Height != bh)
                    {
                        barR?.Dispose(); barBmp?.Dispose();
                        barR = new Renderer(IntPtr.Zero, bw, bh, share: desk);
                        barBmp = new Bitmap(bw, bh, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        barFontKey = "";
                    }
                    var bf = bar.BarFont();
                    if (bf.Key != barFontKey) { barR.SetFont(bf); barFontKey = bf.Key; }
                    var ba = barR.Atlas!;
                    barGrid.Resize((bw + ba.CellW - 1) / ba.CellW, (bh + ba.CellH - 1) / ba.CellH);
                    bar.Signature(ba.CellW, ba.CellH, barR.Canvas);
                    bar.Layout(bw, bh, ba.CellW, ba.CellH, barR.Canvas);
                    bar.PaintCells(barGrid);
                    barR.Draw(barGrid, new EffectSettings { Enabled = false }, (float)time, bar.ClearColor, null, bar.PaintVector);
                    barR.ReadBackInto(barBmp!);
                    lock (target) using (var g = Graphics.FromImage(target)) g.DrawImage(barBmp!, info.BarRect.X, info.BarRect.Y, bw, bh);
                    foreach (var (mo, r) in bar.ModuleRects()) info.Modules.Add((mo, new RectangleF(r.X + info.BarRect.X, r.Y + info.BarRect.Y, r.Width, r.Height)));
                }
                Frame?.Invoke(target, info);
                if (comp.Anim.Running) Thread.Sleep(30);   // tweens: smooth enough to judge them
                else if (Animate) Thread.Sleep(60); else if (loading) Thread.Sleep(120);   // playing videos: a lighter preview
            }
        }
        catch (Exception e) { Log.Write("preview: " + e); }
        finally { barR?.Dispose(); desk?.Dispose(); barBmp?.Dispose(); comp.Dispose(); }
    }

    public void Dispose()
    {
        stop = true; wake.Set();
        thread.Join(2000);
    }
}
