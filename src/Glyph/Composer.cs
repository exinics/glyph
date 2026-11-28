using Glyph.Render;
using Glyph.Wallpaper;
using Glyph.Widgets;

namespace Glyph;

/// Loads the active theme and composes each frame: backdrop, wallpaper layers (characters),
/// widgets (characters or smooth shapes) and the problems box. Also handles mouse hover and clicks.
/// Shared by the live desktop and snapshots.
internal sealed class Composer : IDisposable
{
    public AppSettings App = new();
    public Theme Theme = new("none");
    public WallpaperScene Scene = new();
    public readonly List<Widget> Widgets = new();
    public List<string> Problems = new();
    public readonly List<string> RenderProblems = new();
    public readonly Grid Grid = new();
    readonly Frame frame = new();
    public readonly WidgetEnv Env = new();
    /// Display scale (1 = 100%). Smooth themes lay out and draw widgets in pixels ÷ this, drawn scaled up;
    /// terminal themes use a bigger font instead (set by the engine).
    public float Scale = 1;
    float VS => Theme.Smooth ? Scale : 1;
    public int Generation;          // bumps on every load (so caches know to refresh)
    /// The theme's tweens (animations.toml), played on the widgets.
    public readonly Animator Anim = new();
    readonly HashSet<Widget> over = new();   // widgets the mouse is over (for hover tweens)

    public FontOptions Font => Theme.Options.Font;
    public EffectSettings Effects => Theme.Options.Effects;

    /// Loads the theme in use (or `themeOverride`, for the editor's preview).
    public void Load(string? themeOverride = null, bool withWidgets = true)
    {
        var problems = new List<string>();
        var app = AppSettings.Load(problems);
        if (themeOverride != null) app.Theme = themeOverride;
        if (!Directory.Exists(Paths.ThemeDir(app.Theme)))
        {
            var first = Directory.Exists(Paths.Themes) ? Directory.GetDirectories(Paths.Themes).Select(Path.GetFileName).FirstOrDefault() : null;
            problems.Add($"glyph.toml: theme \"{app.Theme}\" not found in config\\themes" + (first != null ? $"; showing \"{first}\"" : ""));
            if (first != null) app.Theme = first;
        }
        var theme = Theme.Load(app.Theme, problems);
        Glyph.Render.Fonts.Refresh();   // font files added to or taken out of config\fonts
        var ui = theme.Options.Ui;
        Glyph.Render.Symbols.Style = ui.IconStyle; Glyph.Render.Symbols.Fill = ui.IconFill; Glyph.Render.Symbols.Scale = ui.IconSize;
        Glyph.Render.Symbols.EnsureStyle(ui.IconStyle);
        Glyph.Data.Variables.Use(Glyph.Data.Variables.LoadList(theme.Name, problems));
        var scene = WallpaperScene.Load(theme, problems);
        var shortcuts = ShortcutStore.Load(problems);
        var widgets = new List<Widget>();
        var wlist = withWidgets ? WidgetsFile.Load(theme, problems).Widgets : new List<WidgetOptions>();
        var tweens = withWidgets ? AnimationsFile.Load(theme, problems).Tweens : new List<TweenOptions>();
        for (int k = 0; k < wlist.Count; k++)
        {
            var o = wlist[k];
            if (!o.Enabled) continue;
            try { var w = Widget.Create(o, theme, shortcuts, problems); w.FileIndex = k; widgets.Add(w); }
            catch (Exception e) { problems.Add($"widgets.toml: {o.Type}: {e.Message}"); }
        }

        Scene.Dispose();
        foreach (var w in Widgets) w.Dispose();
        Widgets.Clear();
        App = app; Theme = theme; Scene = scene; Problems = problems;
        Widgets.AddRange(widgets);
        over.Clear();
        Anim.Bind(tweens, Widgets);
        Env.Painter.Reset();
        Env.Theme = theme;
        Generation++;
        foreach (var p in problems) Log.Write("config: " + p);
        if (problems.Count == 0) Log.Write($"loaded theme \"{theme.Name}\", no problems");
    }

