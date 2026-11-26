using System.Diagnostics;
using System.Reflection;
using Glyph.Bar;
using Glyph.Options;
using Glyph.Wallpaper;
using Glyph.Widgets;

namespace Glyph.UI;

/// The settings window (Glyph.exe --settings). A separate program: it reads and writes the same
/// files the desktop uses, and the desktop picks up every save straight away. Closing it frees everything.
internal sealed partial class SettingsForm : Form
{
    readonly Panel sidebar = new() { Dock = DockStyle.Left, Width = 200, Padding = new Padding(0, 0, 0, 12) };
    readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(18, 6, 18, 10) };
    readonly Label header = new() { Dock = DockStyle.Top, Height = 48, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI Semibold", 15f), UseMnemonic = false };
    readonly Label editing = new() { Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
    readonly StatusStrip statusBar = new() { SizingGrip = false };
    readonly ToolStripStatusLabel status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripStatusLabel running = new();
    readonly System.Windows.Forms.Timer saveTimer = new() { Interval = 400 };
    readonly System.Windows.Forms.Timer runTimer = new() { Interval = 2000 };
    readonly Dictionary<string, Panel> pages = new();
    readonly Dictionary<string, Button> navButtons = new();
    string currentPage = "";

    AppSettings app = null!;
    List<Shortcut> shortcuts = null!;
    string themeName = "";
    Theme theme = null!;
    WallpaperOptions wallpaper = null!;
    WidgetsFile widgets = null!;
    BarFile bar = null!;
    bool dirtyApp, dirtyTheme, dirtyWallpaper, dirtyWidgets, dirtyShortcuts, dirtyBar;
    bool loading;

    // Themes
    readonly ListBox themeList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly Label themeInfo = new() { Dock = DockStyle.Top, Height = 96, Padding = new Padding(4) };
    // Look
    readonly ListView palette = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
    readonly ImageList swatches = new() { ImageSize = new Size(34, 22), ColorDepth = ColorDepth.Depth32Bit };
    readonly PropertyGrid themeGrid = Grid();
    // Wallpaper
    readonly PropertyGrid wallGrid = Grid();
    readonly ListBox layerList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid layerGrid = Grid();
    // Bar
    readonly PropertyGrid barGrid = Grid();
    readonly ComboBox barSection = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    readonly ListBox moduleList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid moduleGrid = Grid();
    // Widgets
    readonly ListBox widgetList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid widgetGrid = Grid();
    // Shortcuts
    readonly ListView scList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
    readonly ImageList scIcons = new() { ImageSize = new Size(24, 24), ColorDepth = ColorDepth.Depth32Bit };
    readonly PropertyGrid scGrid = Grid();
    readonly CheckBox hideIcons = new() { Text = "Hide Windows' own desktop icons while Glyph runs", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(2, 6, 2, 6) };
    // General
    readonly PropertyGrid appGrid = Grid();

    static PropertyGrid Grid() => new() { Dock = DockStyle.Fill, ToolbarVisible = false, PropertySort = PropertySort.Categorized, HelpVisible = true };

    public SettingsForm(string? startPage = null)
    {
        Text = "Glyph settings";
        Width = 1440; Height = 880; MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Dark.UiFont;
        try { Icon = Icon.ExtractAssociatedIcon(Paths.Exe); } catch { }

        app = AppSettings.Load(new List<string>());
        try { Dark.UseAccentFrom(Theme.Load(app.Theme, new List<string>())); } catch { }

        statusBar.Items.Add(status); statusBar.Items.Add(running);
        content.Controls.Add(editing);
        content.Controls.Add(header);
        Controls.Add(content);
        Controls.Add(sidebar);
        Controls.Add(statusBar);

        // pages, added bottom-up because they dock to the top of the sidebar
        AddPage("General", "General", BuildGeneral());
        AddPage("Hotkeys", "Hotkeys", BuildHotkeys());
        AddPage("Shortcuts", "Shortcuts", BuildShortcuts());
        AddPage("Variables", "Variables", BuildVariables());
        AddPage("Widgets", "Widgets", BuildWidgets());
        AddPage("Bar", "Bar", BuildBar());
        AddPage("Wallpaper", "Wallpaper", BuildWallpaper());
        AddPage("Frames", "Frames", BuildFrames());
        AddPage("Look", "Colours & look", BuildLook());
        AddPage("Design", "Design", BuildDesign());
        AddPage("Themes", "Themes", BuildThemes());
        // the app's G and its name at the top of the sidebar
        var logoBox = new Panel { Dock = DockStyle.Top, Height = 64 };
        var logo = new Label { Text = "glyph", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Cascadia Mono", 16f, FontStyle.Bold) };
        var mark = new PictureBox { Dock = DockStyle.Left, Width = 58, SizeMode = PictureBoxSizeMode.CenterImage };
        try { mark.Image = Updater.AppIcon(32).ToBitmap(); } catch { }
        logoBox.Controls.Add(logo); logoBox.Controls.Add(mark);
        sidebar.Controls.Add(logoBox);

        Dark.Apply(this);
        sidebar.BackColor = Dark.Panel;
        logoBox.BackColor = mark.BackColor = logo.BackColor = Dark.Panel; logo.ForeColor = Dark.Accent;
        foreach (var b in navButtons.Values) StyleNav(b, false);
        editing.ForeColor = Dark.Dim;
        HandleCreated += (_, _) => Dark.TitleBar(this);

        saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveNow(); };
        runTimer.Tick += (_, _) => UpdateRunning();
        runTimer.Start();
        FormClosing += (_, _) => { SaveNow(); StopPreview(); soft?.Dispose(); soft = null; };

        LoadAll();
        UpdateRunning();
        var start = startPage != null ? pages.Keys.FirstOrDefault(k => k.Equals(startPage, StringComparison.OrdinalIgnoreCase)) : null;
        ShowPage(start ?? "Themes");
        status.Text = "Changes are saved and shown on the desktop straight away.";
    }

    // ───────────────────────── page framework ─────────────────────────

    void AddPage(string key, string title, Control body)
    {
        var page = new Panel { Dock = DockStyle.Fill, Visible = false, Tag = title };
        page.Controls.Add(body);
        content.Controls.Add(page);
        page.BringToFront();
        pages[key] = page;
        var b = new Button { Text = "     " + title.Replace("&", "&&"), Dock = DockStyle.Top, Height = 44, TextAlign = ContentAlignment.MiddleLeft, Tag = key };
        b.Click += (_, _) => ShowPage(key);
        b.Paint += NavPaint;
        sidebar.Controls.Add(b);
        navButtons[key] = b;
    }

    void StyleNav(Button b, bool active)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.BackColor = active ? Dark.Raised : Dark.Panel;
        b.ForeColor = active ? Dark.Text : Dark.Dim;
        b.FlatAppearance.MouseOverBackColor = Dark.Raised;
        b.FlatAppearance.MouseDownBackColor = Dark.Raised;
        b.Font = new Font("Segoe UI", 10.5f, active ? FontStyle.Bold : FontStyle.Regular);
    }

    void NavPaint(object? s, PaintEventArgs e)
    {
        if (s is Button b && (string)b.Tag! == currentPage)
            using (var a = new SolidBrush(Dark.Accent)) e.Graphics.FillRectangle(a, 0, 9, 4, b.Height - 18);
    }

    void ShowPage(string key)
    {
        currentPage = key;
        foreach (var (k, p) in pages) p.Visible = k == key;
        foreach (var (k, b) in navButtons) { StyleNav(b, k == key); b.Invalidate(); }
        header.Text = (string)pages[key].Tag!;
        editing.Visible = key is "Look" or "Wallpaper" or "Bar" or "Widgets" or "Design" or "Variables" or "Frames";
        if (key == "Frames" && IsHandleCreated) BeginInvoke(PreviewFrame);
        if (IsHandleCreated) BeginInvoke(PlaceSplits);
        if (key == "Design") { StartPreview(); Select(sel); RefreshSwatches(); } else StopPreview();
        varTimer.Enabled = key == "Variables";
    }

    readonly List<(SplitContainer c, int d)> splits = new();
    SplitContainer Split(int distance, bool horizontal = false)
    {
        var c = new SplitContainer { Dock = DockStyle.Fill, Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, SplitterWidth = 10 };
        splits.Add((c, distance));
        return c;
    }

    readonly HashSet<SplitContainer> placed = new();

    /// Sets each split's position the first time its page is on screen (hidden pages have no size yet).
    void PlaceSplits()
    {
        foreach (var (c, d) in splits)
        {
            int size = c.Orientation == Orientation.Vertical ? c.Width : c.Height;
            if (placed.Contains(c) || !c.Visible || size <= 0) continue;
            try { c.SplitterDistance = d < 0 ? size + d : d; placed.Add(c); } catch { }
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        PlaceSplits();
    }

    static FlowLayoutPanel Buttons(int height = 50) => new() { Dock = DockStyle.Bottom, Height = height, Padding = new Padding(0, 8, 0, 0), WrapContents = true };

    static Button Btn(FlowLayoutPanel bar, string text, Action a, bool primary = false)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(10, 0, 10, 0), Margin = new Padding(0, 0, 8, 6) };
        b.Click += (_, _) => a();
        bar.Controls.Add(b);
        if (primary) b.HandleCreated += (_, _) => Dark.Primary(b);
        return b;
    }

    /// A line of help text that grows to fit its words when the window is resized.
    static Label Hint(string text, int height = 30)
    {
        var l = new Label { Text = text, Dock = DockStyle.Top, Height = height, ForeColor = Dark.Dim, UseMnemonic = false };
        void Fit()
        {
            if (l.Width <= 20) return;
            int h = TextRenderer.MeasureText(l.Text, l.Font, new Size(l.Width - l.Padding.Horizontal, int.MaxValue), TextFormatFlags.WordBreak).Height + 8;
            h = Math.Max(h, 24);
            if (h != l.Height) l.Height = h;
        }
        l.SizeChanged += (_, _) => Fit();
        l.TextChanged += (_, _) => Fit();
        l.FontChanged += (_, _) => Fit();
        return l;
    }

    static void DropDown(Control anchor, IEnumerable<(string label, Action act)> items)
    {
        var m = Dark.Menu();
        foreach (var (label, act) in items) m.Items.Add(label, null, (_, _) => act());
        m.Show(anchor, new Point(0, anchor.Height));
    }

    // ───────────────────────── loading & saving ─────────────────────────

    void LoadAll()
    {
        var problems = new List<string>();
        app = AppSettings.Load(problems);
        shortcuts = ShortcutStore.Load(problems);
        hotkeys = HotkeyStore.Load(problems);
        RefreshHotkeys();
        RefreshThemeList();
        SelectTheme(Directory.Exists(Paths.ThemeDir(app.Theme)) ? app.Theme : ThemeNames().FirstOrDefault() ?? "");
        appGrid.SelectedObject = app; appGrid.ExpandAllGridItems();
        loading = true; hideIcons.Checked = app.HideDesktopIcons; loading = false;
        RefreshShortcuts();
        if (problems.Count > 0) status.Text = "Note: " + problems[0];
    }

    static List<string> ThemeNames() =>
        Directory.Exists(Paths.Themes)
            ? Directory.GetDirectories(Paths.Themes).Where(d => File.Exists(Path.Combine(d, "theme.toml"))).Select(Path.GetFileName).OfType<string>().OrderBy(n => n).ToList()
            : new List<string>();

    void SelectTheme(string name)
    {
        SaveNow();
        themeName = name;
        var problems = new List<string>();
        if (name.Length == 0) return;
        theme = Theme.Load(name, problems);
        wallpaper = WallpaperOptions.Load(theme, problems);
        widgets = WidgetsFile.Load(theme, problems);
        bar = File.Exists(BarFile.FilePath(name)) ? BarFile.Load(theme, problems) : new BarFile { Bar = { Enabled = false } };
        ColorNameConverter.Names = theme.PaletteList.Select(p => p.name).Append("none").Append("auto").ToArray();
        editing.Text = $"Editing theme \"{name}\"" + (name == app.Theme ? "   ·   in use on the desktop" : "   ·   not in use (Themes > Use this theme)");
        themeInfo.Text = $"{name}\n{theme.Options.Info.Description}\nStyle: {theme.Options.Info.Style}";
        RefreshPalette();
        dirtyFrames = false;
        RefreshFrames();
        themeGrid.SelectedObject = theme.Options; themeGrid.ExpandAllGridItems();
        wallGrid.SelectedObject = wallpaper; wallGrid.ExpandAllGridItems();
        barGrid.SelectedObject = bar.Bar;
        RefreshLayers();
        RefreshWidgets();
        RefreshModules();
        variables = Glyph.Data.Variables.LoadList(name, problems);
        RefreshVariables();
        DesignThemeChanged();
        int i = themeList.Items.IndexOf(Display(name));
        if (i >= 0 && themeList.SelectedIndex != i) { loading = true; themeList.SelectedIndex = i; loading = false; }
        if (problems.Count > 0) status.Text = "Note: " + problems[0];
    }

    void Changed(ref bool flag) { flag = true; saveTimer.Stop(); saveTimer.Start(); }
    void MarkApp() => Changed(ref dirtyApp);
    void MarkTheme() => Changed(ref dirtyTheme);
    void MarkWallpaper() => Changed(ref dirtyWallpaper);
    void MarkWidgets() => Changed(ref dirtyWidgets);
    void MarkShortcuts() => Changed(ref dirtyShortcuts);
    void MarkBar() => Changed(ref dirtyBar);
    void MarkVariables() => Changed(ref dirtyVariables);
    bool dirtyVariables;

    // ───────────────────────── Variables ─────────────────────────

    List<Glyph.Data.VariableOptions> variables = new();
    readonly ListBox varList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    readonly PropertyGrid varGrid = Grid();
    readonly TextBox tester = new() { Dock = DockStyle.Top, Font = new Font("Cascadia Mono", 11f) };
    readonly Label testerOut = new() { Dock = DockStyle.Top, Height = 64, Font = new Font("Cascadia Mono", 13f), Padding = new Padding(4, 8, 4, 4) };
    readonly Label varValue = new() { Dock = DockStyle.Top, Height = 34, Font = new Font("Cascadia Mono", 11f) };
    readonly System.Windows.Forms.Timer varTimer = new() { Interval = 1000 };

    Control BuildVariables()
    {
        var split = Split(420);
        var buttons = Buttons(50);
        Btn(buttons, "Add variable", () => { var v = new Glyph.Data.VariableOptions { Name = "my_value" + (variables.Count + 1) }; variables.Add(v); RefreshVariables(v); MarkVariables(); }, true);
        Btn(buttons, "Remove", () => { if (varList.SelectedItem is Glyph.Data.VariableOptions v) { variables.Remove(v); RefreshVariables(); MarkVariables(); } });
        Btn(buttons, "Duplicate", () => { if (varList.SelectedItem is Glyph.Data.VariableOptions v) { var c = Binder.Clone(v); c.Name += "_copy"; variables.Add(c); RefreshVariables(c); MarkVariables(); } });
        split.Panel1.Controls.Add(varList);
        split.Panel1.Controls.Add(Hint("Your own values for this theme. Use one anywhere in text as {name}.", 30));
        split.Panel1.Controls.Add(buttons);

        var right = Split(330, true);
        right.Panel1.Controls.Add(varGrid);
        right.Panel1.Controls.Add(varValue);
        var testPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        testPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Dark.Dim,
            Text = "Values: {time} {date} {cpu} {ram} {ram_used} {disk} {disk_free} {net_down} {net_up} {battery} {volume} {window} {media_title} {media_artist} " +
                   "{desktop} {user} {host} {os} {uptime} {temp} {weather} … and your own variables.\n\n" +
                   "Filters, after a |, change how a value looks:  " + Glyph.Data.Fmt.FilterHelp + "\n\n" +
                   "Examples:  {cpu|pad:3}%    {window|upper|max:30}    {ram|bar:12}    {cpu|color>80:accent_bright}    {media_title|default:nothing playing}    [accent]{time:HH:mm}[/]",
        });
        testPanel.Controls.Add(testerOut);
        testPanel.Controls.Add(tester);
        testPanel.Controls.Add(new Label { Text = "Try a format (shows the live result):", Dock = DockStyle.Top, Height = 28 });
        right.Panel2.Controls.Add(testPanel);
        split.Panel2.Controls.Add(right);

        tester.Text = "[accent]cpu[/] {cpu|pad:3}% {cpu|bar:10}  [dim]{window|max:30}[/]";
        varList.SelectedIndexChanged += (_, _) => { if (!loading) varGrid.SelectedObject = varList.SelectedItem; UpdateVarValues(); };
        varGrid.PropertyValueChanged += (_, _) => { MarkVariables(); Glyph.Data.Variables.Use(variables); RefreshVariables(varList.SelectedItem as Glyph.Data.VariableOptions); };
        tester.TextChanged += (_, _) => UpdateVarValues();
        varTimer.Tick += (_, _) => UpdateVarValues();
        return split;
    }

    void RefreshVariables(Glyph.Data.VariableOptions? select = null)
    {
        Glyph.Data.Variables.Use(variables);
        RefreshList(varList, variables, select, varGrid);
        UpdateVarValues();
    }

    void UpdateVarValues()
    {
        varValue.Text = varList.SelectedItem is Glyph.Data.VariableOptions v ? "value now:  " + Glyph.Data.Fmt.Text("{" + v.Name + "}", Glyph.Data.Stats.Value) : "";
        var spans = Glyph.Data.Fmt.Spans(tester.Text, Glyph.Data.Stats.Value, theme, theme?["text"] ?? new Rgba(230, 230, 230));
        testerOut.Text = string.Concat(spans.Select(s => s.Text));
        var first = spans.FirstOrDefault(sp => sp.Text.Trim().Length > 0);
        testerOut.ForeColor = Dark.Text;
    }

    void SaveNow()
    {
        try
        {
            if (dirtyApp) { app.Save(); dirtyApp = false; }
            if (themeName.Length > 0)
            {
                if (dirtyTheme) { Clamp(theme.Options); theme.Save(); dirtyTheme = false; }
                if (dirtyWallpaper) { Clamp(wallpaper); foreach (var l in wallpaper.Layers) Clamp(l); wallpaper.Save(themeName); dirtyWallpaper = false; }
                if (dirtyWidgets) { foreach (var w in widgets.Widgets) Clamp(w); widgets.Save(themeName); dirtyWidgets = false; }
                if (dirtyBar) { Clamp(bar.Bar); foreach (var m in bar.Left.Concat(bar.Center).Concat(bar.Right)) Clamp(m); bar.Save(themeName); dirtyBar = false; }
                if (dirtyVariables) { Glyph.Data.Variables.Save(themeName, variables); Glyph.Data.Variables.Use(variables); dirtyVariables = false; }
                SaveFrames();
                preview?.Reload();
            }
            if (dirtyShortcuts) { ShortcutStore.Save(shortcuts); dirtyShortcuts = false; }
            if (dirtyHotkeys) { HotkeyStore.Save(hotkeys); dirtyHotkeys = false; }
        }
        catch (Exception e) { status.Text = "Couldn't save: " + e.Message; }
    }

    /// Keeps numbers inside their allowed ranges (the desktop would clamp them anyway).
    static void Clamp(object o)
    {
        foreach (var p in Binder.Props(o.GetType()))
        {
            if (p.GetCustomAttribute<SectionAttribute>() != null) { Clamp(p.GetValue(o)!); continue; }
            var r = p.GetCustomAttribute<Options.RangeAttribute>();
            if (r == null) continue;
            if (p.PropertyType == typeof(float)) p.SetValue(o, (float)Math.Clamp((float)p.GetValue(o)!, r.Min, r.Max));
            else if (p.PropertyType == typeof(int)) p.SetValue(o, (int)Math.Clamp((int)p.GetValue(o)!, r.Min, r.Max));
            else if (p.PropertyType == typeof(double)) p.SetValue(o, Math.Clamp((double)p.GetValue(o)!, r.Min, r.Max));
        }
    }

    static List<Process> MainGlyph() => Process.GetProcessesByName("Glyph").Where(p => p.Id != Environment.ProcessId && !IsHelper(p)).ToList();

    void UpdateRunning() => running.Text = MainGlyph().Count > 0 ? "●  Glyph is running" : "○  Glyph isn't running (General > Start Glyph)";

    static bool IsHelper(Process p)
    {
        try { return p.MainWindowTitle is "Glyph settings" or "Glyph converter"; } catch { return false; }
    }

    // ───────────────────────── Themes ─────────────────────────

    string Display(string n) => n == app.Theme ? $"{n}   (in use)" : n;

    void RefreshThemeList()
    {
        loading = true;
        themeList.Items.Clear();
        foreach (var n in ThemeNames()) themeList.Items.Add(Display(n));
        loading = false;
    }

    string? SelectedThemeName() => themeList.SelectedItem is string s ? s.Split("   ")[0] : null;

    Control BuildThemes()
    {
        var split = Split(330);
        split.Panel1.Controls.Add(themeList);
        split.Panel1.Controls.Add(Hint("Your themes. Pick one to edit it on the other pages."));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(4) };
        void B(string text, Action a, bool primary = false)
        {
            var b = new Button { Text = text, Width = 280, Height = 38, Margin = new Padding(0, 0, 0, 8) };
            b.Click += (_, _) => a(); buttons.Controls.Add(b);
            if (primary) b.HandleCreated += (_, _) => Dark.Primary(b);
        }
        B("Use this theme", UseTheme, true);
        B("New theme…", () => NewTheme(false));
        B("Duplicate…", () => NewTheme(true));
        B("Rename…", RenameTheme);
        B("Delete", DeleteTheme);
        B("Open theme folder", () => Open(Paths.ThemeDir(themeName)));
        B("Restore the built-in theme", AddExamples);
        buttons.Controls.Add(new Label
        {
            AutoSize = false, Width = 600, Height = 170, ForeColor = Dark.Dim,
            Text = "A theme is everything you see: colours, fonts, the wallpaper, the bar, widgets and effects.\n\n" +
                   "Style \"terminal\" draws everything with characters; \"smooth\" uses rounded panels, normal fonts and pictures " +
                   "(set it on Colours & look, under About).\n\nNew themes start as a copy of the one you pick, so you can experiment safely.",
        });
        split.Panel2.Controls.Add(buttons);
        split.Panel2.Controls.Add(themeInfo);
        themeList.SelectedIndexChanged += (_, _) => { if (!loading && SelectedThemeName() is string n && n != themeName) SelectTheme(n); };
        themeList.DoubleClick += (_, _) => UseTheme();
        return split;
    }

    void UseTheme()
    {
        if (themeName.Length == 0) return;
        SaveNow();
        app.Theme = themeName;
        app.Save();
        RefreshThemeList();
        SelectTheme(themeName);
        status.Text = $"Now showing \"{themeName}\".";
    }

    string? Ask(string title, string prompt, string initial, string[]? choices, out string? choice)
    {
        choice = null;
        using var f = new Form { Text = title, Width = 490, Height = choices != null ? 260 : 200, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = Dark.UiFont };
        f.Controls.Add(new Label { Text = prompt, Left = 16, Top = 14, Width = 440, Height = 24 });
        var box = new TextBox { Text = initial, Left = 16, Top = 42, Width = 440 };
        f.Controls.Add(box);
        ComboBox? cb = null;
        if (choices != null)
        {
            f.Controls.Add(new Label { Text = "Start from:", Left = 16, Top = 84, Width = 440, Height = 24 });
            cb = new ComboBox { Left = 16, Top = 110, Width = 440, DropDownStyle = ComboBoxStyle.DropDownList };
            cb.Items.AddRange(choices); cb.SelectedIndex = 0;
            f.Controls.Add(cb);
        }
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 274, Top = f.ClientSize.Height - 50, Width = 86, Height = 34 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 370, Top = f.ClientSize.Height - 50, Width = 86, Height = 34 };
        f.Controls.Add(ok); f.Controls.Add(cancel);
        f.AcceptButton = ok; f.CancelButton = cancel;
        Dark.Apply(f); Dark.Primary(ok);
        f.HandleCreated += (_, _) => Dark.TitleBar(f);
        if (f.ShowDialog(this) != DialogResult.OK) return null;
        choice = cb?.SelectedItem as string;
        return box.Text.Trim();
    }

    static string Clean(string s) => new(s.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

    void NewTheme(bool duplicate)
    {
        var names = ThemeNames();
        string? from, name;
        if (duplicate) { name = Ask("Duplicate theme", "Name for the copy:", themeName + "-copy", null, out _); from = themeName; }
        else name = Ask("New theme", "Name for the new theme:", "my-theme", names.ToArray(), out from);
        if (string.IsNullOrWhiteSpace(name) || from == null) return;
        name = Clean(name);
        if (Directory.Exists(Paths.ThemeDir(name))) { MessageBox.Show(this, "A theme with that name already exists.", "Glyph"); return; }
        SaveNow();
        CopyDir(Paths.ThemeDir(from), Paths.ThemeDir(name));
        RefreshThemeList();
        SelectTheme(name);
        status.Text = $"Made \"{name}\" from \"{from}\". Change it on the other pages.";
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from)) CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
    }

    void RenameTheme()
    {
        if (themeName.Length == 0) return;
        var name = Ask("Rename theme", "New name:", themeName, null, out _);
        if (string.IsNullOrWhiteSpace(name)) return;
        name = Clean(name);
        if (name == themeName) return;
        if (Directory.Exists(Paths.ThemeDir(name))) { MessageBox.Show(this, "A theme with that name already exists.", "Glyph"); return; }
        SaveNow();
        bool active = app.Theme == themeName;
        Directory.Move(Paths.ThemeDir(themeName), Paths.ThemeDir(name));
        if (active) { app.Theme = name; app.Save(); }
        RefreshThemeList();
        SelectTheme(name);
    }

    void DeleteTheme()
    {
        if (themeName.Length == 0) return;
        if (themeName == app.Theme) { MessageBox.Show(this, "This theme is in use. Use another theme first, then delete this one.", "Glyph"); return; }
        if (MessageBox.Show(this, $"Delete the theme \"{themeName}\" and everything in its folder? This can't be undone.", "Glyph", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        dirtyTheme = dirtyWallpaper = dirtyWidgets = dirtyBar = false;
        Directory.Delete(Paths.ThemeDir(themeName), true);
        RefreshThemeList();
        SelectTheme(app.Theme);
    }

    void AddExamples()
    {
        var existing = DefaultThemes.Names.Where(n => Directory.Exists(Paths.ThemeDir(n))).ToList();
        if (DefaultThemes.Names.Length == 0) { MessageBox.Show(this, "There's no built-in theme next to Glyph.exe (bin\\themes).", "Glyph"); return; }
        if (existing.Count > 0 && MessageBox.Show(this, $"This resets {string.Join(" and ", existing)} to how it came with Glyph. Continue?", "Glyph", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        SaveNow();
        dirtyTheme = dirtyWallpaper = dirtyWidgets = dirtyBar = false;
        foreach (var n in DefaultThemes.Names) DefaultThemes.Write(n);
        RefreshThemeList();
        SelectTheme(themeName.Length > 0 ? themeName : DefaultThemes.Names[0]);
    }

    // ───────────────────────── Colours & look ─────────────────────────

    Control BuildLook()
    {
        var split = Split(410);
        palette.Columns.Add("Colour", 190);
        palette.Columns.Add("Hex", 120);
        palette.SmallImageList = swatches;
        var bar = Buttons(92);
        Btn(bar, "Change colour…", PickColor, true);
        Btn(bar, "Type hex…", TypeHex);
        Btn(bar, "Add colour…", AddColor);
        Btn(bar, "Remove", RemoveColor);
        split.Panel1.Controls.Add(palette);
        split.Panel1.Controls.Add(Hint("The palette: every colour in the theme. Everything else uses these names. Double-click to change one.", 46));
        split.Panel1.Controls.Add(bar);
        split.Panel2.Controls.Add(themeGrid);
        palette.DoubleClick += (_, _) => PickColor();
        themeGrid.PropertyValueChanged += (_, _) => { MarkTheme(); themeInfo.Text = $"{themeName}\n{theme.Options.Info.Description}\nStyle: {theme.Options.Info.Style}"; };
        return split;
    }

    void RefreshPalette()
    {
        palette.BeginUpdate();
        palette.Items.Clear();
        swatches.Images.Clear();
        foreach (var (n, c) in theme.PaletteList)
        {
            var sw = new Bitmap(34, 22);
            using (var g = Graphics.FromImage(sw))
            {
                g.Clear(Dark.Panel);
                using var b = new SolidBrush(Color.FromArgb(c.R, c.G, c.B));
                g.FillRectangle(b, 2, 3, 30, 16);
                using var pen = new Pen(Dark.Line);
                g.DrawRectangle(pen, 2, 3, 29, 15);
            }
            swatches.Images.Add(sw);
            palette.Items.Add(new ListViewItem(new[] { n, c.Hex }, swatches.Images.Count - 1));
        }
        palette.EndUpdate();
    }

    string? SelectedColor() => palette.SelectedItems.Count > 0 ? palette.SelectedItems[0].Text : null;

    void PickColor()
    {
        if (SelectedColor() is not string n) return;
        var c = theme.Palette[n];
        using var d = new ColorDialog { Color = Color.FromArgb(c.R, c.G, c.B), FullOpen = true, AnyColor = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        theme.SetColor(n, new Rgba(d.Color.R, d.Color.G, d.Color.B, c.A));
        AfterPalette(n);
    }

    void TypeHex()
    {
        if (SelectedColor() is not string n) return;
        var hex = Ask("Colour", $"Hex code for \"{n}\" (#RRGGBB, or #RRGGBBAA for see-through):", theme.Palette[n].Hex, null, out _);
        if (hex == null) return;
        if (!hex.StartsWith('#')) hex = "#" + hex;
        if (!Rgba.TryParseHex(hex, out var c)) { MessageBox.Show(this, "That isn't a hex code like #E0202F.", "Glyph"); return; }
        theme.SetColor(n, c);
        AfterPalette(n);
    }

    void AddColor()
    {
        var name = Ask("Add colour", "Name for the new colour (letters, numbers and _):", "my_colour", null, out _);
        if (string.IsNullOrWhiteSpace(name)) return;
        name = new string(name.Trim().Replace(' ', '_').Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray()).ToLowerInvariant();
        if (name.Length == 0 || theme.Palette.ContainsKey(name)) return;
        theme.SetColor(name, theme["accent"]);
        AfterPalette(name);
        PickColor();
    }

    void RemoveColor()
    {
        if (SelectedColor() is not string n) return;
        string[] core = { "bg", "surface", "line", "dim", "text", "bright", "accent", "accent_dim", "accent_bright" };
        if (core.Contains(n)) { MessageBox.Show(this, $"\"{n}\" is one of the basic colours every theme needs. You can change it, but not remove it.", "Glyph"); return; }
        theme.RemoveColor(n);
        AfterPalette(null);
    }

    void AfterPalette(string? select)
    {
        ColorNameConverter.Names = theme.PaletteList.Select(p => p.name).Append("none").Append("auto").ToArray();
        RefreshPalette();
        if (select != null) foreach (ListViewItem it in palette.Items) if (it.Text == select) { it.Selected = true; it.EnsureVisible(); }
        MarkTheme();
    }

    // ───────────────────────── Wallpaper ─────────────────────────

    static readonly Dictionary<string, string> LayerHelp = new()
    {
        ["noise"] = "smoke / flowing clouds", ["rain"] = "falling streams of characters", ["stars"] = "a starfield",
        ["static"] = "TV static", ["glitch"] = "jolts the layers below it", ["art"] = "ASCII art from a text file", ["clip"] = "an animation from the converter",
    };

    Control BuildWallpaper()
    {
        var split = Split(450);
        split.Panel1.Controls.Add(wallGrid);
        split.Panel1.Controls.Add(Hint("Backdrop (colour, gradient or picture) and the mouse effect."));
        var right = Split(230, true);
        var bar = Buttons();
        Button? add = null;
        add = Btn(bar, "Add layer ▾", () => DropDown(add!, WallpaperOptions.Types.Keys.Select(t => ($"{t}  —  {LayerHelp[t]}", (Action)(() => AddLayer(t))))), true);
        Btn(bar, "Remove", () => { if (layerList.SelectedItem is LayerOptions l) { wallpaper.Layers.Remove(l); RefreshLayers(); MarkWallpaper(); } });
        Btn(bar, "Duplicate", () => { if (layerList.SelectedItem is LayerOptions l) { var c = Binder.Clone(l); wallpaper.Layers.Insert(wallpaper.Layers.IndexOf(l) + 1, c); RefreshLayers(c); MarkWallpaper(); } });
        Btn(bar, "Move up", () => MoveIn(wallpaper.Layers, layerList, -1, () => { RefreshLayers(layerList.SelectedItem as LayerOptions); MarkWallpaper(); }));
        Btn(bar, "Move down", () => MoveIn(wallpaper.Layers, layerList, 1, () => { RefreshLayers(layerList.SelectedItem as LayerOptions); MarkWallpaper(); }));
        right.Panel1.Controls.Add(layerList);
        right.Panel1.Controls.Add(Hint("Layers, drawn in order (later ones on top). Pick one to change it below."));
        right.Panel1.Controls.Add(bar);
        right.Panel2.Controls.Add(layerGrid);
        split.Panel2.Controls.Add(right);
        wallGrid.PropertyValueChanged += (_, _) => MarkWallpaper();
        layerGrid.PropertyValueChanged += (_, _) => { MarkWallpaper(); RefreshLayers(layerList.SelectedItem as LayerOptions); };
        layerList.SelectedIndexChanged += (_, _) => { if (!loading) layerGrid.SelectedObject = layerList.SelectedItem; };
        return split;
    }

    void RefreshLayers(LayerOptions? select = null) => RefreshList(layerList, wallpaper.Layers, select, layerGrid);

    void RefreshList<T>(ListBox list, List<T> items, T? select, PropertyGrid grid) where T : class
    {
        loading = true;
        list.Items.Clear();
        foreach (var i in items) list.Items.Add(i);
        if (select != null && list.Items.Contains(select)) list.SelectedItem = select;
        else if (list.Items.Count > 0) list.SelectedIndex = 0;
        loading = false;
        grid.SelectedObject = list.SelectedItem;
    }

    static void MoveIn<T>(List<T> items, ListBox list, int d, Action after) where T : class
    {
        if (list.SelectedItem is not T x) return;
        int i = items.IndexOf(x), j = i + d;
        if (i < 0 || j < 0 || j >= items.Count) return;
        (items[i], items[j]) = (items[j], items[i]);
        after();
    }

    void AddLayer(string type)
    {
        var l = WallpaperOptions.Types[type]();
        if (l is ArtOptions a) a.File = "glyph.txt";
        wallpaper.Layers.Add(l);
        RefreshLayers(l);
        MarkWallpaper();
    }

    // ───────────────────────── Bar ─────────────────────────

    List<BarModuleOptions> Section => barSection.SelectedIndex switch { 1 => bar.Center, 2 => bar.Right, _ => bar.Left };

    Control BuildBar()
    {
        var split = Split(430);
        split.Panel1.Controls.Add(barGrid);
        split.Panel1.Controls.Add(Hint("The bar itself: where it is, its size and look. Turn it on or off with Enabled.", 46));
        var right = Split(270, true);
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false };
        top.Controls.Add(new Label { Text = "Modules on the", AutoSize = true, Padding = new Padding(0, 8, 6, 0) });
        barSection.Items.AddRange(new object[] { "left", "centre", "right" });
        barSection.SelectedIndex = 0;
        top.Controls.Add(barSection);
        top.Controls.Add(new Label { Text = "of the bar", AutoSize = true, Padding = new Padding(6, 8, 0, 0) });
        var buttons = Buttons();
        Button? add = null;
        add = Btn(buttons, "Add module ▾", () => DropDown(add!, BarFile.Presets.Select(p => (p.name, (Action)(() => { var m = p.make(); Section.Add(m); RefreshModules(m); MarkBar(); })))), true);
        Btn(buttons, "Remove", () => { if (moduleList.SelectedItem is BarModuleOptions m) { Section.Remove(m); RefreshModules(); MarkBar(); } });
        Btn(buttons, "Duplicate", () => { if (moduleList.SelectedItem is BarModuleOptions m) { var c = Binder.Clone(m); Section.Insert(Section.IndexOf(m) + 1, c); RefreshModules(c); MarkBar(); } });
        Btn(buttons, "Move up", () => MoveIn(Section, moduleList, -1, () => { RefreshModules(moduleList.SelectedItem as BarModuleOptions); MarkBar(); }));
        Btn(buttons, "Move down", () => MoveIn(Section, moduleList, 1, () => { RefreshModules(moduleList.SelectedItem as BarModuleOptions); MarkBar(); }));
        Button? mv = null;
        mv = Btn(buttons, "Move to ▾", () => DropDown(mv!, new[] { ("left", 0), ("centre", 1), ("right", 2) }.Select(s => (s.Item1, (Action)(() =>
        {
            if (moduleList.SelectedItem is not BarModuleOptions m) return;
            Section.Remove(m);
            barSection.SelectedIndex = s.Item2;
            Section.Add(m); RefreshModules(m); MarkBar();
        })))));
        right.Panel1.Controls.Add(moduleList);
        right.Panel1.Controls.Add(top);
        right.Panel1.Controls.Add(buttons);
        right.Panel2.Controls.Add(moduleGrid);
        split.Panel2.Controls.Add(right);
        barGrid.PropertyValueChanged += (_, _) => MarkBar();
        moduleGrid.PropertyValueChanged += (_, _) => { MarkBar(); RefreshModules(moduleList.SelectedItem as BarModuleOptions); };
        moduleList.SelectedIndexChanged += (_, _) => { if (!loading) moduleGrid.SelectedObject = moduleList.SelectedItem; };
        barSection.SelectedIndexChanged += (_, _) => RefreshModules();
        return split;
    }

    void RefreshModules(BarModuleOptions? select = null)
    {
        if (bar == null) return;
        RefreshList(moduleList, Section, select, moduleGrid);
    }

    // ───────────────────────── Widgets ─────────────────────────

    Control BuildWidgets()
    {
        var split = Split(390);
        var buttons = Buttons(92);
        Button? add = null;
        add = Btn(buttons, "Add widget ▾", () => DropDown(add!, WidgetsFile.Types.Keys.Select(t => ($"{t}  —  {WidgetsFile.Help[t]}", (Action)(() =>
        {
            var w = WidgetsFile.Types[t]();
            widgets.Widgets.Add(w); RefreshWidgets(w); MarkWidgets();
        })))), true);
        Btn(buttons, "Remove", () => { if (widgetList.SelectedItem is WidgetOptions w) { widgets.Widgets.Remove(w); RefreshWidgets(); MarkWidgets(); } });
        Btn(buttons, "Duplicate", () => { if (widgetList.SelectedItem is WidgetOptions w) { var c = Binder.Clone(w); c.OffsetX += 40; c.OffsetY += 40; widgets.Widgets.Add(c); RefreshWidgets(c); MarkWidgets(); } });
        Btn(buttons, "Move up", () => MoveIn(widgets.Widgets, widgetList, -1, () => { RefreshWidgets(widgetList.SelectedItem as WidgetOptions); MarkWidgets(); }));
        Btn(buttons, "Move down", () => MoveIn(widgets.Widgets, widgetList, 1, () => { RefreshWidgets(widgetList.SelectedItem as WidgetOptions); MarkWidgets(); }));
        split.Panel1.Controls.Add(widgetList);
        split.Panel1.Controls.Add(Hint("Widgets on the desktop (later ones are drawn on top). Text options can use {values} like {cpu} or {time:HH:mm}, and [accent]colours[/].", 48));
        split.Panel1.Controls.Add(buttons);
        split.Panel2.Controls.Add(widgetGrid);
        widgetList.SelectedIndexChanged += (_, _) => { if (!loading) widgetGrid.SelectedObject = widgetList.SelectedItem; };
        widgetGrid.PropertyValueChanged += (_, _) => { MarkWidgets(); RefreshWidgets(widgetList.SelectedItem as WidgetOptions); };
        return split;
    }

    void RefreshWidgets(WidgetOptions? select = null) => RefreshList(widgetList, widgets.Widgets, select, widgetGrid);

    // ───────────────────────── Shortcuts ─────────────────────────

    Control BuildShortcuts()
    {
        var split = Split(680);
        scList.Columns.Add("Name", 240);
        scList.Columns.Add("Group", 90);
        scList.Columns.Add("Opens", 320);
        scList.SmallImageList = scIcons;
        var buttons = Buttons(92);
        Btn(buttons, "Add program or file…", AddFile, true);
        Btn(buttons, "Add folder…", AddFolder);
        Btn(buttons, "Add website…", AddWebsite);
        Btn(buttons, "Import desktop icons", ImportDesktop);
        Btn(buttons, "Remove", RemoveShortcut);
        Btn(buttons, "Move up", () => MoveShortcut(-1));
        Btn(buttons, "Move down", () => MoveShortcut(1));
        Btn(buttons, "Set picture…", SetPicture);
        Btn(buttons, "Use app's own icon", () => { if (SelectedShortcut() is Shortcut s) { s.Image = ""; AfterShortcuts(s); } });
        split.Panel1.Controls.Add(scList);
        split.Panel1.Controls.Add(Hint("Your shortcuts (shared by every theme). Shortcuts widgets show them on the desktop."));
        split.Panel1.Controls.Add(hideIcons);
        split.Panel1.Controls.Add(buttons);
        split.Panel2.Controls.Add(scGrid);
        split.Panel2.Controls.Add(Hint("The selected shortcut. Icons follow the theme's style; \"Set picture…\" uses your own image.", 48));
        scList.SelectedIndexChanged += (_, _) => scGrid.SelectedObject = SelectedShortcut();
        scGrid.PropertyValueChanged += (_, _) => { if (SelectedShortcut() is Shortcut s) AfterShortcuts(s); };
        hideIcons.CheckedChanged += (_, _) => { if (loading) return; app.HideDesktopIcons = hideIcons.Checked; appGrid.Refresh(); MarkApp(); };
        return split;
    }

    Shortcut? SelectedShortcut() => scList.SelectedItems.Count > 0 ? scList.SelectedItems[0].Tag as Shortcut : null;

    void RefreshShortcuts(Shortcut? select = null)
    {
        scList.BeginUpdate();
        scList.Items.Clear();
        scIcons.Images.Clear();
        foreach (var s in shortcuts)
        {
            int img = -1;
            try
            {
                using var src = IconSource.Get(s, themeName);
                if (src != null) { scIcons.Images.Add(IconStyler.Fit(src, 24)); img = scIcons.Images.Count - 1; }
            }
            catch { }
            var it = new ListViewItem(new[] { s.ToString(), s.Group, s.Target }, img) { Tag = s };
            scList.Items.Add(it);
            if (s == select) { it.Selected = true; it.EnsureVisible(); }
        }
        scList.EndUpdate();
        scGrid.SelectedObject = SelectedShortcut();
    }

    void AfterShortcuts(Shortcut? select) { RefreshShortcuts(select); MarkShortcuts(); }

    void AddShortcut(Shortcut s)
    {
        int at = SelectedShortcut() is Shortcut cur ? shortcuts.IndexOf(cur) + 1 : shortcuts.Count;
        shortcuts.Insert(at, s);
        AfterShortcuts(s);
    }

    void AddFile()
    {
        using var d = new OpenFileDialog { Title = "Pick a program, file or shortcut", Filter = "Programs and shortcuts|*.exe;*.lnk;*.url;*.bat;*.cmd|All files|*.*", DereferenceLinks = false, Multiselect = true };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        foreach (var f in d.FileNames) AddShortcut(new Shortcut { Name = Path.GetFileNameWithoutExtension(f), Target = f });
    }

    void AddFolder()
    {
        using var d = new FolderBrowserDialog { Description = "Pick a folder" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        AddShortcut(new Shortcut { Name = Path.GetFileName(d.SelectedPath.TrimEnd('\\')) is { Length: > 0 } n ? n : d.SelectedPath, Target = d.SelectedPath });
    }

    void AddWebsite()
    {
        var url = Ask("Add website", "Web address:", "https://", null, out _);
        if (string.IsNullOrWhiteSpace(url) || url == "https://") return;
        if (!url.Contains("://")) url = "https://" + url;
        string name = url;
        try { name = new Uri(url).Host.Replace("www.", ""); } catch { }
        AddShortcut(new Shortcut { Name = name, Target = url });
    }

    void ImportDesktop()
    {
        var found = ShortcutStore.FromDesktop();
        var known = new HashSet<string>(shortcuts.Select(s => s.Target), StringComparer.OrdinalIgnoreCase);
        var added = found.Where(f => !known.Contains(f.Target)).ToList();
        shortcuts.AddRange(added);
        AfterShortcuts(null);
        status.Text = added.Count == 0 ? "Everything on the desktop is already in the list." : $"Added {added.Count} from the desktop.";
    }

    void RemoveShortcut()
    {
        if (SelectedShortcut() is not Shortcut s) return;
        int i = shortcuts.IndexOf(s);
        shortcuts.Remove(s);
        AfterShortcuts(shortcuts.Count > 0 ? shortcuts[Math.Min(i, shortcuts.Count - 1)] : null);
    }

    void MoveShortcut(int d)
    {
        if (SelectedShortcut() is not Shortcut s) return;
        int i = shortcuts.IndexOf(s), j = i + d;
        if (j < 0 || j >= shortcuts.Count) return;
        (shortcuts[i], shortcuts[j]) = (shortcuts[j], shortcuts[i]);
        AfterShortcuts(s);
    }

    void SetPicture()
    {
        if (SelectedShortcut() is not Shortcut s) return;
        using var d = new OpenFileDialog { Title = $"Picture for {s}", Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.webp|All files|*.*" };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        Directory.CreateDirectory(Paths.Icons);
        var name = Clean(s.ToString()) + Path.GetExtension(d.FileName).ToLowerInvariant();
        var dest = Path.Combine(Paths.Icons, name);
        try { File.Copy(d.FileName, dest, true); s.Image = name; }
        catch { s.Image = d.FileName; }
        AfterShortcuts(s);
    }

    // ───────────────────────── General ─────────────────────────

    Control BuildGeneral()
    {
        var split = Split(640);
        split.Panel1.Controls.Add(appGrid);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8) };
        void B(string t, Action a, bool primary = false)
        {
            var b = new Button { Text = t, Width = 280, Height = 38, Margin = new Padding(0, 0, 0, 8) };
            b.Click += (_, _) => a(); buttons.Controls.Add(b);
            if (primary) b.HandleCreated += (_, _) => Dark.Primary(b);
        }
        B("Start Glyph", () => { if (MainGlyph().Count == 0) Process.Start(new ProcessStartInfo(Paths.Exe) { UseShellExecute = false }); UpdateRunning(); }, true);
        B("Quit Glyph", QuitGlyph);
        B("Restart Glyph", () => { QuitGlyph(); Thread.Sleep(800); Process.Start(new ProcessStartInfo(Paths.Exe) { UseShellExecute = false }); });
        B("Convert a video…", () => Process.Start(new ProcessStartInfo(Paths.Exe, "--convert-gui") { UseShellExecute = false }));
        B("Open config folder", () => Open(Paths.Config));
        B("Open guide", () => Open(Paths.Guide));
        B("Open log", () => Open(Path.Combine(Paths.Logs, "glyph.log")));
        buttons.Controls.Add(new Label { Text = "", Height = 10, Width = 280 });
        B("Check for updates now", () =>
        {
            if (MainGlyph().Count == 0) { status.Text = "Start Glyph first: it does the updating."; return; }
            Native.SendAction("glyph:update");
            status.Text = "Glyph is checking GitHub for a newer version (a note appears by the tray icon).";
        });
        buttons.Controls.Add(new Label
        {
            AutoSize = false, Width = 280, Height = 70, ForeColor = Dark.Dim, UseMnemonic = false,
            Text = $"Glyph {Updater.CurrentText}\nUpdates are looked for 30 seconds after Glyph starts, then every 6 hours (Updates, on the left).",
        });
        split.Panel2.Controls.Add(buttons);
        appGrid.PropertyValueChanged += (_, _) => { MarkApp(); loading = true; hideIcons.Checked = app.HideDesktopIcons; loading = false; };
        return split;
    }

    void QuitGlyph()
    {
        var main = MainGlyph();
        if (main.Count == 0) return;
        try { if (EventWaitHandle.TryOpenExisting(@"Local\Glyph.Quit", out var ev)) { ev.Set(); ev.Dispose(); } } catch { }
        foreach (var p in main) { try { if (!p.WaitForExit(4000)) p.Kill(); } catch { } }
        try { Desktop.DesktopIcons.Restore(); Desktop.Taskbar.Restore(); } catch { }
        UpdateRunning();
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }
}
