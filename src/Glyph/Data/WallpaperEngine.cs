using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Glyph.Data;

/// Wallpaper Engine (Steam app 431960) as the picture under a theme: Glyph finds it and its wallpapers (Workshop
/// items, your own projects and the built-in ones), and asks it to play one on a screen through its command line
/// (`wallpaper64.exe -control openWallpaper -file <project.json> -monitor N`). Glyph's own desktop window then
/// draws see-through on top, so widgets, frames and the bar sit over the live wallpaper.
internal static class WallpaperEngine
{
    internal sealed record Item(string Id, string Title, string Type, string Folder, string Project, string Preview);

    static string? root;
    static List<Item>? cache;
    static DateTime cacheAt;

    /// The Wallpaper Engine folder (…\steamapps\common\wallpaper_engine), or null when it isn't installed.
    public static string? Root
    {
        get
        {
            if (root != null && Directory.Exists(root)) return root;
            foreach (var lib in SteamLibraries())
            {
                var d = Path.Combine(lib, "steamapps", "common", "wallpaper_engine");
                if (File.Exists(Path.Combine(d, "wallpaper64.exe")) || File.Exists(Path.Combine(d, "wallpaper32.exe"))) return root = d;
            }
            return null;
        }
    }

    public static bool Installed => Root != null;

    static string Exe => Path.Combine(Root!, Environment.Is64BitOperatingSystem && File.Exists(Path.Combine(Root!, "wallpaper64.exe")) ? "wallpaper64.exe" : "wallpaper32.exe");

