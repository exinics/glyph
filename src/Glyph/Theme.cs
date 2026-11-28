using System.ComponentModel;
using System.Globalization;
using System.Text;
using Glyph.Options;

namespace Glyph;

/// A colour. Packed as 0xAABBGGRR so the shader can unpack it byte by byte.
internal readonly struct Rgba
{
    public readonly byte R, G, B, A;
    public Rgba(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }
    public uint Packed => (uint)(R | (G << 8) | (B << 16) | (A << 24));
    public static Rgba FromPacked(uint p) => new((byte)p, (byte)(p >> 8), (byte)(p >> 16), (byte)(p >> 24));

    public static Rgba Lerp(Rgba a, Rgba b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
    }

    public Rgba WithAlpha(float a) => new(R, G, B, (byte)Math.Clamp(a * 255f, 0f, 255f));
    public float Luma => (0.2126f * R + 0.7152f * G + 0.0722f * B) / 255f;

    public static bool TryParseHex(string s, out Rgba c)
    {
        c = default;
        s = s.Trim();
        if (s.Length is not (7 or 9) || s[0] != '#') return false;
        if (!uint.TryParse(s.AsSpan(1), NumberStyles.HexNumber, null, out var v)) return false;
        if (s.Length == 7) c = new Rgba((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        else c = new Rgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public string Hex => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

/// Packed-colour helpers used in the hot loops.
internal static class Px
{
    public static uint Lerp(uint a, uint b, float t)
    {
        if (t <= 0) return a;
        if (t >= 1) return b;
        int it = (int)(t * 256);
        uint r = (uint)(((int)(a & 255) * (256 - it) + (int)(b & 255) * it) >> 8);
        uint g = (uint)(((int)((a >> 8) & 255) * (256 - it) + (int)((b >> 8) & 255) * it) >> 8);
        uint bl = (uint)(((int)((a >> 16) & 255) * (256 - it) + (int)((b >> 16) & 255) * it) >> 8);
        uint al = (uint)(((int)(a >> 24) * (256 - it) + (int)(b >> 24) * it) >> 8);
        return r | (g << 8) | (bl << 16) | (al << 24);
    }

    public static uint Alpha(uint c, float a)
    {
        int na = (int)(255 * Math.Clamp(a, 0f, 1f));
        return (c & 0x00FFFFFF) | ((uint)na << 24);
    }

    /// Straight-alpha "over": paints `top` (with its alpha) over `under`.
    public static uint Over(uint under, uint top)
    {
        float ta = (top >> 24) / 255f;
        if (ta >= 1f) return top;
        if (ta <= 0f) return under;
        float ua = (under >> 24) / 255f;
        float oa = ta + ua * (1 - ta);
        if (oa <= 0) return 0;
        float k = ta / oa;
        uint rgb = Lerp(under | 0xFF000000, top | 0xFF000000, k) & 0x00FFFFFF;
        return rgb | ((uint)(oa * 255f + 0.5f) << 24);
    }
}

/// A colour ramp, sampled 0..1. Precomputed into 256 entries.
internal sealed class Gradient
{
    public readonly uint[] Lut = new uint[256];
    public readonly List<Rgba> Stops;

    public Gradient(List<Rgba> stops)
    {
        Stops = stops.Count == 0 ? new List<Rgba> { new(255, 255, 255) } : stops;
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            if (Stops.Count == 1) { Lut[i] = Stops[0].Packed; continue; }
            float p = t * (Stops.Count - 1);
            int k = Math.Min((int)p, Stops.Count - 2);
            Lut[i] = Rgba.Lerp(Stops[k], Stops[k + 1], p - k).Packed;
        }
    }

    public Gradient(Theme theme, IEnumerable<string> names) : this(names.Select(n => theme[n]).ToList()) { }

    public uint At(float t) => Lut[(int)(Math.Clamp(t, 0f, 1f) * 255f)];
}

// ---------------- theme options (theme.toml) ----------------

internal sealed class ThemeOptions
{
    [Section, Category("Theme"), DisplayName("About"), Description("What this theme is"), TypeConverter(typeof(SectionConverter))]
    public InfoOptions Info { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Character font"), Description("The font for characters: ASCII wallpaper and terminal-style widgets"), TypeConverter(typeof(SectionConverter))]
    public FontOptions Font { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Smooth style"), Description("How panels and text look in the smooth style"), TypeConverter(typeof(SectionConverter))]
    public UiOptions Ui { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Screen effects"), Description("Effects over the whole screen (glow, scanlines…)"), TypeConverter(typeof(SectionConverter))]
    public EffectSettings Effects { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Theme sync"), Description("What this theme also changes outside Glyph: Windows Terminal's colours and Windows' accent colour and dark mode"), TypeConverter(typeof(SectionConverter))]
    public SyncOptions Sync { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Launcher look"), Description("How the launcher (Alt+Space) looks in this theme"), TypeConverter(typeof(SectionConverter))]
    public Glyph.Launcher.LauncherLook Launcher { get; set; } = new();

    [Section, Category("Theme"), DisplayName("Window frames"), Description("Frames (Frames page) drawn around your windows, e.g. a title tab and a border in the theme's colours"), TypeConverter(typeof(SectionConverter))]
    public Glyph.Desktop.WindowFrameOptions Windows { get; set; } = new();
}

internal sealed class InfoOptions
{
    [Description("A short note about the theme.")]
    public string Description { get; set; } = "";

    [Description("terminal = everything drawn as characters. smooth = rounded panels, normal fonts and pictures."), Choices("terminal", "smooth"), TypeConverter(typeof(ChoicesConverter))]
    public string Style { get; set; } = "terminal";
}

internal sealed class FontOptions
{
    [Description("Any installed monospace font, e.g. Cascadia Mono, JetBrains Mono, Consolas (or one in config\\fonts: the Fonts page)."), TypeConverter(typeof(FontNameConverter))]
    public string Font { get; set; } = "Cascadia Mono";
    [DisplayName("Font size"), Description("In pixels. Smaller = more, finer characters."), Range(6, 96)] public float FontSize { get; set; } = 14;
    [DisplayName("Font weight"), Choices("thin", "light", "normal", "medium", "semibold", "bold", "black"), TypeConverter(typeof(ChoicesConverter))] public string FontWeight { get; set; } = "normal";
    [DisplayName("Line spacing"), Description("1.0 = normal."), Range(0.5, 3)] public float LineSpacing { get; set; } = 1f;
    [DisplayName("Cell width"), Description("Force a cell width in pixels. 0 = from the font."), Range(0, 200)] public int CellWidth { get; set; }
    [DisplayName("Cell height"), Description("Force a cell height in pixels. 0 = from the font."), Range(0, 400)] public int CellHeight { get; set; }

    [Browsable(false)] public string Key => $"{Font}|{FontSize}|{FontWeight}|{CellWidth}|{CellHeight}|{LineSpacing}|{Glyph.Render.Symbols.LookKey}";
}

internal sealed class UiOptions
{
    [Description("Font for text in the smooth style: any installed font, or one in config\\fonts (the Fonts page)."), TypeConverter(typeof(FontNameConverter))]
    public string Font { get; set; } = "Segoe UI";
    [DisplayName("Font size"), Description("In pixels."), Range(6, 96)] public float FontSize { get; set; } = 13;
    [DisplayName("Bold titles"), Description("Panel titles in bold.")] public bool BoldTitles { get; set; } = true;
    [DisplayName("Icon style"), Description("Look of icons written as {icon:name} anywhere (Google's Material Symbols; browse them on the Icons page). Rounded and sharp are downloaded once, the first time they're used."), Choices("outlined", "rounded", "sharp"), TypeConverter(typeof(ChoicesConverter))] public string IconStyle { get; set; } = "outlined";
    [DisplayName("Filled icons"), Description("Solid icons instead of outlines.")] public bool IconFill { get; set; }
    [DisplayName("Icon size"), Description("Size of icons next to the text they're in: 1 = the text's size."), Range(0.5, 3)] public float IconSize { get; set; } = 1.15f;
    [DisplayName("Corner radius"), Description("Roundness of panel corners, in pixels."), Range(0, 64)] public float CornerRadius { get; set; } = 12;
    [DisplayName("Panel colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string PanelColor { get; set; } = "surface";
    [DisplayName("Panel opacity"), Description("0 = invisible, 1 = solid."), Range(0, 1)] public float PanelOpacity { get; set; } = 0.85f;
    [DisplayName("Border colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string BorderColor { get; set; } = "line";
    [DisplayName("Border width"), Description("In pixels. 0 = no border."), Range(0, 16)] public float BorderWidth { get; set; } = 1;
    [Description("Soft shadow under panels."), Range(0, 1)] public float Shadow { get; set; } = 0.35f;
    [DisplayName("Text colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TextColor { get; set; } = "text";
    [DisplayName("Accent colour"), Description("Hover highlights, titles."), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentColor { get; set; } = "accent";
    [Description("Space inside panels, in pixels."), Range(0, 100)] public float Padding { get; set; } = 12;
    [DisplayName("Panel frame"), Description("A frame (Frames page) for every panel whose Frame is auto. none = plain panels."), TypeConverter(typeof(FrameNameConverter))] public string PanelFrame { get; set; } = "none";
    [DisplayName("Button frame"), Description("A frame for buttons in panels (e.g. media controls). none = plain buttons."), TypeConverter(typeof(FrameNameConverter))] public string ButtonFrame { get; set; } = "none";
    [DisplayName("Halo"), Description("Smooth style: a soft blur of this colour behind every widget's text and shapes, so light text stays readable on a light wallpaper (e.g. black). none = no halo. Each widget can change it (Glow & halo)."), Color, TypeConverter(typeof(ColorNameConverter))] public string Halo { get; set; } = "none";
    [DisplayName("Halo size"), Description("How far the halo spreads, in pixels."), Range(0.5, 60)] public float HaloSize { get; set; } = 6;
    [DisplayName("Halo strength"), Description("How dark (strong) it is: 0 = none, 1 = full, up to 2 = a second, tighter layer for very light wallpapers."), Range(0, 2)] public float HaloStrength { get; set; } = 0.7f;
    [DisplayName("Glow"), Description("Smooth style: things light up like neon or an old screen. self = each thing glows in its own colours, or a colour = everything glows in that colour. none = no glow. Each widget can change it (Glow & halo)."), TypeConverter(typeof(GlowNameConverter))] public string Glow { get; set; } = "none";
    [DisplayName("Glow size"), Description("How far the glow spreads, in pixels."), Range(0.5, 80)] public float GlowSize { get; set; } = 8;
    [DisplayName("Glow strength"), Description("How bright the glow is (up to 3 = blinding)."), Range(0, 3)] public float GlowStrength { get; set; } = 0.8f;
}

/// The active theme: named colours (the palette), the 16 terminal colours and the theme's options.
/// Hex codes live only in the theme file; everything else refers to these names.
internal sealed class Theme
{
    public readonly string Name;
    public readonly List<(string name, Rgba color)> PaletteList = new();
    public readonly Dictionary<string, Rgba> Palette = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<(string name, Rgba color)> TerminalList = new();
    public readonly Dictionary<string, Rgba> Terminal = new(StringComparer.OrdinalIgnoreCase);
    public ThemeOptions Options = new();
    public List<Glyph.Render.FrameOptions> Frames = new();

    /// A frame by name; null for "none", "" or an unknown name. "auto" gives `auto` instead.
    public Glyph.Render.FrameOptions? Frame(string name, string auto = "none")
    {
        if (name == "auto") name = auto;
        if (name.Length == 0 || name == "none" || name == "auto") return null;
        foreach (var f in Frames) if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
        return null;
    }
    public bool Smooth => Options.Info.Style == "smooth";
    public string Dir => Paths.ThemeDir(Name);

    public static readonly string[] TerminalNames =
    {
        "black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
        "bright_black", "bright_red", "bright_green", "bright_yellow", "bright_blue", "bright_magenta", "bright_cyan", "bright_white"
    };

    // Used only if the theme file is missing a name (so a broken theme still shows something).
    static readonly (string, string)[] Fallback =
    {
        ("bg", "#0A0A0A"), ("surface", "#141414"), ("line", "#2A2A2A"), ("dim", "#8A8A8A"), ("text", "#E6E6E6"),
        ("bright", "#FFFFFF"), ("accent", "#E0202F"), ("accent_dim", "#6E0E16"), ("accent_bright", "#FF4D57"),
    };

    public Theme(string name) { Name = name; }

    public static string FilePath(string name) => Path.Combine(Paths.ThemeDir(name), "theme.toml");

    /// True if any of the theme's frames has a spotlight (then the desktop redraws as the mouse moves over widgets).
    public bool AnySpotlight => Frames.Any(f => f.Spotlight != "none");
    /// How far outside a box a spotlight still lights it (FramePainter draws it within 0.6 × its size of the box).
    public float SpotlightReach => Frames.Where(f => f.Spotlight != "none").Select(f => f.SpotlightSize * 0.6f).DefaultIfEmpty(0).Max();

    public static Theme Load(string name, List<string> problems)
    {
        var t = new Theme(name);
        var path = FilePath(name);
        var file = ConfigFile.Load(path, problems);
        var top = file.Top;
        var pal = top.Sub("palette", "[palette]");
        if (pal != null)
            foreach (var kv in pal.Table)
            {
                pal.Touch(kv.Key);
                if (kv.Value is string s && Rgba.TryParseHex(s, out var c)) t.SetColor(kv.Key, c);
                else problems.Add($"{file.Name}: [palette] {kv.Key} should be a hex code like \"#E0202F\"");
            }
        foreach (var (n, hex) in Fallback)
            if (!t.Palette.ContainsKey(n))
            {
                Rgba.TryParseHex(hex, out var c); t.SetColor(n, c);
                if (File.Exists(path)) problems.Add($"{file.Name}: [palette] is missing '{n}' (using {hex})");
            }
        var term = top.Sub("terminal", "[terminal]");
        if (term != null)
            foreach (var kv in term.Table)
            {
                term.Touch(kv.Key);
                if (kv.Value is string s && (Rgba.TryParseHex(s, out var c) || t.TryResolve(s, out c))) { t.Terminal[kv.Key] = c; t.TerminalList.Add((kv.Key, c)); }
                else problems.Add($"{file.Name}: [terminal] {kv.Key} should be a hex code or palette name");
            }
        Binder.Read(t.Options, top, t);
        file.ReportUnknown();
        try { t.Frames = Glyph.Render.FramesFile.Load(t, problems).Frames; } catch (Exception e) { problems.Add("frames.toml: " + e.Message); }
        return t;
    }

    public void SetColor(string name, Rgba c)
    {
        Palette[name] = c;
        int i = PaletteList.FindIndex(p => p.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (i >= 0) PaletteList[i] = (name, c); else PaletteList.Add((name, c));
    }

    public void RemoveColor(string name)
    {
        Palette.Remove(name);
        PaletteList.RemoveAll(p => p.name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void Save()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Theme \"{Name}\". Easiest to change in the settings window (tray icon > Settings);");
        sb.AppendLine("# you can also edit this file. Saved changes apply straight away.");
        sb.AppendLine("#");
        sb.AppendLine("# [palette] is the ONLY place colours are defined. Everything else refers to them by");
        sb.AppendLine("# name (e.g. \"accent\"), so changing a hex code here recolours everything.");
        sb.AppendLine();
        sb.AppendLine("[palette]");
        int w = PaletteList.Count == 0 ? 4 : PaletteList.Max(p => p.name.Length);
        foreach (var (n, c) in PaletteList) sb.AppendLine($"{n.PadRight(w)} = \"{c.Hex}\"");
        if (TerminalList.Count > 0)
        {
            sb.AppendLine().AppendLine("# The 16 colours terminal programs use.").AppendLine("[terminal]");
            int tw = TerminalList.Max(p => p.name.Length);
            foreach (var (n, c) in TerminalList) sb.AppendLine($"{n.PadRight(tw)} = \"{c.Hex}\"");
        }
        Binder.Write(sb, Options, null);
        SafeFile.Write(FilePath(Name), sb.ToString());
    }

    /// Accepts a palette name ("accent"), a hex code ("#E0202F" or "#E0202F80"), or "none" (fully transparent).
    public bool TryResolve(string s, out Rgba c)
    {
        s = s.Trim();
        if (s.Equals("none", StringComparison.OrdinalIgnoreCase) || s.Equals("auto", StringComparison.OrdinalIgnoreCase)) { c = new Rgba(0, 0, 0, 0); return true; }
        if (Palette.TryGetValue(s, out c)) return true;
        if (Terminal.TryGetValue(s, out c)) return true;
        return Rgba.TryParseHex(s, out c);
    }

    public Rgba this[string name] => TryResolve(name, out var c) ? c : new Rgba(255, 0, 255);
}
