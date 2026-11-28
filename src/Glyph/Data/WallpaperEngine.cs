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

    static HashSet<uint> Pids() =>
        Process.GetProcessesByName("wallpaper64").Concat(Process.GetProcessesByName("wallpaper32")).Concat(Process.GetProcessesByName("webwallpaper32"))
            .Select(p => (uint)p.Id).ToHashSet();

    // ───────────── what Wallpaper Engine shows where ─────────────

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, Native.EnumWindowsProc f, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Native.RECT r);

    /// The screens (Glyph's numbers) where a Wallpaper Engine wallpaper window is drawn right now: its windows
    /// sit in the desktop's WorkerW (or Progman) like Glyph's own.
    public static HashSet<int> ShowingOn()
    {
        var res = new HashSet<int>();
        if (!Running) return res;
        var pids = Pids();
        var screens = Glyph.Desktop.Screens.All();
        void Look(IntPtr parent)
        {
            if (parent == IntPtr.Zero) return;
            EnumChildWindows(parent, (h, _) =>
            {
                GetWindowThreadProcessId(h, out var pid);
                if (!pids.Contains(pid) || !Native.IsWindowVisible(h) || !GetWindowRect(h, out var r)) return true;
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                foreach (var s in screens)
                {
                    var o = Rectangle.Intersect(rect, s.Bounds);
                    if (o.Width * (long)o.Height > s.Bounds.Width * (long)s.Bounds.Height / 2) res.Add(s.Number);
                }
                return true;
            }, IntPtr.Zero);
        }
        var progman = Native.FindWindow("Progman", null);
        Look(progman);
        for (IntPtr w = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null); w != IntPtr.Zero; w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) Look(w);
        return res;
    }

    /// Wallpaper Engine's own number for a screen (its -monitor N): from the screen map in its config.json (it keys
    /// screens by their device id), so "screen 2" means the same screen to both programs. null = not known.
    public static int? MonitorIndex(Glyph.Desktop.ScreenInfo s)
    {
        try
        {
            var id = DeviceId(s);
            if (id == null || Root == null) return null;
            var path = Path.Combine(Root, "config.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
            foreach (var user in doc.RootElement.EnumerateObject())
            {
                if (user.Value.ValueKind != JsonValueKind.Object) continue;   // its file has a few plain values at the top too
                if (!user.Value.TryGetProperty("general", out var g) || !g.TryGetProperty("user", out var u) || !u.TryGetProperty("monitormap", out var map)) continue;
                foreach (var m in map.EnumerateObject())
                    if (Norm(m.Name) == Norm(id) && m.Value.TryGetProperty("location", out var loc) && loc.TryGetInt32(out var n)) return n;
            }
        }
        catch (Exception e) { Log.Write("wallpaper engine screen map: " + e.Message); }
        return null;
        static string Norm(string x) => x.Replace('\\', '/').ToLowerInvariant();
    }

    /// The folder of the wallpaper Wallpaper Engine has selected on its monitor `index` (from its config.json), or null.
    public static string? PlayingOn(int index)
    {
        try
        {
            if (Root == null) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "config.json")), new JsonDocumentOptions { AllowTrailingCommas = true });
            foreach (var user in doc.RootElement.EnumerateObject())
                if (user.Value.ValueKind == JsonValueKind.Object && user.Value.TryGetProperty("general", out var g) && g.TryGetProperty("wallpaperconfig", out var wc) && wc.TryGetProperty("selectedwallpapers", out var sel)
                    && sel.TryGetProperty($"Monitor{index}", out var m) && m.TryGetProperty("file", out var f) && f.GetString() is { Length: > 0 } file)
                    return Path.GetDirectoryName(file.Replace('/', '\\'));
        }
        catch { }
        return null;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    struct DISPLAY_DEVICE
    {
        public int cb;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern bool EnumDisplayDevices(string? device, uint i, ref DISPLAY_DEVICE dd, uint flags);

    /// The monitor's device id (\\?\DISPLAY#AOC2270#...#{guid}), the key Wallpaper Engine uses.
    static string? DeviceId(Glyph.Desktop.ScreenInfo s)
    {
        var adapter = $@"\\.\DISPLAY{s.Number}";
        var dd = new DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (EnumDisplayDevices(adapter, 0, ref dd, 1 /* EDD_GET_DEVICE_INTERFACE_NAME */) && dd.DeviceID.Length > 0) return dd.DeviceID;
        return null;
    }

    // ───────────── commands, one at a time ─────────────

    // Commands go to Wallpaper Engine through its exe. Each call starts a short-lived copy that passes the command
    // on, and if Wallpaper Engine isn't running the first one starts it; sending several at once while it starts
    // up made extra copies. So they queue: one at a time, and after a start nothing more is sent until it's up.
    static readonly object queueGate = new();
    static Task queue = Task.CompletedTask;

    static void Control(string args)
    {
        if (!Installed) return;
        lock (queueGate) queue = queue.ContinueWith(_ => Run(args)).Unwrap();
    }

    static async Task Run(string args)
    {
        try
        {
            bool starting = !Running;
            if (starting && !args.Contains("openWallpaper")) return;   // only opening a wallpaper may start it
            using (var p = Process.Start(new ProcessStartInfo(Exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Root! }))
                if (p != null && !starting) await Task.Run(() => p.WaitForExit(5000));
            Log.Write($"wallpaper engine: {args}");
            if (starting)
                for (int i = 0; i < 60 && ShowingOn().Count == 0; i++) await Task.Delay(250);   // up to 15 s for it to start drawing
        }
        catch (Exception e) { Log.Write($"wallpaper engine {args}: {e.Message}"); }
    }

    /// Plays `item` on Wallpaper Engine's monitor (its number), starting Wallpaper Engine if needed.
    public static void Open(int monitor, Item item, bool mute)
    {
        Control($"-control openWallpaper -file \"{item.Project}\" -monitor {monitor}");
        Control(mute ? "-control mute" : "-control unmute");
    }

    /// Takes Wallpaper Engine's wallpaper off one of its monitors (it keeps running for the others).
    public static void Remove(int monitor) => Control($"-control closeWallpaper -monitor {monitor}");

    /// Takes all its wallpapers off (it keeps running, drawing nothing).
    public static void RemoveAll() => Control("-control closeWallpaper");

    static volatile bool closing;

    /// Asks Wallpaper Engine to quit the way Windows does (a close message to its tray window). Never killed:
    /// that made it think it had crashed. `done(true)` when it quit, `done(false)` when it's still running after
    /// a few seconds (some versions ignore the request; quitting from its own tray icon always works).
    public static void TryClose(Action<bool>? done = null)
    {
        if (!Running) { done?.Invoke(true); return; }
        if (closing) return;
        closing = true;
        var pids = Pids();
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
                bool quit = !Running;
                Log.Write(quit ? "closed Wallpaper Engine" : "Wallpaper Engine didn't quit when asked");
                done?.Invoke(quit);
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
