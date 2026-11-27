using System.ComponentModel;
using System.Text;
using Glyph.Options;

namespace Glyph.Bar;

/// bar.toml in a theme folder: the bar and its modules (left, centre and right).
internal sealed class BarFile
{
    public BarOptions Bar { get; set; } = new();
    public List<BarModuleOptions> Left { get; set; } = new();
    public List<BarModuleOptions> Center { get; set; } = new();
    public List<BarModuleOptions> Right { get; set; } = new();

    public static readonly Dictionary<string, Func<BarModuleOptions>> Types = new()
    {
        ["text"] = () => new TextModule(), ["meter"] = () => new MeterModule(), ["graph"] = () => new GraphModule(),
        ["workspaces"] = () => new WorkspacesModule(), ["spacer"] = () => new SpacerModule(), ["icons"] = () => new IconsModule(),
        ["tasks"] = () => new TasksModule(), ["tray"] = () => new TrayModule(),
    };

    /// Ready-made modules for the settings window's "Add module" menu.
    public static readonly (string name, Func<BarModuleOptions> make)[] Presets =
    {
        ("Clock", () => new TextModule { Format = "{time:HH:mm}", OnClick = "keys:win+n" }),
        ("Date", () => new TextModule { Format = "{date:ddd d MMM}", Color = "dim", OnClick = "keys:win+n" }),
        ("Active window", () => new TextModule { Format = "{window}", MaxLength = 60, Color = "dim" }),
        ("Workspaces (virtual desktops)", () => new WorkspacesModule()),
        ("Tiling (click: next layout, right-click: on/off)", () => new TextModule { Format = "[accent]tile[/] {tiling}", Color = "dim", OnClick = "tile:layout:next", OnRightClick = "tile:toggle" }),
        ("Launcher button", () => new TextModule { Format = "⌕", Color = "accent", OnClick = "launcher" }),
        ("App icons (a shortcuts group)", () => new IconsModule()),
        ("Open windows (like the taskbar)", () => new TasksModule()),
        ("Tray icons (notification area)", () => new TrayModule()),
        ("Start menu button", () => new TextModule { Format = "{icon:apps}", OnClick = "keys:win", OnRightClick = "keys:win+x" }),
        ("Notifications and calendar", () => new TextModule { Format = "{icon:notifications}", OnClick = "keys:win+n" }),
        ("Quick settings (Wi-Fi, sound…)", () => new TextModule { Format = "{icon:tune}", OnClick = "keys:win+a" }),
        ("Show desktop", () => new TextModule { Format = "{icon:desktop_windows}", OnClick = "keys:win+d" }),
        ("Wi-Fi", () => new TextModule { Format = "[dim]net[/] {wifi_signal|bar:4:▮▯} {wifi|default:{network}}", OnClick = "ms-settings:network-wifi" }),
        ("Bluetooth", () => new TextModule { Format = "[dim]bt[/] {bluetooth|upper|default:--}", OnClick = "ms-settings:bluetooth" }),
        ("Keyboard language", () => new TextModule { Format = "[dim]lng[/] {lang}", OnClick = "keys:win+space" }),
        ("Power menu", () => new TextModule { Format = "⏻", Color = "accent_bright", OnClick = "keys:win+x" }),
        ("Media", () => new TextModule { Format = "{media_icon} {media_artist} – {media_title}", MaxLength = 50, OnClick = "media:toggle", OnScrollUp = "media:prev", OnScrollDown = "media:next" }),
        ("CPU", () => new TextModule { Format = "[accent]cpu[/] {cpu:0}%", OnClick = "taskmgr" }),
        ("CPU meter", () => new MeterModule { Value = "cpu", Label = "cpu", Format = "{cpu:0}%", OnClick = "taskmgr" }),
        ("CPU graph", () => new GraphModule { Value = "cpu" }),
        ("Memory", () => new TextModule { Format = "[accent]ram[/] {ram:0}%", OnClick = "taskmgr" }),
        ("Disk", () => new TextModule { Format = "[accent]disk[/] {disk_free}" }),
        ("Network", () => new TextModule { Format = "↓{net_down} ↑{net_up}", Color = "dim" }),
        ("Battery", () => new TextModule { Format = "{battery_icon} {battery}%", OnClick = "ms-settings:batterysaver" }),
        ("Volume", () => new TextModule { Format = "[accent]vol[/] {volume:0}% {muted}", OnClick = "volume:mute", OnScrollUp = "volume:up", OnScrollDown = "volume:down" }),
        ("Sound output (click to switch)", () => new TextModule { Format = "[accent]out[/] {audio_device}", MaxLength = 32, Color = "dim", OnClick = "audio:device:next", OnRightClick = "audio:mixer", OnScrollUp = "volume:up", OnScrollDown = "volume:down" }),
        ("Weather", () => new TextModule { Format = "{weather_icon} {temp:0}°" }),
        ("Start button", () => new TextModule { Format = "❖", Color = "accent", OnClick = "keys:win" }),
        ("Settings button", () => new TextModule { Format = "⚙", Color = "dim", OnClick = "settings" }),
        ("Separator", () => new TextModule { Format = "│", Color = "line", HideWhenEmpty = false }),
        ("Spacer", () => new SpacerModule()),
        ("Custom text", () => new TextModule { Format = "hello {user}" }),
    };

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "bar.toml");

    public static BarFile Load(Theme theme, List<string> problems)
    {
        var b = new BarFile();
        if (!File.Exists(FilePath(theme.Name))) { b.Bar.Enabled = false; return b; }
        var file = ConfigFile.Load(FilePath(theme.Name), problems);
        var top = file.Top;
        Binder.Read(b.Bar, top, theme);
        foreach (var (key, list) in new[] { ("left", b.Left), ("center", b.Center), ("right", b.Right) })
            foreach (var n in top.Array(key))
            {
                var type = n.Str("type", "text", Types.Keys.ToArray());
                var o = Types[type]();
                Binder.Read(o, n.WithLabel($"{n.Label} ({type})"), theme);
                list.Add(o);
            }
        file.ReportUnknown();
        return b;
    }

    public void Save(string theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# The bar. Easiest to change in the settings window (tray icon > Settings > Bar);");
        sb.AppendLine("# you can also edit this file. Modules are listed under [[left]], [[center]] and [[right]].");
        sb.AppendLine();
        Binder.Write(sb, Bar, null);
        foreach (var (key, list) in new[] { ("left", Left), ("center", Center), ("right", Right) })
            foreach (var m in list)
            {
                sb.AppendLine().AppendLine($"[[{key}]]");
                sb.AppendLine($"type = \"{m.Type}\"");
                Binder.WriteValues(sb, m);
            }
        SafeFile.Write(FilePath(theme), sb.ToString());
    }
}