    static IEnumerable<string> SteamLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? steam = null;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            steam = (k?.GetValue("SteamPath") as string)?.Replace('/', '\\');
        }
        catch { }
        steam ??= @"C:\Program Files (x86)\Steam";
        if (seen.Add(steam)) yield return steam;
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        string text = "";
        try { if (File.Exists(vdf)) text = File.ReadAllText(vdf); } catch { }
        foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
        {
            var p = m.Groups[1].Value.Replace(@"\\", @"\");
            if (seen.Add(p)) yield return p;
        }
    }

    /// Every wallpaper Wallpaper Engine has: Workshop subscriptions, your own projects and its built-in ones.
    public static List<Item> All(bool fresh = false)
    {
        if (!fresh && cache != null && (DateTime.Now - cacheAt).TotalSeconds < 30) return cache;
        var list = new List<Item>();
        var r = Root;
        if (r != null)
        {
            var folders = new List<string>();
            var steamapps = Path.GetFullPath(Path.Combine(r, "..", ".."));
            void Add(string dir) { try { if (Directory.Exists(dir)) folders.AddRange(Directory.GetDirectories(dir)); } catch { } }
            Add(Path.Combine(steamapps, "workshop", "content", "431960"));
            foreach (var lib in SteamLibraries()) { var w = Path.Combine(lib, "steamapps", "workshop", "content", "431960"); if (!w.StartsWith(steamapps, StringComparison.OrdinalIgnoreCase)) Add(w); }
            Add(Path.Combine(r, "projects", "myprojects"));
            Add(Path.Combine(r, "projects", "defaultprojects"));
            foreach (var f in folders.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pj = Path.Combine(f, "project.json");
                if (!File.Exists(pj)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(pj), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                    var j = doc.RootElement;
                    string S(string k) => j.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                    var prev = S("preview");
                    list.Add(new Item(Path.GetFileName(f), S("title").Trim(), S("type").ToLowerInvariant(), f, pj, prev.Length > 0 ? Path.Combine(f, prev) : ""));
                }
                catch { }
            }
        }
        cache = list.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase).ToList();
        cacheAt = DateTime.Now;
        return cache;
    }

    /// The wallpaper a theme names: a Workshop id, a title, a folder or a project.json path.
    public static Item? Find(string what)
    {
        what = what.Trim();
        if (what.Length == 0) return null;
        var m = Regex.Match(what, @"\((\d+)\)\s*$");   // "Title (123456)" as the settings window lists them
        if (m.Success) what = m.Groups[1].Value;
        if (what.EndsWith("project.json", StringComparison.OrdinalIgnoreCase)) what = Path.GetDirectoryName(what) ?? what;
        var all = All();
        return all.FirstOrDefault(i => i.Id.Equals(what, StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(i => i.Folder.Equals(what.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(i => i.Title.Equals(what, StringComparison.OrdinalIgnoreCase))
            ?? (Directory.Exists(what) && File.Exists(Path.Combine(what, "project.json"))
                ? new Item(Path.GetFileName(what), Path.GetFileName(what), "", what, Path.Combine(what, "project.json"), "") : null);
    }

    /// How a wallpaper is listed in the settings window (and written to the file).
    public static string Label(Item i) => $"{(i.Title.Length > 0 ? i.Title : i.Id)} ({i.Id})";

    public static bool Running => Process.GetProcessesByName("wallpaper64").Length > 0 || Process.GetProcessesByName("wallpaper32").Length > 0;

    static void Control(string args)
    {
        if (!Installed) return;
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Root! });
            Log.Write($"wallpaper engine: {args}");
        }
        catch (Exception e) { Log.Write($"wallpaper engine {args}: {e.Message}"); }
    }

    // What Glyph last asked for on each Wallpaper Engine monitor (so reloads don't restart the wallpaper)
    static readonly Dictionary<int, string> shown = new();
    static bool? muted;

    /// Plays `item` on Wallpaper Engine's monitor `monitor` (0 = its first), starting Wallpaper Engine if needed.
    public static void Show(int monitor, Item item, bool mute)
    {
        bool running = Running;
        if (!running) shown.Clear();
        if (!shown.TryGetValue(monitor, out var cur) || cur != item.Project)
        {
            Control($"-control openWallpaper -file \"{item.Project}\" -monitor {monitor}");
            shown[monitor] = item.Project;
            muted = null;
        }
        if (muted != mute) { Control(mute ? "-control mute" : "-control unmute"); muted = mute; }
    }

    /// Takes Glyph's wallpaper off one monitor (Wallpaper Engine keeps running for the others).
    public static void Hide(int monitor)
    {
        if (!shown.Remove(monitor)) return;
        if (Running) Control($"-control closeWallpaper -monitor {monitor}");
    }

    /// Closes Wallpaper Engine entirely (Glyph draws every screen itself again).
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    static volatile bool closing;

    /// Asks Wallpaper Engine to quit the way Windows does at shut-down (a close message to its tray window), in
    /// the background. Killing it made it think it had crashed: its next start showed a "Safe Start" box. If it
    /// doesn't quit within a few seconds it's left running with no wallpaper (never killed).
    public static void Close()
    {
        shown.Clear(); muted = null;
        if (!Running || closing) return;
        closing = true;
        var pids = Process.GetProcessesByName("wallpaper64").Concat(Process.GetProcessesByName("wallpaper32")).Select(p => (uint)p.Id).ToHashSet();
        for (IntPtr h = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WPETrayWindow", null); h != IntPtr.Zero; h = FindWindowEx(IntPtr.Zero, h, "WPETrayWindow", null))
        {
            GetWindowThreadProcessId(h, out var pid);
            if (pids.Contains(pid)) PostMessage(h, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 24 && Running; i++) await Task.Delay(250);
                if (Running) { Log.Write("Wallpaper Engine didn't quit when asked; its wallpaper is closed instead"); Control("-control closeWallpaper"); }
                else Log.Write("closed Wallpaper Engine");
            }
            finally { closing = false; }
        });
    }
}

/// Drop-down of the wallpapers Wallpaper Engine has ("Title (id)"); an id, title or folder can be typed too.
internal sealed class EngineWallpaperConverter : System.ComponentModel.StringConverter
{
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(System.ComponentModel.ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext? c) =>
        new(WallpaperEngine.All(fresh: true).Select(WallpaperEngine.Label).ToArray());
}
