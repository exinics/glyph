using System.Diagnostics;
using System.Globalization;
using System.Text;
using Glyph.Data;
using Glyph.Render;
using Glyph.Ui;

namespace Glyph.Widgets;

/// An info panel: builds its contents as elements each frame; this class draws the panel, the title,
/// places it, and handles hover and clicks. Works in both styles.
internal abstract class PanelWidget : Widget
{
    protected readonly PanelOptions P;
    protected readonly Theme T;
    protected readonly Rgba Text, Accent, Dim, Line, PanelBg, Border;
    ColEl? root;
    List<El> clickables = new();
    string title = "", titleRight = "";
    readonly FrameOptions? frame;
    readonly FrameOptions? buttonFrame;
    int cx0, cy0, wc, hc, titleRow;
    float pad, titleH;
    bool visible;

    protected PanelWidget(PanelOptions p, Theme t)
    {
        P = p; T = t;
        Text = t[p.TextColor]; Accent = t[p.AccentColor]; Dim = t[p.DimColor]; Line = t["line"];
        PanelBg = p.BoxColor(t); Border = p.BoxBorder(t);
        frame = p.Panel ? t.Frame(p.Frame, t.Options.Ui.PanelFrame) : null;
        buttonFrame = t.Frame(t.Options.Ui.ButtonFrame);
    }

    /// The theme's button frame (for buttons inside panels), or null.
    protected FrameOptions? ButtonFrame => buttonFrame;

    /// The contents, or null to hide the panel.
    protected abstract ColEl? Build(WidgetEnv e);

    protected string V(string format) => Fmt.Text(format, Stats.Value);
    protected List<Span> S(string format, Rgba color) => Fmt.Spans(format, Stats.Value, T, color);

    public override string Signature(WidgetEnv e)
    {
        var sb = new StringBuilder();
        Build(e)?.Sig(sb);
        sb.Append(P.Title.Length > 0 ? V(P.Title) : "").Append(P.TitleRight.Length > 0 ? V(P.TitleRight) : "").Append(Hover);
        return sb.ToString();
    }

    void Scale(WidgetEnv e) => e.Painter.FontSize = e.BaseFontSize * P.Scale;

    WidgetEnv? layoutEnv;

    public override IEnumerable<Part> Parts()
    {
        if (!visible || root == null || Bounds.IsEmpty) yield break;
        var own = new[] { "*own", "Colours" };
        yield return new Part("Box", Bounds, new[] { "Placement", "Box", "Panel", "Link" }, P.Panel ? "Frame" : "");
        var env = layoutEnv;
        if (env == null) yield break;
        var content = PxRect(root, env);
        if (title.Length > 0)
        {
            float top = content.Y - Bounds.Y;
            if (top > 4) yield return new Part("Title", new RectangleF(Bounds.X, Bounds.Y, Bounds.Width, top), new[] { "Panel", "Colours" }, P.Panel ? "Frame" : "");
        }
        yield return new Part("Contents", content, own);
        foreach (var k in clickables)
            if (k is ButtonEl) yield return new Part("Buttons", PxRect(k, env), own, "theme:ButtonFrame");
    }

    // hover reveal: 0 = closed, 1 = showing the Hover text lines
    float reveal, revealAt = -1;
    bool revealing;
    public override bool Animated => revealing;
    public override bool OnTop => reveal > 0;

    static float Smooth(float a) => a * a * (3 - 2 * a);

    public override void Layout(WidgetEnv e)
    {
        float dt = revealAt < 0 ? 0 : Math.Clamp(e.Time - revealAt, 0, 0.2f);
        revealAt = e.Time;
        if (P.HoverText.Length == 0) { reveal = 0; revealing = false; LayoutCore(e, false); return; }
        float target = MouseOver ? 1 : 0;
        if (P.RevealTime <= 0) reveal = target;
        else reveal = target > reveal ? Math.Min(1, reveal + dt * 1000 / P.RevealTime) : Math.Max(0, reveal - dt * 1000 / P.RevealTime);
        revealing = reveal != target;
        if (e.Painter.Terminal || reveal <= 0 || reveal >= 1) { LayoutCore(e, reveal >= 0.5f); return; }
        // part way: the box between its closed and open size (the contents are laid out open and clipped)
        LayoutCore(e, false);
        var closed = Bounds;
        LayoutCore(e, true);
        var open = Bounds;
        float k = Smooth(reveal);
        Bounds = new RectangleF(closed.X + (open.X - closed.X) * k, closed.Y + (open.Y - closed.Y) * k,
                                closed.Width + (open.Width - closed.Width) * k, closed.Height + (open.Height - closed.Height) * k);
    }

