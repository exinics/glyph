using System.ComponentModel;
using System.Text;
using Glyph.Options;

namespace Glyph.Widgets;

/// widgets.toml in a theme folder: the panels on the desktop.
internal sealed class WidgetsFile
{
    public List<WidgetOptions> Widgets { get; set; } = new();

    public static readonly Dictionary<string, Func<WidgetOptions>> Types = new()
    {
        ["shortcuts"] = () => new ShortcutsOptions(), ["clock"] = () => new ClockOptions(), ["system"] = () => new SystemOptions(),
        ["media"] = () => new MediaOptions(), ["calendar"] = () => new CalendarOptions(), ["weather"] = () => new WeatherOptions(),
        ["text"] = () => new TextOptions(), ["command"] = () => new CommandOptions(), ["sysinfo"] = () => new SysInfoOptions(),
        ["image"] = () => new ImageOptions(), ["video"] = () => new VideoOptions(), ["audio"] = () => new AudioOptions(),
        ["feed"] = () => new FeedOptions(),
    };

    /// Short descriptions for the settings window's "Add widget" menu and the guide.
    public static readonly Dictionary<string, string> Help = new()
    {
        ["shortcuts"] = "your shortcuts in a grid", ["clock"] = "a big clock and date", ["system"] = "CPU, memory, disk, network with meters and graphs",
        ["media"] = "what's playing, with album art and controls", ["calendar"] = "this month", ["weather"] = "weather and forecast for a city",
        ["text"] = "any text, with live values like {cpu} and colours", ["command"] = "the output of a command or script",
        ["sysinfo"] = "ASCII logo with system info (neofetch style)", ["image"] = "a picture", ["video"] = "plays a video or an ASCII clip (as characters or the real picture)", ["audio"] = "sound controls: output device, master and per-app volume",
        ["feed"] = "lines that keep coming in: made-up hex, logs or values (a live-looking readout)",
    };

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "widgets.toml");

    public static WidgetsFile Load(Theme theme, List<string> problems)
    {
        var w = new WidgetsFile();
        if (!File.Exists(FilePath(theme.Name))) return w;
        var file = ConfigFile.Load(FilePath(theme.Name), problems);
        foreach (var n in file.Top.Array("widget"))
        {
            var type = n.Str("type", "", Types.Keys.ToArray());
            if (type.Length == 0) { if (!n.Has("type")) problems.Add($"{file.Name}: {n.Label} needs a type, e.g. type = \"shortcuts\""); continue; }
            var o = Types[type]();
            Binder.Read(o, n.WithLabel($"{n.Label} ({type})"), theme);
            w.Widgets.Add(o);
        }
        file.ReportUnknown();
        return w;
    }

    public void Save(string theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# The panels on the desktop. Easiest to change in the settings window");
        sb.AppendLine("# (tray icon > Settings > Shortcuts); you can also edit this file.");
        foreach (var o in Widgets)
        {
            sb.AppendLine().AppendLine("[[widget]]");
            sb.AppendLine($"type = \"{o.Type}\"");
            Binder.WriteValues(sb, o);
        }
        SafeFile.Write(FilePath(theme), sb.ToString());
    }
}

