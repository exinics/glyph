using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Drawing.Text;
using System.Globalization;
using Glyph.Convert;
using Glyph.Options;
using Glyph.Render;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using FileEditor = System.Windows.Forms.Design.FileNameEditor;

namespace Glyph.Widgets;

internal sealed class VideoOptions : WidgetOptions
{
    public override string Type => "video";
    public VideoOptions() { Anchor = "center"; OffsetX = 0; OffsetY = 0; }

    [Category("Video"), Description("A video (mp4, webm, mkv, mov, gif…; looked for in the theme folder and config\\videos) or an ASCII clip made by the converter (.gclip, config\\clips). Normal videos need ffmpeg."), Editor(typeof(FileEditor), typeof(UITypeEditor))]
    public string File { get; set; } = "";
    [Category("Video"), Description("contain shows all of it, cover fills the box (may crop), stretch fills it exactly."), Choices("contain", "cover", "stretch"), TypeConverter(typeof(ChoicesConverter))]
    public string Fit { get; set; } = "contain";
    [Category("Video"), Description("Playback speed."), Range(0.05, 10)] public float Speed { get; set; } = 1;
    [Category("Video"), Description("Off = play once and stop on the last frame.")] public bool Loop { get; set; } = true;
    [Category("Video"), DisplayName("Click to pause"), Description("Clicking the video pauses and resumes it (when On click is empty).")] public bool ClickToPause { get; set; } = true;
    [Category("Video"), DisplayName("On click"), Description(Glyph.Data.Actions.Help)] public string OnClick { get; set; } = "";
    [Category("Video"), DisplayName("Max frames per second"), Description("Videos faster than this skip frames (saves power). The desktop's own frame rate also limits it."), Range(1, 120)] public int MaxFps { get; set; } = 30;
    [Category("Video"), Description("Who decodes the video. gpu = the graphics chip: much less CPU but ~50-70 MB more memory. cpu = less memory, fine for small videos. auto = gpu for videos bigger than 640×480. Try cpu if a video won't play."), Choices("auto", "gpu", "cpu"), TypeConverter(typeof(ChoicesConverter))]
    public string Decoder { get; set; } = "auto";

