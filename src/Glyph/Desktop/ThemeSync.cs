using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Glyph.Options;
using Microsoft.Win32;

namespace Glyph;

/// [sync] in theme.toml: what the theme also changes outside Glyph.
internal sealed class SyncOptions
{
    [DisplayName("Windows Terminal"), Description("Give Windows Terminal this theme's colours: a colour scheme called \"Glyph\" that every profile uses, and matching tabs. Turning it off puts Terminal back as it was.")]
    public bool Terminal { get; set; } = true;
    [DisplayName("Terminal background"), Color, TypeConverter(typeof(ColorNameConverter))] public string TerminalBackground { get; set; } = "bg";
    [DisplayName("Terminal text"), Color, TypeConverter(typeof(ColorNameConverter))] public string TerminalText { get; set; } = "text";
    [DisplayName("Terminal cursor"), Color, TypeConverter(typeof(ColorNameConverter))] public string TerminalCursor { get; set; } = "accent";
    [DisplayName("Terminal selection"), Description("Background of selected text."), Color, TypeConverter(typeof(ColorNameConverter))] public string TerminalSelection { get; set; } = "accent_dim";
    [DisplayName("Terminal tab bar"), Description("The strip behind the tabs (the open tab takes the terminal's background)."), Color, TypeConverter(typeof(ColorNameConverter))] public string TerminalTabBar { get; set; } = "surface";
    [DisplayName("Terminal font"), Description("Empty = leave Terminal's font alone. theme = this theme's character font. Or any installed monospace font.")]
    public string TerminalFont { get; set; } = "";
    [DisplayName("Terminal font size"), Description("In points. 0 = leave it alone."), Range(0, 72)] public float TerminalFontSize { get; set; }
    [DisplayName("Terminal opacity"), Description("100 = solid. Lower lets the desktop show through."), Range(0, 100)] public int TerminalOpacity { get; set; } = 100;
    [DisplayName("Terminal blur"), Description("Blur what shows through (acrylic). Needs opacity under 100.")] public bool TerminalBlur { get; set; }
    [DisplayName("Terminal CRT effect"), Description("off = none. retro = Windows Terminal's own glow and scanlines. effects = this theme's screen effects (scanlines, vignette, curvature, colour fringing, brightness, contrast) while they're on; still, so it costs nothing when nothing changes."),
     Choices("off", "retro", "effects"), TypeConverter(typeof(ChoicesConverter))]
    public string TerminalCrt { get; set; } = "effects";
    [DisplayName("Terminal cursor shape"), Choices("keep", "bar", "vintage", "underscore", "doubleUnderscore", "filledBox", "emptyBox"), TypeConverter(typeof(ChoicesConverter))]
    public string TerminalCursorShape { get; set; } = "keep";

