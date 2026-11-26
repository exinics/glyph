using Glyph.Bar;
using Glyph.Wallpaper;
using Glyph.Widgets;

namespace Glyph;

/// The example themes Glyph ships with. Also used by the settings window's "Add example themes".
internal static class DefaultThemes
{
    public static readonly string[] Names = { "red-mono", "soft" };

    static readonly (string, string)[] Terminal16 =
    {
        ("black", "#0A0A0A"), ("red", "#E0202F"), ("green", "#A3B8A0"), ("yellow", "#D9C9A3"), ("blue", "#8E9BB0"), ("magenta", "#C97A86"),
        ("cyan", "#9FB5B5"), ("white", "#E6E6E6"), ("bright_black", "#5A5A5A"), ("bright_red", "#FF4D57"), ("bright_green", "#BFD1BC"),
        ("bright_yellow", "#EDE0C2"), ("bright_blue", "#AEB9CB"), ("bright_magenta", "#E39BA6"), ("bright_cyan", "#BDD0D0"), ("bright_white", "#FFFFFF"),
    };

    /// Writes an example theme (overwrites that theme's files).
    public static void Write(string name)
    {
        Directory.CreateDirectory(Paths.ThemeDir(name));
        if (name == "soft") Soft(); else RedMono();
    }

    /// First run: make the example themes and import the desktop's shortcuts.
    public static void EnsureExist()
    {
        bool any = Directory.Exists(Paths.Themes) && Directory.GetDirectories(Paths.Themes).Any(d => File.Exists(Path.Combine(d, "theme.toml")));
        if (!any) foreach (var n in Names) Write(n);
        if (!File.Exists(ShortcutStore.FilePath)) ShortcutStore.Save(ShortcutStore.FromDesktop());
        if (!File.Exists(AppSettings.FilePath)) new AppSettings().Save();
        Directory.CreateDirectory(Paths.Art); Directory.CreateDirectory(Paths.Clips); Directory.CreateDirectory(Paths.Images); Directory.CreateDirectory(Paths.Icons);
        var logo = Path.Combine(Paths.Art, "glyph.txt");
        if (!File.Exists(logo)) File.WriteAllText(logo, Logo);
    }

    static Theme Palette(string name, params (string, string)[] colors)
    {
        var t = new Theme(name);
        foreach (var (n, hex) in colors) { Rgba.TryParseHex(hex, out var c); t.SetColor(n, c); }
        foreach (var (n, hex) in Terminal16) { Rgba.TryParseHex(hex, out var c); t.TerminalList.Add((n, c)); t.Terminal[n] = c; }
        return t;
    }

