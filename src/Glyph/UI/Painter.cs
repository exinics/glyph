using System.Drawing.Imaging;
using Glyph.Data;
using Glyph.Render;
using Glyph.Widgets;

namespace Glyph.Ui;

/// Measures, lays out and draws elements, either as characters (terminal style, units = cells)
/// or as smooth shapes and text (units = pixels).
internal sealed class Painter
{
    public bool Terminal;
    public int CellW = 9, CellH = 17;
    public Theme Theme = null!;
    public Canvas? Canvas;              // for measuring text in the smooth style
    public string Font = "Segoe UI", MonoFont = "Cascadia Mono";
    public float FontSize = 13;
    public string HoverColor = "accent";   // highlight under the mouse on clickable text
    Rgba? textOverride;                     // text colour inside a framed box under the mouse (its frame's Hover text)

    readonly Dictionary<string, (int[] ch, uint[] fg)> asciiCache = new();
    readonly Dictionary<string, (byte[] px, int w, int h)> pixelCache = new();

    public void Reset() { asciiCache.Clear(); pixelCache.Clear(); }

    public float Px(TextSize s) => FontSize * s switch { TextSize.Small => 0.86f, TextSize.Large => 1.45f, TextSize.Title => 1.05f, _ => 1f };
    public float LineH(TextSize s = TextSize.Normal) => Px(s) * 1.36f;
    float Gap => Terminal ? 1 : FontSize * 0.5f;

    float MeasureText(TextEl t)
    {
        if (Terminal) return Fmt.Length(t.Spans) + (t.Background.HasValue ? 2 : 0);
        if (Canvas == null) return t.Plain.Length * Px(t.Size) * 0.55f;
        float w = 0;
        foreach (var s in t.Spans) w += Glyph.Render.FramePainter.MeasureText(Canvas, s.Text, t.Mono ? MonoFont : Font, Px(t.Size), t.Bold || t.Size == TextSize.Title, t.Spacing);
        return w + (t.Background.HasValue ? FontSize * 1.2f : 0);
    }

    // ───────────── measuring ─────────────

