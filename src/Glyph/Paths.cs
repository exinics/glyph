namespace Glyph;

/// Where Glyph's folders are. The Glyph folder is the first folder, walking up from the
/// program, that contains a "config" folder (or GLYPH_HOME if that is set).
internal static class Paths
{
    public static readonly string Home = FindHome();
    public static string Config => Path.Combine(Home, "config");
    public static string Themes => Path.Combine(Config, "themes");
    public static string Art => Path.Combine(Config, "art");
    public static string Clips => Path.Combine(Config, "clips");
    public static string Icons => Path.Combine(Config, "icons");
    public static string IconCache => Path.Combine(Icons, "cache");
    public static string Images => Path.Combine(Config, "images");
    public static string Logs => Path.Combine(Home, "logs");
    public static string Guide => Path.Combine(Config, "GUIDE.md");
    public static string State => Path.Combine(Logs, "state.txt");
    public static string Exe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Glyph.exe");

    public static string ThemeDir(string name) => Path.Combine(Themes, name);

    static string FindHome()
    {
        var env = Environment.GetEnvironmentVariable("GLYPH_HOME");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "config"))) return env;
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "config"))) d = d.Parent;
        return d?.FullName ?? AppContext.BaseDirectory;
    }

    /// Resolves a file named in the config: absolute paths stay, others are looked up in the given folder.
    public static string Resolve(string file, string folder) =>
        Path.IsPathRooted(file) ? file : Path.Combine(folder, file);

    /// Finds a file for a theme: the theme's own folder (and its sub-folder of that kind) first,
    /// then the shared config folder of that kind. `kind` is "art", "clips", "images" or "icons".
    public static string FindAsset(string file, string? theme, string kind)
    {
        if (Path.IsPathRooted(file)) return file;
        var shared = Path.Combine(Config, kind);
        var tries = new List<string>();
        if (theme != null) { tries.Add(Path.Combine(ThemeDir(theme), kind, file)); tries.Add(Path.Combine(ThemeDir(theme), file)); }
        tries.Add(Path.Combine(shared, file));
        return tries.FirstOrDefault(File.Exists) ?? tries.Last();
    }
}

internal static class Log
{
    static readonly object Gate = new();
    public static readonly bool Debug = Environment.GetEnvironmentVariable("GLYPH_DEBUG") == "1";
    public static void D(string msg) { if (Debug) Write("debug: " + msg); }
    public static void Write(string msg)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Paths.Logs);
                var f = Path.Combine(Paths.Logs, "glyph.log");
                if (File.Exists(f) && new FileInfo(f).Length > 1_000_000) File.Delete(f);
                File.AppendAllText(f, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
