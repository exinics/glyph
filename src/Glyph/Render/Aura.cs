using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;

namespace Glyph.Render;

/// A soft light around what a widget (or the bar) draws, in two kinds that can be combined:
/// halo = a dark (or any colour) blur behind it, so light text stays readable on a light wallpaper;
/// glow = a blurred copy in its own colours (or one colour) added on top, like a neon sign or an old CRT.
internal readonly record struct AuraLook(Rgba? Halo, float HaloSize, float HaloStrength, bool GlowSelf, Rgba? Glow, float GlowSize, float GlowStrength)
{
    public bool Any => Halo.HasValue || GlowSelf || Glow.HasValue;

    /// The look from a theme's [ui] and a widget's own settings ("auto" = the theme's, "none" = off).
    public static AuraLook From(Theme t, string halo, float haloSize, float haloStrength, string glow, float glowSize, float glowStrength)
    {
        var ui = t.Options.Ui;
        string h = halo is "" or "auto" ? ui.Halo : halo, g = glow is "" or "auto" ? ui.Glow : glow;
        return new AuraLook(
            h == "none" ? null : t[h], haloSize > 0 ? haloSize : ui.HaloSize, haloStrength >= 0 ? haloStrength : ui.HaloStrength,
            g == "self", g is "none" or "self" ? null : t[g], glowSize > 0 ? glowSize : ui.GlowSize, glowStrength >= 0 ? glowStrength : ui.GlowStrength);
    }
}

internal static class Aura
{
    /// Runs `draw`, adding the halo behind it and the glow over it. Without either it just draws.
    /// A widget's finished glow/halo picture, kept while it shows the same thing (`Key`) in the same place (`M`).
    internal sealed class Kept : IDisposable
    {
        public string Key = ""; public Matrix3x2 M; public ID2D1Bitmap1? Pic; public int X, Y;
        public void Dispose() { Pic?.Dispose(); Pic = null; Key = ""; }
    }

