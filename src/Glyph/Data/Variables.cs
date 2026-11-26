using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Design;
using System.Text;
using Glyph.Options;

namespace Glyph.Data;

/// A value you make yourself, used in any text as {name}.
internal sealed class VariableOptions
{
    [Category("Variable"), Description("The name to use in text: a variable called mycpu is written {mycpu}. Letters, numbers and _.")]
    public string Name { get; set; } = "my_value";
    [Category("Variable"), Description("text = built from other values; command = the output of a program; file = the contents of a text file."), Choices("text", "command", "file"), TypeConverter(typeof(ChoicesConverter))]
    public string Kind { get; set; } = "text";
    [Category("Variable"), Description("For text: what it's made of, e.g. {cpu|pad:3}% or {media_artist} – {media_title}. Filters: " + Fmt.FilterHelp)]
    public string Text { get; set; } = "{cpu|fmt:0}%";
    [Category("Variable"), Description("For command: a program and its arguments; what it prints becomes the value.")]
    public string Command { get; set; } = "";
    [Category("Variable"), Description("For file: a text file whose contents become the value."), Editor(typeof(System.Windows.Forms.Design.FileNameEditor), typeof(UITypeEditor))]
    public string File { get; set; } = "";
    [Category("Variable"), Description("For command and file: read it again every this many seconds."), Range(1, 86400)]
    public int Interval { get; set; } = 30;
    [Category("Variable"), Description("For command and file: keep only the first line.")]
    public bool FirstLine { get; set; } = true;
    public override string ToString() => "{" + Name + "}  " + Kind;
}

/// The theme's own variables (variables.toml in the theme folder).
internal static class Variables
{
    sealed class Live { public VariableOptions O = new(); public volatile string Value = ""; public DateTime Next; public volatile bool Busy; }
    static volatile Dictionary<string, Live> vars = new(StringComparer.OrdinalIgnoreCase);
    [ThreadStatic] static int depth;

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "variables.toml");

    public static List<VariableOptions> LoadList(string theme, List<string> problems)
    {
        var list = new List<VariableOptions>();
        if (!System.IO.File.Exists(FilePath(theme))) return list;
        var file = ConfigFile.Load(FilePath(theme), problems);
        foreach (var n in file.Top.Array("variable")) { var v = new VariableOptions(); Binder.Read(v, n, null); list.Add(v); }
        file.ReportUnknown();
        return list;
    }

    public static void Save(string theme, List<VariableOptions> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Your own values for this theme, used in any text as {name}. Easiest to change in the");
        sb.AppendLine("# settings window (Variables page).");
        foreach (var v in list) Binder.WriteTable(sb, "variable", v);
        SafeFile.Write(FilePath(theme), sb.ToString());
    }

    /// Makes these the active variables (called when a theme loads).
    public static void Use(IEnumerable<VariableOptions> list)
    {
        var d = new Dictionary<string, Live>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in list) if (v.Name.Trim().Length > 0) d[v.Name.Trim()] = new Live { O = v };
        vars = d;
    }

    public static string? Value(string name)
    {
        if (!vars.TryGetValue(name, out var v)) return null;
        switch (v.O.Kind)
        {
            case "command":
            case "file":
                Refresh(v);
                return v.Value;
            default:
                if (depth > 5) return "";   // a variable that uses itself
                depth++;
                try { return Fmt.Text(v.O.Text, Stats.Value); }
                finally { depth--; }
        }
    }

    static void Refresh(Live v)
    {
        if (v.Busy || DateTime.UtcNow < v.Next) return;
        v.Busy = true; v.Next = DateTime.UtcNow.AddSeconds(Math.Max(1, v.O.Interval));
        Task.Run(() =>
        {
            try
            {
                string text;
                if (v.O.Kind == "file")
                {
                    var path = Paths.FindAsset(v.O.File, null, "art");
                    text = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "(file not found)";
                }
                else
                {
                    var (file, args) = Actions.SplitCommand(v.O.Command);
                    using var p = Process.Start(new ProcessStartInfo(file, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    if (!p.WaitForExit(15000)) { try { p.Kill(true); } catch { } text = "(took too long)"; }
                    else text = outTask.Result;
                }
                text = text.Replace("\r", "").Trim();
                if (v.O.FirstLine) text = text.Split('\n')[0];
                v.Value = text;
            }
            catch (Exception e) { v.Value = "(" + e.Message + ")"; }
            finally { v.Busy = false; Stats.Version++; }
        });
    }
}
