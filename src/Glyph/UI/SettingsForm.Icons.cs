using System.Drawing.Drawing2D;
using System.IO.Compression;
using System.Drawing.Text;
using Glyph.Render;

namespace Glyph.UI;

/// The Icons page (Google's Material Symbols: click one to copy {icon:name}) and the Fonts page (font files
/// Glyph can use besides Windows' own).
internal sealed partial class SettingsForm
{
    // ───────────────────────── Icons ─────────────────────────

    readonly IconGrid iconGrid = new() { Dock = DockStyle.Fill };
    readonly TextBox iconSearch = new() { Width = 260, PlaceholderText = "Search icons (e.g. wifi, play, battery)" };
    readonly ComboBox iconStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };

    Control BuildIcons()
    {
        var page = new Panel { Dock = DockStyle.Fill };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        top.Controls.Add(iconSearch);
        top.Controls.Add(new Label { Text = "Style", AutoSize = true, Margin = new Padding(16, 6, 6, 0), ForeColor = Dark.Dim });
        top.Controls.Add(iconStyle);
        var count = new Label { AutoSize = true, Margin = new Padding(16, 6, 0, 0), ForeColor = Dark.Dim };
        top.Controls.Add(count);
        foreach (var s in Symbols.Styles) iconStyle.Items.Add(s);
        iconStyle.SelectedIndex = 0;

        void Filter()
        {
            var q = iconSearch.Text.Trim().ToLowerInvariant().Replace(' ', '_');
            var all = Symbols.All;
            iconGrid.SetItems(q.Length == 0 ? all : all.Where(i => i.Name.Contains(q)).ToList());
            count.Text = $"{iconGrid.Count} of {all.Count} icons";
        }
        iconSearch.TextChanged += (_, _) => Filter();
        iconStyle.SelectedIndexChanged += (_, _) =>
        {
            var st = (string)iconStyle.SelectedItem!;
            if (!iconGrid.UseStyle(st))
            {
                Symbols.EnsureStyle(st);
                status.Text = $"The {st} icons are downloaded the first time a theme uses them (Colours & look > Icon style); showing outlined until then.";
                iconGrid.UseStyle("outlined");
            }
        };
        iconGrid.Picked += name =>
        {
            var code = "{icon:" + name + "}";
            try { Clipboard.SetText(code); status.Text = $"Copied {code}. Paste it into any text: bar modules, widget text and titles, button labels."; }
            catch { status.Text = code; }
        };
        page.Controls.Add(iconGrid);
        page.Controls.Add(Hint("Google's Material Symbols (fonts.google.com/icons). Click an icon to copy {icon:name}, then paste it anywhere text is shown: bar modules, widget text and titles, the media buttons (Widgets > Media > Play button…). " +
                               "Style, filled and size are per theme: Colours & look > Icon style / Filled icons / Icon size.", 48));
        page.Controls.Add(top);
        iconGrid.UseStyle("outlined");
        Filter();
        return page;
    }

    /// A scrolling grid of icons with their names; only the visible ones are drawn.
    sealed class IconGrid : Control
    {
        const int CellW = 104, CellH = 84;
        readonly VScrollBar bar = new() { Dock = DockStyle.Right };
        IReadOnlyList<(string Name, int Cp)> items = Array.Empty<(string, int)>();
        PrivateFontCollection? pfc; Font? iconFont; string style = "";
        int hover = -1;
        public event Action<string>? Picked;
        public int Count => items.Count;

        public IconGrid()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Controls.Add(bar);
            bar.Scroll += (_, _) => Invalidate();
            Cursor = Cursors.Hand;
        }

        /// Uses the icon font of a style; false if it isn't on this PC yet.
        public bool UseStyle(string st)
        {
            if (st == style && iconFont != null) return true;
            var file = new[] { Fonts.BuiltInDir, Fonts.UserDir }.Select(d => Path.Combine(d, Symbols.FileOf(st))).FirstOrDefault(File.Exists);
            if (file == null) return false;
            try
            {
                var p = new PrivateFontCollection();
                p.AddFontFile(file);
                var fam = p.Families.FirstOrDefault(f => f.Name == Symbols.FamilyOf(st)) ?? p.Families[0];
                iconFont?.Dispose(); pfc?.Dispose();
                pfc = p; iconFont = new Font(fam, 30, GraphicsUnit.Pixel); style = st;
                Invalidate();
                return true;
            }
            catch { return false; }
        }

        public void SetItems(IReadOnlyList<(string, int)> list) { items = list; bar.Value = 0; hover = -1; Layout2(); Invalidate(); }

        int Cols => Math.Max(1, (Width - bar.Width) / CellW);

        void Layout2()
        {
            int rows = (items.Count + Cols - 1) / Cols;
            bar.Maximum = Math.Max(0, rows * CellH);
            bar.LargeChange = Math.Max(1, Height);
            bar.SmallChange = CellH / 2;
            bar.Enabled = rows * CellH > Height;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Layout2(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, bar.Maximum - bar.LargeChange + 1);
            bar.Value = Math.Clamp(bar.Value - e.Delta, 0, max);
            Invalidate();
        }

        int At(Point p)
        {
            int c = p.X / CellW, r = (p.Y + bar.Value) / CellH;
            if (c >= Cols) return -1;
            int i = r * Cols + c;
            return i >= 0 && i < items.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = At(e.Location); if (h != hover) { hover = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); }
        protected override void OnMouseClick(MouseEventArgs e) { int i = At(e.Location); if (i >= 0) Picked?.Invoke(items[i].Name); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Dark.Bg);
            if (iconFont == null) return;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cols = Cols, first = bar.Value / CellH * cols;
            using var text = new SolidBrush(Dark.Text);
            using var dim = new SolidBrush(Dark.Dim);
            using var hl = new SolidBrush(Dark.Raised);
            using var small = new Font("Segoe UI", 8f);
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            var nameFmt = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            for (int i = first; i < items.Count; i++)
            {
                int r = i / cols, c = i % cols;
                int y = r * CellH - bar.Value;
                if (y > Height) break;
                var cell = new Rectangle(c * CellW, y, CellW, CellH);
                if (i == hover) g.FillRectangle(hl, Rectangle.Inflate(cell, -3, -3));
                g.DrawString(char.ConvertFromUtf32(items[i].Cp), iconFont, i == hover ? text : text, new RectangleF(cell.X, cell.Y + 4, CellW, 48), center);
                g.DrawString(items[i].Name, small, dim, new RectangleF(cell.X + 4, cell.Y + 56, CellW - 8, 18), nameFmt);
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) { iconFont?.Dispose(); pfc?.Dispose(); } base.Dispose(disposing); }
    }

    // ───────────────────────── Fonts ─────────────────────────

    readonly ListBox fontList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly Panel fontPreview = new() { Dock = DockStyle.Fill };
    PrivateFontCollection? previewFonts;

    sealed record FontFile(string Path, string Family, bool BuiltIn)
    {
        public override string ToString() => BuiltIn ? $"{Family}   (comes with Glyph)" : $"{Family}   ({System.IO.Path.GetFileName(Path)})";
    }

    Control BuildFonts()
    {
        var split = Split(460);
        var buttons = Buttons(92);
        Btn(buttons, "Add font files…", AddFonts, true);
        Btn(buttons, "Remove", RemoveFont);
        Btn(buttons, "Open fonts folder", () => { Directory.CreateDirectory(Fonts.UserDir); Open(Fonts.UserDir); });
        Btn(buttons, "Find fonts on Google Fonts", () => Open("https://fonts.google.com"));
        split.Panel1.Controls.Add(fontList);
        split.Panel1.Controls.Add(Hint("Fonts Glyph can use besides Windows' own. Get free ones from fonts.google.com (Get font > Download: a .zip), then Add font files… and pick the .zip or its .ttf/.otf files. " +
                                       "They're only used by Glyph, not installed into Windows. Pick them anywhere a font is asked for: Colours & look, Bar, Frames, each widget's Box > Font.", 64));
        split.Panel1.Controls.Add(buttons);
        split.Panel2.Controls.Add(fontPreview);
        fontPreview.Paint += PaintFontPreview;
        fontList.SelectedIndexChanged += (_, _) => fontPreview.Invalidate();
        RefreshFonts();
        return split;
    }

    void RefreshFonts(string? select = null)
    {
        previewFonts?.Dispose(); previewFonts = new PrivateFontCollection();
        fontList.BeginUpdate();
        fontList.Items.Clear();
        foreach (var f in Fonts.Files().OrderBy(f => f.StartsWith(Fonts.UserDir, StringComparison.OrdinalIgnoreCase)).ThenBy(Path.GetFileName))
        {
            string fam = Path.GetFileNameWithoutExtension(f);
            try
            {
                using var one = new PrivateFontCollection();
                one.AddFontFile(f);
                if (one.Families.Length > 0) fam = one.Families[0].Name;
                previewFonts.AddFontFile(f);
            }
            catch { }
            var item = new FontFile(f, fam, !f.StartsWith(Fonts.UserDir, StringComparison.OrdinalIgnoreCase));
            fontList.Items.Add(item);
            if (select != null && string.Equals(f, select, StringComparison.OrdinalIgnoreCase)) fontList.SelectedItem = item;
        }
        fontList.EndUpdate();
        if (fontList.SelectedIndex < 0 && fontList.Items.Count > 0) fontList.SelectedIndex = 0;
        Glyph.Options.FontNameConverter.Forget();
        fontPreview.Invalidate();
    }

    void PaintFontPreview(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Dark.Bg);
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        if (fontList.SelectedItem is not FontFile f || previewFonts == null) return;
        var fam = previewFonts.Families.FirstOrDefault(x => x.Name == f.Family);
        using var dim = new SolidBrush(Dark.Dim);
        using var text = new SolidBrush(Dark.Text);
        g.DrawString(f.Family, Font, dim, 16, 12);
        if (fam == null) { g.DrawString("No preview for this font here (Glyph can still use it).", Font, dim, 16, 40); return; }
        float y = 44;
        bool icons = f.Family.StartsWith("Material Symbols", StringComparison.Ordinal);
        foreach (var size in new[] { 36f, 24f, 16f })
        {
            using var font = new Font(fam, size, GraphicsUnit.Pixel);
            var sample = icons ? string.Concat(new[] { "home", "wifi", "settings", "play_arrow", "search", "battery_full", "volume_up", "favorite" }.Select(n => Symbols.Text(n) ?? "")) : "The quick brown fox jumps over 0123456789";
            g.DrawString(sample, font, text, 16, y);
            y += size * 1.6f;
        }
    }

    void AddFonts()
    {
        using var dlg = new OpenFileDialog { Title = "Add fonts", Multiselect = true, Filter = "Fonts or a Google Fonts download (*.ttf;*.otf;*.ttc;*.zip)|*.ttf;*.otf;*.ttc;*.otc;*.zip|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        Directory.CreateDirectory(Fonts.UserDir);
        int added = 0; string? last = null;
        foreach (var file in dlg.FileNames)
        {
            try
            {
                if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // a Google Fonts download: take the font files out (the static ones, if it has both kinds)
                    using var zip = System.IO.Compression.ZipFile.OpenRead(file);
                    var fonts = zip.Entries.Where(z => z.Name.Length > 0 && new[] { ".ttf", ".otf" }.Contains(Path.GetExtension(z.Name).ToLowerInvariant())).ToList();
                    var stat = fonts.Where(z => z.FullName.Contains("static/", StringComparison.OrdinalIgnoreCase)).ToList();
                    var take = fonts.Any(z => !z.FullName.Contains("static/", StringComparison.OrdinalIgnoreCase)) ? fonts.Where(z => !z.FullName.Contains("static/", StringComparison.OrdinalIgnoreCase)).ToList() : stat;
                    foreach (var z in take) { last = Path.Combine(Fonts.UserDir, z.Name); z.ExtractToFile(last, true); added++; }
                }
                else { last = Path.Combine(Fonts.UserDir, Path.GetFileName(file)); File.Copy(file, last, true); added++; }
            }
            catch (Exception ex) { status.Text = $"Couldn't add {Path.GetFileName(file)}: {ex.Message}"; }
        }
        RefreshFonts(last);
        if (added > 0) status.Text = $"Added {added} font file{(added == 1 ? "" : "s")}. They're in every font list now (reopen a list if it was open).";
    }

    void RemoveFont()
    {
        if (fontList.SelectedItem is not FontFile f) return;
        if (f.BuiltIn) { status.Text = "That font comes with Glyph (it draws the icons)."; return; }
        if (MessageBox.Show(this, $"Remove {f.Family} ({Path.GetFileName(f.Path)}) from Glyph's fonts?", "Fonts", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        previewFonts?.Dispose(); previewFonts = null;
        try { File.Delete(f.Path); status.Text = $"Removed {f.Family}. Anything set to it falls back to the theme's font."; }
        catch (Exception ex) { status.Text = "Couldn't remove it (in use?): " + ex.Message; }
        RefreshFonts();
    }
}