internal abstract class WidgetOptions
{
    [Browsable(false)] public abstract string Type { get; }
    [Category("Placement"), Description("Show this panel.")] public bool Enabled { get; set; } = true;
    [Category("Placement"), Description("A name for this panel, so another panel can draw a line to it (Link to).")] public string Name { get; set; } = "";
    [Category("Placement"), Description("Which corner or edge of the screen it sits against."), Choices("top_left", "top", "top_right", "left", "center", "right", "bottom_left", "bottom", "bottom_right"), TypeConverter(typeof(ChoicesConverter))]
    public string Anchor { get; set; } = "top_left";
    [Category("Placement"), DisplayName("Offset X"), Description("Move it right (or left if negative), in pixels.")] public int OffsetX { get; set; } = 16;
    [Category("Placement"), DisplayName("Offset Y"), Description("Move it down (or up if negative), in pixels.")] public int OffsetY { get; set; } = 16;
    [Category("Box"), Description("A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style."), TypeConverter(typeof(FrameNameConverter))] public string Frame { get; set; } = "auto";
    [Category("Box"), DisplayName("Box colour"), Description("Colour of this panel. auto = the theme's panel colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string Background { get; set; } = "auto";
    [Category("Box"), DisplayName("Box opacity"), Description("0 = see-through, 1 = solid, -1 = the theme's."), Range(-1, 1)] public float Opacity { get; set; } = -1;
    [Category("Box"), DisplayName("Corner radius"), Description("Smooth style, in pixels. -1 = the theme's."), Range(-1, 200)] public float CornerRadius { get; set; } = -1;
    [Category("Box"), DisplayName("Border colour"), Description("auto = the theme's."), Color, TypeConverter(typeof(ColorNameConverter))] public string BorderColor { get; set; } = "auto";
    [Category("Box"), DisplayName("Border width"), Description("Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines."), Range(-1, 50)] public float BorderWidth { get; set; } = -1;
    [Category("Box"), DisplayName("Box shadow"), Description("Smooth style. -1 = the theme's."), Range(-1, 1)] public float PanelShadow { get; set; } = -1;
    [Category("Box"), DisplayName("Hover colour"), Description("If the box does something on click, it lights up in this colour under the mouse. none = no highlight."), Color, TypeConverter(typeof(ColorNameConverter))] public string BoxHoverColor { get; set; } = "none";
    [Category("Link"), DisplayName("Link to"), Description("Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style.")] public string LinkTo { get; set; } = "";
    [Category("Link"), DisplayName("Link colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string LinkColor { get; set; } = "accent_dim";
    [Category("Link"), DisplayName("Link width"), Range(0.5, 20)] public float LinkWidth { get; set; } = 1;
    [Category("Link"), DisplayName("Link lines"), Description("How many lines side by side."), Range(1, 5)] public int LinkLines { get; set; } = 1;
    [Category("Link"), DisplayName("Link gap"), Description("Pixels between the lines."), Range(1, 50)] public float LinkGap { get; set; } = 5;
    [Category("Link"), DisplayName("Link shape"), Description("elbow = a 45° bend then straight, straight = one straight line."), Choices("elbow", "straight"), TypeConverter(typeof(ChoicesConverter))] public string LinkShape { get; set; } = "elbow";
    [Category("Link"), DisplayName("Link ends"), Description("A small square where the line meets the panels."), Choices("none", "dot", "both"), TypeConverter(typeof(ChoicesConverter))] public string LinkEnds { get; set; } = "dot";

    // the box look with "auto" values filled in from the theme
    public Rgba BoxColor(Theme t) => t[Background == "auto" ? t.Options.Ui.PanelColor : Background];
    public float BoxOpacity(Theme t) => Opacity < 0 ? t.Options.Ui.PanelOpacity : Opacity;
    public float BoxRadius(Theme t) => CornerRadius < 0 ? t.Options.Ui.CornerRadius : CornerRadius;
    public Rgba BoxBorder(Theme t) => t[BorderColor == "auto" ? t.Options.Ui.BorderColor : BorderColor];
    public float BoxBorderWidth(Theme t) => BorderWidth < 0 ? t.Options.Ui.BorderWidth : BorderWidth;
    public float BoxShadow(Theme t) => PanelShadow < 0 ? t.Options.Ui.Shadow : PanelShadow;
}

internal sealed class ShortcutsOptions : WidgetOptions
{
    public override string Type => "shortcuts";
    [Category("Panel"), Description("Title shown on the panel. Empty = none.")] public string Title { get; set; } = "";
    [Category("Panel"), Description("Draw a panel behind the shortcuts.")] public bool Panel { get; set; } = true;
    [Category("Panel"), Description("Only show shortcuts in this group. Empty = all shortcuts.")] public string Group { get; set; } = "";
    [Category("Panel"), Description("Number of columns."), Range(1, 50)] public int Columns { get; set; } = 3;
    [Category("Panel"), DisplayName("Open with"), Description("double_click like Windows, or a single click."), Choices("double_click", "click"), TypeConverter(typeof(ChoicesConverter))]
    public string Launch { get; set; } = "double_click";

    [Category("Icons"), DisplayName("Icon size"), Description("In pixels."), Range(12, 512)] public int IconSize { get; set; } = 48;
    [Category("Icons"), DisplayName("Item width"), Description("Width of each shortcut including its label, in pixels."), Range(16, 1000)] public int ItemWidth { get; set; } = 84;
    [Category("Icons"), Description("Gap between shortcuts, in pixels."), Range(0, 200)] public int Spacing { get; set; } = 8;
    [Category("Icons"), DisplayName("Icon style"),
     Description("auto = ascii in terminal themes, original in smooth ones. original = the app's own icon. tint = recoloured with the tint colours. mono = a flat silhouette. ascii = drawn with characters."),
     Choices("auto", "original", "tint", "mono", "ascii"), TypeConverter(typeof(ChoicesConverter))]
    public string IconStyle { get; set; } = "auto";
    [Category("Icons"), DisplayName("ASCII kind"), Description("For the ascii style: braille = fine dots, blocks = chunky pixels."), Choices("braille", "blocks"), TypeConverter(typeof(ChoicesConverter))]
    public string AsciiMode { get; set; } = "braille";
    [Category("Icons"), DisplayName("Tint colours"), Description("Dark to bright, for the tint and ascii styles."), Color, TypeConverter(typeof(ListConverter))]
    public string[] TintColors { get; set; } = { "accent_dim", "accent", "bright" };
    [Category("Icons"), DisplayName("Mono colour"), Description("For the mono style."), Color, TypeConverter(typeof(ColorNameConverter))]
    public string IconColor { get; set; } = "accent";

    [Category("Labels"), DisplayName("Show labels")] public bool ShowLabels { get; set; } = true;
    [Category("Labels"), DisplayName("Label lines"), Description("How many lines a long name may take."), Range(1, 3)] public int LabelLines { get; set; } = 2;
    [Category("Labels"), DisplayName("Label colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string LabelColor { get; set; } = "text";
    [Category("Labels"), DisplayName("Hover colour"), Description("Highlight under the mouse."), Color, TypeConverter(typeof(ColorNameConverter))] public string HoverColor { get; set; } = "accent";

    public override string ToString() => $"shortcuts{(Title.Length > 0 ? ": " + Title : "")}{(Group.Length > 0 ? $" [{Group}]" : "")}{(Enabled ? "" : " (off)")}";
}


/// Options every info panel shares.
internal abstract class PanelOptions : WidgetOptions
{
    [Category("Panel"), Description("Title shown on the panel. Empty = none. Can use {values}.")] public string Title { get; set; } = "";
    [Category("Panel"), DisplayName("Title right"), Description("Text at the right end of the title (e.g. a status like LIVE). Can use {values}.")] public string TitleRight { get; set; } = "";
    [Category("Panel"), Description("Draw a panel behind it.")] public bool Panel { get; set; } = true;
    [Category("Panel"), Description("Width in pixels. 0 = fit the contents."), Range(0, 4000)] public int Width { get; set; }
    [Category("Panel"), Description("Height in pixels. 0 = fit the contents."), Range(0, 4000)] public int Height { get; set; }
    [Category("Panel"), DisplayName("Vertical align"), Description("Where the contents sit when the panel is taller than them."), Choices("top", "center", "bottom"), TypeConverter(typeof(ChoicesConverter))] public string VerticalAlign { get; set; } = "top";
    [Category("Panel"), Description("Make the text bigger or smaller (1 = the theme's size). Smooth style."), Range(0.3, 6)] public float Scale { get; set; } = 1;
    [Category("Colours"), DisplayName("Text colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TextColor { get; set; } = "text";
    [Category("Colours"), DisplayName("Accent colour"), Description("Highlights, meters, graphs."), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentColor { get; set; } = "accent";
    [Category("Colours"), DisplayName("Dim colour"), Description("Labels and less important text."), Color, TypeConverter(typeof(ColorNameConverter))] public string DimColor { get; set; } = "dim";
    [Category("Panel"), DisplayName("On click"), Description(Glyph.Data.Actions.Help)] public string OnClick { get; set; } = "";
    public override string ToString() => $"{Type}{(Title.Length > 0 ? ": " + Title : "")}{(Enabled ? "" : " (off)")}";
}

internal sealed class ClockOptions : PanelOptions
{
    public override string Type => "clock";
    public ClockOptions() { Panel = false; Anchor = "top_right"; OffsetX = 40; OffsetY = 40; }
    [Category("Clock"), Description("The big line. {time:HH:mm} = 24-hour, {time:h:mm tt} = 12-hour, add :ss for seconds.")] public string Format { get; set; } = "{time:HH:mm}";
    [Category("Clock"), Description("The line under it. Empty = none.")] public string Subtitle { get; set; } = "{date:dddd d MMMM}";
    [Category("Clock"), Choices("left", "center", "right"), TypeConverter(typeof(ChoicesConverter))] public string Align { get; set; } = "center";
    [Category("Clock"), DisplayName("Terminal pixel"), Description("Terminal style: the character(s) the big digits are built from, e.g. █, ▓, # or [].")] public string Pixel { get; set; } = "█";
    [Category("Clock"), DisplayName("Shadow colour"), Description("A shadow behind the big digits. none = no shadow."), Color, TypeConverter(typeof(ColorNameConverter))] public string Shadow { get; set; } = "none";
    [Category("Clock"), DisplayName("Big size"), Description("Smooth style: size of the big line compared with normal text."), Range(1, 20)] public float BigScale { get; set; } = 5;
    [Category("Clock"), DisplayName("Big font"), Description("Smooth style: font of the big line. Empty = the theme's font."), TypeConverter(typeof(FontNameConverter))] public string BigFont { get; set; } = "Segoe UI Light";
}

internal sealed class SystemOptions : PanelOptions
{
    public override string Type => "system";
    public SystemOptions() { Title = "system"; Anchor = "top_right"; OffsetY = 260; Width = 300; }
    [Category("System"), Description("What to show, in order: cpu, ram, disk, net, battery, volume."), TypeConverter(typeof(ListConverter))] public string[] Show { get; set; } = { "cpu", "ram", "disk", "net" };
    [Category("System"), Description("Draw a history graph under CPU and network.")] public bool Graphs { get; set; } = true;
    [Category("System"), DisplayName("Graph rows"), Description("Height of the graphs, in lines of text."), Range(1, 10)] public int GraphRows { get; set; } = 2;
    [Category("System"), DisplayName("Meter characters"), Description("Terminal style: the filled and empty characters of a meter, e.g. █░, ▮▯, =-, ■□.")] public string MeterChars { get; set; } = "█░";
    [Category("System"), DisplayName("Meter blocks"), Description("Smooth style: draw meters as this many separate blocks. 0 = smooth bars."), Range(0, 100)] public int Segments { get; set; }
    [Category("System"), DisplayName("Meters on one line"), Description("Smooth style: label, meter and value on one line instead of the meter under the label.")] public bool OneLine { get; set; }
    [Category("System"), Description("Which drive the disk line shows.")] public string Drive { get; set; } = "C";
}

internal sealed class MediaOptions : PanelOptions
{
    public override string Type => "media";
    public MediaOptions() { Title = "now playing"; Anchor = "bottom_right"; OffsetX = 40; OffsetY = 40; Width = 360; }
    [Category("Media"), DisplayName("Line above"), Description("A small line above the first line, e.g. [dim]{media_app|upper} // ON AIR[/]. Empty = none.")] public string Line0 { get; set; } = "";
    [Category("Media"), DisplayName("First line"), Description("Can use {media_title}, {media_artist}, {media_album}, {media_app}, {media_state}.")] public string Line1 { get; set; } = "{media_title}";
    [Category("Media"), DisplayName("Second line")] public string Line2 { get; set; } = "{media_artist}";
    [Category("Media"), DisplayName("Album art"), Description("Show the album art (as characters in terminal themes).")] public bool Art { get; set; } = true;
    [Category("Media"), DisplayName("Art size"), Description("Height of the album art, in lines of text."), Range(2, 30)] public int ArtRows { get; set; } = 5;
    [Category("Media"), DisplayName("Art style"), Choices("auto", "original", "tint", "mono", "ascii"), TypeConverter(typeof(ChoicesConverter))] public string ArtStyle { get; set; } = "auto";
    [Category("Media"), Description("Show the progress bar and times.")] public bool Progress { get; set; } = true;
    [Category("Media"), DisplayName("Times beside the bar"), Description("Smooth style: position on the left of the progress bar, length on the right (instead of both after it).")] public bool TimesBeside { get; set; }
    [Category("Media"), DisplayName("Progress blocks"), Description("Smooth style: draw the progress bar as this many blocks. 0 = a smooth bar."), Range(0, 200)] public int ProgressSegments { get; set; }
    [Category("Media"), DisplayName("Visualiser"), Description("Bars that bounce with the sound playing (reads what your speakers play; costs a little CPU while music plays).")] public bool Visualiser { get; set; }
    [Category("Media"), DisplayName("Visualiser bars"), Range(4, 128)] public int VisualiserBars { get; set; } = 32;
    [Category("Media"), DisplayName("Visualiser height"), Description("In lines of text."), Range(0.5, 20)] public float VisualiserRows { get; set; } = 2;
    [Category("Media"), DisplayName("Visualiser colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string VisualiserColor { get; set; } = "accent_dim";
    [Category("Media"), DisplayName("Visualiser peak colour"), Description("Colour of the tallest bars."), Color, TypeConverter(typeof(ColorNameConverter))] public string VisualiserPeak { get; set; } = "accent";
    [Category("Media"), DisplayName("Visualiser rate"), Description("Frames per second while it moves (higher = smoother, more CPU)."), Range(5, 60)] public int VisualiserFps { get; set; } = 24;
    [Category("Media"), Description("Show previous / play-pause / next buttons.")] public bool Controls { get; set; } = true;
    [Category("Media"), DisplayName("Hide when nothing plays"), Description("Hide the whole panel when no media app is open.")] public bool HideWhenIdle { get; set; } = true;
}

internal sealed class CalendarOptions : PanelOptions
{
    public override string Type => "calendar";
    public CalendarOptions() { Title = "{date:MMMM yyyy}"; Anchor = "bottom_left"; OffsetX = 360; OffsetY = 40; }
    [Category("Calendar"), DisplayName("First day"), Choices("monday", "sunday"), TypeConverter(typeof(ChoicesConverter))] public string FirstDay { get; set; } = "monday";
    [Category("Calendar"), DisplayName("Today colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TodayColor { get; set; } = "accent";
    [Category("Calendar"), DisplayName("Weekend colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string WeekendColor { get; set; } = "dim";
    [Category("Calendar"), DisplayName("Week numbers")] public bool WeekNumbers { get; set; }
}

internal sealed class WeatherOptions : PanelOptions
{
    public override string Type => "weather";
    public WeatherOptions() { Title = "{city}"; Anchor = "bottom_right"; OffsetX = 420; OffsetY = 40; }
    [Category("Weather"), Description("City name (looked up online). Or set latitude and longitude instead.")] public string City { get; set; } = "";
    [Category("Weather"), Range(-90, 90)] public double Latitude { get; set; }
    [Category("Weather"), Range(-180, 180)] public double Longitude { get; set; }
    [Category("Weather"), Choices("celsius", "fahrenheit"), TypeConverter(typeof(ChoicesConverter))] public string Units { get; set; } = "celsius";
    [Category("Weather"), DisplayName("Main line"), Description("Can use {temp}, {feels}, {weather}, {weather_icon}, {wind}, {humidity}, {high}, {low}, {city}.")] public string Line { get; set; } = "{weather_icon} {temp:0}°  {weather}";
    [Category("Weather"), DisplayName("Detail line")] public string Detail { get; set; } = "feels {feels:0}° · wind {wind:0} · {humidity:0}%";
    [Category("Weather"), DisplayName("Forecast days"), Range(0, 7)] public int Days { get; set; } = 4;
}

internal sealed class TextOptions : PanelOptions
{
    public override string Type => "text";
    public TextOptions() { Anchor = "center"; OffsetY = 0; }
    [Category("Text"), Description("The text. Use {values} like {cpu}, [colour]…[/] for colours, and \\n for a new line.")] public string Text { get; set; } = "[accent]{user}[/]@{host}\\nup {uptime}";
    [Category("Text"), Choices("small", "normal", "large", "title"), TypeConverter(typeof(ChoicesConverter))] public string Size { get; set; } = "normal";
    [Category("Text"), Choices("left", "center", "right"), TypeConverter(typeof(ChoicesConverter))] public string Align { get; set; } = "left";
    [Category("Text"), Description("Use the theme's monospace font (lines up columns and ASCII art).")] public bool Mono { get; set; }
}

internal sealed class CommandOptions : PanelOptions
{
    public override string Type => "command";
    public CommandOptions() { Title = "command"; Anchor = "left"; }
    [Category("Command"), Description("A program and its arguments; its printed output is shown. E.g. powershell -NoProfile -Command \"Get-Date\".")]
    public string Command { get; set; } = "powershell -NoProfile -Command \"Get-Process | Sort-Object CPU -Descending | Select-Object -First 5 -ExpandProperty Name\"";
    [Category("Command"), Description("Run it again every this many seconds."), Range(1, 86400)] public int Interval { get; set; } = 30;
    [Category("Command"), DisplayName("Max lines"), Range(1, 200)] public int MaxLines { get; set; } = 12;
    [Category("Command"), Description("Use the theme's monospace font.")] public bool Mono { get; set; } = true;
}

internal sealed class SysInfoOptions : PanelOptions
{
    public override string Type => "sysinfo";
    public SysInfoOptions() { Anchor = "center"; OffsetY = 200; Panel = false; }
    [Category("System info"), Description("ASCII art file (in the theme folder or config\\art). Empty = a built-in Windows logo.")] public string Art { get; set; } = "";
    [Category("System info"), DisplayName("Art colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string ArtColor { get; set; } = "accent";
    [Category("System info"), Description("The lines beside the art. Use {values} and [colour]…[/] markup."), TypeConverter(typeof(ListConverter))]
    public string[] Lines { get; set; } = { "[accent]{user}[/]@[accent]{host}[/]", "[dim]───────────────[/]", "[accent]os[/]      {os}", "[accent]cpu[/]     {cpu_name}", "[accent]gpu[/]     {gpu_name}", "[accent]memory[/]  {ram_used} / {ram_total}", "[accent]uptime[/]  {uptime}" };
}

internal sealed class FeedOptions : PanelOptions
{
    public override string Type => "feed";
    public FeedOptions() { Title = "feed"; Anchor = "top_right"; OffsetY = 60; Width = 280; }
    [Category("Feed"), Description("Patterns for new lines (one is picked at random each time). Besides {values}: {hex:4} = random hex digits, {num:10-99} = a random number, {pick:a,b,c} = one of these, {seq:X4} = a counting number (X4 = 4 hex digits), {spaces:0-6} = random spaces. [colour]…[/] works too."), TypeConverter(typeof(ListConverter))]
    public string[] Lines { get; set; } = { "0x{seq:X4}  {pick:SYN,ACK,PKT,AUTH,ROUTE}  {hex:2}:{hex:2}  {pick:OK,OK,OK,LOCKED,PASS}  {hex:4}-{hex:4}" };
    [Category("Feed"), Description("Seconds between new lines."), Range(0.05, 3600)] public float Interval { get; set; } = 1.5f;
    [Category("Feed"), DisplayName("Random wait"), Description("Make the wait between lines vary by up to this fraction (0.5 = ±50%)."), Range(0, 1)] public float Jitter { get; set; } = 0.5f;
    [Category("Feed"), DisplayName("Lines shown"), Range(1, 200)] public int MaxLines { get; set; } = 8;
    [Category("Feed"), DisplayName("Newest at the top")] public bool NewestFirst { get; set; }
    [Category("Feed"), DisplayName("Newest colour"), Description("Colour of the newest line."), Color, TypeConverter(typeof(ColorNameConverter))] public string NewestColor { get; set; } = "bright";
    [Category("Feed"), DisplayName("Older colour"), Description("Older lines fade from Text colour to this one."), Color, TypeConverter(typeof(ColorNameConverter))] public string OldColor { get; set; } = "dim";
    [Category("Feed"), Description("Use the theme's monospace font (lines up columns).")] public bool Mono { get; set; } = true;
    [Category("Feed"), Choices("small", "normal", "large"), TypeConverter(typeof(ChoicesConverter))] public string Size { get; set; } = "small";
}

internal sealed class ImageOptions : PanelOptions
{
    public override string Type => "image";
    public ImageOptions() { Panel = false; Anchor = "right"; }
    [Category("Image"), Description("A picture (PNG/JPG), looked for in the theme folder and config\\images."), Editor(typeof(System.Windows.Forms.Design.FileNameEditor), typeof(System.Drawing.Design.UITypeEditor))] public string File { get; set; } = "";
    [Category("Image"), Description("Height, in lines of text."), Range(1, 100)] public int Rows { get; set; } = 10;
    [Category("Image"), Description("auto = characters in terminal themes, the picture in smooth ones."), Choices("auto", "original", "tint", "mono", "ascii"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "auto";
    [Category("Image"), DisplayName("ASCII kind"), Choices("braille", "blocks"), TypeConverter(typeof(ChoicesConverter))] public string AsciiMode { get; set; } = "braille";
}
