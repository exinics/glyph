using System.ComponentModel;
using System.Drawing.Imaging;
using Glyph.Options;
using Glyph.Render;
using Vortice.Mathematics;
using Vortice.Direct2D1;

namespace Glyph.Widgets;

internal sealed class ThemesOptions : WidgetOptions
{
    public override string Type => "themes";
    public ThemesOptions() { Anchor = "right"; OffsetX = 40; OffsetY = 0; }
    [Category("Themes"), Description("Title over the cards. Empty = none.")] public string Title { get; set; } = "THEMES";
    [Category("Themes"), Description("Which themes, in order (folder names). Empty = all of them.")]
    [TypeConverter(typeof(ListConverter))] public string[] Show { get; set; } = Array.Empty<string>();
    [Category("Themes"), Description("Width of the panel, in pixels."), Range(80, 3000)] public float Width { get; set; } = 260;
    [Category("Themes"), Description("Cards side by side."), Range(1, 10)] public int Columns { get; set; } = 1;
    [Category("Themes"), DisplayName("Card height"), Range(24, 600)] public float CardHeight { get; set; } = 72;
    [Category("Themes"), DisplayName("Card gap"), Range(0, 100)] public float CardGap { get; set; } = 10;
    [Category("Themes"), DisplayName("Colour dots"), Description("Which of each theme's colours to show as little squares on its card."), TypeConverter(typeof(ListConverter))]
    public string[] Dots { get; set; } = { "accent", "accent_bright", "bright" };
    [Category("Themes"), DisplayName("Picture dim"), Description("Darken each card's picture (the theme's wallpaper) so the name stays readable: 0 = not at all, 1 = black."), Range(0, 1)] public float Dim { get; set; } = 0.35f;
    [Category("Themes"), DisplayName("Card frame"), Description("A frame for the cards (Frames page). none = plain rounded cards."), TypeConverter(typeof(FrameNameConverter))] public string CardFrame { get; set; } = "none";
    [Category("Themes"), DisplayName("Current frame"), Description("A frame over the card of the theme in use (and the one under the mouse). none = an outline in the accent colour."), TypeConverter(typeof(FrameNameConverter))] public string CurrentFrame { get; set; } = "none";
    [Category("Themes"), DisplayName("Name colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string NameColor { get; set; } = "bright";
    [Category("Themes"), DisplayName("Current colour"), Description("The outline and tick on the theme in use (when Current frame is none)."), Color, TypeConverter(typeof(ColorNameConverter))] public string CurrentColor { get; set; } = "accent";
    [Category("Themes"), DisplayName("Corner radius"), Range(0, 60)] public float Radius { get; set; } = 10;
    public override string ToString() => $"themes{(Enabled ? "" : " (off)")}";
}

/// Cards for your themes: each with its wallpaper as a picture, a few of its colours and its name. The theme in use
/// is marked; clicking another card switches to it.
internal sealed class ThemesWidget : Widget
{
    readonly ThemesOptions o;
    readonly Theme t;
    readonly List<(string name, string picture, Rgba[] dots, Rgba bg, Rgba surface)> themes = new();
    readonly List<RectangleF> cards = new();
    readonly FrameOptions? panel, cardFrame, currentFrame;
    float titleH, fontSize;
    string font = "";
    static readonly Dictionary<string, (int w, int h)> thumbSizes = new();

    public ThemesWidget(ThemesOptions o, Theme t)
    {
        this.o = o; this.t = t;
        panel = t.Frame(o.Frame, t.Options.Ui.PanelFrame);
        cardFrame = t.Frame(o.CardFrame);
        currentFrame = t.Frame(o.CurrentFrame);
        var names = o.Show.Length > 0 ? o.Show.Select(s => s.Trim()).Where(s => s.Length > 0)
            : Directory.Exists(Paths.Themes) ? Directory.GetDirectories(Paths.Themes).Where(d => File.Exists(Path.Combine(d, "theme.toml"))).Select(Path.GetFileName).OfType<string>().OrderBy(n => n) : Enumerable.Empty<string>();
        foreach (var n in names)
        {
            if (!Directory.Exists(Paths.ThemeDir(n))) continue;
            try
            {
                var problems = new List<string>();
                var th = n == t.Name ? t : Theme.Load(n, problems);
                var wp = Glyph.Wallpaper.WallpaperOptions.Load(th, problems);
                var pic = wp.Backdrop.Type == "image" && wp.Backdrop.File.Length > 0 ? Paths.FindAsset(wp.Backdrop.File, n, "images") : "";
                themes.Add((n, pic, o.Dots.Select(d => th[d.Trim()]).ToArray(), th["bg"], th["surface"]));
            }
            catch { }
        }
    }

    public override void Layout(WidgetEnv e)
    {
        font = e.Painter.Font; fontSize = e.BaseFontSize;
        var pad = panel != null ? FramePainter.Space(panel, t, fontSize, o.Title.Length > 0) : new FramePainter.Insets(0, o.Title.Length > 0 ? fontSize * 2 : 0, 0, 0);
        titleH = pad.T;
        int cols = Math.Max(1, o.Columns), rows = (themes.Count + cols - 1) / cols;
        float w = WantW(o, o.Width, e);
        float innerW = w - pad.L - pad.R, cw = (innerW - (cols - 1) * o.CardGap) / cols;
        float h = pad.T + pad.B + rows * o.CardHeight + Math.Max(0, rows - 1) * o.CardGap;
        h = WantH(o, h, e);
        var pt = PlaceFor(o, w, h, e.Area);
        Bounds = new RectangleF(pt.X, pt.Y, w, h);
        cards.Clear();
        for (int i = 0; i < themes.Count; i++)
            cards.Add(new RectangleF(pt.X + pad.L + (i % cols) * (cw + o.CardGap), pt.Y + pad.T + (i / cols) * (o.CardHeight + o.CardGap), cw, o.CardHeight));
    }

    public override int HitTest(float x, float y)
    {
        for (int i = 0; i < cards.Count; i++) if (cards[i].Contains(x, y)) return i;
        return -1;
    }

    public override void Click(int item, int count, WidgetEnv e)
    {
        if (count == 1 && item >= 0 && item < themes.Count && themes[item].name != t.Name) e.Do?.Invoke("theme:" + themes[item].name);
    }

    public override IEnumerable<Part> Parts()
    {
        yield return new Part("Box", Bounds, new[] { "Placement", "Box", "Themes" }, "Frame");
        foreach (var r in cards) yield return new Part("Theme card", r, new[] { "Themes" }, "CardFrame");
    }

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        if (panel != null) FramePainter.Draw(c, panel, t, Bounds, o.Title, "", fontSize);
        else if (o.Title.Length > 0) FramePainter.DrawText(c, o.Title, Bounds.X, Bounds.Y, fontSize * 1.6f, t["dim"], font, fontSize * 0.9f, false, 2);
        var fac = c.Dc.Factory;
        for (int i = 0; i < themes.Count && i < cards.Count; i++)
        {
            var (name, pic, dots, bg, surface) = themes[i];
            var r = cards[i];
            bool current = name == t.Name, hot = Hover == i;
            // the picture (or the theme's own two colours), clipped to the card's rounded shape
            using var shape = fac.CreateRoundedRectangleGeometry(new RoundedRectangle(r, o.Radius, o.Radius));
            c.Dc.PushLayer(new Vortice.Direct2D1.LayerParameters1
            {
                ContentBounds = new Rect(r.X, r.Y, r.Width, r.Height), GeometricMask = shape, MaskAntialiasMode = Vortice.Direct2D1.AntialiasMode.PerPrimitive,
                MaskTransform = System.Numerics.Matrix3x2.Identity, Opacity = 1,
            }, null);
            if (pic.Length > 0 && File.Exists(pic)) Thumb(c, pic, r);
            else c.Gradient(r, new List<Rgba> { surface, bg }, 0, false);
            if (o.Dim > 0) c.Fill(r, new Rgba(0, 0, 0), 0, o.Dim * (hot ? 0.6f : 1));
            c.Dc.PopLayer();
            if (cardFrame != null) FramePainter.Draw(c, cardFrame, t, r, "", "", fontSize, hot, false);
            // colour dots and the name
            float dx = r.X + 12, dy = r.Y + 10, ds = Math.Max(8, fontSize * 0.85f);
            foreach (var d in dots) { c.Fill(new RectangleF(dx, dy, ds, ds), d, ds * 0.25f); dx += ds + 5; }
            c.Text(name, new RectangleF(r.X + 12, r.Bottom - fontSize * 2.1f, r.Width - 24, fontSize * 1.6f), t[o.NameColor], font, fontSize, true, "left");
            if (current || hot)
            {
                if (currentFrame != null)
                {
                    if (!current) c.Dc.PushLayer(new Vortice.Direct2D1.LayerParameters1 { ContentBounds = new Rect(r.X - 20, r.Y - 20, r.Width + 40, r.Height + 40), Opacity = 0.5f, MaskTransform = System.Numerics.Matrix3x2.Identity }, null);
                    FramePainter.Draw(c, currentFrame, t, r, "", "", fontSize, false, false);
                    if (!current) c.Dc.PopLayer();
                }
                else c.Stroke(r, t[o.CurrentColor], current ? 2 : 1, o.Radius, current ? 1 : 0.5f);
            }
            if (current) c.Text("{icon:check}", new RectangleF(r.Right - fontSize * 2.2f, r.Y + 6, fontSize * 1.8f, fontSize * 1.8f), t[o.CurrentColor], font, fontSize, false, "center");
        }
    }

    /// The wallpaper, shrunk once to a small picture (a full-size wallpaper per card would take a lot of memory).
    static void Thumb(Canvas c, string path, RectangleF r)
    {
        var key = "thumb|" + path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
        var bmp = c.Bitmap(key, () =>
        {
            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            float k = Math.Min(1, 480f / Math.Max(1, img.Width));
            int w = Math.Max(1, (int)(img.Width * k)), h = Math.Max(1, (int)(img.Height * k));
            using var small = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(small)) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(img, 0, 0, w, h); }
            var data = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, px, y * w * 4, w * 4);
            small.UnlockBits(data);
            lock (thumbSizes) thumbSizes[key] = (w, h);
            return (px, w, h);
        });
        (int w, int h) size;
        lock (thumbSizes) if (!thumbSizes.TryGetValue(key, out size)) size = (16, 9);
        float s = Math.Max(r.Width / size.w, r.Height / size.h);
        c.Draw(bmp, new RectangleF(r.X + (r.Width - size.w * s) / 2, r.Y + (r.Height - size.h * s) / 2, size.w * s, size.h * s));
    }

    public override string Signature(WidgetEnv e) => Hover.ToString();
}
