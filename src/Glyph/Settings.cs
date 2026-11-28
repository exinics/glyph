using System.ComponentModel;
using System.Text;
using Glyph.Options;

namespace Glyph;

/// glyph.toml: settings for Glyph itself (the look lives in each theme).
internal sealed class AppSettings
{
    [Browsable(false), Description("Which theme folder in config\\themes is shown")]
    public string Theme { get; set; } = DefaultThemes.Main;

    [Category("Glyph"), DisplayName("Start with Windows"), Description("Start Glyph when you sign in.")]
    public bool StartWithWindows { get; set; }

    [Category("Glyph"), DisplayName("Close Wallpaper Engine"), Description("Close Wallpaper Engine while Glyph draws the wallpaper itself. Themes whose backdrop is a Wallpaper Engine wallpaper (Wallpaper > Backdrop > Type: wallpaper_engine) start it instead and draw on top of it.")]
    public bool CloseWallpaperEngine { get; set; } = true;

    [Category("Glyph"), DisplayName("Hide Windows desktop icons"), Description("Hide Windows' own desktop icons while Glyph runs (shortcut panels replace them). They come back when Glyph quits.")]
    public bool HideDesktopIcons { get; set; } = true;

    [Category("Glyph"), DisplayName("Windows taskbar"), Description("keep = leave it as it is. autohide = hide it until the mouse touches the screen edge. hide = hide it completely (Glyph's bar replaces it; the Start menu still opens with the Win key). Restored when Glyph quits."), Glyph.Options.Choices("keep", "autohide", "hide"), TypeConverter(typeof(ChoicesConverter))]
    public string Taskbar { get; set; } = "keep";

    [Category("Glyph"), DisplayName("Theme sync"), Description("Let themes change Windows Terminal's colours and Windows' accent colour and dark mode (each theme's [sync] says what). Off = put all of it back as it was.")]
    public bool ThemeSync { get; set; } = true;

    [Category("Updates"), DisplayName("Updates"), Description("auto = get new versions from GitHub by itself (Glyph restarts for a moment). ask = say when there's one (tray menu > Update). off = never look."), Glyph.Options.Choices("auto", "ask", "off"), TypeConverter(typeof(ChoicesConverter))]
    public string Updates { get; set; } = "auto";

    [Category("Updates"), DisplayName("Update from"), Description("The GitHub project new versions come from (owner/name).")]
    public string UpdateRepo { get; set; } = "exinics/glyph";

    [Section, Category("More"), DisplayName("Screens"), Description("What Glyph shows on each display: the same theme everywhere, a theme per screen, or only one screen"), TypeConverter(typeof(SectionConverter))]
    public Glyph.Desktop.MonitorOptions Monitors { get; set; } = new();

    [Section, Category("More"), DisplayName("Launcher"), Description("The pop-up search (Alt+Space): what it finds. Its look is in each theme"), TypeConverter(typeof(SectionConverter))]
    public Glyph.Launcher.LauncherSettings Launcher { get; set; } = new();

    [Section, Category("More"), DisplayName("Tiling"), Description("Windows arranged side by side automatically"), TypeConverter(typeof(SectionConverter))]
    public Glyph.Tiling.TilingOptions Tiling { get; set; } = new();

    [Section, Category("More"), DisplayName("Performance"), Description("When Glyph rests to save power"), TypeConverter(typeof(SectionConverter))]
    public PerformanceOptions Performance { get; set; } = new();

    public static string FilePath => Path.Combine(Paths.Config, "glyph.toml");

    public static AppSettings Load(List<string> problems)
    {
        var s = new AppSettings();
        var file = ConfigFile.Load(FilePath, problems);
        Binder.Read(s, file.Top, null);
        s.Performance.ReadOld(file.Top.Sub("performance", "[performance]"));
        foreach (var (name, pr) in new[] { ("highest", s.Performance.Highest), ("balanced", s.Performance.Balanced), ("low", s.Performance.Low) })
            if (!PresetOptions.ValidFps(pr.Fps)) { problems.Add($"glyph.toml [performance.{name}]: fps \"{pr.Fps}\" should be max, theme or a number from 1 to 240"); pr.Fps = "theme"; }
        file.ReportUnknown();
        return s;
    }

