using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Glyph.Data;

/// Time-synced lyrics for the song playing, from lrclib.net (free, no account). Only the song's title, artist,
/// album and length are sent. Found lyrics are kept in logs\lyrics so a song is only looked up once.
internal static class Lyrics
{
    internal readonly record struct Line(double At, string Text);

    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static string key = "";
    static volatile List<Line> lines = new();
    static volatile bool synced;
    public static volatile int Version;
    public static volatile string Status = "";   // "" = found, else why there are none ("looking…", "no lyrics")

    static Lyrics() { http.DefaultRequestHeaders.UserAgent.ParseAdd("Glyph/1.0"); }

    static string Folder => Path.Combine(Paths.Logs, "lyrics");

    /// Makes sure the lyrics for the song now playing are loaded (or being looked up).
    public static void Use()
    {
        Media.Use();
        if (!Media.HasSession) { if (key.Length > 0) { key = ""; lines = new(); Status = ""; Version++; } return; }
        string title = Media.Title, artist = Media.Artist, album = Media.Album;
        double len = Media.Length;
        if (title.Length == 0) return;
        var k = $"{artist}\n{title}";
        if (k == key) return;
        key = k; lines = new(); synced = false; Status = "looking…"; Version++;
        _ = Task.Run(() => Fetch(k, title, artist, album, len));
    }

    static string CacheFile(string k)
    {
        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(k)))[..16];
        return Path.Combine(Folder, hash + ".lrc");
    }

    static async Task Fetch(string k, string title, string artist, string album, double len)
    {
        try
        {
            var cache = CacheFile(k);
            string text = "";
            if (File.Exists(cache)) text = await File.ReadAllTextAsync(cache);
            else
            {
                // wait for the length to arrive (the timeline comes a moment after the title)
                for (int i = 0; i < 10 && Media.Length <= 0 && key == k; i++) await Task.Delay(300);
                len = Media.Length > 0 ? Media.Length : len;
                string Q(string s) => Uri.EscapeDataString(s);
                var url = $"https://lrclib.net/api/get?track_name={Q(title)}&artist_name={Q(artist)}" + (album.Length > 0 ? $"&album_name={Q(album)}" : "") + (len > 0 ? $"&duration={Math.Round(len)}" : "");
                using (var r = await http.GetAsync(url))
                    if (r.IsSuccessStatusCode) text = Pick(JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement);
                if (text.Length == 0)
                {
                    using var r2 = await http.GetAsync($"https://lrclib.net/api/search?track_name={Q(title)}&artist_name={Q(artist)}");
                    if (r2.IsSuccessStatusCode)
                    {
                        var arr = JsonDocument.Parse(await r2.Content.ReadAsStringAsync()).RootElement;
                        if (arr.ValueKind == JsonValueKind.Array)
                            foreach (var e in arr.EnumerateArray().OrderBy(e => len > 0 && e.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? Math.Abs(d.GetDouble() - len) : 0))
                            { text = Pick(e); if (text.Length > 0) break; }
                    }
                }
                Directory.CreateDirectory(Folder);
                await File.WriteAllTextAsync(cache, text);   // an empty file = looked, none found
            }
            if (key != k) return;
            var (parsed, isSynced) = Parse(text);
            lines = parsed; synced = isSynced; Status = parsed.Count == 0 ? "no lyrics found" : ""; Version++;
        }
        catch (Exception e)
        {
            Log.Write("lyrics: " + e.Message);
            if (key == k) { Status = "lyrics unavailable"; Version++; key = ""; }   // try again next time it's asked for
        }
    }

    static string Pick(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return "";
        if (e.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() is { Length: > 0 } sl) return sl;
        if (e.TryGetProperty("plainLyrics", out var p) && p.ValueKind == JsonValueKind.String && p.GetString() is { Length: > 0 } pl) return pl;
        return "";
    }

    static readonly Regex Stamp = new(@"\[(\d+):(\d+(?:\.\d+)?)\]");

    static (List<Line>, bool) Parse(string text)
    {
        var list = new List<Line>();
        bool any = false;
        foreach (var raw in text.Split('\n'))
        {
            var ms = Stamp.Matches(raw);
            var words = Stamp.Replace(raw, "").Trim();
            if (ms.Count == 0) { if (raw.Trim().Length > 0 && !raw.TrimStart().StartsWith('[')) list.Add(new Line(-1, raw.Trim())); continue; }
            any = true;
            foreach (Match m in ms)
                list.Add(new Line(int.Parse(m.Groups[1].Value) * 60 + double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), words));
        }
        if (any) list = list.Where(l => l.At >= 0).OrderBy(l => l.At).ToList();
        return (list, any);
    }

    /// The lines around the one being sung now: `before` lines before it, it, `after` lines after.
    /// Index of the current line in the result (or -1 when it hasn't started / isn't synced).
    public static (List<string> lines, int current) Around(int before, int after, double offset)
    {
        var all = lines;
        if (all.Count == 0) return (new(), -1);
        if (!synced) return (all.Take(before + after + 1).Select(l => l.Text).ToList(), -1);
        double pos = Media.Position + offset;
        int cur = -1;
        for (int i = 0; i < all.Count; i++) { if (all[i].At <= pos) cur = i; else break; }
        int from = Math.Max(0, cur - before), to = Math.Min(all.Count - 1, Math.Max(cur, 0) + after);
        var shown = new List<string>();
        for (int i = from; i <= to; i++) shown.Add(all[i].Text);
        return (shown, cur < 0 ? -1 : cur - from);
    }

    /// Seconds until the next line starts (so the widget redraws right then), or -1.
    public static double NextIn(double offset)
    {
        if (!synced || !Media.Playing) return -1;
        double pos = Media.Position + offset;
        foreach (var l in lines) if (l.At > pos) return l.At - pos;
        return -1;
    }
}
