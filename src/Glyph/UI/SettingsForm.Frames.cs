using Glyph.Options;
using Glyph.Render;

namespace Glyph.UI;

/// The Frames page: the theme's box designs (frames.toml), with a live preview drawn by the same code as the desktop.
internal sealed partial class SettingsForm
{
    readonly ListBox frameList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid frameGrid = Grid();
    readonly PictureBox framePreview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Normal };
    readonly TextBox sampleTitle = new() { Width = 220, Text = "NET.TRACE // 追跡" };
    readonly TextBox sampleRight = new() { Width = 120, Text = "LIVE" };
    readonly ComboBox sampleAs = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    readonly Label frameUse = new() { Dock = DockStyle.Bottom, Height = 46, ForeColor = Dark.Dim, UseMnemonic = false };
    SoftCanvas? soft;
    bool dirtyFrames;
    readonly System.Windows.Forms.Timer frameTimer = new() { Interval = 60 };

    /// Starting points for a new frame.
    static readonly (string name, Func<FrameOptions> make)[] FramePresets =
    {
        ("Plain box", () => new FrameOptions { Name = "box" }),
        ("Cut corner", () => new FrameOptions { Name = "cut", CutBottomRight = 12, Border = "accent_dim", Header = "line", TitleUpper = true, TitleSpacing = 1.5f, Radius = 0 }),
        ("Title strip + corner brackets", () => new FrameOptions { Name = "strip", Header = "bar", HeaderFill = "accent_dim", HeaderOpacity = 0.5f, HeaderLine = "accent_dim", Border = "accent_dim", Brackets = "accent", BracketLength = 8, BracketWidth = 1.5f, BracketOffset = 2, TitleUpper = true, TitleSpacing = 1.2f, Radius = 0 }),
        ("Tab on top (good for windows)", () => new FrameOptions { Name = "tab", Header = "tab", Fill = "none", Border = "accent", HeaderFill = "accent_dim", HeaderLine = "accent", TabSlant = 12, TabWidth = 96, TitleUpper = true, CornerMark = "accent_dim", Radius = 0, Shadow = 0 }),
        ("Filled tag", () => new FrameOptions { Name = "tag", Fill = "accent", Border = "none", TitleColor = "bg", TitleFont = "Bahnschrift", TitleSize = 15, TitleUpper = true, TitleSpacing = 2, CutBottomRight = 8, Radius = 0 }),
        ("Underline only", () => new FrameOptions { Name = "underline", Fill = "none", Border = "accent", BorderSides = "bottom", Header = "plain", Shadow = 0, Radius = 0 }),
        ("Slanted (like /  /)", () => new FrameOptions { Name = "slanted", Slant = 10, Border = "accent_dim", Header = "none", Radius = 0 }),
        ("Scanlines + glow", () => new FrameOptions { Name = "glow", Pattern = "scanlines", PatternColor = "accent_dim", PatternOpacity = 0.25f, Glow = "accent", GlowOpacity = 0.3f, Border = "accent", Radius = 0 }),
        ("Hatch + ruler", () => new FrameOptions { Name = "ruler", Hatch = "accent_dim", Ticks = "line", TicksSide = "top", Stripe = "accent", StripeSide = "left", StripeWidth = 2, BorderSides = "left", Border = "line", Header = "none", Radius = 0 }),
    };

    Control BuildFrames()
    {
        var split = Split(300);
        var buttons = Buttons(92);
        Button? add = null;
        add = Btn(buttons, "New frame ▾", () => DropDown(add!, FramePresets.Select(p => (p.name, (Action)(() => AddFrame(p.make()))))), true);
        Btn(buttons, "Duplicate", () => { if (frameList.SelectedItem is FrameOptions f) { var c = Binder.Clone(f); c.Name = UniqueFrameName(f.Name); AddFrame(c); } });
        Btn(buttons, "Remove", () => { if (frameList.SelectedItem is FrameOptions f) { theme.Frames.Remove(f); AfterFrames(null); } });
        split.Panel1.Controls.Add(frameList);
        split.Panel1.Controls.Add(Hint("Box designs for this theme. Widgets, bar modules, buttons, the launcher and window frames pick one by name (their Frame option).", 64));
        split.Panel1.Controls.Add(buttons);

        var right = Split(330, true);
        var top = new Panel { Dock = DockStyle.Fill };
        var strip = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        strip.Controls.Add(new Label { Text = "Title:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        strip.Controls.Add(sampleTitle);
        strip.Controls.Add(new Label { Text = "Right:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        strip.Controls.Add(sampleRight);
        strip.Controls.Add(new Label { Text = "Show as:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        sampleAs.Items.AddRange(new object[] { "panel", "window", "bar module", "button" });
        sampleAs.SelectedIndex = 0;
        strip.Controls.Add(sampleAs);
        top.Controls.Add(framePreview);
        top.Controls.Add(strip);
        top.Controls.Add(frameUse);
        right.Panel1.Controls.Add(top);
        right.Panel2.Controls.Add(frameGrid);
        split.Panel2.Controls.Add(right);

        frameList.SelectedIndexChanged += (_, _) => { if (!loading) { frameGrid.SelectedObject = frameList.SelectedItem; PreviewFrame(); } };
        frameGrid.PropertyValueChanged += (_, e) =>
        {
            dirtyFrames = true; Changed(ref dirtyFrames);
            if (e.ChangedItem?.PropertyDescriptor?.Name == nameof(FrameOptions.Name)) AfterFrames(frameList.SelectedItem as FrameOptions, mark: false);
            PreviewFrame();
        };
        sampleTitle.TextChanged += (_, _) => PreviewFrame();
        sampleRight.TextChanged += (_, _) => PreviewFrame();
        sampleAs.SelectedIndexChanged += (_, _) => PreviewFrame();
        framePreview.SizeChanged += (_, _) => { frameTimer.Stop(); frameTimer.Start(); };
        frameTimer.Tick += (_, _) => { frameTimer.Stop(); PreviewFrame(); };
        return split;
    }

    string UniqueFrameName(string baseName)
    {
        var n = baseName; int i = 2;
        while (theme.Frames.Any(f => f.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) n = $"{baseName}{i++}";
        return n;
    }

    void AddFrame(FrameOptions f)
    {
        f.Name = UniqueFrameName(f.Name);
        theme.Frames.Add(f);
        AfterFrames(f);
    }

    void AfterFrames(FrameOptions? select, bool mark = true)
    {
        FrameNameConverter.Names = theme.Frames.Select(f => f.Name).Prepend("auto").Prepend("none").ToArray();
        RefreshList(frameList, theme.Frames, select, frameGrid);
        if (mark) Changed(ref dirtyFrames);
        PreviewFrame();
    }

    void RefreshFrames()
    {
        FrameNameConverter.Names = theme.Frames.Select(f => f.Name).Prepend("auto").Prepend("none").ToArray();
        RefreshList(frameList, theme.Frames, null, frameGrid);
        PreviewFrame();
    }

    /// Where this frame is used in the theme (so you know what changing it affects).
    string FrameUses(string name)
    {
        var uses = new List<string>();
        bool Is(string v) => v.Equals(name, StringComparison.OrdinalIgnoreCase);
        var ui = theme.Options.Ui;
        if (Is(ui.PanelFrame)) uses.Add("every panel (Panel frame)");
        if (Is(ui.ButtonFrame)) uses.Add("buttons");
        foreach (var w in widgets.Widgets) if (Is(w.Frame)) uses.Add($"widget \"{w}\"");
        uses.AddRange(FrameUsers(name));
        if (Is(bar.Bar.Frame)) uses.Add("the bar");
        if (Is(bar.Bar.ModuleFrame)) uses.Add("bar modules");
        foreach (var m in bar.Left.Concat(bar.Center).Concat(bar.Right))
        {
            if (Is(m.Frame)) uses.Add($"module {m}");
            if (m is Glyph.Bar.WorkspacesModule ws && (Is(ws.ActiveFrame) || Is(ws.InactiveFrame))) uses.Add("workspace numbers");
        }
        var l = theme.Options.Launcher;
        if (Is(l.Frame) || Is(l.RowFrame) || Is(l.SelectedFrame) || Is(l.NumberFrame)) uses.Add("the launcher");
        var win = theme.Options.Windows;
        if (Is(win.Frame) || Is(win.InactiveFrame)) uses.Add("window frames");
        return uses.Count == 0 ? "Not used yet: pick it as the Frame of a widget, bar module, launcher or window frames." : "Used by: " + string.Join(", ", uses.Distinct().Take(8)) + (uses.Count > 8 ? "…" : "");
    }

    void PreviewFrame()
    {
        if (theme == null || framePreview.Width < 40 || framePreview.Height < 40) return;
        int W = framePreview.Width, H = framePreview.Height;
        var f = frameList.SelectedItem as FrameOptions;
        frameUse.Text = f == null ? "Add a frame with New frame ▾." : FrameUses(f.Name);
        try
        {
            soft ??= new SoftCanvas();
            var c = soft.Begin(W, H);
            c.Fill(new RectangleF(0, 0, W, H), theme["bg"]);
            // a hint of the backdrop so see-through fills read right
            for (int y = 0; y < H; y += 24) c.Fill(new RectangleF(0, y, W, 1), theme["line"], 0, 0.35f);
            if (f != null)
            {
                float fs = theme.Options.Ui.FontSize;
                string kind = sampleAs.SelectedItem as string ?? "panel";
                RectangleF r;
                var text = theme["text"]; var dim = theme["dim"];
                switch (kind)
                {
                    case "button": r = new RectangleF(W / 2f - 40, H / 2f - 18, 80, 36); break;
                    case "bar module": r = new RectangleF(W / 2f - 110, H / 2f - 15, 220, 30); break;
                    case "window": r = new RectangleF(60, 70, W - 120, H - 120); break;
                    default: r = new RectangleF(60, 40, Math.Min(W - 120, 460), Math.Min(H - 80, 220)); break;
                }
                if (kind == "window")
                {
                    // a pretend window: the frame goes around it
                    c.Fill(r, theme["surface"]);
                    c.Text("C:\\> _", new RectangleF(r.X + 12, r.Y + 10, 300, 24), text, theme.Options.Font.Font, fs);
                    var bw = f.Border != "none" ? f.BorderWidth : 0;
                    float above = FramePainter.Above(f, theme, fs, sampleTitle.Text.Length > 0);
                    float barH = f.Header == "bar" && sampleTitle.Text.Length > 0 ? FramePainter.StripH(f, theme, fs) : 0;
                    var outer = RectangleF.FromLTRB(r.Left - 2 - bw, r.Top - 2 - bw - barH - above, r.Right + 2 + bw, r.Bottom + 2 + bw);
                    FramePainter.Draw(c, f, theme, outer, sampleTitle.Text, sampleRight.Text, fs, false, fill: false);
                }
                else
                {
                    bool withTitle = kind == "panel";
                    FramePainter.Draw(c, f, theme, r, withTitle ? sampleTitle.Text : "", withTitle ? sampleRight.Text : "", fs);
                    var ins = FramePainter.Space(f, theme, fs, withTitle && sampleTitle.Text.Length > 0);
                    var content = RectangleF.FromLTRB(r.Left + ins.L, r.Top + ins.T, r.Right - ins.R, r.Bottom - ins.B);
                    if (kind == "panel")
                    {
                        string[] lines = { "0x8DCA  TRACE  9B:6A  ACK   87DC-3254", "0x8E1F  ROUTE  41:0C  OK    1B9A-77E0", "0x8E44  AUTH   C2:19  LOCKED  F0A3-0C11" };
                        for (int i = 0; i < lines.Length && content.Y + i * fs * 1.5f < content.Bottom - fs; i++)
                            c.Text(lines[i], new RectangleF(content.X, content.Y + i * fs * 1.5f, content.Width, fs * 1.5f), i == lines.Length - 1 ? theme["bright"] : dim, theme.Options.Font.Font, fs * 0.9f);
                    }
                    else c.Text(kind == "button" ? "⏸" : "CPU  ▮▮▮▯  40", new RectangleF(r.X, r.Y + (r.Height - fs * 1.4f) / 2, r.Width, fs * 1.5f), text, theme.Options.Ui.Font, fs, false, "center");
                    // the same frame under the mouse, small
                    if (f.HoverFill != "none" && kind != "window")
                    {
                        var hr = new RectangleF(r.X, Math.Min(H - 44, r.Bottom + 30), Math.Min(160, r.Width), 30);
                        FramePainter.Draw(c, f, theme, hr, "", "", fs, hot: true);
                        c.Text("under the mouse", new RectangleF(hr.X, hr.Y + 6, hr.Width, 20), dim, theme.Options.Ui.Font, fs * 0.85f, false, "center");
                    }
                }
            }
            soft.End();
            var old = framePreview.Image;
            framePreview.Image = soft.ToBitmap();
            old?.Dispose();
        }
        catch (Exception e) { frameUse.Text = "Can't draw the preview: " + e.Message; }
    }

    void SaveFrames()
    {
        if (!dirtyFrames || themeName.Length == 0) return;
        foreach (var f in theme.Frames) Clamp(f);
        new FramesFile { Frames = theme.Frames }.Save(themeName);
        dirtyFrames = false;
    }
}
