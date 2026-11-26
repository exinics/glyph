using System.Text;
using Glyph.Data;
using Glyph.Render;
using Glyph.Ui;

namespace Glyph.Bar;

/// Builds, lays out and draws the bar's modules (left, centre, right), and finds what's under the mouse.
internal sealed class BarScene
{
    public BarFile File = new();
    List<Glyph.Widgets.Shortcut> shortcuts = new();
    public Theme Theme = new("none");
    public readonly Painter Painter = new();
    readonly RowEl[] groups = { new(), new(), new() };
    List<El> clickables = new();
    public int Hover = -1;
    int rows = 1, contentRow;
    float width, height;

    public BarOptions O => File.Bar;
    public bool Enabled => O.Enabled;

    public void Load(Theme theme, List<string> problems)
    {
        Theme = theme;
        try { File = BarFile.Load(theme, problems); }
        catch (Exception e) { problems.Add("bar.toml: " + e.Message); File = new BarFile(); File.Bar.Enabled = false; }
        shortcuts = File.Left.Concat(File.Center).Concat(File.Right).Any(m => m is IconsModule) ? Glyph.Widgets.ShortcutStore.Load(new List<string>()) : new();
        Painter.Reset();
    }

    /// The character font for a terminal-style bar.
    public FontOptions BarFont()
    {
        var f = Theme.Options.Font;
        if (O.Font.Length == 0 && O.FontSize <= 0) return f;
        return new FontOptions { Font = O.Font.Length > 0 ? O.Font : f.Font, FontSize = O.FontSize > 0 ? O.FontSize : f.FontSize, FontWeight = f.FontWeight, LineSpacing = f.LineSpacing };
    }

    void Setup(int cellW, int cellH, Canvas? canvas)
    {
        var p = Painter;
        p.Terminal = !Theme.Smooth; p.CellW = cellW; p.CellH = cellH; p.Theme = Theme; p.Canvas = canvas;
        p.Font = O.Font.Length > 0 ? O.Font : Theme.Options.Ui.Font;
        p.MonoFont = Theme.Options.Font.Font;
        p.FontSize = O.FontSize > 0 ? O.FontSize : Theme.Options.Ui.FontSize;
    }

    Rgba? Pill(BarModuleOptions m)
    {
        if (m.Background == "none") return null;
        if (m.Background != "auto") return Theme[m.Background];
        if (O.ModuleBackground != "none") return Theme[O.ModuleBackground];
        return null;
    }