    [Category("Look"), Description("auto = characters in terminal themes, the real picture in smooth ones. ascii = turn the video into characters. original = the real picture. (.gclip clips are always characters.)"), Choices("auto", "original", "ascii"), TypeConverter(typeof(ChoicesConverter))]
    public string Style { get; set; } = "auto";
    [Category("Look"), Description("Characters: gradient = theme colours by brightness, original = the video's colours, single = one colour."), Choices("gradient", "original", "single"), TypeConverter(typeof(ChoicesConverter))]
    public string Colors { get; set; } = "gradient";
    [Category("Look"), Description("Dark to bright, for colors = gradient."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "accent_dim", "accent", "bright" };
    [Category("Look"), Description("For colors = single."), Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "accent";
    [Category("Look"), Description("Characters, faintest first, picked by brightness. For a .gclip, empty = the clip's own characters."), DisplayName("Characters")] public string Chars { get; set; } = " .:-=+*#%@";
    [Category("Look"), DisplayName("Character size"), Description("Smooth style: size of the characters compared with the theme's character font."), Range(0.3, 6)] public float CharScale { get; set; } = 1;
    [Category("Look"), Description("Cells darker than this are left empty, so the box shows through."), Range(0, 1)] public float Threshold { get; set; } = 0.04f;
    [Category("Look"), Range(0, 10)] public float Brightness { get; set; } = 1;
    [Category("Look"), DisplayName("Video opacity"), Description("0 = invisible, 1 = solid."), Range(0, 1)] public float VideoOpacity { get; set; } = 1;

    [Category("Box"), Description("Draw a box behind the video.")] public bool Panel { get; set; } = true;
    [Category("Box"), Description("Width in pixels."), Range(16, 8000)] public int Width { get; set; } = 480;
    [Category("Box"), Description("Height in pixels."), Range(16, 8000)] public int Height { get; set; } = 270;
    [Category("Box"), Description("Space between the box edge and the video, in pixels."), Range(0, 400)] public int Padding { get; set; }

    public override string ToString() => $"video{(File.Length > 0 ? ": " + Path.GetFileName(File) : "")}{(Enabled ? "" : " (off)")}";
}

/// A box that plays a video or an ASCII clip, as characters or as the real picture.
internal sealed class VideoWidget : Widget
{
    readonly VideoOptions o;
    readonly Theme T;
    readonly string path;
    readonly bool isClip;
    readonly Rgba boxBg, border, dim;
    readonly Gradient grad;
    readonly uint single;
    readonly int[] ramp;
    ClipReader? clip;
    FfmpegVideo? video;
    string error = "";

    float play, lastTime = -1, clipAcc;
    bool paused, ended;

    // layout
    bool terminal;
    int cx0, cy0, wc, hc;          // the box in cells (terminal)
    RectangleF inner;              // where the video may draw, pixels
    RectangleF dest;               // where it does draw, pixels
    int cols, rows, gw, gh;        // character frame size and glyph size (smooth characters)
    bool asChars;

    // smooth drawing
    Canvas? owner;
    ID2D1Bitmap1? bmp; int bw, bh;
    byte[] raster = Array.Empty<byte>();
    int rasterFrame = -1, uploaded = -1;
    readonly Dictionary<int, byte[]> glyphs = new();
    string glyphKey = "";
    // terminal characters from a video
    int[] fch = Array.Empty<int>(); uint[] ffg = Array.Empty<uint>(); int charsFrame = -1;

    public VideoWidget(VideoOptions o, Theme t)
    {
        this.o = o; T = t;
        boxBg = o.BoxColor(t); border = o.BoxBorder(t); dim = t["dim"];
        grad = new Gradient(t, o.Gradient); single = t[o.Color].Packed;
        ramp = Cp.Of(o.Chars.Length > 0 ? o.Chars : " .:-=+*#%@");
        isClip = o.File.EndsWith(".gclip", StringComparison.OrdinalIgnoreCase);
        path = o.File.Length == 0 ? "" : Paths.FindAsset(o.File, t.Name, isClip ? "clips" : "videos");
        if (path.Length == 0) { error = "pick a video: File (Video)"; return; }
        if (!System.IO.File.Exists(path)) { error = "not found: " + Path.GetFileName(o.File); return; }
        if (isClip)
        {
            try { clip = new ClipReader(path); clip.Next(); }
            catch (Exception e) { error = "can't read clip: " + e.Message; clip = null; }
        }
        else video = new FfmpegVideo(path, o.Loop, o.Decoder);
    }

    bool Playing => !paused && !ended && error.Length == 0;
    public override bool Animated => Playing || (video != null && !video.Ready && video.Error.Length == 0);

    public override string Signature(WidgetEnv e) =>
        $"{Hover}|{paused}|{error}|{(clip != null ? clip.Index : video?.Frame ?? -2)}|{video?.Error}";

    public override int HitTest(float x, float y) => Bounds.Contains(x, y) && (o.OnClick.Length > 0 || o.ClickToPause) ? 0 : -1;

    public override void Click(int item, int count, WidgetEnv e)
    {
        if (count != 1) return;
        if (o.OnClick.Length > 0) e.Do?.Invoke(o.OnClick);
        else if (o.ClickToPause) { paused = !paused; if (!paused && ended) { ended = false; play = 0; clip?.Rewind(); clipAcc = 0; video?.Restart(0); } }
    }

    // ───────────── layout + timing ─────────────

    public override void Layout(WidgetEnv e)
    {
        terminal = e.Painter.Terminal;
        float dt = lastTime < 0 ? 0 : Math.Clamp(e.Time - lastTime, 0, 0.1f);
        lastTime = e.Time;
        if (Playing) play += dt * o.Speed;

        var pt = Place(o.Anchor, o.Width, o.Height, e.Area, o.OffsetX, o.OffsetY);
        if (terminal)
        {
            wc = Math.Max(2, (int)Math.Round(o.Width / (float)e.CellW)); hc = Math.Max(2, (int)Math.Round(o.Height / (float)e.CellH));
            cx0 = (int)MathF.Round(pt.X / e.CellW); cy0 = (int)MathF.Round(pt.Y / e.CellH);
            Bounds = new RectangleF(cx0 * e.CellW, cy0 * e.CellH, wc * e.CellW, hc * e.CellH);
            int edge = o.Panel && o.BoxBorderWidth(T) != 0 ? 1 : 0;
            int px = edge + (int)Math.Round(o.Padding / (float)e.CellW), py = edge + (int)Math.Round(o.Padding / (float)e.CellH);
            inner = new RectangleF((cx0 + px) * e.CellW, (cy0 + py) * e.CellH, Math.Max(1, wc - 2 * px) * e.CellW, Math.Max(1, hc - 2 * py) * e.CellH);
        }
        else
        {
            Bounds = new RectangleF(pt.X, pt.Y, o.Width, o.Height);
            inner = RectangleF.Inflate(Bounds, -o.Padding, -o.Padding);
            if (inner.Width < 1 || inner.Height < 1) inner = Bounds;
        }
        if (error.Length > 0) return;
        if (clip != null) LayoutClip(e, dt);
        else if (video != null) LayoutVideo(e);
    }

    static (float w, float h) FitSize(string fit, float sw, float sh, float bw, float bh)
    {
        if (fit == "stretch") return (bw, bh);
        float s = fit == "cover" ? Math.Max(bw / sw, bh / sh) : Math.Min(bw / sw, bh / sh);
        return (sw * s, sh * s);
    }

    static RectangleF Centered(RectangleF box, float w, float h) => new(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);

    void LayoutClip(WidgetEnv e, float dt)
    {
        var c = clip!;
        if (Playing)
        {
            clipAcc += dt * o.Speed * c.Fps;
            int steps = 0;
            while (clipAcc >= 1 && steps < 8)
            {
                clipAcc -= 1; steps++;
                if (!o.Loop && c.Index + 1 >= c.FrameCount) { ended = true; break; }
                if (!c.Next()) { ended = true; break; }
            }
            if (clipAcc > 8) clipAcc = 0;
        }
        asChars = true;
        cols = c.Width; rows = c.Height;
        float aspect = c.CellAspect > 0 ? c.CellAspect : e.CellH / (float)e.CellW;   // cell height / width the clip was made for
        var (fw, fh) = FitSize(o.Fit, c.Width, c.Height * aspect, inner.Width, inner.Height);
        dest = Centered(inner, fw, fh);
        if (!terminal)
        {
            gw = Math.Max(2, (int)Math.Round(fw / c.Width)); gh = Math.Max(2, (int)Math.Round(fh / c.Height));
            while ((long)gw * gh * c.Width * c.Height > 8_000_000) { gw = Math.Max(1, gw / 2); gh = Math.Max(1, gh / 2); }
        }
    }

    void LayoutVideo(WidgetEnv e)
    {
        var v = video!;
        if (v.Error.Length > 0) { error = v.Error; return; }
        if (!v.Ready) return;
        var style = o.Style == "auto" ? (terminal ? "ascii" : "original") : o.Style;
        asChars = style == "ascii";
        var (fw, fh) = FitSize(o.Fit, v.SrcW, v.SrcH, inner.Width, inner.Height);
        var full = Centered(inner, fw, fh);
        var vis = RectangleF.Intersect(full, inner);
        if (vis.Width < 1 || vis.Height < 1) return;
        float dx, dy;                       // pixels per sample
        if (asChars)
        {
            if (terminal) { dx = e.CellW; dy = e.CellH; }
            else { dx = Math.Max(2, MathF.Round(e.CellW * o.CharScale)); dy = Math.Max(2, MathF.Round(e.CellH * o.CharScale)); }
        }
        else { float p = Math.Max(1, fw / v.SrcW); dx = dy = p; }
        int sw = Math.Max(1, (int)MathF.Round(fw / dx)), sh = Math.Max(1, (int)MathF.Round(fh / dy));
        int cw = Math.Clamp((int)MathF.Round(vis.Width / dx), 1, sw), ch = Math.Clamp((int)MathF.Round(vis.Height / dy), 1, sh);
        if (terminal && asChars)
        {
            // snap to whole cells
            int vx = (int)MathF.Round(vis.X / e.CellW), vy = (int)MathF.Round(vis.Y / e.CellH);
            dest = new RectangleF(vx * e.CellW, vy * e.CellH, cw * e.CellW, ch * e.CellH);
        }
        else dest = new RectangleF(vis.X + (vis.Width - cw * dx) / 2, vis.Y + (vis.Height - ch * dy) / 2, cw * dx, ch * dy);
        if (!asChars) dest = vis;
        cols = cw; rows = ch; gw = (int)dx; gh = (int)dy;
        double fps = Perf.VideoFps(v.SrcFps, o.MaxFps);
        v.Configure(sw, sh, cw, ch, fps);
        if (Playing) v.Want((int)(play * v.OutFps));
        if (v.Ended && !o.Loop) ended = true;
    }

    // ───────────── drawing: terminal ─────────────

    public override void DrawCells(Grid g, WidgetEnv e)
    {
        if (!terminal || Bounds.IsEmpty) return;
        g.FillRect(cx0, cy0, wc, hc, 0);
        if (o.Panel)
        {
            bool hot = Hover == 0 && o.BoxHoverColor != "none";
            g.FillRect(cx0, cy0, wc, hc, Px.Alpha((hot ? T[o.BoxHoverColor] : boxBg).Packed, hot ? Math.Max(0.35f, o.BoxOpacity(T)) : o.BoxOpacity(T)));
            if (o.BoxBorderWidth(T) != 0) g.Box(cx0, cy0, wc, hc, border.Packed);
        }
        if (error.Length > 0 || (video != null && !video.Ready))
        {
            var msg = error.Length > 0 ? error : "loading…";
            g.Text((int)(inner.X / e.CellW), (int)(inner.Y / e.CellH), msg.Length > wc - 2 ? msg[..Math.Max(0, wc - 2)] : msg, dim.Packed);
            return;
        }
        if (!asChars) return;   // the real picture is drawn smooth, in DrawVector
        int x0 = (int)MathF.Round(dest.X / e.CellW), y0 = (int)MathF.Round(dest.Y / e.CellH);
        int iw = Math.Max(1, (int)MathF.Round(dest.Width / e.CellW)), ih = Math.Max(1, (int)MathF.Round(dest.Height / e.CellH));
        int ix0 = (int)(inner.X / e.CellW), iy0 = (int)(inner.Y / e.CellH), ix1 = (int)(inner.Right / e.CellW), iy1 = (int)(inner.Bottom / e.CellH);
        if (clip != null)
        {
            for (int y = Math.Max(y0, iy0); y < Math.Min(y0 + ih, iy1); y++)
            {
                int sy = Math.Min(clip.Height - 1, (int)((y - y0) * (float)clip.Height / ih));
                for (int x = Math.Max(x0, ix0); x < Math.Min(x0 + iw, ix1); x++)
                {
                    int sx = Math.Min(clip.Width - 1, (int)((x - x0) * (float)clip.Width / iw));
                    var (ch, c) = ClipCell(sy * clip.Width + sx);
                    if (ch != 32) g.Put(x, y, ch, c, o.VideoOpacity);
                }
            }
        }
        else if (VideoChars())
        {
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (fch[i] != 32) g.Put(x0 + x, y0 + y, fch[i], ffg[i], o.VideoOpacity);
                }
        }
    }

    (int ch, uint c) ClipCell(int si)
    {
        var c = clip!;
        float b = Math.Min(1f, c.Bright[si] / 255f * o.Brightness);
        if (b <= o.Threshold) return (32, 0);
        int ch = c.Ch[si];
        if (o.Chars.Length > 0) ch = ramp[Math.Min(ramp.Length - 1, (int)(b * ramp.Length))];
        uint col = o.Colors switch
        {
            "original" when c.HasColor => c.Color[si],
            "single" => Px.Alpha(single, b),
            _ => grad.At(b),
        };
        return (ch, col);
    }

    /// Turns the latest video frame into characters (fch/ffg, cols × rows). False if there's no frame yet.
    bool VideoChars()
    {
        var v = video!;
        lock (v.Gate)
        {
            if (v.Frame < 0 || v.FrameW != cols || v.FrameH != rows) return false;
            if (charsFrame == v.Frame && fch.Length == cols * rows) return true;
            if (fch.Length != cols * rows) { fch = new int[cols * rows]; ffg = new uint[cols * rows]; }
            var px = v.Pixels;
            for (int i = 0; i < cols * rows; i++)
            {
                int b = px[i * 4], gg = px[i * 4 + 1], r = px[i * 4 + 2];
                float l = Math.Min(1f, (0.2126f * r + 0.7152f * gg + 0.0722f * b) / 255f * o.Brightness);
                if (l <= o.Threshold) { fch[i] = 32; continue; }
                fch[i] = ramp[Math.Min(ramp.Length - 1, (int)(l * ramp.Length))];
                ffg[i] = o.Colors switch
                {
                    "original" => (uint)(r | (gg << 8) | (b << 16) | (255 << 24)),
                    "single" => Px.Alpha(single, l),
                    _ => grad.At(l),
                };
            }
            charsFrame = v.Frame;
        }
        return true;
    }

    // ───────────── drawing: smooth ─────────────

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        if (Bounds.IsEmpty) return;
        if (!terminal)
        {
            float radius = o.BoxRadius(T);
            if (o.Panel)
            {
                c.Shadow(Bounds, radius, o.BoxShadow(T));
                c.Fill(Bounds, boxBg, radius, o.BoxOpacity(T));
                if (Hover == 0 && o.BoxHoverColor != "none") c.Fill(Bounds, T[o.BoxHoverColor], radius, 0.25f);
            }
            if (error.Length > 0 || (video != null && !video.Ready))
            {
                c.Text(error.Length > 0 ? error : "loading…", RectangleF.Inflate(inner, -8, -6), dim, e.Painter.Font, e.Painter.FontSize, false, "center", true);
            }
            else
            {
                float clipR = Math.Max(0, radius - o.Padding);
                bool layer = clipR > 0.5f;
                ID2D1Geometry? geo = null;
                if (layer)
                {
                    geo = c.Dc.Factory.CreateRoundedRectangleGeometry(new RoundedRectangle(inner, clipR, clipR));
                    c.Dc.PushLayer(new LayerParameters1
                    {
                        ContentBounds = new Rect(inner.X, inner.Y, inner.Width, inner.Height), GeometricMask = geo,
                        MaskAntialiasMode = AntialiasMode.PerPrimitive, MaskTransform = System.Numerics.Matrix3x2.Identity, Opacity = 1,
                    }, null);
                }
                else c.Dc.PushAxisAlignedClip(new Rect(inner.X, inner.Y, inner.Width, inner.Height), AntialiasMode.Aliased);
                DrawFrame(c, e);
                if (layer) c.Dc.PopLayer(); else c.Dc.PopAxisAlignedClip();
                geo?.Dispose();
            }
            if (o.Panel) c.Stroke(Bounds, border, o.BoxBorderWidth(T), radius);
            return;
        }
        // terminal themes: only the real picture is drawn here (characters went into the grid)
        if (error.Length == 0 && !asChars && video != null && video.Ready)
        {
            c.Dc.PushAxisAlignedClip(new Rect(inner.X, inner.Y, inner.Width, inner.Height), AntialiasMode.Aliased);
            DrawFrame(c, e);
            c.Dc.PopAxisAlignedClip();
        }
    }