    [DisplayName("Windows accent colour"), Description("Set Windows' accent colour (Start, toggles, buttons, links, focus outlines) from this theme. Turning it off puts the old one back.")]
    public bool Accent { get; set; } = true;
    [DisplayName("Accent colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentColor { get; set; } = "accent";
    [DisplayName("Accent shades"), Description("Windows uses lighter and darker shades of the accent too. theme = made from Accent light and Accent dark below. windows = Windows works them out (it may change them for readability)."),
     Choices("theme", "windows"), TypeConverter(typeof(ChoicesConverter))]
    public string AccentShades { get; set; } = "theme";
    [DisplayName("Accent light"), Description("The lightest shade (dark mode shows these on toggles and links)."), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentLight { get; set; } = "accent_bright";
    [DisplayName("Accent dark"), Description("The darkest shade."), Color, TypeConverter(typeof(ColorNameConverter))] public string AccentDark { get; set; } = "accent_dim";
    [DisplayName("Accent on Start and taskbar"), Description("keep = leave Windows' setting. Windows only allows it in dark mode."), Choices("keep", "on", "off"), TypeConverter(typeof(ChoicesConverter))]
    public string AccentOnTaskbar { get; set; } = "keep";
    [DisplayName("Accent on title bars"), Description("Colour window title bars and borders with the accent. keep = leave Windows' setting."), Choices("keep", "on", "off"), TypeConverter(typeof(ChoicesConverter))]
    public string AccentOnTitleBars { get; set; } = "keep";

    [DisplayName("Dark mode"), Description("auto = dark if this theme's background is dark. keep = leave Windows' setting. Sets both apps and Windows itself."),
     Choices("auto", "dark", "light", "keep"), TypeConverter(typeof(ChoicesConverter))]
    public string DarkMode { get; set; } = "auto";
}

/// Carries a theme over to Windows Terminal and Windows itself (accent colour, dark mode).
/// Before changing anything it notes the old value in logs\sync.json, so each part can be put back
/// exactly when it's turned off (in the theme or with theme_sync in glyph.toml). Things are only
/// written when what the theme asks for changes, so edits you make in Terminal afterwards stay.
internal static class ThemeSync
{
    static readonly object Gate = new();
    static string StatePath => Path.Combine(Paths.Logs, "sync.json");
    static string ShaderPath => Path.Combine(Paths.Logs, "terminal", "glyph-crt.hlsl");
    const string Name = "Glyph";

    /// Runs off the UI thread (Windows can take a moment to repaint everything after a colour change).
    public static void Apply(Theme? t, bool enabled) => Task.Run(() =>
    {
        lock (Gate)
        {
            try { Run(enabled ? t : null); }
            catch (Exception e) { Log.Write("theme sync: " + e.Message); }
        }
    });

    sealed class State
    {
        public Dictionary<string, string> Applied { get; set; } = new();
        /// part -> key -> the value before Glyph changed it ("" = there wasn't one)
        public Dictionary<string, Dictionary<string, string>> Orig { get; set; } = new();
    }

    static State Load()
    {
        try { if (File.Exists(StatePath)) return JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new(); }
        catch (Exception e) { Log.Write("theme sync: can't read sync.json: " + e.Message); }
        return new();
    }

    static void Save(State s)
    {
        Directory.CreateDirectory(Paths.Logs);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
    }

    static void Run(Theme? t)
    {
        var s = Load();
        var o = t?.Options.Sync;
        bool changedWindows = false;

        // Windows Terminal: one part per settings file (stable, preview, unpackaged)
        foreach (var file in TerminalFiles())
        {
            string part = "terminal|" + file;
            if (o != null && o.Terminal) { var want = TerminalWanted(t!, o); Step(s, part, want, () => WriteTerminal(file, want, s.Orig.GetValueOrDefault(part))); }
            else Undo(s, part, () => RestoreTerminal(file, s.Orig[part]));
        }

        // Windows: accent colour
        if (o != null && o.Accent)
        {
            var want = AccentWanted(t!, o);
            Step(s, "accent", want, () => { WriteAccent(want, s); changedWindows = true; });
        }
        else Undo(s, "accent", () => { RestoreRegistry(s.Orig["accent"], true); changedWindows = true; });

        // Windows: dark mode
        string mode = o == null ? "keep" : o.DarkMode == "auto" ? (t![o.TerminalBackground].Luma < 0.5f ? "dark" : "light") : o.DarkMode;
        if (mode is "dark" or "light")
        {
            int light = mode == "light" ? 1 : 0;
            var want = new Dictionary<string, string> { [Personalize + "|AppsUseLightTheme"] = "d:" + light, [Personalize + "|SystemUsesLightTheme"] = "d:" + light };
            Step(s, "dark", want, () => { WriteRegistry(want, Orig(s, "dark")); changedWindows = true; Log.Write("theme sync: Windows " + mode + " mode"); });
        }
        else Undo(s, "dark", () => { RestoreRegistry(s.Orig["dark"], false); changedWindows = true; });

        Save(s);
        if (changedWindows) Broadcast();
    }

    static Dictionary<string, string> Orig(State s, string part)
    {
        if (!s.Orig.TryGetValue(part, out var d)) s.Orig[part] = d = new();
        return d;
    }

    static string Hash(Dictionary<string, string> want) => string.Join("\n", want.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value));

    /// Applies a part if what's wanted changed since it was last applied.
    static void Step(State s, string part, Dictionary<string, string> want, Action apply)
    {
        var h = Hash(want);
        if (s.Applied.TryGetValue(part, out var old) && old == h) return;
        Orig(s, part);
        try { apply(); s.Applied[part] = h; }
        catch (Exception e) { Log.Write($"theme sync: {part}: {e.Message}"); }
    }

    static void Undo(State s, string part, Action restore)
    {
        if (!s.Orig.ContainsKey(part)) return;
        try { restore(); } catch (Exception e) { Log.Write($"theme sync: putting {part} back: {e.Message}"); }
        s.Orig.Remove(part); s.Applied.Remove(part);
    }

    // ───────────── Windows Terminal ─────────────

    static IEnumerable<string> TerminalFiles()
    {
        var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var f in new[]
        {
            Path.Combine(la, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(la, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(la, "Microsoft", "Windows Terminal", "settings.json"),
        })
            if (File.Exists(f)) yield return f;
    }

    static readonly (string glyph, string wt)[] TermKeys =
    {
        ("black", "black"), ("red", "red"), ("green", "green"), ("yellow", "yellow"), ("blue", "blue"), ("magenta", "purple"), ("cyan", "cyan"), ("white", "white"),
        ("bright_black", "brightBlack"), ("bright_red", "brightRed"), ("bright_green", "brightGreen"), ("bright_yellow", "brightYellow"),
        ("bright_blue", "brightBlue"), ("bright_magenta", "brightPurple"), ("bright_cyan", "brightCyan"), ("bright_white", "brightWhite"),
    };

    // if a theme has no [terminal] colour for a slot, take the nearest palette colour
    static readonly Dictionary<string, string> TermFallback = new()
    {
        ["black"] = "bg", ["red"] = "accent", ["white"] = "text", ["bright_black"] = "dim", ["bright_red"] = "accent_bright", ["bright_white"] = "bright",
    };

    static string Hex(Rgba c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// Everything Glyph sets in Terminal's settings, as JSON path -> JSON value.
    static Dictionary<string, string> TerminalWanted(Theme t, SyncOptions o)
    {
        var scheme = new JsonObject { ["name"] = Name };
        scheme["background"] = Hex(t[o.TerminalBackground]);
        scheme["foreground"] = Hex(t[o.TerminalText]);
        scheme["cursorColor"] = Hex(t[o.TerminalCursor]);
        scheme["selectionBackground"] = Hex(t[o.TerminalSelection]);
        foreach (var (g, wt) in TermKeys)
            scheme[wt] = Hex(t.Terminal.TryGetValue(g, out var c) ? c : t[TermFallback.GetValueOrDefault(g, "text")]);

        bool dark = t[o.TerminalBackground].Luma < 0.5f;
        var wtTheme = new JsonObject
        {
            ["name"] = Name,
            ["window"] = new JsonObject { ["applicationTheme"] = dark ? "dark" : "light" },
            ["tab"] = new JsonObject { ["background"] = "terminalBackground", ["unfocusedBackground"] = "terminalBackground", ["showCloseButton"] = "hover" },
            ["tabRow"] = new JsonObject { ["background"] = Hex(t[o.TerminalTabBar]), ["unfocusedBackground"] = Hex(t[o.TerminalTabBar]) },
        };

        var w = new Dictionary<string, string>
        {
            ["schemes[Glyph]"] = scheme.ToJsonString(),
            ["themes[Glyph]"] = wtTheme.ToJsonString(),
            ["theme"] = JsonValue.Create(Name).ToJsonString(),
            ["profiles/defaults/colorScheme"] = JsonValue.Create(Name).ToJsonString(),
            ["profiles/defaults/opacity"] = o.TerminalOpacity.ToString(),
            ["profiles/defaults/useAcrylic"] = o.TerminalBlur ? "true" : "false",
        };
        var font = o.TerminalFont.Trim();
        if (font.Equals("theme", StringComparison.OrdinalIgnoreCase)) font = t.Options.Font.Font;
        if (font.Length > 0) w["profiles/defaults/font/face"] = JsonValue.Create(font).ToJsonString();
        if (o.TerminalFontSize > 0) w["profiles/defaults/font/size"] = o.TerminalFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (o.TerminalCursorShape != "keep") w["profiles/defaults/cursorShape"] = JsonValue.Create(o.TerminalCursorShape).ToJsonString();
        if (o.TerminalCrt == "retro") w["profiles/defaults/experimental.retroTerminalEffect"] = "true";
        else if (o.TerminalCrt == "effects" && t.Options.Effects.Enabled && Shader(t.Options.Effects) is string hlsl)
        {
            w["profiles/defaults/experimental.pixelShaderPath"] = JsonValue.Create(ShaderPath).ToJsonString();
            w["shader"] = JsonValue.Create(hlsl).ToJsonString();   // not a settings key: makes a changed shader count as a change
        }
        return w;
    }

    static readonly JsonDocumentOptions ReadOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static JsonObject ReadSettings(string file) => JsonNode.Parse(File.ReadAllText(file), null, ReadOpts) as JsonObject ?? throw new InvalidDataException("not a JSON object");

    static void WriteSettings(string file, JsonObject root, string before)
    {
        var text = root.ToJsonString(WriteOpts);
        if (text == before) return;
        var backup = Path.Combine(Path.GetDirectoryName(file)!, "settings.before-glyph.json");
        if (!File.Exists(backup)) File.Copy(file, backup);
        var tmp = file + ".glyph-tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, file, true);   // Terminal notices and reloads by itself
    }

    static void WriteTerminal(string file, Dictionary<string, string> want, Dictionary<string, string>? orig)
    {
        orig ??= new();
        var before = File.ReadAllText(file);
        var root = ReadSettings(file);
        if (want.TryGetValue("shader", out var hlsl))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ShaderPath)!);
            File.WriteAllText(ShaderPath, JsonNode.Parse(hlsl)!.GetValue<string>());
        }
        // keys set last time but not wanted now go back to how they were
        foreach (var k in orig.Keys.Where(k => !want.ContainsKey(k)).ToList()) { Put(root, k, orig[k]); orig.Remove(k); }
        foreach (var (k, v) in want)
        {
            if (k == "shader") continue;
            if (!orig.ContainsKey(k)) orig[k] = Get(root, k);
            Put(root, k, v);
        }
        WriteSettings(file, root, before);
        Log.Write("theme sync: Windows Terminal colours set (" + file + ")");
    }

    static void RestoreTerminal(string file, Dictionary<string, string> orig)
    {
        var before = File.ReadAllText(file);
        var root = ReadSettings(file);
        foreach (var (k, v) in orig) Put(root, k, v);
        WriteSettings(file, root, before);
        Log.Write("theme sync: Windows Terminal put back as it was");
    }

    /// Paths are split by / ("profiles/defaults/font/face"; Terminal keys may hold dots, e.g. "experimental.pixelShaderPath") or name an entry of a list ("schemes[Glyph]").
    static (JsonObject? parent, JsonArray? list, string key) Locate(JsonObject root, string path, bool create)
    {
        int b = path.IndexOf('[');
        if (b > 0)
        {
            string listName = path[..b], entry = path[(b + 1)..^1];
            if (root[listName] is not JsonArray arr) { if (!create) return (null, null, entry); root[listName] = arr = new JsonArray(); }
            return (null, arr, entry);
        }
        var parts = path.Split('/');
        JsonObject cur = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var n = cur[parts[i]];
            if (n is JsonArray profileList && parts[i] == "profiles")
            {
                // the old format: "profiles": [ ... ] -> { "defaults": {}, "list": [ ... ] }
                if (!create) return (null, null, "");
                cur[parts[i]] = null;
                var obj = new JsonObject { ["defaults"] = new JsonObject(), ["list"] = profileList };
                cur[parts[i]] = obj; cur = obj; continue;
            }
            if (n is not JsonObject next) { if (!create) return (null, null, ""); cur[parts[i]] = next = new JsonObject(); }
            cur = next;
        }
        return (cur, null, parts[^1]);
    }