    void LayoutCore(WidgetEnv e, bool withHover)
    {
        layoutEnv = e;
        var p = e.Painter;
        Scale(e);
        root = Build(e);
        if (withHover && root != null)
        {
            root.Items.Add(new GapEl(p.Terminal ? 0 : 0.35f));
            foreach (var line in Fmt.Lines(S(P.HoverText, Dim))) root.Items.Add(new TextEl { Spans = line, Size = TextSize.Small });
        }
        visible = root != null;
        if (root == null) { Bounds = RectangleF.Empty; clickables.Clear(); return; }
        title = P.Title.Length > 0 ? V(P.Title) : "";
        titleRight = P.TitleRight.Length > 0 ? V(P.TitleRight) : "";
        var (mw, mh) = p.Measure(root);
        if (p.Terminal)
        {
            int padX = P.Panel ? 2 : 0, padY = P.Panel ? 1 : 0;
            titleRow = !P.Panel && title.Length > 0 ? 1 : 0;
            int cw = (int)Math.Ceiling(mw);
            float wantW = WantW(P, P.Width, e), wantH = WantH(P, P.Height, e);
            if (wantW > 0) cw = Math.Max(cw, (int)Math.Round(wantW / (float)e.CellW) - 2 * padX);
            if (title.Length > 0) cw = Math.Max(cw, title.Length + (P.Panel ? 6 : 0) - 2 * padX);
            wc = cw + 2 * padX; hc = (int)Math.Ceiling(mh) + 2 * padY + titleRow;
            int wantRows = wantH > 0 ? (int)Math.Round(wantH / (float)e.CellH) : 0;
            int extraRows = Math.Max(0, wantRows - hc);
            hc += extraRows;
            var pt = PlaceFor(P, wc * e.CellW, hc * e.CellH, e.Area);
            cx0 = (int)MathF.Round(pt.X / e.CellW); cy0 = (int)MathF.Round(pt.Y / e.CellH);
            Bounds = new RectangleF(cx0 * e.CellW, cy0 * e.CellH, wc * e.CellW, hc * e.CellH);
            int shift = P.VerticalAlign == "center" ? extraRows / 2 : P.VerticalAlign == "bottom" ? extraRows : 0;
            p.Arrange(root, cx0 + padX, cy0 + padY + titleRow + shift, cw);
        }
        else if (frame != null && e.Canvas != null)
        {
            var ins = FramePainter.Space(frame, T, p.FontSize, title.Length > 0);
            float cw = mw, wantW = WantW(P, P.Width, e), wantH = WantH(P, P.Height, e);
            if (wantW > 0) cw = Math.Max(cw, wantW - ins.L - ins.R);
            if (title.Length > 0)
            {
                var tt = frame.TitleUpper ? title.ToUpperInvariant() : title;
                float tsz = FramePainter.TitleSize(frame, T, p.FontSize);
                float tw = FramePainter.MeasureText(e.Canvas, tt, FramePainter.TitleFont(frame, T), tsz, frame.TitleBold, frame.TitleSpacing);
                if (titleRight.Length > 0) tw += FramePainter.MeasureText(e.Canvas, titleRight, FramePainter.TitleFont(frame, T), tsz * 0.9f, false, frame.TitleSpacing) + tsz * 2;
                if (frame.Header == "tab") tw = frame.TabWidth > 0 ? frame.TabWidth : tw + 2 * Math.Max(8, ins.L) + frame.TabSlant;
                if (frame.Header != "above") cw = Math.Max(cw, tw - (frame.Header == "tab" ? ins.L + ins.R : 0));   // an above-title may run past the box
            }
            float hh = Math.Max(mh + ins.T + ins.B, wantH);
            float extra = hh - (mh + ins.T + ins.B);
            var size = new SizeF(cw + ins.L + ins.R, hh);
            var pt = PlaceFor(P, size.Width, size.Height, e.Area);
            Bounds = new RectangleF(pt, size);
            float shiftY = P.VerticalAlign == "center" ? extra / 2 : P.VerticalAlign == "bottom" ? extra : 0;
            p.Arrange(root, pt.X + ins.L, pt.Y + ins.T + shiftY, cw);
        }
        else
        {
            var ui = T.Options.Ui;
            pad = P.Panel ? ui.Padding : 0;
            titleH = title.Length > 0 ? p.LineH(TextSize.Title) * 1.25f : 0;
            float cw = mw, wantW = WantW(P, P.Width, e), wantH = WantH(P, P.Height, e);
            if (wantW > 0) cw = Math.Max(cw, wantW - 2 * pad);
            if (title.Length > 0 && e.Canvas != null) cw = Math.Max(cw, e.Canvas.Measure(title, p.Font, p.Px(TextSize.Title), true));
            var size = new SizeF(cw + 2 * pad, Math.Max(mh + 2 * pad + titleH, wantH));
            float extra = size.Height - (mh + 2 * pad + titleH);
            var pt = PlaceFor(P, size.Width, size.Height, e.Area);
            Bounds = new RectangleF(pt, size);
            float shiftY = P.VerticalAlign == "center" ? extra / 2 : P.VerticalAlign == "bottom" ? extra : 0;
            p.Arrange(root, pt.X + pad, pt.Y + pad + titleH + shiftY, cw);
        }
        clickables = Painter.Clickable(root).ToList();
        if (Hover >= 0 && Hover < clickables.Count) clickables[Hover].Hot = true;
    }

