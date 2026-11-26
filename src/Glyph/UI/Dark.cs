using System.Runtime.InteropServices;

namespace Glyph.UI;

/// A simple dark look for the settings window and the converter. The accent comes from the theme in use.
internal static class Dark
{
    public static Color Bg = Color.FromArgb(0x17, 0x18, 0x1C);
    public static Color Panel = Color.FromArgb(0x1F, 0x21, 0x26);
    public static Color Raised = Color.FromArgb(0x2A, 0x2C, 0x33);
    public static Color Line = Color.FromArgb(0x3A, 0x3D, 0x46);
    public static Color Text = Color.FromArgb(0xE8, 0xE9, 0xEC);
    public static Color Dim = Color.FromArgb(0x9C, 0xA0, 0xAA);
    public static Color Accent = Color.FromArgb(0xE0, 0x20, 0x2F);

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string? app, string? id);

    public static void UseAccentFrom(Theme t)
    {
        var a = t["accent"];
        Accent = Color.FromArgb(a.R, a.G, a.B);
        // keep it readable on dark grey
        if (a.Luma < 0.25f) Accent = ControlPaint.Light(Accent, 0.6f);
    }

    public static void TitleBar(Form f)
    {
        int on = 1;
        DwmSetWindowAttribute(f.Handle, 20, ref on, 4);
        int caption = ColorTranslator.ToWin32(Bg);
        DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);   // caption colour (Windows 11)
    }

    public static Font UiFont => new("Segoe UI", 10f);

    /// Colours a whole tree of controls.
    public static void Apply(Control root)
    {
        void Walk(Control c)
        {
            switch (c)
            {
                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.BackColor = Raised; b.ForeColor = Text;
                    b.FlatAppearance.BorderColor = Line;
                    b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x34, 0x37, 0x40);
                    b.FlatAppearance.MouseDownBackColor = Accent;
                    b.Cursor = Cursors.Hand;
                    break;
                case PropertyGrid pg:
                    pg.BackColor = Panel; pg.ViewBackColor = Panel; pg.ViewForeColor = Text; pg.LineColor = Raised;
                    pg.CategoryForeColor = Accent; pg.CategorySplitterColor = Raised; pg.HelpBackColor = Bg; pg.HelpForeColor = Dim;
                    pg.HelpBorderColor = Line; pg.ViewBorderColor = Line; pg.SelectedItemWithFocusBackColor = Accent; pg.SelectedItemWithFocusForeColor = Color.White;
                    pg.DisabledItemForeColor = Dim; pg.CommandsBackColor = Bg; pg.CommandsForeColor = Text;
                    break;
                case TextBox or NumericUpDown or ComboBox:
                    c.BackColor = Raised; c.ForeColor = Text;
                    if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
                    if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
                    break;
                case ListBox lb:
                    lb.BackColor = Panel; lb.ForeColor = Text; lb.BorderStyle = BorderStyle.None;
                    lb.DrawMode = DrawMode.OwnerDrawFixed; lb.ItemHeight = 30;
                    lb.DrawItem -= DrawListItem; lb.DrawItem += DrawListItem;
                    break;
                case ListView lv:
                    lv.BackColor = Panel; lv.ForeColor = Text; lv.BorderStyle = BorderStyle.None;
                    lv.OwnerDraw = true;
                    lv.DrawColumnHeader -= DrawHeader; lv.DrawColumnHeader += DrawHeader;
                    lv.DrawItem -= DrawLvItem; lv.DrawItem += DrawLvItem;
                    lv.DrawSubItem -= DrawLvSub; lv.DrawSubItem += DrawLvSub;
                    break;
                case SplitContainer sc:
                    sc.BackColor = Bg; sc.Panel1.BackColor = Bg; sc.Panel2.BackColor = Bg;
                    break;
                case StatusStrip ss:
                    ss.BackColor = Panel; ss.ForeColor = Dim; ss.Renderer = new MenuRenderer();
                    break;
                case CheckBox ck:
                    ck.ForeColor = Text; ck.BackColor = Color.Transparent;
                    break;
                default:
                    if (c is not Form) { c.BackColor = c.Parent?.BackColor ?? Bg; c.ForeColor = Text; }
                    break;
            }
            if (c.IsHandleCreated) SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            else c.HandleCreated += (_, _) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
            foreach (Control k in c.Controls) Walk(k);
        }
        root.BackColor = Bg; root.ForeColor = Text;
        Walk(root);
    }

    static void DrawListItem(object? s, DrawItemEventArgs e)
    {
        if (s is not ListBox lb || e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(sel ? Raised : Panel);
        e.Graphics.FillRectangle(bg, e.Bounds);
        if (sel) using (var a = new SolidBrush(Accent)) e.Graphics.FillRectangle(a, e.Bounds.X, e.Bounds.Y + 4, 3, e.Bounds.Height - 8);
        var text = lb.GetItemText(lb.Items[e.Index]);
        TextRenderer.DrawText(e.Graphics, text, lb.Font, new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height), sel ? Text : Color.FromArgb(0xC8, 0xCA, 0xD0), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    static void DrawHeader(object? s, DrawListViewColumnHeaderEventArgs e)
    {
        using var bg = new SolidBrush(Bg);
        e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text, e.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), Dim, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    static void DrawLvItem(object? s, DrawListViewItemEventArgs e) { }

    static void DrawLvSub(object? s, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null) return;
        bool sel = e.Item.Selected;
        using var bg = new SolidBrush(sel ? Raised : Panel);
        e.Graphics.FillRectangle(bg, e.Bounds);
        int x = e.Bounds.X + 4;
        if (e.ColumnIndex == 0)
        {
            if (sel) using (var a = new SolidBrush(Accent)) e.Graphics.FillRectangle(a, e.Bounds.X, e.Bounds.Y + 3, 3, e.Bounds.Height - 6);
            var il = e.Item.ListView?.SmallImageList;
            if (il != null && e.Item.ImageIndex >= 0 && e.Item.ImageIndex < il.Images.Count)
            {
                var img = il.Images[e.Item.ImageIndex];
                e.Graphics.DrawImage(img, x + 4, e.Bounds.Y + (e.Bounds.Height - img.Height) / 2);
                x += img.Width + 10;
            }
        }
        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.ListView?.Font, new Rectangle(x, e.Bounds.Y, e.Bounds.Right - x - 2, e.Bounds.Height), sel ? Text : Color.FromArgb(0xC8, 0xCA, 0xD0), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    /// Dark menus (drop-down "Add" menus, the status bar).
    public sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Text : Dim; base.OnRenderItemText(e); }
        sealed class Colors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Panel;
            public override Color MenuItemSelected => Raised;
            public override Color MenuItemSelectedGradientBegin => Raised;
            public override Color MenuItemSelectedGradientEnd => Raised;
            public override Color MenuItemBorder => Accent;
            public override Color MenuBorder => Line;
            public override Color ImageMarginGradientBegin => Panel;
            public override Color ImageMarginGradientMiddle => Panel;
            public override Color ImageMarginGradientEnd => Panel;
            public override Color SeparatorDark => Line;
            public override Color StatusStripGradientBegin => Panel;
            public override Color StatusStripGradientEnd => Panel;
        }
    }

    /// A drop-down menu styled dark.
    public static ContextMenuStrip Menu()
    {
        var m = new ContextMenuStrip { Renderer = new MenuRenderer(), Font = UiFont, ShowImageMargin = false };
        m.BackColor = Panel; m.ForeColor = Text;
        return m;
    }

    /// An accent-coloured main button.
    public static Button Primary(Button b)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = Accent; b.ForeColor = Color.White;
        b.FlatAppearance.BorderColor = Accent; b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Accent, 0.15f);
        return b;
    }
}