    public void Save()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# glyph.toml: settings for Glyph itself. The look (colours, fonts, wallpaper, widgets)");
        sb.AppendLine("# lives in each theme's folder in config\\themes. Easiest to change in the settings window");
        sb.AppendLine("# (tray icon > Settings); you can also edit this file. Saved changes apply straight away.");
        sb.AppendLine();
        Binder.Write(sb, this, null);
        SafeFile.Write(FilePath, sb.ToString());
    }
}

/// glyph.toml [performance]: which preset runs when (plugged in, on battery, battery saver), the presets
/// themselves, and when Glyph rests completely.
internal sealed class PerformanceOptions
{
    static readonly string[] Modes = { "highest", "balanced", "low", "pause" };

    [DisplayName("Plugged in"), Description("Preset while the PC runs on mains power (a desktop PC always uses this). pause = don't draw at all."), Choices("highest", "balanced", "low", "pause"), TypeConverter(typeof(ChoicesConverter))]
    public string PluggedIn { get; set; } = "highest";

    [DisplayName("On battery"), Description("Preset while unplugged."), Choices("highest", "balanced", "low", "pause"), TypeConverter(typeof(ChoicesConverter))]
    public string OnBattery { get; set; } = "balanced";

    [DisplayName("Battery saver"), Description("Preset while Windows battery saver (energy saver) is on."), Choices("highest", "balanced", "low", "pause"), TypeConverter(typeof(ChoicesConverter))]
    public string BatterySaver { get; set; } = "pause";

    [DisplayName("Pause when fullscreen"), Description("Stop drawing while a game or video is fullscreen.")]
    public bool PauseWhenFullscreen { get; set; } = true;

    [DisplayName("Pause when maximised"), Description("Stop drawing while maximised windows cover the desktop.")]
    public bool PauseWhenMaximized { get; set; } = true;

    [DisplayName("Pause when locked"), Description("Stop drawing on the lock screen.")]
    public bool PauseWhenLocked { get; set; } = true;

    [Section, DisplayName("Highest"), Description("Preset: everything at the screen's full frame rate and full quality"), TypeConverter(typeof(SectionConverter))]
    public PresetOptions Highest { get; set; } = PresetOptions.MakeHighest();

    [Section, DisplayName("Balanced"), Description("Preset: each theme as it's made"), TypeConverter(typeof(SectionConverter))]
    public PresetOptions Balanced { get; set; } = PresetOptions.MakeBalanced();

    [Section, DisplayName("Low"), Description("Preset: slow and simple, for the longest battery"), TypeConverter(typeof(SectionConverter))]
    public PresetOptions Low { get; set; } = PresetOptions.MakeLow();

    public PresetOptions? Preset(string mode) => mode switch { "highest" => Highest, "balanced" => Balanced, "low" => Low, _ => null };

    /// Settings from before presets: pause_on_battery_saver becomes the battery saver choice; the old battery
    /// frame rate options are replaced by the presets.
    public void ReadOld(Node? n)
    {
        if (n == null) return;
        if (n.Has("pause_on_battery_saver") && !n.Has("battery_saver")) BatterySaver = n.Bool("pause_on_battery_saver", true) ? "pause" : "low";
        else if (n.Has("pause_on_battery_saver")) n.Bool("pause_on_battery_saver", true);
        if (n.Has("battery_fps")) n.Int("battery_fps", 15);
        if (n.Has("battery_limits_dragging")) n.Bool("battery_limits_dragging", false);
    }
}

/// One performance preset (glyph.toml [performance.highest] / .balanced / .low). Each value overrides what
/// the theme says while the preset is in use.
internal sealed class PresetOptions
{
    [DisplayName("Frame rate"), Description("max = as fast as the screen shows frames (its refresh rate, e.g. 60 or 144), whatever the theme says. theme = each theme's own frame rate. A number = the theme's frame rate, but no more than this.")]
    public string Fps { get; set; } = "max";

