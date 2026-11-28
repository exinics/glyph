using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Glyph.Options;

// ─────────────────────────────────────────────────────────────────────────────
// Every setting is a property on an options class. The same class is used to:
//   • read the .toml file (with range checks, choice checks and typo detection),
//   • show it in the settings window (PropertyGrid: [Category], [DisplayName], [Description]),
//   • write the .toml file back, with the description as a comment.
// Defaults are the property initialisers.
// ─────────────────────────────────────────────────────────────────────────────

/// Allowed range for a number.
[AttributeUsage(AttributeTargets.Property)]
internal sealed class RangeAttribute : Attribute
{
    public readonly double Min, Max;
    public RangeAttribute(double min, double max) { Min = min; Max = max; }
}

/// The only values a text option may take.
[AttributeUsage(AttributeTargets.Property)]
internal sealed class ChoicesAttribute : Attribute
{
    public readonly string[] Values;
    public ChoicesAttribute(params string[] values) { Values = values; }
}

/// A colour: a palette name or a hex code (for text or lists of text).
[AttributeUsage(AttributeTargets.Property)]
internal sealed class ColorAttribute : Attribute { }

/// A [section] made from a nested options object.
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SectionAttribute : Attribute { }

/// Not read or written by the binder (handled by hand, e.g. lists of layers).
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SkipAttribute : Attribute { }

internal static class Binder
{
    static readonly Dictionary<Type, PropertyInfo[]> Cache = new();

    public static PropertyInfo[] Props(Type t)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(t, out var p)) return p;
            // base class properties first, in declaration order
            var chain = new List<Type>();
            for (var x = t; x != null && x != typeof(object); x = x.BaseType) chain.Insert(0, x);
            p = chain.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(q => q.CanRead && q.CanWrite && q.GetCustomAttribute<SkipAttribute>() == null)
                .ToArray();
            Cache[t] = p;
            return p;
        }
    }

    /// "SpeedMin" -> "speed_min", "DriftX" -> "drift_x"
    public static string Key(PropertyInfo p)
    {
        var n = p.Name;
        var sb = new StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            if (char.IsUpper(n[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(n[i]));
        }
        return sb.ToString();
    }

    public static string Describe(PropertyInfo p) => p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

    /// Reads a table into an options object. Missing keys keep their defaults; problems are recorded.
    public static void Read(object target, Node n, Theme? theme)
    {
        foreach (var p in Props(target.GetType()))
        {
            var key = Key(p);
            var t = p.PropertyType;
            if (p.GetCustomAttribute<SectionAttribute>() != null)
            {
                var sub = n.Sub(key, $"[{key}]");
                if (sub != null) Read(p.GetValue(target)!, sub, theme);
                continue;
            }
            if (!n.Has(key)) continue;
            var range = p.GetCustomAttribute<RangeAttribute>();
            double min = range?.Min ?? double.NegativeInfinity, max = range?.Max ?? double.PositiveInfinity;
            var cur = p.GetValue(target);
            if (t == typeof(float)) p.SetValue(target, n.F(key, (float)cur!, min, max));
            else if (t == typeof(double)) p.SetValue(target, n.Num(key, (double)cur!, min, max));
            else if (t == typeof(int)) p.SetValue(target, n.Int(key, (int)cur!, (int)Math.Max(int.MinValue, min), (int)Math.Min(int.MaxValue, max)));
            else if (t == typeof(bool)) p.SetValue(target, n.Bool(key, (bool)cur!));
            else if (t == typeof(string))
            {
                var choices = p.GetCustomAttribute<ChoicesAttribute>();
                var v = choices != null ? n.Str(key, (string)cur!, choices.Values) : n.Str(key, (string)cur!);
                if (p.GetCustomAttribute<ColorAttribute>() != null && theme != null && !theme.TryResolve(v, out _))
                {
                    n.Problems.Add($"{n.FileName}: '{key}'{(n.Label.Length > 0 ? " in " + n.Label : "")}: unknown colour \"{v}\" (use a palette name like \"accent\" or a hex code like \"#E0202F\")");
                    v = (string)cur!;
                }
                p.SetValue(target, v);
            }
            else if (t == typeof(string[]))
            {
                var v = n.StrList(key, (string[])cur!).ToArray();
                if (p.GetCustomAttribute<ColorAttribute>() != null && theme != null)
                {
                    var bad = v.Where(c => !theme.TryResolve(c, out _)).ToList();
                    if (bad.Count > 0)
                    {
                        n.Problems.Add($"{n.FileName}: '{key}'{(n.Label.Length > 0 ? " in " + n.Label : "")}: unknown colour \"{bad[0]}\"");
                        v = v.Where(c => theme.TryResolve(c, out _)).ToArray();
                        if (v.Length == 0) v = (string[])cur!;
                    }
                }
                p.SetValue(target, v);
            }
        }
    }

    // ---------------- writing ----------------

    public static string Value(object? v) => v switch
    {
        null => "\"\"",
        bool b => b ? "true" : "false",
        float f => f.ToString("0.####", CultureInfo.InvariantCulture),
        double d => d.ToString("0.####", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        string s => Quote(s),
        IEnumerable e => "[" + string.Join(", ", e.Cast<object>().Select(Value)) + "]",
        _ => Quote(v.ToString() ?? ""),
    };

    public static string Quote(string s)
    {
        if (s.Contains('\\') && !s.Contains('\'') && !s.Contains('\n')) return "'" + s + "'";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    }

    /// Writes the plain options (not sections) of an object as key = value lines with comments.
    public static void WriteValues(StringBuilder sb, object o)
    {
        var props = Props(o.GetType()).Where(p => p.GetCustomAttribute<SectionAttribute>() == null).ToArray();
        if (props.Length == 0) return;
        int w = props.Max(p => Key(p).Length);
        foreach (var p in props)
        {
            var line = $"{Key(p).PadRight(w)} = {Value(p.GetValue(o))}";
            var d = new string(Describe(p).Where(ch => ch >= ' ').ToArray());   // TOML comments can't hold control characters
            var choices = p.GetCustomAttribute<ChoicesAttribute>();
            if (choices != null) d += (d.Length > 0 ? ". " : "") + "One of: " + string.Join(", ", choices.Values);
            var r = p.GetCustomAttribute<RangeAttribute>();
            if (r != null) d += $" ({Fmt(r.Min)} to {Fmt(r.Max)})";
            sb.Append(line);
            if (d.Length > 0) sb.Append(new string(' ', Math.Max(1, 34 - line.Length))).Append("# ").Append(d);
            sb.AppendLine();
        }
    }

    static string Fmt(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    /// Writes an object: its values, then each [section] (dotted names for nested ones).
    public static void Write(StringBuilder sb, object o, string? header)
    {
        if (header != null) sb.AppendLine().AppendLine($"[{header}]");
        WriteValues(sb, o);
        foreach (var p in Props(o.GetType()).Where(p => p.GetCustomAttribute<SectionAttribute>() != null))
        {
            var cat = p.GetCustomAttribute<DescriptionAttribute>()?.Description;
            var name = header == null ? Key(p) : header + "." + Key(p);
            if (cat != null) sb.AppendLine().Append("# ").Append(cat);
            Write(sb, p.GetValue(o)!, name);
        }
    }

    /// Writes one [[name]] table from an object (values only).
    public static void WriteTable(StringBuilder sb, string name, object o, string? comment = null)
    {
        sb.AppendLine();
        if (comment != null) sb.AppendLine("# " + comment);
        sb.AppendLine($"[[{name}]]");
        WriteValues(sb, o);
    }

    public static T Clone<T>(T o) where T : class
    {
        var c = (T)Activator.CreateInstance(o.GetType())!;
        CopyInto(o, c);
        return c;
    }

    static void CopyInto(object from, object to)
    {
        foreach (var p in Props(from.GetType()))
        {
            var v = p.GetValue(from);
            if (p.GetCustomAttribute<SectionAttribute>() != null) CopyInto(v!, p.GetValue(to)!);
            else p.SetValue(to, v is string[] a ? (string[])a.Clone() : v);
        }
    }
}

/// Writes a file only if the text changed, via a temp file so a reader never sees half a file.
internal static class SafeFile
{
    public static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && File.ReadAllText(path) == text) return;
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text, new UTF8Encoding(false));
        File.Move(tmp, path, true);
    }
}

