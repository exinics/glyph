namespace Glyph.Wallpaper;

/// What a layer gets each frame.
internal sealed class Frame
{
    public Grid Grid = null!;
    public Theme Theme = null!;
    public float Time, Dt;
    public float Aspect = 2f;          // cell height / cell width
    public float CursorX = -1e6f, CursorY = -1e6f; // cursor position in cells (fractional)
    public Random Rng = new();
}

internal abstract class Layer : IDisposable
{
    public bool Enabled = true;
    public float Opacity = 1f;
    public string Type = "";
    public float ParallaxX, ParallaxY;
    /// False if the layer never changes (then the wallpaper can stop redrawing).
    public virtual bool Animated => true;

    public abstract void Draw(Frame f);
    public virtual void Dispose() { }

    protected void Common(LayerOptions o) { Enabled = o.Enabled; Opacity = o.Opacity; Type = o.Type; ParallaxX = o.ParallaxX; ParallaxY = o.ParallaxY; }

    /// Top-left position of a w*h block placed by anchor + offset inside a W*H grid.
    protected static (int x, int y) Place(string anchor, int w, int h, int W, int H, int ox, int oy)
    {
        int x = anchor.EndsWith("left") || anchor == "left" ? 0 : anchor.EndsWith("right") || anchor == "right" ? W - w : (W - w) / 2;
        int y = anchor.StartsWith("top") ? 0 : anchor.StartsWith("bottom") ? H - h : (H - h) / 2;
        return (x + ox, y + oy);
    }

    protected static int[] Chars(string s, char fallback) { var c = Cp.Of(s); return c.Length == 0 ? new[] { (int)fallback } : c; }

    public static Layer Create(LayerOptions o, Theme theme, List<string> problems) => o switch
    {
        NoiseOptions n => new NoiseLayer(n, theme),
        RainOptions r => new RainLayer(r, theme),
        StarsOptions st => new StarsLayer(st, theme),
        StaticOptions sc => new StaticLayer(sc, theme),
        GlitchOptions g => new GlitchLayer(g, theme),
        ArtOptions a => new ArtLayer(a, theme, problems),
        ClipOptions c => new ClipLayer(c, theme, problems),
        _ => throw new ArgumentException("unknown layer " + o.Type),
    };
}

/// Flowing smoke / plasma made of characters.
/// To stay cheap, the noise is sampled on a half-width grid `update_fps` times a second, and each
/// frame blends between the last two samples. The next sample is worked out a few rows per frame,
/// so there are no spikes and no extra threads.
internal sealed class NoiseLayer : Layer
{
    readonly int[] chars; readonly Gradient grad;
    readonly float scale, speed, contrast, brightness, threshold, driftX, driftY, warp, interval;
    readonly int octaves; readonly bool invert;
    float[] prev = Array.Empty<float>(), next = Array.Empty<float>();
    int fw, fh, gw, gh, rowsDone;
    float tPrev, tNext; float aspect = 2;
    bool primed;

    public NoiseLayer(NoiseOptions o, Theme theme)
    {
        Common(o);
        chars = Chars(o.Chars, '#');
        grad = new Gradient(theme, o.Gradient);
        scale = o.Scale; speed = o.Speed; octaves = o.Octaves; contrast = o.Contrast; brightness = o.Brightness;
        threshold = o.Threshold; driftX = o.DriftX; driftY = o.DriftY; warp = o.Warp; invert = o.Invert;
        interval = 1f / o.UpdateFps;
    }

    void Row(float[] field, int y, float t)
    {
        for (int fx = 0; fx < fw; fx++)
        {
            float x = fx * 2;
            float nx = x * scale + t * driftX * scale, ny = y * scale * aspect + t * driftY * scale * aspect, nz = t * speed;
            if (warp > 0)
            {
                float wx = Noise.Perlin(nx * 0.7f + 5.2f, ny * 0.7f + 1.3f, nz), wy = Noise.Perlin(nx * 0.7f + 1.7f, ny * 0.7f + 9.2f, nz + 3.1f);
                nx += warp * (wx - 0.5f) * 2; ny += warp * (wy - 0.5f) * 2;
            }
            field[y * fw + fx] = Noise.Fbm(nx, ny, nz, octaves);
        }
    }