    [DisplayName("Wallpaper animation"), Description("Frame rate while the only thing moving is the wallpaper's own animation (ASCII sparks, rain, smoke, stars...). theme = the theme's own frame rate (e.g. 20 or 30: these move a character at a time, so more looks the same and costs far more). max = the screen's refresh rate. A number = no more than this. The mouse, widgets, animations and dragging still use Frame rate above.")]
    public string WallpaperFps { get; set; } = "theme";

    [DisplayName("Screen effects"), Description("Draw the theme's screen effects (bloom, scanlines, curvature...). Off saves the most work for the graphics chip.")]
    public bool Effects { get; set; } = true;

    [DisplayName("Animation detail"), Description("max = smoke/noise worked out fresh every frame and videos at their own full frame rate. theme = as each theme sets them. low = smoke worked out half as often, videos at most 15 frames a second."), Choices("max", "theme", "low"), TypeConverter(typeof(ChoicesConverter))]
    public string AnimationDetail { get; set; } = "max";

    [DisplayName("Smooth dragging"), Description("The selection box and the launcher run at the screen's refresh rate even when the frame rate above is lower (drawing them costs next to nothing). Off = they keep to the frame rate above.")]
    public bool SmoothDragging { get; set; } = true;

    public static PresetOptions MakeHighest() => new();
    public static PresetOptions MakeBalanced() => new() { Fps = "theme", AnimationDetail = "theme" };
    public static PresetOptions MakeLow() => new() { Fps = "15", WallpaperFps = "15", Effects = false, AnimationDetail = "low", SmoothDragging = false };

    /// The frame rate while only the wallpaper layers move (never more than FrameRate).
    public int WallpaperRate(int screen, int theme)
    {
        var v = WallpaperFps.Trim().ToLowerInvariant();
        int r = v == "max" ? Math.Max(1, screen) : int.TryParse(v, out int n) && n > 0 ? Math.Min(theme, n) : theme;
        return Math.Max(1, Math.Min(r, FrameRate(screen, theme)));
    }

    /// The frame rate to draw at: `screen` = the display's refresh rate, `theme` = the theme's own.
    public int FrameRate(int screen, int theme)
    {
        var v = Fps.Trim().ToLowerInvariant();
        if (v == "max") return Math.Max(1, screen);
        if (int.TryParse(v, out int n) && n > 0) return Math.Min(theme, n);
        return theme;
    }

    public static bool ValidFps(string v) => v.Trim().ToLowerInvariant() is "max" or "theme" || (int.TryParse(v.Trim(), out int n) && n is >= 1 and <= 240);
}

/// Screen effects drawn over everything.
internal sealed class EffectSettings
{
    [DisplayName("Effects on"), Description("Turn all screen effects on or off.")]
    public bool Enabled { get; set; } = true;
    [Description("Glow around bright things."), Range(0, 5)] public float Bloom { get; set; } = 0.5f;
    [DisplayName("Bloom threshold"), Description("How bright something must be to glow."), Range(0, 0.99)] public float BloomThreshold { get; set; } = 0.35f;
    [DisplayName("Bloom radius"), Description("How far the glow spreads."), Range(0.1, 6)] public float BloomRadius { get; set; } = 1f;
    [Description("Dark horizontal lines like an old monitor."), Range(0, 1)] public float Scanlines { get; set; } = 0.12f;
    [DisplayName("Scanline size"), Description("Pixels from one scanline to the next."), Range(1, 32)] public float ScanlineSize { get; set; } = 3f;
    [Description("Darker corners."), Range(0, 2)] public float Vignette { get; set; } = 0.3f;
    [Description("Slight brightness flicker."), Range(0, 1)] public float Flicker { get; set; } = 0.02f;
    [Description("Film grain."), Range(0, 1)] public float Grain { get; set; } = 0.03f;
    [Description("Red/blue fringing towards the edges, in pixels."), Range(0, 20)] public float Aberration { get; set; } = 0.5f;
    [Description("Bulge like a curved CRT screen."), Range(0, 1)] public float Curvature { get; set; }
    [Description("Overall brightness."), Range(0, 4)] public float Brightness { get; set; } = 1f;
    [Description("Overall contrast."), Range(0, 4)] public float Contrast { get; set; } = 1f;
}
