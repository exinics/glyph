using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Glyph.Options;

namespace Glyph.Launcher;

/// glyph.toml [launcher]: what the launcher searches.
internal sealed class LauncherSettings
{
    [Description("Apps from the Start menu (including Store apps).")] public bool Apps { get; set; } = true;
    [Description("Your Glyph shortcuts (Shortcuts page).")] public bool Shortcuts { get; set; } = true;
    [Description("Glyph commands: switch theme, sound output, tiling layout, settings, pause, quit…")] public bool Commands { get; set; } = true;
    [Description("Maths: type 12*7, (3+4)^2, sqrt(2), 15% of 80. Enter copies the answer.")] public bool Calculator { get; set; } = true;
    [DisplayName("Files and folders"), Description("Type a path (C:\\, ~\\, %appdata%\\…) to open it; shows what's in the folder as you type.")] public bool Paths { get; set; } = true;
    [DisplayName("Run commands"), Description("Start with > to run a command, e.g. >notepad or >cmd /k ipconfig.")] public bool Run { get; set; } = true;
    [DisplayName("Web search"), Description("The web search offered under the results; {q} is what you typed. Empty = none.")]
    public string WebSearch { get; set; } = "https://www.google.com/search?q={q}";
    [DisplayName("Learn favourites"), Description("Things you open often come first, and an empty search shows them.")] public bool Remember { get; set; } = true;
}

/// One result.
internal sealed record Item(string Id, string Title, string Kind, string Action, string Icon = "")
{
    public int Score;
    public int[] Hits = Array.Empty<int>();   // which characters of the title matched (highlighted)
}

/// Finds results for a query.
internal sealed class Search
{
    public LauncherSettings S = new();
    public Func<List<Item>> Commands = () => new();
    List<Item> apps = new(), shortcuts = new(), commands = new();
    readonly Dictionary<string, (int n, long last)> history = new();
    static string HistoryPath => Path.Combine(Paths.Logs, "launcher-history.json");
    public event Action? AppsLoaded;

    public Search()
    {
        LoadHistory();
        AppScan.Done += () => { LoadApps(); AppsLoaded?.Invoke(); };
    }

    /// Called when the launcher opens: reads the Start menu again now and then, in the background.
    public void Refresh()
    {
        // shortcuts and commands are read once per opening, not on every key
        try
        {
            shortcuts = Glyph.Widgets.ShortcutStore.Load(new List<string>())
                .Select(s => new Item("sc:" + s.Target.ToLowerInvariant(), s.ToString(), "shortcut", "shortcut:" + s.Target, s.Target)).ToList();
        }
        catch (Exception e) { Log.Write("launcher: shortcuts: " + e.Message); }
        try { commands = Commands(); } catch (Exception e) { Log.Write("launcher: commands: " + e.Message); }
        if (!S.Apps) return;
        LoadApps();
        // the Start menu is read by a helper program (Glyph.exe --scan-apps): reading it loads a lot of
        // Windows shell code that would otherwise stay in Glyph's memory for good
        if (DateTime.UtcNow - AppScan.ListTime() > TimeSpan.FromMinutes(3)) AppScan.Start();
    }

    DateTime appsFileTime;

    void LoadApps()
    {
        var t = AppScan.ListTime();
        if (t == appsFileTime) return;
        appsFileTime = t;
        var list = AppScan.Read();
        if (list.Count > 0) { apps = list; Log.D($"launcher: {list.Count} apps in the Start menu"); }
    }

    public List<Item> Find(string q, int max)
    {
        q = q.Trim();
        var res = new List<Item>();
        if (q.Length == 0)
        {
            if (!S.Remember) return res;
            var known = Pool().GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var (id, _) in history.OrderByDescending(h => h.Value.n * 4 + Recency(h.Value.last)).Take(max))
                if (known.TryGetValue(id, out var it)) res.Add(it with { });
            return res;
        }

