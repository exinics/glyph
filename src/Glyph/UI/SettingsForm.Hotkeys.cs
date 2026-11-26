using System.Runtime.InteropServices;

namespace Glyph.UI;

/// The Hotkeys page: key combinations that work anywhere, and what they do.
internal sealed partial class SettingsForm
{
    List<HotkeyOptions> hotkeys = new();
    bool dirtyHotkeys;
    readonly ListView hkList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
    readonly PropertyGrid hkGrid = Grid();

    void MarkHotkeys() => Changed(ref dirtyHotkeys);

    Control BuildHotkeys()
    {
        var split = Split(700);
        hkList.Columns.Add("Keys", 200);
        hkList.Columns.Add("Does", 260);
        hkList.Columns.Add("", 200);
        var buttons = Buttons(92);
        Btn(buttons, "Add hotkey…", AddHotkey, true);
        Btn(buttons, "Change keys…", () => { if (SelectedHotkey() is HotkeyOptions h && RecordKeys(h.Keys) is string k) { h.Keys = k; AfterHotkeys(h); } });
        Btn(buttons, "Remove", () =>
        {
            if (SelectedHotkey() is not HotkeyOptions h) return;
            int i = hotkeys.IndexOf(h); hotkeys.Remove(h);
            AfterHotkeys(hotkeys.Count > 0 ? hotkeys[Math.Min(i, hotkeys.Count - 1)] : null);
        });
        Btn(buttons, "Put back the defaults", () =>
        {
            if (MessageBox.Show(this, "Replace your hotkeys with Glyph's defaults?", "Hotkeys", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            hotkeys = HotkeyStore.Defaults.Select(d => new HotkeyOptions { Keys = d.keys, Action = d.action }).ToList();
            AfterHotkeys(null);
        });
        split.Panel1.Controls.Add(hkList);
        split.Panel1.Controls.Add(Hint("Keys that work anywhere, even while Glyph's desktop is hidden. Alt+Space opens the launcher; the Win+Ctrl+Alt keys control tiling (General > Tiling)."));
        split.Panel1.Controls.Add(buttons);
        split.Panel2.Controls.Add(hkGrid);
        split.Panel2.Controls.Add(Hint("The selected hotkey. \"Does\" can be any click action: pick one from the list or type your own (e.g. run:notepad, theme:soft, volume:+10).", 48));
        hkList.SelectedIndexChanged += (_, _) => hkGrid.SelectedObject = SelectedHotkey();
        hkList.DoubleClick += (_, _) => { if (SelectedHotkey() is HotkeyOptions h && RecordKeys(h.Keys) is string k) { h.Keys = k; AfterHotkeys(h); } };
        hkGrid.PropertyValueChanged += (_, _) => { if (SelectedHotkey() is HotkeyOptions h) AfterHotkeys(h); };
        return split;
    }

    HotkeyOptions? SelectedHotkey() => hkList.SelectedItems.Count > 0 ? hkList.SelectedItems[0].Tag as HotkeyOptions : null;

    void RefreshHotkeys(HotkeyOptions? select = null)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { var f = Path.Combine(Paths.Logs, "hotkeys-taken.txt"); if (File.Exists(f)) foreach (var l in File.ReadAllLines(f)) taken.Add(l.Trim()); } catch { }
        hkList.BeginUpdate();
        hkList.Items.Clear();
        foreach (var h in hotkeys)
        {
            string note = !h.Enabled ? "off" : Hotkeys.Parse(h.Keys) == null ? "can't read these keys" : taken.Contains(h.Keys) ? "taken by another program" : "";
            var it = new ListViewItem(new[] { h.Keys, h.Action, note }) { Tag = h };
            if (note.Length > 0) it.ForeColor = note == "off" ? Dark.Dim : Dark.Accent;
            hkList.Items.Add(it);
            if (h == select) { it.Selected = true; it.EnsureVisible(); }
        }
        hkList.EndUpdate();
        hkGrid.SelectedObject = SelectedHotkey();
    }

    void AfterHotkeys(HotkeyOptions? select) { RefreshHotkeys(select); MarkHotkeys(); }

    void AddHotkey()
    {
        var keys = RecordKeys("");
        if (keys == null) return;
        var h = new HotkeyOptions { Keys = keys, Action = "launcher" };
        hotkeys.Add(h);
        AfterHotkeys(h);
    }

    [DllImport("user32.dll")] static extern short GetKeyState(int vk);

    /// A small window that shows the keys you press (or lets you type them) and returns e.g. "win+alt+left".
    string? RecordKeys(string current)
    {
        using var f = new Form
        {
            Text = "Press the keys", Width = 520, Height = 230, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent, KeyPreview = true, Font = Font, ShowInTaskbar = false,
        };
        var label = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 12, 12, 0), Text = "Press the key combination now, or type it (e.g. ctrl+alt+k). Keys Glyph or Windows already use may act instead of showing here: type those." };
        var box = new TextBox { Dock = DockStyle.Top, Font = new Font("Cascadia Mono", 14f), Text = current };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100, Height = 34 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100, Height = 34 };
        var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        row.Controls.Add(cancel); row.Controls.Add(ok);
        f.Controls.Add(box); f.Controls.Add(label); f.Controls.Add(row);
        f.AcceptButton = ok; f.CancelButton = cancel;
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
            bool win = (GetKeyState(0x5B) & 0x8000) != 0 || (GetKeyState(0x5C) & 0x8000) != 0;
            if (!win && !e.Control && !e.Alt && e.KeyCode is Keys.Back or Keys.Delete or Keys.Left or Keys.Right or Keys.Home or Keys.End) return;   // editing the text
            if (!win && !e.Control && !e.Alt && !e.Shift && e.KeyCode is not (>= Keys.F1 and <= Keys.F24)) return;   // typing
            var name = Hotkeys.Name(e.KeyCode);
            if (name == null) return;
            var parts = new List<string>();
            if (win) parts.Add("win");
            if (e.Control) parts.Add("ctrl");
            if (e.Alt) parts.Add("alt");
            if (e.Shift) parts.Add("shift");
            parts.Add(name);
            box.Text = string.Join("+", parts);
            box.SelectionStart = box.Text.Length;
            e.SuppressKeyPress = true; e.Handled = true;
        };
        Dark.Apply(f);
        if (f.ShowDialog(this) != DialogResult.OK) return null;
        var keys = box.Text.Trim().ToLowerInvariant();
        if (Hotkeys.Parse(keys) == null) { MessageBox.Show(this, $"Can't read \"{keys}\". Write it like win+alt+left or ctrl+shift+f5.", "Hotkeys"); return null; }
        return keys;
    }
}

