using System.ComponentModel;
using System.Drawing.Design;
using System.Text;
using Glyph.Options;

namespace Glyph.Wallpaper;

using FileEditor = System.Windows.Forms.Design.FileNameEditor;

/// wallpaper.toml in a theme folder.
internal sealed class WallpaperOptions
{
    [Category("Wallpaper"), DisplayName("Frames per second"), Description("How smoothly it animates. Lower = less power. A wallpaper with nothing moving only redraws when needed."), Range(1, 240)]
    public int Fps { get; set; } = 30;

    [Section, Category("Wallpaper"), DisplayName("Backdrop"), Description("What's behind the layers: a colour, a gradient or a picture"), TypeConverter(typeof(SectionConverter))]
    public BackdropOptions Backdrop { get; set; } = new();

    [Section, Category("Wallpaper"), DisplayName("Mouse effect"), Description("The glow that follows the mouse"), TypeConverter(typeof(SectionConverter))]
    public CursorOptions Cursor { get; set; } = new();

    [Section, Category("Wallpaper"), DisplayName("Interaction"), Description("What happens when you click, double-click or scroll on empty desktop, plus click ripples and a mouse trail"), TypeConverter(typeof(SectionConverter))]
    public InteractionOptions Interaction { get; set; } = new();

    [Skip, Browsable(false)]
    public List<LayerOptions> Layers { get; set; } = new();

    public static readonly Dictionary<string, Func<LayerOptions>> Types = new()
    {
        ["noise"] = () => new NoiseOptions(), ["rain"] = () => new RainOptions(), ["stars"] = () => new StarsOptions(),
        ["static"] = () => new StaticOptions(), ["glitch"] = () => new GlitchOptions(), ["art"] = () => new ArtOptions(),
        ["clip"] = () => new ClipOptions(),
    };

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "wallpaper.toml");

    public static WallpaperOptions Load(Theme theme, List<string> problems)
    {
        var w = new WallpaperOptions();
        var file = ConfigFile.Load(FilePath(theme.Name), problems);
        var top = file.Top;
        Binder.Read(w, top, theme);
        foreach (var n in top.Array("layer"))
        {
            var type = n.Str("type", "", Types.Keys.ToArray());
            if (type.Length == 0)
            {
                if (!n.Has("type")) problems.Add($"{file.Name}: {n.Label} needs a type, e.g. type = \"noise\"");
                continue;
            }
            var o = Types[type]();
            Binder.Read(o, n.WithLabel($"{n.Label} ({type})"), theme);
            w.Layers.Add(o);
        }
        file.ReportUnknown();
        return w;
    }

    public void Save(string theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# What's drawn behind your desktop icons. Easiest to change in the settings window");
        sb.AppendLine("# (tray icon > Settings > Wallpaper); you can also edit this file.");
        sb.AppendLine("# The [[layer]] sections are drawn in order: later ones on top. Spaces in a layer let");
        sb.AppendLine("# the layers underneath show through.");
        sb.AppendLine();
        Binder.Write(sb, this, null);
        foreach (var l in Layers)
        {
            sb.AppendLine().AppendLine("[[layer]]");
            sb.AppendLine($"type = \"{l.Type}\"");
            Binder.WriteValues(sb, l);
        }
        SafeFile.Write(FilePath(theme), sb.ToString());
    }
}

internal sealed class BackdropOptions
{
    [Description("solid = one colour, gradient = a blend of colours, image = a picture."), Choices("solid", "gradient", "image"), TypeConverter(typeof(ChoicesConverter))]
    public string Type { get; set; } = "solid";
    [Description("The colour for solid (and behind a picture)."), Color, TypeConverter(typeof(ColorNameConverter))]
    public string Color { get; set; } = "bg";
    [Description("Gradient colours, first to last."), Color, TypeConverter(typeof(ListConverter))]
    public string[] Colors { get; set; } = { "bg", "surface" };
    [Description("Gradient direction in degrees: 0 = left to right, 90 = top to bottom."), Range(0, 360)]
    public float Angle { get; set; } = 90;
    [Description("Gradient shape."), Choices("linear", "radial"), TypeConverter(typeof(ChoicesConverter))]
    public string Shape { get; set; } = "linear";
    [Description("Picture file (PNG/JPG). A file name is looked for in the theme folder and config\\images."), Editor(typeof(FileEditor), typeof(UITypeEditor))]
    public string File { get; set; } = "";
    [Description("How the picture fills the screen."), Choices("cover", "contain", "stretch", "center"), TypeConverter(typeof(ChoicesConverter))]
    public string Fit { get; set; } = "cover";
    [Description("Darken the picture (0 = not at all, 1 = black)."), Range(0, 1)]
    public float Dim { get; set; }
}