    /// True while something on screen moves by itself (so frames must keep coming).
    public bool Animated => Scene.Animated || Widgets.Any(w => w.Animated) || ripples.Count > 0 || trail.Count > 0 || (Theme.Smooth && Anim.Running);
    /// Something drawn with the widgets (smooth layer) is moving: they can't be reused from the last frame.
    public bool OverlayMoving => Widgets.Any(w => w.Animated) || ripples.Count > 0 || trail.Count > 0 || (Theme.Smooth && Anim.Running) || selecting;
    /// False = the widgets are known not to have changed: skip laying them out (smooth themes).
    public bool LayoutWidgets { get; set; } = true;

    /// Plays the appear and loop animations again from the start (the editor's Play button).
    public void ReplayAnimations() => Anim.Restart();

    // ---------------- background interaction: ripples, trail, actions ----------------

    readonly List<(float x, float y, float t)> ripples = new();
    readonly List<(float x, float y, float t)> trail = new();
    float lastTime;
    public Action<string>? BackgroundAction;
    InteractionOptions I => Scene.Options.Interaction;

    public void AddTrail(float x, float y)
    {
        if (!I.Trail) return;
        x /= VS; y /= VS;
        trail.Add((x, y, lastTime));
        if (trail.Count > 400) trail.RemoveAt(0);
    }

    /// The mouse wheel over empty desktop.
    public void Wheel(float x, float y, int delta)
    {
        x /= VS; y /= VS;
        foreach (var w in Widgets) if (w.Covers(x, y)) { w.Wheel(x, y, delta, Env); return; }
        var a = delta > 0 ? I.OnScrollUp : I.OnScrollDown;
        if (a.Length > 0) BackgroundAction?.Invoke(a);
    }

    void BackgroundClick(float x, float y, int count)
    {
        if (I.Ripple) ripples.Add((x, y, lastTime));
        var a = count == 2 ? I.OnDoubleClick : I.OnClick;
        if (a.Length > 0) BackgroundAction?.Invoke(a);
    }

    void DrawInteractionCells()
    {
        var g = Grid;
        float cw = Env.CellW, ch = Env.CellH;
        ripples.RemoveAll(r => lastTime - r.t > I.RippleDuration);
        trail.RemoveAll(p => lastTime - p.t > I.TrailDuration);
        if (Theme.Smooth) return;
        var rc = Theme[I.RippleColor].Packed;
        var rchars = Cp.Of(I.RippleChars.Length > 0 ? I.RippleChars : "·");
        foreach (var r in ripples)
        {
            float k = (lastTime - r.t) / I.RippleDuration;
            float radius = I.RippleSize * k;
            int steps = Math.Max(12, (int)(radius * 0.4f));
            int chi = Math.Min(rchars.Length - 1, (int)(k * rchars.Length));
            for (int s = 0; s < steps; s++)
            {
                float a = s * MathF.Tau / steps;
                int x = (int)((r.x + MathF.Cos(a) * radius) / cw), y = (int)((r.y + MathF.Sin(a) * radius) / ch);
                g.Put(x, y, rchars[chi], Px.Alpha(rc, 1 - k));
            }
        }
        var tc = Theme[I.TrailColor].Packed;
        var tchars = Cp.Of(I.TrailChars.Length > 0 ? I.TrailChars : "·");
        foreach (var p in trail)
        {
            float k = (lastTime - p.t) / I.TrailDuration;
            g.Put((int)(p.x / cw), (int)(p.y / ch), tchars[Math.Min(tchars.Length - 1, (int)(k * tchars.Length))], Px.Alpha(tc, 1 - k));
        }
    }

    public void DrawInteractionVector(Canvas c)
    {
        if (!Theme.Smooth) return;
        var rc = Theme[I.RippleColor];
        foreach (var r in ripples)
        {
            float k = (lastTime - r.t) / I.RippleDuration;
            float radius = I.RippleSize * k;
            c.Ring(r.x, r.y, radius, rc, 2.5f * (1 - k) + 0.5f, 1 - k);
        }
        var tc = Theme[I.TrailColor];
        foreach (var p in trail)
        {
            float k = (lastTime - p.t) / I.TrailDuration;
            c.Dot(p.x, p.y, 3.5f * (1 - k) + 0.5f, tc, 1 - k);
        }
    }

