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

    [Category("Glyph"), DisplayName("Close Wallpaper Engine"), Description("Glyph and Wallpaper Engine can't both draw the wallpaper, so Glyph closes it when it starts.")]
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

internal sealed class PerformanceOptions
{
    [DisplayName("Pause when fullscreen"), Description("Stop drawing while a game or video is fullscreen.")]
    public bool PauseWhenFullscreen { get; set; } = true;

    [DisplayName("Pause when maximised"), Description("Stop drawing while maximised windows cover the desktop.")]
    public bool PauseWhenMaximized { get; set; } = true;

    [DisplayName("Pause on battery saver"), Description("Stop drawing while Windows battery saver is on.")]
    public bool PauseOnBatterySaver { get; set; } = true;

    [DisplayName("Pause when locked"), Description("Stop drawing on the lock screen.")]
    public bool PauseWhenLocked { get; set; } = true;

    [DisplayName("Frame rate on battery"), Description("Frame rate limit when unplugged. 0 = no limit."), Range(0, 240)]
    public int BatteryFps { get; set; } = 15;
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
