namespace Glyph.Data;

/// Lists for list widgets (a to-do list, an agenda…): config\lists\<name>.txt, one item per line.
/// "[x] text" = done, "[ ] text" or just "text" = to do. Agenda items: "19:30 | Maths class | Today".
/// Lines starting with # are notes. Clicking an item on the desktop ticks it (action list:toggle:<name>:<n>).
internal static class Lists
{
    internal sealed record Item(string Text, bool Done, int Line);

    public static string Folder => Path.Combine(Paths.Config, "lists");
    public static string FileOf(string name) => Path.Combine(Folder, Safe(name) + ".txt");
    static string Safe(string name) { var n = string.Concat(name.Trim().Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)); return n.Length > 0 ? n : "todo"; }

    sealed class Cached { public DateTime Stamp; public DateTime CheckedAt; public List<Item> Items = new(); }
    static readonly Dictionary<string, Cached> cache = new(StringComparer.OrdinalIgnoreCase);
    static readonly object gate = new();

    /// The list's items (read again when the file changes; looked at at most twice a second).
    public static List<Item> Get(string name)
    {
        lock (gate)
        {
            if (!cache.TryGetValue(name, out var c)) cache[name] = c = new Cached();
            if ((DateTime.UtcNow - c.CheckedAt).TotalSeconds < 0.5) return c.Items;
            c.CheckedAt = DateTime.UtcNow;
            var path = FileOf(name);
            var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (stamp == c.Stamp) return c.Items;
            c.Stamp = stamp;
            c.Items = Parse(ReadLines(path));
            return c.Items;
        }
    }

    static List<string> ReadLines(string path)
    {
        try { return File.Exists(path) ? File.ReadAllLines(path).ToList() : new(); } catch { return new(); }
    }

    static List<Item> Parse(List<string> lines)
    {
        var items = new List<Item>();
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i].Trim();
            if (l.Length == 0 || l.StartsWith('#')) continue;
            bool done = false;
            if (l.StartsWith("[x]", StringComparison.OrdinalIgnoreCase)) { done = true; l = l[3..].Trim(); }
            else if (l.StartsWith("[ ]")) l = l[3..].Trim();
            items.Add(new Item(l, done, i));
        }
        return items;
    }

    /// The list as text for the settings window's editor (the file as it is).
    public static string Text(string name) { var p = FileOf(name); try { return File.Exists(p) ? File.ReadAllText(p) : ""; } catch { return ""; } }

    public static void Save(string name, string text)
    {
        Directory.CreateDirectory(Folder);
        Glyph.Options.SafeFile.Write(FileOf(name), text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
    }

    /// Adds an item at the end.
    public static void Add(string name, string text)
    {
        if (text.Trim().Length == 0) return;
        var lines = ReadLines(FileOf(name));
        lines.Add(text.Trim());
        Save(name, string.Join("\n", lines));
        lock (gate) cache.Remove(name);
    }

    /// Changes item `n`'s text (keeping whether it's done); empty text removes it.
    public static void Set(string name, int n, string text)
    {
        var lines = ReadLines(FileOf(name));
        var items = Parse(lines);
        if (n < 0 || n >= items.Count) return;
        var it = items[n];
        if (text.Trim().Length == 0) lines.RemoveAt(it.Line);
        else lines[it.Line] = (it.Done ? "[x] " : "") + text.Trim();
        Save(name, string.Join("\n", lines));
        lock (gate) cache.Remove(name);
    }

    public static Item? At(string name, int n) { var items = Parse(ReadLines(FileOf(name))); return n >= 0 && n < items.Count ? items[n] : null; }

    /// The desktop's edit box for adding (n = -1) or changing item n. Agenda lists ask for time, what and note.
    public static void Edit(string style, string name, int n)
    {
        var it = n >= 0 ? At(name, n) : null;
        if (n >= 0 && it == null) return;
        void Put(string text) { if (n < 0) Add(name, text); else Set(name, n, text); }
        Action? del = n >= 0 ? () => Set(name, n, "") : null;
        if (style == "agenda")
        {
            var parts = (it?.Text ?? "").Split('|').Select(p => p.Trim()).ToArray();
            string time = parts.Length > 1 ? parts[0] : "", what = parts.Length > 1 ? parts[1] : parts.ElementAtOrDefault(0) ?? "", note = parts.Length > 2 ? parts[2] : "";
            Glyph.Ui.Prompt.Ask(n < 0 ? "add to " + name : "edit " + name, new[] { ("Time (e.g. 19:30)", time), ("What", what), ("Note (e.g. Today)", note) },
                v => { if (v[1].Length == 0 && v[0].Length == 0) { if (n >= 0) Set(name, n, ""); return; } Put(string.Join(" | ", new[] { v[0], v[1], v[2] }.Where((x, i) => i < 2 || x.Length > 0))); }, del);
        }
        else
            Glyph.Ui.Prompt.Ask(n < 0 ? "add to " + name : "edit " + name, new[] { ("What to do…", it?.Text ?? "") }, v => Put(v[0]), del);
    }
    public static void Toggle(string name, int n)
    {
        var path = FileOf(name);
        var lines = ReadLines(path);
        var items = Parse(lines);
        if (n < 0 || n >= items.Count) return;
        var it = items[n];
        lines[it.Line] = (it.Done ? "[ ] " : "[x] ") + it.Text;
        Save(name, string.Join("\n", lines));
        lock (gate) cache.Remove(name);
    }
}
