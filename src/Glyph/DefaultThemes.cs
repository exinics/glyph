using Glyph.Widgets;

namespace Glyph;

/// First run, and the theme that comes with Glyph. Glyph ships one theme, "operator" (the repo's themes\ folder,
/// copied next to Glyph.exe as bin\themes). It's copied into config\themes only when there are no themes at all:
/// updates never add, change or bring back themes.
internal static class DefaultThemes
{
    public const string Main = "operator";

    static string Shipped => Path.Combine(AppContext.BaseDirectory, "themes");

    /// The themes that come with Glyph.
    public static string[] Names => Directory.Exists(Shipped) ? Directory.GetDirectories(Shipped).Select(Path.GetFileName).OfType<string>().ToArray() : Array.Empty<string>();

    /// Copies a built-in theme into config\themes (overwrites that theme's files).
    public static void Write(string name)
    {
        var from = Path.Combine(Shipped, name);
        if (!Directory.Exists(from)) throw new DirectoryNotFoundException($"no built-in theme \"{name}\" next to Glyph.exe");
        Copy(from, Paths.ThemeDir(name));
    }

    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    /// First run: the built-in theme, your desktop's shortcuts, default settings.
    public static void EnsureExist()
    {
        bool any = Directory.Exists(Paths.Themes) && Directory.GetDirectories(Paths.Themes).Any(d => File.Exists(Path.Combine(d, "theme.toml")));
        if (!any) foreach (var n in Names) Write(n);
        if (!File.Exists(ShortcutStore.FilePath)) ShortcutStore.Save(ShortcutStore.FromDesktop());
        if (!File.Exists(AppSettings.FilePath)) new AppSettings().Save();
        Directory.CreateDirectory(Paths.Art); Directory.CreateDirectory(Paths.Clips); Directory.CreateDirectory(Paths.Images); Directory.CreateDirectory(Paths.Icons);
        var logo = Path.Combine(Paths.Art, "glyph.txt");
        if (!File.Exists(logo)) File.WriteAllText(logo, Logo);
    }

    const string Logo = """
 ██████╗ ██╗  ██╗   ██╗██████╗ ██╗  ██╗
██╔════╝ ██║  ╚██╗ ██╔╝██╔══██╗██║  ██║
██║  ███╗██║   ╚████╔╝ ██████╔╝███████║
██║   ██║██║    ╚██╔╝  ██╔═══╝ ██╔══██║
╚██████╔╝███████╗██║   ██║     ██║  ██║
 ╚═════╝ ╚══════╝╚═╝   ╚═╝     ╚═╝  ╚═╝

""";
}