    public override void Draw(Frame f)
    {
        var g = f.Grid;
        aspect = f.Aspect;
        if (!primed || g.W != gw || g.H != gh)
        {
            gw = g.W; gh = g.H; fw = gw / 2 + 2; fh = gh;
            prev = new float[fw * fh]; next = new float[fw * fh];
            tPrev = f.Time; tNext = f.Time + interval;
            for (int y = 0; y < fh; y++) { Row(prev, y, tPrev); Row(next, y, tNext); }
            rowsDone = fh; primed = true;
        }
        if (f.Time >= tNext)
        {
            for (; rowsDone < fh; rowsDone++) Row(next, rowsDone, tNext);   // finish if behind
            (prev, next) = (next, prev);
            tPrev = tNext;
            tNext = Math.Max(tNext + interval, f.Time);
            if (f.Time - tPrev > interval) tPrev = f.Time;                // after a pause, don't race to catch up
            rowsDone = 0;
        }
        // work out a share of the next sample, aiming to finish a little early
        float progress = Math.Clamp((f.Time - tPrev) / interval, 0, 1);
        int target = Math.Min(fh, (int)MathF.Ceiling((progress + 0.3f) * fh));
        for (; rowsDone < target; rowsDone++) Row(next, rowsDone, tNext);

        float a = progress;
        int n = chars.Length;
        float op = Opacity;
        for (int y = 0; y < gh; y++)
        {
            int frow = y * fw, row = y * gw;
            for (int x = 0; x < gw; x++)
            {
                int fi = frow + (x >> 1);
                float v0 = prev[fi], v1 = next[fi];
                if ((x & 1) == 1) { v0 = (v0 + prev[fi + 1]) * 0.5f; v1 = (v1 + next[fi + 1]) * 0.5f; }
                float v = v0 + (v1 - v0) * a;
                v = (v - 0.5f) * contrast * 2.2f + 0.5f + brightness;
                if (invert) v = 1 - v;
                if (v <= threshold) continue;
                float k = (v - threshold) / (1 - threshold);
                if (k > 1) k = 1;
                int ch = chars[Math.Min(n - 1, (int)(k * n))];
                if (ch == 32) continue;
                uint c = grad.Lut[(int)(k * 255f)];
                int dx = x + g.ShiftX, dy = y + g.ShiftY;
                if ((uint)dx >= (uint)gw || (uint)dy >= (uint)gh) continue;
                int di = dy * gw + dx;
                g.Ch[di] = ch;
                g.Fg[di] = op >= 1f ? c : Px.Alpha(c, ((c >> 24) / 255f) * op);
            }
        }
    }
}

/// Falling (or rising) streams of characters.
internal sealed class RainLayer : Layer
{
    struct Drop { public int X; public float Y, Speed; public int Len; public int Seed; }
    readonly int[] chars; readonly Gradient grad; readonly uint head;
    readonly float density, speedMin, speedMax, changeRate; readonly int lenMin, lenMax; readonly bool up, fade;
    Drop[] drops = Array.Empty<Drop>();
    int gridW, gridH;
    readonly Random rng = new();

    public RainLayer(RainOptions o, Theme theme)
    {
        Common(o);
        chars = Chars(o.Chars, '|');
        grad = new Gradient(theme, o.Gradient);
        head = theme[o.Head].Packed;
        density = o.Density; speedMin = o.SpeedMin; speedMax = Math.Max(o.SpeedMin, o.SpeedMax);
        lenMin = o.LengthMin; lenMax = Math.Max(lenMin, o.LengthMax);
        changeRate = o.ChangeRate; up = o.Direction == "up"; fade = o.Fade;
    }

    void Spawn(ref Drop d, bool anywhere)
    {
        d.X = rng.Next(Math.Max(1, gridW));
        d.Len = rng.Next(lenMin, lenMax + 1);
        d.Speed = speedMin + (float)rng.NextDouble() * (speedMax - speedMin);
        d.Y = anywhere ? (float)rng.NextDouble() * (gridH + d.Len) : -(float)rng.NextDouble() * gridH * 0.3f;
        d.Seed = rng.Next();
    }

    public override void Draw(Frame f)
    {
        var g = f.Grid;
        if (g.W != gridW || g.H != gridH)
        {
            gridW = g.W; gridH = g.H;
            drops = new Drop[Math.Max(0, (int)(density * gridW))];
            for (int i = 0; i < drops.Length; i++) Spawn(ref drops[i], true);
        }
        for (int i = 0; i < drops.Length; i++)
        {
            ref var d = ref drops[i];
            d.Y += d.Speed * f.Dt;
            if (d.Y - d.Len > gridH) Spawn(ref d, false);
            int hy = (int)d.Y;
            for (int k = 0; k < d.Len; k++)
            {
                int row = hy - k;
                if (row < 0 || row >= gridH) continue;
                int y = up ? gridH - 1 - row : row;
                float tick = changeRate > 0 ? f.Time * changeRate + Noise.Hash(d.X, row, d.Seed) * 10 : 0;
                int ch = chars[(int)(Noise.Hash(d.X, row, d.Seed + (int)tick) * chars.Length) % chars.Length];
                float along = 1f - k / (float)d.Len;
                uint col = k == 0 ? head : grad.At(along);
                g.Put(d.X, y, ch, col, Opacity * (fade ? MathF.Max(0.08f, along) : 1f));
            }
        }
    }
}