    static string Get(JsonObject root, string path)
    {
        var (parent, list, key) = Locate(root, path, false);
        if (list != null) return list.FirstOrDefault(e => e is JsonObject o && (string?)o["name"] == key)?.ToJsonString() ?? "";
        return parent?[key]?.ToJsonString() ?? "";
    }

    /// value "" = remove it
    static void Put(JsonObject root, string path, string value)
    {
        var (parent, list, key) = Locate(root, path, value.Length > 0);
        if (list != null)
        {
            int i = list.ToList().FindIndex(e => e is JsonObject o && (string?)o["name"] == key);
            if (i >= 0) list.RemoveAt(i);
            if (value.Length > 0) { var node = JsonNode.Parse(value); if (i >= 0) list.Insert(i, node); else list.Add(node); }
            return;
        }
        if (parent == null) return;
        if (value.Length == 0) parent.Remove(key);
        else parent[key] = JsonNode.Parse(value);
    }

    /// Windows Terminal pixel shader made from the theme's screen effects. Doesn't use Time, so
    /// Terminal only redraws when its text changes. null when none of its effects are on.
    static string? Shader(EffectSettings e)
    {
        static string F(float v) => v.ToString("0.0####", System.Globalization.CultureInfo.InvariantCulture);
        bool any = e.Scanlines > 0 || e.Vignette > 0 || e.Curvature > 0 || e.Aberration > 0 || Math.Abs(e.Brightness - 1) > 0.001f || Math.Abs(e.Contrast - 1) > 0.001f;
        if (!any) return null;
        return $$"""
// Made by Glyph from the theme's screen effects (theme.toml [effects]). Rewritten when the theme changes.
Texture2D shaderTexture;
SamplerState samplerState;
cbuffer PixelShaderSettings { float Time; float Scale; float2 Resolution; float4 Background; };

static const float Scan = {{F(e.Scanlines)}};
static const float ScanSize = {{F(Math.Max(1, e.ScanlineSize))}};
static const float Vignette = {{F(e.Vignette)}};
static const float Aberration = {{F(e.Aberration)}};
static const float Curvature = {{F(e.Curvature)}};
static const float Brightness = {{F(e.Brightness)}};
static const float Contrast = {{F(e.Contrast)}};

float4 main(float4 pos : SV_POSITION, float2 tex : TEXCOORD) : SV_TARGET
{
    float2 uv = tex;
    if (Curvature > 0)
    {
        float2 cc = uv * 2 - 1;
        cc *= 1 + Curvature * 0.25 * dot(cc, cc);
        uv = cc * 0.5 + 0.5;
        if (any(uv < 0) || any(uv > 1)) return float4(Background.rgb, 1);
    }
    float4 c = shaderTexture.Sample(samplerState, uv);
    float2 off = Aberration * Scale / Resolution * (uv - 0.5) * 2;
    float3 col = float3(shaderTexture.Sample(samplerState, uv + off).r, c.g, shaderTexture.Sample(samplerState, uv - off).b);

    float s = sin(pos.y * 6.2831853 / (ScanSize * Scale)) * 0.5 + 0.5;
    col *= 1 - Scan * (1 - s);
    float2 d = uv - 0.5;
    col *= saturate(1 - Vignette * dot(d, d) * 2.2);
    col = (col - 0.5) * Contrast + 0.5;
    col *= Brightness;
    return float4(saturate(col), c.a);
}
""";
    }

