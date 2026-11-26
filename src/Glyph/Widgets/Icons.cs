using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Glyph.Convert;

namespace Glyph.Widgets;

/// Gets icon pictures: your own image if set, otherwise the app's icon from Windows (cached as PNG).
internal static class IconSource
{
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }

    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct DIBSECTION
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits;
        public int biSize, biWidth, biHeight; public ushort biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        public int dsBitfields0, dsBitfields1, dsBitfields2; public IntPtr dshSection; public int dsOffset;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out DIBSECTION ds);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);

    const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;

    /// The picture to use for a shortcut, or null if Windows has none.
    public static Bitmap? Get(Shortcut s, string? theme)
    {
        try
        {
            if (s.Image.Length > 0)
            {
                var path = Paths.FindAsset(s.Image, theme, "icons");
                if (File.Exists(path)) return LoadFile(path);
                Log.Write($"icon image not found: {s.Image}");
            }
            var cache = Path.Combine(Paths.IconCache, s.IconKey + ".png");
            if (File.Exists(cache)) return LoadFile(cache);
            var bmp = FromUrlFile(s.Target) ?? OnSta(() => Extract(s.Target, 256));
            if (bmp != null && Framed(bmp))
            {
                // Windows put a small icon in a faint frame: ask for the small icon itself
                var small = OnSta(() => Extract(s.Target, 48));
                if (small != null) { bmp.Dispose(); bmp = small; }
            }
            if (bmp != null) bmp = Trim(bmp);
            if (bmp != null)
            {
                Directory.CreateDirectory(Paths.IconCache);
                bmp.Save(cache, ImageFormat.Png);
            }
            return bmp;
        }
        catch (Exception e) { Log.Write($"icon for {s.Name}: {e.Message}"); return null; }
    }

    /// Internet shortcuts (.url, e.g. Steam games) name a proper .ico file; Windows' own picture for them is tiny.
    static Bitmap? FromUrlFile(string target)
    {
        if (!target.EndsWith(".url", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) return null;
        var line = File.ReadAllLines(target).FirstOrDefault(l => l.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase));
        var ico = line?["IconFile=".Length..].Trim();
        if (string.IsNullOrEmpty(ico) || !File.Exists(ico)) return null;
        try { return LoadFile(ico); } catch { return null; }
    }

    /// True if the picture has the faint square frame Windows draws round icons that have no big size.
    static bool Framed(Bitmap b)
    {
        if (b.Width < 96) return false;
        int[] xs = { 2, b.Width - 3 }, ys = { 2, b.Height - 3 };
        int framed = 0;
        foreach (var x in xs) foreach (var y in ys) { var a = b.GetPixel(x, y).A; if (a >= 8 && a <= 140) framed++; }
        return framed >= 3;
    }

    /// Cuts away empty borders (so small icons fill their space) and makes the result square.
    static unsafe Bitmap Trim(Bitmap src)
    {
        using var argb = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(argb)) g.DrawImage(src, 0, 0, src.Width, src.Height);
        src.Dispose();
        int minX = argb.Width, minY = argb.Height, maxX = -1, maxY = -1;
        var data = argb.LockBits(new Rectangle(0, 0, argb.Width, argb.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < argb.Height; y++)
        {
            byte* row = (byte*)data.Scan0 + y * data.Stride;
            for (int x = 0; x < argb.Width; x++)
                if (row[x * 4 + 3] > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        }
        argb.UnlockBits(data);
        if (maxX < 0) return new Bitmap(argb);
        int w = maxX - minX + 1, h = maxY - minY + 1, side = Math.Max(w, h);
        var dst = new Bitmap(side, side, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst))
            g.DrawImage(argb, new Rectangle((side - w) / 2, (side - h) / 2, w, h), new Rectangle(minX, minY, w, h), GraphicsUnit.Pixel);
        return dst;
    }

    /// Loads without keeping the file locked.
    static Bitmap LoadFile(string path)
    {
        if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var ico = new Icon(path, 256, 256);
            return ico.ToBitmap();
        }
        using var fs = File.OpenRead(path);
        using var img = Image.FromStream(fs);
        return new Bitmap(img);
    }

    static T OnSta<T>(Func<T> f)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA) return f();
        T result = default!;
        Exception? err = null;
        var t = new Thread(() => { try { result = f(); } catch (Exception e) { err = e; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start(); t.Join();
        if (err != null) throw err;
        return result;
    }

    /// The shell's own picture for a file, folder, shortcut or shell: location.
    public static Bitmap? Extract(string target, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        IShellItemImageFactory f;
        try { SHCreateItemFromParsingName(target, IntPtr.Zero, ref iid, out f); }
        catch { return null; }
        try
        {
            if (f.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out var hbm) != 0 || hbm == IntPtr.Zero) return null;
            try { return FromHBitmap(hbm); }
            finally { DeleteObject(hbm); }
        }
        finally { Marshal.ReleaseComObject(f); }
    }

    static unsafe Bitmap? FromHBitmap(IntPtr hbm)
    {
        if (GetObject(hbm, Marshal.SizeOf<DIBSECTION>(), out var ds) == 0 || ds.bmBits == IntPtr.Zero || ds.bmBitsPixel != 32)
            return Image.FromHbitmap(hbm);
        int w = ds.bmWidth, h = Math.Abs(ds.bmHeight);
        bool bottomUp = ds.biHeight > 0;
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        bool anyAlpha = false;
        for (int y = 0; y < h; y++)
        {
            byte* src = (byte*)ds.bmBits + (bottomUp ? h - 1 - y : y) * ds.bmWidthBytes;
            byte* dst = (byte*)data.Scan0 + y * data.Stride;
            Buffer.MemoryCopy(src, dst, w * 4, w * 4);
            for (int x = 0; x < w; x++) if (src[x * 4 + 3] != 0) { anyAlpha = true; break; }
        }
        if (!anyAlpha) // no alpha channel at all: solid, except pure black (the usual empty background)
            for (int y = 0; y < h; y++)
            {
                byte* dst = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < w; x++) dst[x * 4 + 3] = (byte)(dst[x * 4] == 0 && dst[x * 4 + 1] == 0 && dst[x * 4 + 2] == 0 ? 0 : 255);
            }
        bmp.UnlockBits(data);
        return bmp;
    }
}