/// A starfield: flying through ("warp") or drifting sideways ("drift").
internal sealed class StarsLayer : Layer
{
    struct Star { public float X, Y, Z; }
    readonly int[] chars; readonly Gradient grad; readonly float speed; readonly int count; readonly string mode, direction;
    readonly Star[] stars; readonly Random rng = new();

    public StarsLayer(StarsOptions o, Theme theme)
    {
        Common(o);
        chars = Chars(o.Chars, '.');
        grad = new Gradient(theme, o.Gradient);
        speed = o.Speed; count = o.Count; mode = o.Mode; direction = o.Direction;
        stars = new Star[count];
        for (int i = 0; i < count; i++) Reset(ref stars[i], true);
    }

    void Reset(ref Star s, bool anyDepth)
    {
        s.X = (float)rng.NextDouble() * 2 - 1; s.Y = (float)rng.NextDouble() * 2 - 1;
        s.Z = anyDepth ? 0.05f + (float)rng.NextDouble() * 0.95f : 1f;
    }

    public override void Draw(Frame f)
    {
        var g = f.Grid; float cx = g.W / 2f, cy = g.H / 2f;
        for (int i = 0; i < stars.Length; i++)
        {
            ref var s = ref stars[i];
            int x, y; float near;
            if (mode == "warp")
            {
                s.Z -= speed * f.Dt;
                if (s.Z <= 0.02f) Reset(ref s, false);
                x = (int)(cx + s.X / s.Z * cx);
                y = (int)(cy + s.Y / s.Z * cy);
                if (!g.In(x, y)) { if (s.Z < 0.5f) Reset(ref s, false); continue; }
                near = 1 - s.Z;
            }
            else
            {
                float v = speed * f.Dt * (1.2f - s.Z) * 0.5f;
                switch (direction)
                {
                    case "left": s.X -= v; break; case "right": s.X += v; break;
                    case "up": s.Y -= v; break; default: s.Y += v; break;
                }
                if (s.X < -1) s.X += 2; if (s.X > 1) s.X -= 2; if (s.Y < -1) s.Y += 2; if (s.Y > 1) s.Y -= 2;
                x = (int)((s.X + 1) * 0.5f * g.W); y = (int)((s.Y + 1) * 0.5f * g.H);
                near = 1 - s.Z;
            }
            int ch = chars[Math.Min(chars.Length - 1, (int)(near * chars.Length))];
            g.Put(x, y, ch, grad.At(near), Opacity);
        }
    }
}

/// TV static: random characters that flicker.
internal sealed class StaticLayer : Layer
{
    readonly int[] chars; readonly Gradient grad; readonly float density, rate;
    public StaticLayer(StaticOptions o, Theme theme)
    {
        Common(o);
        chars = Chars(o.Chars, '.');
        grad = new Gradient(theme, o.Gradient);
        density = o.Density; rate = o.Rate;
    }

    public override void Draw(Frame f)
    {
        var g = f.Grid; int tick = (int)(f.Time * rate);
        for (int y = 0; y < g.H; y++)
            for (int x = 0; x < g.W; x++)
            {
                float h = Noise.Hash(x, y, tick);
                if (h >= density) continue;
                float b = Noise.Hash(y, x, tick + 7);
                g.Put(x, y, chars[(int)(b * chars.Length) % chars.Length], grad.At(b), Opacity);
            }
    }
}

/// Glitches what's underneath: bands of rows jump sideways and scramble for a moment.
internal sealed class GlitchLayer : Layer
{
    readonly float rate, duration, scramble, tint; readonly int maxShift, bandMin, bandMax; readonly uint color; readonly int[] chars;
    readonly Random rng = new();
    readonly List<(float until, int y, int h, int shift)> active = new();
    int[] rowCh = Array.Empty<int>(); uint[] rowFg = Array.Empty<uint>();

