using System.ComponentModel;
using Glyph.Options;
using Glyph.Render;
using Glyph.Widgets;

namespace Glyph.Launcher;

/// theme.toml [launcher]: how the launcher looks in this theme.
internal sealed class LauncherLook
{
    [Description("Width in pixels."), Range(200, 3000)] public int Width { get; set; } = 680;
    [DisplayName("Results shown"), Range(1, 20)] public int MaxResults { get; set; } = 8;
    [DisplayName("From the top"), Description("Where it opens: 0 = top of the screen, 0.5 = middle."), Range(0, 0.8)] public float Top { get; set; } = 0.22f;
    [Description("Smooth style: text size compared with the theme's."), Range(0.5, 4)] public float Scale { get; set; } = 1.15f;
    [Description("Shown before what you type. Empty = > in terminal themes, › in smooth ones.")] public string Prompt { get; set; } = "";
    [Description("Shown while nothing is typed.")] public string Placeholder { get; set; } = "Search apps, shortcuts, files, maths…";
    [Description("Smooth style: the apps' own icons.")] public bool Icons { get; set; } = true;
    [Color, TypeConverter(typeof(ColorNameConverter))] public string Background { get; set; } = "surface";
    [Description("0 = invisible, 1 = solid."), Range(0, 1)] public float Opacity { get; set; } = 1f;
    [DisplayName("Border colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string BorderColor { get; set; } = "accent";
    [DisplayName("Border width"), Range(0, 16)] public float BorderWidth { get; set; } = 1;
    [DisplayName("Corner radius"), Description("-1 = the theme's panel corners."), Range(-1, 64)] public float CornerRadius { get; set; } = -1;
    [DisplayName("Text colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TextColor { get; set; } = "text";
    [DisplayName("Dim colour"), Description("The placeholder and the kind of each result."), Color, TypeConverter(typeof(ColorNameConverter))] public string DimColor { get; set; } = "dim";
    [DisplayName("Accent colour"), Description("The prompt, the cursor and the letters that matched."), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentColor { get; set; } = "accent";
    [DisplayName("Selected colour"), Description("Behind the selected result."), Color, TypeConverter(typeof(ColorNameConverter))] public string SelectedColor { get; set; } = "accent_dim";
    [DisplayName("Selected text"), Color, TypeConverter(typeof(ColorNameConverter))] public string SelectedText { get; set; } = "bright";

    [Category("Frames"), Description("Smooth style: a frame (Frames page) for the whole launcher instead of the plain box above. none = plain."), TypeConverter(typeof(FrameNameConverter))] public string Frame { get; set; } = "none";
    [Category("Frames"), DisplayName("Row frame"), Description("A frame for each result. none = plain rows."), TypeConverter(typeof(FrameNameConverter))] public string RowFrame { get; set; } = "none";
    [Category("Frames"), DisplayName("Selected frame"), Description("A frame for the selected result. auto = the row frame."), TypeConverter(typeof(FrameNameConverter))] public string SelectedFrame { get; set; } = "auto";
    [Category("Frames"), DisplayName("Row gap"), Description("Pixels between framed rows."), Range(0, 50)] public float RowGap { get; set; } = 4;
    [Category("Frames"), Description("Numbers in front of the results (1, 2, 3...).")] public bool Numbers { get; set; }
    [Category("Frames"), DisplayName("Number frame"), Description("A frame for the numbers. none = just the number."), TypeConverter(typeof(FrameNameConverter))] public string NumberFrame { get; set; } = "none";
    [Category("Frames"), Description("Bend the list: rows near the top and bottom move right by up to this many pixels (negative = left), the middle stays."), Range(-300, 300)] public float Curve { get; set; }
    [Category("Frames"), DisplayName("Kind under the name"), Description("Show what each result is (app, file...) as a small tag under its name instead of on the right.")] public bool KindBelow { get; set; }
    [Category("Frames"), Description("A big heading above the search line, e.g. APPS. Empty = none.")] public string Heading { get; set; } = "";
    [Category("Frames"), DisplayName("Heading note"), Description("Small text after the heading.")] public string HeadingNote { get; set; } = "";
    [Category("Frames"), DisplayName("Heading font"), Description("Empty = the theme's font."), TypeConverter(typeof(FontNameConverter))] public string HeadingFont { get; set; } = "";
    [Category("Frames"), DisplayName("Top line"), Description("A small line at the very top. {count} = how many results. Empty = none.")] public string TopLine { get; set; } = "";
    [Category("Frames"), DisplayName("Bottom line"), Description("A small line at the bottom, e.g. key hints. {count} = how many results, {selected} = which one is selected. Empty = none.")] public string BottomLine { get; set; } = "";
    [Category("Frames"), DisplayName("Letter spacing"), Description("Extra pixels between letters of the heading, top and bottom lines."), Range(-5, 50)] public float Spacing { get; set; }
}

/// What the launcher shows right now (made on the UI thread, drawn on the render thread).
internal sealed class LauncherState
{
    public string Query = "";
    public List<Item> Results = new();
    public int Selected, Top, Hover = -1;
    public string Signature => Query + "|" + Selected + "|" + Top + "|" + Hover + "|" + string.Join(",", Results.Select(r => r.Id + r.Title + (IconCache.Has(r.Icon) ? "i" : "")));
}

/// Draws the launcher in the theme's style.
internal sealed class LauncherScene
{
    public Theme Theme = new("none");
    public LauncherLook O = new();
    /// rows' rectangles (window pixels) from the last layout, for the mouse
    public volatile RectangleF[] RowRects = Array.Empty<RectangleF>();
    public volatile float ContentHeight;

    float R => O.CornerRadius >= 0 ? O.CornerRadius : Theme.Options.Ui.CornerRadius;
    string Prompt => O.Prompt.Length > 0 ? O.Prompt : Theme.Smooth ? "›" : ">";

    // ───────────── smooth ─────────────

    bool Framed => Theme.Frame(O.Frame) != null || Theme.Frame(O.RowFrame) != null || O.Heading.Length > 0 || O.Numbers || O.TopLine.Length > 0 || O.BottomLine.Length > 0;

    string Line(string fmt, LauncherState st) => Glyph.Data.Fmt.Text(fmt, (n, s) => n switch
    {
        "count" => st.Results.Count.ToString(s.Length > 0 ? s : "0", System.Globalization.CultureInfo.InvariantCulture),
        "selected" => (st.Results.Count == 0 ? 0 : st.Selected + 1).ToString(s.Length > 0 ? s : "0", System.Globalization.CultureInfo.InvariantCulture),
        _ => Glyph.Data.Stats.Value(n, s),
    });

    /// The framed look: heading, search line, framed (and numbered, curved) rows, hint lines.
    void PaintFramed(Canvas c, LauncherState st, int width)
    {
        var ui = Theme.Options.Ui;
        var t = Theme;
        float fs = ui.FontSize * O.Scale;
        var font = ui.Font;
        var panelF = t.Frame(O.Frame);
        var rowF = t.Frame(O.RowFrame);
        var selF = O.SelectedFrame == "auto" ? rowF : t.Frame(O.SelectedFrame);
        var numF = t.Frame(O.NumberFrame);
        float pad = panelF != null ? (panelF.Padding >= 0 ? panelF.Padding : ui.Padding) : ui.Padding;
        float small = fs * 0.78f, lineH = small * 1.6f;
        float headSize = fs * 2.0f, headH = O.Heading.Length > 0 ? headSize * 1.35f : 0;
        float inputH = fs * 2.1f;
        float rowH = O.KindBelow ? fs * 2.75f : fs * 2.15f;
        float gap = rowF != null ? O.RowGap : 0;
        float numW = O.Numbers ? rowH * 0.62f + fs * 0.8f : 0;
        float curve = Math.Abs(O.Curve);
        int n = Math.Min(st.Results.Count - st.Top, O.MaxResults);
        float h = pad * 2 + (O.TopLine.Length > 0 ? lineH : 0) + headH + inputH + (n > 0 ? pad * 0.6f + n * rowH + Math.Max(0, n - 1) * gap : 0) + (O.BottomLine.Length > 0 ? lineH + pad * 0.4f : 0);
        var outs = panelF != null ? FramePainter.Outside(panelF) : new FramePainter.Insets(0, 0, 0, 0);
        var panel = new RectangleF(6 + outs.L, 6 + outs.T, width - 12 - outs.L - outs.R, h);
        if (panelF != null) FramePainter.Draw(c, panelF, t, panel, "", "", fs);
        else
        {
            if (ui.Shadow > 0) c.Shadow(panel, R, ui.Shadow);
            c.Fill(panel, t[O.Background], R, O.Opacity);
            if (O.BorderWidth > 0) c.Stroke(panel, t[O.BorderColor], O.BorderWidth, R);
        }
        float x = panel.X + pad, y = panel.Y + pad, right = panel.Right - pad;
        var dim = t[O.DimColor]; var acc = t[O.AccentColor]; var text = t[O.TextColor];
        if (O.TopLine.Length > 0)
        {
            FramePainter.DrawText(c, Line(O.TopLine, st), x, y, lineH, dim, font, small, false, O.Spacing);
            y += lineH;
        }
        if (headH > 0)
        {
            var hf = O.HeadingFont.Length > 0 ? O.HeadingFont : font;
            float hw = FramePainter.MeasureText(c, O.Heading, hf, headSize, true, O.Spacing * 2);
            FramePainter.DrawText(c, O.Heading, x, y, headH, t[O.SelectedText], hf, headSize, true, O.Spacing * 2);
            if (O.HeadingNote.Length > 0) FramePainter.DrawText(c, O.HeadingNote, x + hw + fs * 0.8f, y + headH * 0.12f, headH, dim, font, small, false, O.Spacing);
            y += headH;
        }
        // the search line, with a ruler under it
        float big = fs * 1.05f;
        float ix = x + numW;
        float pw = c.Measure(Prompt, font, big, false);
        c.Text(Prompt, new RectangleF(ix, y + (inputH - big * 1.36f) / 2, pw + 6, big * 1.5f), acc, font, big);
        float tx = ix + pw + fs * 0.6f;
        if (st.Query.Length == 0) c.Text(O.Placeholder, new RectangleF(tx, y + (inputH - big * 1.36f) / 2, right - tx, big * 1.5f), dim, font, big);
        float qw = st.Query.Length > 0 ? c.Measure(st.Query, font, big, false) : 0;
        if (st.Query.Length > 0) c.Text(st.Query, new RectangleF(tx, y + (inputH - big * 1.36f) / 2, qw + 8, big * 1.5f), text, font, big);
        c.Fill(new RectangleF(tx + qw + 2, y + (inputH - big * 1.2f) / 2, Math.Max(2, fs * 0.13f), big * 1.2f), acc);
        float ly = y + inputH - 2;
        c.Fill(new RectangleF(x, ly, right - x, 1), t[O.BorderColor], 0, 0.5f);
        for (float q = x; q <= right; q += 7) c.Fill(new RectangleF(q, ly, 1, ((int)((q - x) / 7) % 5 == 0) ? 5 : 3), t[O.BorderColor], 0, 0.5f);
        y += inputH + pad * 0.6f;

        var rects = new RectangleF[n];
        for (int k = 0; k < n; k++)
        {
            var it = st.Results[st.Top + k];
            bool sel = st.Top + k == st.Selected, hov = st.Top + k == st.Hover;
            float tk = n <= 1 ? 0.5f : k / (float)(n - 1);
            float bend = O.Curve * (1 - MathF.Sin(MathF.PI * tk));
            float rx = x + numW + Math.Max(0, bend) + (O.Curve < 0 ? curve + bend : 0);
            var row = new RectangleF(rx, y, right - rx - curve, rowH);
            rects[k] = row;
            if (O.Numbers)
            {
                float ns = rowH * 0.62f;
                var nr = new RectangleF(rx - numW + fs * 0.2f, y + (rowH - ns) / 2, ns, ns);
                if (numF != null) FramePainter.Draw(c, numF, t, nr, "", "", fs, sel);
                var num = (st.Top + k + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                c.Text(num, new RectangleF(nr.X, nr.Y + (ns - small * 1.36f) / 2, ns, small * 1.5f), sel ? t[O.SelectedText] : dim, font, small, false, "center");
            }
            var f = sel ? selF : rowF;
            if (f != null) FramePainter.Draw(c, f, t, row, "", "", fs, hov && !sel);
            else if (sel) c.Fill(row, t[O.SelectedColor], Math.Min(R, rowH / 2) * 0.7f, 0.9f);
            else if (hov) c.Fill(row, t[O.SelectedColor], Math.Min(R, rowH / 2) * 0.7f, 0.35f);
            float inner = f != null ? (f.PaddingX >= 0 ? f.PaddingX : f.Padding >= 0 ? f.Padding : pad * 0.5f) + 6 : pad * 0.5f;
            float cx = row.X + inner;
            float isz = Math.Min(rowH * 0.62f, fs * 1.6f);
            if (O.Icons && it.Icon.Length > 0)
            {
                var px = IconCache.Pixels(it.Icon, (int)isz);
                if (px != null) c.Draw(c.Bitmap($"launch|{it.Icon}|{(int)isz}", () => px.Value), new RectangleF(cx, y + (rowH - isz) / 2, isz, isz));
                cx += isz + fs * 0.7f;
            }
            float kw = c.Measure(it.Kind.ToUpperInvariant(), font, small * 0.85f, false);
            if (O.KindBelow)
            {
                float ty = y + rowH * 0.12f;
                Title(c, it, cx, ty, row.Right - inner - cx, fs, sel, font);
                var kr = new RectangleF(cx, ty + fs * 1.42f, kw + 8, small * 1.2f);
                c.Stroke(kr, sel ? acc : t[O.BorderColor], 1);
                c.Text(it.Kind.ToUpperInvariant(), new RectangleF(kr.X + 4, kr.Y + (kr.Height - small * 0.85f * 1.36f) / 2, kw + 4, small * 1.3f), sel ? acc : dim, font, small * 0.85f);
            }
            else
            {
                c.Text(it.Kind, new RectangleF(row.Right - kw - inner, y + (rowH - small * 1.36f) / 2, kw + 4, small * 1.5f), dim, font, small * 0.85f);
                Title(c, it, cx, y + (rowH - fs * 1.36f) / 2, row.Right - kw - inner * 2 - cx, fs, sel, font);
            }
            y += rowH + gap;
        }
        if (O.BottomLine.Length > 0)
        {
            y += pad * 0.4f - gap;
            FramePainter.DrawText(c, Line(O.BottomLine, st), x, y, lineH, dim, font, small, false, O.Spacing);
        }
        RowRects = rects;
        ContentHeight = panel.Bottom + 6 + outs.B;
    }

    public void PaintVector(Canvas c, LauncherState st, int width)
    {
        if (!Theme.Smooth) return;
        if (Framed) { PaintFramed(c, st, width); return; }
        var ui = Theme.Options.Ui;
        float fs = ui.FontSize * O.Scale, pad = ui.Padding, inputH = fs * 2.3f, rowH = fs * 2.15f;
        int n = Math.Min(st.Results.Count - st.Top, O.MaxResults);
        float h = pad * 2 + inputH + (n > 0 ? pad * 0.6f + n * rowH : 0);
        var panel = new RectangleF(6, 6, width - 12, h);   // room for the shadow
        if (ui.Shadow > 0) c.Shadow(panel, R, ui.Shadow);
        c.Fill(panel, Theme[O.Background], R, O.Opacity);
        if (O.BorderWidth > 0) c.Stroke(panel, Theme[O.BorderColor], O.BorderWidth, R);

        // the line you type in
        float x = panel.X + pad, y = panel.Y + pad;
        float big = fs * 1.25f;
        var font = ui.Font;
        float pw = c.Measure(Prompt, font, big, false);
        c.Text(Prompt, new RectangleF(x, y + (inputH - big * 1.36f) / 2, pw + 6, big * 1.5f), Theme[O.AccentColor], font, big);
        float tx = x + pw + fs * 0.6f;
        if (st.Query.Length == 0)
            c.Text(O.Placeholder, new RectangleF(tx, y + (inputH - big * 1.36f) / 2, panel.Right - tx - pad, big * 1.5f), Theme[O.DimColor], font, big);
        float qw = st.Query.Length > 0 ? c.Measure(st.Query, font, big, false) : 0;
        if (st.Query.Length > 0) c.Text(st.Query, new RectangleF(tx, y + (inputH - big * 1.36f) / 2, qw + 8, big * 1.5f), Theme[O.TextColor], font, big);
        c.Fill(new RectangleF(tx + qw + 2, y + (inputH - big * 1.2f) / 2, Math.Max(2, fs * 0.13f), big * 1.2f), Theme[O.AccentColor]);

        var rects = new RectangleF[n];
        if (n > 0)
        {
            float ly = y + inputH + pad * 0.3f;
            c.Fill(new RectangleF(panel.X + pad, ly, panel.Width - pad * 2, 1), Theme[O.BorderColor], 0, 0.35f);
            float ry = ly + pad * 0.3f;
            for (int k = 0; k < n; k++)
            {
                var it = st.Results[st.Top + k];
                bool sel = st.Top + k == st.Selected;
                var row = new RectangleF(panel.X + pad * 0.5f, ry, panel.Width - pad, rowH);
                rects[k] = row;
                if (sel) c.Fill(row, Theme[O.SelectedColor], Math.Min(R, rowH / 2) * 0.7f, 0.9f);
                else if (st.Top + k == st.Hover) c.Fill(row, Theme[O.SelectedColor], Math.Min(R, rowH / 2) * 0.7f, 0.35f);
                float ix = row.X + pad * 0.5f;
                float isz = rowH * 0.62f;
                if (O.Icons)
                {
                    var px = IconCache.Pixels(it.Icon, (int)isz);
                    if (px != null) c.Draw(c.Bitmap($"launch|{it.Icon}|{(int)isz}", () => px.Value), new RectangleF(ix, ry + (rowH - isz) / 2, isz, isz));
                    ix += isz + fs * 0.7f;
                }
                var kind = it.Kind;
                float kw = c.Measure(kind, font, fs * 0.86f, false);
                c.Text(kind, new RectangleF(row.Right - kw - pad * 0.8f, ry + (rowH - fs * 0.86f * 1.36f) / 2, kw + 4, fs * 1.4f), Theme[O.DimColor], font, fs * 0.86f);
                Title(c, it, ix, ry + (rowH - fs * 1.36f) / 2, row.Right - kw - pad * 1.6f - ix, fs, sel, font);
                ry += rowH;
            }
        }
        RowRects = rects;
        ContentHeight = panel.Bottom + 6;
    }

    /// The title with the letters that matched in the accent colour.
    void Title(Canvas c, Item it, float x, float y, float maxW, float fs, bool sel, string font)
    {
        var text = it.Title;
        var baseCol = sel ? Theme[O.SelectedText] : Theme[O.TextColor];
        var hit = new HashSet<int>(it.Hits);
        float cx = x;
        int i = 0;
        while (i < text.Length)
        {
            bool h = hit.Contains(i);
            int j = i;
            while (j < text.Length && hit.Contains(j) == h) j++;
            var part = text[i..j];
            float w = c.Measure(part, font, fs, h);
            if (cx + w > x + maxW) { c.Text("…", new RectangleF(cx, y, fs * 2, fs * 1.5f), baseCol, font, fs); break; }
            c.Text(part, new RectangleF(cx, y, w + 4, fs * 1.5f), h ? Theme[O.AccentColor] : baseCol, font, fs, h);
            cx += w;
            i = j;
        }
    }


    // ───────────── terminal ─────────────

    public void PaintCells(Grid g, LauncherState st, int cellW, int cellH)
    {
        g.Clear(0);
        if (Theme.Smooth) return;
        int cols = g.W;
        int n = Math.Min(st.Results.Count - st.Top, O.MaxResults);
        int rows = 3 + (n > 0 ? n + 1 : 0);
        var bg = Theme[O.Background];
        g.FillRect(0, 0, cols, rows, Px.Alpha(bg.Packed, O.Opacity));
        if (O.BorderWidth > 0) g.Box(0, 0, cols, rows, Theme[O.BorderColor].Packed);
        uint text = Theme[O.TextColor].Packed, dim = Theme[O.DimColor].Packed, acc = Theme[O.AccentColor].Packed;

        // input line
        var prompt = Prompt + " ";
        g.Text(2, 1, prompt, acc);
        int qx = 2 + prompt.Length;
        int room = cols - qx - 3;
        var q = st.Query.Length > room ? "…" + st.Query[^(room - 1)..] : st.Query;
        if (q.Length == 0) g.Text(qx + 1, 1, Cut(O.Placeholder, room - 1), dim);
        else g.Text(qx, 1, q, text);
        g.Put(qx + q.Length, 1, '█', acc);

        var rects = new RectangleF[n];
        if (n > 0)
        {
            for (int x = 1; x < cols - 1; x++) g.Put(x, 2, '─', Theme[O.BorderColor].Packed);
            for (int k = 0; k < n; k++)
            {
                var it = st.Results[st.Top + k];
                int y = 3 + k;
                bool sel = st.Top + k == st.Selected;
                rects[k] = new RectangleF(cellW, y * cellH, (cols - 2) * cellW, cellH);
                if (sel) g.FillRect(1, y, cols - 2, 1, Theme[O.SelectedColor].Packed);
                else if (st.Top + k == st.Hover) g.FillRect(1, y, cols - 2, 1, Px.Alpha(Theme[O.SelectedColor].Packed, 0.4f));
                g.Text(2, y, sel ? "▶" : " ", acc);
                var kind = it.Kind;
                int kx = cols - 2 - kind.Length;
                g.Text(kx, y, kind, dim);
                int tw = kx - 5;
                var title = Cut(it.Title, tw);
                var hit = new HashSet<int>(it.Hits);
                uint tc = sel ? Theme[O.SelectedText].Packed : text;
                for (int i = 0; i < title.Length; i++) g.Put(4 + i, y, title[i], hit.Contains(i) && !(title.EndsWith('…') && i == title.Length - 1) ? acc : tc);
            }
            if (st.Results.Count > O.MaxResults)
            {
                var more = $" {st.Selected + 1}/{st.Results.Count} ";
                g.Text(cols - 2 - more.Length, rows - 1, more, dim);
            }
        }
        RowRects = rects;
        ContentHeight = rows * cellH;
    }

    static string Cut(string s, int n) => n <= 0 ? "" : s.Length <= n ? s : s[..Math.Max(0, n - 1)] + "…";
}

/// Apps' icons for the launcher, loaded in the background (pulling an icon out of Windows takes a few ms).
internal static class IconCache
{
    static readonly Dictionary<string, Bitmap?> icons = new();
    static readonly Dictionary<string, (byte[], int, int)> pixels = new();
    static readonly object gate = new();
    static readonly Queue<string> wanted = new();
    static readonly HashSet<string> asked = new();   // handed to the app scan already
    static Thread? worker;
    public static event Action? Loaded;

    static IconCache() => AppScan.Done += () => { lock (gate) { foreach (var k in icons.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) icons.Remove(k); } Loaded?.Invoke(); };

    public static bool Has(string key) { if (key.Length == 0) return false; lock (gate) return icons.TryGetValue(key, out var b) && b != null; }

    /// asks for the icons of these results (most important first)
    public static void Want(IEnumerable<string> keys)
    {
        lock (gate)
        {
            foreach (var k in keys) if (k.Length > 0 && !icons.ContainsKey(k) && !wanted.Contains(k)) wanted.Enqueue(k);
            if (wanted.Count == 0 || worker != null) return;
            worker = new Thread(Work) { IsBackground = true, Name = "Glyph launcher icons" };
            worker.Start();
        }
    }

    /// Only reads saved PNGs: icons not saved yet are made by the app scan (a separate program), so the
    /// Windows shell code that pulls icons out of apps never loads into Glyph itself.
    static void Work()
    {
        var missing = new List<string>();
        while (true)
        {
            string k;
            lock (gate)
            {
                if (wanted.Count == 0)
                {
                    worker = null;
                    if (missing.Count > 0) AppScan.Want(missing);
                    return;
                }
                k = wanted.Dequeue();
            }
            Bitmap? b = null;
            var file = Path.Combine(Paths.IconCache, new Shortcut { Target = k }.IconKey + ".png");
            try
            {
                if (File.Exists(file)) { using var s = File.OpenRead(file); using var tmp = new Bitmap(s); b = new Bitmap(tmp); }
                else lock (gate) if (asked.Add(k)) missing.Add(k);
            }
            catch { }
            lock (gate)
            {
                if (icons.Count > 300) { foreach (var v in icons.Values) v?.Dispose(); icons.Clear(); pixels.Clear(); }
                icons[k] = b;
            }
            if (b != null) Loaded?.Invoke();
        }
    }

    public static (byte[], int, int)? Pixels(string key, int size)
    {
        if (key.Length == 0) return null;
        lock (gate)
        {
            var pk = key + "|" + size;
            if (pixels.TryGetValue(pk, out var p)) return p;
            if (!icons.TryGetValue(key, out var b) || b == null) return null;
            using var fit = IconStyler.Fit(b, size);
            var px = IconStyler.Pixels(fit, "original", new Gradient(new List<Rgba> { new(255, 255, 255) }), new Rgba(255, 255, 255));
            return pixels[pk] = (px, size, size);
        }
    }

    /// gives the memory back when the launcher has been closed a while
    public static void Clear() { lock (gate) { foreach (var v in icons.Values) v?.Dispose(); icons.Clear(); pixels.Clear(); } }
}
