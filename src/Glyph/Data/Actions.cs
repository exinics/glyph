using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Glyph.Data;

/// Windows' virtual desktops (read from the registry; switched with Ctrl+Win+Left/Right).
internal static class VirtualDesktops
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    public static (int current, int count, string name) Current()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key);
            if (k?.GetValue("VirtualDesktopIDs") is not byte[] ids || ids.Length < 16) return (1, 1, "");
            int count = ids.Length / 16;
            if (k.GetValue("CurrentVirtualDesktop") is not byte[] cur) return (1, count, "");
            for (int i = 0; i < count; i++)
                if (ids.AsSpan(i * 16, 16).SequenceEqual(cur))
                {
                    var g = new Guid(cur);
                    using var dk = Registry.CurrentUser.OpenSubKey($@"{Key}\Desktops\{{{g.ToString().ToUpperInvariant()}}}");
                    return (i + 1, count, dk?.GetValue("Name") as string ?? "");
                }
            return (1, count, "");
        }
        catch { return (1, 1, ""); }
    }

    public static void GoTo(int n)
    {
        var (cur, count, _) = Current();
        n = Math.Clamp(n, 1, 20);
        int steps = Math.Min(n, count) - cur;
        for (int i = 0; i < Math.Abs(steps); i++) { Keys.Press(steps > 0 ? "ctrl+win+right" : "ctrl+win+left"); Thread.Sleep(60); }
        // a desktop that doesn't exist yet is made (like workspaces on Linux); each new one opens straight away
        for (int i = count; i < n; i++) { Keys.Press("ctrl+win+d"); Thread.Sleep(120); }
    }
}

/// Sends key combinations like "win+n" or "ctrl+win+right".
internal static class Keys
{
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public KEYBDINPUT Ki; public long Pad; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk, Scan; public uint Flags, Time; public IntPtr Extra; }
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    static ushort Vk(string k) => k.ToLowerInvariant() switch
    {
        "win" => 0x5B, "ctrl" or "control" => 0x11, "alt" => 0x12, "shift" => 0x10, "tab" => 0x09, "esc" or "escape" => 0x1B,
        "enter" => 0x0D, "space" => 0x20, "left" => 0x25, "up" => 0x26, "right" => 0x27, "down" => 0x28, "delete" or "del" => 0x2E,
        "printscreen" => 0x2C, "volumeup" => 0xAF, "volumedown" => 0xAE, "mute" => 0xAD, "playpause" => 0xB3, "next" => 0xB0, "prev" => 0xB1,
        var s when s.Length == 1 && char.IsLetterOrDigit(s[0]) => (ushort)char.ToUpperInvariant(s[0]),
        var s when s.Length >= 2 && s[0] == 'f' && int.TryParse(s[1..], out var f) && f is >= 1 and <= 24 => (ushort)(0x6F + f),
        _ => 0,
    };

    public static void Press(string combo)
    {
        var keys = combo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Vk).Where(v => v != 0).ToList();
        if (keys.Count == 0) return;
        var list = new List<INPUT>();
        foreach (var k in keys) list.Add(new INPUT { Type = 1, Ki = new KEYBDINPUT { Vk = k } });
        for (int i = keys.Count - 1; i >= 0; i--) list.Add(new INPUT { Type = 1, Ki = new KEYBDINPUT { Vk = keys[i], Flags = 2 } });
        SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<INPUT>());
    }
}

/// What clicking something does. Written in the settings as text:
///   media:toggle | media:next | media:prev      play/pause, next and previous track
///   volume:mute | volume:up | volume:down       volume (up/down by 5%)
///   desktop:next | desktop:prev | desktop:3     switch virtual desktop
///   keys:win+n                                  press a key combination
///   run:program arguments                       start a program with arguments
///   settings                                    open Glyph's settings
///   anything else                               opened like a double-click: a file, folder, program, website or ms-settings: page
internal static class Actions
{
    /// Actions only the running Glyph can do (launcher, tiling, themes...); set by the app.
    public static Func<string, string, bool>? Internal;