internal sealed class InteractionOptions
{
    [Category("Actions"), DisplayName("On click"), Description("Clicking empty desktop. " + Glyph.Data.Actions.Help)] public string OnClick { get; set; } = "";
    [Category("Actions"), DisplayName("On double-click"), Description("Double-clicking empty desktop.")] public string OnDoubleClick { get; set; } = "";
    [Category("Actions"), DisplayName("On scroll up"), Description("Scrolling up over empty desktop, e.g. volume:up or desktop:prev.")] public string OnScrollUp { get; set; } = "";
    [Category("Actions"), DisplayName("On scroll down")] public string OnScrollDown { get; set; } = "";
    [Category("Ripple"), Description("A ring spreads out where you click.")] public bool Ripple { get; set; } = true;
    [Category("Ripple"), DisplayName("Ripple colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string RippleColor { get; set; } = "accent";
    [Category("Ripple"), DisplayName("Ripple size"), Description("How far it spreads, in pixels."), Range(10, 3000)] public float RippleSize { get; set; } = 220;
    [Category("Ripple"), DisplayName("Ripple time"), Description("Seconds it lasts."), Range(0.1, 10)] public float RippleDuration { get; set; } = 0.9f;
    [Category("Ripple"), DisplayName("Ripple characters"), Description("Terminal style: characters it's drawn with, from start to end.")] public string RippleChars { get; set; } = "#*+:·";
    [Category("Trail"), Description("The mouse leaves a fading trail on the desktop.")] public bool Trail { get; set; }
    [Category("Trail"), DisplayName("Trail colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TrailColor { get; set; } = "accent_dim";
    [Category("Trail"), DisplayName("Trail time"), Description("Seconds each mark lasts."), Range(0.05, 10)] public float TrailDuration { get; set; } = 0.6f;
    [Category("Trail"), DisplayName("Trail characters"), Description("Terminal style: newest to oldest.")] public string TrailChars { get; set; } = "*+:·.";
}

internal sealed class CursorOptions
{
    [Description("Turn the mouse effect on or off.")] public bool Enabled { get; set; } = true;
    [Description("Size of the area around the mouse, in characters."), Range(0.5, 200)] public float Radius { get; set; } = 8;
    [Description("How much characters near it take its colour."), Range(0, 1)] public float Strength { get; set; } = 0.6f;
    [Description("Tint of the background around it."), Range(0, 1)] public float Glow { get; set; } = 0.12f;
    [Description("Chance characters near it scramble."), Range(0, 1)] public float Scatter { get; set; }
    [DisplayName("Scatter characters"), Description("What they scramble into.")] public string ScatterChars { get; set; } = "#%&*+=-:.";
    [Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "accent_bright";
}

// ---------------- layers ----------------

internal abstract class LayerOptions
{
    [Browsable(false)] public abstract string Type { get; }
    [Category("Layer"), Description("Show this layer.")] public bool Enabled { get; set; } = true;
    [Category("Layer"), Description("Fades the layer's characters (0 to 1)."), Range(0, 1)] public float Opacity { get; set; } = 1;
    [Category("Layer"), DisplayName("Parallax X"), Description("The layer shifts this many characters against the mouse (left-right), for depth. 0 = still."), Range(-100, 100)] public float ParallaxX { get; set; }
    [Category("Layer"), DisplayName("Parallax Y"), Description("Same, up-down."), Range(-100, 100)] public float ParallaxY { get; set; }
    public override string ToString() => Type + (Enabled ? "" : " (off)");
}

internal sealed class NoiseOptions : LayerOptions
{
    public override string Type => "noise";
    [Category("Noise"), Description("Characters from faintest to strongest. Spaces = empty.")] public string Chars { get; set; } = " .:-=+*#%@";
    [Category("Noise"), Description("Colours from faint to strong."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "bg", "accent_dim", "accent", "bright" };
    [Category("Noise"), Description("Size of the shapes: smaller = bigger, softer blobs."), Range(0.0001, 10)] public float Scale { get; set; } = 0.05f;
    [Category("Noise"), Description("How fast the shapes change."), Range(-100, 100)] public float Speed { get; set; } = 0.1f;
    [Category("Noise"), DisplayName("Drift X"), Description("Steady sideways movement, characters per second."), Range(-100, 100)] public float DriftX { get; set; }
    [Category("Noise"), DisplayName("Drift Y"), Description("Steady up/down movement, characters per second."), Range(-100, 100)] public float DriftY { get; set; }
    [Category("Noise"), Description("Swirliness (0 = plain clouds)."), Range(0, 10)] public float Warp { get; set; } = 0.5f;
    [Category("Noise"), Description("Detail."), Range(1, 8)] public int Octaves { get; set; } = 3;
    [Category("Noise"), Range(0, 20)] public float Contrast { get; set; } = 1;
    [Category("Noise"), Range(-1, 1)] public float Brightness { get; set; }
    [Category("Noise"), Description("Below this it's empty. Raise it for sparse wisps."), Range(0, 0.99)] public float Threshold { get; set; }
    [Category("Noise"), Description("Swap faint and strong.")] public bool Invert { get; set; }
    [Category("Noise"), DisplayName("Update FPS"), Description("Times per second the smoke is worked out; frames in between blend. Lower = less CPU."), Range(0.5, 240)] public float UpdateFps { get; set; } = 6;
}

internal sealed class RainOptions : LayerOptions
{
    public override string Type => "rain";
    [Category("Rain")] public string Chars { get; set; } = "0123456789ABCDEF:;<>=+-*/";
    [Category("Rain"), Description("Streams per column (0.25 = 1 in 4 columns)."), Range(0, 20)] public float Density { get; set; } = 0.25f;
    [Category("Rain"), DisplayName("Speed min"), Description("Characters per second."), Range(0.1, 500)] public float SpeedMin { get; set; } = 8;
    [Category("Rain"), DisplayName("Speed max"), Range(0.1, 500)] public float SpeedMax { get; set; } = 20;
    [Category("Rain"), DisplayName("Length min"), Range(1, 500)] public int LengthMin { get; set; } = 6;
    [Category("Rain"), DisplayName("Length max"), Range(1, 500)] public int LengthMax { get; set; } = 24;
    [Category("Rain"), Description("Colour of the leading character."), Color, TypeConverter(typeof(ColorNameConverter))] public string Head { get; set; } = "bright";
    [Category("Rain"), Description("Tail end, then near the head."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "accent_dim", "accent" };
    [Category("Rain"), DisplayName("Change rate"), Description("How often characters flip, per second."), Range(0, 120)] public float ChangeRate { get; set; } = 4;
    [Category("Rain"), Choices("down", "up"), TypeConverter(typeof(ChoicesConverter))] public string Direction { get; set; } = "down";
    [Category("Rain"), Description("Tails fade out.")] public bool Fade { get; set; } = true;
}

internal sealed class StarsOptions : LayerOptions
{
    public override string Type => "stars";
    [Category("Stars"), Description("warp = fly through the stars, drift = they slide sideways."), Choices("warp", "drift"), TypeConverter(typeof(ChoicesConverter))] public string Mode { get; set; } = "warp";
    [Category("Stars"), Description("Drift mode direction."), Choices("left", "right", "up", "down"), TypeConverter(typeof(ChoicesConverter))] public string Direction { get; set; } = "left";
    [Category("Stars"), Range(0, 20000)] public int Count { get; set; } = 400;
    [Category("Stars"), Range(0, 50)] public float Speed { get; set; } = 0.3f;
    [Category("Stars"), Description("Far to near.")] public string Chars { get; set; } = ".·+*";
    [Category("Stars"), Description("Far to near."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "line", "dim", "text", "bright" };
}

internal sealed class StaticOptions : LayerOptions
{
    public override string Type => "static";
    [Category("Static"), Description("Share of cells with static."), Range(0, 1)] public float Density { get; set; } = 0.02f;
    [Category("Static"), Description("Changes per second."), Range(0.1, 240)] public float Rate { get; set; } = 12;
    [Category("Static")] public string Chars { get; set; } = ".:'`,-";
    [Category("Static"), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "line", "dim" };
}

internal sealed class GlitchOptions : LayerOptions
{
    public override string Type => "glitch";
    [Category("Glitch"), Description("Glitches per second."), Range(0, 100)] public float Rate { get; set; } = 0.3f;
    [Category("Glitch"), Description("Seconds each lasts."), Range(0.01, 10)] public float Duration { get; set; } = 0.15f;
    [Category("Glitch"), DisplayName("Max shift"), Description("How far rows jump, in characters."), Range(0, 500)] public int MaxShift { get; set; } = 6;
    [Category("Glitch"), DisplayName("Band min"), Description("Rows per glitch."), Range(1, 500)] public int BandMin { get; set; } = 1;
    [Category("Glitch"), DisplayName("Band max"), Range(1, 500)] public int BandMax { get; set; } = 6;
    [Category("Glitch"), Description("Chance characters turn into the glitch characters."), Range(0, 1)] public float Scramble { get; set; } = 0.3f;
    [Category("Glitch")] public string Chars { get; set; } = "#%&@$!?/\\|<>";
    [Category("Glitch"), Description("How much glitched characters take the colour."), Range(0, 1)] public float Tint { get; set; } = 0.5f;
    [Category("Glitch"), Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "accent_bright";
}

internal sealed class ArtOptions : LayerOptions
{
    public override string Type => "art";
    [Category("Art"), Description("A text file with ASCII art (looked for in the theme folder and config\\art)."), Editor(typeof(FileEditor), typeof(UITypeEditor))] public string File { get; set; } = "";
    [Category("Art"), Choices("top_left", "top", "top_right", "left", "center", "right", "bottom_left", "bottom", "bottom_right"), TypeConverter(typeof(ChoicesConverter))] public string Anchor { get; set; } = "center";
    [Category("Art"), DisplayName("Offset X"), Description("Move it, in characters (negative = left).")] public int OffsetX { get; set; }
    [Category("Art"), DisplayName("Offset Y"), Description("Move it, in characters (negative = up).")] public int OffsetY { get; set; }
    [Category("Art"), Description("Colour (if no gradient)."), Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "accent";
    [Category("Art"), Description("Optional gradient across the art. Empty = use Color."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = Array.Empty<string>();
    [Category("Art"), DisplayName("Gradient direction"), Choices("vertical", "horizontal", "diagonal"), TypeConverter(typeof(ChoicesConverter))] public string GradientDirection { get; set; } = "vertical";
    [Category("Art"), DisplayName("Transparent spaces"), Description("Spaces in the art let layers below show.")] public bool TransparentSpaces { get; set; } = true;
    [Category("Art"), Description("Fill behind the art, e.g. bg to blank out what's under it."), Color, TypeConverter(typeof(ColorNameConverter))] public string Background { get; set; } = "none";
    [Category("Art"), Description("Extra blanked space around it (rows; twice as many columns)."), Range(0, 50)] public int Padding { get; set; } = 1;
    [Category("Art"), Description("Gentle breathing."), Range(0, 1)] public float Pulse { get; set; }
    [Category("Art"), DisplayName("Pulse speed"), Description("Breaths per second."), Range(0, 50)] public float PulseSpeed { get; set; } = 1;
    [Category("Art"), Description("Lines jump sideways, times per second."), Range(0, 50)] public float Glitch { get; set; }
}

internal sealed class ClipOptions : LayerOptions
{
    public override string Type => "clip";
    [Category("Clip"), Description("A .gclip made by the converter (looked for in the theme folder and config\\clips)."), Editor(typeof(FileEditor), typeof(UITypeEditor))] public string File { get; set; } = "";
    [Category("Clip"), Description("cover fills the screen (may crop), contain shows all of it, none = actual size."), Choices("contain", "cover", "stretch", "none"), TypeConverter(typeof(ChoicesConverter))] public string Fit { get; set; } = "cover";
    [Category("Clip"), Description("Size multiplier."), Range(0.01, 10)] public float Scale { get; set; } = 1;
    [Category("Clip"), Choices("top_left", "top", "top_right", "left", "center", "right", "bottom_left", "bottom", "bottom_right"), TypeConverter(typeof(ChoicesConverter))] public string Anchor { get; set; } = "center";
    [Category("Clip"), DisplayName("Offset X")] public int OffsetX { get; set; }
    [Category("Clip"), DisplayName("Offset Y")] public int OffsetY { get; set; }
    [Category("Clip"), Description("Playback speed."), Range(0, 20)] public float Speed { get; set; } = 1;
    [Category("Clip"), Description("Off = play once and stop on the last frame.")] public bool Loop { get; set; } = true;
    [Category("Clip"), Description("gradient = theme colours by brightness, original = the video's colours, single = one colour."), Choices("gradient", "original", "single"), TypeConverter(typeof(ChoicesConverter))] public string Colors { get; set; } = "gradient";
    [Category("Clip"), Description("Dark to bright."), Color, TypeConverter(typeof(ListConverter))] public string[] Gradient { get; set; } = { "bg", "accent_dim", "accent", "bright" };
    [Category("Clip"), Description("For colors = single."), Color, TypeConverter(typeof(ColorNameConverter))] public string Color { get; set; } = "accent";
    [Category("Clip"), Description("Cells darker than this are empty, so layers below show."), Range(0, 1)] public float Threshold { get; set; }
    [Category("Clip"), Range(0, 10)] public float Brightness { get; set; } = 1;
    [Category("Clip"), Description("If set, re-picks every character by brightness from this list (faintest first).")] public string Chars { get; set; } = "";
}