// ---------------- settings window helpers (PropertyGrid) ----------------

/// Drop-down of the allowed values for a [Choices] option.
internal sealed class ChoicesConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c)
    {
        var a = c?.PropertyDescriptor?.Attributes.OfType<ChoicesAttribute>().FirstOrDefault();
        return new StandardValuesCollection(a?.Values ?? Array.Empty<string>());
    }
}

/// Drop-down of palette names for a colour option; a hex code can be typed instead.
internal sealed class ColorNameConverter : StringConverter
{
    public static string[] Names = { "bg", "surface", "line", "dim", "text", "bright", "accent", "accent_dim", "accent_bright", "none" };
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c) => new(Names);
}

/// Drop-down of the theme's frames (frames.toml).
/// Palette colour names plus self (glow in each thing's own colours), none and auto.
internal sealed class GlowNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c) => new(ColorNameConverter.Names.Prepend("self").Distinct().ToArray());
}

internal sealed class FrameNameConverter : StringConverter
{
    public static string[] Names = { "none", "auto" };
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c) => new(Names);
}

/// Drop-down of installed fonts; any name can be typed.
internal sealed class FontNameConverter : StringConverter
{
    static string[]? names;
    public static void Forget() => names = null;   // fonts were added or removed
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? c)
    {
        names ??= Glyph.Render.Fonts.FamilyNames().Prepend("").ToArray();   // Windows' fonts + config\fonts
        return new StandardValuesCollection(names);
    }
}

/// Shows a list as "a, b, c" so it can be typed in one line.
internal sealed class ListConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? c, Type t) => t == typeof(string) || base.CanConvertFrom(c, t);
    public override bool CanConvertTo(ITypeDescriptorContext? c, Type? t) => t == typeof(string) || base.CanConvertTo(c, t);
    public override object? ConvertFrom(ITypeDescriptorContext? c, CultureInfo? culture, object value) =>
        value is string s ? s.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray() : base.ConvertFrom(c, culture, value);
    public override object? ConvertTo(ITypeDescriptorContext? c, CultureInfo? culture, object? value, Type t) =>
        t == typeof(string) && value is string[] a ? string.Join(", ", a) : base.ConvertTo(c, culture, value, t);
}

/// Lets a nested section expand in the settings window, showing a short summary when closed.
internal sealed class SectionConverter : ExpandableObjectConverter
{
    public override object? ConvertTo(ITypeDescriptorContext? c, CultureInfo? culture, object? value, Type t) =>
        t == typeof(string) ? "" : base.ConvertTo(c, culture, value, t);
}