    public Rgba ClearColor => LiveEngine ? Theme[Scene.Options.Backdrop.Color].WithAlpha(0) : Theme[Scene.Options.Backdrop.Color];

    /// The backdrop is a Wallpaper Engine wallpaper.
    public bool UsesEngine => Scene.Options.Backdrop.Type == "wallpaper_engine";
    /// Wallpaper Engine is drawing it under this desktop (set by the engine): the desktop is drawn see-through.
    /// Off (previews, snapshots) = its preview picture is drawn instead.
    public bool LiveEngine { get; set; }

    public void Compose(int pxW, int pxH, int cellW, int cellH, float time, float dt, float cursorPxX, float cursorPxY, RectangleF? area = null)
    {
        float vs = VS;
        var a = area ?? new RectangleF(0, 0, pxW, pxH);
        Env.Area = new RectangleF(a.X / vs, a.Y / vs, a.Width / vs, a.Height / vs);
        int cols = Math.Max(1, (pxW + cellW - 1) / cellW), rows = Math.Max(1, (pxH + cellH - 1) / cellH);
        Grid.Resize(cols, rows);
        frame.Grid = Grid; frame.Theme = Theme; frame.Time = time; frame.Dt = dt;
        frame.Aspect = cellH / (float)cellW;
        frame.CursorX = cursorPxX / cellW; frame.CursorY = cursorPxY / cellH;
        Env.W = (int)(pxW / vs); Env.H = (int)(pxH / vs); Env.CellW = cellW; Env.CellH = cellH; Env.Theme = Theme; Env.Time = time;
        SetupPainter(cellW, cellH);
        lastTime = time;
        Anim.Step(time, dt, w => over.Contains(w) || w.MouseOver, Env.MouseX, Env.MouseY);
        Scene.Draw(frame);
        DrawInteractionCells();
        if (LayoutWidgets || !Theme.Smooth) foreach (var w in Widgets) { UseFont(w); w.Layout(Env); w.DrawCells(Grid, Env); }
        UseFont(null);
        DrawSelectionCells();
        DrawProblems();
    }

    /// A box's own font and size (Box > Font / Font size), or the theme's for null.
    void UseFont(Widget? w)
    {
        var ui = Theme.Options.Ui;
        var o = w?.Opt;
        Env.Painter.Font = o != null && o.Font.Length > 0 ? o.Font : ui.Font;
        Env.BaseFontSize = Env.Painter.FontSize = o != null && o.FontSize > 0 ? o.FontSize : ui.FontSize;
    }

    void SetupPainter(int cellW, int cellH)
    {
        var p = Env.Painter;
        p.Terminal = !Theme.Smooth; p.CellW = cellW; p.CellH = cellH; p.Theme = Theme; p.Canvas = Env.Canvas;
        p.Font = Theme.Options.Ui.Font; p.MonoFont = Theme.Options.Font.Font;
        Env.BaseFontSize = p.FontSize = Theme.Options.Ui.FontSize;
    }

    /// What the widgets would show now; the desktop redraws when this changes.
    public string Signature()
    {
        if (Env.Theme == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var w in Widgets) { var s = w.Signature(Env); w.LastSig = s; sb.Append(s).Append('#'); }
        return sb.ToString();
    }

    /// Wraps a smooth drawing step so it's drawn at the display's scale.
    public Action<Canvas> Scaled(Action<Canvas> draw) => c =>
    {
        float vs = VS;
        if (Math.Abs(vs - 1) < 0.001f) { draw(c); return; }
        var t0 = c.Dc.Transform;
        c.Dc.Transform = System.Numerics.Matrix3x2.CreateScale(vs) * t0;
        try { draw(c); } finally { c.Dc.Transform = t0; }
    };