    unsafe void DrawFrame(Canvas c, WidgetEnv e)
    {
        int frame;
        if (clip != null)
        {
            int w = cols * gw, h = rows * gh;
            frame = clip.Index;
            if (rasterFrame != frame || raster.Length != w * h * 4 || glyphKey != GlyphKey(e)) { RasterClip(e); rasterFrame = frame; }
            Upload(c, raster, w, h, frame);
        }
        else
        {
            var v = video!;
            if (asChars)
            {
                if (!VideoChars()) return;
                int w = cols * gw, h = rows * gh;
                frame = charsFrame;
                if (rasterFrame != frame || raster.Length != w * h * 4 || glyphKey != GlyphKey(e))
                {
                    Raster(e, cols, rows, i => (fch[i], ffg[i]));
                    rasterFrame = frame;
                }
                Upload(c, raster, w, h, frame);
            }
            else
            {
                lock (v.Gate)
                {
                    if (v.Frame < 0) return;
                    frame = v.Frame;
                    Upload(c, v.Pixels, v.FrameW, v.FrameH, frame);
                }
            }
        }
        if (bmp != null) c.Dc.DrawBitmap(bmp, new Rect(dest.X, dest.Y, dest.Width, dest.Height), o.VideoOpacity, asChars ? BitmapInterpolationMode.Linear : BitmapInterpolationMode.Linear, null);
    }

