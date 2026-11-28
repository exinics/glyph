using System.Runtime.InteropServices;
using System.Text;
using Vortice.DirectWrite;

namespace Glyph.Render;

/// Glyph's fonts: Windows' own, plus every font file in config\fonts (drop any .ttf/.otf there, e.g. from
/// fonts.google.com) and bin\fonts (the icon font that comes with Glyph). Nothing is installed into Windows:
/// the files are only used by Glyph.
internal static class Fonts
{
    static readonly object Gate = new();
    static readonly Dictionary<IntPtr, IDWriteFontCollection> collections = new();
    static string signature = "";

    /// Goes up whenever the font files change (text and character caches compare it).
    public static int Version { get; private set; }

    public static string UserDir => Path.Combine(Paths.Config, "fonts");
    public static string BuiltInDir => Path.Combine(AppContext.BaseDirectory, "fonts");

    static readonly string[] Kinds = { ".ttf", ".otf", ".ttc", ".otc" };

    public static List<string> Files()
    {
        var res = new List<string>();
        foreach (var d in new[] { BuiltInDir, UserDir })
            try { if (Directory.Exists(d)) res.AddRange(Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Where(f => Kinds.Contains(Path.GetExtension(f).ToLowerInvariant()))); }
            catch { }
        return res;
    }

    /// Checks the font folders; if a file was added, changed or removed, the next text drawn uses the new set.
    public static void Refresh()
    {
        var sig = string.Join("|", Files().Select(f => { try { var i = new FileInfo(f); return $"{f}:{i.Length}:{i.LastWriteTimeUtc.Ticks}"; } catch { return f; } }));
        lock (Gate)
        {
            if (sig == signature) return;
            bool first = signature.Length == 0 && Version == 0;
            signature = sig;
            foreach (var c in collections.Values) c.Dispose();
            collections.Clear();
            if (!first) Version++;
        }
    }

    /// The font collection for a DirectWrite factory (made once per factory).
    public static IDWriteFontCollection Collection(IDWriteFactory dw)
    {
        lock (Gate)
        {
            if (signature.Length == 0) Refresh();   // first use (the lock can be taken again by the same thread)
            if (collections.TryGetValue(dw.NativePointer, out var have)) return have;
            IDWriteFontCollection made;
            try
            {
                using var f5 = dw.QueryInterface<IDWriteFactory5>();
                using var b = f5.CreateFontSetBuilder();
                using (var sys = f5.SystemFontSet) b.AddFontSet(sys);
                foreach (var file in Files())
                {
                    try { using var ff = dw.CreateFontFileReference(file, null); b.AddFontFile(ff); }
                    catch (Exception e) { Log.Write($"font {Path.GetFileName(file)}: can't be used ({e.Message})"); }
                }
                using var set = b.CreateFontSet();
                made = f5.CreateFontCollectionFromFontSet(set);
            }
            catch (Exception e)
            {
                Log.Write("fonts: " + e.Message + " (using Windows' fonts only)");
                made = dw.GetSystemFontCollection(false);
            }
            collections[dw.NativePointer] = made;
            return made;
        }
    }

    /// Call when a DirectWrite factory is freed.
    public static void Forget(IDWriteFactory dw)
    {
        lock (Gate) if (collections.Remove(dw.NativePointer, out var c)) c.Dispose();
    }

    public static bool Has(IDWriteFactory dw, string family) => Collection(dw).FindFamilyName(family, out _);

    static readonly (string word, FontWeight weight)[] WeightWords =
    {
        ("ExtraLight", FontWeight.ExtraLight), ("Extra Light", FontWeight.ExtraLight), ("UltraLight", FontWeight.ExtraLight), ("Thin", FontWeight.Thin),
        ("Light", FontWeight.Light), ("Regular", FontWeight.Normal), ("Medium", FontWeight.Medium), ("SemiBold", FontWeight.SemiBold), ("Semi Bold", FontWeight.SemiBold),
        ("DemiBold", FontWeight.SemiBold), ("ExtraBold", FontWeight.ExtraBold), ("Extra Bold", FontWeight.ExtraBold), ("UltraBold", FontWeight.ExtraBold),
        ("Bold", FontWeight.Bold), ("Black", FontWeight.Black), ("Heavy", FontWeight.Heavy),
    };

