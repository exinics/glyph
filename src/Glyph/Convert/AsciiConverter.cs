using System.Drawing;
using System.Drawing.Text;

namespace Glyph.Convert;

/// Every converter setting. Defaults are tuned for wallpapers.
internal sealed class ConvertOptions
{
    public string Input = "";
    public string Output = "";            // .gclip path; empty = config/clips/<input name>.gclip
    public int Columns;                   // 0 = as wide as the screen in characters
    public int Rows;                      // 0 = from the video's shape
    public double Fps;                    // 0 = the video's own (at most 30)
    public double Start, Length;          // seconds; Length 0 = to the end
    public string Mode = "shape";         // ramp, shape, edges, blocks, braille
    public string Chars = "";             // "" = the mode's default
    public float Brightness, Contrast = 1f, Gamma = 1f;
    public bool Invert, AutoLevels = true, Color;
    public string Dither = "ordered";     // ordered, none
    public float EdgeThreshold = 0.08f;   // edges mode: how strong an edge must be
    public string Loop = "crossfade";     // crossfade = the end fades into the start; bounce = forwards then backwards; off
    public double LoopBlend = 1.0;        // crossfade: seconds of overlap
    public static readonly string[] Loops = { "crossfade", "bounce", "off" };

    public static readonly string[] Modes = { "ramp", "shape", "edges", "blocks", "braille" };
    public const string DefaultRamp = " .:-=+*#%@";
    public const string DefaultShape = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

    public string EffectiveChars => Chars.Length > 0 ? Chars : Mode == "shape" ? DefaultShape : DefaultRamp;
}

/// Turns pictures into character cells. Each cell is sampled as a 4x8 grid of brightness values.
internal sealed class AsciiConverter
{
    public const int SX = 4, SY = 8;   // samples per cell
    public readonly int Cols, Rows;
    public readonly int[] CharTable;   // index -> code point (what the clip stores)
    readonly ConvertOptions o;
    readonly float[] lum;              // sample brightness, (Cols*SX) x (Rows*SY)
    readonly float[][] glyphs = Array.Empty<float[]>();   // shape mode: coverage per char (SX*SY)
    readonly int[] glyphIndex = Array.Empty<int>();
    readonly float[] glyphMean = Array.Empty<float>();
    readonly float[] cellMean;          // per cell, for edge detection between cells
    readonly int edgeBase;
    float lo = 0, hi = 1; bool levelsPrimed;

    static readonly float[] Bayer4 =
    {
        0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5,
    };
    static readonly int[] Quadrants = { ' ', '▘', '▝', '▀', '▖', '▌', '▞', '▛', '▗', '▚', '▐', '▜', '▄', '▙', '▟', '█' };

    public AsciiConverter(ConvertOptions o, int cols, int rows, string font, float fontSize, int cellW, int cellH)
    {
        this.o = o; Cols = cols; Rows = rows;
        lum = new float[cols * SX * rows * SY];
        cellMean = new float[cols * rows];
        switch (o.Mode)
        {
            case "blocks": CharTable = Quadrants; break;
            case "braille":
                CharTable = new int[256];
                CharTable[0] = ' ';
                for (int p = 1; p < 256; p++) CharTable[p] = 0x2800 + p;
                break;
            case "shape":
                CharTable = Cp.Of(o.EffectiveChars).Distinct().Take(256).ToArray();
                glyphs = RenderGlyphs(CharTable, font, fontSize, cellW, cellH);
                glyphIndex = Enumerable.Range(0, CharTable.Length).ToArray();
                glyphMean = glyphs.Select(g => g.Average()).ToArray();
                break;
            default:
                {
                    var ramp = Cp.Of(o.EffectiveChars).ToList();
                    edgeBase = ramp.Count;
                    if (o.Mode == "edges") ramp.AddRange(new[] { (int)'-', '\\', '|', '/' });
                    CharTable = ramp.Take(256).ToArray();
                    break;
                }
        }
    }