    unsafe void Upload(Canvas c, byte[] px, int w, int h, int frame)
    {
        if (w <= 0 || h <= 0 || px.Length < w * h * 4) return;
        if (owner != c || bmp == null || bw != w || bh != h)
        {
            bmp?.Dispose();
            var props = new BitmapProperties1(new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96);
            fixed (byte* p = px) bmp = c.Dc.CreateBitmap(new SizeI(w, h), (IntPtr)p, (uint)(w * 4), props);
            owner = c; bw = w; bh = h; uploaded = frame;
            return;
        }
        if (uploaded == frame) return;
        fixed (byte* p = px) bmp.CopyFromMemory((IntPtr)p, (uint)(w * 4));
        uploaded = frame;
    }

    string GlyphKey(WidgetEnv e) => $"{gw}x{gh}|{e.Painter.MonoFont}";

    void RasterClip(WidgetEnv e)
    {
        Raster(e, cols, rows, ClipCell);
    }

    /// Draws a frame of characters into `raster` (premultiplied BGRA), each cell gw × gh pixels.
    void Raster(WidgetEnv e, int cw, int ch, Func<int, (int ch, uint c)> cell)
    {
        var key = GlyphKey(e);
        if (key != glyphKey) { glyphs.Clear(); glyphKey = key; }
        int w = cw * gw, h = ch * gh;
        if (raster.Length != w * h * 4) raster = new byte[w * h * 4]; else Array.Clear(raster);
        for (int cy = 0; cy < ch; cy++)
            for (int cx = 0; cx < cw; cx++)
            {
                var (code, col) = cell(cy * cw + cx);
                if (code == 32) continue;
                var mask = Glyph(code, e.Painter.MonoFont);
                int r = (int)(col & 255), g = (int)((col >> 8) & 255), b = (int)((col >> 16) & 255), a = (int)(col >> 24);
                for (int y = 0; y < gh; y++)
                {
                    int row = ((cy * gh + y) * w + cx * gw) * 4, mrow = y * gw;
                    for (int x = 0; x < gw; x++)
                    {
                        int m = mask[mrow + x];
                        if (m == 0) continue;
                        int aa = m * a / 255, p = row + x * 4;
                        raster[p] = (byte)(b * aa / 255); raster[p + 1] = (byte)(g * aa / 255); raster[p + 2] = (byte)(r * aa / 255); raster[p + 3] = (byte)aa;
                    }
                }
            }
    }