    El? Make(BarModuleOptions m)
    {
        if (!m.Enabled) return null;
        var color = Theme[m.Color];
        El? el = null;
        switch (m)
        {
            case TextModule t:
                if (t.HideWhenEmpty && !Fmt.HasValues(t.Format, Stats.Value)) return null;
                el = new TextEl
                {
                    Spans = Fmt.Cut(Fmt.Spans(t.Format, Stats.Value, Theme, color), t.MaxLength), Bold = t.Bold, Background = Pill(t),
                    Size = t.Size switch { "small" => TextSize.Small, "large" => TextSize.Large, _ => TextSize.Normal }, Spacing = t.Spacing,
                };
                break;
            case MeterModule mm:
                el = new MeterEl
                {
                    Label = mm.Label, ValueText = Fmt.Text(mm.Format, Stats.Value), Value = Stats.Fraction(mm.Value), Cells = mm.Width, Inline = true,
                    Fill = Theme[mm.FillColor], Track = Theme[mm.TrackColor], LabelColor = mm.LabelColor == "auto" ? color : Theme[mm.LabelColor], ValueColor = color, Chars = mm.Chars,
                    Segments = mm.Segments, BarHeight = mm.BarHeight,
                };
                break;
            case IconsModule ic:
                {
                    var row = new RowEl { Gap = Painter.Terminal ? 1 : ic.Spacing / (Painter.FontSize * 0.5f) };
                    float size = ic.IconSize > 0 ? ic.IconSize : MathF.Round(Painter.FontSize * 1.3f);
                    var tint = new Gradient(Theme, new[] { "accent_dim", "accent", "bright" });
                    var pick = ic.Names.Length > 0
                        ? ic.Names.Select(n => shortcuts.FirstOrDefault(s => s.Name.Equals(n.Trim(), StringComparison.OrdinalIgnoreCase))).OfType<Glyph.Widgets.Shortcut>()
                        : shortcuts.Where(s => ic.Group.Length == 0 || s.Group.Equals(ic.Group, StringComparison.OrdinalIgnoreCase));
                    foreach (var s in pick)
                    {
                        var sc = s;
                        row.Items.Add(new ImageEl
                        {
                            Key = "bar|" + sc.IconKey + sc.Image, Px = size, Rows = 1, Style = Painter.Terminal ? "mono" : ic.Style, Tint = tint, Mono = color,
                            Source = () => Glyph.Widgets.IconSource.Get(sc, Theme.Name), Action = "shortcut:" + sc.Target,
                        });
                    }
                    if (row.Items.Count == 0) return null;
                    row.Tag = m;
                    return row;
                }
            case GraphModule g:
                {
                    var row = new RowEl { Gap = 1 };
                    if (g.Label.Length > 0) row.Items.Add(new TextEl(g.Label, color));
                    row.Items.Add(new GraphEl { Values = Stats.History(g.Value), Max = g.Value is "cpu" or "ram" ? 100 : 0, Color = Theme["accent"], Rows = 1, Cells = g.Width });
                    el = row;
                    break;
                }
            case WorkspacesModule w:
                {
                    Stats.Use();
                    var s = Stats.Now;
                    var row = new RowEl { Gap = Painter.Terminal ? 0 : 0.4f };
                    var af = Painter.Terminal ? null : Theme.Frame(w.ActiveFrame);
                    var inf = Painter.Terminal ? null : Theme.Frame(w.InactiveFrame);
                    var activeText = w.ActiveText == "auto" ? Theme[O.Background] : Theme[w.ActiveText];
                    for (int i = 1; i <= Math.Max(Math.Max(1, w.MinCount), s.Desktops); i++)
                    {
                        bool active = i == s.Desktop;
                        string num = i.ToString().PadLeft(w.Digits, '0');
                        string label = w.Style == "dots" ? (active ? "●" : "○") : w.Style == "names" && active && s.DesktopName.Length > 0 ? s.DesktopName : num;
                        var fr = active ? af : inf;
                        var te = new TextEl
                        {
                            Spans = { new Span(Painter.Terminal && !active ? $" {label} " : label, active ? (w.Style == "dots" ? Theme[w.ActiveColor] : activeText) : Theme[w.InactiveColor]) },
                            Background = active && w.Style != "dots" && af == null ? Theme[w.ActiveColor] : null,
                            Action = $"desktop:{i}", Bold = active,
                        };
                        if (af != null || inf != null)
                        {
                            // framed numbers: all the same size, the frame takes the clicks
                            bool inOuter = Theme.Frame(w.Frame, O.ModuleFrame) != null;
                            var box = new BoxEl { Child = te, Frame = fr, PadX = Painter.FontSize * 0.45f, PadY = 0, MinW = Painter.FontSize * 2, MinH = inOuter ? BoxHeight() - 2 * O.ModuleInset - 2 : BoxHeight(), Action = te.Action };
                            te.Action = "";
                            if (fr != null) te.Background = null;
                            row.Items.Add(box);
                        }
                        else row.Items.Add(te);
                    }
                    row.WheelUp = "desktop:prev"; row.WheelDown = "desktop:next";
                    row.Tag = m;
                    return row;
                }
            case SpacerModule sp:
                return new TextEl(new string(' ', Math.Max(0, sp.Width)), color) { Tag = m };
        }
        if (el == null) return null;
        el.Tag = m;
        el.Action = m.OnClick; el.RightAction = m.OnRightClick; el.WheelUp = m.OnScrollUp; el.WheelDown = m.OnScrollDown;
        return el;
    }

    /// Height of a module frame: the bar's height less the inset (smooth style).
    float BoxHeight() => Math.Max(0, height - 2 * O.ModuleInset - (O.BorderWidth > 0 && O.Frame == "none" ? O.BorderWidth : 0));

