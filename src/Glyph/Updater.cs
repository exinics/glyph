using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Glyph;

/// Updates Glyph from the newest release on GitHub (glyph.toml: updates, update_repo).
/// A release has a zip (Glyph-win-x64.zip) laid out like the Glyph folder: Glyph\bin\... It is downloaded into
/// logs\update, then the new Glyph.exe is started from there with --finish-update: it waits for this Glyph to close,
/// copies itself over bin\ and starts Glyph again. Your config folder is never touched.
internal static class Updater
{
    public sealed record Release(Version Version, string Tag, string Url, string Notes);

    public const string Asset = "Glyph-win-x64.zip";
    public static Version Current => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);
    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";

    static Version? Parse(string tag)
    {
        var t = tag.Trim().TrimStart('v', 'V');
        int dash = t.IndexOfAny(new[] { '-', '+' });
        if (dash > 0) t = t[..dash];
        return Version.TryParse(t, out var v) ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : null;
    }

    static readonly Version Mine = new(Current.Major, Current.Minor, Math.Max(0, Current.Build));

    /// The newest release if it's newer than this Glyph, else null.
    public static async Task<Release?> Check(string repo)
    {
        using var http = Client();
        var json = await http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var v = Parse(tag);
        if (v == null || v <= Mine) return null;
        string url = "";
        foreach (var a in root.GetProperty("assets").EnumerateArray())
            if (string.Equals(a.GetProperty("name").GetString(), Asset, StringComparison.OrdinalIgnoreCase)) url = a.GetProperty("browser_download_url").GetString() ?? "";
        if (url.Length == 0) return null;
        var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        return new Release(v, tag, url, notes);
    }

    static HttpClient Client()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("Glyph-updater");
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return h;
    }

    /// Downloads and unpacks a release. Returns the folder with the new Glyph.exe.
    public static async Task<string> Download(Release r)
    {
        var dir = Path.Combine(Paths.Logs, "update");
        if (Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } }
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, Asset);
        using (var http = Client())
        using (var s = await http.GetStreamAsync(r.Url))
        using (var f = File.Create(zip))
            await s.CopyToAsync(f);
        var into = Path.Combine(dir, "v" + r.Version);
        ZipFile.ExtractToDirectory(zip, into, true);
        var exe = Directory.GetFiles(into, "Glyph.exe", SearchOption.AllDirectories).OrderBy(p => p.Length).FirstOrDefault()
                  ?? throw new Exception("the download has no Glyph.exe in it");
        return Path.GetDirectoryName(exe)!;
    }

    /// Starts the new Glyph to copy itself over this one (after this one closes). The caller then quits.
    public static void Apply(string newBin, bool start = true)
    {
        var target = AppContext.BaseDirectory.TrimEnd('\\');
        var psi = new ProcessStartInfo(Path.Combine(newBin, "Glyph.exe")) { UseShellExecute = false, WorkingDirectory = newBin };
        psi.ArgumentList.Add("--finish-update");
        psi.ArgumentList.Add(target);
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        if (!start) psi.ArgumentList.Add("--no-start");
        Process.Start(psi);
    }

    /// Glyph.exe --update [--no-start]: updates from a terminal (closes a running Glyph while it copies).
    public static int Command(bool start)
    {
        Native.AttachConsole(-1);
        Console.WriteLine();
        try
        {
            var repo = AppSettings.Load(new List<string>()).UpdateRepo;
            var r = Check(repo).GetAwaiter().GetResult();
            if (r == null) { Console.WriteLine($"Glyph {CurrentText} is the newest version ({repo})."); return 0; }
            Console.WriteLine($"downloading {r.Tag}...");
            var dir = Download(r).GetAwaiter().GetResult();
            Apply(dir, start);
            Console.WriteLine($"updating to {r.Tag}: see logs\\update-result.txt");
            return 0;
        }
        catch (Exception e) { Console.WriteLine("update failed: " + e.Message); return 1; }
    }

    /// Glyph.exe --finish-update <bin folder> <pid>: run from the downloaded copy. Kept free of the rest of Glyph
    /// (Paths would find the download's own folder).
    public static int Finish(string[] args)
    {
        var target = args[1];
        var report = Path.Combine(Path.GetDirectoryName(target.TrimEnd('\\')) ?? target, "logs", "update-result.txt");
        void Note(string s) { try { Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.AppendAllText(report, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {s}{Environment.NewLine}"); } catch { } }
        try
        {
            if (args.Length > 2 && int.TryParse(args[2], out var pid))
                try { using var p = Process.GetProcessById(pid); p.WaitForExit(20000); } catch { }
            var from = AppContext.BaseDirectory.TrimEnd('\\');
            var me = Environment.ProcessId;
            int copied = 0;
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(from, file);
                var dest = Path.Combine(target, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                for (int tries = 0; ; tries++)
                {
                    try { File.Copy(file, dest, true); copied++; break; }
                    catch (IOException) when (tries < 40)
                    {
                        // still in use (e.g. the settings window is open): after a few seconds, close Glyph's other windows
                        if (tries == 15)
                            foreach (var p in Process.GetProcessesByName("Glyph"))
                                try { if (p.Id != me && string.Equals(Path.GetDirectoryName(p.MainModule?.FileName), target, StringComparison.OrdinalIgnoreCase)) p.Kill(); } catch { }
                        Thread.Sleep(300);
                    }
                }
            }
            Note($"updated to {CurrentText}: {copied} files copied into {target}");
            if (!args.Contains("--no-start")) Process.Start(new ProcessStartInfo(Path.Combine(target, "Glyph.exe")) { UseShellExecute = false, WorkingDirectory = target });
            return 0;
        }
        catch (Exception e)
        {
            Note("update failed: " + e.Message);
            try { Process.Start(new ProcessStartInfo(Path.Combine(target, "Glyph.exe")) { UseShellExecute = false, WorkingDirectory = target }); } catch { }
            return 1;
        }
    }

    /// The app's icon (the G), at about `size` pixels.
    public static Icon AppIcon(int size)
    {
        using var s = typeof(Updater).Assembly.GetManifestResourceStream("Glyph.glyph.ico");
        return s != null ? new Icon(s, size, size) : SystemIcons.Application;
    }
}