    byte[] Glyph(int code, string font)
    {
        if (glyphs.TryGetValue(code, out var m)) return m;
        m = new byte[gw * gh];
        try
        {
            using var b = new Bitmap(gw, gh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                using var f = new Font(font, Math.Max(1, gh * 0.78f), GraphicsUnit.Pixel);
                using var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(char.ConvertFromUtf32(code), f, Brushes.White, new RectangleF(-gw, 0, gw * 3, gh), sf);
            }
            var d = b.LockBits(new Rectangle(0, 0, gw, gh), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var row = new byte[gw * 4];
            for (int y = 0; y < gh; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(d.Scan0 + y * d.Stride, row, 0, row.Length);
                for (int x = 0; x < gw; x++) m[y * gw + x] = row[x * 4 + 3];
            }
            b.UnlockBits(d);
        }
        catch { }
        glyphs[code] = m;
        return m;
    }

    public override void Dispose()
    {
        video?.Dispose(); clip?.Dispose(); bmp?.Dispose(); bmp = null;
    }
}

/// Decodes a video with ffmpeg on its own thread, at exactly the size it's shown at, only as fast as it's played
/// (when the desktop pauses, ffmpeg waits).
internal sealed class FfmpegVideo : IDisposable
{
    readonly string path;
    readonly bool loop;
    readonly string decoder;
    bool hardware;
    readonly Thread thread;
    readonly AutoResetEvent wake = new(false);
    volatile bool stop;
    volatile Process? current;
    public volatile bool Ready, Ended;
    public volatile string Error = "";
    public int SrcW, SrcH;
    public double SrcFps = 30, Duration;