/// The common actions, offered in the hotkey's "Does" box (you can still type anything).
internal sealed class ActionChoices : System.ComponentModel.StringConverter
{
    public static readonly string[] Common =
    {
        "launcher", "tile:toggle", "tile:focus:left", "tile:focus:right", "tile:focus:up", "tile:focus:down",
        "tile:move:left", "tile:move:right", "tile:move:up", "tile:move:down", "tile:master", "tile:float",
        "tile:layout:next", "tile:layout:dwindle", "tile:layout:master", "tile:layout:columns", "tile:layout:rows", "tile:layout:monocle",
        "tile:ratio:+5", "tile:ratio:-5", "tile:retile",
        "window:close", "window:minimize", "window:maximize",
        "media:toggle", "media:next", "media:prev", "volume:mute", "volume:+5", "volume:-5", "audio:device:next", "audio:mixer",
        "desktop:next", "desktop:prev", "glyph:pause", "glyph:reload", "settings", "run:notepad",
    };
    public override bool GetStandardValuesSupported(System.ComponentModel.ITypeDescriptorContext? c) => true;
    public override bool GetStandardValuesExclusive(System.ComponentModel.ITypeDescriptorContext? c) => false;
    public override StandardValuesCollection GetStandardValues(System.ComponentModel.ITypeDescriptorContext? c) => new(Common);
}
