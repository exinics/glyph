using System.Runtime.InteropServices;

namespace Glyph.Ui;

/// A small box that opens at the mouse on the desktop to type something in (a to-do item, an agenda entry, your
/// city), so widgets can be edited without opening the settings window. Drawn in the current theme's colours and
/// font. Enter = save, Esc or clicking elsewhere = cancel. UI thread only (actions already run there).
internal sealed class Prompt : Form
{
    /// The desktop's current theme (set by the app whenever it loads).
    public static Theme? Theme;
    static Prompt? open;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

    readonly TextBox[] boxes;
    readonly Action<string[]> save;
    readonly Action? delete;
    bool done;

    /// Asks for `fields` (label, starting text). `onDelete` = also offer a Delete button.
    public static void Ask(string title, (string label, string value)[] fields, Action<string[]> onSave, Action? onDelete = null)
    {
        open?.Close();
        var p = new Prompt(title, fields, onSave, onDelete);
        open = p;
        p.Show();
        p.Activate();
        if (!SetForegroundWindow(p.Handle))
        {
            // Windows only lets the app the user last used take the focus; a tap of Alt counts as using it
            keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero);
            SetForegroundWindow(p.Handle);
        }
        p.boxes[0].Focus();
        p.boxes[0].SelectionStart = p.boxes[0].Text.Length;
    }

    static readonly System.Drawing.Text.PrivateFontCollection own = new();
    static bool ownLoaded;

    /// The font family by name: installed in Windows, or one of Glyph's own (config\fonts).
    static FontFamily Family(string name)
    {
        try { var f = new FontFamily(name); return f; } catch { }
        if (!ownLoaded)
        {
            ownLoaded = true;
            foreach (var file in Glyph.Render.Fonts.Files().Where(x => x.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                try { own.AddFontFile(file); } catch { }
        }
        return own.Families.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? FontFamily.GenericSansSerif;
    }

    static Color C(string name, Color fallback)
    {
        if (Theme == null) return fallback;
        var c = Theme[name];
        return Color.FromArgb(c.R, c.G, c.B);
    }

    Prompt(string title, (string label, string value)[] fields, Action<string[]> onSave, Action? onDelete)
    {
        save = onSave; delete = onDelete;
        var ui = Theme?.Options.Ui;
        var font = new Font(Family(ui?.Font ?? "Segoe UI"), Math.Clamp((ui?.FontSize ?? 13) * 0.78f, 8, 18));
        Color bg = C("bg", Color.FromArgb(20, 20, 20)), surface = C("surface", Color.FromArgb(32, 32, 32)), text = C("text", Color.Gainsboro),
              dim = C("dim", Color.Gray), accent = C("accent", Color.SteelBlue), line = C("line", Color.DimGray);
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        BackColor = line; Padding = new Padding(1); Font = font; KeyPreview = true;
        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, Padding = new Padding(14, 10, 14, 12), ColumnCount = 1, AutoSize = true };
        inner.Controls.Add(new Label { Text = title.ToUpperInvariant(), ForeColor = accent, AutoSize = true, Font = new Font(font.FontFamily, font.Size * 0.85f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 6) });
        boxes = new TextBox[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields.Length > 1) inner.Controls.Add(new Label { Text = fields[i].label, ForeColor = dim, AutoSize = true, Margin = new Padding(0, i == 0 ? 0 : 6, 0, 2) });
            var tb = new TextBox
            {
                Text = fields[i].value, BackColor = surface, ForeColor = text, BorderStyle = BorderStyle.FixedSingle, Width = 320, Font = new Font(font.FontFamily, font.Size * 1.15f),
                PlaceholderText = fields.Length == 1 ? fields[i].label : "", Margin = new Padding(0, 0, 0, 2),
            };
            boxes[i] = tb;
            inner.Controls.Add(tb);
        }
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 10, 0, 0), BackColor = bg };
        Button B(string t, Color fg, Color back) => new()
        {
            Text = t, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = fg, BackColor = back, Margin = new Padding(0, 0, 8, 0), Padding = new Padding(8, 2, 8, 2),
            FlatAppearance = { BorderColor = line, MouseOverBackColor = surface },
        };
        var ok = B("Save", bg, accent); ok.FlatAppearance.MouseOverBackColor = accent;
        ok.Click += (_, _) => Finish(true);
        row.Controls.Add(ok);
        var cancel = B("Cancel", text, bg); cancel.Click += (_, _) => Close();
        row.Controls.Add(cancel);
        if (onDelete != null)
        {
            var del = B("Delete", C("red", Color.IndianRed), bg);
            del.Click += (_, _) => { done = true; try { delete!(); } catch (Exception e) { Log.Write("prompt delete: " + e.Message); } Close(); };
            row.Controls.Add(del);
        }
        inner.Controls.Add(row);
        inner.Controls.Add(new Label { Text = "Enter = save · Esc = cancel", ForeColor = dim, AutoSize = true, Font = new Font(font.FontFamily, font.Size * 0.8f), Margin = new Padding(0, 6, 0, 0) });
        Controls.Add(inner);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // at the mouse, kept on its screen
        var at = Cursor.Position;
        var wa = Screen.FromPoint(at).WorkingArea;
        Load += (_, _) =>
        {
            int x = Math.Clamp(at.X + 8, wa.Left + 8, Math.Max(wa.Left + 8, wa.Right - Width - 8));
            int y = Math.Clamp(at.Y + 8, wa.Top + 8, Math.Max(wa.Top + 8, wa.Bottom - Height - 8));
            Location = new Point(x, y);
            int corner = 2; DwmSetWindowAttribute(Handle, 33, ref corner, 4);   // rounded corners
            int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
            else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Finish(true); }
        };
        Deactivate += (_, _) => { if (!done) BeginInvoke(Close); };
        FormClosed += (_, _) => { if (open == this) open = null; };
    }

    void Finish(bool ok)
    {
        if (done) return;
        done = true;
        if (ok) { try { save(boxes.Select(b => b.Text.Trim()).ToArray()); } catch (Exception e) { Log.Write("prompt save: " + e.Message); } }
        Close();
    }

    protected override bool ShowWithoutActivation => false;
}