    public (float w, float h) Measure(El e)
    {
        switch (e)
        {
            case TextEl t when t.Rotate != 0 && !Terminal: return (t.Tight ? Px(t.Size) * 1.17f : LineH(t.Size), MeasureText(t));
            case TextEl t: return (MeasureText(t), Terminal ? 1 : t.Tight ? Px(t.Size) * 1.17f : LineH(t.Size));
            case BigEl b:
                if (Terminal) return (BigFont.Width(b.Text) + (b.HasShadow ? 2 : 0), BigFont.Rows + (b.HasShadow ? 1 : 0));
                {
                    float size = FontSize * b.Scale;
                    var font = b.Font.Length > 0 ? b.Font : Font;
                    float bw = Canvas == null ? b.Text.Length * size * 0.6f : Glyph.Render.FramePainter.MeasureText(Canvas, b.Text, font, size, false, b.Spacing);
                    return (bw, size * 1.2f);
                }
            case MeterEl m:
                if (Terminal) return (m.Label.Length + m.ValueText.Length + (m.Cells > 0 ? m.Cells : 10) + 2, 1);
                if (m.ValueAlign == "sides") return (FontSize * 14, LineH());
                if (m.Inline)
                    return ((Canvas?.Measure(m.Label, Font, FontSize, false) ?? 0) + (Canvas?.Measure(m.ValueText, Font, FontSize, false) ?? 0) + m.Cells * FontSize * 0.6f + FontSize, LineH());
                return (FontSize * 14, LineH() + FontSize * 0.7f);
            case GraphEl g: return Terminal ? (g.Cells > 0 ? g.Cells : 16, g.Rows) : (g.Cells > 0 ? g.Cells * FontSize * 0.6f : FontSize * 14, g.Rows * LineH());
            case ImageEl i: return Terminal ? (i.Rows * 2, i.Rows) : i.Px > 0 ? (i.Px, i.Px) : (i.Rows * LineH(), i.Rows * LineH());
            case ButtonEl k:
                if (Terminal) return (k.Label.Length + 4, 1);
                return ((Canvas?.Measure(k.Label, Font, FontSize, false) ?? k.Label.Length * FontSize * 0.6f) + FontSize * 1.8f, LineH() * 1.45f);
            case GridEl gr:
                {
                    var cols = ColumnWidths(gr);
                    return (cols.Sum(), gr.Rows.Count * (Terminal ? 1 : LineH() * 1.12f));
                }
            case GapEl gp: return (0, Terminal ? (float)Math.Ceiling(gp.Rows - 0.25f) : gp.Rows * LineH());
            case BoxEl bx:
                {
                    var (cw, ch) = Measure(bx.Child);
                    return (Math.Max(bx.MinW, cw + 2 * bx.PadX), Math.Max(bx.MinH, ch + 2 * bx.PadY));
                }
            case SpectrumEl sp: return (Terminal ? 16 : FontSize * 14, Terminal ? Math.Max(1, MathF.Round(sp.Rows)) : sp.Rows * LineH());
            case RowEl r when r.Vertical && !Terminal:
                {
                    float w = 0, h = 0;
                    foreach (var c in r.Items) { var (cw, ch) = Measure(c); w = Math.Max(w, cw); h += ch; }
                    h += Math.Max(0, r.Items.Count - 1) * r.Gap * Gap;
                    return (w, h);
                }
            case RowEl r:
                {
                    float w = 0, h = 0;
                    foreach (var c in r.Items) { var (cw, ch) = Measure(c); w += cw; h = Math.Max(h, ch); }
                    w += Math.Max(0, r.Items.Count - 1) * r.Gap * Gap;
                    return (w, h);
                }
            case ColEl col:
                {
                    float w = 0, h = 0;
                    foreach (var c in col.Items) { var (cw, ch) = Measure(c); w = Math.Max(w, cw); h += ch; }
                    return (w, h);
                }
        }
        return (0, 0);
    }

    float[] ColumnWidths(GridEl g)
    {
        int n = g.Rows.Count == 0 ? 0 : g.Rows.Max(r => r.Count);
        var w = new float[n];
        foreach (var r in g.Rows)
            for (int i = 0; i < r.Count; i++)
            {
                float cw = Terminal ? r[i].Text.Length + 1 : (Canvas?.Measure(r[i].Text, Font, FontSize, false) ?? r[i].Text.Length * FontSize * 0.6f) + FontSize * 0.9f;
                w[i] = Math.Max(w[i], cw);
            }
        return w;
    }

    // ───────────── layout ─────────────

    public void Arrange(El e, float x, float y, float w)
    {
        var (mw, mh) = Measure(e);
        e.X = x; e.Y = y; e.W = e.Stretch || e is ColEl || e is TextEl { Align: "center" or "right", Rotate: 0 } ? w : Math.Min(mw, w); e.H = mh;   // centred / right text needs the room to move in
        if (e is RowEl { Vertical: true } vr && !Terminal)
        {
            // a column: each item centred across the width, one under the other
            float cy = y;
            foreach (var c in vr.Items)
            {
                var (cw, ch) = Measure(c);
                Arrange(c, x + Math.Max(0, (w - cw) / 2), cy, Math.Min(cw, w));
                cy += ch + vr.Gap * Gap;
            }
            e.W = w;
        }
        else if (e is RowEl r)
        {
            float fixedW = 0; int stretchers = 0;
            foreach (var c in r.Items) { if (c.Stretch) stretchers++; else fixedW += Measure(c).w; }
            float gaps = Math.Max(0, r.Items.Count - 1) * r.Gap * Gap;
            float each = stretchers > 0 ? Math.Max(0, (w - fixedW - gaps) / stretchers) : 0;
            float total = fixedW + gaps + each * stretchers;
            float cx = stretchers == 0 && r.Align == "right" ? x + w - total : stretchers == 0 && r.Align == "center" ? x + (w - total) / 2 : x;
            if (Terminal) cx = MathF.Round(cx);
            foreach (var c in r.Items)
            {
                float cw = c.Stretch ? each : Measure(c).w;
                if (Terminal) cw = MathF.Floor(cw);
                float ch = Measure(c).h;
                Arrange(c, cx, y + (mh - ch) / (Terminal ? 2 : 2), cw);
                if (Terminal) c.Y = MathF.Floor(c.Y);
                cx += cw + r.Gap * Gap;
            }
            e.W = w;
        }
        else if (e is ColEl col)
        {
            float cy = y;
            foreach (var c in col.Items) { Arrange(c, x, cy, w); cy += c.H; }
        }
        else if (e is BoxEl bx)
        {
            var (cw, ch) = Measure(bx.Child);
            float innerW = e.W - 2 * bx.PadX;
            float cx = x + bx.PadX + (bx.Child.Stretch ? 0 : Math.Max(0, (innerW - cw) / 2));
            Arrange(bx.Child, Terminal ? MathF.Round(cx) : cx, y + (mh - ch) / 2, bx.Child.Stretch ? innerW : cw);
        }
    }

