namespace Glyph.Wallpaper;

/// The running wallpaper of a theme: its layers (drawn in order) and the cursor effect.
/// The backdrop (colour / gradient / picture) is drawn by the renderer underneath.
internal sealed class WallpaperScene : IDisposable
{
    public WallpaperOptions Options = new();
    public readonly List<Layer> Layers = new();
    uint cursorColor; int[] scatterChars = Array.Empty<int>();
    readonly Random rng = new();

    /// Time spent per layer (ms, summed), for profiling.
    public readonly Dictionary<string, double> Timing = new();
    readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
    public bool Profile;

    public int Fps => Options.Fps;
    public bool Animated => Layers.Any(l => l.Enabled && l.Animated);
    public bool Parallax => Layers.Any(l => l.Enabled && (l.ParallaxX != 0 || l.ParallaxY != 0));
    public bool CursorOn => Options.Cursor.Enabled;

    public static WallpaperScene Load(Theme theme, List<string> problems)
    {
        var s = new WallpaperScene();
        try { s.Options = WallpaperOptions.Load(theme, problems); }
        catch (Exception e) { problems.Add($"wallpaper.toml: {e.Message}"); }
        s.cursorColor = theme[s.Options.Cursor.Color].Packed;
        s.scatterChars = Cp.Of(s.Options.Cursor.ScatterChars);
        if (s.scatterChars.Length == 0) s.scatterChars = new[] { (int)'#' };
        foreach (var o in s.Options.Layers)
        {
            try { s.Layers.Add(Layer.Create(o, theme, problems)); }
            catch (Exception e) { problems.Add($"wallpaper.toml: {o.Type} layer: {e.Message}"); }
        }
        return s;
    }

    public void Draw(Frame f)
    {
        f.Grid.Clear(0);
        int k = 0;
        foreach (var l in Layers)
        {
            k++;
            if (!l.Enabled) continue;
            double t0 = sw.Elapsed.TotalMilliseconds;
            if (l.ParallaxX != 0 || l.ParallaxY != 0)
            {
                float nx = f.Grid.W > 0 && f.CursorX > -1e5f ? Math.Clamp(f.CursorX / f.Grid.W * 2 - 1, -1, 1) : 0;
                float ny = f.Grid.H > 0 && f.CursorY > -1e5f ? Math.Clamp(f.CursorY / f.Grid.H * 2 - 1, -1, 1) : 0;
                f.Grid.ShiftX = (int)MathF.Round(-nx * l.ParallaxX); f.Grid.ShiftY = (int)MathF.Round(-ny * l.ParallaxY);
            }
            l.Draw(f);
            f.Grid.ShiftX = f.Grid.ShiftY = 0;
            if (Profile) { var key = $"{k}:{l.Type}"; Timing[key] = Timing.GetValueOrDefault(key) + sw.Elapsed.TotalMilliseconds - t0; }
        }
        if (CursorOn) Cursor(f);
    }

    void Cursor(Frame f)
    {
        var c = Options.Cursor;
        var g = f.Grid;
        float cx = f.CursorX, cy = f.CursorY;
        float radius = c.Radius, ry = radius / f.Aspect;
        int x0 = (int)(cx - radius) - 1, x1 = (int)(cx + radius) + 1, y0 = (int)(cy - ry) - 1, y1 = (int)(cy + ry) + 1;
        if (x1 < 0 || y1 < 0 || x0 >= g.W || y0 >= g.H) return;
        for (int y = Math.Max(0, y0); y <= Math.Min(g.H - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(g.W - 1, x1); x++)
            {
                float dx = x + 0.5f - cx, dy = (y + 0.5f - cy) * f.Aspect;
                float d = MathF.Sqrt(dx * dx + dy * dy) / radius;
                if (d >= 1) continue;
                float k = 1 - d; k = k * k * (3 - 2 * k);
                int i = y * g.W + x;
                if (c.Glow > 0) g.Bg[i] = Px.Over(g.Bg[i], Px.Alpha(cursorColor, k * c.Glow));
                if (g.Ch[i] == 32) continue;
                if (c.Scatter > 0 && rng.NextDouble() < c.Scatter * k) g.Ch[i] = scatterChars[rng.Next(scatterChars.Length)];
                uint fg = g.Fg[i];
                float a = (fg >> 24) / 255f;
                g.Fg[i] = Px.Alpha(Px.Lerp(fg | 0xFF000000, cursorColor | 0xFF000000, k * c.Strength), a + (1 - a) * k * c.Strength);
            }
    }

    public void Dispose()
    {
        foreach (var l in Layers) l.Dispose();
        Layers.Clear();
    }
}
