using Glyph.Render;

namespace Glyph.Widgets;

/// A grid of shortcuts that open programs, files, folders or websites.
internal sealed class ShortcutsWidget : Widget
{
    sealed class Item
    {
        public Shortcut S = null!;
        public bool Loaded;              // picture fetched (or found missing)
        public bool Missing;             // no picture: draw a letter tile
        public byte[]? Px;               // styled picture for smooth drawing (premultiplied BGRA)
        public int PxSize;
        public int[]? AsciiCh; public uint[]? AsciiFg;   // character version
        public RectangleF Rect;          // pixels
        public int CX, CY;               // cells (terminal)
    }

    readonly ShortcutsOptions o;
    readonly Theme theme;
    readonly List<Item> items;
    readonly Gradient tint;
    readonly Rgba mono, label, hover, panel, border, text, accent;
    string style = "";
    int cols, rows, ic, ir, iwc, ihc, cx0, cy0, wc, hc;
    bool terminal;
    int loadedCount;

    public ShortcutsWidget(ShortcutsOptions o, Theme theme, List<Shortcut> all)
    {
        this.o = o; this.theme = theme;
        items = all.Where(s => o.Group.Length == 0 || s.Group.Equals(o.Group, StringComparison.OrdinalIgnoreCase))
                   .Select(s => new Item { S = s }).ToList();
        tint = new Gradient(theme, o.TintColors);
        mono = theme[o.IconColor]; label = theme[o.LabelColor]; hover = theme[o.HoverColor];
        var ui = theme.Options.Ui;
        panel = o.BoxColor(theme); border = o.BoxBorder(theme); text = theme[ui.TextColor]; accent = theme[ui.AccentColor];
    }

    public override bool Animated => loadedCount < items.Count;   // keep drawing until every icon is in
    public override string Signature(WidgetEnv e) => $"{Hover}|{loadedCount}/{items.Count}";

    string Style(bool smooth) => o.IconStyle == "auto" ? (smooth ? "original" : "ascii") : o.IconStyle;

    public override void Layout(WidgetEnv e)
    {
        terminal = !e.Smooth;
        style = Style(e.Smooth);
        int n = Math.Max(1, items.Count);
        cols = Math.Clamp(o.Columns, 1, n);
        rows = (n + cols - 1) / cols;
        if (terminal)
        {
            ic = Math.Max(2, (int)MathF.Round(o.IconSize / (float)e.CellW));
            ir = Math.Max(1, (int)MathF.Round(o.IconSize / (float)e.CellH));
            iwc = Math.Max(ic, (int)MathF.Round(o.ItemWidth / (float)e.CellW));
            ihc = ir + (o.ShowLabels ? o.LabelLines : 0);
            int sx = Math.Max(1, (int)MathF.Round(o.Spacing / (float)e.CellW)), sy = Math.Max(0, (int)MathF.Round(o.Spacing / (float)e.CellH));
            int padX = o.Panel ? 2 : 0, padY = o.Panel ? 1 : 0;
            wc = cols * iwc + (cols - 1) * sx + 2 * padX;
            hc = rows * ihc + (rows - 1) * sy + 2 * padY + (o.Panel && o.Title.Length > 0 ? 1 : 0);
            var p = Place(o.Anchor, wc * e.CellW, hc * e.CellH, e.Area, o.OffsetX, o.OffsetY);
            cx0 = (int)MathF.Round(p.X / e.CellW); cy0 = (int)MathF.Round(p.Y / e.CellH);
            Bounds = new RectangleF(cx0 * e.CellW, cy0 * e.CellH, wc * e.CellW, hc * e.CellH);
            int top = padY + (o.Panel && o.Title.Length > 0 ? 1 : 0);
            for (int i = 0; i < items.Count; i++)
            {
                int c = i % cols, r = i / cols;
                var it = items[i];
                it.CX = cx0 + padX + c * (iwc + sx);
                it.CY = cy0 + top + r * (ihc + sy);
                it.Rect = new RectangleF(it.CX * e.CellW, it.CY * e.CellH, iwc * e.CellW, ihc * e.CellH);
            }
        }
        else
        {
            var ui = e.Theme.Options.Ui;
            float lineH = ui.FontSize * 1.34f;
            float labelH = o.ShowLabels ? o.LabelLines * lineH + 2 : 0;
            float itemW = Math.Max(o.ItemWidth, o.IconSize + 8), itemH = o.IconSize + 10 + labelH;
            float pad = o.Panel ? ui.Padding : 0;
            float titleH = o.Title.Length > 0 ? ui.FontSize * 1.9f : 0;
            float W = cols * itemW + (cols - 1) * o.Spacing + 2 * pad, H = rows * itemH + (rows - 1) * o.Spacing + 2 * pad + titleH;
            var p = Place(o.Anchor, W, H, e.Area, o.OffsetX, o.OffsetY);
            Bounds = new RectangleF(p.X, p.Y, W, H);
            for (int i = 0; i < items.Count; i++)
            {
                int c = i % cols, r = i / cols;
                items[i].Rect = new RectangleF(p.X + pad + c * (itemW + o.Spacing), p.Y + pad + titleH + r * (itemH + o.Spacing), itemW, itemH);
            }
        }
    }