    /// Leaves with an action, for clicking and hover.
    public static IEnumerable<El> Clickable(El e)
    {
        if (e.Clickable) yield return e;
        foreach (var c in e.Children) foreach (var k in Clickable(c)) yield return k;
    }

    // ───────────── drawing: characters ─────────────

    /// Where a meter's bar is, in the meter's own units (cells or pixels): its left and right ends, and
    /// whether point (px, py) is on the label instead.
    public (float x0, float x1, bool onLabel) MeterHit(MeterEl m, float px, float py)
    {
        float x = m.X, w = m.W;
        if (Terminal)
        {
            float bx = x + m.Label.Length + (m.Label.Length > 0 ? 1 : 0);
            float bw = Math.Max(1, x + w - bx - m.ValueText.Length - (m.ValueText.Length > 0 ? 1 : 0));
            return (bx, bx + bw, px < bx);
        }
        if (m.Inline)
        {
            float lw = Canvas?.Measure(m.Label, Font, FontSize, false) ?? 0;
            float bx = x + lw + (lw > 0 ? FontSize * 0.5f : 0);
            return (bx, bx + m.Cells * FontSize * 0.6f, px < bx);
        }
        float labelW = Canvas?.Measure(m.Label, Font, FontSize, false) ?? m.Label.Length * FontSize * 0.55f;
        return (x, x + w, py < m.Y + LineH() && px < x + labelW + FontSize * 0.5f);
    }