    public const string Help = "What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open.";

    public static void Run(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        action = action.Trim();
        try
        {
            int colon = action.IndexOf(':');
            string kind = colon > 0 ? action[..colon].ToLowerInvariant() : action.ToLowerInvariant(), arg = colon > 0 ? action[(colon + 1)..].Trim() : "";
            if (Internal != null && Internal(kind, arg)) return;
            if (Internal == null && kind is "launcher" or "tile" or "theme" or "glyph" or "shortcut" or "window" or "copy" or "runas" && Native.SendAction(action)) return;
            switch (kind)
            {
                case "media": Media.Control(arg); return;
                case "list":   // list:toggle:<name>:<n> = tick item n; list:edit:<style>:<n>:<name> = edit box (n = -1: add)
                    {
                        var parts = arg.Split(':');
                        if (parts.Length >= 3 && parts[0] == "toggle" && int.TryParse(parts[^1], out var n)) Lists.Toggle(string.Join(":", parts[1..^1]), n);
                        else if (parts.Length >= 4 && parts[0] == "edit" && int.TryParse(parts[2], out var k)) Lists.Edit(parts[1], string.Join(":", parts[3..]), k);
                        return;
                    }
                case "weather":   // weather:city:<widget name> = ask for the city and save it to that weather widget
                    {
                        if (!arg.StartsWith("city:")) return;
                        var wname = arg[5..];
                        var theme = Glyph.Ui.Prompt.Theme;
                        if (theme == null) return;
                        var file = Glyph.Widgets.WidgetsFile.Load(theme, new List<string>());
                        var w = file.Widgets.OfType<Glyph.Widgets.WeatherOptions>().FirstOrDefault(x => x.Name == wname) ?? file.Widgets.OfType<Glyph.Widgets.WeatherOptions>().FirstOrDefault();
                        if (w == null) return;
                        Glyph.Ui.Prompt.Ask("weather", new[] { ("Your city (e.g. London)", w.City) }, v =>
                        {
                            w.City = v[0]; w.Latitude = 0; w.Longitude = 0;
                            file.Save(theme.Name);
                        });
                        return;
                    }
                case "volume":
                    if (arg == "mute") Audio.ToggleMute(); else if (arg == "up") Audio.Change(0.05f); else if (arg == "down") Audio.Change(-0.05f);
                    else if ((arg.StartsWith('+') || arg.StartsWith('-')) && float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv)) Audio.Change(dv / 100f);
                    else if (float.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) Audio.Set(v > 1 ? v / 100f : v);
                    Stats.Use();
                    Mixer.RefreshIfUsed();
                    return;
                case "audio": Mixer.Action(arg); return;
                case "desktop":
                    if (arg == "next") Keys.Press("ctrl+win+right"); else if (arg is "prev" or "previous") Keys.Press("ctrl+win+left");
                    else if (int.TryParse(arg, out var n)) VirtualDesktops.GoTo(n);
                    return;
                case "keys": Keys.Press(arg); return;
                case "settings": Process.Start(new ProcessStartInfo(Paths.Exe, "--settings") { UseShellExecute = false }); return;
                case "run":
                    {
                        var (file, args) = SplitCommand(arg);
                        Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
                        return;
                    }
            }
            Process.Start(new ProcessStartInfo(action) { UseShellExecute = true });
        }
        catch (Exception e) { Log.Write($"action '{action}': {e.Message}"); }
    }

    /// "C:\Program Files\x.exe" -a b  →  (file, "-a b")
    public static (string, string) SplitCommand(string s)
    {
        s = s.Trim();
        if (s.StartsWith('"')) { int e = s.IndexOf('"', 1); if (e > 0) return (s[1..e], s[(e + 1)..].Trim()); }
        int sp = s.IndexOf(' ');
        return sp < 0 ? (s, "") : (s[..sp], s[(sp + 1)..]);
    }
}