    static void RedMono()
    {
        var t = Palette("red-mono",
            ("bg", "#0A0A0A"), ("surface", "#141414"), ("line", "#2A2A2A"), ("dim", "#8A8A8A"), ("text", "#E6E6E6"),
            ("bright", "#FFFFFF"), ("accent", "#E0202F"), ("accent_dim", "#6E0E16"), ("accent_bright", "#FF4D57"));
        t.Options.Info.Description = "Black and white with red accents, drawn entirely with characters. CRT glow and scanlines.";
        t.Options.Info.Style = "terminal";
        t.Save();

        var w = new WallpaperOptions { Fps = 30 };
        w.Cursor.Radius = 9; w.Cursor.Glow = 0.1f; w.Cursor.Scatter = 0.05f;
        w.Layers.Add(new NoiseOptions
        {
            Chars = "  .:-=+*#", Gradient = new[] { "line", "accent_dim", "accent", "accent_bright" }, Scale = 0.045f, Speed = 0.06f,
            DriftX = 1.5f, Warp = 0.8f, Contrast = 1.3f, Threshold = 0.5f, UpdateFps = 6, Opacity = 0.75f,
        });
        w.Layers.Add(new RainOptions { Density = 0.18f, SpeedMin = 6, SpeedMax = 16, LengthMin = 6, LengthMax = 22, ChangeRate = 3, Opacity = 0.75f });
        w.Layers.Add(new ArtOptions
        {
            File = "glyph.txt", OffsetY = -2, Gradient = new[] { "bright", "accent" }, Background = "bg", Padding = 1,
            Pulse = 0.15f, PulseSpeed = 0.25f, Glitch = 0.4f,
        });
        w.Layers.Add(new GlitchOptions { Rate = 0.25f, Duration = 0.12f, MaxShift = 8, BandMin = 1, BandMax = 4, Scramble = 0.35f });
        w.Save(t.Name);

        new WidgetsFile
        {
            Widgets =
            {
                new ShortcutsOptions { Title = "~/desktop", Columns = 3, IconSize = 48, ItemWidth = 90 },
                new ClockOptions { OffsetX = 48, OffsetY = 30, Shadow = "accent_dim" },
                new SystemOptions { Title = "~/system", OffsetX = 40, OffsetY = 190, Width = 330 },
                new CalendarOptions { Title = "{date:MMMM yyyy}", Anchor = "bottom_right", OffsetX = 40, OffsetY = 30 },
                new MediaOptions { Title = "~/now-playing", Anchor = "bottom_right", OffsetX = 260, OffsetY = 30, Width = 420 },
            },
        }.Save(t.Name);

        var bar = new BarFile();
        bar.Bar.Height = 34; bar.Bar.Background = "bg"; bar.Bar.BorderWidth = 1; bar.Bar.BorderColor = "accent_dim"; bar.Bar.Padding = 9; bar.Bar.Gap = 18;
        bar.Left.Add(new TextModule { Format = "[bg] glyph [/]", Background = "accent", OnClick = "keys:win", HideWhenEmpty = false });
        bar.Left.Add(new WorkspacesModule());
        bar.Left.Add(new TextModule { Format = "[accent]>[/] {window}", MaxLength = 60, Color = "dim" });
        bar.Center.Add(new TextModule { Format = "{time:HH:mm:ss}", Color = "bright", OnClick = "keys:win+n" });
        bar.Right.Add(new TextModule { Format = "{media_icon} {media_title}", MaxLength = 36, Color = "dim", OnClick = "media:toggle", OnScrollUp = "media:prev", OnScrollDown = "media:next" });
        bar.Right.Add(new MeterModule { Value = "cpu", Label = "cpu", Format = "{cpu:00}%", Width = 6, OnClick = "taskmgr" });
        bar.Right.Add(new MeterModule { Value = "ram", Label = "ram", Format = "{ram:00}%", Width = 6, OnClick = "taskmgr" });
        bar.Right.Add(new TextModule { Format = "[accent]vol[/] {volume:0}%", OnClick = "volume:mute", OnScrollUp = "volume:up", OnScrollDown = "volume:down" });
        bar.Right.Add(new TextModule { Format = "[accent]bat[/] {battery}%", OnClick = "ms-settings:batterysaver" });
        bar.Right.Add(new TextModule { Format = "{date:ddd dd/MM}", Color = "dim", OnClick = "keys:win+n" });
        bar.Right.Add(new TextModule { Format = "⚙", Color = "dim", OnClick = "settings", HideWhenEmpty = false });
        bar.Save(t.Name);
    }