    public void PaintCells(El e, Grid g, int ox, int oy, Rgba bg)
    {
        int x = ox + (int)e.X, y = oy + (int)e.Y, w = (int)e.W;
        switch (e)
        {
            case TextEl t:
                {
                    int len = Fmt.Length(t.Spans) + (t.Background.HasValue ? 2 : 0);
                    int sx = t.Align == "right" ? x + w - len : t.Align == "center" ? x + (w - len) / 2 : x;
                    if (t.Background is Rgba pill)
                    {
                        g.FillRect(sx, y, len, 1, t.Hot ? Px2(pill, 1.35f) : pill.Packed);
                        sx++;
                    }
                    else if (t.Hot) g.FillRect(sx, y, len, 1, Theme[HoverColor].WithAlpha(0.25f).Packed);
                    foreach (var s in t.Spans) { g.Text(sx, y, s.Text, s.Color.Packed); sx += s.Text.Length; }
                    break;
                }
            case BigEl b:
                {
                    int bw = BigFont.Width(b.Text);
                    int sx = b.Align == "right" ? x + w - bw : b.Align == "center" ? x + (w - bw) / 2 : x;
                    var px = Cp.Of(b.Pixel.Length > 0 ? b.Pixel : "█");
                    void Draw(int dx, int dy, Rgba c)
                    {
                        int cx = sx + dx;
                        foreach (var ch in b.Text)
                        {
                            var gl = BigFont.Glyph(ch);
                            for (int r = 0; r < BigFont.Rows; r++)
                                for (int k = 0; k < gl[r].Length; k++)
                                    if (gl[r][k] == '#') { g.Put(cx + k * 2, y + dy + r, px[0], c.Packed); g.Put(cx + k * 2 + 1, y + dy + r, px[px.Length > 1 ? 1 : 0], c.Packed); }
                            cx += gl[0].Length * 2 + 2;
                        }
                    }
                    if (b.HasShadow) Draw(1, 1, b.Shadow);
                    Draw(0, 0, b.Color);
                    break;
                }
            case MeterEl m:
                {
                    var chars = Cp.Of(m.Chars.Length >= 2 ? m.Chars : "█░");
                    g.Text(x, y, m.Label, m.LabelColor.Packed);
                    int barX = x + m.Label.Length + (m.Label.Length > 0 ? 1 : 0);
                    int barW = Math.Max(1, x + w - barX - m.ValueText.Length - (m.ValueText.Length > 0 ? 1 : 0));
                    int fill = (int)MathF.Round(Math.Clamp(m.Value, 0, 1) * barW);
                    for (int i = 0; i < barW; i++) g.Put(barX + i, y, i < fill ? chars[0] : chars[1], (i < fill ? m.Fill : m.Track).Packed);
                    if (m.ValueText.Length > 0) g.Text(x + w - m.ValueText.Length, y, m.ValueText, m.ValueColor.Packed);
                    break;
                }
            case GraphEl gr:
                {
                    int dotsW = w * 2, dotsH = gr.Rows * 4;
                    var v = gr.Values;
                    float max = gr.Max > 0 ? gr.Max : Math.Max(1e-6f, v.Length == 0 ? 1 : v.Max());
                    var heights = new int[dotsW];
                    for (int i = 0; i < dotsW; i++)
                    {
                        int src = v.Length - dotsW + i;
                        heights[i] = src < 0 ? 0 : (int)MathF.Round(Math.Clamp(v[src] / max, 0, 1) * dotsH);
                    }
                    ReadOnlySpan<int> bitL = stackalloc int[] { 6, 2, 1, 0 }, bitR = stackalloc int[] { 7, 5, 4, 3 }; // dot bits, bottom row first
                    for (int cy = 0; cy < gr.Rows; cy++)
                        for (int cx = 0; cx < w; cx++)
                        {
                            int bits = 0;
                            int rowBase = (gr.Rows - 1 - cy) * 4;       // dots below this cell
                            for (int d = 0; d < 4; d++)
                            {
                                if (heights[cx * 2] > rowBase + d) bits |= 1 << bitL[d];
                                if (heights[cx * 2 + 1] > rowBase + d) bits |= 1 << bitR[d];
                            }
                            if (bits != 0) g.Put(x + cx, y + cy, 0x2800 + bits, gr.Color.Packed);
                        }
                    break;
                }
            case ImageEl im:
                {
                    int cols = (int)e.W, rows = (int)e.H;
                    var key = $"{im.Key}|{cols}x{rows}|{im.AsciiMode}";
                    if (!asciiCache.TryGetValue(key, out var a))
                    {
                        using var src = im.Source();
                        a = src == null ? (Array.Empty<int>(), Array.Empty<uint>()) : IconStyler.Ascii(src, cols, rows, im.AsciiMode, im.Tint ?? new Gradient(Theme, new[] { "accent_dim", "accent", "bright" }));
                        if (asciiCache.Count > 200) asciiCache.Clear();
                        asciiCache[key] = a;
                    }
                    for (int i = 0; i < a.ch.Length; i++) if (a.ch[i] != 32) g.Put(x + i % cols, y + i / cols, a.ch[i], a.fg[i]);
                    break;
                }
            case SpectrumEl sp:
                {
                    int n = Math.Max(1, w);
                    int rowsN = Math.Max(1, (int)e.H);
                    for (int i = 0; i < n; i++)
                    {
                        float v = sp.Bands.Length == 0 ? 0 : Math.Clamp(sp.Bands[Math.Min(sp.Bands.Length - 1, i * sp.Bands.Length / n)], 0, 1);
                        int eighths = (int)MathF.Round(v * rowsN * 8);
                        for (int r = 0; r < rowsN; r++)
                        {
                            int here = Math.Clamp(eighths - (rowsN - 1 - r) * 8, 0, 8);
                            if (here > 0) g.Put(x + i, y + r, here == 8 ? 0x2588 : 0x2580 + here, Rgba.Lerp(sp.Color, sp.Peak, v).Packed);
                        }
                    }
                    break;
                }
            case ButtonEl k:
                {
                    var s = $"[ {k.Label} ]";
                    if (k.Hot) { g.FillRect(x, y, s.Length, 1, k.Hover.Packed); g.Text(x, y, s, bg.Packed); }
                    else g.Text(x, y, s, k.Color.Packed);
                    break;
                }
            case GridEl gd:
                {
                    var cw = ColumnWidths(gd);
                    for (int r = 0; r < gd.Rows.Count; r++)
                    {
                        int cx = x;
                        for (int c = 0; c < gd.Rows[r].Count; c++)
                        {
                            var cell = gd.Rows[r][c];
                            int colW = (int)cw[c];
                            g.Text(cx + colW - 1 - cell.Text.Length, y + r, cell.Text, cell.Color.Packed);
                            cx += colW;
                        }
                    }
                    break;
                }
        }
        foreach (var c in e.Children) PaintCells(c, g, ox, oy, bg);
    }