    /// A font name as a family and a weight. Newer fonts (e.g. Google Fonts' static files) put every weight in one
    /// family ("Rubik"), so a name like "Rubik Black" becomes family "Rubik", weight Black. null = use the default.
    public static (string family, FontWeight? weight) Resolve(IDWriteFactory dw, string name)
    {
        if (name.Length == 0 || Has(dw, name)) return (name, null);
        foreach (var (word, weight) in WeightWords)
            if (name.EndsWith(" " + word, StringComparison.OrdinalIgnoreCase))
            {
                var fam = name[..^(word.Length + 1)].Trim();
                if (Has(dw, fam)) return (fam, weight);
            }
        return (name, null);
    }

    /// Every font family Glyph can use, A to Z (for the settings window's font lists).
    public static List<string> FamilyNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var dw = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Isolated);
            var coll = Collection(dw);
            for (uint i = 0; i < coll.FontFamilyCount; i++)
            {
                using var fam = coll.GetFontFamily(i);
                using var n = fam.FamilyNames;
                var s = Str(n, FindIndex(n));
                if (s.Length > 0) names.Add(s);
            }
            Forget(dw);
        }
        catch (Exception e) { Log.Write("font list: " + e.Message); }
        return names.ToList();
    }

    static uint FindIndex(IDWriteLocalizedStrings n) => n.FindLocaleName("en-us", out uint i) ? i : 0;

    static string Str(IDWriteLocalizedStrings n, uint i) => n.GetString(i) ?? "";
}

/// Icons from Google's Material Symbols (fonts.google.com/icons). Write {icon:name} in any text (bar
/// modules, widget text, titles, buttons): e.g. {icon:home}, {icon:wifi}, {icon:play_arrow}. Names are the
/// ones on fonts.google.com/icons. They're drawn from the icon font, sized and coloured like the text.
/// The theme's [ui] picks the style (outlined, rounded, sharp), filled or not, and the size.
internal static class Symbols
{
    public static readonly string[] Styles = { "outlined", "rounded", "sharp" };

    static readonly object Gate = new();
    static Dictionary<string, int>? byName;
    static HashSet<int>? all;
    static List<(string Name, int Cp)>? list;

    /// The look icons are drawn with (from the theme loaded last).
    public static volatile string Style = "outlined";
    public static volatile bool Fill;
    public static float Scale = 1;
    public static string LookKey => $"{FamilyNow}|{Fill}|{Scale}|{Fonts.Version}";

    static void Load()
    {
        if (byName != null) return;
        lock (Gate)
        {
            if (byName != null) return;
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var set = new HashSet<int>(); var l = new List<(string, int)>();
            try
            {
                foreach (var line in File.ReadLines(Path.Combine(Fonts.BuiltInDir, "MaterialSymbols.codepoints")))
                {
                    var p = line.Split(' ');
                    if (p.Length < 2 || !int.TryParse(p[1], System.Globalization.NumberStyles.HexNumber, null, out int cp)) continue;
                    if (d.TryAdd(p[0], cp)) l.Add((p[0], cp));
                    set.Add(cp);
                }
            }
            catch (Exception e) { Log.Write("icons: can't read the icon names (" + e.Message + ")"); }
            all = set; list = l; byName = d;
        }
    }

    public static IReadOnlyList<(string Name, int Cp)> All { get { Load(); return list!; } }

    public static int? Find(string name) { Load(); return byName!.TryGetValue(name.Trim().Replace(' ', '_').Replace('-', '_'), out int cp) ? cp : null; }

    /// The icon's character, or null if there's no icon with that name.
    public static string? Text(string name) => Find(name) is int cp ? char.ConvertFromUtf32(cp) : null;

    public static bool IsIcon(int cp)
    {
        if (cp < 0xE000 || (cp > 0xF8FF && cp < 0xF0000)) return false;
        Load();
        return all!.Contains(cp);
    }