    static void Soft()
    {
        var t = Palette("soft",
            ("bg", "#14171C"), ("surface", "#1E232B"), ("line", "#2D3440"), ("dim", "#8C96A5"), ("text", "#E7EBF0"),
            ("bright", "#FFFFFF"), ("accent", "#6FA8DC"), ("accent_dim", "#2F4A63"), ("accent_bright", "#9CC7F0"),
            ("glow", "#232B3A"));
        t.Options.Info.Description = "Smooth example: rounded see-through panels, normal fonts, a gradient backdrop. Nothing moves, so it uses almost no power.";
        t.Options.Info.Style = "smooth";
        t.Options.Ui.CornerRadius = 14; t.Options.Ui.PanelOpacity = 0.72f; t.Options.Ui.Padding = 14; t.Options.Ui.Shadow = 0.4f;
        t.Options.Effects.Enabled = false;
        t.Save();

        var w = new WallpaperOptions { Fps = 30 };
        w.Backdrop.Type = "gradient"; w.Backdrop.Colors = new[] { "bg", "glow", "accent_dim" }; w.Backdrop.Angle = 120;
        w.Cursor.Enabled = false;
        w.Save(t.Name);

        new WidgetsFile
        {
            Widgets =
            {
                new ShortcutsOptions { Title = "Desktop", Columns = 3, IconSize = 40, ItemWidth = 86, OffsetX = 20, OffsetY = 20, HoverColor = "accent" },
                new ClockOptions { OffsetX = 60, OffsetY = 40, BigScale = 6, TextColor = "text", AccentColor = "bright" },
                new SystemOptions { Title = "System", OffsetX = 40, OffsetY = 250, Width = 320, Show = new[] { "cpu", "ram", "disk", "net" } },
                new MediaOptions { Title = "Now playing", Anchor = "bottom_right", OffsetX = 40, OffsetY = 40, Width = 380 },
                new CalendarOptions { Title = "{date:MMMM yyyy}", Anchor = "bottom_right", OffsetX = 40, OffsetY = 210 },
            },
        }.Save(t.Name);

        var bar = new BarFile();
        var b = bar.Bar;
        b.Height = 40; b.Margin = 8; b.CornerRadius = 14; b.Background = "surface"; b.Opacity = 0.55f; b.Blur = true;
        b.ModuleBackground = "glow"; b.ModuleRadius = 10; b.Padding = 8; b.Gap = 8; b.Effects = false;
        bar.Left.Add(new TextModule { Format = "  ❖  ", Color = "accent", OnClick = "keys:win", HideWhenEmpty = false });
        bar.Left.Add(new WorkspacesModule { Style = "dots", ActiveColor = "accent" });
        bar.Left.Add(new TextModule { Format = "{window}", MaxLength = 60, Color = "dim", Background = "none" });
        bar.Center.Add(new TextModule { Format = "{time:HH:mm}", Bold = true, OnClick = "keys:win+n" });
        bar.Right.Add(new TextModule { Format = "♪  {media_title}", MaxLength = 32, OnClick = "media:toggle", OnScrollUp = "media:prev", OnScrollDown = "media:next" });
        bar.Right.Add(new MeterModule { Value = "cpu", Label = "CPU", Format = "{cpu:0}%", Width = 5, OnClick = "taskmgr" });
        bar.Right.Add(new MeterModule { Value = "ram", Label = "RAM", Format = "{ram:0}%", Width = 5, OnClick = "taskmgr" });
        bar.Right.Add(new TextModule { Format = "Vol {volume:0}%", OnClick = "volume:mute", OnScrollUp = "volume:up", OnScrollDown = "volume:down" });
        bar.Right.Add(new TextModule { Format = "Bat {battery}%", OnClick = "ms-settings:batterysaver" });
        bar.Right.Add(new TextModule { Format = " ⚙ ", Color = "dim", OnClick = "settings", HideWhenEmpty = false });
        bar.Save(t.Name);
    }

    const string Logo = """
 ██████╗ ██╗  ██╗   ██╗██████╗ ██╗  ██╗
██╔════╝ ██║  ╚██╗ ██╔╝██╔══██╗██║  ██║
██║  ███╗██║   ╚████╔╝ ██████╔╝███████║
██║   ██║██║    ╚██╔╝  ██╔═══╝ ██╔══██║
╚██████╔╝███████╗██║   ██║     ██║  ██║
 ╚═════╝ ╚══════╝╚═╝   ╚═╝     ╚═╝  ╚═╝

""";
}