    static uint Px2(Rgba c, float k) => new Rgba((byte)Math.Min(255, c.R * k + 12), (byte)Math.Min(255, c.G * k + 12), (byte)Math.Min(255, c.B * k + 12), c.A).Packed;

    // ───────────── drawing: smooth ─────────────

    public void PaintVector(El e, Canvas c, float ox, float oy, Rgba bg, float radius)
    {
        float x = ox + e.X, y = oy + e.Y, w = e.W, h = e.H;
        switch (e)
        {
            case TextEl t when t.Rotate != 0:
                {
                    // drawn as if flat in a box turned around the element's middle
                    var t0 = c.Dc.Transform;
                    var mid = new System.Numerics.Vector2(x + w / 2, y + h / 2);
                    c.Dc.Transform = System.Numerics.Matrix3x2.CreateRotation(t.Rotate * MathF.PI / 180) * System.Numerics.Matrix3x2.CreateTranslation(mid) * t0;
                    var (ex, ey, ew, eh, er) = (t.X, t.Y, t.W, t.H, t.Rotate);
                    t.X = -h / 2 - ox; t.Y = -w / 2 - oy; t.W = h; t.H = w; t.Rotate = 0;
                    try { PaintVector(t, c, ox, oy, bg, radius); }
                    finally { (t.X, t.Y, t.W, t.H, t.Rotate) = (ex, ey, ew, eh, er); c.Dc.Transform = t0; }
                    return;
                }
            case TextEl t:
                {
                    float tw = MeasureText(t);
                    float sx = t.Align == "right" ? x + w - tw : t.Align == "center" ? x + (w - tw) / 2 : x;
                    if (t.Background is Rgba pill)
                    {
                        c.Fill(new RectangleF(sx, y + h * 0.08f, tw, h * 0.84f), t.Hot ? Lighter(pill) : pill, radius);
                        sx += FontSize * 0.6f;
                    }
                    else if (t.Hot) c.Fill(new RectangleF(sx - 4, y, tw + 8, h), Theme[HoverColor], radius, 0.18f);
                    float size = Px(t.Size);
                    var font = t.Mono ? MonoFont : Font;
                    bool bold = t.Bold || t.Size == TextSize.Title;
                    if (t.Shadow is Rgba sh)
                    {
                        float shx = sx;
                        foreach (var s in t.Spans)
                        {
                            float sw = Glyph.Render.FramePainter.MeasureText(Canvas!, s.Text, font, size, bold, t.Spacing);
                            Glyph.Render.FramePainter.DrawText(c, s.Text, shx + t.ShadowX, y + (h - size * 1.36f) / 2 + t.ShadowY, size * 1.36f, sh, font, size, bold, t.Spacing);
                            shx += sw;
                        }
                    }
                    foreach (var s in t.Spans)
                    {
                        float sw = Glyph.Render.FramePainter.MeasureText(Canvas!, s.Text, font, size, bold, t.Spacing);
                        var sc = textOverride ?? s.Color;
                        if (t.Spacing != 0) Glyph.Render.FramePainter.DrawText(c, s.Text, sx, y + (h - size * 1.36f) / 2, size * 1.36f, sc, font, size, bold, t.Spacing);
                        else c.Text(s.Text, new RectangleF(sx, y + (h - size * 1.36f) / 2, sw + 4, size * 1.5f), sc, font, size, bold);
                        if (t.Strike && s.Text.Trim().Length > 0) c.Fill(new RectangleF(sx, y + h * 0.53f, sw, Math.Max(1, size * 0.07f)), sc);
                        sx += sw;
                    }
                    break;
                }
            case BigEl b:
                {
                    float size = FontSize * b.Scale;
                    var font = b.Font.Length > 0 ? b.Font : Font;
                    if (Math.Abs(b.Spacing) >= 0.01f)
                    {
                        float bw = Glyph.Render.FramePainter.MeasureText(c, b.Text, font, size, false, b.Spacing);
                        float bx = b.Align == "right" ? x + w - bw : b.Align == "center" ? x + (w - bw) / 2 : x;
                        float lh = size * 1.36f;
                        if (b.HasShadow) Glyph.Render.FramePainter.DrawText(c, b.Text, bx + 2, y + 3, lh, b.Shadow, font, size, false, b.Spacing);
                        Glyph.Render.FramePainter.DrawText(c, b.Text, bx, y, lh, b.Color, font, size, false, b.Spacing);
                        break;
                    }
                    if (b.HasShadow) c.Text(b.Text, new RectangleF(x + 2, y + 3, w, h), b.Shadow, font, size, false, b.Align);
                    c.Text(b.Text, new RectangleF(x, y, w, h), b.Color, font, size, false, b.Align);
                    break;
                }
            case MeterEl m when m.Inline:
                {
                    float lw = Canvas!.Measure(m.Label, Font, FontSize, false), vw = Canvas.Measure(m.ValueText, Font, FontSize, false);
                    float lh = LineH(), ty = y + (h - lh) / 2;
                    c.Text(m.Label, new RectangleF(x, ty, lw + 4, lh), m.LabelColor, Font, FontSize);
                    float bx = x + lw + (lw > 0 ? FontSize * 0.5f : 0), bw = m.Cells * FontSize * 0.6f, bh = m.BarHeight > 0 ? m.BarHeight : Math.Max(4, FontSize * 0.42f), by = y + (h - bh) / 2;
                    Bar(c, new RectangleF(bx, by, bw, bh), m);
                    c.Text(m.ValueText, new RectangleF(bx + bw + FontSize * 0.5f, ty, vw + 4, lh), m.ValueColor, Font, FontSize);
                    break;
                }
            case MeterEl m when m.ValueAlign == "sides":
                {
                    float lh = LineH();
                    float lw = m.Label.Length > 0 ? Math.Max(m.LabelWidth, Canvas!.Measure(m.Label, Font, FontSize, false) + FontSize * 0.6f) : 0;
                    float vw = m.ValueText.Length > 0 ? Math.Max(m.ValueWidth, Canvas!.Measure(m.ValueText, Font, FontSize, false) + FontSize * 0.6f) : 0;
                    float ty = y + (h - lh) / 2;
                    c.Text(m.Label, new RectangleF(x, ty, lw + 4, lh), m.LabelColor, Font, FontSize);
                    c.Text(m.ValueText, new RectangleF(x + w - vw, ty, vw, lh), m.ValueColor, Font, FontSize, false, "right");
                    float bh = m.BarHeight > 0 ? m.BarHeight : Math.Max(4, FontSize * 0.42f);
                    Bar(c, new RectangleF(x + lw, y + (h - bh) / 2, Math.Max(4, w - lw - vw), bh), m);
                    break;
                }
            case MeterEl m:
                {
                    float lh = LineH();
                    c.Text(m.Label, new RectangleF(x, y, w, lh), m.LabelColor, Font, FontSize);
                    c.Text(m.ValueText, new RectangleF(x, y, w, lh), m.ValueColor, Font, FontSize, false, "right");
                    float bh = m.BarHeight > 0 ? m.BarHeight : Math.Max(4, FontSize * 0.42f), by = y + lh + 1;
                    Bar(c, new RectangleF(x, by, w, bh), m);
                    break;
                }
            case GraphEl gr:
                {
                    var v = gr.Values;
                    if (v.Length < 2) break;
                    float max = gr.Max > 0 ? gr.Max : Math.Max(1e-6f, v.Max());
                    var pts = new List<PointF>(v.Length);
                    for (int i = 0; i < v.Length; i++)
                        pts.Add(new PointF(x + w * i / (Stats.HistoryLength - 1f) + w * (Stats.HistoryLength - v.Length) / (Stats.HistoryLength - 1f), y + h - 2 - (h - 4) * Math.Clamp(v[i] / max, 0, 1)));
                    c.Area(pts, y + h, gr.Color, 0.35f, 1.5f);
                    break;
                }
            case ImageEl im:
                {
                    int size = Math.Max(8, (int)Math.Min(w, h));
                    var key = $"{im.Key}|{size}|{im.Style}";
                    if (!pixelCache.TryGetValue(key, out var p))
                    {
                        using var src = im.Source();
                        if (src != null)
                        {
                            using var fitted = IconStyler.Fit(src, size);
                            p = (IconStyler.Pixels(fitted, im.Style, im.Tint ?? new Gradient(Theme, new[] { "accent_dim", "accent", "bright" }), im.Mono), size, size);
                        }
                        else p = (Array.Empty<byte>(), 0, 0);
                        if (pixelCache.Count > 100) pixelCache.Clear();
                        pixelCache[key] = p;
                    }
                    if (p.w > 0)
                    {
                        var bmp = c.Bitmap("el|" + key, () => p);
                        c.Draw(bmp, new RectangleF(x, y, size, size));
                    }
                    break;
                }
            case ButtonEl k when k.Frame != null:
                {
                    var r = new RectangleF(x, y + 1, w, h - 2);
                    Glyph.Render.FramePainter.Draw(c, k.Frame, Theme, r, "", "", FontSize, k.Hot);
                    var kc = k.Hot && k.Frame.HoverText != "none" ? Theme[k.Frame.HoverText] : k.Hot && k.Frame.HoverFill == "none" ? k.Hover : k.Frame.TextColor != "none" ? Theme[k.Frame.TextColor] : k.Color;
                    c.Text(k.Label, new RectangleF(x, y + (h - LineH()) / 2, w, LineH()), kc, Font, FontSize, false, "center");
                    break;
                }
            case ButtonEl k:
                {
                    var r = new RectangleF(x, y + 1, w, h - 2);
                    c.Fill(r, k.Hot ? k.Hover : Theme["line"], Math.Min(radius, h / 2), k.Hot ? 0.9f : 0.6f);
                    c.Text(k.Label, new RectangleF(x, y + (h - LineH()) / 2, w, LineH()), k.Hot ? bg : k.Color, Font, FontSize, false, "center");
                    break;
                }
            case BoxEl bx when bx.Frame != null:
                Glyph.Render.FramePainter.Draw(c, bx.Frame, Theme, new RectangleF(x, y, w, h), "", "", FontSize, bx.Hot);
                break;
            case SpectrumEl sp:
                {
                    int n = sp.Bands.Length;
                    if (n == 0) break;
                    float bwid = (w - sp.BarGap * (n - 1)) / n;
                    if (bwid < 1) break;
                    for (int i = 0; i < n; i++)
                    {
                        float v = Math.Clamp(sp.Bands[i], 0, 1);
                        float bh = Math.Max(1, v * h);
                        float bxp = x + i * (bwid + sp.BarGap);
                        var col = Rgba.Lerp(sp.Color, sp.Peak, v);
                        if (sp.Mirror) c.Fill(new RectangleF(bxp, y + (h - bh) / 2, bwid, bh), col);
                        else c.Fill(new RectangleF(bxp, y + h - bh, bwid, bh), col);
                    }
                    break;
                }
            case GridEl gd:
                {
                    var cw = ColumnWidths(gd);
                    float rh = LineH() * 1.12f;
                    for (int r = 0; r < gd.Rows.Count; r++)
                    {
                        float cx = x;
                        for (int col = 0; col < gd.Rows[r].Count; col++)
                        {
                            var cell = gd.Rows[r][col];
                            c.Text(cell.Text, new RectangleF(cx, y + r * rh, cw[col] - FontSize * 0.45f, rh), cell.Color, Font, FontSize, false, "right");
                            cx += cw[col];
                        }
                    }
                    break;
                }
        }
        var saved = textOverride;
        if (e is BoxEl hb && hb.Frame != null)
        {
            if (hb.Hot && hb.Frame.HoverText != "none") textOverride = Theme[hb.Frame.HoverText];
            else if (hb.Frame.TextColor != "none") textOverride = Theme[hb.Frame.TextColor];
        }
        foreach (var k in e.Children) PaintVector(k, c, ox, oy, bg, radius);
        textOverride = saved;
    }

