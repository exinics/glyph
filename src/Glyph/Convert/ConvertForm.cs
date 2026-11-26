using System.Diagnostics;
using System.Globalization;

namespace Glyph.Convert;

/// The converter window: pick a file, tweak settings with a live preview, convert, add to the wallpaper.
internal sealed class ConvertForm : Form
{
    readonly TextBox input = new(), output = new(), chars = new();
    readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox loop = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown blend = new() { Minimum = 0.1m, Maximum = 10, DecimalPlaces = 1, Increment = 0.25m, Value = 1 };
    readonly NumericUpDown width = new() { Minimum = 0, Maximum = 2000 }, fps = new() { Minimum = 0, Maximum = 120, DecimalPlaces = 1 },
        start = new() { Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Increment = 0.5m }, length = new() { Minimum = 0, Maximum = 100000, DecimalPlaces = 2, Increment = 1 };
    readonly TrackBar brightness = Slider(-100, 100, 0), contrast = Slider(10, 300, 100), gamma = Slider(20, 300, 100), edge = Slider(1, 60, 8), when = Slider(0, 1000, 0);
    readonly CheckBox invert = new() { Text = "Invert (swap light and dark)" }, autoLevels = new() { Text = "Auto levels", Checked = true },
        color = new() { Text = "Keep original colours" }, dither = new() { Text = "Dither", Checked = true }, mp4 = new() { Text = "Also save an MP4 video" };
    readonly Button browse = new() { Text = "Browse…" }, convert = new() { Text = "Convert" }, addToWallpaper = new() { Text = "Add to wallpaper", Enabled = false },
        openFolder = new() { Text = "Open clips folder" }, cancel = new() { Text = "Cancel", Enabled = false };
    readonly PictureBox preview = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    readonly ProgressBar progress = new() { Dock = DockStyle.Fill };
    readonly Label status = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Label modeHelp = new() { AutoSize = false, Height = 34, Dock = DockStyle.Fill };
    readonly System.Windows.Forms.Timer previewDelay = new() { Interval = 350 };
    readonly Theme theme;
    FFmpeg.Info? info;
    CancellationTokenSource? job;
    int previewVersion;
    string? lastClip;

    static readonly Dictionary<string, string> ModeHelp = new()
    {
        ["shape"] = "Best detail: each cell gets the character whose shape matches the picture.",
        ["ramp"] = "Classic: brighter = denser character, from the list below (faintest first).",
        ["edges"] = "Outlines drawn with - | / \\ over ramp shading. Great for line art and logos.",
        ["blocks"] = "Chunky pixels from quarter blocks (▘▝▖▗▀▄▌▐█).",
        ["braille"] = "Finest: 2×4 dots per character. Smooth pictures, retro dot-matrix look.",
    };

    static TrackBar Slider(int min, int max, int val) => new() { Minimum = min, Maximum = max, Value = val, TickStyle = TickStyle.None, Dock = DockStyle.Fill, AutoSize = false, Height = 26 };