    // ───────────── Windows: accent colour and dark mode ─────────────

    const string Accent = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    const string Dwm = @"Software\Microsoft\Windows\DWM";
    const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    static uint Abgr(Rgba c) => 0xFF000000u | ((uint)c.B << 16) | ((uint)c.G << 8) | c.R;

    static Dictionary<string, string> AccentWanted(Theme t, SyncOptions o)
    {
        Rgba a = t[o.AccentColor], light = t[o.AccentLight], dark = t[o.AccentDark], white = new(255, 255, 255);
        var w = new Dictionary<string, string> { ["accent"] = Hex(a) };
        if (o.AccentShades == "theme")
        {
            // Windows' order: three lighter shades, the accent, three darker, then a grey it barely uses
            var shades = new[]
            {
                Rgba.Lerp(light, white, 0.4f), light, Rgba.Lerp(a, light, 0.5f), a,
                Rgba.Lerp(a, dark, 0.35f), Rgba.Lerp(a, dark, 0.7f), dark, new Rgba(0x69, 0x79, 0x7E),
            };
            w[Accent + "|AccentPalette"] = "b:" + System.Convert.ToHexString(shades.SelectMany(c => new[] { c.R, c.G, c.B, (byte)0 }).ToArray());
            w[Accent + "|StartColorMenu"] = "d:" + unchecked((int)Abgr(shades[4]));
        }
        if (o.AccentOnTaskbar != "keep") w[Personalize + "|ColorPrevalence"] = "d:" + (o.AccentOnTaskbar == "on" ? 1 : 0);
        if (o.AccentOnTitleBars != "keep") w[Dwm + "|ColorPrevalence"] = "d:" + (o.AccentOnTitleBars == "on" ? 1 : 0);
        return w;
    }

