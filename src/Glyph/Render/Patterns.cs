using System.Numerics;
using Vortice.Mathematics;

namespace Glyph.Render;

/// Repeating line and dot patterns, shared by frames (over a box's fill) and the backdrop (over the whole screen).
internal static class Patterns
{
    public static readonly string[] Kinds = { "none", "scanlines", "grid", "dots", "crosses", "diagonal", "crosshatch", "vlines" };

    /// Fills `r` with the pattern. `size` = thickness of lines / dots, in pixels. Clip to a shape first if needed.
    public static void Fill(Canvas c, RectangleF r, string kind, Rgba color, float opacity, float spacing, float size = 1)
    {
        if (kind == "none" || opacity <= 0 || color.A == 0) return;
        var dc = c.Dc;
        var br = c.Brush(color, opacity);
        float sp = Math.Max(2, spacing), s = Math.Clamp(size, 0.5f, sp);
        switch (kind)
        {
            case "scanlines": for (float y = r.Y; y < r.Bottom; y += sp) dc.FillRectangle(new Rect(r.X, y, r.Width, s), br); break;
            case "vlines": for (float x = r.X; x < r.Right; x += sp) dc.FillRectangle(new Rect(x, r.Y, s, r.Height), br); break;
            case "grid":
                for (float y = r.Y; y < r.Bottom; y += sp) dc.FillRectangle(new Rect(r.X, y, r.Width, s), br);
                for (float x = r.X; x < r.Right; x += sp) dc.FillRectangle(new Rect(x, r.Y, s, r.Height), br);
                break;
            case "dots":
                for (float y = r.Y + sp / 2; y < r.Bottom; y += sp)
                    for (float x = r.X + sp / 2; x < r.Right; x += sp) dc.FillRectangle(new Rect(x, y, s, s), br);
                break;
            case "crosses":
                {
                    float arm = Math.Max(1.5f, s * 2);
                    for (float y = r.Y + sp / 2; y < r.Bottom; y += sp)
                        for (float x = r.X + sp / 2; x < r.Right; x += sp)
                        {
                            dc.FillRectangle(new Rect(x - arm, y, arm * 2 + 1, 1), br);
                            dc.FillRectangle(new Rect(x, y - arm, 1, arm * 2 + 1), br);
                        }
                    break;
                }
            case "diagonal":
            case "crosshatch":
                for (float d = -r.Height; d < r.Width; d += sp)
                {
                    dc.DrawLine(new Vector2(r.X + d, r.Bottom), new Vector2(r.X + d + r.Height, r.Y), br, s);
                    if (kind == "crosshatch") dc.DrawLine(new Vector2(r.X + d, r.Y), new Vector2(r.X + d + r.Height, r.Bottom), br, s);
                }
                break;
        }
    }
}