    /// A meter's bar: one rounded bar, or separate blocks when it has segments.
    void Bar(Canvas c, RectangleF r, MeterEl m)
    {
        float v = Math.Clamp(m.Value, 0, 1);
        if (m.Segments > 0)
        {
            int n = m.Segments;
            float gap = Math.Max(1, MathF.Round(r.Height * 0.35f));
            float sw = (r.Width - gap * (n - 1)) / n;
            if (sw >= 1)
            {
                int fill = (int)MathF.Round(v * n);
                for (int i = 0; i < n; i++)
                    c.Fill(new RectangleF(r.X + i * (sw + gap), r.Y, sw, r.Height), i < fill ? m.Fill : m.Track);
                return;
            }
        }
        c.Fill(r, m.Track, r.Height / 2);
        if (v > 0.002f) c.Fill(new RectangleF(r.X, r.Y, Math.Max(r.Height, r.Width * v), r.Height), m.Fill, r.Height / 2);
    }

    /// Terminal themes: pictures that aren't drawn as characters are drawn smooth, in their cells.
    public void PaintPictures(El e, Canvas c)
    {
        if (e is ImageEl im && im.Style != "ascii")
        {
            var saveX = e.X; var saveY = e.Y; var saveW = e.W; var saveH = e.H;
            bool t = Terminal;
            e.X *= CellW; e.Y *= CellH; e.W *= CellW; e.H *= CellH;
            Terminal = false;
            PaintVector(e, c, 0, 0, Theme["bg"], 0);
            Terminal = t;
            e.X = saveX; e.Y = saveY; e.W = saveW; e.H = saveH;
        }
        foreach (var k in e.Children) PaintPictures(k, c);
    }

    static Rgba Lighter(Rgba c) => new((byte)Math.Min(255, c.R + 28), (byte)Math.Min(255, c.G + 28), (byte)Math.Min(255, c.B + 28), c.A);
}