    // every value the accent colour touches, so turning it off puts all of them back
    static readonly string[] AccentValues =
    {
        Accent + "|AccentPalette", Accent + "|AccentColorMenu", Accent + "|StartColorMenu",
        Dwm + "|AccentColor", Dwm + "|ColorizationColor", Dwm + "|ColorizationAfterglow",
    };

    static void WriteAccent(Dictionary<string, string> want, State s)
    {
        var orig = Orig(s, "accent");
        foreach (var k in AccentValues) if (!orig.ContainsKey(k)) orig[k] = ReadReg(k);
        Rgba.TryParseHex(want["accent"], out var a);
        // this is what the Settings app calls: it sets the accent everywhere and works out a palette
        var pref = new ImmersiveColorPreference { Start = Abgr(Rgba.Lerp(a, new Rgba(0, 0, 0), 0.35f)), Accent = Abgr(a) };
        int hr = SetUserColorPreference(ref pref, true);
        if (hr != 0) Log.Write($"theme sync: SetUserColorPreference failed (0x{hr:X8})");
        // then our own shades and taskbar / title bar choices over the top
        var rest = want.Where(k => k.Key != "accent").ToDictionary(k => k.Key, k => k.Value);
        // values that were set last time but aren't wanted now (e.g. title bars back to keep) go back
        foreach (var k in orig.Keys.Where(k => !AccentValues.Contains(k) && !rest.ContainsKey(k)).ToList()) { WriteReg(k, orig[k]); orig.Remove(k); }
        WriteRegistry(rest, orig);
        Log.Write("theme sync: Windows accent colour " + want["accent"]);
    }