internal sealed class BarOptions
{
    [Category("Bar"), Description("Show the bar.")] public bool Enabled { get; set; } = true;
    [Category("Bar"), Description("Top or bottom of the screen."), Choices("top", "bottom"), TypeConverter(typeof(ChoicesConverter))] public string Position { get; set; } = "top";
    [Category("Bar"), Description("Height in pixels (terminal themes round this to whole rows of characters)."), Range(10, 200)] public int Height { get; set; } = 34;
    [Category("Bar"), DisplayName("Reserve space"), Description("Windows (and maximised windows) stop at the bar instead of going under it.")] public bool ReserveSpace { get; set; } = true;
    [Category("Bar"), Description("Gap between the bar and the screen edges, in pixels. Above 0 the bar floats."), Range(0, 200)] public int Margin { get; set; }
    [Category("Look"), DisplayName("Corner radius"), Description("Round the bar's corners (pixels). Looks best with a margin."), Range(0, 100)] public float CornerRadius { get; set; }
    [Category("Look"), Description("Bar colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string Background { get; set; } = "surface";
    [Category("Look"), Description("0 = see-through, 1 = solid."), Range(0, 1)] public float Opacity { get; set; } = 1;
    [Category("Look"), Description("Blur what's behind the bar (frosted glass). Use with opacity below 1.")] public bool Blur { get; set; }
    [Category("Look"), DisplayName("Border colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string BorderColor { get; set; } = "line";
    [Category("Look"), DisplayName("Border width"), Description("A line along the bar's inner edge, in pixels (terminal: any value draws a line of characters)."), Range(0, 20)] public float BorderWidth { get; set; }
    [Category("Look"), Description("Font for the bar. Empty = the theme's (character font in terminal themes)."), TypeConverter(typeof(FontNameConverter))] public string Font { get; set; } = "";
    [Category("Look"), DisplayName("Font size"), Description("0 = the theme's."), Range(0, 96)] public float FontSize { get; set; }
    [Category("Look"), Description("Space at the left and right ends, in pixels."), Range(0, 400)] public int Padding { get; set; } = 12;
    [Category("Look"), Description("Space between modules, in pixels."), Range(0, 400)] public int Gap { get; set; } = 18;
    [Category("Modules"), DisplayName("Module background"), Description("A pill behind every module. none = no pills (a module's own Background wins)."), Color, TypeConverter(typeof(ColorNameConverter))] public string ModuleBackground { get; set; } = "none";
    [Category("Modules"), DisplayName("Module radius"), Description("Roundness of module pills (smooth style)."), Range(0, 100)] public float ModuleRadius { get; set; } = 8;
    [Category("Look"), DisplayName("Screen effects"), Description("Use the theme's glow, scanlines and grain on the bar too.")] public bool Effects { get; set; } = true;
    [Category("Look"), Description("A frame (Frames page) for the whole bar instead of the plain colour above. none = plain. Smooth style."), TypeConverter(typeof(FrameNameConverter))] public string Frame { get; set; } = "none";
    [Category("Modules"), DisplayName("Module frame"), Description("A frame drawn around every module (or group of joined modules). none = no frames. Smooth style."), TypeConverter(typeof(FrameNameConverter))] public string ModuleFrame { get; set; } = "none";
    [Category("Modules"), DisplayName("Module padding"), Description("Space inside a module frame, left and right, in pixels."), Range(0, 100)] public float ModulePadding { get; set; } = 8;
    [Category("Modules"), DisplayName("Module inset"), Description("Space between a module frame and the top and bottom of the bar, in pixels."), Range(0, 100)] public float ModuleInset { get; set; } = 3;
    [Category("Modules"), DisplayName("Joined gap"), Description("Space between modules that share a frame (Join), in pixels."), Range(0, 100)] public float JoinGap { get; set; } = 8;
    [Category("Modules"), DisplayName("Hover colour"), Description("The highlight under the mouse on modules that do something on click (without a frame)."), Color, TypeConverter(typeof(ColorNameConverter))] public string HoverColor { get; set; } = "accent";
}

// ---------------- modules ----------------

internal abstract class BarModuleOptions
{
    [Browsable(false)] public abstract string Type { get; }
    [Category("Module"), Description("Show this module.")] public bool Enabled { get; set; } = true;
    [Category("Module"), Description("Text colour (colour markup in the format overrides it)."), Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "text";
    [Category("Module"), Description("A pill behind this module. auto = the bar's Module background, none = no pill."), Color, TypeConverter(typeof(ColorNameConverter))] public string Background { get; set; } = "auto";
    [Category("Module"), Description("Bold text (smooth style).")] public bool Bold { get; set; }
    [Category("Module"), Description("A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style."), TypeConverter(typeof(FrameNameConverter))] public string Frame { get; set; } = "auto";
    [Category("Module"), Description("Share one frame with the next module (e.g. a label, a meter and a number in one box).")] public bool Join { get; set; }
    [Category("Module"), Description("A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style.")] public string Caption { get; set; } = "";
    [Category("Module"), DisplayName("Caption colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string CaptionColor { get; set; } = "dim";
    [Category("Module"), DisplayName("Caption spacing"), Description("Extra pixels between the caption's letters."), Range(-5, 50)] public float CaptionSpacing { get; set; } = 2;
    [Category("Module"), DisplayName("Caption below"), Description("Put the caption under the module instead of above it.")] public bool CaptionBelow { get; set; }
    [Category("Clicks"), DisplayName("On click"), Description(Glyph.Data.Actions.Help)] public string OnClick { get; set; } = "";
    [Category("Clicks"), DisplayName("On right-click")] public string OnRightClick { get; set; } = "";
    [Category("Clicks"), DisplayName("On scroll up")] public string OnScrollUp { get; set; } = "";
    [Category("Clicks"), DisplayName("On scroll down")] public string OnScrollDown { get; set; } = "";
    public override string ToString() => Type;
}

internal sealed class TextModule : BarModuleOptions
{
    public override string Type => "text";
    [Category("Text"), Description("What it shows: text with {values} like {time:HH:mm}, {cpu}, {media_title}, and [colour]…[/] markup.")] public string Format { get; set; } = "{time:HH:mm}";
    [Category("Text"), DisplayName("Max length"), Description("Cut longer text with …  (0 = no limit)."), Range(0, 1000)] public int MaxLength { get; set; }
    [Category("Text"), DisplayName("Hide when empty"), Description("Hide the module when all its values are empty (e.g. media with nothing playing).")] public bool HideWhenEmpty { get; set; } = true;
    [Category("Text"), Description("Text size (smooth style)."), Choices("small", "normal", "large"), TypeConverter(typeof(ChoicesConverter))] public string Size { get; set; } = "normal";
    [Category("Text"), DisplayName("Letter spacing"), Description("Smooth style: extra pixels between letters."), Range(-5, 50)] public float Spacing { get; set; }
    public override string ToString() => Format.Length > 40 ? Format[..40] + "…" : Format;
}

internal sealed class MeterModule : BarModuleOptions
{
    public override string Type => "meter";
    [Category("Meter"), Description("What it measures."), Choices("cpu", "ram", "disk", "battery", "volume", "media", "wifi"), TypeConverter(typeof(ChoicesConverter))] public string Value { get; set; } = "cpu";
    [Category("Meter"), Description("Text before the bar.")] public string Label { get; set; } = "cpu";
    [Category("Meter"), Description("Text after the bar (with {values}).")] public string Format { get; set; } = "{cpu:0}%";
    [Category("Meter"), Description("Length of the bar, in characters."), Range(2, 100)] public int Width { get; set; } = 8;
    [Category("Meter"), DisplayName("Fill colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string FillColor { get; set; } = "accent";
    [Category("Meter"), DisplayName("Meter characters"), Description("Terminal style: filled and empty characters, e.g. █░, ▮▯, ■□.")] public string Chars { get; set; } = "▮▯";
    [Category("Meter"), DisplayName("Blocks"), Description("Smooth style: draw the bar as this many separate blocks. 0 = one smooth bar."), Range(0, 100)] public int Segments { get; set; }
    [Category("Meter"), DisplayName("Bar height"), Description("Smooth style: thickness of the bar in pixels. 0 = from the font."), Range(0, 100)] public float BarHeight { get; set; }
    [Category("Meter"), DisplayName("Track colour"), Description("The empty part of the bar."), Color, TypeConverter(typeof(ColorNameConverter))] public string TrackColor { get; set; } = "line";
    [Category("Meter"), DisplayName("Label colour"), Description("auto = the module's colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string LabelColor { get; set; } = "auto";
    public override string ToString() => $"meter: {Value}";
}

internal sealed class GraphModule : BarModuleOptions
{
    public override string Type => "graph";
    [Category("Graph"), Description("What it plots over the last two minutes."), Choices("cpu", "ram", "net_down", "net_up"), TypeConverter(typeof(ChoicesConverter))] public string Value { get; set; } = "cpu";
    [Category("Graph"), Description("Width, in characters."), Range(2, 120)] public int Width { get; set; } = 12;
    [Category("Graph"), Description("Text before the graph.")] public string Label { get; set; } = "";
    public override string ToString() => $"graph: {Value}";
}

internal sealed class WorkspacesModule : BarModuleOptions
{
    public override string Type => "workspaces";
    [Category("Workspaces"), Description("numbers = 1 2 3, dots = ● ○ ○, names = the desktops' names."), Choices("numbers", "dots", "names"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "numbers";
    [Category("Workspaces"), DisplayName("Active colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string ActiveColor { get; set; } = "accent";
    [Category("Workspaces"), DisplayName("Inactive colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string InactiveColor { get; set; } = "dim";
    [Category("Workspaces"), DisplayName("Active text"), Description("Text colour of the current desktop's number. auto = the bar's colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string ActiveText { get; set; } = "auto";
    [Category("Workspaces"), Description("numbers style: show at least this many digits (2 = 01 02 03)."), Range(1, 4)] public int Digits { get; set; } = 1;
    [Category("Workspaces"), DisplayName("Show at least"), Description("Always show this many numbers; clicking one that doesn't exist yet makes the desktops up to it."), Range(1, 20)] public int MinCount { get; set; } = 1;
    [Category("Workspaces"), DisplayName("Active frame"), Description("Smooth style: a frame around the current desktop's number (instead of a pill). none = a pill."), TypeConverter(typeof(FrameNameConverter))] public string ActiveFrame { get; set; } = "none";
    [Category("Workspaces"), DisplayName("Inactive frame"), Description("Smooth style: a frame around the other numbers. none = no frame."), TypeConverter(typeof(FrameNameConverter))] public string InactiveFrame { get; set; } = "none";
    public override string ToString() => "workspaces";
}

internal sealed class IconsModule : BarModuleOptions
{
    public override string Type => "icons";
    [Category("Icons"), Description("Which shortcuts to show: their names, in order (as on the Shortcuts page). Empty = use Group instead."), TypeConverter(typeof(ListConverter))] public string[] Names { get; set; } = Array.Empty<string>();
    [Category("Icons"), Description("Or: the shortcuts in this group (Shortcuts page). Both empty = all of them.")] public string Group { get; set; } = "bar";
    [Category("Icons"), DisplayName("Icon size"), Description("In pixels. 0 = a bit bigger than the text."), Range(0, 128)] public float IconSize { get; set; }
    [Category("Icons"), DisplayName("Icon style"), Description("original = the app's own icon, tint = recoloured, mono = a flat silhouette in the module's colour."), Choices("original", "tint", "mono"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "original";
    [Category("Icons"), Description("Space between icons, in pixels."), Range(0, 100)] public float Spacing { get; set; } = 6;
    public override string ToString() => $"icons: {(Group.Length > 0 ? Group : "all shortcuts")}";
}

internal sealed class TasksModule : BarModuleOptions
{
    public override string Type => "tasks";
    [Category("Tasks"), Description("What each window shows: {title} = the window's title, {app} = the program's name. Empty = just the icon.")] public string Format { get; set; } = "{app}";
    [Category("Tasks"), DisplayName("Max length"), Description("Cut longer text with … (0 = no limit)."), Range(0, 200)] public int MaxLength { get; set; } = 18;
    [Category("Tasks"), DisplayName("Show icons")] public bool Icons { get; set; } = true;
    [Category("Tasks"), DisplayName("Icon size"), Description("In pixels. 0 = a bit bigger than the text."), Range(0, 128)] public float IconSize { get; set; }
    [Category("Tasks"), DisplayName("Icon style"), Description("original = the program's own icon, mono = a flat silhouette in the module's colour."), Choices("original", "tint", "mono"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "original";
    [Category("Tasks"), DisplayName("Task frame"), Description("A frame around each window's button. none = no frame."), TypeConverter(typeof(FrameNameConverter))] public string TaskFrame { get; set; } = "none";
    [Category("Tasks"), DisplayName("Active frame"), Description("A frame for the window in front. none = the same as Task frame."), TypeConverter(typeof(FrameNameConverter))] public string ActiveFrame { get; set; } = "none";
    [Category("Tasks"), DisplayName("Active colour"), Description("Text colour of the window in front."), Color, TypeConverter(typeof(ColorNameConverter))] public string ActiveColor { get; set; } = "auto";
    [Category("Tasks"), DisplayName("Minimised colour"), Description("Text colour of minimised windows."), Color, TypeConverter(typeof(ColorNameConverter))] public string MinimisedColor { get; set; } = "dim";
    [Category("Tasks"), DisplayName("Most shown"), Description("Show at most this many windows."), Range(1, 60)] public int MaxCount { get; set; } = 12;
    [Category("Tasks"), Description("Space between windows, in pixels."), Range(0, 100)] public float Spacing { get; set; } = 6;
    [Category("Tasks"), DisplayName("Group by program"), Description("One button per program instead of per window ({count} = how many windows). Clicking it again brings its next window to the front.")] public bool Combine { get; set; }
    [Category("Tasks"), DisplayName("Right-click"), Description("What right-clicking a window's button does."), Choices("none", "close", "minimise", "maximise"), TypeConverter(typeof(ChoicesConverter))] public string RightClick { get; set; } = "none";
    [Category("Tasks"), DisplayName("Leave out"), Description("Programs not to show: parts of their names (e.g. Settings)."), TypeConverter(typeof(ListConverter))] public string[] Hide { get; set; } = Array.Empty<string>();
    public override string ToString() => "tasks";
}

internal sealed class TrayModule : BarModuleOptions
{
    public override string Type => "tray";
    [Category("Tray"), DisplayName("Icon size"), Description("In pixels. 0 = a bit bigger than the text."), Range(0, 128)] public float IconSize { get; set; }
    [Category("Tray"), DisplayName("Icon style"), Description("original = the programs' own icons, tint = recoloured, mono = flat silhouettes in the module's colour."), Choices("original", "tint", "mono"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "original";
    [Category("Tray"), Description("Space between icons, in pixels."), Range(0, 100)] public float Spacing { get; set; } = 6;
    [Category("Tray"), DisplayName("Icon frame"), Description("A frame around each icon. none = no frame."), TypeConverter(typeof(FrameNameConverter))] public string IconFrame { get; set; } = "none";
    [Category("Tray"), DisplayName("Leave out"), Description("Icons not to show: parts of their program's name or tooltip (e.g. OneDrive)."), TypeConverter(typeof(ListConverter))] public string[] Hide { get; set; } = Array.Empty<string>();
    [Category("Tray"), DisplayName("Most shown"), Range(1, 60)] public int MaxCount { get; set; } = 16;
    [Category("Tray"), DisplayName("Show hidden"), Description("Also show icons their programs asked to hide.")] public bool ShowHidden { get; set; }
    public override string ToString() => "tray";
}

internal sealed class SpacerModule : BarModuleOptions
{
    public override string Type => "spacer";
    [Category("Spacer"), Description("Width, in characters."), Range(0, 200)] public int Width { get; set; } = 2;
    public override string ToString() => "spacer";
}
