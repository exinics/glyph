using System.Globalization;
using System.Text;

namespace Glyph.Data;

/// A piece of text in one colour.
internal readonly record struct Span(string Text, Rgba Color);

/// Format strings used everywhere text is shown:
///   {name}            a value, e.g. {cpu}, {media_title}
///   {name:spec}       times/dates take .NET formats ({time:HH:mm}); numbers take number formats ({cpu:0.0});
///                     text takes a maximum length ({window:40})
///   [colour]…[/]      colour markup: a palette name or hex code; [/] goes back to the default colour
///   \n                a new line (in multi-line widgets)
internal static class Fmt
{
    /// Expands values only (no colour markup kept).
    public static string Text(string format, Func<string, string, string?> value)
    {
        var sb = new StringBuilder();
        foreach (var s in Spans(format, value, null, default)) sb.Append(s.Text);
        return sb.ToString();
    }

    /// Expands values and splits into coloured spans. `theme` may be null (then markup is dropped).
    public static List<Span> Spans(string format, Func<string, string, string?> value, Theme? theme, Rgba color)
    {
        var res = new List<Span>();
        var cur = new StringBuilder();
        var col = color;
        void Flush() { if (cur.Length > 0) { res.Add(new Span(cur.ToString(), col)); cur.Clear(); } }
        for (int i = 0; i < format.Length; i++)
        {
            char c = format[i];
            if (c == '{')
            {
                int end = format.IndexOf('}', i + 1);
                if (end < 0) { cur.Append(c); continue; }
                var inner = format.Substring(i + 1, end - i - 1);
                if (inner.Length == 0) { cur.Append("{}"); i = end; continue; }
                var parts = inner.Split('|');
                int colon = parts[0].IndexOf(':');
                string name = colon < 0 ? parts[0] : parts[0][..colon], spec = colon < 0 ? "" : parts[0][(colon + 1)..];
                var v = value(name.Trim().ToLowerInvariant(), spec);
                if (v == null) { cur.Append("{" + inner + "}"); i = end; continue; }
                Rgba? vc = null;
                if (parts.Length > 1) (v, vc) = Filter(v, parts.AsSpan(1), theme);
                if (vc is Rgba fc) { Flush(); res.Add(new Span(v, fc)); }
                else cur.Append(v);
                i = end;
            }
            else if (c == '[' && theme != null)
            {
                int end = format.IndexOf(']', i + 1);
                if (end > i)
                {
                    var tag = format.Substring(i + 1, end - i - 1).Trim();
                    if (tag == "/") { Flush(); col = color; i = end; continue; }
                    if (tag.Length > 0 && theme.TryResolve(tag, out var nc)) { Flush(); col = nc; i = end; continue; }
                }
                cur.Append(c);
            }
            else if (c == '\\' && i + 1 < format.Length && format[i + 1] == 'n') { cur.Append('\n'); i++; }
            else cur.Append(c);
        }
        Flush();
        return res;
    }

    /// The filters that change how a value is shown: {cpu|pad:3|color>80:accent_bright}.
    public const string FilterHelp =
        "upper, lower, title, pad:N, rpad:N, center:N, max:N, fmt:0.0, prefix:TEXT, suffix:TEXT, default:TEXT, " +
        "bar:N (or bar:N:█░), map:a=b,c=d, replace:a=b, color>80:colour, when>80:TEXT (also <, >=, <=, =, !=)";