    /// Backdrop gradient or picture (a solid colour is just the clear colour).
    public void DrawBackdrop(Canvas c)
    {
        var b = Scene.Options.Backdrop;
        var area = new RectangleF(0, 0, Env.W, Env.H);
        if (b.Type == "gradient") c.Gradient(area, b.Colors.Select(n => Theme[n]).ToList(), b.Angle, b.Shape == "radial");
        else if (b.Type == "image" && b.File.Length > 0) c.Picture(Paths.FindAsset(b.File, Theme.Name, "images"), area, b.Fit, b.Dim);
        else if (b.Type == "wallpaper_engine")
        {
            if (!LiveEngine && Glyph.Data.WallpaperEngine.Find(b.Engine) is { Preview.Length: > 0 } we && File.Exists(we.Preview)) c.Picture(we.Preview, area, "cover", b.Dim);
            else if (LiveEngine && b.Dim > 0) c.Fill(area, new Rgba(0, 0, 0, 255), 0, b.Dim);
        }
        if (!Theme.Smooth) return;
        if (b.Tint != "none" && b.TintAmount > 0)
        {
            var tc = Theme[b.Tint];
            if (b.TintFade == "none") c.Fill(area, tc, 0, b.TintAmount);
            else
            {
                var clear = tc.WithAlpha(0); var full = tc.WithAlpha(b.TintAmount);
                c.Gradient(area, b.TintFade == "bottom" ? new List<Rgba> { clear, full } : new List<Rgba> { full, clear }, 90, false);
            }
        }
        Patterns.Fill(c, area, b.Pattern, Theme[b.PatternColor], b.PatternOpacity, b.PatternSpacing, b.PatternSize);
        Patterns.Fill(c, area, b.Pattern2, Theme[b.Pattern2Color], b.Pattern2Opacity, b.Pattern2Spacing, b.Pattern2Size);
    }

    public bool HasBackdrop
    {
        get
        {
            var b = Scene.Options.Backdrop;
            return b.Type is "gradient" || (b.Type == "image" && b.File.Length > 0) || (b.Type == "wallpaper_engine" && (!LiveEngine || b.Dim > 0)) || (Theme.Smooth && (b.Pattern != "none" || b.Pattern2 != "none" || b.Tint != "none"));
        }
    }

    readonly HashSet<string> drawErrors = new();

    public void DrawWidgets(Canvas c)
    {
        DrawInteractionVector(c);
        if (Theme.Smooth) DrawLinks(c);
        // spotlights in frames follow the mouse; a widget that's growing under the mouse is drawn last (on top)
        FramePainter.Mouse = Env.MouseOn ? new PointF(Env.MouseX, Env.MouseY) : null;
        try
        {
            foreach (var w in Widgets.Where(w => !w.OnTop).Concat(Widgets.Where(w => w.OnTop)))
            {
                UseFont(w); var ww = w;
                var o = w.Opt;
                var aura = o == null ? default : AuraLook.From(Theme, o.Halo, o.HaloSize, o.HaloStrength, o.Glow, o.GlowSize, o.GlowStrength);
                w.Aura = aura;
                // its kept glow picture can be reused if it shows the same and nothing about it moves
                bool still = w.LastSig != null && !w.Animated && !Anim.Running && !(w.MouseOver && (w.FollowsMouse || (Theme.Smooth && Theme.AnySpotlight)));
                // only widgets with a glow/halo are kept as pictures: redrawing the others costs little, keeping
                // every widget cost 10-20 MB for almost no CPU
                w.AuraKey = still && aura.Any ? w.LastSig + "|" + w.Bounds + "|" + w.Hover : null;
                // panels add the halo / glow around their contents themselves (not around their glass); others whole
                try { Anim.Apply(c, w, () => { if (ww is PanelWidget) ww.DrawVector(c, Env); else Aura.Draw(c, aura, () => ww.DrawVector(c, Env), ww, ww.AuraKey); }); }
                catch (Exception ex)
                {
                    // one widget failing mustn't take the others with it
                    var key = (w.Opt?.Name ?? w.GetType().Name) + ex.Message;
                    if (drawErrors.Add(key)) Log.Write($"widget {w.Opt?.Type} \"{w.Opt?.Name}\" failed to draw: {ex}");
                    try { c.Dc.Transform = System.Numerics.Matrix3x2.Identity; } catch { }
                }
                // several glows/halos in one batch can make Direct2D fail the whole frame (seen with ~10 of them):
                // hand each one to the GPU on its own
                if (aura.Any) try { c.Dc.Flush(out _, out _); } catch (Exception fx) { if (drawErrors.Add("flush" + w.Opt?.Name)) Log.Write($"widget \"{w.Opt?.Name}\" glow: {fx.Message}"); }
            }
        }
        finally { FramePainter.Mouse = null; }
        UseFont(null);
        if (!SelectionApart) DrawSelectionVector(c);
    }