/// Recolours icons to fit the theme.
internal static class IconStyler
{
    /// Resizes to size x size with high quality (keeps the picture's shape, centred).
    public static Bitmap Fit(Bitmap src, int size)
    {
        var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dst);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        float s = Math.Min(size / (float)src.Width, size / (float)src.Height);
        float w = src.Width * s, h = src.Height * s;
        g.DrawImage(src, (size - w) / 2, (size - h) / 2, w, h);
        return dst;
    }

    /// Returns premultiplied BGRA pixels for the style (original, tint, mono).
    public static unsafe byte[] Pixels(Bitmap fitted, string style, Gradient tint, Rgba mono)
    {
        int w = fitted.Width, h = fitted.Height;
        var outp = new byte[w * h * 4];
        var data = fitted.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
            {
                byte* row = (byte*)data.Scan0 + y * data.Stride;
                for (int x = 0; x < w; x++)
                {
                    byte b = row[x * 4], g = row[x * 4 + 1], r = row[x * 4 + 2], a = row[x * 4 + 3];
                    int o = (y * w + x) * 4;
                    if (style == "tint")
                    {
                        float l = (0.2126f * r + 0.7152f * g + 0.0722f * b) / 255f;
                        var c = Rgba.FromPacked(tint.At(l));
                        r = c.R; g = c.G; b = c.B;
                    }
                    else if (style == "mono") { r = mono.R; g = mono.G; b = mono.B; }
                    outp[o] = (byte)(b * a / 255); outp[o + 1] = (byte)(g * a / 255); outp[o + 2] = (byte)(r * a / 255); outp[o + 3] = a;
                }
            }
        }
        finally { fitted.UnlockBits(data); }
        return outp;
    }

    /// Draws the icon with characters: returns cols*rows cells (code point and colour).
    public static unsafe (int[] ch, uint[] fg) Ascii(Bitmap src, int cols, int rows, string mode, Gradient grad)
    {
        int W = cols * AsciiConverter.SX, H = rows * AsciiConverter.SY;
        using var fitted = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(fitted))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, W, H);
        }
        var rgb = new byte[W * H * 3];
        var data = fitted.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < H; y++)
        {
            byte* row = (byte*)data.Scan0 + y * data.Stride;
            for (int x = 0; x < W; x++)
            {
                // see-through parts become black (empty); solid parts are at least fairly bright,
                // so dark icons still show as a shape
                float a = row[x * 4 + 3] / 255f;
                float l = (0.2126f * row[x * 4 + 2] + 0.7152f * row[x * 4 + 1] + 0.0722f * row[x * 4]) / 255f;
                byte v = (byte)(255 * a * (0.4f + 0.6f * l));
                int i = (y * W + x) * 3;
                rgb[i] = v; rgb[i + 1] = v; rgb[i + 2] = v;
            }
        }
        fitted.UnlockBits(data);
        var o = new ConvertOptions { Mode = mode, AutoLevels = false, Dither = "ordered" };
        var conv = new AsciiConverter(o, cols, rows, "Consolas", 14, 9, 17);
        var cells = new byte[cols * rows * 2];
        conv.Convert(rgb, cells);
        var ch = new int[cols * rows]; var fg = new uint[cols * rows];
        for (int i = 0; i < ch.Length; i++)
        {
            ch[i] = conv.CharTable[cells[i * 2]];
            fg[i] = grad.At(cells[i * 2 + 1] / 255f);
        }
        return (ch, fg);
    }
}
