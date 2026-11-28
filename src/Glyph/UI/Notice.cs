using System.Runtime.InteropServices;

namespace Glyph.Ui;

/// A small window in the theme's colours that says what's going on and offers a few choices (e.g. what to do
/// about Wallpaper Engine), with an optional "remember my choice" box. Not modal: Glyph keeps running. One at a
/// time; a new one replaces the open one. UI thread only.
internal sealed class Notice : Form
{
    static Notice? open;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

    /// `buttons`: labels, the first is the main one. `choose(index, remember)`; closing the window = the last button.
    public static void Show(string title, string message, string[] buttons, Action<int, bool> choose, string? remember = null)
    {
        open?.Close();
        var n = new Notice(title, message, buttons, choose, remember);
        open = n;
        n.Show();
        n.Activate();
        if (!SetForegroundWindow(n.Handle)) { keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero); SetForegroundWindow(n.Handle); }
    }

    bool answered;

    static Color C(string name, Color fallback)
    {
        var t = Prompt.Theme;
        if (t == null) return fallback;
        var c = t[name];
        return Color.FromArgb(c.R, c.G, c.B);
    }

    Notice(string title, string message, string[] buttons, Action<int, bool> choose, string? remember)
    {
        Color bg = C("bg", Color.FromArgb(20, 20, 20)), surface = C("surface", Color.FromArgb(32, 32, 32)), text = C("text", Color.Gainsboro),
              dim = C("dim", Color.Gray), accent = C("accent", Color.SteelBlue), line = C("line", Color.DimGray);
        var font = new Font("Segoe UI", 10f);
        Text = "Glyph"; FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = true; TopMost = true; StartPosition = FormStartPosition.CenterScreen;
        BackColor = line; Padding = new Padding(1); Font = font; KeyPreview = true;
        try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "glyph.ico")); } catch { }
        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, Padding = new Padding(18, 14, 18, 14), ColumnCount = 1, AutoSize = true };
        inner.Controls.Add(new Label { Text = title.ToUpperInvariant(), ForeColor = accent, AutoSize = true, Font = new Font(font.FontFamily, 9f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
        inner.Controls.Add(new Label { Text = message, ForeColor = text, AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(0, 0, 0, 12) });
        CheckBox? box = null;
        if (remember != null)
        {
            box = new CheckBox { Text = remember, ForeColor = dim, AutoSize = true, Margin = new Padding(0, 0, 0, 10), FlatStyle = FlatStyle.Flat };
            inner.Controls.Add(box);
        }
        var col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, BackColor = bg, Margin = new Padding(0) };
        for (int i = 0; i < buttons.Length; i++)
        {
            int k = i;
            bool main = i == 0;
            var b = new Button
            {
                Text = buttons[i], AutoSize = true, MinimumSize = new Size(300, 0), FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = main ? bg : text, BackColor = main ? accent : surface, Margin = new Padding(0, 0, 0, 6), Padding = new Padding(8, 3, 8, 3),
                FlatAppearance = { BorderColor = line, MouseOverBackColor = main ? accent : line },
            };
            b.Click += (_, _) => { answered = true; Close(); try { choose(k, box?.Checked == true); } catch (Exception e) { Log.Write("notice: " + e.Message); } };
            col.Controls.Add(b);
        }
        inner.Controls.Add(col);
        Controls.Add(inner);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Load += (_, _) => { int corner = 2; DwmSetWindowAttribute(Handle, 33, ref corner, 4); int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); };
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        FormClosed += (_, _) =>
        {
            if (open == this) open = null;
            if (!answered) { answered = true; try { choose(buttons.Length - 1, false); } catch { } }   // closed = the last choice (the "leave it" one)
        };
    }
}