    public GlitchLayer(GlitchOptions o, Theme theme)
    {
        Common(o);
        rate = o.Rate; duration = o.Duration; maxShift = o.MaxShift; bandMin = o.BandMin; bandMax = Math.Max(bandMin, o.BandMax);
        scramble = o.Scramble; tint = o.Tint; color = theme[o.Color].Packed;
        chars = Chars(o.Chars, '#');
    }

    public override void Draw(Frame f)
    {
        var g = f.Grid;
        if (rng.NextDouble() < rate * f.Dt)
        {
            int h = rng.Next(bandMin, bandMax + 1);
            active.Add((f.Time + duration * (0.5f + (float)rng.NextDouble()), rng.Next(Math.Max(1, g.H)), h, rng.Next(-maxShift, maxShift + 1)));
        }
        active.RemoveAll(a => a.until < f.Time);
        if (rowCh.Length < g.W) { rowCh = new int[g.W]; rowFg = new uint[g.W]; }
        foreach (var (_, by, bh, shift) in active)
        {
            for (int y = by; y < Math.Min(g.H, by + bh); y++)
            {
                int i0 = y * g.W;
                Array.Copy(g.Ch, i0, rowCh, 0, g.W); Array.Copy(g.Fg, i0, rowFg, 0, g.W);
                for (int x = 0; x < g.W; x++)
                {
                    int sx = ((x - shift) % g.W + g.W) % g.W;
                    int ch = rowCh[sx]; uint fg = rowFg[sx];
                    if (ch != 32 && rng.NextDouble() < scramble) ch = chars[rng.Next(chars.Length)];
                    if (ch != 32) fg = Px.Lerp(fg, color, tint * Opacity);
                    g.Ch[i0 + x] = ch; g.Fg[i0 + x] = fg;
                }
            }
        }
    }
}

/// ASCII art from a text file in config/art.
internal sealed class ArtLayer : Layer
{
    readonly int[][] lines = Array.Empty<int[]>();
    readonly string anchor, gradDir; readonly int ox, oy, padding; readonly uint color, background;
    readonly Gradient? grad; readonly bool transparent; readonly float pulse, pulseSpeed, glitch;
    readonly Random rng = new();
    readonly Dictionary<int, (float until, int shift)> jitter = new();

    public ArtLayer(ArtOptions o, Theme theme, List<string> problems)
    {
        Common(o);
        if (o.File.Length == 0) problems.Add("wallpaper: an art layer needs a file (a text file in config\\art)");
        else
        {
            var path = Paths.FindAsset(o.File, theme.Name, "art");
            if (File.Exists(path)) lines = File.ReadAllText(path).Replace("\r", "").TrimEnd('\n').Split('\n').Select(Cp.Of).ToArray();
            else problems.Add($"wallpaper: art file not found: {o.File}");
        }
        anchor = o.Anchor; ox = o.OffsetX; oy = o.OffsetY;
        color = theme[o.Color].Packed;
        if (o.Gradient.Length > 0) grad = new Gradient(theme, o.Gradient);
        gradDir = o.GradientDirection; transparent = o.TransparentSpaces;
        background = theme[o.Background].Packed; padding = o.Padding;
        pulse = o.Pulse; pulseSpeed = o.PulseSpeed; glitch = o.Glitch;
    }

    public override bool Animated => pulse > 0 || glitch > 0;

    public override void Draw(Frame f)
    {
        if (lines.Length == 0) return;
        var g = f.Grid;
        int w = lines.Max(l => l.Length), h = lines.Length;
        var (x0, y0) = Place(anchor, w, h, g.W, g.H, ox, oy);
        float a = Opacity * (1 - pulse * (0.5f + 0.5f * MathF.Sin(f.Time * pulseSpeed * MathF.Tau)));

        if ((background >> 24) != 0)
            for (int y = y0 - padding; y < y0 + h + padding; y++)
                for (int x = x0 - padding * 2; x < x0 + w + padding * 2; x++) { g.PutBg(x, y, background); g.Put(x, y, 32, 0); }

        if (glitch > 0 && rng.NextDouble() < glitch * f.Dt) jitter[rng.Next(h)] = (f.Time + 0.08f + (float)rng.NextDouble() * 0.15f, rng.Next(-3, 4));
        for (int ly = 0; ly < h; ly++)
        {
            int shift = jitter.TryGetValue(ly, out var j) && j.until > f.Time ? j.shift : 0;
            var line = lines[ly];
            for (int lx = 0; lx < line.Length; lx++)
            {
                int ch = line[lx];
                if (ch == 32 && transparent) continue;
                uint c = color;
                if (grad != null)
                {
                    float t = gradDir == "vertical" ? ly / (float)Math.Max(1, h - 1)
                        : gradDir == "horizontal" ? lx / (float)Math.Max(1, w - 1)
                        : (lx / (float)Math.Max(1, w - 1) + ly / (float)Math.Max(1, h - 1)) * 0.5f;
                    c = grad.At(t);
                }
                g.Put(x0 + lx + shift, y0 + ly, ch, c, a);
            }
        }
    }
}

