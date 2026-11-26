using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Glyph.Options;

namespace Glyph;

/// One hotkey (config\hotkeys.toml): a key combination and the action it runs.
internal sealed class HotkeyOptions
{
    [Category("Hotkey"), Description("The keys, e.g. alt+space, win+alt+left, ctrl+shift+f5. Modifiers: win, ctrl, alt, shift. Keys: a-z, 0-9, f1-f24, left, right, up, down, space, enter, tab, esc, backspace, delete, insert, home, end, pageup, pagedown, minus, equals, comma, period, slash, backslash, semicolon, quote, backtick, [, ], numpad0-9, printscreen, pause.")]
    public string Keys { get; set; } = "";
    [Category("Hotkey"), DisplayName("Does"), TypeConverter(typeof(Glyph.UI.ActionChoices)), Description("What it does: launcher, tile:toggle, tile:focus:left, tile:move:right, tile:float, tile:layout:next, tile:ratio:+5, theme:soft, plus every click action (media:toggle, volume:+5, audio:device:next, run:program, a file or website…).")]
    public string Action { get; set; } = "";
    [Category("Hotkey"), Description("Turn this hotkey on or off.")] public bool Enabled { get; set; } = true;

    public override string ToString() => $"{Keys}  →  {Action}";
}

internal static class HotkeyStore
{
    public static string FilePath => Path.Combine(Paths.Config, "hotkeys.toml");

    // Win+Alt+arrows, T, F, N and K are taken on many PCs (Windows, Teams, PowerToys), so the
    // tiling keys use Win+Ctrl+Alt; moving windows uses Win+Alt+Shift+arrows.
    public static readonly (string keys, string action)[] Defaults =
    {
        ("alt+space", "launcher"),
        ("win+ctrl+alt+t", "tile:toggle"),
        ("win+ctrl+alt+left", "tile:focus:left"), ("win+ctrl+alt+right", "tile:focus:right"), ("win+ctrl+alt+up", "tile:focus:up"), ("win+ctrl+alt+down", "tile:focus:down"),
        ("win+alt+shift+left", "tile:move:left"), ("win+alt+shift+right", "tile:move:right"), ("win+alt+shift+up", "tile:move:up"), ("win+alt+shift+down", "tile:move:down"),
        ("win+ctrl+alt+enter", "tile:master"),
        ("win+ctrl+alt+f", "tile:float"),
        ("win+ctrl+alt+n", "tile:layout:next"),
        ("win+ctrl+alt+minus", "tile:ratio:-5"), ("win+ctrl+alt+equals", "tile:ratio:+5"),
    };

    public static List<HotkeyOptions> Load(List<string> problems)
    {
        if (!File.Exists(FilePath))
        {
            var d = Defaults.Select(x => new HotkeyOptions { Keys = x.keys, Action = x.action }).ToList();
            try { Save(d); } catch (Exception e) { Log.Write("hotkeys.toml: " + e.Message); }
            return d;
        }
        var list = new List<HotkeyOptions>();
        var file = ConfigFile.Load(FilePath, problems);
        foreach (var n in file.Top.Array("hotkey"))
        {
            var h = new HotkeyOptions();
            Binder.Read(h, n, null);
            if (h.Keys.Length == 0) continue;
            if (Hotkeys.Parse(h.Keys) == null) problems.Add($"hotkeys.toml: can't read the keys \"{h.Keys}\"");
            list.Add(h);
        }
        file.ReportUnknown();
        return list;
    }

    public static void Save(List<HotkeyOptions> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hotkeys: key combinations that work anywhere. Easiest to change in the settings window");
        sb.AppendLine("# (tray icon > Settings > Hotkeys); you can also edit this file.");
        foreach (var h in list) Binder.WriteTable(sb, "hotkey", h);
        SafeFile.Write(FilePath, sb.ToString());
    }
}