    static void WriteRegistry(Dictionary<string, string> want, Dictionary<string, string> orig)
    {
        foreach (var (k, v) in want)
        {
            if (!orig.ContainsKey(k)) orig[k] = ReadReg(k);
            WriteReg(k, v);
        }
    }

    static void RestoreRegistry(Dictionary<string, string> orig, bool accent)
    {
        if (accent && orig.GetValueOrDefault(Accent + "|AccentColorMenu") is string m && m.StartsWith("d:"))
        {
            // let Windows switch the accent back first (so every app hears it), then restore the exact values
            uint acc = unchecked((uint)int.Parse(m[2..]));
            uint start = orig.GetValueOrDefault(Accent + "|StartColorMenu") is string st && st.StartsWith("d:") ? unchecked((uint)int.Parse(st[2..])) : acc;
            var pref = new ImmersiveColorPreference { Start = start, Accent = acc };
            SetUserColorPreference(ref pref, true);
        }
        foreach (var (k, v) in orig) WriteReg(k, v);
        Log.Write(accent ? "theme sync: Windows accent colour put back" : "theme sync: Windows light/dark mode put back");
    }

    /// "d:123" a DWORD, "b:ABCD" binary, "" = there was no value
    static string ReadReg(string kv)
    {
        var (key, name) = Split(kv);
        using var k = Registry.CurrentUser.OpenSubKey(key);
        return k?.GetValue(name) switch
        {
            int i => "d:" + i,
            byte[] b => "b:" + System.Convert.ToHexString(b),
            _ => "",
        };
    }

    static void WriteReg(string kv, string v)
    {
        var (key, name) = Split(kv);
        using var k = Registry.CurrentUser.CreateSubKey(key);
        if (v.Length == 0) k.DeleteValue(name, false);
        else if (v.StartsWith("d:")) k.SetValue(name, int.Parse(v[2..]), RegistryValueKind.DWord);
        else if (v.StartsWith("b:")) k.SetValue(name, System.Convert.FromHexString(v[2..]), RegistryValueKind.Binary);
    }

    static (string, string) Split(string kv) { int i = kv.LastIndexOf('|'); return (kv[..i], kv[(i + 1)..]); }

    /// Tells running programs (Explorer, Start, apps) that the colours changed.
    static void Broadcast()
    {
        SendMessageTimeout((IntPtr)0xFFFF, 0x001A /* WM_SETTINGCHANGE */, IntPtr.Zero, "ImmersiveColorSet", 2 /* SMTO_ABORTIFHUNG */, 1000, out _);
    }

    [StructLayout(LayoutKind.Sequential)] struct ImmersiveColorPreference { public uint Start, Accent; }
    [DllImport("uxtheme.dll", EntryPoint = "#122")] static extern int SetUserColorPreference(ref ImmersiveColorPreference p, bool forceCommit);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
}