/// Plays a .gclip made by the converter.
internal sealed class ClipLayer : Layer
{
    readonly ClipReader? clip;
    readonly string fit, anchor, colors; readonly int ox, oy; readonly float scale, speed, threshold, brightness;
    readonly bool loop; readonly Gradient grad; readonly uint single; readonly int[]? rechar;
    float acc; bool ended;

    public ClipLayer(ClipOptions o, Theme theme, List<string> problems)
    {
        Common(o);
        fit = o.Fit; anchor = o.Anchor; ox = o.OffsetX; oy = o.OffsetY; scale = o.Scale; speed = o.Speed; loop = o.Loop;
        colors = o.Colors; grad = new Gradient(theme, o.Gradient); single = theme[o.Color].Packed;
        threshold = o.Threshold; brightness = o.Brightness;
        if (o.Chars.Length > 0) rechar = Cp.Of(o.Chars);
        if (o.File.Length == 0) { problems.Add("wallpaper: a clip layer needs a file (a .gclip in config\\clips)"); return; }
        var path = Paths.FindAsset(o.File, theme.Name, "clips");
        if (!File.Exists(path)) { problems.Add($"wallpaper: clip not found: {o.File}"); return; }
        try { clip = new ClipReader(path); clip.Next(); }
        catch (Exception e) { problems.Add($"wallpaper: can't read clip {o.File} ({e.Message})"); clip = null; }
    }

    public override void Draw(Frame f)
    {
        if (clip == null) return;
        if (!ended)
        {
            acc += f.Dt * speed * clip.Fps;
            int steps = 0;
            while (acc >= 1 && steps < 8)
            {
                acc -= 1; steps++;
                if (!loop && clip.Index + 1 >= clip.FrameCount) { ended = true; break; }
                if (!clip.Next()) { ended = true; break; }
            }
            if (acc > 8) acc = 0;
        }

        var g = f.Grid;
        // clip cells may have been made for a different cell shape: correct the aspect
        float aspectFix = clip.CellAspect > 0 ? clip.CellAspect / f.Aspect : 1f;
        float cw = clip.Width, chh = clip.Height * aspectFix;
        float rw, rh;
        switch (fit)
        {
            case "stretch": rw = g.W * scale; rh = g.H * scale; break;
            case "none": rw = cw * scale; rh = chh * scale; break;
            case "contain": { float s = MathF.Min(g.W / cw, g.H / chh) * scale; rw = cw * s; rh = chh * s; break; }
            default: { float s = MathF.Max(g.W / cw, g.H / chh) * scale; rw = cw * s; rh = chh * s; break; }
        }
        int iw = Math.Max(1, (int)MathF.Round(rw)), ih = Math.Max(1, (int)MathF.Round(rh));
        var (x0, y0) = Place(anchor, iw, ih, g.W, g.H, ox, oy);
        int xa = Math.Max(0, x0), xb = Math.Min(g.W, x0 + iw), ya = Math.Max(0, y0), yb = Math.Min(g.H, y0 + ih);
        for (int y = ya; y < yb; y++)
        {
            int sy = Math.Min(clip.Height - 1, (int)((y - y0) * (float)clip.Height / ih));
            for (int x = xa; x < xb; x++)
            {
                int sx = Math.Min(clip.Width - 1, (int)((x - x0) * (float)clip.Width / iw));
                int si = sy * clip.Width + sx;
                float b = Math.Min(1f, clip.Bright[si] / 255f * brightness);
                if (b <= threshold) continue;
                int ch = clip.Ch[si];
                if (rechar != null) ch = rechar[Math.Min(rechar.Length - 1, (int)(b * rechar.Length))];
                if (ch == 32) continue;
                uint c = colors switch
                {
                    "original" when clip.HasColor => clip.Color[si],
                    "single" => Px.Alpha(single, b),
                    _ => grad.At(b),
                };
                g.Put(x, y, ch, c, Opacity);
            }
        }
    }

    public override void Dispose() => clip?.Dispose();
}