    public readonly object Gate = new();
    public byte[] Pixels = Array.Empty<byte>();    // latest frame, BGRA (under Gate)
    public int FrameW, FrameH;
    public int Frame = -1;                          // its number

    // wanted configuration (set by the widget)
    (int sw, int sh, int cw, int ch, double fps) want, running;
    volatile int wantFrame;
    volatile bool restart;
    double restartAt;
    public double OutFps => running.fps > 0 ? running.fps : want.fps > 0 ? want.fps : SrcFps;

    public FfmpegVideo(string path, bool loop, string decoder)
    {
        this.path = path; this.loop = loop; this.decoder = decoder;
        thread = new Thread(Run) { IsBackground = true, Name = "Glyph video", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    public void Configure(int sw, int sh, int cw, int ch, double fps)
    {
        var w = (sw, sh, cw, ch, fps);
        if (w == want) return;
        lock (Gate) want = w;
        wake.Set();
    }

    public void Want(int frame)
    {
        if (frame == wantFrame) return;
        wantFrame = frame; wake.Set();
    }

    public void Restart(double at) { restartAt = at; restart = true; Ended = false; wantFrame = 0; wake.Set(); }

    void Run()
    {
        Process? p = null;
        Stream? s = null;
        byte[] back = Array.Empty<byte>();
        int got = -1;
        try
        {
            var exe = FFmpeg.Find();
            if (exe == null) { Error = "ffmpeg not found (install it: winget install Gyan.FFmpeg)"; return; }
            try
            {
                var info = FFmpeg.Probe(path);
                SrcW = info.Width; SrcH = info.Height; SrcFps = info.Fps; Duration = info.Duration;
                hardware = decoder == "gpu" || (decoder != "cpu" && SrcW * SrcH > 640 * 480);
            }
            catch (Exception e) { Error = "can't play: " + e.Message; return; }
            Ready = true;
            while (!stop)
            {
                (int sw, int sh, int cw, int ch, double fps) w;
                lock (Gate) w = want;
                if (w.sw > 0 && (w != running || restart))
                {
                    // start (or restart) ffmpeg for this size, at the current position
                    Kill(ref p);
                    double at = restart ? restartAt : running.fps > 0 ? (got + 1) / running.fps : 0;
                    restart = false;
                    if (Duration > 0 && at >= Duration) at = loop ? at % Duration : Math.Max(0, Duration - 0.05);
                    running = w;
                    int startFrame = (int)Math.Round(at * w.fps);
                    if (wantFrame < startFrame) wantFrame = startFrame;
                    var args = new List<string> { "-nostdin", "-loglevel", "error", "-threads", "1" };
                    if (hardware) args.AddRange(new[] { "-hwaccel", "d3d11va" });   // the graphics chip decodes; falls back to the CPU by itself
                    if (loop) args.AddRange(new[] { "-stream_loop", "-1" });
                    if (at > 0.01) args.AddRange(new[] { "-ss", FFmpeg.F(at) });
                    args.AddRange(new[] { "-i", path, "-an", "-sn", "-vf",
                        $"fps={FFmpeg.F(w.fps)},scale={w.sw}:{w.sh}:flags=bilinear,crop={w.cw}:{w.ch}", "-pix_fmt", "bgra", "-f", "rawvideo", "-" });
                    try { p = FFmpeg.Start(exe, args, false, true); s = p.StandardOutput.BaseStream; current = p; }
                    catch (Exception e) { Error = "can't start ffmpeg: " + e.Message; return; }
                    back = new byte[w.cw * w.ch * 4];
                    got = startFrame - 1;
                    Ended = false;
                }
                if (p == null || s == null || Ended || wantFrame <= got) { wake.WaitOne(250); continue; }
                if (wantFrame > got + 1 && wantFrame - got > running.fps * 3)
                {
                    // far behind (e.g. after a long pause): jump instead of decoding everything in between
                    restartAt = wantFrame / running.fps; restart = true; continue;
                }
                if (!ReadAll(s, back))
                {
                    if (stop) break;
                    var err = FFmpeg.Errors(p);
                    Kill(ref p); s = null;
                    if (got < 0 && err.Length > 0) { Error = "can't play: " + err.Split('\n')[0]; return; }
                    Ended = true;
                    continue;
                }
                got++;
                if (got >= wantFrame)
                {
                    lock (Gate)
                    {
                        (Pixels, back) = (back, Pixels.Length == back.Length ? Pixels : new byte[back.Length]);
                        FrameW = running.cw; FrameH = running.ch; Frame = got;
                    }
                }
            }
        }
        catch (Exception e) { if (!stop) Error = "video: " + e.Message; }
        finally { Kill(ref p); }
    }

    static bool ReadAll(Stream s, byte[] buf)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n;
            try { n = s.Read(buf, off, buf.Length - off); } catch { return false; }
            if (n <= 0) return false;
            off += n;
        }
        return true;
    }

    static void Kill(ref Process? p)
    {
        if (p == null) return;
        try { if (!p.HasExited) p.Kill(); } catch { }
        try { p.Dispose(); } catch { }
        p = null;
    }

    public void Dispose()
    {
        stop = true; wake.Set();
        try { var c = current; if (c != null && !c.HasExited) c.Kill(); } catch { }
        thread.Join(1500);
    }
}
