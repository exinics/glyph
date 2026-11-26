using System.Diagnostics;
using System.Globalization;

namespace Glyph.Convert;

/// Finds ffmpeg / ffprobe and reads basic facts about a video.
internal static class FFmpeg
{
    public static string? Find(string exe = "ffmpeg")
    {
        var candidates = new List<string>
        {
            Path.Combine(Paths.Home, "tools", exe + ".exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links", exe + ".exe"),
        };
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(dir.Trim(), exe + ".exe"));
        foreach (var c in candidates) if (File.Exists(c)) return c;
        var pkgs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(pkgs))
        {
            try { return Directory.EnumerateFiles(pkgs, exe + ".exe", SearchOption.AllDirectories).FirstOrDefault(); } catch { }
        }
        return null;
    }

    public sealed record Info(int Width, int Height, double Fps, double Duration, bool IsImage);

    static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };

    public static Info Probe(string input)
    {
        var probe = Find("ffprobe") ?? throw new Exception("ffprobe not found (it comes with ffmpeg)");
        var psi = new ProcessStartInfo(probe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var a in new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height,avg_frame_rate,r_frame_rate:format=duration", "-of", "default=noprint_wrappers=1", input })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var text = p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        var kv = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().Split('=', 2)).Where(a => a.Length == 2)
            .GroupBy(a => a[0]).ToDictionary(g => g.Key, g => g.First()[1]);
        if (!kv.TryGetValue("width", out var ws)) throw new Exception("can't read this file: " + (err.Trim().Length > 0 ? err.Trim() : "no video found"));
        double Rate(string s)
        {
            var parts = s.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && b > 0) return a / b;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
        double fps = kv.TryGetValue("avg_frame_rate", out var af) ? Rate(af) : 0;
        if (fps <= 0 && kv.TryGetValue("r_frame_rate", out var rf)) fps = Rate(rf);
        double dur = kv.TryGetValue("duration", out var ds) && double.TryParse(ds, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        bool image = ImageExts.Contains(Path.GetExtension(input).ToLowerInvariant());
        return new Info(int.Parse(ws), int.Parse(kv["height"]), fps > 0 ? fps : 30, dur, image);
    }

    public static Process Start(string exe, IEnumerable<string> args, bool stdin, bool stdout)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardInput = stdin, RedirectStandardOutput = stdout, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        var p = Process.Start(psi)!;
        // drain stderr so ffmpeg never blocks on it
        var errBuf = new System.Text.StringBuilder();
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errBuf) { if (errBuf.Length < 8000) errBuf.AppendLine(e.Data); } };
        p.BeginErrorReadLine();
        LastErrors.AddOrUpdate(p, errBuf);
        return p;
    }

    public static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Process, System.Text.StringBuilder> LastErrors = new();

    public static string Errors(Process p) => LastErrors.TryGetValue(p, out var sb) ? sb.ToString().Trim() : "";

    public static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