    /// The engine draws the smooth selection box by itself (over a kept frame) instead of with the widgets.
    public bool SelectionApart { get; set; }

    /// Smooth themes: the box can be drawn on its own, many times a second, over a kept picture of the desktop.
    public bool SmoothSelecting => selecting && Theme.Smooth;

    public void DrawSelection(Canvas c) => DrawSelectionVector(c);

    /// Lines from a panel to the panel named in its Link to (drawn under the panels).
    void DrawLinks(Canvas c)
    {
        foreach (var w in Widgets)
        {
            var o = w.Opt;
            if (o == null || o.LinkTo.Length == 0 || w.Bounds.IsEmpty) continue;
            var to = Widgets.FirstOrDefault(x => x != w && x.Opt != null && x.Opt.Name.Equals(o.LinkTo, StringComparison.OrdinalIgnoreCase));
            if (to == null || to.Bounds.IsEmpty) continue;
            LinkLine(c, w.Bounds, to.Bounds, o);
        }
    }

    void LinkLine(Canvas c, RectangleF s, RectangleF d, WidgetOptions o)
    {
        var col = c.Brush(Theme[o.LinkColor]);
        bool horiz = d.Left >= s.Right || d.Right <= s.Left;
        PointF a, b;
        if (horiz)
        {
            bool right = d.Left >= s.Right;
            float ey = d.Y + d.Height / 2;
            a = new PointF(right ? s.Right : s.Left, Math.Clamp(ey, s.Top + 8, s.Bottom - 8));
            b = new PointF(right ? d.Left : d.Right, ey);
        }
        else
        {
            bool down = d.Top >= s.Bottom;
            float ex = d.X + d.Width / 2;
            a = new PointF(Math.Clamp(ex, s.Left + 8, s.Right - 8), down ? s.Bottom : s.Top);
            b = new PointF(ex, down ? d.Top : d.Bottom);
        }
        int n = Math.Max(1, o.LinkLines);
        for (int i = 0; i < n; i++)
        {
            float off = (i - (n - 1) / 2f) * o.LinkGap;
            var a2 = horiz ? new PointF(a.X, a.Y + off) : new PointF(a.X + off, a.Y);
            var b2 = horiz ? new PointF(b.X, b.Y + off) : new PointF(b.X + off, b.Y);
            var pts = new List<System.Numerics.Vector2> { new(a2.X, a2.Y) };
            if (o.LinkShape == "elbow")
            {
                float dx = b2.X - a2.X, dy = b2.Y - a2.Y, m = Math.Min(Math.Abs(dx), Math.Abs(dy));
                // straight out of the box first, then the 45° part, then straight into the other box
                float lead = horiz ? Math.Min(12, (Math.Abs(dx) - m) / 2) : Math.Min(12, (Math.Abs(dy) - m) / 2);
                var p = new System.Numerics.Vector2(a2.X, a2.Y);
                if (lead > 0) { p += horiz ? new System.Numerics.Vector2(Math.Sign(dx) * lead, 0) : new System.Numerics.Vector2(0, Math.Sign(dy) * lead); pts.Add(p); }
                p += new System.Numerics.Vector2(Math.Sign(dx) * m, Math.Sign(dy) * m);
                pts.Add(p);
            }
            pts.Add(new(b2.X, b2.Y));
            for (int k = 1; k < pts.Count; k++) c.Dc.DrawLine(pts[k - 1], pts[k], col, o.LinkWidth);
            if (o.LinkEnds is "dot" or "both") c.Fill(new RectangleF(b2.X - 2, b2.Y - 2, 4, 4), Theme[o.LinkColor]);
            if (o.LinkEnds == "both") c.Fill(new RectangleF(a2.X - 2, a2.Y - 2, 4, 4), Theme[o.LinkColor]);
        }
    }