    /// Coverage of each character as a 4x8 grid, scaled so the densest character is about 1.
    static float[][] RenderGlyphs(int[] chars, string font, float fontSize, int cellW, int cellH)
    {
        var result = new float[chars.Length][];
        using var bmp = new Bitmap(cellW, cellH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var f = new Font(font, fontSize, GraphicsUnit.Pixel);
        using var fmt = StringFormat.GenericTypographic;
        float lineH = f.GetHeight(g);
        float top = MathF.Floor((cellH - lineH) / 2f);
        float maxMean = 0.01f;
        for (int k = 0; k < chars.Length; k++)
        {
            g.Clear(System.Drawing.Color.Black);
            g.DrawString(char.ConvertFromUtf32(chars[k]), f, Brushes.White, 0, top, fmt);
            var cov = new float[SX * SY];
            for (int y = 0; y < cellH; y++)
                for (int x = 0; x < cellW; x++)
                {
                    int sx = Math.Min(SX - 1, x * SX / cellW), sy = Math.Min(SY - 1, y * SY / cellH);
                    cov[sy * SX + sx] += bmp.GetPixel(x, y).R / 255f;
                }
            float area = (cellW / (float)SX) * (cellH / (float)SY);
            float mean = 0;
            for (int i = 0; i < cov.Length; i++) { cov[i] /= area; mean += cov[i]; }
            mean /= cov.Length;
            maxMean = MathF.Max(maxMean, mean);
            result[k] = cov;
        }
        float s = 1f / maxMean;
        foreach (var c in result) for (int i = 0; i < c.Length; i++) c[i] *= s;
        if (Environment.GetEnvironmentVariable("GLYPH_DEBUG_GLYPHS") is string dbg)
        {
            bmp.Save(dbg + ".png");
            File.WriteAllLines(dbg, Enumerable.Range(0, chars.Length).OrderBy(k => result[k].Average())
                .Select(k => $"{char.ConvertFromUtf32(chars[k])} {result[k].Average():0.000}  top={top} lineH={lineH} cell={cellW}x{cellH}"));
        }
        return result;
    }

    /// Converts one RGB24 picture of size (Cols*SX) x (Rows*SY) into clip cell data.
    /// `cells` gets [charIndex, brightness] (+ r,g,b when colour is on) per cell.
    public void Convert(byte[] rgb, byte[] cells)
    {
        int W = Cols * SX, H = Rows * SY;
        // brightness
        for (int i = 0, p = 0; i < W * H; i++, p += 3)
            lum[i] = (0.2126f * rgb[p] + 0.7152f * rgb[p + 1] + 0.0722f * rgb[p + 2]) / 255f;

        if (o.AutoLevels) UpdateLevels();
        float range = MathF.Max(0.02f, hi - lo);
        float invGamma = 1f / MathF.Max(0.05f, o.Gamma);
        for (int i = 0; i < lum.Length; i++)
        {
            float v = o.AutoLevels ? (lum[i] - lo) / range : lum[i];
            v = Math.Clamp(v, 0, 1);
            if (invGamma != 1f) v = MathF.Pow(v, invGamma);
            v = (v - 0.5f) * o.Contrast + 0.5f + o.Brightness;
            if (o.Invert) v = 1 - v;
            lum[i] = Math.Clamp(v, 0, 1);
        }

        for (int cy = 0; cy < Rows; cy++)
            for (int cx = 0; cx < Cols; cx++)
            {
                float m = 0;
                for (int y = 0; y < SY; y++)
                {
                    int r0 = (cy * SY + y) * W + cx * SX;
                    for (int x = 0; x < SX; x++) m += lum[r0 + x];
                }
                cellMean[cy * Cols + cx] = m / (SX * SY);
            }

        int bpc = o.Color ? 5 : 2;
        Parallel.For(0, Rows, cy =>
        {
            Span<float> s = stackalloc float[SX * SY];
            for (int cx = 0; cx < Cols; cx++)
            {
                float mean = 0;
                for (int y = 0; y < SY; y++)
                    for (int x = 0; x < SX; x++)
                    {
                        float v = lum[(cy * SY + y) * W + cx * SX + x];
                        s[y * SX + x] = v; mean += v;
                    }
                mean /= SX * SY;
                int ci; float bright = mean;
                switch (o.Mode)
                {
                    case "shape": ci = Shape(s, mean + (o.Dither == "ordered" ? (Threshold(cx, cy) - 0.5f) * 0.12f : 0f)); break;
                    case "blocks": ci = Blocks(s, cx, cy, ref bright); break;
                    case "braille": ci = Braille(s, cx, cy, ref bright); break;
                    case "edges": ci = Edge(cx, cy, mean, ref bright); break;
                    default: ci = Ramp(mean, cx, cy); break;
                }
                int o2 = (cy * Cols + cx) * bpc;
                cells[o2] = (byte)ci;
                cells[o2 + 1] = (byte)(Math.Clamp(bright, 0, 1) * 255f + 0.5f);
                if (o.Color)
                {
                    int r = 0, g = 0, b = 0;
                    for (int y = 0; y < SY; y++)
                        for (int x = 0; x < SX; x++)
                        {
                            int p = ((cy * SY + y) * W + cx * SX + x) * 3;
                            r += rgb[p]; g += rgb[p + 1]; b += rgb[p + 2];
                        }
                    int n = SX * SY;
                    cells[o2 + 2] = (byte)(r / n); cells[o2 + 3] = (byte)(g / n); cells[o2 + 4] = (byte)(b / n);
                }
            }
        });
    }

    /// Auto levels: stretch the 1st..99th percentile to full range, smoothed over time so it doesn't pump.
    void UpdateLevels()
    {
        Span<int> hist = stackalloc int[256];
        int step = Math.Max(1, lum.Length / 20000);
        int count = 0;
        for (int i = 0; i < lum.Length; i += step) { hist[Math.Clamp((int)(lum[i] * 255), 0, 255)]++; count++; }
        int acc = 0, a = 0, b = 255;
        for (int i = 0; i < 256; i++) { acc += hist[i]; if (acc >= count * 0.01) { a = i; break; } }
        acc = 0;
        for (int i = 255; i >= 0; i--) { acc += hist[i]; if (acc >= count * 0.01) { b = i; break; } }
        float nlo = a / 255f, nhi = MathF.Max(nlo + 0.05f, b / 255f);
        if (!levelsPrimed) { lo = nlo; hi = nhi; levelsPrimed = true; }
        else { lo += (nlo - lo) * 0.1f; hi += (nhi - hi) * 0.1f; }
    }

    float Threshold(int x, int y) => o.Dither == "ordered" ? (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f : 0.5f;

    int Ramp(float v, int cx, int cy)
    {
        int n = edgeBase > 0 ? edgeBase : CharTable.Length;
        float f = v * (n - 1);
        int i = (int)f;
        if (o.Dither == "ordered" && f - i > Threshold(cx, cy)) i++;
        return Math.Clamp(i, 0, n - 1);
    }

    /// Picks the character whose ink best matches the cell: its overall brightness always counts;
    /// its shape (where the bright parts are) counts as much as the cell actually has detail,
    /// so flat areas get an even spread of characters and edges get characters that follow them.
    int Shape(Span<float> s, float target)
    {
        float sm = 0;
        for (int i = 0; i < s.Length; i++) sm += s[i];
        sm /= s.Length;
        float var = 0;
        for (int i = 0; i < s.Length; i++) { float d = s[i] - sm; var += d * d; }
        float detail = Math.Clamp(MathF.Sqrt(var / s.Length) / 0.18f, 0f, 1f);
        float meanWeight = 4f * s.Length, shapeWeight = detail * detail;
        int best = 0; float bestErr = float.MaxValue;
        for (int k = 0; k < glyphs.Length; k++)
        {
            var g = glyphs[k];
            float gm = glyphMean[k], dm = target - gm;
            float e = meanWeight * dm * dm;
            if (e >= bestErr) continue;
            if (shapeWeight > 0.001f)
            {
                float sh = 0;
                for (int i = 0; i < g.Length; i++) { float d = (s[i] - sm) - (g[i] - gm); sh += d * d; }
                e += shapeWeight * sh;
            }
            if (e < bestErr) { bestErr = e; best = k; }
        }
        return glyphIndex[best];
    }

    int Blocks(Span<float> s, int cx, int cy, ref float bright)
    {
        int mask = 0; float sum = 0; int on = 0;
        for (int q = 0; q < 4; q++)
        {
            int qx = (q & 1) * 2, qy = (q >> 1) * 4;
            float m = 0;
            for (int y = 0; y < 4; y++) for (int x = 0; x < 2; x++) m += s[(qy + y) * SX + qx + x];
            m /= 8;
            if (m > Threshold(cx * 2 + (q & 1), cy * 2 + (q >> 1))) { mask |= 1 << q; sum += m; on++; }
        }
        if (on > 0) bright = MathF.Max(0.35f, sum / on);
        return mask;
    }

    int Braille(Span<float> s, int cx, int cy, ref float bright)
    {
        // dot (col, row) -> bit, per the braille layout
        ReadOnlySpan<int> bit = stackalloc int[] { 0, 3, 1, 4, 2, 5, 6, 7 }; // index = row*2 + col
        int pattern = 0; float sum = 0; int on = 0;
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 2; col++)
            {
                float m = (s[(row * 2) * SX + col * 2] + s[(row * 2) * SX + col * 2 + 1] + s[(row * 2 + 1) * SX + col * 2] + s[(row * 2 + 1) * SX + col * 2 + 1]) * 0.25f;
                if (m > Threshold(cx * 2 + col, cy * 4 + row)) { pattern |= 1 << bit[row * 2 + col]; sum += m; on++; }
            }
        if (on > 0) bright = MathF.Max(0.35f, sum / on);
        return pattern;
    }

    int Edge(int cx, int cy, float mean, ref float bright)
    {
        float M(int dx, int dy) => cellMean[Math.Clamp(cy + dy, 0, Rows - 1) * Cols + Math.Clamp(cx + dx, 0, Cols - 1)];
        float gx = (M(1, -1) + 2 * M(1, 0) + M(1, 1)) - (M(-1, -1) + 2 * M(-1, 0) + M(-1, 1));
        float gy = (M(-1, 1) + 2 * M(0, 1) + M(1, 1)) - (M(-1, -1) + 2 * M(0, -1) + M(1, -1));
        // cells are about twice as tall as wide, so a vertical step covers more picture
        gy *= 0.5f;
        float mag = MathF.Sqrt(gx * gx + gy * gy) / 4f;
        if (mag < o.EdgeThreshold) return Ramp(mean, cx, cy);
        // the edge runs at right angles to the gradient; y points down on screen
        float ang = MathF.Atan2(gy * 2f, gx) * 180f / MathF.PI + 90f;
        ang = ((ang % 180) + 180) % 180;
        bright = MathF.Max(mean, 0.6f + 0.4f * MathF.Min(1, mag * 2));
        int e = ang < 22.5f || ang >= 157.5f ? 0 : ang < 67.5f ? 1 : ang < 112.5f ? 2 : 3;
        return edgeBase + e;
    }
}
