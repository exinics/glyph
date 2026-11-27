using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Text;
using Glyph.Options;

namespace Glyph.Widgets;

using FileEditor = System.Windows.Forms.Design.FileNameEditor;

/// One shortcut. The list is shared by all themes (config\shortcuts.toml).
internal sealed class Shortcut
{
    [Category("Shortcut"), Description("The name shown under the icon.")] public string Name { get; set; } = "";
    [Category("Shortcut"), Description("What it opens: a program, file, folder, shortcut (.lnk), website or shell: location."), Editor(typeof(FileEditor), typeof(UITypeEditor))]
    public string Target { get; set; } = "";
    [Category("Shortcut"), Description("Extra command-line arguments (optional).")] public string Arguments { get; set; } = "";
    [Category("Shortcut"), DisplayName("Start in"), Description("Working folder (optional).")] public string StartIn { get; set; } = "";
    [Category("Shortcut"), Description("Optional group name; a panel can show just one group, and a menu shows each group as a tab.")] public string Group { get; set; } = "";
    [Category("Shortcut"), Description("A short line about it (optional), shown by menus under the name.")] public string Description { get; set; } = "";
    [Category("Shortcut"), Description("Your own picture for the icon (PNG/JPG/ICO). Empty = the app's own icon."), Editor(typeof(FileEditor), typeof(UITypeEditor))]
    public string Image { get; set; } = "";

    public override string ToString() => Name.Length > 0 ? Name : Target;

    /// Key for the extracted-icon cache.
    public string IconKey
    {
        get
        {
            var bytes = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(Target.ToLowerInvariant()));
            return System.Convert.ToHexString(bytes)[..16];
        }
    }

    public void Launch()
    {
        try
        {
            var psi = new ProcessStartInfo(Target) { UseShellExecute = true, Arguments = Arguments };
            if (StartIn.Length > 0) psi.WorkingDirectory = StartIn;
            Process.Start(psi);
        }
        catch (Exception e)
        {
            Log.Write($"open '{Name}' ({Target}): {e.Message}");
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Target}\"") { UseShellExecute = true }); } catch { }
        }
    }
}

/// How a group of shortcuts is shown as a tab in a menu (shortcuts.toml [[group]]). Groups without one still work.
internal sealed class ShortcutGroup
{
    [Category("Group"), Description("The group's name, as typed in the shortcuts' Group.")] public string Name { get; set; } = "";
    [Category("Group"), Description("An icon for its tab: {icon:name} from the Icons page (e.g. {icon:sports_esports}), or any text. Empty = none.")] public string Icon { get; set; } = "";
    [Category("Group"), Description("A line under the group's heading in a menu (e.g. GAMES / PLAY).")] public string Subtitle { get; set; } = "";
    [Category("Group"), Description("A sentence about the group, under the subtitle.")] public string Description { get; set; } = "";
    [Category("Group"), DisplayName("Label"), Description("The tab's text if it should differ from the name. Empty = the name.")] public string Label { get; set; } = "";
    public override string ToString() => Name;
}

internal static class ShortcutStore
{
    public static string FilePath => Path.Combine(Paths.Config, "shortcuts.toml");

    /// The [[group]] tables from the last load (kept so Save writes them back).
    public static List<ShortcutGroup> Groups { get; set; } = new();

    public static List<Shortcut> Load(List<string> problems)
    {
        var list = new List<Shortcut>();
        if (!File.Exists(FilePath)) return list;
        var file = ConfigFile.Load(FilePath, problems);
        foreach (var n in file.Top.Array("shortcut"))
        {
            var s = new Shortcut();
            Binder.Read(s, n, null);
            if (s.Target.Length > 0) list.Add(s);
        }
        var groups = new List<ShortcutGroup>();
        foreach (var n in file.Top.Array("group"))
        {
            var g = new ShortcutGroup();
            Binder.Read(g, n, null);
            if (g.Name.Trim().Length > 0) groups.Add(g);
        }
        Groups = groups;
        file.ReportUnknown();
        return list;
    }

    public static void Save(List<Shortcut> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Your shortcuts, shared by every theme. Easiest to change in the settings window");
        sb.AppendLine("# (tray icon > Settings > Shortcuts); you can also edit this file.");
        sb.AppendLine("# [[group]] tables (optional) give a group an icon and text for menus.");
        foreach (var g in Groups) Binder.WriteTable(sb, "group", g);
        foreach (var s in list) Binder.WriteTable(sb, "shortcut", s);
        SafeFile.Write(FilePath, sb.ToString());
    }

    /// Group names in use, in order: [[group]] tables first, then any other group the shortcuts name.
    public static List<string> GroupNames(IEnumerable<Shortcut> list)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in Groups) if (seen.Add(g.Name.Trim())) names.Add(g.Name.Trim());
        foreach (var s in list) if (s.Group.Trim().Length > 0 && seen.Add(s.Group.Trim())) names.Add(s.Group.Trim());
        return names;
    }

    public static ShortcutGroup? Group(string name) => Groups.FirstOrDefault(g => g.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

    /// Everything on the desktop (yours and the shared Public one), plus the Recycle Bin.
    public static List<Shortcut> FromDesktop()
    {
        var list = new List<Shortcut> { new() { Name = "Recycle Bin", Target = "shell:RecycleBinFolder" } };
        var dirs = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) };
        foreach (var d in dirs.Where(Directory.Exists).Distinct())
        {
            foreach (var f in Directory.EnumerateFileSystemEntries(d).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(f);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if ((File.GetAttributes(f) & FileAttributes.Hidden) != 0) continue;
                var shown = Directory.Exists(f) ? name : Path.GetFileNameWithoutExtension(f);
                list.Add(new Shortcut { Name = shown, Target = f });
            }
        }
        return list;
    }
}