        if (S.Run && q.StartsWith('>'))
        {
            var cmd = q[1..].Trim();
            if (cmd.Length > 0) res.Add(new Item("run", "Run  " + cmd, "command", "run:" + cmd));
            return res;
        }
        if (S.Calculator && Calc.TryEval(q, out var value))
            res.Add(new Item("calc", "= " + value, "copy", "copy:" + value) { Score = 100000 });
        if (S.Paths && LooksLikePath(q)) res.AddRange(PathItems(q, max));
        else if (LooksLikeUrl(q)) res.Add(new Item("url:" + q, "Open " + q, "website", q.Contains("://") ? q : "https://" + q) { Score = 90000 });

        foreach (var it in Pool())
        {
            var (s, hits) = Score(it.Title, q);
            if (s <= 0) continue;
            if (S.Remember && history.TryGetValue(it.Id, out var h)) s += (int)Math.Min(300, 60 * Math.Log2(1 + h.n)) + Recency(h.last);
            res.Add(it with { Score = s, Hits = hits });
        }
        // the same name from two places (an app and a shortcut to it): keep the best
        var list = res.GroupBy(i => i.Title.ToLowerInvariant() + "|" + (i.Kind is "app" or "shortcut" ? "x" : i.Kind)).Select(g => g.OrderByDescending(i => i.Score).First())
            .OrderByDescending(i => i.Score).Take(max).ToList();
        if (S.WebSearch.Length > 0 && list.Count < max)
            list.Add(new Item("web", $"Search the web for \u201c{q}\u201d", "web", S.WebSearch.Replace("{q}", Uri.EscapeDataString(q))));
        return list;
    }

    static int Recency(long last)
    {
        double days = (DateTime.UtcNow.Ticks - last) / (double)TimeSpan.TicksPerDay;
        return days < 1 ? 60 : days < 7 ? 30 : 0;
    }

    IEnumerable<Item> Pool()
    {
        if (S.Apps) foreach (var a in apps) yield return a;
        if (S.Shortcuts) foreach (var s in shortcuts) yield return s;
        if (S.Commands) foreach (var c in commands) yield return c;
    }

    public void Used(Item it)
    {
        if (!S.Remember || it.Id is "calc" or "web" or "run") return;
        history.TryGetValue(it.Id, out var h);
        history[it.Id] = (h.n + 1, DateTime.UtcNow.Ticks);
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(history.ToDictionary(k => k.Key, k => new[] { k.Value.n, k.Value.last })));
        }
        catch (Exception e) { Log.Write("launcher history: " + e.Message); }
    }

    void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return;
            var d = JsonSerializer.Deserialize<Dictionary<string, long[]>>(File.ReadAllText(HistoryPath));
            if (d != null) foreach (var (k, v) in d) if (v.Length == 2) history[k] = ((int)v[0], v[1]);
        }
        catch { }
    }

    // ───────────── matching ─────────────

    /// How well `q` matches `text` (0 = not at all), and which characters matched.
    public static (int score, int[] hits) Score(string text, string q)
    {
        var t = text.ToLowerInvariant();
        var ql = q.ToLowerInvariant();
        if (t == ql) return (1000, Enumerable.Range(0, t.Length).ToArray());
        int idx = t.IndexOf(ql, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var hits = Enumerable.Range(idx, ql.Length).ToArray();
            if (idx == 0) return (900 - Math.Min(100, t.Length - ql.Length), hits);
            if (!char.IsLetterOrDigit(t[idx - 1])) return (750 - Math.Min(100, idx), hits);
            return (520 - Math.Min(100, idx), hits);
        }
        // initials: "vsc" -> Visual Studio Code
        var starts = new List<int>();
        for (int i = 0; i < t.Length; i++) if (char.IsLetterOrDigit(t[i]) && (i == 0 || !char.IsLetterOrDigit(t[i - 1]) || (char.IsUpper(text[i]) && char.IsLower(text[i - 1])))) starts.Add(i);
        if (ql.Length >= 2 && !ql.Contains(' '))
        {
            int k = 0; var ih = new List<int>();
            foreach (var s in starts) if (k < ql.Length && t[s] == ql[k]) { ih.Add(s); k++; }
            if (k == ql.Length) return (700 - starts.Count, ih.ToArray());
        }
        // every word of the query somewhere in the name
        var words = ql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && words.All(w => t.Contains(w)))
        {
            var wh = new List<int>();
            foreach (var w in words) { int i = t.IndexOf(w, StringComparison.Ordinal); wh.AddRange(Enumerable.Range(i, w.Length)); }
            return (480, wh.Distinct().ToArray());
        }
        // letters in order (fuzzy): fewer gaps = better
        if (ql.Length >= 2)
        {
            int k = 0, gaps = 0, last = -1; var sh = new List<int>();
            for (int i = 0; i < t.Length && k < ql.Length; i++)
                if (t[i] == ql[k]) { if (last >= 0) gaps += i - last - 1; last = i; sh.Add(i); k++; }
            if (k == ql.Length && gaps <= ql.Length + 2) return (300 - gaps * 8 - sh[0] * 2, sh.ToArray());
        }
        return (0, Array.Empty<int>());
    }

    // ───────────── paths and websites ─────────────

    static bool LooksLikePath(string q) =>
        q.Length >= 2 && (q[1] == ':' && char.IsLetter(q[0]) || q.StartsWith("\\\\") || q.StartsWith("~") || q.StartsWith("%") || q.StartsWith("shell:", StringComparison.OrdinalIgnoreCase));

    static bool LooksLikeUrl(string q) =>
        !q.Contains(' ') && (q.StartsWith("http://") || q.StartsWith("https://") ||
        System.Text.RegularExpressions.Regex.IsMatch(q, @"^[\w-]+(\.[\w-]+)*\.(com|org|net|io|dev|uk|co|app|gg|tv|me|ai|edu|gov)(/\S*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    static string Expand(string q)
    {
        if (q.StartsWith("~")) q = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + q[1..];
        return Environment.ExpandEnvironmentVariables(q);
    }

    static IEnumerable<Item> PathItems(string q, int max)
    {
        var p = Expand(q);
        if (p.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) { yield return new Item("path", "Open " + q, "folder", p) { Score = 95000 }; yield break; }
        bool dir = Directory.Exists(p), file = !dir && File.Exists(p);
        if (dir || file) yield return new Item("path:" + p.ToLowerInvariant(), p, dir ? "folder" : "file", p, p) { Score = 95000 };
        // what's in the folder that starts with what's typed after the last \
        string parent, start;
        if (dir && (p.EndsWith('\\') || p.EndsWith('/'))) { parent = p; start = ""; }
        else { parent = Path.GetDirectoryName(p) ?? ""; start = Path.GetFileName(p); }
        if (parent.Length == 0 || !Directory.Exists(parent)) yield break;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(parent, start + "*").Take(max * 3).ToList(); }
        catch { yield break; }
        int n = 0;
        foreach (var e in entries.OrderBy(e => !Directory.Exists(e)).ThenBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(e.TrimEnd('\\'), p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
            bool d = Directory.Exists(e);
            yield return new Item("path:" + e.ToLowerInvariant(), e + (d ? "\\" : ""), d ? "folder" : "file", e, e) { Score = 94000 - n };
            if (++n >= max) break;
        }
    }
}

/// The helper program that reads the Start menu and pulls the apps' icons out of Windows, saving
/// both in config\icons\cache: apps.json and one PNG per icon (named like the shortcut icons).
internal static class AppScan
{
    public static string Dir => Path.Combine(Paths.IconCache, "apps");
    static string ListFile => Path.Combine(Dir, "apps.json");
    static string WantedFile => Path.Combine(Dir, "wanted.txt");
    static System.Diagnostics.Process? running;
    static bool again;
    static readonly object gate = new();
    /// raised (on a thread-pool thread) when a scan finished
    public static event Action? Done;

    public static DateTime ListTime() => File.Exists(ListFile) ? File.GetLastWriteTimeUtc(ListFile) : DateTime.MinValue;

    public static List<Item> Read()
    {
        try
        {
            var rows = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(ListFile)) ?? new();
            return rows.Where(r => r.Length == 2).Select(r => new Item("app:" + r[0].ToLowerInvariant(), r[1], "app", "shell:AppsFolder\\" + r[0], "shell:AppsFolder\\" + r[0])).ToList();
        }
        catch { return new(); }
    }

    /// Icons the launcher wants that aren't saved yet: the next scan makes them.
    public static void Want(IEnumerable<string> targets)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.AppendAllLines(WantedFile, targets);
        }
        catch { }
        Start();
    }

    public static void Start()
    {
        lock (gate)
        {
            if (running != null) { again = true; return; }
            try
            {
                var p = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(Paths.Exe, "--scan-apps") { UseShellExecute = false, CreateNoWindow = true }, EnableRaisingEvents = true };
                p.Exited += (_, _) =>
                {
                    bool more;
                    lock (gate) { running?.Dispose(); running = null; more = again; again = false; }
                    Done?.Invoke();
                    if (more) Start();
                };
                p.Start();
                running = p;
            }
            catch (Exception e) { Log.Write("launcher: can't start the app scan: " + e.Message); }
        }
    }

    /// Glyph.exe --scan-apps (a separate, short-lived program)
    public static int Run()
    {
        Directory.CreateDirectory(Dir);
        var apps = StartMenu.Apps();
        if (apps.Count > 0)
        {
            var rows = apps.Select(a => new[] { a.Action["shell:AppsFolder\\".Length..], a.Title }).ToList();
            var tmp = ListFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(rows));
            File.Move(tmp, ListFile, true);
        }
        var targets = apps.Select(a => a.Icon).ToList();
        if (File.Exists(WantedFile))
        {
            targets.InsertRange(0, File.ReadAllLines(WantedFile).Where(l => l.Length > 0).Distinct());
            try { File.Delete(WantedFile); } catch { }
        }
        foreach (var t in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var sc = new Shortcut { Target = t };
            if (File.Exists(Path.Combine(Paths.IconCache, sc.IconKey + ".png"))) continue;
            try { Glyph.Widgets.IconSource.Get(sc, null)?.Dispose(); } catch { }
        }
        return 0;
    }
}

