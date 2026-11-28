using System.Drawing.Imaging;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Glyph.Render;

/// Smooth drawing (Direct2D) for backdrops and smooth-style widgets: rounded panels, text in any
/// font, pictures and gradients. Bitmaps and text formats are cached until the theme changes.
internal sealed class Canvas : IDisposable
{
    public readonly ID2D1DeviceContext Dc;
    readonly IDWriteFactory dw;
    readonly ID2D1SolidColorBrush brush;
    readonly Dictionary<string, IDWriteTextFormat> formats = new();
    readonly Dictionary<string, (ID2D1Bitmap1 bmp, int w, int h)> bitmaps = new();
    readonly Dictionary<string, IDWriteInlineObject> ellipses = new();
    int fontsVersion = -1;
    readonly Dictionary<string, IDWriteTextLayout> layouts = new();   // text with icons, laid out once

    public Canvas(ID2D1DeviceContext dc, IDWriteFactory dw)
    {
        Dc = dc; this.dw = dw;
        brush = dc.CreateSolidColorBrush(new Color4(1, 1, 1, 1));
        dc.AntialiasMode = AntialiasMode.PerPrimitive;
        dc.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
    }

    static Color4 C(Rgba c, float opacity = 1) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f * opacity);

    public ID2D1SolidColorBrush Brush(Rgba c, float opacity = 1) { brush.Color = C(c, opacity); return brush; }

    public void Fill(RectangleF r, Rgba c, float radius = 0, float opacity = 1)
    {
        if (c.A == 0 || opacity <= 0) return;
        var rect = new Rect(r.X, r.Y, r.Width, r.Height);
        if (radius > 0) Dc.FillRoundedRectangle(new RoundedRectangle(r, radius, radius), Brush(c, opacity));
        else Dc.FillRectangle(rect, Brush(c, opacity));
    }

    public void Stroke(RectangleF r, Rgba c, float width, float radius = 0, float opacity = 1)
    {
        if (width <= 0 || c.A == 0) return;
        var rect = new Rect(r.X + width / 2, r.Y + width / 2, r.Width - width, r.Height - width);
        if (radius > 0) Dc.DrawRoundedRectangle(new RoundedRectangle(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius, radius), Brush(c, opacity), width);
        else Dc.DrawRectangle(rect, Brush(c, opacity), width);
    }

    public void Ring(float x, float y, float r, Rgba c, float width, float opacity)
    {
        if (r <= 0 || opacity <= 0) return;
        Dc.DrawEllipse(new Ellipse(new Vector2(x, y), r, r), Brush(c, opacity), width);
    }

    public void Dot(float x, float y, float r, Rgba c, float opacity)
    {
        if (opacity <= 0) return;
        Dc.FillEllipse(new Ellipse(new Vector2(x, y), r, r), Brush(c, opacity));
    }

    /// A soft shadow: a few widening, fading rounded rectangles below the panel.
    public void Shadow(RectangleF r, float radius, float strength)
    {
        if (strength <= 0) return;
        for (int i = 1; i <= 6; i++)
        {
            var g = r; g.Inflate(i * 2, i * 2); g.Offset(0, 3);
            Fill(g, new Rgba(0, 0, 0), radius + i * 2, strength * 0.09f);
        }
    }

    public IDWriteTextFormat Format(string font, float size, bool bold, string align, bool wrap)
    {
        if (fontsVersion != Fonts.Version)   // font files changed: make the formats again from the new set
        {
            fontsVersion = Fonts.Version;
            foreach (var x in formats.Values) x.Dispose();
            formats.Clear();
            foreach (var e in ellipses.Values) e.Dispose();
            ellipses.Clear();
            widths.Clear();
            foreach (var l in layouts.Values) l.Dispose();
            layouts.Clear();
        }
        var key = $"{font}|{size}|{bold}|{align}|{wrap}";
        if (formats.TryGetValue(key, out var f)) return f;
        var (family, weight) = Fonts.Resolve(dw, font);
        f = dw.CreateTextFormat(family, Fonts.Collection(dw), weight ?? (bold ? FontWeight.SemiBold : FontWeight.Normal), Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, size, "en-us");
        f.TextAlignment = align switch { "center" => TextAlignment.Center, "right" => TextAlignment.Trailing, _ => TextAlignment.Leading };
        f.ParagraphAlignment = ParagraphAlignment.Near;
        f.WordWrapping = wrap ? WordWrapping.Wrap : WordWrapping.NoWrap;
        var sign = dw.CreateEllipsisTrimmingSign(f);
        ellipses[key] = sign;
        f.SetTrimming(new Trimming { Granularity = TrimmingGranularity.Character }, sign);
        formats[key] = f;
        return f;
    }

    public void Text(string s, RectangleF r, Rgba c, string font, float size, bool bold = false, string align = "left", bool wrap = false, float opacity = 1)
    {
        s = Symbols.Expand(s);
        if (s.Length == 0) return;
        var f = Format(font, size, bold, align, wrap);
        // a box exactly as wide as the measured text can still cut it to "…" (rounding in DirectWrite): a pixel of room
        if (!wrap && align == "left") r = new RectangleF(r.X, r.Y, r.Width + 1.5f, r.Height);
        Dc.PushAxisAlignedClip(new Rect(r.X, r.Y, r.Width, r.Height), AntialiasMode.Aliased);
        if (Symbols.HasIcon(s))
        {
            // icons ({icon:name}) come from the icon font, whatever font the text is in
            var lk = $"{font}|{size}|{bold}|{align}|{wrap}|{r.Width:0.#}|{r.Height:0.#}|{Symbols.LookKey}|{s}";
            if (!layouts.TryGetValue(lk, out var layout))
            {
                if (layouts.Count > 300) { foreach (var x in layouts.Values) x.Dispose(); layouts.Clear(); }
                layout = dw.CreateTextLayout(s, f, Math.Max(1, r.Width), Math.Max(1, r.Height));
                Symbols.Mark(layout, s, size, Fonts.Collection(dw));
                layouts[lk] = layout;
            }
            Dc.DrawTextLayout(new Vector2(r.X, r.Y), layout, Brush(c, opacity), DrawTextOptions.None);
        }
        else Dc.DrawText(s, f, new Rect(r.X, r.Y, r.Width, r.Height), Brush(c, opacity), DrawTextOptions.None, Vortice.DCommon.MeasuringMode.Natural);
        Dc.PopAxisAlignedClip();
    }

    readonly Dictionary<string, float> widths = new();

    /// Width of a line of text in pixels (cached).
    public float Measure(string s, string font, float size, bool bold)
    {
        s = Symbols.Expand(s);
        if (s.Length == 0) return 0;
        bool icons = Symbols.HasIcon(s);
        var key = icons ? $"{font}|{size}|{bold}|{Symbols.LookKey}|{s}" : $"{font}|{size}|{bold}|{s}";
        if (widths.TryGetValue(key, out var w)) return w;
        if (widths.Count > 5000) widths.Clear();
        var f = Format(font, size, bold, "left", false);
        using var layout = dw.CreateTextLayout(s, f, 100000, 1000);
        if (icons) Symbols.Mark(layout, s, size, Fonts.Collection(dw));
        w = layout.Metrics.WidthIncludingTrailingWhitespace;
        widths[key] = w;
        return w;
    }

    /// A filled area under a line (graphs), fading from `color` at the line to see-through at the bottom.
    public void Area(IReadOnlyList<PointF> pts, float bottom, Rgba color, float fillOpacity, float lineWidth)
    {
        if (pts.Count < 2) return;
        using (var geo = d2dFactory().CreatePathGeometry())
        {
            using (var sink = geo.Open())
            {
                sink.BeginFigure(new Vector2(pts[0].X, bottom), FigureBegin.Filled);
                foreach (var p in pts) sink.AddLine(new Vector2(p.X, p.Y));
                sink.AddLine(new Vector2(pts[^1].X, bottom));
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
            float top = pts.Min(p => p.Y);
            var stops = new[] { new GradientStop { Position = 0, Color = C(color, fillOpacity) }, new GradientStop { Position = 1, Color = C(color, 0) } };
            using var coll = Dc.CreateGradientStopCollection(stops);
            using var b = Dc.CreateLinearGradientBrush(new LinearGradientBrushProperties(new Vector2(0, top), new Vector2(0, bottom)), coll);
            Dc.FillGeometry(geo, b);
        }
        if (lineWidth > 0)
            for (int i = 1; i < pts.Count; i++)
                Dc.DrawLine(new Vector2(pts[i - 1].X, pts[i - 1].Y), new Vector2(pts[i].X, pts[i].Y), Brush(color), lineWidth);
    }

    ID2D1Factory d2dFactory() => Dc.Factory;

    /// A bitmap from premultiplied BGRA pixels, cached under `key`.
    public unsafe ID2D1Bitmap1 Bitmap(string key, Func<(byte[] px, int w, int h)> make)
    {
        if (bitmaps.TryGetValue(key, out var b)) return b.bmp;
        var (px, w, h) = make();
        var props = new BitmapProperties1(new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96);
        ID2D1Bitmap1 bmp;
        fixed (byte* p = px) bmp = Dc.CreateBitmap(new SizeI(w, h), (IntPtr)p, (uint)(w * 4), props);
        bitmaps[key] = (bmp, w, h);
        return bmp;
    }

    public void Draw(ID2D1Bitmap1 bmp, RectangleF dest, float opacity = 1)
    {
        Dc.DrawBitmap(bmp, new Rect(dest.X, dest.Y, dest.Width, dest.Height), opacity, BitmapInterpolationMode.Linear, null);
    }

    /// A picture from disk, scaled to fill the area as `fit` says; optionally darkened.
    public void Picture(string path, RectangleF area, string fit, float dim, bool soften = false)
    {
        if (!File.Exists(path)) return;
        var size = PictureSize(path);
        float need = fit switch { "contain" => Math.Min(area.Width / Math.Max(1, size.Width), area.Height / Math.Max(1, size.Height)), "center" or "stretch" => 1f, _ => Math.Max(area.Width / Math.Max(1, size.Width), area.Height / Math.Max(1, size.Height)) };
        float shrink = fit == "stretch" ? Math.Max(area.Width / Math.Max(1, size.Width), area.Height / Math.Max(1, size.Height)) : need;
        int lw = shrink < 0.95f ? Math.Max(1, (int)MathF.Ceiling(size.Width * shrink)) : size.Width, lh = shrink < 0.95f ? Math.Max(1, (int)MathF.Ceiling(size.Height * shrink)) : size.Height;
        var key = "img|" + path + "|" + File.GetLastWriteTimeUtc(path).Ticks + "|" + lw + "x" + lh;
        var bmp = Bitmap(key, () => LoadPremultiplied(path, lw, lh));
        var (_, w, h) = bitmaps[key];
        if (w != size.Width && size.Width > 0) { w = size.Width; h = size.Height; }   // place it as the full picture
        float sx = area.Width / w, sy = area.Height / h;
        float s = fit switch { "contain" => Math.Min(sx, sy), "center" => 1f, "stretch" => 1f, _ => Math.Max(sx, sy) };
        RectangleF dest = fit == "stretch" ? area : new RectangleF(area.X + (area.Width - w * s) / 2, area.Y + (area.Height - h * s) / 2, w * s, h * s);
        var (_, bw, bh) = bitmaps[key];
        float up = dest.Width / Math.Max(1, bw);
        if (soften && up > 1.5f)
        {
            // a small picture blown up: blurred by about one of its own pixels, so it reads as soft, not blocky
            using var blur = new Vortice.Direct2D1.Effects.GaussianBlur(Dc) { StandardDeviation = 1.1f, BorderMode = Vortice.Direct2D1.BorderMode.Hard };
            blur.SetInput(0, bmp, true);
            var t0 = Dc.Transform;
            Dc.Transform = System.Numerics.Matrix3x2.CreateScale(up, dest.Height / Math.Max(1, bh)) * System.Numerics.Matrix3x2.CreateTranslation(dest.X, dest.Y) * t0;
            try { Dc.DrawImage(blur, interpolationMode: Vortice.Direct2D1.InterpolationMode.HighQualityCubic); } finally { Dc.Transform = t0; }
        }
        else Draw(bmp, dest);
        if (dim > 0) Fill(area, new Rgba(0, 0, 0), 0, dim);
    }

    static readonly Dictionary<string, System.Drawing.Size> pictureSizes = new();

    /// A picture's size without decoding it all (cached).
    static System.Drawing.Size PictureSize(string path)
    {
        if (pictureSizes.TryGetValue(path, out var s)) return s;
        try { using var fs = File.OpenRead(path); using var img = Image.FromStream(fs, false, false); s = img.Size; }
        catch { s = new System.Drawing.Size(1, 1); }
        pictureSizes[path] = s;
        return s;
    }

    /// Decodes a picture to premultiplied pixels, scaled to w x h (its own size if 0).
    static unsafe (byte[], int, int) LoadPremultiplied(string path, int w = 0, int h = 0)
    {
        using var fs = File.OpenRead(path);
        using var img = Image.FromStream(fs);
        if (w <= 0 || h <= 0) { w = img.Width; h = img.Height; }
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            if (w != img.Width || h != img.Height)
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);   // no dark fringe at the edges
                g.DrawImage(img, new Rectangle(0, 0, w, h), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
            else g.DrawImage(img, 0, 0, img.Width, img.Height);
        }
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        var px = new byte[bmp.Width * bmp.Height * 4];
        for (int y = 0; y < bmp.Height; y++)
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, px, y * bmp.Width * 4, bmp.Width * 4);
        bmp.UnlockBits(data);
        return (px, bmp.Width, bmp.Height);
    }

    public void Gradient(RectangleF r, IReadOnlyList<Rgba> colors, float angleDeg, bool radial)
    {
        if (colors.Count == 0) return;
        if (colors.Count == 1) { Fill(r, colors[0]); return; }
        var stops = colors.Select((c, i) => new GradientStop { Position = i / (float)(colors.Count - 1), Color = C(c) }).ToArray();
        using var coll = Dc.CreateGradientStopCollection(stops);
        var rect = new Rect(r.X, r.Y, r.Width, r.Height);
        if (radial)
        {
            var center = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
            float rad = MathF.Sqrt(r.Width * r.Width + r.Height * r.Height) / 2;
            using var b = Dc.CreateRadialGradientBrush(new RadialGradientBrushProperties(center, Vector2.Zero, rad, rad), coll);
            Dc.FillRectangle(rect, b);
        }
        else
        {
            float a = angleDeg * MathF.PI / 180f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var c = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2);
            float half = (MathF.Abs(dir.X) * r.Width + MathF.Abs(dir.Y) * r.Height) / 2;
            using var b = Dc.CreateLinearGradientBrush(new LinearGradientBrushProperties(c - dir * half, c + dir * half), coll);
            Dc.FillRectangle(rect, b);
        }
    }

    /// Forget cached pictures and fonts (after a theme change).
    readonly Dictionary<object, Aura.Kept> kept = new();
    /// The kept glow/halo picture of a widget (see Aura.Draw).
    public Aura.Kept KeptFor(object owner) { if (!kept.TryGetValue(owner, out var k)) kept[owner] = k = new Aura.Kept(); return k; }
    public void ForgetKept(object owner) { if (kept.Remove(owner, out var k)) k.Dispose(); }

    public void Reset()
    {
        foreach (var k in kept.Values) k.Dispose();
        kept.Clear();
        foreach (var b in bitmaps.Values) b.bmp.Dispose();
        bitmaps.Clear();
        foreach (var f in formats.Values) f.Dispose();
        formats.Clear();
        foreach (var e in ellipses.Values) e.Dispose();
        ellipses.Clear();
        widths.Clear();
        foreach (var l in layouts.Values) l.Dispose();
        layouts.Clear();
    }

    public void Dispose()
    {
        Reset();
        brush.Dispose();
    }
}