    /// `owner` + `key` (what it shows now, e.g. its contents' signature): the result is kept and redrawn from the
    /// picture until the key or the transform changes, so an unchanged widget isn't blurred again every frame.
    public static void Draw(Canvas c, AuraLook a, Action draw, object? owner = null, string? key = null)
    {
        if (!a.Any) { draw(); return; }
        var dc = c.Dc;
        Kept? kept = owner != null && key != null ? c.KeptFor(owner) : null;
        if (kept is { Pic: not null } && kept.Key == key && kept.M == dc.Transform)
        {
            var m0 = dc.Transform;
            dc.Transform = Matrix3x2.CreateTranslation(kept.X, kept.Y);
            try { dc.DrawBitmap(kept.Pic, 1, BitmapInterpolationMode.NearestNeighbor); } finally { dc.Transform = m0; }
            return;
        }
        var target = dc.Target;
        using var list = dc.CreateCommandList();
        dc.Target = list;
        try { draw(); }
        finally { list.Close(); dc.Target = target; }
        var m = dc.Transform;   // the list already holds the drawing in place
        dc.Transform = Matrix3x2.Identity;
        // nothing drawn (e.g. an empty panel): effects on an empty picture fail the whole frame, so skip them
        var bounds = dc.GetImageLocalBounds(list);
        if (!(bounds.Right > bounds.Left && bounds.Bottom > bounds.Top) || !float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Right) || bounds.Right - bounds.Left > 20000 || bounds.Bottom - bounds.Top > 20000)
        {
            try { if (bounds.Right > bounds.Left) dc.DrawImage(list); } finally { dc.Transform = m; }
            return;
        }
        // the drawing is turned into a picture once and the effects work on that: a recording (command list)
        // used by several effects at once fails when it holds a layer (clips, fades), and it's less GPU work
        int x0 = (int)MathF.Floor(bounds.Left), y0 = (int)MathF.Floor(bounds.Top);
        int bw = Math.Max(1, (int)MathF.Ceiling(bounds.Right) - x0), bh = Math.Max(1, (int)MathF.Ceiling(bounds.Bottom) - y0);
        using var pic = dc.CreateBitmap(new Vortice.Mathematics.SizeI(bw, bh), IntPtr.Zero, 0, new BitmapProperties1(
            new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target));
        dc.Target = pic;
        dc.Transform = Matrix3x2.CreateTranslation(-x0, -y0);
        dc.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 0));
        dc.DrawImage(list);
        dc.Target = target;
        dc.Transform = Matrix3x2.CreateTranslation(x0, y0);
        ID2D1Image src = pic;
        try
        {
            if (kept != null)
            {
                // into a picture with room around it for the blur, kept for the next frames
                int margin = (int)MathF.Ceiling(3 * Math.Max(a.Halo.HasValue ? a.HaloSize : 0, a.GlowSelf || a.Glow.HasValue ? a.GlowSize : 0)) + 2;
                int fx = x0 - margin, fy = y0 - margin, fw = bw + 2 * margin, fh = bh + 2 * margin;
                var final = dc.CreateBitmap(new Vortice.Mathematics.SizeI(fw, fh), IntPtr.Zero, 0, new BitmapProperties1(
                    new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target));
                dc.Target = final;
                dc.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 0));
                dc.Transform = Matrix3x2.CreateTranslation(margin, margin);
                Effects();
                dc.Target = target;
                dc.Transform = Matrix3x2.CreateTranslation(fx, fy);
                dc.DrawBitmap(final, 1, BitmapInterpolationMode.NearestNeighbor);
                kept.Pic?.Dispose();
                kept.Pic = final; kept.Key = key!; kept.M = m; kept.X = fx; kept.Y = fy;
            }
            else Effects();
        }
        finally { dc.Transform = m; }

        void Effects()
        {
            if (a.Halo is Rgba h && a.HaloStrength > 0)
            {
                // blurred thin text is very faint, so its coverage is boosted (and clamped) into a solid soft backing
                using var sh = new Shadow(dc) { BlurStandardDeviation = Math.Max(0.5f, a.HaloSize), Color = Vec(h, 1) };
                sh.SetInput(0, src, true);
                using var boost = new ColorMatrix(dc) { Matrix = Scale(a.HaloStrength * 3.5f) };
                boost.SetValue(2, true);   // D2D1_COLORMATRIX_PROP_CLAMP_OUTPUT
                boost.SetInputEffect(0, sh, true);
                using var fade = new ColorMatrix(dc) { Matrix = Scale(Math.Min(1, a.HaloStrength * 0.6f)) };
                fade.SetInputEffect(0, boost, true);
                dc.DrawImage(fade);
            }
            dc.DrawImage(src);
            if (a.GlowStrength > 0 && (a.GlowSelf || a.Glow.HasValue))
            {
                float k = Math.Clamp(a.GlowStrength, 0, 3);
                // a wide soft bloom and a tight bright one, added on top (Plus = light adds up, like real glow)
                foreach (var (size, amount) in new[] { (a.GlowSize, k * 0.35f), (a.GlowSize * 0.3f, k * 0.45f) })
                {
                    if (amount <= 0.01f) continue;
                    ID2D1Effect fx;
                    if (a.GlowSelf)
                    {
                        var blur = new GaussianBlur(dc) { StandardDeviation = Math.Max(0.5f, size), BorderMode = BorderMode.Soft };
                        blur.SetInput(0, src, true);
                        fx = blur;
                    }
                    else
                    {
                        var sh = new Shadow(dc) { BlurStandardDeviation = Math.Max(0.5f, size), Color = Vec(a.Glow!.Value, 1) };
                        sh.SetInput(0, src, true);
                        fx = sh;
                    }
                    using (fx)
                    {
                        // blurred thin strokes are faint: boost their coverage (clamped) so the light spreads visibly
                        using var boost = new ColorMatrix(dc) { Matrix = Scale(size > a.GlowSize * 0.5f ? 3.2f : 2f) };
                        boost.SetValue(2, true);
                        boost.SetInputEffect(0, fx, true);
                        using var dim = new ColorMatrix(dc) { Matrix = Scale(Math.Min(1, amount)) };
                        dim.SetInputEffect(0, boost, true);
                        dc.DrawImage(dim, compositeMode: CompositeMode.Plus);
                        if (amount > 1)
                        {
                            using var more = new ColorMatrix(dc) { Matrix = Scale(Math.Min(1, amount - 1)) };
                            more.SetInputEffect(0, boost, true);
                            dc.DrawImage(more, compositeMode: CompositeMode.Plus);
                        }
                    }
                }
            }
        }
    }

    /// Draws `draw` (a whole widget) from a kept picture while `key` (what it shows) and the transform stay the
    /// same; otherwise draws it into a new picture and keeps that. key = null: just draws.
    public static void Keep(Canvas c, object owner, string? key, Action draw)
    {
        if (key == null) { c.ForgetKept(owner); draw(); return; }
        var dc = c.Dc;
        var kept = c.KeptFor(owner);
        var m = dc.Transform;
        if (kept.Pic != null && kept.Key == key && kept.M == m)
        {
            dc.Transform = Matrix3x2.CreateTranslation(kept.X, kept.Y);
            try { dc.DrawBitmap(kept.Pic, 1, BitmapInterpolationMode.NearestNeighbor); } finally { dc.Transform = m; }
            return;
        }
        var target = dc.Target;
        using var list = dc.CreateCommandList();
        dc.Target = list;
        try { draw(); }
        finally { list.Close(); dc.Target = target; }
        dc.Transform = Matrix3x2.Identity;
        try
        {
            var b = dc.GetImageLocalBounds(list);
            if (!(b.Right > b.Left && b.Bottom > b.Top) || !float.IsFinite(b.Left) || !float.IsFinite(b.Right) || b.Right - b.Left > 8000 || b.Bottom - b.Top > 8000)
            {
                kept.Dispose();
                if (b.Right > b.Left) dc.DrawImage(list);
                return;
            }
            int x0 = (int)MathF.Floor(b.Left), y0 = (int)MathF.Floor(b.Top);
            int bw = Math.Max(1, (int)MathF.Ceiling(b.Right) - x0), bh = Math.Max(1, (int)MathF.Ceiling(b.Bottom) - y0);
            var pic = dc.CreateBitmap(new Vortice.Mathematics.SizeI(bw, bh), IntPtr.Zero, 0, new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target));
            dc.Target = pic;
            dc.Transform = Matrix3x2.CreateTranslation(-x0, -y0);
            dc.Clear(new Vortice.Mathematics.Color4(0, 0, 0, 0));
            dc.DrawImage(list);
            dc.Target = target;
            dc.Transform = Matrix3x2.CreateTranslation(x0, y0);
            dc.DrawBitmap(pic, 1, BitmapInterpolationMode.NearestNeighbor);
            kept.Pic?.Dispose();
            kept.Pic = pic; kept.Key = key; kept.M = m; kept.X = x0; kept.Y = y0;
        }
        finally { dc.Transform = m; }
    }

    /// For a widget drawing its own contents: kept under the widget's key.
    public static void Draw(Canvas c, AuraLook a, (object owner, string? key) keep, Action draw) => Draw(c, a, draw, keep.owner, keep.key);

    static Vector4 Vec(Rgba c, float a) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f * a);

    /// Fades by k (the effect works on straight colours, so only alpha is scaled).
    static Vortice.Mathematics.Matrix5x4 Scale(float k) => new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, k, 0, 0, 0, 0);
}
