namespace Glyph;

/// The character grid that everything draws into. One cell = one character with a
/// foreground colour (its alpha fades the character into the background) and a background colour.
/// A space (32) counts as empty: layers draw "over" only where they put a non-space character.
internal sealed class Grid
{
    public int W, H;
    /// Added to every Put (layers with parallax).
    public int ShiftX, ShiftY;
    public int[] Ch = System.Array.Empty<int>();
    public uint[] Fg = System.Array.Empty<uint>();
    public uint[] Bg = System.Array.Empty<uint>();

    public void Resize(int w, int h)
    {
        if (w == W && h == H) return;
        W = w; H = h;
        Ch = new int[w * h]; Fg = new uint[w * h]; Bg = new uint[w * h];
    }

    public void Clear(uint bg)
    {
        System.Array.Fill(Ch, 32);
        System.Array.Fill(Fg, 0u);
        System.Array.Fill(Bg, bg);
    }

    public bool In(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

    public void Put(int x, int y, int ch, uint fg)
    {
        x += ShiftX; y += ShiftY;
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
        int i = y * W + x;
        Ch[i] = ch; Fg[i] = fg;
    }

    /// Puts a character, faded by opacity (0..1).
    public void Put(int x, int y, int ch, uint fg, float opacity)
    {
        x += ShiftX; y += ShiftY;
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
        int i = y * W + x;
        Ch[i] = ch; Fg[i] = opacity >= 1f ? fg : Px.Alpha(fg, ((fg >> 24) / 255f) * opacity);
    }

    /// Blends a background colour over the cell's background (alpha of `bg` is its opacity).
    public void PutBg(int x, int y, uint bg)
    {
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
        int i = y * W + x;
        Bg[i] = Px.Over(Bg[i], bg);
    }

    /// Fills a rectangle of cells: background colour, and clears the characters.
    public void FillRect(int x, int y, int w, int h, uint bg)
    {
        for (int yy = Math.Max(0, y); yy < Math.Min(H, y + h); yy++)
            for (int xx = Math.Max(0, x); xx < Math.Min(W, x + w); xx++)
            {
                int i = yy * W + xx;
                Bg[i] = Px.Over(Bg[i], bg); Ch[i] = 32; Fg[i] = 0;
            }
    }

    public void Text(int x, int y, string s, uint fg, uint? bg = null)
    {
        int cx = x;
        for (int k = 0; k < s.Length; k++)
        {
            int cp = char.IsHighSurrogate(s[k]) && k + 1 < s.Length ? char.ConvertToUtf32(s[k], s[++k]) : s[k];
            if (bg.HasValue) PutBg(cx, y, bg.Value);
            Put(cx, y, cp, fg);
            cx++;
        }
    }

    /// Box outline with light box-drawing lines.
    public void Box(int x, int y, int w, int h, uint fg, uint? fill = null)
    {
        if (w < 2 || h < 2) return;
        if (fill.HasValue)
            for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) { PutBg(xx, yy, fill.Value); Put(xx, yy, 32, 0); }
        for (int xx = x + 1; xx < x + w - 1; xx++) { Put(xx, y, '─', fg); Put(xx, y + h - 1, '─', fg); }
        for (int yy = y + 1; yy < y + h - 1; yy++) { Put(x, yy, '│', fg); Put(x + w - 1, yy, '│', fg); }
        Put(x, y, '┌', fg); Put(x + w - 1, y, '┐', fg); Put(x, y + h - 1, '└', fg); Put(x + w - 1, y + h - 1, '┘', fg);
    }
}

/// Splits text into code points (handles characters outside the basic plane).
internal static class Cp
{
    public static int[] Of(string s)
    {
        var list = new List<int>(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { list.Add(char.ConvertToUtf32(s[i], s[i + 1])); i++; }
            else list.Add(s[i]);
        }
        return list.ToArray();
    }
}