/// The apps in the Start menu (the same list as "All apps"), from the shell's AppsFolder.
internal static class StartMenu
{
    public static List<Item> Apps()
    {
        var res = new List<Item>();
        var folderId = new Guid("1e87508d-89c2-42f0-8a7e-645a0f50ca58");   // FOLDERID_AppsFolder
        var iidItem = typeof(IShellItem).GUID;
        int hr = SHGetKnownFolderItem(ref folderId, 0, IntPtr.Zero, ref iidItem, out var folder);
        if (hr != 0 || folder == null) { Log.Write($"launcher: can't open the apps folder (0x{hr:X8})"); return res; }
        try
        {
            var bhid = new Guid("94f60519-2850-4924-aa5a-d15e84868039");   // BHID_EnumItems
            var iidEnum = typeof(IEnumShellItems).GUID;
            hr = folder.BindToHandler(IntPtr.Zero, ref bhid, ref iidEnum, out var enumObj);
            if (hr != 0) { Log.Write($"launcher: can't list the apps folder (0x{hr:X8})"); return res; }
            var en = (IEnumShellItems)enumObj;
            try
            {
                while (en.Next(1, out var item, out var got) == 0 && got == 1)
                {
                    try
                    {
                        var name = Name(item, 0);                    // SIGDN_NORMALDISPLAY
                        var id = Name(item, unchecked((int)0x80018001)); // SIGDN_PARENTRELATIVEPARSING: the app's id (or path)
                        if (name.Length == 0 || id.Length == 0) continue;
                        if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) || name.Equals("Read me", StringComparison.OrdinalIgnoreCase)) continue;
                        var target = "shell:AppsFolder\\" + id;
                        res.Add(new Item("app:" + id.ToLowerInvariant(), name, "app", target, target));
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            finally { Marshal.ReleaseComObject(en); }
        }
        finally { Marshal.ReleaseComObject(folder); }
        return res;
    }

    static string Name(IShellItem item, int sigdn)
    {
        if (item.GetDisplayName(sigdn, out var p) != 0 || p == IntPtr.Zero) return "";
        try { return Marshal.PtrToStringUni(p) ?? ""; }
        finally { Marshal.FreeCoTaskMem(p); }
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(int sigdn, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attrs);
        [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IEnumShellItems
    {
        [PreserveSig] int Next(uint celt, out IShellItem item, out uint fetched);
        [PreserveSig] int Skip(uint celt);
        [PreserveSig] int Reset();
        [PreserveSig] int Clone(out IEnumShellItems e);
    }

    [DllImport("shell32.dll")] static extern int SHGetKnownFolderItem(ref Guid id, uint flags, IntPtr token, ref Guid riid, out IShellItem item);
}

/// A small calculator: + - * / % ^, brackets, sqrt sin cos tan log ln abs round floor ceil, pi, e, and "15% of 80".
internal static class Calc
{
    public static bool TryEval(string q, out string result)
    {
        result = "";
        var s = q.Trim();
        bool forced = s.StartsWith('=');
        if (forced) s = s[1..];
        s = s.Replace("×", "*").Replace("÷", "/").Replace(",", "").Trim();
        if (s.Length == 0) return false;
        // "15% of 80" -> (15/100)*80
        s = System.Text.RegularExpressions.Regex.Replace(s, @"([\d.]+)\s*%\s*of\s*", "($1/100)*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!forced)
        {
            if (!s.Any(char.IsDigit) && !s.Contains("pi")) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(s, @"[+\-*/^%()]|sqrt|sin|cos|tan|log|ln|abs")) return false;   // a plain number isn't maths
            if (System.Text.RegularExpressions.Regex.IsMatch(s, @"[^0-9a-z.+\-*/^%() \t]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return false;
        }
        try
        {
            var p = new Parser(s.ToLowerInvariant());
            double v = p.Expr();
            if (!p.End || double.IsNaN(v) || double.IsInfinity(v)) return false;
            result = Math.Abs(v) >= 1e15 || (Math.Abs(v) < 1e-6 && v != 0) ? v.ToString("G10", CultureInfo.InvariantCulture) : Math.Round(v, 10).ToString("0.##########", CultureInfo.InvariantCulture);
            return true;
        }
        catch { return false; }
    }

    sealed class Parser
    {
        readonly string s; int i;
        public Parser(string s) { this.s = s; }
        public bool End { get { Ws(); return i >= s.Length; } }
        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        bool Eat(char c) { Ws(); if (i < s.Length && s[i] == c) { i++; return true; } return false; }

        public double Expr()
        {
            double v = Term();
            while (true) { if (Eat('+')) v += Term(); else if (Eat('-')) v -= Term(); else return v; }
        }
        double Term()
        {
            double v = Power();
            while (true)
            {
                if (Eat('*')) v *= Power();
                else if (Eat('/')) v /= Power();
                else if (Eat('%')) { Ws(); if (i < s.Length && (char.IsDigit(s[i]) || s[i] == '(' || s[i] == '.')) v %= Power(); else v /= 100; }
                else return v;
            }
        }
        double Power() { double b = Unary(); return Eat('^') ? Math.Pow(b, Power()) : b; }
        double Unary() => Eat('-') ? -Unary() : Eat('+') ? Unary() : Atom();
        double Atom()
        {
            Ws();
            if (Eat('(')) { double v = Expr(); if (!Eat(')')) throw new FormatException(); return v; }
            int st = i;
            if (i < s.Length && char.IsLetter(s[i]))
            {
                while (i < s.Length && char.IsLetter(s[i])) i++;
                var name = s[st..i];
                if (name == "pi") return Math.PI;
                if (name == "e") return Math.E;
                double a = Atom();
                return name switch
                {
                    "sqrt" => Math.Sqrt(a), "sin" => Math.Sin(a), "cos" => Math.Cos(a), "tan" => Math.Tan(a), "log" => Math.Log10(a), "ln" => Math.Log(a),
                    "abs" => Math.Abs(a), "round" => Math.Round(a), "floor" => Math.Floor(a), "ceil" => Math.Ceiling(a), "exp" => Math.Exp(a),
                    _ => throw new FormatException(),
                };
            }
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (st == i) throw new FormatException();
            return double.Parse(s[st..i], CultureInfo.InvariantCulture);
        }
    }
}