    RectangleF PxRect(El el, WidgetEnv e) => e.Painter.Terminal
        ? new RectangleF(el.X * e.CellW, el.Y * e.CellH, Math.Max(1, el.W) * e.CellW, Math.Max(1, el.H) * e.CellH)
        : new RectangleF(el.X, el.Y, el.W, el.H);

    WidgetEnv? lastEnv;

    float hitFrac; bool hitLabel;   // where on a meter the last hit was

    public override int HitTest(float x, float y)
    {
        if (!visible || lastEnv == null || !Bounds.Contains(x, y)) return -1;
        for (int i = 0; i < clickables.Count; i++)
            if (PxRect(clickables[i], lastEnv).Contains(x, y))
            {
                if (clickables[i] is MeterEl m)
                {
                    var p = lastEnv.Painter;
                    float ux = p.Terminal ? x / lastEnv.CellW : x, uy = p.Terminal ? y / lastEnv.CellH : y;
                    var (x0, x1, onLabel) = p.MeterHit(m, ux, uy);
                    hitFrac = p.Terminal
                        ? Math.Clamp((MathF.Floor(ux - x0) + 1) / Math.Max(1, x1 - x0), 0, 1)   // the clicked cell counts as filled
                        : Math.Clamp((ux - x0) / Math.Max(0.001f, x1 - x0), 0, 1);
                    hitLabel = onLabel;
                }
                return i;
            }
        return P.OnClick.Length > 0 ? clickables.Count : -1;
    }