    public bool HasVectorLayer => Widgets.Count > 0 || Theme.Smooth;

    // ---------------- mouse ----------------

    Widget? downWidget; int downItem = -1;
    Widget? lastWidget; int lastItem = -1; long lastClickMs; PointF lastClickPt;
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    bool nearSpot;

    /// Updates which item is under the mouse. Returns true if that changed (needs a redraw).
    public bool UpdateHover(float x, float y, bool overDesktop)
    {
        x /= VS; y /= VS;
        bool changed = false;
        bool hoverTweens = Anim.HasHover;
        Env.MouseX = x; Env.MouseY = y; Env.MouseOn = overDesktop;
        bool spot = Theme.Smooth && Theme.AnySpotlight;
        // a spotlight also lights a box's edge from just outside it: redraw while the mouse is that near any
        // widget (and once more when it leaves, to take the glow away), not only while it's on one
        if (spot)
        {
            float reach = Theme.SpotlightReach;
            bool near = overDesktop && Widgets.Any(w => !w.Bounds.IsEmpty && RectangleF.Inflate(w.Bounds, reach, reach).Contains(x, y));
            if (near || nearSpot) changed = true;
            nearSpot = near;
        }
        foreach (var w in Widgets)
        {
            int h = overDesktop ? w.HitTest(x, y) : -1;
            if (h != w.Hover) { w.Hover = h; changed = true; }
            bool on = overDesktop && w.Covers(x, y);
            if (on != w.MouseOver) { w.MouseOver = on; changed = true; }
            if (on && (w.FollowsMouse || spot)) changed = true;   // it changes with every move over it
            if (hoverTweens && (on ? over.Add(w) : over.Remove(w))) changed = true;
        }
        return changed;
    }

    public void MouseDown(float x, float y)
    {
        x /= VS; y /= VS;
        downWidget = null; downItem = -1;
        bgDown = Widgets.Any(w => w.Covers(x, y)) ? new PointF(-1e6f, -1e6f) : new PointF(x, y);
        selFrom = bgDown.X > -1e5f && I.SelectBox && App.HideDesktopIcons ? bgDown : null;
        selecting = false;
        foreach (var w in Widgets)
        {
            int h = w.HitTest(x, y);
            if (h >= 0) { downWidget = w; downItem = h; return; }
        }
    }

    PointF bgDown = new(-1e6f, -1e6f); long lastBgClick; PointF lastBgPt;

    // ---------------- the selection box (dragging on empty desktop) ----------------
    PointF? selFrom; PointF selTo; bool selecting;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    public bool Selecting => selecting;

    /// The mouse moved (screen pixels relative to this desktop). Returns true if the selection box changed.
    public bool DragTo(float x, float y)
    {
        if (selFrom is not PointF from) return false;
        x /= VS; y /= VS;
        if ((GetAsyncKeyState(0x01) & 0x8000) == 0) { selFrom = null; bool was = selecting; selecting = false; return was; }   // let go somewhere we didn't see
        if (!selecting && Math.Abs(x - from.X) + Math.Abs(y - from.Y) < 5) return false;
        selecting = true; selTo = new PointF(x, y);
        return true;
    }

    RectangleF SelRect() { var a = selFrom ?? selTo; return RectangleF.FromLTRB(Math.Min(a.X, selTo.X), Math.Min(a.Y, selTo.Y), Math.Max(a.X, selTo.X), Math.Max(a.Y, selTo.Y)); }

    void DrawSelectionVector(Canvas c)
    {
        if (!selecting) return;
        var r = SelRect();
        if (Theme.Frame(I.SelectFrame) is { } f) { Glyph.Render.FramePainter.Draw(c, f, Theme, r, "", "", Env.BaseFontSize); return; }
        c.Fill(r, Theme[I.SelectColor], 0, I.SelectOpacity);
        c.Stroke(r, Theme[I.SelectBorder], I.SelectBorderWidth);
    }