/// Registers hotkeys with Windows (on the app's hidden window) and says which ones another program already took.
internal static class Hotkeys
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    public const int WM_HOTKEY = 0x0312;
    const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    static readonly Dictionary<int, string> actions = new();
    static string registeredKey = "";
    static IntPtr owner;
    public static readonly List<string> Taken = new();

    public static string? ActionFor(int id) => actions.TryGetValue(id, out var a) ? a : null;

    /// (re)registers the list; does nothing if it didn't change
    public static void Apply(IntPtr window, List<HotkeyOptions> list)
    {
        var on = list.Where(h => h.Enabled && h.Keys.Length > 0 && h.Action.Length > 0).ToList();
        var key = string.Join("\n", on.Select(h => h.Keys.ToLowerInvariant() + "=" + h.Action));
        if (key == registeredKey && window == owner) return;
        Clear();
        owner = window; registeredKey = key;
        int id = 1;
        foreach (var h in on)
        {
            var p = Parse(h.Keys);
            if (p == null) { Log.Write($"hotkey \"{h.Keys}\": can't read the keys"); continue; }
            if (RegisterHotKey(window, id, p.Value.mods | MOD_NOREPEAT, p.Value.vk)) actions[id] = h.Action;
            else { Taken.Add(h.Keys); Log.Write($"hotkey {h.Keys} ({h.Action}): already used by Windows or another program"); }
            id++;
        }
        try { File.WriteAllLines(Path.Combine(Paths.Logs, "hotkeys-taken.txt"), Taken); } catch { }
    }

    public static void Clear()
    {
        foreach (var id in actions.Keys) UnregisterHotKey(owner, id);
        actions.Clear(); Taken.Clear(); registeredKey = "";
    }

    /// "win+alt+left" -> (MOD_WIN|MOD_ALT, VK_LEFT)
    public static (uint mods, uint vk)? Parse(string keys)
    {
        uint mods = 0, vk = 0;
        // "+" itself can't be a key name here (it separates them); use "equals" / "plus"
        foreach (var raw in keys.ToLowerInvariant().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw)
            {
                case "win" or "super" or "meta": mods |= MOD_WIN; continue;
                case "ctrl" or "control": mods |= MOD_CONTROL; continue;
                case "alt": mods |= MOD_ALT; continue;
                case "shift": mods |= MOD_SHIFT; continue;
            }
            if (vk != 0) return null;   // two ordinary keys
            vk = Vk(raw);
            if (vk == 0) return null;
        }
        return vk == 0 ? null : (mods, vk);
    }

    public static uint Vk(string k) => k switch
    {
        "space" => 0x20, "enter" or "return" => 0x0D, "tab" => 0x09, "esc" or "escape" => 0x1B, "backspace" => 0x08,
        "delete" or "del" => 0x2E, "insert" or "ins" => 0x2D, "home" => 0x24, "end" => 0x23, "pageup" or "pgup" => 0x21, "pagedown" or "pgdn" => 0x22,
        "left" => 0x25, "up" => 0x26, "right" => 0x27, "down" => 0x28,
        "minus" or "-" => 0xBD, "equals" or "plus" or "=" => 0xBB, "comma" or "," => 0xBC, "period" or "." => 0xBE, "slash" or "/" => 0xBF,
        "backslash" or "\\" => 0xDC, "semicolon" or ";" => 0xBA, "quote" or "'" => 0xDE, "backtick" or "`" => 0xC0, "[" or "lbracket" => 0xDB, "]" or "rbracket" => 0xDD,
        "printscreen" or "prtsc" => 0x2C, "pause" => 0x13, "capslock" => 0x14,
        "volumeup" => 0xAF, "volumedown" => 0xAE, "mute" => 0xAD, "playpause" => 0xB3, "nexttrack" => 0xB0, "prevtrack" => 0xB1,
        var s when s.Length == 1 && (char.IsAsciiLetter(s[0]) || char.IsAsciiDigit(s[0])) => char.ToUpperInvariant(s[0]),
        var s when s.StartsWith("numpad") && s.Length == 7 && char.IsAsciiDigit(s[6]) => (uint)(0x60 + (s[6] - '0')),
        var s when s.Length >= 2 && s[0] == 'f' && int.TryParse(s[1..], out var f) && f is >= 1 and <= 24 => (uint)(0x6F + f),
        _ => 0,
    };

    /// For the settings window's "press the keys" box: the name of a key Windows Forms reports.
    public static string? Name(System.Windows.Forms.Keys k) => k switch
    {
        >= System.Windows.Forms.Keys.A and <= System.Windows.Forms.Keys.Z => ((char)('a' + (k - System.Windows.Forms.Keys.A))).ToString(),
        >= System.Windows.Forms.Keys.D0 and <= System.Windows.Forms.Keys.D9 => ((char)('0' + (k - System.Windows.Forms.Keys.D0))).ToString(),
        >= System.Windows.Forms.Keys.F1 and <= System.Windows.Forms.Keys.F24 => "f" + (k - System.Windows.Forms.Keys.F1 + 1),
        >= System.Windows.Forms.Keys.NumPad0 and <= System.Windows.Forms.Keys.NumPad9 => "numpad" + (k - System.Windows.Forms.Keys.NumPad0),
        System.Windows.Forms.Keys.Space => "space", System.Windows.Forms.Keys.Enter => "enter", System.Windows.Forms.Keys.Tab => "tab",
        System.Windows.Forms.Keys.Escape => "esc", System.Windows.Forms.Keys.Back => "backspace", System.Windows.Forms.Keys.Delete => "delete",
        System.Windows.Forms.Keys.Insert => "insert", System.Windows.Forms.Keys.Home => "home", System.Windows.Forms.Keys.End => "end",
        System.Windows.Forms.Keys.PageUp => "pageup", System.Windows.Forms.Keys.PageDown => "pagedown",
        System.Windows.Forms.Keys.Left => "left", System.Windows.Forms.Keys.Right => "right", System.Windows.Forms.Keys.Up => "up", System.Windows.Forms.Keys.Down => "down",
        System.Windows.Forms.Keys.OemMinus => "minus", System.Windows.Forms.Keys.Oemplus => "equals", System.Windows.Forms.Keys.Oemcomma => "comma",
        System.Windows.Forms.Keys.OemPeriod => "period", System.Windows.Forms.Keys.OemQuestion => "slash", System.Windows.Forms.Keys.OemPipe => "backslash",
        System.Windows.Forms.Keys.OemSemicolon => "semicolon", System.Windows.Forms.Keys.OemQuotes => "quote", System.Windows.Forms.Keys.Oemtilde => "backtick",
        System.Windows.Forms.Keys.OemOpenBrackets => "[", System.Windows.Forms.Keys.OemCloseBrackets => "]",
        System.Windows.Forms.Keys.PrintScreen => "printscreen", System.Windows.Forms.Keys.Pause => "pause",
        _ => null,
    };
}