    void Build()
    {
        var lists = new[] { File.Left, File.Center, File.Right };
        for (int g = 0; g < 3; g++)
        {
            var row = groups[g];
            row.Items.Clear();
            float gapUnits = Painter.Terminal ? Math.Max(1, MathF.Round(O.Gap / (float)Painter.CellW)) : O.Gap / (Painter.FontSize * 0.5f);
            row.Gap = gapUnits;
            var list = lists[g];
            for (int i = 0; i < list.Count; i++)
            {
                // modules marked Join share a frame with the ones after them
                var members = new List<BarModuleOptions> { list[i] };
                while (list[i].Join && i + 1 < list.Count) members.Add(list[++i]);
                var els = new List<El>();
                foreach (var m in members) { var el = Make(m); if (el != null) els.Add(el); }
                if (els.Count == 0) continue;
                var first = members.First(m => m.Enabled);
                var fr = Painter.Terminal ? null : Theme.Frame(first.Frame, O.ModuleFrame);
                if (fr == null) { row.Items.AddRange(els); continue; }
                El body = els.Count == 1 ? els[0] : new RowEl { Gap = O.JoinGap / (Painter.FontSize * 0.5f), Items = els };
                var box = new BoxEl { Child = body, Frame = fr, PadX = O.ModulePadding + Math.Abs(fr.Slant) * 0.5f, MinH = BoxHeight(), Tag = first };
                if (els.Count == 1 && body is not RowEl && body.Clickable)
                {
                    // the whole frame takes the clicks (and lights up)
                    box.Action = body.Action; box.RightAction = body.RightAction; box.WheelUp = body.WheelUp; box.WheelDown = body.WheelDown;
                    body.Action = body.RightAction = body.WheelUp = body.WheelDown = "";
                }
                row.Items.Add(box);
            }
        }
    }

    public string Signature(int cellW, int cellH, Canvas? canvas)
    {
        Setup(cellW, cellH, canvas);
        Build();
        var sb = new StringBuilder();
        foreach (var g in groups) g.Sig(sb);
        sb.Append(Hover);
        return sb.ToString();
    }

    /// Lays out for a bar of w × h pixels.
    public void Layout(int w, int h, int cellW, int cellH, Canvas? canvas)
    {
        Setup(cellW, cellH, canvas);
        width = w; height = h;
        Build();
        var p = Painter;
        if (p.Terminal)
        {
            int cols = Math.Max(1, w / cellW);
            rows = Math.Max(1, h / cellH);
            bool border = O.BorderWidth > 0 && rows >= 2;
            int usable = border ? rows - 1 : rows;
            contentRow = (O.Position == "bottom" && border ? 1 : 0) + (usable - 1) / 2;
            int pad = (int)MathF.Round(O.Padding / (float)cellW);
            Place(groups[0], pad, contentRow, cols - 2 * pad, "left");
            Place(groups[2], pad, contentRow, cols - 2 * pad, "right");
            Place(groups[1], pad, contentRow, cols - 2 * pad, "center");
            Unclash(1);
        }
        else
        {
            float pad = O.Padding;
            foreach (var (g, align) in new[] { (groups[0], "left"), (groups[2], "right"), (groups[1], "center") })
            {
                var (_, mh) = p.Measure(g);
                Place(g, pad, (h - mh) / 2, w - 2 * pad, align);
            }
            Unclash(O.Gap);
        }
        clickables = groups.SelectMany(Painter.Clickable).ToList();
        if (Hover >= 0 && Hover < clickables.Count) clickables[Hover].Hot = true;
    }

    /// The centre group moves aside if it would overlap the left or right group (a full bar on a small screen).
    void Unclash(float gap)
    {
        var (l, c, r) = (groups[0], groups[1], groups[2]);
        if (c.Items.Count == 0) return;
        float lEnd = l.Items.Count > 0 ? l.X + Painter.Measure(l).w + gap : 0;
        float rStart = r.Items.Count > 0 ? r.X - gap : float.MaxValue;
        float cw = Painter.Measure(c).w;
        float x = c.X;
        if (x + cw > rStart) x = rStart - cw;
        if (x < lEnd) x = lEnd;
        if (Math.Abs(x - c.X) > 0.01f) Painter.Arrange(c, Painter.Terminal ? MathF.Round(x) : x, c.Y, cw);
    }

