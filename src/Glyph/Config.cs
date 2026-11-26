using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;

namespace Glyph;

/// One loaded .toml file. Reading values through Node records which keys were used, so
/// typos ("speeed = 3") can be reported instead of silently ignored.
internal sealed class ConfigFile
{
    public readonly string Path;
    public readonly string Name;
    public readonly TomlTable Root;
    public readonly List<string> Problems;
    readonly string[] lines;
    readonly HashSet<string> used = new();

    ConfigFile(string path, TomlTable root, string[] lines, List<string> problems)
    {
        Path = path; Name = System.IO.Path.GetFileName(path); Root = root; this.lines = lines; Problems = problems;
    }

    /// Loads a file. If it is missing or has a syntax error, an empty table is used and the problem is recorded.
    public static ConfigFile Load(string path, List<string> problems)
    {
        var name = System.IO.Path.GetFileName(path);
        if (!File.Exists(path))
        {
            problems.Add($"{name}: file not found ({path})");
            return new ConfigFile(path, new TomlTable(), Array.Empty<string>(), problems);
        }
        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception e) { problems.Add($"{name}: can't read file ({e.Message})"); return new ConfigFile(path, new TomlTable(), Array.Empty<string>(), problems); }

        var doc = Toml.Parse(text, path);
        if (doc.HasErrors)
        {
            foreach (var d in doc.Diagnostics.Take(4))
                problems.Add($"{name} line {d.Span.Start.Line + 1}: {d.Message}");
            return new ConfigFile(path, new TomlTable(), text.Split('\n'), problems);
        }
        return new ConfigFile(path, doc.ToModel(), text.Split('\n'), problems);
    }

    public Node Top => new(this, Root, "", "");

    internal void MarkUsed(string fullKey) => used.Add(fullKey);

    /// Best-effort line number for a key, to point people at the right place.
    internal string Where(string key)
    {
        var re = new Regex(@"^\s*""?" + Regex.Escape(key) + @"""?\s*=");
        for (int i = 0; i < lines.Length; i++) if (re.IsMatch(lines[i])) return $" line {i + 1}";
        return "";
    }

    /// Reports keys that were never read (usually typos). Call after everything is read.
    public void ReportUnknown()
    {
        Walk(Root, "");
        void Walk(TomlTable t, string prefix)
        {
            foreach (var kv in t)
            {
                var full = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
                if (kv.Value is TomlTable sub) { if (used.Contains(full)) Walk(sub, full); else Problems.Add($"{Name}{Where(kv.Key)}: unknown section [{full}]"); }
                else if (kv.Value is TomlTableArray arr)
                {
                    if (!used.Contains(full)) { Problems.Add($"{Name}: unknown section [[{full}]]"); continue; }
                    for (int i = 0; i < arr.Count; i++) Walk(arr[i], $"{full}#{i + 1}");
                }
                else if (!used.Contains(full)) Problems.Add($"{Name}{Where(kv.Key)}: unknown option '{kv.Key}'" + (prefix.Length > 0 ? $" in {Pretty(prefix)}" : "") + " (typo?)");
            }
        }
    }

    static string Pretty(string p)
    {
        int h = p.LastIndexOf('#');
        return h > 0 ? $"[[{p[..h]}]] #{p[(h + 1)..]}" : $"[{p}]";
    }
}

/// A table inside a config file, with typed getters that fall back to defaults and record problems.
internal sealed class Node
{
    readonly ConfigFile file;
    public readonly TomlTable Table;
    readonly string prefix;   // key path used for "used" tracking
    public readonly string Label; // human label, e.g. "[[layer]] #2 (rain)"

    public Node(ConfigFile file, TomlTable table, string prefix, string label)
    {
        this.file = file; Table = table; this.prefix = prefix; Label = label;
    }

    string Full(string key) => prefix.Length == 0 ? key : prefix + "." + key;
    string Ctx(string key) => $"{file.Name}{file.Where(key)}: '{key}'" + (Label.Length > 0 ? $" in {Label}" : "");

    public bool Has(string key) => Table.ContainsKey(key);
    public List<string> Problems => file.Problems;
    public string FileName => file.Name;

    object? Raw(string key)
    {
        file.MarkUsed(Full(key));
        return Table.TryGetValue(key, out var v) ? v : null;
    }

    public double Num(string key, double def, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
    {
        var v = Raw(key);
        if (v == null) return def;
        double d;
        if (v is long l) d = l; else if (v is double dd) d = dd;
        else { Problems.Add($"{Ctx(key)} should be a number, got {Describe(v)}"); return def; }
        if (d < min || d > max) { Problems.Add($"{Ctx(key)} = {d} is out of range ({Range(min, max)}); using {Math.Clamp(d, min, max)}"); d = Math.Clamp(d, min, max); }
        return d;
    }

    public float F(string key, double def, double min = double.NegativeInfinity, double max = double.PositiveInfinity) => (float)Num(key, def, min, max);
    public int Int(string key, int def, int min = int.MinValue, int max = int.MaxValue) => (int)Math.Round(Num(key, def, min, max));

    public bool Bool(string key, bool def)
    {
        var v = Raw(key);
        if (v == null) return def;
        if (v is bool b) return b;
        Problems.Add($"{Ctx(key)} should be true or false, got {Describe(v)}");
        return def;
    }

    public string Str(string key, string def, params string[] allowed)
    {
        var v = Raw(key);
        if (v == null) return def;
        if (v is not string s) { Problems.Add($"{Ctx(key)} should be text in quotes, got {Describe(v)}"); return def; }
        if (allowed.Length > 0 && !allowed.Contains(s, StringComparer.OrdinalIgnoreCase))
        {
            Problems.Add($"{Ctx(key)} = \"{s}\" isn't one of: {string.Join(", ", allowed)}");
            return def;
        }
        return allowed.Length > 0 ? allowed.First(a => a.Equals(s, StringComparison.OrdinalIgnoreCase)) : s;
    }

    public List<string> StrList(string key, IEnumerable<string> def)
    {
        var v = Raw(key);
        if (v == null) return def.ToList();
        if (v is string one) return new List<string> { one };
        if (v is TomlArray arr && arr.All(x => x is string)) return arr.Cast<string>().ToList();
        Problems.Add($"{Ctx(key)} should be a list of text, like [\"a\", \"b\"]");
        return def.ToList();
    }

    public Rgba Color(string key, string def, Theme theme)
    {
        var s = Str(key, def);
        if (theme.TryResolve(s, out var c)) return c;
        Problems.Add($"{Ctx(key)}: unknown colour \"{s}\" (use a palette name like \"accent\" or a hex code like \"#E0202F\")");
        theme.TryResolve(def, out c);
        return c;
    }

    public Gradient Gradient(string key, IEnumerable<string> def, Theme theme)
    {
        var names = StrList(key, def);
        var stops = new List<Rgba>();
        foreach (var n in names)
        {
            if (theme.TryResolve(n, out var c)) stops.Add(c);
            else Problems.Add($"{Ctx(key)}: unknown colour \"{n}\"");
        }
        if (stops.Count == 0) foreach (var n in def) if (theme.TryResolve(n, out var c)) stops.Add(c);
        return new Gradient(stops);
    }

    public Node? Sub(string key, string label)
    {
        var v = Raw(key);
        if (v == null) return null;
        if (v is TomlTable t) return new Node(file, t, Full(key), label);
        Problems.Add($"{Ctx(key)} should be a section [{key}]");
        return null;
    }

    public Node SubOrEmpty(string key, string label) => Sub(key, label) ?? new Node(file, new TomlTable(), Full(key), label);

    public List<Node> Array(string key)
    {
        var v = Raw(key);
        var list = new List<Node>();
        if (v == null) return list;
        if (v is TomlTableArray arr)
        {
            for (int i = 0; i < arr.Count; i++) list.Add(new Node(file, arr[i], $"{Full(key)}#{i + 1}", $"[[{key}]] #{i + 1}"));
            return list;
        }
        Problems.Add($"{Ctx(key)} should be written as [[{key}]] sections");
        return list;
    }

    /// Marks a key as known without reading it (for keys read elsewhere).
    public void Touch(string key) => file.MarkUsed(Full(key));

    public Node WithLabel(string label) => new(file, Table, prefix, label);

    static string Describe(object v) => v switch
    {
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        TomlArray => "a list",
        TomlTable => "a section",
        _ => v.ToString() ?? "?"
    };

    static string Range(double a, double b) =>
        double.IsNegativeInfinity(a) ? $"at most {b}" : double.IsPositiveInfinity(b) ? $"at least {a}" : $"{a} to {b}";
}