    /// The number at the start of a value ("62%" → 62, "8.3 GB" → 8.3).
    public static double? Number(string v)
    {
        int i = 0;
        while (i < v.Length && (char.IsDigit(v[i]) || v[i] == '.' || v[i] == ',' || (i == 0 && v[i] == '-'))) i++;
        return i > 0 && double.TryParse(v[..i].Replace(",", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    static (string, Rgba?) Filter(string v, ReadOnlySpan<string> filters, Theme? theme)
    {
        Rgba? color = null;
        string raw = v;
        foreach (var f0 in filters)
        {
            var f = f0.Trim();
            int c = f.IndexOf(':');
            string fn = (c < 0 ? f : f[..c]).Trim().ToLowerInvariant(), arg = c < 0 ? "" : f[(c + 1)..];
            int.TryParse(arg.Split(':')[0], out int n);
            // comparisons: color>80:accent, when<=20:LOW
            int op = fn.IndexOfAny(new[] { '>', '<', '=', '!' });
            if (op > 0)
            {
                string what = fn[..op], rest = fn[op..];
                string sym = rest.StartsWith(">=") || rest.StartsWith("<=") || rest.StartsWith("!=") ? rest[..2] : rest[..1];
                bool ok;
                var num = Number(raw);
                var limitText = rest[sym.Length..];
                if (num.HasValue && double.TryParse(limitText, NumberStyles.Float, CultureInfo.InvariantCulture, out var lim))
                    ok = sym switch { ">" => num > lim, "<" => num < lim, ">=" => num >= lim, "<=" => num <= lim, "!=" => num != lim, _ => num == lim };
                else ok = sym == "!=" ? !raw.Equals(limitText, StringComparison.OrdinalIgnoreCase) : sym == "=" && raw.Equals(limitText, StringComparison.OrdinalIgnoreCase);
                if (!ok) continue;
                if (what is "color" or "colour") { if (theme != null && theme.TryResolve(arg, out var col)) color = col; }
                else if (what == "when") v = arg;
                continue;
            }
            switch (fn)
            {
                case "upper": v = v.ToUpperInvariant(); break;
                case "lower": v = v.ToLowerInvariant(); break;
                case "title": v = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(v.ToLowerInvariant()); break;
                case "pad": v = v.PadLeft(n); break;
                case "rpad": v = v.PadRight(n); break;
                case "center": { int total = Math.Max(n, v.Length), left = (total - v.Length) / 2; v = v.PadLeft(v.Length + left).PadRight(total); break; }
                case "max": if (n > 0 && v.Length > n) v = v[..Math.Max(0, n - 1)] + "…"; break;
                case "fmt": if (Number(v) is double d) v = d.ToString(arg, CultureInfo.InvariantCulture); break;
                case "prefix": if (v.Length > 0) v = arg + v; break;
                case "suffix": if (v.Length > 0) v += arg; break;
                case "default": if (string.IsNullOrWhiteSpace(v)) v = arg; break;
                case "bar":
                    {
                        var a = arg.Split(':', 2);
                        int len = Math.Clamp(n > 0 ? n : 10, 1, 200);
                        var chars = a.Length > 1 && a[1].Length >= 2 ? a[1] : "█░";
                        double frac = Math.Clamp((Number(raw) ?? 0) / 100.0, 0, 1);
                        int fill = (int)Math.Round(frac * len);
                        v = new string(chars[0], fill) + new string(chars[1], len - fill);
                        break;
                    }
                case "map":
                    foreach (var pair in arg.Split(','))
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2 && v.Equals(kv[0].Trim(), StringComparison.OrdinalIgnoreCase)) { v = kv[1]; break; }
                    }
                    break;
                case "replace": { var kv = arg.Split('=', 2); if (kv.Length == 2 && kv[0].Length > 0) v = v.Replace(kv[0], kv[1]); break; }
            }
        }
        return (v, color);
    }

    /// Splits spans into lines at '\n'.
    public static List<List<Span>> Lines(List<Span> spans)
    {
        var lines = new List<List<Span>> { new() };
        foreach (var s in spans)
        {
            var parts = s.Text.Split('\n');
            for (int k = 0; k < parts.Length; k++)
            {
                if (k > 0) lines.Add(new List<Span>());
                if (parts[k].Length > 0) lines[^1].Add(new Span(parts[k], s.Color));
            }
        }
        return lines;
    }

    /// True if the format has no {values}, or at least one of them isn't empty.
    public static bool HasValues(string format, Func<string, string, string?> value)
    {
        bool any = false, some = false;
        for (int i = format.IndexOf('{'); i >= 0; i = format.IndexOf('{', i + 1))
        {
            int end = format.IndexOf('}', i + 1);
            if (end < 0) break;
            var inner = format.Substring(i + 1, end - i - 1).Split('|')[0];
            int colon = inner.IndexOf(':');
            any = true;
            var v = value((colon < 0 ? inner : inner[..colon]).Trim().ToLowerInvariant(), colon < 0 ? "" : inner[(colon + 1)..]);
            if (!string.IsNullOrWhiteSpace(v)) some = true;
        }
        return !any || some;
    }

    /// Cuts spans to at most `max` characters in total (with …).
    public static List<Span> Cut(List<Span> spans, int max)
    {
        if (max <= 0 || Length(spans) <= max) return spans;
        var res = new List<Span>(); int left = max - 1;
        foreach (var s in spans)
        {
            if (left <= 0) break;
            var t = s.Text.Length <= left ? s.Text : s.Text[..left];
            res.Add(new Span(t, s.Color)); left -= t.Length;
        }
        if (res.Count > 0) res[^1] = new Span(res[^1].Text + "…", res[^1].Color);
        return res;
    }

    public static int Length(IEnumerable<Span> spans) => spans.Sum(s => s.Text.Length);

    // ---------------- value formatting helpers ----------------

    public static string Num(double v, string spec, string def = "0") =>
        v.ToString(spec.Length > 0 ? spec : def, CultureInfo.InvariantCulture);

    public static string Trunc(string s, string spec) =>
        int.TryParse(spec, out var n) && n > 0 && s.Length > n ? s[..Math.Max(0, n - 1)] + "…" : s;

    public static string Bytes(double b, string spec)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int k = 0;
        while (b >= 1024 && k < u.Length - 1) { b /= 1024; k++; }
        return (k == 0 ? b.ToString("0", CultureInfo.InvariantCulture) : b.ToString(spec.Length > 0 ? spec : (b < 10 ? "0.0" : "0"), CultureInfo.InvariantCulture)) + " " + u[k];
    }

    public static string Duration(TimeSpan t)
    {
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m";
        if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
        return $"{t.Minutes}m";
    }

    public static string Clock(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
}