    /// Fetches and styles at most one picture per frame, so the desktop never stalls.
    void LoadOne()
    {
        var it = items.FirstOrDefault(i => !i.Loaded);
        if (it == null) return;
        it.Loaded = true; loadedCount++;
        using var src = IconSource.Get(it.S, theme.Name);
        if (src == null) { it.Missing = true; return; }
        if (style == "ascii")
        {
            var (ch, fg) = IconStyler.Ascii(src, ic, ir, o.AsciiMode, tint);
            it.AsciiCh = ch; it.AsciiFg = fg;
        }
        else
        {
            int size = Math.Max(16, o.IconSize);
            using var fitted = IconStyler.Fit(src, size);
            it.Px = IconStyler.Pixels(fitted, style, tint, mono);
            it.PxSize = size;
        }
    }

    public override int HitTest(float x, float y)
    {
        for (int i = 0; i < items.Count; i++) if (items[i].Rect.Contains(x, y)) return i;
        return -1;
    }

    public override void Click(int item, int count, WidgetEnv e)
    {
        if (item < 0 || item >= items.Count) return;
        bool go = o.Launch == "click" ? count == 1 : count == 2;
        if (go) e.Launch?.Invoke(items[item].S);
    }

    // ---------------- terminal (characters) ----------------

    public override void DrawCells(Grid g, WidgetEnv e)
    {
        LoadOne();
        if (!terminal) return;
        var ui = e.Theme.Options.Ui;
        if (o.Panel)
        {
            g.FillRect(cx0, cy0, wc, hc, Px.Alpha(panel.Packed, o.BoxOpacity(theme)));
            if (o.BoxBorderWidth(theme) != 0) g.Box(cx0, cy0, wc, hc, border.Packed);
            if (o.Title.Length > 0) g.Text(cx0 + 2, cy0, $" {Trim(o.Title, wc - 6)} ", accent.Packed);
        }
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            bool hot = i == Hover;
            if (hot) g.FillRect(it.CX, it.CY, iwc, ihc, Px.Alpha(hover.Packed, 0.16f));
            int ix = it.CX + (iwc - ic) / 2;
            if (style == "ascii")
            {
                if (it.AsciiCh != null)
                {
                    for (int y = 0; y < ir; y++)
                        for (int x = 0; x < ic; x++)
                        {
                            int k = y * ic + x;
                            if (it.AsciiCh[k] == 32) continue;
                            uint fg = it.AsciiFg![k];
                            g.Put(ix + x, it.CY + y, it.AsciiCh[k], hot ? Px.Lerp(fg, hover.Packed | 0xFF000000, 0.35f) : fg);
                        }
                }
                else if (it.Missing || !it.Loaded) LetterTile(g, ix, it.CY, it.S.ToString(), hot);
            }
            if (o.ShowLabels)
            {
                var lines = Wrap(it.S.ToString(), iwc, o.LabelLines);
                for (int l = 0; l < lines.Count; l++)
                {
                    var s = lines[l];
                    int lx = it.CX + (iwc - s.Length) / 2, ly = it.CY + ir + l;
                    if (hot) { g.FillRect(lx, ly, s.Length, 1, hover.Packed); g.Text(lx, ly, s, e.Theme["bg"].Packed); }
                    else g.Text(lx, ly, s, label.Packed);
                }
            }
        }
    }

    void LetterTile(Grid g, int x, int y, string name, bool hot)
    {
        uint c = (hot ? hover : mono).Packed;
        if (ir >= 3 && ic >= 3) g.Box(x, y, ic, ir, c);
        var letter = name.Length > 0 ? char.ToUpperInvariant(name[0]) : '?';
        g.Put(x + ic / 2, y + ir / 2, letter, c);
    }

    static string Trim(string s, int max) => max <= 1 ? "" : s.Length <= max ? s : s[..(max - 1)] + "…";

    /// Splits a name over up to `lines` lines of `width` characters (word-wise, with … if cut).
    static List<string> Wrap(string s, int width, int lines)
    {
        var res = new List<string>();
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cur = "";
        int wi = 0;
        while (wi < words.Length && res.Count < lines)
        {
            var w = words[wi];
            var next = cur.Length == 0 ? w : cur + " " + w;
            if (next.Length <= width) { cur = next; wi++; continue; }
            if (cur.Length == 0) { cur = w[..Math.Min(width, w.Length)]; words[wi] = w[cur.Length..]; if (words[wi].Length == 0) wi++; }
            res.Add(cur); cur = "";
        }
        if (cur.Length > 0 && res.Count < lines) res.Add(cur);
        if (wi < words.Length && res.Count > 0) res[^1] = Trim(res[^1] + " " + string.Join(' ', words[wi..]), width);
        return res.Count == 0 ? new List<string> { "" } : res;
    }

    // ---------------- smooth (shapes) ----------------

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        var ui = e.Theme.Options.Ui;
        if (!terminal && o.Panel)
        {
            c.Shadow(Bounds, o.BoxRadius(theme), o.BoxShadow(theme));
            c.Fill(Bounds, panel, o.BoxRadius(theme), o.BoxOpacity(theme));
            c.Stroke(Bounds, border, o.BoxBorderWidth(theme), o.BoxRadius(theme));
            if (o.Title.Length > 0)
                c.Text(o.Title, new RectangleF(Bounds.X + ui.Padding, Bounds.Y + ui.Padding * 0.7f, Bounds.Width - 2 * ui.Padding, ui.FontSize * 1.6f), accent, ui.Font, ui.FontSize, ui.BoldTitles);
        }
        if (style == "ascii") return;  // drawn with characters
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            bool hot = i == Hover;
            RectangleF iconR;
            if (terminal)
            {
                // picture icons in a terminal theme sit in the icon cells
                float w = ic * e.CellW, h = ir * e.CellH, s = Math.Min(w, h);
                iconR = new RectangleF(it.Rect.X + (it.Rect.Width - s) / 2, it.Rect.Y + (h - s) / 2, s, s);
            }
            else
            {
                if (hot) c.Fill(it.Rect, hover, ui.CornerRadius * 0.6f, 0.22f);
                iconR = new RectangleF(it.Rect.X + (it.Rect.Width - o.IconSize) / 2, it.Rect.Y + 5, o.IconSize, o.IconSize);
            }
            if (it.Px != null)
            {
                var bmp = c.Bitmap($"sc|{it.S.IconKey}|{it.S.Image}|{style}|{it.PxSize}|{string.Join(',', o.TintColors)}|{o.IconColor}|{theme.Name}", () => (it.Px, it.PxSize, it.PxSize));
                c.Draw(bmp, iconR, hot || terminal ? 1f : 0.95f);
            }
            else if (it.Missing || !it.Loaded)
            {
                c.Fill(iconR, accent, iconR.Width * 0.22f, 0.9f);
                var letter = it.S.ToString().Length > 0 ? it.S.ToString()[..1].ToUpperInvariant() : "?";
                c.Text(letter, new RectangleF(iconR.X, iconR.Y + iconR.Height * 0.18f, iconR.Width, iconR.Height), e.Theme["bg"], ui.Font, iconR.Height * 0.5f, true, "center");
            }
            if (!terminal && o.ShowLabels)
            {
                float lineH = ui.FontSize * 1.34f;
                var lr = new RectangleF(it.Rect.X + 2, iconR.Bottom + 4, it.Rect.Width - 4, o.LabelLines * lineH + 2);
                c.Text(it.S.ToString(), lr, hot ? text : label, ui.Font, ui.FontSize, false, "center", o.LabelLines > 1);
            }
        }
    }
}