    public override void Click(int item, int count, WidgetEnv e)
    {
        if (count != 1) return;
        string action = P.OnClick;
        if (item >= 0 && item < clickables.Count)
        {
            var el = clickables[item];
            action = el.Action;
            if (el is MeterEl m)
            {
                if (hitLabel && m.LabelAction.Length > 0) action = m.LabelAction;
                else if (m.SetAction.Length > 0) action = m.SetAction.Replace("{v}", MathF.Round(hitFrac * 100).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        if (action.Length > 0) e.Do?.Invoke(action);
    }

    public override bool Wheel(float x, float y, int delta, WidgetEnv e)
    {
        if (!visible || lastEnv == null || !Bounds.Contains(x, y)) return false;
        foreach (var el in clickables)
        {
            var a = delta > 0 ? el.WheelUp : el.WheelDown;
            if (a.Length > 0 && PxRect(el, lastEnv).Contains(x, y)) { e.Do?.Invoke(a); return true; }
        }
        return false;
    }

    public override void DrawCells(Grid g, WidgetEnv e)
    {
        lastEnv = e;
        if (!visible || root == null || !e.Painter.Terminal) return;
        var ui = T.Options.Ui;
        g.FillRect(cx0, cy0, wc, hc, 0);   // the wallpaper's characters don't show through
        if (P.Panel)
        {
            bool hot = Hover == clickables.Count && P.BoxHoverColor != "none";
            g.FillRect(cx0, cy0, wc, hc, Px.Alpha((hot ? T[P.BoxHoverColor] : PanelBg).Packed, hot ? Math.Max(0.35f, P.BoxOpacity(T)) : P.BoxOpacity(T)));
            if (P.BoxBorderWidth(T) != 0) g.Box(cx0, cy0, wc, hc, Border.Packed);
            if (title.Length > 0) g.Text(cx0 + 2, cy0, $" {Trunc(title, wc - 6)} ", Accent.Packed);
        }
        else if (title.Length > 0) g.Text(cx0, cy0, title, Accent.Packed);
        if (P.Panel && titleRight.Length > 0 && titleRight.Length < wc - title.Length - 8) g.Text(cx0 + wc - 3 - titleRight.Length, cy0, $" {titleRight} ", Dim.Packed);
        e.Painter.PaintCells(root, g, 0, 0, T["bg"]);
    }

    static string Trunc(string s, int n) => n <= 1 ? "" : s.Length <= n ? s : s[..(n - 1)] + "…";

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        bool clip = reveal > 0 && reveal < 1 && !e.Painter.Terminal;
        if (clip) c.Dc.PushAxisAlignedClip(new Vortice.Mathematics.Rect(Bounds.X - 30, Bounds.Y - 30, Bounds.Width + 60, Bounds.Height + 30), Vortice.Direct2D1.AntialiasMode.Aliased);
        try { DrawPanel(c, e); }
        finally { if (clip) c.Dc.PopAxisAlignedClip(); }
    }

    void DrawPanel(Canvas c, WidgetEnv e)
    {
        if (!visible || root == null) return;
        var p = e.Painter;
        if (p.Terminal) { p.PaintPictures(root, c); return; }
        Scale(e);
        var ui = T.Options.Ui;
        float radius = P.BoxRadius(T);
        if (frame != null)
        {
            bool hotF = Hover == clickables.Count && (P.BoxHoverColor != "none" || frame.HoverFill != "none");
            FramePainter.Draw(c, frame, T, Bounds, title, titleRight, p.FontSize, hotF);
            p.PaintVector(root, c, 0, 0, PanelBg, Math.Max(2, (frame.Radius >= 0 ? frame.Radius : radius) * 0.6f));
            p.FontSize = e.BaseFontSize;
            return;
        }
        if (P.Panel)
        {
            bool hot = Hover == clickables.Count && P.BoxHoverColor != "none";
            c.Shadow(Bounds, radius, P.BoxShadow(T));
            c.Fill(Bounds, PanelBg, radius, P.BoxOpacity(T));
            if (hot) c.Fill(Bounds, T[P.BoxHoverColor], radius, 0.25f);
            c.Stroke(Bounds, Border, P.BoxBorderWidth(T), radius);
        }
        if (title.Length > 0)
            c.Text(title, new RectangleF(Bounds.X + pad, Bounds.Y + pad * 0.8f, Bounds.Width - 2 * pad, titleH), Accent, p.Font, p.Px(TextSize.Title), ui.BoldTitles);
        if (titleRight.Length > 0)
            c.Text(titleRight, new RectangleF(Bounds.X + pad, Bounds.Y + pad * 0.8f, Bounds.Width - 2 * pad, titleH), Dim, p.Font, p.Px(TextSize.Small), false, "right");
        p.PaintVector(root, c, 0, 0, PanelBg, Math.Max(4, radius * 0.6f));
        p.FontSize = e.BaseFontSize;
    }
}

internal sealed class ClockWidget : PanelWidget
{
    readonly ClockOptions o;
    public ClockWidget(ClockOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        var col = new ColEl();
        col.Items.Add(new BigEl
        {
            Text = V(o.Format), Color = Accent, Pixel = o.Pixel, HasShadow = o.Shadow != "none", Shadow = T[o.Shadow],
            Scale = o.BigScale, Font = o.BigFont, Align = o.Align, Spacing = o.Spacing,
        });
        if (o.Subtitle.Length > 0)
        {
            if (e.Painter.Terminal) col.Items.Add(new GapEl(0.6f));
            var size = o.SubtitleSize switch { "small" => TextSize.Small, "normal" => TextSize.Normal, _ => TextSize.Large };
            col.Items.Add(new TextEl { Spans = S(o.Subtitle, o.SubtitleColor == "auto" ? Text : T[o.SubtitleColor]), Size = size, Align = o.Align, Spacing = o.SubtitleSpacing });
        }
        return col;
    }
}

internal sealed class SystemWidget : PanelWidget
{
    readonly SystemOptions o;
    public SystemWidget(SystemOptions o, Theme t) : base(o, t) { this.o = o; }

    MeterEl Meter(string label, float v, string text) => new()
    {
        Label = label, Value = v, ValueText = text, Fill = Accent, Track = Line, LabelColor = Dim, ValueColor = Text, Chars = o.MeterChars,
        Segments = o.Segments, ValueAlign = o.OneLine ? "sides" : "",
    };

    protected override ColEl? Build(WidgetEnv e)
    {
        Stats.Use();
        var s = Stats.Now;
        bool term = e.Painter.Terminal;
        var col = new ColEl();
        void Add(El el) { if (col.Items.Count > 0 && !term) col.Items.Add(new GapEl(0.35f)); col.Items.Add(el); }
        foreach (var what in o.Show)
        {
            switch (what.Trim().ToLowerInvariant())
            {
                case "cpu":
                    Add(Meter(term ? "cpu " : "CPU", s.Cpu / 100f, $"{s.Cpu:0}%"));
                    if (o.Graphs) col.Items.Add(new GraphEl { Values = s.CpuHist, Max = 100, Color = Accent, Rows = o.GraphRows });
                    break;
                case "ram":
                case "memory":
                    Add(Meter(term ? "ram " : "Memory", s.Ram / 100f, term ? $"{s.Ram:0}%" : $"{Fmt.Bytes(s.RamUsed, "")} / {Fmt.Bytes(s.RamTotal, "0")}"));
                    break;
                case "disk":
                    Add(Meter(term ? "disk" : $"Disk {o.Drive}:", s.Disk / 100f, term ? $"{s.Disk:0}%" : $"{Fmt.Bytes(s.DiskFree, "")} free"));
                    break;
                case "battery":
                    if (s.HasBattery) Add(Meter(term ? "bat " : "Battery", s.Battery / 100f, $"{s.Battery}%{(s.Charging ? " ⚡" : "")}"));
                    break;
                case "volume":
                    if (s.Volume >= 0) Add(Meter(term ? "vol " : "Volume", s.Volume, s.Muted ? "muted" : $"{s.Volume * 100:0}%"));
                    break;
                case "net":
                case "network":
                    Add(new TextEl { Spans = { new Span(term ? "net  " : "Network  ", Dim), new Span($"↓ {Fmt.Bytes(s.NetDown, "")}/s   ↑ {Fmt.Bytes(s.NetUp, "")}/s", Text) } });
                    if (o.Graphs) col.Items.Add(new GraphEl { Values = s.DownHist, Max = 0, Color = Accent, Rows = o.GraphRows });
                    break;
            }
        }
        return col;
    }
}

internal sealed class MediaWidget : PanelWidget
{
    readonly MediaOptions o;
    public MediaWidget(MediaOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        Media.Use();
        if (!Media.HasSession && o.HideWhenIdle) return null;
        var col = new ColEl();
        bool term = e.Painter.Terminal;
        if (!Media.HasSession && o.Line0.Length == 0 && !o.Visualiser) { col.Items.Add(new TextEl("nothing playing", Dim)); return col; }
        var row = new RowEl { Gap = 2 };
        if (o.Art && Media.Art != null)
        {
            var style = o.ArtStyle == "auto" ? (term ? "ascii" : "original") : o.ArtStyle;
            row.Items.Add(new ImageEl
            {
                Key = "media" + Media.ArtVersion, Rows = o.ArtRows, Style = style, AsciiMode = "braille",
                Source = () => { var a = Media.Art; lock (Gate) return a == null ? null : new Bitmap(a); },
                Tint = new Gradient(T, new[] { "accent_dim", T.Options.Ui.AccentColor, "bright" }), Mono = Accent,
            });
        }
        var info = new ColEl();
        if (o.Line0.Length > 0) info.Items.Add(new TextEl { Spans = S(o.Line0, Dim), Size = TextSize.Small });
        info.Items.Add(new TextEl { Spans = Media.HasSession ? S(o.Line1, Text) : new List<Span> { new("nothing playing", Text) }, Bold = true });
        info.Items.Add(new TextEl { Spans = Media.HasSession ? S(o.Line2, Dim) : new List<Span> { new("—", Dim) } });
        if (o.Progress)
        {
            info.Items.Add(new GapEl(term ? 0 : 0.3f));
            string pos = Media.Value("media_position", "") ?? "", len = Media.Value("media_length", "") ?? "";
            bool beside = o.TimesBeside && !term;
            info.Items.Add(new MeterEl
            {
                Value = Media.Progress, Label = beside ? pos : "", ValueText = beside ? len : $"{pos} / {len}", Fill = Accent, Track = Line, ValueColor = Dim, LabelColor = Dim, Chars = "━─",
                Segments = o.ProgressSegments, ValueAlign = beside ? "sides" : "",
            });
        }
        if (o.Controls)
        {
            var buttons = new RowEl { Gap = 1 };
            var bf = ButtonFrame;
            static string L(string own, string builtIn) => own.Length > 0 ? Glyph.Render.Symbols.Expand(own) : builtIn;
            buttons.Items.Add(new ButtonEl { Label = L(o.PrevLabel, bf != null ? "⏮" : "◀◀"), Action = "media:prev", Color = Text, Hover = Accent, Frame = bf });
            buttons.Items.Add(new ButtonEl { Label = Media.Playing ? L(o.PauseLabel, bf != null ? "⏸" : "❚❚") : L(o.PlayLabel, "▶"), Action = "media:toggle", Color = Text, Hover = Accent, Frame = bf });
            buttons.Items.Add(new ButtonEl { Label = L(o.NextLabel, bf != null ? "⏭" : "▶▶"), Action = "media:next", Color = Text, Hover = Accent, Frame = bf });
            info.Items.Add(new GapEl(term ? 0 : 0.3f));
            info.Items.Add(buttons);
        }
        row.Items.Add(info);
        col.Items.Add(row);
        if (o.Visualiser)
        {
            if (Media.HasSession && Media.Playing) Spectrum.Use();   // the audio reader only runs while something plays
            col.Items.Add(new GapEl(term ? 0 : 0.3f));
            col.Items.Add(new SpectrumEl { Bands = Spectrum.Bands(o.VisualiserBars), Color = T[o.VisualiserColor], Peak = T[o.VisualiserPeak], Rows = o.VisualiserRows });
        }
        return col;
    }

    static readonly object Gate = new();

    public override bool Animated => base.Animated || (o.Visualiser && Spectrum.Moving);
    public int Fps => o.VisualiserFps;
}

/// Lines that keep coming in (made-up hex dumps, logs, values), older ones fading.
internal sealed class FeedWidget : PanelWidget
{
    readonly FeedOptions o;
    readonly List<string> lines = new();
    readonly Random rng = new();
    double next;
    int seq, gen;
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

    public FeedWidget(FeedOptions o, Theme t) : base(o, t)
    {
        this.o = o;
        seq = rng.Next(0x1000, 0xE000);
        for (int i = 0; i < o.MaxLines; i++) Add();
        Schedule();
    }

    void Schedule() => next = clock.Elapsed.TotalSeconds + o.Interval * (1 + (rng.NextDouble() * 2 - 1) * o.Jitter);

    string? Value(string name, string spec)
    {
        switch (name)
        {
            case "hex":
                {
                    int n = int.TryParse(spec, out var k) ? Math.Clamp(k, 1, 32) : 4;
                    var sb = new StringBuilder(n);
                    for (int i = 0; i < n; i++) sb.Append("0123456789ABCDEF"[rng.Next(16)]);
                    return sb.ToString();
                }
            case "num":
                {
                    var a = spec.Split('-');
                    int lo = 0, hi = 100;
                    if (a.Length == 2) { int.TryParse(a[0], out lo); int.TryParse(a[1], out hi); }
                    else if (a.Length == 1 && int.TryParse(a[0], out var h1)) hi = h1;
                    return rng.Next(Math.Min(lo, hi), Math.Max(lo, hi) + 1).ToString(CultureInfo.InvariantCulture);
                }
            case "spaces":
                {
                    var a = spec.Split('-');
                    int lo = 0, hi = 4;
                    if (a.Length == 2) { int.TryParse(a[0], out lo); int.TryParse(a[1], out hi); }
                    return new string(' ', rng.Next(Math.Max(0, lo), Math.Max(lo, hi) + 1));
                }
            case "pick":
                {
                    var a = spec.Split(',');
                    return a.Length == 0 ? "" : a[rng.Next(a.Length)];
                }
            case "seq":
                seq = (seq + rng.Next(1, 40)) & 0xFFFF;
                return spec.Length > 0 ? seq.ToString(spec, CultureInfo.InvariantCulture) : seq.ToString(CultureInfo.InvariantCulture);
        }
        return Stats.Value(name, spec);
    }

    void Add()
    {
        if (o.Lines.Length == 0) return;
        var pattern = o.Lines[rng.Next(o.Lines.Length)];
        lines.Add(Fmt.Text(pattern, Value));      // values filled in now; colour markup kept for drawing
        while (lines.Count > o.MaxLines) lines.RemoveAt(0);
        gen++;
    }

    protected override ColEl? Build(WidgetEnv e)
    {
        double now = clock.Elapsed.TotalSeconds;
        if (now >= next)
        {
            int n = 0;
            while (now >= next && n++ < o.MaxLines) { Add(); next += o.Interval * (1 + (rng.NextDouble() * 2 - 1) * o.Jitter); }
            if (now >= next) Schedule();
        }
        var size = o.Size switch { "large" => TextSize.Large, "normal" => TextSize.Normal, _ => TextSize.Small };
        var col = new ColEl();
        var newest = T[o.NewestColor]; var old = T[o.OldColor];
        int count = lines.Count;
        for (int k = 0; k < count; k++)
        {
            int i = o.NewestFirst ? count - 1 - k : k;      // i = age order: 0 oldest
            float age = count <= 1 ? 0 : (count - 1 - i) / (float)(count - 1);   // 0 = newest
            var c = i == count - 1 ? newest : Rgba.Lerp(Text, old, age);
            col.Items.Add(new TextEl { Spans = Fmt.Spans(lines[i], (_, _) => null, T, c), Mono = o.Mono, Size = size, Tight = true });
        }
        return col;
    }

    public override string Signature(WidgetEnv e) { var s = base.Signature(e); return gen + "|" + s; }
}

internal sealed class CalendarWidget : PanelWidget
{
    readonly CalendarOptions o;
    public CalendarWidget(CalendarOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        var today = DateTime.Today;
        var first = new DateTime(today.Year, today.Month, 1);
        int startDow = o.FirstDay == "sunday" ? 0 : 1;
        var culture = CultureInfo.CurrentCulture;
        var grid = new GridEl();
        var head = new List<Span>();
        if (o.WeekNumbers) head.Add(new Span("wk", Dim));
        for (int i = 0; i < 7; i++) head.Add(new Span(culture.DateTimeFormat.AbbreviatedDayNames[(startDow + i) % 7][..2], Dim));
        grid.Rows.Add(head);
        int offset = ((int)first.DayOfWeek - startDow + 7) % 7;
        var day = first.AddDays(-offset);
        var todayC = T[o.TodayColor]; var weekendC = T[o.WeekendColor];
        for (int week = 0; week < 6; week++)
        {
            if (week > 0 && day.Month != today.Month) break;
            var row = new List<Span>();
            if (o.WeekNumbers) row.Add(new Span(ISOWeek.GetWeekOfYear(day.AddDays(3)).ToString(), Dim));
            for (int i = 0; i < 7; i++, day = day.AddDays(1))
            {
                if (day.Month != today.Month) { row.Add(new Span("", Dim)); continue; }
                var c = day == today ? todayC : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? weekendC : Text;
                row.Add(new Span(day.Day.ToString(), c));
            }
            grid.Rows.Add(row);
        }
        return new ColEl { Items = { grid } };
    }
}

internal sealed class WeatherWidget : PanelWidget
{
    readonly WeatherOptions o;
    public WeatherWidget(WeatherOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        Weather.Configure(o.City, o.Latitude, o.Longitude, o.Units == "fahrenheit");
        var col = new ColEl();
        col.Items.Add(new TextEl { Spans = S(o.Line, Text), Size = TextSize.Large });
        if (o.Detail.Length > 0 && Weather.Days.Count > 0) col.Items.Add(new TextEl { Spans = S(o.Detail, Dim), Size = TextSize.Small });
        if (o.Days > 0 && Weather.Days.Count > 1)
        {
            col.Items.Add(new GapEl(0.5f));
            var g = new GridEl();
            foreach (var d in Weather.Days.Skip(1).Take(o.Days))
                g.Rows.Add(new List<Span> { new(d.Date.ToString("ddd", CultureInfo.CurrentCulture), Dim), new(Weather.Icon(d.Code), Accent), new($"{d.High:0}°", Text), new($"{d.Low:0}°", Dim) });
            col.Items.Add(g);
        }
        return col;
    }
}

internal sealed class TextWidget : PanelWidget
{
    readonly TextOptions o;
    public TextWidget(TextOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        var size = o.Size switch { "small" => TextSize.Small, "large" => TextSize.Large, "title" => TextSize.Title, _ => TextSize.Normal };
        var col = new ColEl();
        foreach (var line in Fmt.Lines(S(o.Text, Text)))
            col.Items.Add(new TextEl
            {
                Spans = line, Size = size, Align = o.Align, Mono = o.Mono, Spacing = o.Spacing,
                Shadow = o.TextShadow != "none" ? T[o.TextShadow] : null, ShadowX = o.ShadowOffset, ShadowY = o.ShadowOffset,
            });
        return col;
    }
}

internal sealed class CommandWidget : PanelWidget
{
    readonly CommandOptions o;
    volatile string[] lines = { "running…" };
    DateTime next = DateTime.MinValue;
    volatile bool running;
    public CommandWidget(CommandOptions o, Theme t) : base(o, t) { this.o = o; }

    void RunIfDue()
    {
        if (running || DateTime.UtcNow < next || o.Command.Trim().Length == 0) return;
        running = true; next = DateTime.UtcNow.AddSeconds(o.Interval);
        Task.Run(() =>
        {
            try
            {
                var (file, args) = Actions.SplitCommand(o.Command);
                var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi)!;
                var outTask = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit(15000)) { try { p.Kill(true); } catch { } lines = new[] { "(took too long)" }; return; }
                var text = outTask.Result.Replace("\r", "").TrimEnd('\n');
                if (text.Length == 0) text = p.StandardError.ReadToEnd().Trim();
                lines = text.Split('\n').Take(o.MaxLines).Select(l => l.TrimEnd()).ToArray();
            }
            catch (Exception ex) { lines = new[] { "can't run: " + ex.Message }; }
            finally { running = false; }
        });
    }