    void DrawSelectionCells()
    {
        if (!selecting || Theme.Smooth) return;
        var r = SelRect();
        int x0 = (int)(r.Left / Env.CellW), y0 = (int)(r.Top / Env.CellH), x1 = (int)(r.Right / Env.CellW), y1 = (int)(r.Bottom / Env.CellH);
        int w = Math.Max(1, x1 - x0 + 1), h = Math.Max(1, y1 - y0 + 1);
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                if (x < 0 || y < 0 || x >= Grid.W || y >= Grid.H) continue;
                int i = y * Grid.W + x;
                Grid.Bg[i] = Px.Over(Grid.Bg[i], Px.Alpha(Theme[I.SelectColor].Packed, I.SelectOpacity));
            }
        if (I.SelectBorderWidth > 0 && w >= 2 && h >= 2) Grid.Box(x0, y0, w, h, Theme[I.SelectBorder].Packed);
    }

    public void MouseUp(float x, float y)
    {
        x /= VS; y /= VS;
        bool wasSelecting = selecting;
        selFrom = null; selecting = false;
        if (wasSelecting) { downWidget = null; return; }
        if (downWidget == null)
        {
            if (Math.Abs(x - bgDown.X) > 6 || Math.Abs(y - bgDown.Y) > 6) return;
            long nowMs = clock.ElapsedMilliseconds;
            bool dblBg = nowMs - lastBgClick <= SystemInformation.DoubleClickTime && Math.Abs(x - lastBgPt.X) <= 8 && Math.Abs(y - lastBgPt.Y) <= 8;
            BackgroundClick(x, y, dblBg ? 2 : 1);
            lastBgClick = dblBg ? 0 : nowMs; lastBgPt = new PointF(x, y);
            return;
        }
        int h = downWidget.HitTest(x, y);
        if (h != downItem) { downWidget = null; return; }
        long now = clock.ElapsedMilliseconds;
        bool dbl = lastWidget == downWidget && lastItem == h && now - lastClickMs <= SystemInformation.DoubleClickTime
                   && Math.Abs(x - lastClickPt.X) <= SystemInformation.DoubleClickSize.Width && Math.Abs(y - lastClickPt.Y) <= SystemInformation.DoubleClickSize.Height;
        Log.D($"click on item {h} ({(dbl ? "double" : "single")})");
        Anim.Clicked(downWidget);
        downWidget.Click(h, dbl ? 2 : 1, Env);
        if (dbl) { lastWidget = null; lastItem = -1; }
        else { lastWidget = downWidget; lastItem = h; lastClickMs = now; lastClickPt = new PointF(x, y); }
        downWidget = null;
    }

    // ---------------- problems box ----------------

    void DrawProblems()
    {
        var all = Problems.Concat(RenderProblems).ToList();
        if (all.Count == 0) return;
        var g = Grid;
        const int maxShown = 10;
        var lines = new List<string> { " glyph found a problem in your config " };
        lines.Add("");
        foreach (var p in all.Take(maxShown)) lines.Add(" • " + p);
        if (all.Count > maxShown) lines.Add($" … and {all.Count - maxShown} more (see logs\\glyph.log)");
        lines.Add("");
        lines.Add(" Fix it and save: changes apply straight away. Until then, anything unreadable uses its default.");
        int maxW = Math.Max(20, g.W - 8);
        var wrapped = new List<string>();
        foreach (var l in lines)
        {
            var s = l;
            while (s.Length > maxW - 4) { wrapped.Add(s[..(maxW - 4)]); s = "   " + s[(maxW - 4)..]; }
            wrapped.Add(s);
        }
        int w = Math.Min(maxW, wrapped.Max(l => l.Length) + 4), h = wrapped.Count + 2;
        int x0 = (g.W - w) / 2, y0 = 2;
        uint border = Theme["accent_bright"].Packed, text = Theme["text"].Packed, fill = Theme["surface"].Packed, title = Theme["accent"].Packed;
        g.Box(x0, y0, w, h, border, fill);
        for (int i = 0; i < wrapped.Count; i++) g.Text(x0 + 2, y0 + 1 + i, wrapped[i], i == 0 ? title : text);
    }

    public void Dispose()
    {
        Scene.Dispose();
        foreach (var w in Widgets) w.Dispose();
    }
}