    void Place(RowEl g, float x, float y, float w, string align)
    {
        var (mw, _) = Painter.Measure(g);
        float gx = align == "right" ? x + w - mw : align == "center" ? x + (w - mw) / 2 : x;
        if (Painter.Terminal) gx = MathF.Round(gx);
        g.Align = "left";
        Painter.Arrange(g, gx, y, mw);
    }

    RectangleF PxRect(El e) => Painter.Terminal
        ? new RectangleF(e.X * Painter.CellW, e.Y * Painter.CellH, Math.Max(1, e.W) * Painter.CellW, Math.Max(1, e.H) * Painter.CellH)
        : new RectangleF(e.X, e.Y, e.W, e.H);

    public int HitTest(float x, float y)
    {
        for (int i = clickables.Count - 1; i >= 0; i--)
        {
            var r = PxRect(clickables[i]);
            if (Painter.Terminal) r = new RectangleF(r.X, 0, r.Width, height);   // the whole height of the bar counts
            if (r.Contains(x, y)) return i;
        }
        return -1;
    }

    public string ActionAt(int i, string kind)
    {
        if (i < 0 || i >= clickables.Count) return "";
        var e = clickables[i];
        return kind switch { "right" => e.RightAction, "up" => e.WheelUp, "down" => e.WheelDown, _ => e.Action };
    }

    public Rgba ClearColor => new(0, 0, 0, 0);

    /// Where each module ended up (pixels in the bar), for the editor.
    public List<(BarModuleOptions m, RectangleF r)> ModuleRects()
    {
        var list = new List<(BarModuleOptions, RectangleF)>();
        foreach (var g in groups)
            foreach (var e in g.Items)
                if (e.Tag is BarModuleOptions m)
                {
                    var r = PxRect(e);
                    if (Painter.Terminal) r = new RectangleF(r.X, 0, r.Width, height);
                    list.Add((m, r));
                }
        return list;
    }

    public void PaintCells(Grid g)
    {
        var bg = Theme[O.Background];
        g.Clear(0);
        if (Painter.Terminal)
        {
            g.FillRect(0, 0, g.W, g.H, Px.Alpha(bg.Packed, O.Opacity));
            if (O.BorderWidth > 0 && rows >= 2)
            {
                int ry = O.Position == "bottom" ? 0 : rows - 1;
                for (int x = 0; x < g.W; x++) g.Put(x, ry, '─', Theme[O.BorderColor].Packed);
            }
            foreach (var grp in groups) Painter.PaintCells(grp, g, 0, 0, bg);
        }
    }

    public void PaintVector(Canvas c)
    {
        if (Painter.Terminal) return;
        var r = new RectangleF(0, 0, width, height);
        if (Theme.Frame(O.Frame) is { } bf)
        {
            Glyph.Render.FramePainter.Draw(c, bf, Theme, r, "", "", Painter.FontSize);
            foreach (var grp in groups) Painter.PaintVector(grp, c, 0, 0, Theme[O.Background], O.ModuleRadius);
            return;
        }
        c.Fill(r, Theme[O.Background], O.CornerRadius, O.Opacity);
        if (O.BorderWidth > 0)
        {
            if (O.CornerRadius > 0 || O.Margin > 0) c.Stroke(r, Theme[O.BorderColor], O.BorderWidth, O.CornerRadius);
            else
            {
                float y = O.Position == "bottom" ? 0 : height - O.BorderWidth;
                c.Fill(new RectangleF(0, y, width, O.BorderWidth), Theme[O.BorderColor]);
            }
        }
        foreach (var grp in groups) Painter.PaintVector(grp, c, 0, 0, Theme[O.Background], O.ModuleRadius);
    }
}