    protected override ColEl? Build(WidgetEnv e)
    {
        RunIfDue();
        var col = new ColEl();
        foreach (var l in lines) col.Items.Add(new TextEl(l, Text) { Mono = o.Mono });
        return col;
    }
}

internal sealed class SysInfoWidget : PanelWidget
{
    readonly SysInfoOptions o;
    readonly string[] art;
    static readonly string[] Logo =
    {
        "███████ ███████", "███████ ███████", "███████ ███████", "               ", "███████ ███████", "███████ ███████", "███████ ███████",
    };

    public SysInfoWidget(SysInfoOptions o, Theme t) : base(o, t)
    {
        this.o = o;
        art = Logo;
        if (o.Art.Length > 0)
        {
            var path = Paths.FindAsset(o.Art, t.Name, "art");
            if (File.Exists(path)) art = File.ReadAllText(path).Replace("\r", "").TrimEnd('\n').Split('\n');
        }
    }

    protected override ColEl? Build(WidgetEnv e)
    {
        var row = new RowEl { Gap = 3 };
        var left = new ColEl(); var right = new ColEl();
        var ac = T[o.ArtColor];
        foreach (var l in art) left.Items.Add(new TextEl(l, ac) { Mono = true, Tight = true });
        foreach (var l in o.Lines) right.Items.Add(new TextEl { Spans = S(l, Text), Mono = true });
        row.Items.Add(left); row.Items.Add(right);
        return new ColEl { Items = { row } };
    }
}

internal sealed class ImageWidget : PanelWidget
{
    readonly ImageOptions o;
    readonly string path;
    public ImageWidget(ImageOptions o, Theme t) : base(o, t) { this.o = o; path = o.File.Length > 0 ? Paths.FindAsset(o.File, t.Name, "images") : ""; }

    protected override ColEl? Build(WidgetEnv e)
    {
        if (path.Length == 0 || !File.Exists(path)) return new ColEl { Items = { new TextEl("pick a picture (Widgets > File)", Dim) } };
        var style = o.Style == "auto" ? (e.Painter.Terminal ? "ascii" : "original") : o.Style;
        return new ColEl
        {
            Items =
            {
                new ImageEl
                {
                    Key = path + File.GetLastWriteTimeUtc(path).Ticks, Rows = o.Rows, Style = style, AsciiMode = o.AsciiMode,
                    Source = () => { try { using var fs = File.OpenRead(path); using var img = Image.FromStream(fs); return new Bitmap(img); } catch { return null; } },
                    Tint = new Gradient(T, new[] { "accent_dim", T.Options.Ui.AccentColor, "bright" }), Mono = Accent,
                },
            },
        };
    }
}