    public ConvertForm(string? file = null)
    {
        theme = Theme.Load(AppSettings.Load(new List<string>()).Theme, new List<string>());
        Text = "Glyph converter";
        Width = 1280; Height = 800; MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        Font = new Font("Segoe UI", 9.5f);
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch { }

        mode.Items.AddRange(ConvertOptions.Modes);
        mode.SelectedItem = "shape";
        loop.Items.AddRange(new object[] { "crossfade (end fades into the start)", "bounce (forwards, then backwards)", "off" });
        loop.SelectedIndex = 0;

        var left = new TableLayoutPanel { Dock = DockStyle.Left, Width = 380, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control c, string? tip = null)
        {
            var l = new Label { Text = label, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            c.Dock = DockStyle.Fill;
            left.Controls.Add(l); left.Controls.Add(c);
            if (tip != null) { var t = new ToolTip(); t.SetToolTip(l, tip); t.SetToolTip(c, tip); }
        }
        void Full(Control c) { c.Dock = DockStyle.Fill; left.Controls.Add(c); left.SetColumnSpan(c, 2); }

        var filePanel = new TableLayoutPanel { ColumnCount = 2, Height = 30, Margin = Padding.Empty };
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        input.Dock = DockStyle.Fill; browse.Dock = DockStyle.Fill;
        filePanel.Controls.Add(input); filePanel.Controls.Add(browse);
        Full(new Label { Text = "Video, GIF or picture (or drop one on this window)", AutoSize = true });
        Full(filePanel);
        Row("Mode", mode);
        Full(modeHelp);
        Row("Characters", chars, "Leave empty for the mode's default. For ramp/edges: faintest first.");
        Row("Width", width, "Width in characters. 0 = as wide as your screen.");
        Row("Brightness", brightness);
        Row("Contrast", contrast);
        Row("Gamma", gamma, "Higher brightens the dark parts.");
        Row("Edge strength", edge, "Edges mode: lower finds more edges.");
        Full(invert); Full(autoLevels); Full(dither); Full(color);
        Row("Start (s)", start);
        Row("Length (s)", length, "0 = to the end.");
        Row("FPS", fps, "0 = the video's own (at most 30).");
        Row("Loop", loop, "Clips repeat on the wallpaper. crossfade: the last part fades into the start, so there's no jump. bounce: plays forwards then backwards.");
        Row("Fade (s)", blend, "crossfade: how many seconds the end and the start overlap.");
        Row("Save as", output, "Name of the clip in config\\clips.");
        Full(mp4);
        var buttons = new FlowLayoutPanel { Height = 76, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 8, 0, 0) };
        foreach (var b in new[] { convert, cancel, addToWallpaper, openFolder }) { b.Width = 120; b.Height = 30; buttons.Controls.Add(b); }
        Full(buttons);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, Padding = new Padding(8) };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        right.Controls.Add(preview, 0, 0);
        var whenRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        whenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        whenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        whenRow.Controls.Add(new Label { Text = "Preview at", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        whenRow.Controls.Add(when);
        right.Controls.Add(whenRow, 0, 1);
        right.Controls.Add(progress, 0, 2);
        right.Controls.Add(status, 0, 3);
        Controls.Add(right);
        Controls.Add(left);

        ApplyTheme(this);
        status.Text = "Pick a file to start.";
        UpdateModeHelp();

        browse.Click += (_, _) => Browse();
        convert.Click += async (_, _) => await Convert();
        cancel.Click += (_, _) => job?.Cancel();
        addToWallpaper.Click += (_, _) => AddToWallpaper();
        openFolder.Click += (_, _) => { Directory.CreateDirectory(Paths.Clips); Process.Start(new ProcessStartInfo(Paths.Clips) { UseShellExecute = true }); };
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) LoadFile(f[0]); };
        previewDelay.Tick += async (_, _) => { previewDelay.Stop(); await RefreshPreview(); };
        foreach (Control c in new Control[] { mode, chars, width, brightness, contrast, gamma, edge, invert, autoLevels, dither, color, when, start })
        {
            switch (c)
            {
                case ComboBox cb: cb.SelectedIndexChanged += (_, _) => { UpdateModeHelp(); Schedule(); }; break;
                case TrackBar tb: tb.ValueChanged += (_, _) => Schedule(); break;
                case CheckBox ck: ck.CheckedChanged += (_, _) => Schedule(); break;
                case NumericUpDown nu: nu.ValueChanged += (_, _) => Schedule(); break;
                default: c.TextChanged += (_, _) => Schedule(); break;
            }
        }
        if (file != null) Shown += (_, _) => LoadFile(file);
    }

    void ApplyTheme(Control root)
    {
        Color C(string n) { var c = theme[n]; return Color.FromArgb(c.R, c.G, c.B); }
        var bg = C("bg"); var surface = C("surface"); var text = C("text"); var accent = C("accent"); var line = C("line");
        void Walk(Control c)
        {
            c.ForeColor = text;
            c.BackColor = c is TextBox or NumericUpDown or ComboBox ? surface : bg;
            if (c is Button b) { b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = accent; b.BackColor = surface; }
            if (c is TextBox t) t.BorderStyle = BorderStyle.FixedSingle;
            if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
            foreach (Control k in c.Controls) Walk(k);
        }
        Walk(root);
        preview.BackColor = C("bg");
        modeHelp.ForeColor = C("dim");
        status.ForeColor = C("dim");
    }

    void UpdateModeHelp()
    {
        var m = (string)mode.SelectedItem!;
        modeHelp.Text = ModeHelp[m];
        edge.Enabled = m == "edges";
        chars.Enabled = m is "ramp" or "edges" or "shape";
        chars.PlaceholderText = m == "shape" ? "(all printable ASCII)" : m is "ramp" or "edges" ? ConvertOptions.DefaultRamp : "";
    }

    void Browse()
    {
        using var d = new OpenFileDialog { Filter = "Videos, GIFs and pictures|*.mp4;*.mkv;*.webm;*.mov;*.avi;*.gif;*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*" };
        if (d.ShowDialog(this) == DialogResult.OK) LoadFile(d.FileName);
    }

    void LoadFile(string file)
    {
        input.Text = file;
        output.Text = Path.GetFileNameWithoutExtension(file);
        try
        {
            info = FFmpeg.Probe(file);
            status.Text = info.IsImage ? $"Picture, {info.Width}×{info.Height}" : $"Video, {info.Width}×{info.Height}, {info.Fps:0.##} fps, {info.Duration:0.0} s";
            when.Enabled = !info.IsImage;
            start.Enabled = length.Enabled = fps.Enabled = !info.IsImage;
            mp4.Enabled = !info.IsImage;
            Schedule();
        }
        catch (Exception e) { info = null; status.Text = "Can't read that file: " + e.Message; }
    }

    ConvertOptions Options() => new()
    {
        Input = input.Text,
        Output = output.Text.Trim(),
        Mode = (string)mode.SelectedItem!,
        Chars = chars.Text,
        Columns = (int)width.Value,
        Brightness = brightness.Value / 100f,
        Contrast = contrast.Value / 100f,
        Gamma = gamma.Value / 100f,
        EdgeThreshold = edge.Value / 100f,
        Invert = invert.Checked,
        AutoLevels = autoLevels.Checked,
        Color = color.Checked,
        Dither = dither.Checked ? "ordered" : "none",
        Loop = ConvertOptions.Loops[Math.Max(0, loop.SelectedIndex)],
        LoopBlend = (double)blend.Value,
        Start = (double)start.Value,
        Length = (double)length.Value,
        Fps = (double)fps.Value,
    };

    void Schedule() { if (info != null) { previewDelay.Stop(); previewDelay.Start(); } }

    async Task RefreshPreview()
    {
        if (info == null) return;
        var o = Options();
        double t = info.IsImage ? 0 : o.Start + (Math.Max(0.01, (o.Length > 0 ? o.Length : info.Duration - o.Start)) * when.Value / 1000.0);
        t = Math.Min(t, Math.Max(0, info.Duration - 0.05));
        int version = ++previewVersion;
        try
        {
            var (bmp, l) = await Task.Run(() => { var b = ConvertJob.Preview(o, theme, t, out var lay); return (b, lay); });
            if (version != previewVersion) { bmp.Dispose(); return; }
            var old = preview.Image;
            preview.Image = bmp;
            old?.Dispose();
            if (job == null) status.Text = $"Preview at {t:0.0} s: {l.Cols}×{l.Rows} characters" + (info.IsImage ? "" : $", {l.Frames} frames at {l.Fps:0.#} fps");
        }
        catch (Exception e) { if (version == previewVersion) status.Text = "Preview failed: " + e.Message; }
    }

    async Task Convert()
    {
        if (info == null) { status.Text = "Pick a file first."; return; }
        var o = Options();
        job = new CancellationTokenSource();
        var ct = job.Token;
        convert.Enabled = false; cancel.Enabled = true; addToWallpaper.Enabled = false;
        var bar = new Progress<(int d, int t, string what)>(p => { progress.Maximum = Math.Max(1, p.t); progress.Value = Math.Min(p.d, progress.Maximum); status.Text = $"{p.what}… {p.d}/{p.t}"; });
        IProgress<(int, int, string)> rep = bar;
        try
        {
            bool wantMp4 = mp4.Checked && mp4.Enabled;
            var res = await Task.Run(() =>
            {
                var r = ConvertJob.Run(o, theme, (d, t) => rep.Report((d, t, "Converting")), ct);
                if (wantMp4)
                {
                    var video = Path.ChangeExtension(r.Path, ".mp4");
                    ConvertJob.ExportVideo(r.Path, video, theme, o.Color ? "original" : "gradient", (d, t) => rep.Report((d, t, "Saving MP4")), ct);
                }
                return r;
            }, ct);
            lastClip = res.Path;
            addToWallpaper.Enabled = true;
            status.Text = $"Saved {Path.GetFileName(res.Path)}: {res.Frames} frames, {res.Cols}×{res.Rows}, {res.Bytes / 1024.0 / 1024.0:0.0} MB, {res.Took.TotalSeconds:0.0} s" + (wantMp4 ? " (+ MP4 next to it)" : "");
        }
        catch (OperationCanceledException) { status.Text = "Cancelled."; }
        catch (Exception e) { status.Text = "Failed: " + e.Message; Log.Write("convert: " + e); }
        finally { job = null; convert.Enabled = true; cancel.Enabled = false; progress.Value = 0; }
    }

    /// Adds a clip layer to the end of wallpaper.toml (nothing else in the file is touched).
    void AddToWallpaper()
    {
        if (lastClip == null) return;
        var path = Glyph.Wallpaper.WallpaperOptions.FilePath(theme.Name);
        var name = Path.GetFileName(lastClip);
        var block = $"""


# Added by the converter on {DateTime.Now:yyyy-MM-dd HH:mm}. Layers above still show underneath;
# set enabled = false on them if you only want the clip.
[[layer]]
type      = "clip"
file      = "{name}"
fit       = "cover"         # contain, cover, stretch, none
colors    = "{(color.Checked ? "original" : "gradient")}"       # gradient, original, single
gradient  = ["bg", "accent_dim", "accent", "bright"]
threshold = 0.05          # cells darker than this stay empty (so layers underneath show)
speed     = 1.0
""";
        File.AppendAllText(path, block);
        status.Text = $"Added {name} to wallpaper.toml. It's on your desktop now.";
        addToWallpaper.Enabled = false;
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1;
        DwmSetWindowAttribute(Handle, 20, ref on, 4); // dark title bar
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        job?.Cancel();
        preview.Image?.Dispose();
        base.OnFormClosed(e);
    }
}