    /// Replaces {icon:name} with the icon (for text that doesn't go through the format strings).
    public static string Expand(string s)
    {
        if (!s.Contains("{icon:", StringComparison.OrdinalIgnoreCase)) return s;
        var sb = new StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            int at = s.IndexOf("{icon:", i, StringComparison.OrdinalIgnoreCase);
            int end = at < 0 ? -1 : s.IndexOf('}', at);
            if (at < 0 || end < 0) { sb.Append(s, i, s.Length - i); break; }
            sb.Append(s, i, at - i);
            var t = Text(s.Substring(at + 6, end - at - 6));
            sb.Append(t ?? s.Substring(at, end - at + 1));
            i = end + 1;
        }
        return sb.ToString();
    }

    public static bool HasIcon(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 0xE000 && c <= 0xF8FF && IsIcon(c)) return true;
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && IsIcon(char.ConvertToUtf32(c, s[i + 1]))) return true;
        }
        return false;
    }

    public static string FamilyOf(string style) => "Material Symbols " + char.ToUpperInvariant(style[0]) + style[1..];
    public static string FileOf(string style) => "MaterialSymbols" + char.ToUpperInvariant(style[0]) + style[1..] + ".ttf";

    /// The family in use: the theme's style if its font is here, otherwise outlined (which comes with Glyph).
    public static string FamilyNow
    {
        get
        {
            var key = Style + "|" + Fonts.Version;
            if (key != familyFor)
            {
                family = File.Exists(Path.Combine(Fonts.BuiltInDir, FileOf(Style))) || File.Exists(Path.Combine(Fonts.UserDir, FileOf(Style))) ? FamilyOf(Style) : FamilyOf("outlined");
                familyFor = key;
            }
            return family;
        }
    }
    static string family = "Material Symbols Outlined", familyFor = "";

    static readonly HashSet<string> fetching = new();

    /// Rounded and sharp don't come with Glyph (10-15 MB each): the first theme that asks for one downloads it
    /// from Google's icon project into config\fonts, once. Until then icons show outlined.
    public static void EnsureStyle(string style)
    {
        if (!Styles.Contains(style) || style == "outlined") return;
        var target = Path.Combine(Fonts.UserDir, FileOf(style));
        if (File.Exists(target) || File.Exists(Path.Combine(Fonts.BuiltInDir, FileOf(style)))) return;
        lock (fetching) if (!fetching.Add(style)) return;
        _ = Task.Run(async () =>
        {
            var part = target + ".part";
            try
            {
                Directory.CreateDirectory(Fonts.UserDir);
                var url = $"https://raw.githubusercontent.com/google/material-design-icons/master/variablefont/MaterialSymbols{char.ToUpperInvariant(style[0]) + style[1..]}%5BFILL,GRAD,opsz,wght%5D.ttf";
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Glyph");
                using (var src = await http.GetStreamAsync(url))
                using (var dst = File.Create(part)) await src.CopyToAsync(dst);
                File.Move(part, target, true);
                Log.Write($"icons: downloaded the {style} icon style");
            }
            catch (Exception e) { Log.Write($"icons: couldn't download the {style} style ({e.Message}); showing outlined"); try { File.Delete(part); } catch { } }
            finally { lock (fetching) fetching.Remove(style); }
        });
    }

    const uint FILL = 'F' | ('I' << 8) | ('L' << 16) | ('L' << 24);

    /// Makes the icon characters in a text layout use the icon font (sized `size` × Scale, filled if set).
    public static void Mark(IDWriteTextLayout layout, string s, float size, IDWriteFontCollection coll)
    {
        var family = FamilyNow;
        using var l4 = layout.QueryInterfaceOrNull<IDWriteTextLayout4>();
        var axes = new[] { new FontAxisValue { AxisTag = (FontAxisTag)FILL, Value = Fill ? 1 : 0 } };
        int i = 0;
        while (i < s.Length)
        {
            int start = i;
            while (i < s.Length && IconAt(s, i, out int n)) i += n;
            if (i > start)
            {
                var r = new TextRange((uint)start, (uint)(i - start));
                layout.SetFontCollection(coll, r);
                layout.SetFontFamilyName(family, r);
                if (Math.Abs(Scale - 1) > 0.001f) layout.SetFontSize(size * Scale, r);
                l4?.SetFontAxisValues(axes, 1, r);
            }
            else i++;
        }
    }

    static bool IconAt(string s, int i, out int n)
    {
        char c = s[i]; n = 1;
        if (char.IsHighSurrogate(c) && i + 1 < s.Length) { n = 2; return IsIcon(char.ConvertToUtf32(c, s[i + 1])); }
        return IsIcon(c);
    }

    /// A text format for drawing one icon on its own (the terminal style's character cells).
    public static IDWriteTextFormat Format(IDWriteFactory dw, float size)
    {
        var f = dw.CreateTextFormat(FamilyNow, Fonts.Collection(dw), FontWeight.Normal, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, size, "en-us");
        using var f3 = f.QueryInterfaceOrNull<IDWriteTextFormat3>();
        f3?.SetFontAxisValues(new[] { new FontAxisValue { AxisTag = (FontAxisTag)FILL, Value = Fill ? 1 : 0 } }, 1);
        return f;
    }
}
