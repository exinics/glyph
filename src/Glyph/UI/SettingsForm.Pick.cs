using System.Drawing.Imaging;
using Glyph.Options;

namespace Glyph.UI;

/// Design page: an eyedropper on the preview, and colours taken from the wallpaper (an accent, or a whole palette).
internal sealed partial class SettingsForm
{
    bool picking;

    void StartPicking()
    {
        picking = true; drawType = "";
        view.Cursor = Cursors.Cross;
        drawHint.Text = "Click the preview to take a colour (Esc to stop)";
    }

    void StopPicking() { picking = false; view.Cursor = Cursors.Default; drawHint.Text = ""; }

    /// The colour under a point of the preview view.
    Color? PreviewColor(Point viewPt)
    {
        if (frame == null || info == null) return null;
        var p = ToScreen(viewPt);
        lock (frame)
        {
            int x = (int)(p.X * frame.Width / (float)info.W), y = (int)(p.Y * frame.Height / (float)info.H);
            if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height) return null;
            var c = frame.GetPixel(x, y);
            return Color.FromArgb(c.R, c.G, c.B);
        }
    }

    void Picked(Color c, Point at)
    {
        StopPicking();
        AssignMenu(new Rgba(c.R, c.G, c.B), view, at);
    }

    /// A menu that puts colour `c` into the palette: as the accent (with matching shades), or as any colour.
    void AssignMenu(Rgba c, Control anchor, Point at)
    {
        var m = Dark.Menu();
        m.Items.Add(new ToolStripMenuItem($"{c.Hex}") { Enabled = false, Image = Swatch(c) });
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Make it the accent (and its dark and bright shades)", null, (_, _) => { SetAccent(c); });
        foreach (var (n, _) in theme.PaletteList)
        {
            var name = n;
            m.Items.Add($"Use for \"{name}\"", Swatch(theme.Palette[name]), (_, _) => { theme.SetColor(name, c); AfterPicked(); });
        }
        m.Items.Add("Add as a new colour…", null, (_, _) =>
        {
            var nm = Ask("Add colour", "Name for the new colour (letters, numbers and _):", "picked", null, out _);
            if (string.IsNullOrWhiteSpace(nm)) return;
            nm = new string(nm.Trim().Replace(' ', '_').Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray()).ToLowerInvariant();
            if (nm.Length == 0) return;
            theme.SetColor(nm, c); AfterPicked();
        });
        m.Items.Add("Copy the hex code", null, (_, _) => { try { Clipboard.SetText(c.Hex); } catch { } });
        m.Show(anchor, at);
    }

    static Bitmap Swatch(Rgba c)
    {
        var b = new Bitmap(16, 16);
        using var g = Graphics.FromImage(b);
        using var br = new SolidBrush(Color.FromArgb(c.R, c.G, c.B));
        g.FillRectangle(br, 1, 1, 14, 14);
        return b;
    }

    void SetAccent(Rgba c)
    {
        var bg = theme.Palette.TryGetValue("bg", out var b) ? b : new Rgba(10, 10, 10);
        theme.SetColor("accent", c);
        theme.SetColor("accent_dim", Rgba.Lerp(c, bg, 0.62f));
        theme.SetColor("accent_bright", Rgba.Lerp(c, new Rgba(255, 255, 255), 0.38f));
        AfterPicked();
        status.Text = $"Accent is now {c.Hex} (with matching dark and bright shades).";
    }

    void AfterPicked()
    {
        ColorNameConverter.Names = theme.PaletteList.Select(p => p.name).Append("none").Append("auto").ToArray();
        RefreshPalette(); RefreshSwatches();
        SaveSoon(ref dirtyTheme);
    }

    // ───────────── colours from the wallpaper ─────────────

    /// The wallpaper's picture if it has one, otherwise what the preview shows.
    Bitmap? WallpaperPicture()
    {
        var b = wallpaper.Backdrop;
        if (b.Type == "image" && b.File.Length > 0)
        {
            var path = Paths.FindAsset(b.File, themeName, "images");
            try { if (File.Exists(path)) { using var fs = File.OpenRead(path); using var img = Image.FromStream(fs); return new Bitmap(img, 96, Math.Max(1, 96 * img.Height / Math.Max(1, img.Width))); } } catch { }
        }
        if (frame == null) return null;
        lock (frame) return new Bitmap(frame, 96, Math.Max(1, 96 * frame.Height / Math.Max(1, frame.Width)));
    }

    /// The main colours of a picture (k-means), most common first.
    static List<(Rgba c, float share)> MainColors(Bitmap bmp, int k = 8)
    {
        var px = new List<(float r, float g, float b)>();
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        unsafe
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                byte* row = (byte*)d.Scan0 + y * d.Stride;
                for (int x = 0; x < bmp.Width; x++) px.Add((row[x * 4 + 2], row[x * 4 + 1], row[x * 4]));
            }
        }
        bmp.UnlockBits(d);
        if (px.Count == 0) return new();
        var rng = new Random(7);
        var centres = Enumerable.Range(0, k).Select(_ => px[rng.Next(px.Count)]).ToArray();
        var assign = new int[px.Count];
        for (int iter = 0; iter < 12; iter++)
        {
            for (int i = 0; i < px.Count; i++)
            {
                int best = 0; float bd = float.MaxValue;
                for (int j = 0; j < k; j++)
                {
                    float dr = px[i].r - centres[j].r, dg = px[i].g - centres[j].g, db = px[i].b - centres[j].b;
                    float dd = dr * dr * 0.3f + dg * dg * 0.59f + db * db * 0.11f;
                    if (dd < bd) { bd = dd; best = j; }
                }
                assign[i] = best;
            }
            var sum = new (float r, float g, float b, int n)[k];
            for (int i = 0; i < px.Count; i++) { var s = sum[assign[i]]; sum[assign[i]] = (s.r + px[i].r, s.g + px[i].g, s.b + px[i].b, s.n + 1); }
            for (int j = 0; j < k; j++) centres[j] = sum[j].n > 0 ? (sum[j].r / sum[j].n, sum[j].g / sum[j].n, sum[j].b / sum[j].n) : px[rng.Next(px.Count)];
        }
        var counts = new int[k];
        foreach (var a in assign) counts[a]++;
        return Enumerable.Range(0, k).Where(j => counts[j] > 0)
            .Select(j => (new Rgba((byte)centres[j].r, (byte)centres[j].g, (byte)centres[j].b), counts[j] / (float)px.Count))
            .OrderByDescending(t => t.Item2).ToList();
    }

    static float Saturation(Rgba c) { float mx = Math.Max(c.R, Math.Max(c.G, c.B)) / 255f, mn = Math.Min(c.R, Math.Min(c.G, c.B)) / 255f; return mx <= 0 ? 0 : (mx - mn) / mx; }
    static float Vivid(Rgba c, float share) => Saturation(c) * Math.Max(c.R, Math.Max(c.G, c.B)) / 255f * (0.35f + MathF.Sqrt(share));

    static Rgba Brighten(Rgba c, float minMax)
    {
        float mx = Math.Max(c.R, Math.Max(c.G, c.B));
        if (mx <= 0) return c;
        float k = Math.Max(1, minMax * 255 / mx);
        return new Rgba((byte)Math.Min(255, c.R * k), (byte)Math.Min(255, c.G * k), (byte)Math.Min(255, c.B * k));
    }

    /// A palette made from the picture's colours: the most vivid one as the accent, the darkest as the background.
    static Dictionary<string, Rgba> PaletteFrom(List<(Rgba c, float share)> cols)
    {
        var accent = Brighten(cols.OrderByDescending(t => Vivid(t.c, t.share)).First().c, 0.78f);
        var darkest = cols.OrderBy(t => t.c.Luma).First().c;
        var lightest = cols.OrderByDescending(t => t.c.Luma).First().c;
        var black = new Rgba(0, 0, 0); var white = new Rgba(255, 255, 255);
        var bg = Rgba.Lerp(darkest, black, Math.Clamp(1 - 0.06f / Math.Max(0.01f, darkest.Luma), 0, 0.92f));
        var text = Rgba.Lerp(Rgba.Lerp(lightest, new Rgba(220, 220, 220), 0.6f), white, 0.25f);
        return new()
        {
            ["bg"] = bg,
            ["surface"] = Rgba.Lerp(Rgba.Lerp(bg, accent, 0.07f), white, 0.03f),
            ["line"] = Rgba.Lerp(bg, accent, 0.22f),
            ["dim"] = Rgba.Lerp(text, bg, 0.5f),
            ["text"] = text,
            ["bright"] = Rgba.Lerp(text, white, 0.65f),
            ["accent"] = accent,
            ["accent_dim"] = Rgba.Lerp(accent, bg, 0.62f),
            ["accent_bright"] = Rgba.Lerp(accent, white, 0.38f),
        };
    }

    void ColorsFromWallpaper()
    {
        using var pic = WallpaperPicture();
        if (pic == null) { status.Text = "The preview isn't ready yet."; return; }
        var cols = MainColors(pic);
        if (cols.Count == 0) return;
        var proposal = PaletteFrom(cols);

        using var f = new Form { Text = "Colours from the wallpaper", Width = 770, Height = 430, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Dark.UiFont };
        f.Controls.Add(new Label { Text = "The wallpaper's main colours (most used first). Click one to put it in the palette:", Left = 16, Top = 12, Width = 680, Height = 24 });
        var colored = new List<(Control ctl, Rgba c)>();
        int x = 16;
        foreach (var (c, share) in cols)
        {
            var b = new Button { Left = x, Top = 42, Width = 76, Height = 58, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(c.R, c.G, c.B), Text = $"{share * 100:0}%", ForeColor = c.Luma > 0.5f ? Color.Black : Color.White };
            new ToolTip().SetToolTip(b, c.Hex);
            colored.Add((b, c));
            var cc = c;
            b.Click += (_, _) => AssignMenu(cc, b, new Point(0, b.Height));
            f.Controls.Add(b);
            x += 84;
        }
        f.Controls.Add(new Label { Text = "Or use a palette made from them (the most vivid colour becomes the accent):", Left = 16, Top = 118, Width = 680, Height = 24 });
        x = 16;
        foreach (var (n, c) in proposal)
        {
            var p = new Panel { Left = x, Top = 148, Width = 70, Height = 44, BackColor = Color.FromArgb(c.R, c.G, c.B), BorderStyle = BorderStyle.FixedSingle };
            f.Controls.Add(p);
            colored.Add((p, c));
            f.Controls.Add(new Label { Text = n, Left = x, Top = 196, Width = 74, Height = 34, ForeColor = Dark.Dim, Font = new Font("Segoe UI", 8.5f) });
            x += 76;
        }
        var accentOnly = new Button { Text = "Use just the accent", Left = 16, Top = 300, Width = 200, Height = 36 };
        var all = new Button { Text = "Use the whole palette", Left = 228, Top = 300, Width = 220, Height = 36 };
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Left = 640, Top = 300, Width = 100, Height = 36 };
        accentOnly.Click += (_, _) => { SetAccent(proposal["accent"]); f.Close(); };
        all.Click += (_, _) =>
        {
            foreach (var (n, c) in proposal) theme.SetColor(n, c);
            AfterPicked();
            status.Text = "Palette made from the wallpaper. Change any colour on Colours & look.";
            f.Close();
        };
        f.Controls.Add(accentOnly); f.Controls.Add(all); f.Controls.Add(close);
        f.Controls.Add(new Label { Text = "Tip: the eyedropper (Pick colour) takes any single colour from the preview.", Left = 16, Top = 250, Width = 680, Height = 24, ForeColor = Dark.Dim });
        f.CancelButton = close;
        Dark.Apply(f); Dark.Primary(all);
        foreach (var (ctl, c) in colored)   // the dark look recolours controls: put the swatches back
        {
            ctl.BackColor = Color.FromArgb(c.R, c.G, c.B);
            ctl.ForeColor = c.Luma > 0.5f ? Color.Black : Color.White;
            if (ctl is Button bt) { bt.FlatAppearance.BorderColor = Dark.Line; bt.FlatAppearance.MouseOverBackColor = ctl.BackColor; }
        }
        f.HandleCreated += (_, _) => Dark.TitleBar(f);
        f.ShowDialog(this);
    }
}
