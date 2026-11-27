using Glyph.Bar;
using Glyph.Ui;
using Glyph.Widgets;

namespace Glyph.UI;

/// The Design page: a live preview of the theme you can click, drag, resize and draw boxes on.
internal sealed partial class SettingsForm
{
    sealed class Canvas2 : Control
    {
        public Canvas2() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); }
    }

    enum Sel { None, Widget, Bar, Module, Background, Look }
    enum Drag { None, Move, Resize, Draw }

    readonly Canvas2 view = new() { Dock = DockStyle.Fill };
    readonly PropertyGrid designGrid = Grid();
    readonly Label selLabel = new() { Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI Semibold", 11f), AutoEllipsis = true };
    readonly FlowLayoutPanel swatchStrip = new() { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(0, 6, 0, 0), WrapContents = false, AutoScroll = true };
    readonly CheckBox animate = new() { Text = "Animate (mouse effects play)", AutoSize = true, Padding = new Padding(0, 7, 0, 0) };
    readonly Label drawHint = new() { AutoSize = true, Padding = new Padding(8, 9, 0, 0) };
    Preview? preview;
    Bitmap? frame;
    Preview.Info? info;
    Sel sel = Sel.Background;
    int selIndex = -1;                 // widget's place in widgets.toml
    BarModuleOptions? selModule;
    Drag drag = Drag.None;
    string drawType = "";
    PointF dragStart; RectangleF dragOrig, dragRect;

    Control BuildDesign()
    {
        var root = new Panel { Dock = DockStyle.Fill };
        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 4, 0, 4) };
        Button T(string text, Action a, bool primary = false)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 34, Padding = new Padding(8, 0, 8, 0), Margin = new Padding(0, 0, 6, 6) };
            b.Click += (_, _) => a(); tools.Controls.Add(b);
            if (primary) b.HandleCreated += (_, _) => Dark.Primary(b);
            return b;
        }
        Button? draw = null;
        draw = T("Draw box ▾", () => DropDown(draw!, WidgetsFile.Types.Keys.Select(t => ($"{t}  —  {WidgetsFile.Help[t]}", (Action)(() =>
        {
            drawType = t; drawHint.Text = $"Drag on the preview to draw a {t} box (Esc to stop)"; view.Cursor = Cursors.Cross;
        })))), true);
        T("Delete", DeleteSelected);
        T("Duplicate", DuplicateSelected);
        T("To front", () => Reorder(1));
        T("To back", () => Reorder(-1));
        T("Background", () => Select(Sel.Background));
        T("Bar", () => Select(Sel.Bar));
        T("Theme look", () => Select(Sel.Look));
        T("Pick colour", StartPicking);
        T("Wallpaper colours…", ColorsFromWallpaper);
        tools.Controls.Add(animate);
        tools.Controls.Add(drawHint);

        var split = Split(-400);   // the properties panel keeps 400 pixels
        split.Panel1.Controls.Add(view);
        split.Panel1.Controls.Add(swatchStrip);
        BuildPanes(split.Panel2);
        split.Panel2.Controls.Add(selLabel);
        root.Controls.Add(split);
        root.Controls.Add(tools);
        root.Controls.Add(Hint("Click anything to change it: a part of a widget (a tab, a title, a button…), the bar, a module on the bar, or the background. Drag to move, drag the corner to resize, or draw a new box. On the right: Properties, Look (the part's frame) and Animations.", 46));

        view.Paint += PaintView;
        view.MouseDown += ViewDown;
        view.MouseMove += ViewMove;
        view.MouseUp += ViewUp;
        view.MouseLeave += (_, _) => preview?.SetMouse(new PointF(-1e6f, -1e6f));
        view.KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { drawType = ""; drawHint.Text = ""; view.Cursor = Cursors.Default; StopPicking(); } if (e.KeyCode == Keys.Delete) DeleteSelected(); };
        animate.CheckedChanged += (_, _) => { if (preview != null) { preview.Animate = animate.Checked; preview.Redraw(); } };
        designGrid.PropertyValueChanged += (_, _) => DesignChanged();
        return root;
    }

    // ───────────── preview life ─────────────

    void StartPreview()
    {
        if (preview != null || themeName.Length == 0) return;
        preview = new Preview(themeName) { Animate = animate.Checked };
        preview.Frame += (bmp, inf) =>
        {
            try { BeginInvoke(() => { frame = bmp; info = inf; view.Invalidate(); }); } catch { }
        };
    }

    void StopPreview()
    {
        preview?.Dispose(); preview = null;
        frame = null; info = null;
    }

    void DesignThemeChanged()
    {
        if (preview != null) preview.Theme = themeName;
        Select(Sel.Background);
        RefreshSwatches();
    }

    // ───────────── selection ─────────────

    void Select(Sel s, int index = -1, BarModuleOptions? module = null, Part? part = null)
    {
        sel = s; selIndex = index; selModule = module; selPart = s == Sel.Widget ? part : null;
        if (s != Sel.Widget) { paneButtons.Visible = false; ShowPane(Pane.Props); }
        switch (s)
        {
            case Sel.Widget when index >= 0 && index < widgets.Widgets.Count:
                ShowSelection();
                break;
            case Sel.Module when module != null:
                designGrid.SelectedObject = module;
                selLabel.Text = "Bar module: " + module;
                break;
            case Sel.Bar:
                designGrid.SelectedObject = bar.Bar;
                selLabel.Text = "The bar" + (bar.Bar.Enabled ? "" : "  (off: set Enabled to show it)");
                break;
            case Sel.Look:
                designGrid.SelectedObject = theme.Options;
                selLabel.Text = "Theme look: style, fonts, effects";
                break;
            default:
                sel = Sel.Background;
                designGrid.SelectedObject = wallpaper;
                designGrid.ExpandAllGridItems();
                selLabel.Text = "Background: backdrop, mouse effect, clicks";
                break;
        }
        view.Invalidate();
    }

    void DesignChanged()
    {
        switch (sel)
        {
            case Sel.Widget: MarkWidgets(); RefreshWidgets(widgetList.SelectedItem as WidgetOptions); break;
            case Sel.Module: case Sel.Bar: MarkBar(); RefreshModules(moduleList.SelectedItem as BarModuleOptions); break;
            case Sel.Look: MarkTheme(); RefreshSwatches(); break;
            default: MarkWallpaper(); break;
        }
    }

    void DeleteSelected()
    {
        if (sel == Sel.Widget && selIndex >= 0 && selIndex < widgets.Widgets.Count)
        {
            widgets.Widgets.RemoveAt(selIndex); RefreshWidgets(); SaveSoon(ref dirtyWidgets); Select(Sel.Background);
        }
        else if (sel == Sel.Module && selModule != null)
        {
            bar.Left.Remove(selModule); bar.Center.Remove(selModule); bar.Right.Remove(selModule);
            RefreshModules(); SaveSoon(ref dirtyBar); Select(Sel.Bar);
        }
    }

    void DuplicateSelected()
    {
        if (sel != Sel.Widget || selIndex < 0 || selIndex >= widgets.Widgets.Count) return;
        var c = Glyph.Options.Binder.Clone(widgets.Widgets[selIndex]);
        c.OffsetX += 30; c.OffsetY += 30;
        widgets.Widgets.Add(c); RefreshWidgets(c); SaveSoon(ref dirtyWidgets);
        Select(Sel.Widget, widgets.Widgets.Count - 1);
    }

    void Reorder(int d)
    {
        if (sel != Sel.Widget || selIndex < 0 || selIndex >= widgets.Widgets.Count) return;
        int j = d > 0 ? widgets.Widgets.Count - 1 : 0;
        var w = widgets.Widgets[selIndex];
        widgets.Widgets.RemoveAt(selIndex); widgets.Widgets.Insert(j, w);
        RefreshWidgets(w); SaveSoon(ref dirtyWidgets);
        Select(Sel.Widget, j);
    }

    /// Saves now (not after the usual short wait), so the preview catches up quickly after a drag.
    void SaveSoon(ref bool flag) { flag = true; SaveNow(); }

    // ───────────── swatches: quick colour changes ─────────────

    void RefreshSwatches()
    {
        swatchStrip.Controls.Clear();
        if (theme == null) return;
        swatchStrip.Controls.Add(new Label { Text = "Colours:", AutoSize = true, Padding = new Padding(0, 8, 4, 0), ForeColor = Dark.Dim });
        foreach (var (n, c) in theme.PaletteList)
        {
            var b = new Button { Width = 34, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(c.R, c.G, c.B), Margin = new Padding(0, 0, 4, 0), Tag = n };
            b.FlatAppearance.BorderColor = Dark.Line;
            new ToolTip().SetToolTip(b, $"{n}  {c.Hex}  (click to change)");
            b.Click += (_, _) =>
            {
                var cur = theme.Palette[n];
                using var d = new ColorDialog { Color = Color.FromArgb(cur.R, cur.G, cur.B), FullOpen = true, AnyColor = true };
                if (d.ShowDialog(this) != DialogResult.OK) return;
                theme.SetColor(n, new Rgba(d.Color.R, d.Color.G, d.Color.B, cur.A));
                RefreshPalette(); RefreshSwatches();
                SaveSoon(ref dirtyTheme);
            };
            swatchStrip.Controls.Add(b);
        }
    }

    // ───────────── view mapping ─────────────

    (float scale, float ox, float oy) Map()
    {
        if (info == null) return (1, 0, 0);
        float s = Math.Min(view.Width / (float)info.W, view.Height / (float)info.H);
        return (s, (view.Width - info.W * s) / 2, (view.Height - info.H * s) / 2);
    }

    PointF ToScreen(Point p) { var (s, ox, oy) = Map(); return new PointF((p.X - ox) / s, (p.Y - oy) / s); }
    RectangleF ToView(RectangleF r) { var (s, ox, oy) = Map(); return new RectangleF(ox + r.X * s, oy + r.Y * s, r.Width * s, r.Height * s); }

    RectangleF? SelectedRect()
    {
        if (info == null) return null;
        if (sel == Sel.Widget) foreach (var (fi, r, _) in info.Widgets) if (fi == selIndex) return r;
        if (sel == Sel.Module && selModule != null) foreach (var (m, r) in info.Modules) if (Same(m, selModule)) return r;
        if (sel == Sel.Bar && info.BarOn) return info.BarRect;
        return null;
    }

    RectangleF Handle(RectangleF viewRect) => new(viewRect.Right - 7, viewRect.Bottom - 7, 14, 14);

    /// Where the picked part is in the latest preview frame (parts are rebuilt every frame: find the same one again).
    RectangleF? PartRect(Part part)
    {
        if (info == null) return null;
        foreach (var (fi, pt) in info.Parts) if (fi == selIndex && pt.Name == part.Name && pt.Rect == part.Rect) return pt.Rect;
        // moved or resized: the part with the same name nearest to where it was
        var same = info.Parts.Where(x => x.fileIndex == selIndex && x.part.Name == part.Name).Select(x => x.part.Rect).ToList();
        if (same.Count == 0) return null;
        float cx = part.Rect.X + part.Rect.Width / 2, cy = part.Rect.Y + part.Rect.Height / 2;
        return same.OrderBy(r => Math.Abs(r.X + r.Width / 2 - cx) + Math.Abs(r.Y + r.Height / 2 - cy)).First();
    }

    void PaintView(object? s, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Dark.Bg);
        if (frame == null || info == null)
        {
            TextRenderer.DrawText(g, "Starting the preview…", Font, view.ClientRectangle, Dark.Dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        var (sc, ox, oy) = Map();
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        lock (frame) g.DrawImage(frame, ox, oy, info.W * sc, info.H * sc);
        using var dim = new Pen(Color.FromArgb(110, Dark.Dim)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        foreach (var (_, r, _) in info.Widgets) { var v = ToView(r); g.DrawRectangle(dim, v.X, v.Y, v.Width, v.Height); }
        using var acc = new Pen(Dark.Accent, 2);
        RectangleF? box = drag is Drag.Move or Drag.Resize or Drag.Draw ? dragRect : SelectedRect();
        // the picked part, filled faintly, inside the widget's outline
        if (drag == Drag.None && sel == Sel.Widget && selPart is Part sp && PartRect(sp) is RectangleF pr)
        {
            var pv = ToView(pr);
            using var pf = new SolidBrush(Color.FromArgb(46, Dark.Accent));
            using var pp = new Pen(Color.FromArgb(230, 255, 255, 255), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.FillRectangle(pf, pv); g.DrawRectangle(pp, pv.X, pv.Y, pv.Width, pv.Height);
            TextRenderer.DrawText(g, sp.Name, Font, new Point((int)pv.X, (int)pv.Bottom + 2), Color.White, Dark.Accent);
        }
        if (box is RectangleF b)
        {
            var v = ToView(b);
            g.DrawRectangle(acc, v.X, v.Y, v.Width, v.Height);
            if (sel == Sel.Widget || drag == Drag.Draw)
            {
                using var fill = new SolidBrush(Dark.Accent);
                var h = Handle(v);
                if (drag != Drag.Draw) g.FillRectangle(fill, h);
                var label = drag == Drag.Draw ? drawType : $"{(int)b.Width} × {(int)b.Height}";
                TextRenderer.DrawText(g, label, Font, new Point((int)v.X, (int)v.Y - 22), Dark.Accent);
            }
        }
        if (info.Problems.Count > 0) TextRenderer.DrawText(g, "⚠ " + info.Problems[0], Font, new Point(8, view.Height - 26), Color.Orange);
    }

    // ───────────── mouse ─────────────

    void ViewDown(object? s, MouseEventArgs e)
    {
        view.Focus();
        if (info == null || e.Button != MouseButtons.Left) return;
        if (picking) { if (PreviewColor(e.Location) is Color pc) Picked(pc, e.Location); return; }
        var p = ToScreen(e.Location);
        dragStart = p;
        if (drawType.Length > 0) { drag = Drag.Draw; dragRect = new RectangleF(p, SizeF.Empty); return; }
        // resize handle of the selected widget
        if (sel == Sel.Widget && SelectedRect() is RectangleF sr && Handle(ToView(sr)).Contains(e.Location))
        {
            drag = Drag.Resize; dragOrig = dragRect = sr; return;
        }
        // the smallest part under the mouse (so a box stretched over the whole screen doesn't take every click)
        var hit = info.Parts.Where(x => x.part.Rect.Contains(p)).OrderBy(x => x.part.Rect.Width * x.part.Rect.Height).FirstOrDefault();
        if (hit.part != null)
        {
            var wr = info.Widgets.FirstOrDefault(x => x.fileIndex == hit.fileIndex).r;
            Select(Sel.Widget, hit.fileIndex, null, hit.part);
            drag = Drag.Move; dragOrig = dragRect = wr;
            return;
        }
        // else the smallest widget under the mouse
        var wh = info.Widgets.Where(x => x.r.Contains(p)).OrderBy(x => x.r.Width * x.r.Height).FirstOrDefault();
        if (wh.label != null)
        {
            Select(Sel.Widget, wh.fileIndex);
            drag = Drag.Move; dragOrig = dragRect = wh.r;
            return;
        }
        if (info.BarOn && info.BarRect.Contains(p))
        {
            foreach (var (m, r) in info.Modules) if (r.Contains(p)) { SelectModule(m); return; }
            Select(Sel.Bar); return;
        }
        Select(Sel.Background);
    }

    void SelectModule(BarModuleOptions m)
    {
        // the preview's module objects are its own copies: find ours by position in the same list
        foreach (var (list, idx) in new[] { (bar.Left, 0), (bar.Center, 1), (bar.Right, 2) })
            for (int k = 0; k < list.Count; k++)
                if (list[k].Type == m.Type && Same(list[k], m))
                {
                    barSection.SelectedIndex = idx;
                    RefreshModules(list[k]);
                    Select(Sel.Module, -1, list[k]);
                    return;
                }
        Select(Sel.Bar);
    }

    static bool Same(BarModuleOptions a, BarModuleOptions b)
    {
        var sa = new System.Text.StringBuilder(); var sb = new System.Text.StringBuilder();
        Glyph.Options.Binder.WriteValues(sa, a); Glyph.Options.Binder.WriteValues(sb, b);
        return sa.ToString() == sb.ToString();
    }

    void ViewMove(object? s, MouseEventArgs e)
    {
        if (info == null) return;
        var p = ToScreen(e.Location);
        preview?.SetMouse(p);
        switch (drag)
        {
            case Drag.Move: dragRect = new RectangleF(dragOrig.X + p.X - dragStart.X, dragOrig.Y + p.Y - dragStart.Y, dragOrig.Width, dragOrig.Height); view.Invalidate(); break;
            case Drag.Resize: dragRect = new RectangleF(dragOrig.X, dragOrig.Y, Math.Max(24, dragOrig.Width + p.X - dragStart.X), Math.Max(16, dragOrig.Height + p.Y - dragStart.Y)); view.Invalidate(); break;
            case Drag.Draw: dragRect = RectangleF.FromLTRB(Math.Min(dragStart.X, p.X), Math.Min(dragStart.Y, p.Y), Math.Max(dragStart.X, p.X), Math.Max(dragStart.Y, p.Y)); view.Invalidate(); break;
            default:
                if (sel == Sel.Widget && SelectedRect() is RectangleF sr && Handle(ToView(sr)).Contains(e.Location)) view.Cursor = Cursors.SizeNWSE;
                else if (drawType.Length > 0 || picking) view.Cursor = Cursors.Cross;
                else view.Cursor = info.Widgets.Any(w => w.r.Contains(p)) ? Cursors.SizeAll : Cursors.Default;
                break;
        }
    }

    void ViewUp(object? s, MouseEventArgs e)
    {
        if (info == null) { drag = Drag.None; return; }
        var d = drag; drag = Drag.None;
        switch (d)
        {
            case Drag.Move when sel == Sel.Widget && selIndex >= 0 && selIndex < widgets.Widgets.Count:
                if (Math.Abs(dragRect.X - dragOrig.X) + Math.Abs(dragRect.Y - dragOrig.Y) < 2) break;
                PlaceWidget(widgets.Widgets[selIndex], dragRect, false);
                break;
            case Drag.Resize when sel == Sel.Widget && selIndex >= 0 && selIndex < widgets.Widgets.Count:
                PlaceWidget(widgets.Widgets[selIndex], dragRect, true);
                break;
            case Drag.Draw:
                if (dragRect.Width < 12 || dragRect.Height < 12) break;
                var w = WidgetsFile.Types[drawType]();
                w.Anchor = AnchorFor(dragRect, info.Area);
                if (w is PanelOptions po) { po.Panel = true; if (po is TextOptions to) to.Text = "your text — {time:HH:mm}"; }
                widgets.Widgets.Add(w);
                PlaceWidget(w, dragRect, true);
                drawType = ""; drawHint.Text = ""; view.Cursor = Cursors.Default;
                Select(Sel.Widget, widgets.Widgets.Count - 1);
                break;
        }
        view.Invalidate();
    }

    static string AnchorFor(RectangleF r, RectangleF a)
    {
        float cx = (r.X + r.Width / 2 - a.X) / a.Width, cy = (r.Y + r.Height / 2 - a.Y) / a.Height;
        string v = cy < 0.34f ? "top" : cy > 0.66f ? "bottom" : "";
        string h = cx < 0.34f ? "left" : cx > 0.66f ? "right" : "";
        return v.Length > 0 && h.Length > 0 ? $"{v}_{h}" : v.Length > 0 ? v : h.Length > 0 ? h : "center";
    }

    /// Turns a rectangle on the screen into the widget's anchor offsets (and size, if `size`).
    void PlaceWidget(WidgetOptions w, RectangleF r, bool size)
    {
        var a = info!.Area;
        var an = w.Anchor;
        bool left = an is "top_left" or "left" or "bottom_left", right = an is "top_right" or "right" or "bottom_right";
        bool top = an.StartsWith("top"), bottom = an.StartsWith("bottom");
        w.OffsetX = (int)Math.Round(left ? r.X - a.X : right ? a.Right - r.Right : r.X + r.Width / 2 - (a.X + a.Width / 2));
        w.OffsetY = (int)Math.Round(top ? r.Y - a.Y : bottom ? a.Bottom - r.Bottom : r.Y + r.Height / 2 - (a.Y + a.Height / 2));
        if (size)
        {
            if (w is PanelOptions p) { p.Width = (int)r.Width; p.Height = (int)r.Height; }
            else if (w is VideoOptions vo) { vo.Width = Math.Max(16, (int)r.Width); vo.Height = Math.Max(16, (int)r.Height); }
            else if (w is ShortcutsOptions so) so.Columns = Math.Max(1, (int)Math.Round(r.Width / (so.ItemWidth + so.Spacing)));
        }
        RefreshWidgets(w);
        designGrid.Refresh();
        SaveSoon(ref dirtyWidgets);
    }
}
