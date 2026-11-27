using System.ComponentModel;
using System.Text;
using Glyph.Options;
using Glyph.Render;
using Vortice.Mathematics;

namespace Glyph.Widgets;

internal sealed class MenuOptions : WidgetOptions
{
    public override string Type => "menu";
    public MenuOptions() { Anchor = "top_left"; OffsetX = 20; OffsetY = 140; Stretch = "both"; }

    [Category("Menu"), DisplayName("Tabs"), Description("The shortcut groups shown as tabs, in order (names as in the shortcuts' Group). Empty = every group: the Shortcuts page's groups first, then any other group a shortcut names."), TypeConverter(typeof(ListConverter))]
    public string[] Tabs { get; set; } = Array.Empty<string>();
    [Category("Menu"), DisplayName("Ungrouped tab"), Description("A tab for the shortcuts that have no group, with this name. Empty = leave them out.")] public string UngroupedTab { get; set; } = "Other";
    [Category("Menu"), DisplayName("Open with"), Description("double_click = a click selects (and shows the details), a double click opens. click = a click opens."), Choices("double_click", "click"), TypeConverter(typeof(ChoicesConverter))]
    public string Launch { get; set; } = "double_click";
    [Category("Menu"), DisplayName("Hover wipe"), Description("Milliseconds for the dark fill to slide in under the mouse (tabs and list). 0 = at once."), Range(0, 2000)] public float WipeMs { get; set; } = 160;
    [Category("Menu"), DisplayName("Wipe easing"), Description("The shape of the hover wipe's motion (as in animations): cubic = quick then settling, linear = steady, back = overshoots, bounce, elastic…"),
     Choices("linear", "sine", "quad", "cubic", "quart", "quint", "exponential", "circular", "back", "elastic", "bounce"), TypeConverter(typeof(ChoicesConverter))]
    public string WipeStyle { get; set; } = "cubic";
    [Category("Menu"), DisplayName("Wipe direction"), Description("out = fast start, gentle end. in = gentle start, fast end. inout = gentle at both ends."), Choices("in", "out", "inout"), TypeConverter(typeof(ChoicesConverter))]
    public string WipeDirection { get; set; } = "out";
    [Category("Menu"), DisplayName("Capitals"), Description("Tabs and the heading in capitals.")] public bool Upper { get; set; } = true;

    [Category("Tabs"), DisplayName("Tab layout"), Description("spread = the tabs fill the width with even gaps. packed = side by side from the left."), Choices("spread", "packed"), TypeConverter(typeof(ChoicesConverter))] public string TabLayout { get; set; } = "spread";
    [Category("Tabs"), DisplayName("Tab height"), Range(12, 200)] public float TabHeight { get; set; } = 28;
    [Category("Tabs"), DisplayName("Tab gap"), Description("Pixels between tabs (packed), or the least gap (spread)."), Range(0, 400)] public float TabGap { get; set; } = 18;
    [Category("Tabs"), DisplayName("Tab inset"), Description("Space at the left and right ends of the tab row, in pixels."), Range(0, 1000)] public float TabInset { get; set; } = 40;
    [Category("Tabs"), DisplayName("Tab padding"), Description("Space inside a tab, left and right."), Range(0, 100)] public float TabPadding { get; set; } = 12;
    [Category("Tabs"), DisplayName("Tab letter spacing"), Range(-5, 50)] public float TabSpacing { get; set; } = 2;
    [Category("Tabs"), DisplayName("Icon box"), Description("Size of the square behind a tab's icon (the group's Icon). 0 = no box, just the icon."), Range(0, 100)] public float IconBox { get; set; } = 20;
    [Category("Tabs"), DisplayName("Show counts"), Description("Show how many shortcuts each tab holds, like (5).")] public bool TabCounts { get; set; }
    [Category("Tabs"), DisplayName("Tab frame"), Description("A frame (Frames page) for the tabs. none = a plain plate in the colours below (its Hover fill becomes Active colour)."), TypeConverter(typeof(FrameNameConverter))] public string TabFrame { get; set; } = "none";
    [Category("Tabs"), DisplayName("Tab colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TabColor { get; set; } = "surface";
    [Category("Tabs"), DisplayName("Tab text"), Color, TypeConverter(typeof(ColorNameConverter))] public string TabText { get; set; } = "text";
    [Category("Tabs"), DisplayName("Active colour"), Description("The fill of the chosen tab / item and the one under the mouse."), Color, TypeConverter(typeof(ColorNameConverter))] public string ActiveColor { get; set; } = "text";
    [Category("Tabs"), DisplayName("Active text"), Color, TypeConverter(typeof(ColorNameConverter))] public string ActiveText { get; set; } = "bg";
    [Category("Tabs"), DisplayName("Rule"), Description("The line under the tab row. band = a line, dashes and little dot triangles."), Choices("none", "solid", "band"), TypeConverter(typeof(ChoicesConverter))] public string Rule { get; set; } = "band";
    [Category("Tabs"), DisplayName("Rule colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string RuleColor { get; set; } = "text";
    [Category("Tabs"), DisplayName("Rule gap"), Description("Pixels between the tabs and the rule."), Range(0, 200)] public float RuleGap { get; set; } = 14;

    [Category("Heading"), DisplayName("Show heading"), Description("The chosen tab's name in big letters, with the group's Subtitle and Description under it.")] public bool Heading { get; set; } = true;
    [Category("Heading"), DisplayName("Heading gap"), Description("Pixels between the rule and the heading."), Range(0, 400)] public float HeadingGap { get; set; } = 36;
    [Category("Heading"), DisplayName("Heading size"), Description("Size of the big name compared with normal text."), Range(1, 8)] public float HeadingScale { get; set; } = 2.3f;
    [Category("Heading"), DisplayName("Heading spacing"), Range(-5, 60)] public float HeadingSpacing { get; set; } = 4;
    [Category("Heading"), DisplayName("Heading shadow"), Description("The heading printed again a little lower right, in this colour. none = no shadow."), Color, TypeConverter(typeof(ColorNameConverter))] public string HeadingShadow { get; set; } = "none";
    [Category("Heading"), DisplayName("Heading colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string HeadingColor { get; set; } = "text";
    [Category("Heading"), DisplayName("Subtitle colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string SubtitleColor { get; set; } = "dim";
    [Category("Heading"), DisplayName("Subtitle spacing"), Range(-5, 50)] public float SubtitleSpacing { get; set; } = 3;
    [Category("Heading"), DisplayName("Heading inset"), Description("Space from the left edge to the heading and the list."), Range(0, 1000)] public float Inset { get; set; } = 40;

    [Category("List"), DisplayName("List width"), Range(60, 2000)] public float ListWidth { get; set; } = 240;
    [Category("List"), DisplayName("Item height"), Range(12, 200)] public float ItemHeight { get; set; } = 32;
    [Category("List"), DisplayName("Item gap"), Description("Pixels between items."), Range(0, 100)] public float ItemGap { get; set; } = 12;
    [Category("List"), DisplayName("List gap"), Description("Pixels between the heading and the list."), Range(0, 400)] public float ListGap { get; set; } = 36;
    [Category("List"), DisplayName("Item frame"), Description("A frame for the items. none = a plain plate in the tab colours."), TypeConverter(typeof(FrameNameConverter))] public string ItemFrame { get; set; } = "none";
    [Category("List"), DisplayName("Bullet size"), Description("The little square before each name. 0 = none."), Range(0, 60)] public float Bullet { get; set; } = 10;
    [Category("List"), DisplayName("Rail colour"), Description("A thick line down the left of the list, with a mark at the chosen item. none = no rail."), Color, TypeConverter(typeof(ColorNameConverter))] public string RailColor { get; set; } = "line";
    [Category("List"), DisplayName("Rail width"), Range(1, 40)] public float RailWidth { get; set; } = 6;

    [Category("Details"), DisplayName("Show details"), Description("A panel beside the list about the chosen shortcut: what it is, where it points, open buttons.")] public bool Details { get; set; } = true;
    [Category("Details"), DisplayName("Details width"), Range(100, 3000)] public float DetailsWidth { get; set; } = 420;
    [Category("Details"), DisplayName("Details frame"), Description("A frame for the panel (its title strip holds the name). none = a dark title strip and no box."), TypeConverter(typeof(FrameNameConverter))] public string DetailsFrame { get; set; } = "none";
    [Category("Details"), DisplayName("Label colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string LabelColor { get; set; } = "dim";
    [Category("Details"), DisplayName("Row lines"), Description("A faint line between rows. none = no lines."), Color, TypeConverter(typeof(ColorNameConverter))] public string RowLine { get; set; } = "line";

    public override string ToString() => $"menu{(Name.Length > 0 ? ": " + Name : "")}{(Enabled ? "" : " (off)")}";
}

/// A full menu screen built from the shortcut groups: tabs across the top, the chosen group's heading, its
/// shortcuts as a list (dark fill wiping in under the mouse) and a details panel about the chosen one.
internal sealed class MenuWidget : Widget
{
    readonly MenuOptions o;
    readonly Theme t;
    readonly List<(string name, string label, string icon, string sub, string desc, List<Shortcut> items)> tabs = new();
    readonly FrameOptions tabFrame, itemFrame, detailsFrame;
    readonly string key;
    static readonly Dictionary<string, (int tab, int item)> remembered = new();
    int tab, item = -1, scroll;

    // layout (pixels)
    readonly List<RectangleF> tabRects = new();
    readonly List<RectangleF> itemRects = new();
    readonly List<(RectangleF r, string label, Action<WidgetEnv> run)> buttons = new();
    RectangleF listArea, detailsRect, headRect;
    float ruleY, headY, fontSize;
    string font = "";
    int visibleItems;

    // hover / chosen animation: 0 = resting, 1 = filled
    readonly Dictionary<int, float> amount = new();
    float lastTime = -1;
    bool moving;

    const int ItemBase = 1000, ButtonBase = 2000;

    public MenuWidget(MenuOptions o, Theme t, List<Shortcut> shortcuts)
    {
        this.o = o; this.t = t;
        key = t.Name + "|" + o.Name + "|" + o.Tabs.Length;
        var names = o.Tabs.Length > 0 ? o.Tabs.Select(x => x.Trim()).Where(x => x.Length > 0).ToList() : ShortcutStore.GroupNames(shortcuts);
        foreach (var n in names)
        {
            var g = ShortcutStore.Group(n);
            var items = shortcuts.Where(s => s.Group.Trim().Equals(n, StringComparison.OrdinalIgnoreCase)).ToList();
            tabs.Add((n, g != null && g.Label.Length > 0 ? g.Label : n, g?.Icon ?? "", g?.Subtitle ?? "", g?.Description ?? "", items));
        }
        if (o.UngroupedTab.Trim().Length > 0)
        {
            var known = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var rest = shortcuts.Where(s => s.Group.Trim().Length == 0 || (o.Tabs.Length > 0 && !known.Contains(s.Group.Trim()))).ToList();
            var g = ShortcutStore.Group(o.UngroupedTab.Trim());
            if (rest.Count > 0) tabs.Add((o.UngroupedTab.Trim(), o.UngroupedTab.Trim(), g?.Icon ?? "", g?.Subtitle ?? "", g?.Description ?? "", rest));
        }
        if (remembered.TryGetValue(key, out var r)) { tab = Math.Clamp(r.tab, 0, Math.Max(0, tabs.Count - 1)); item = r.item; }
        if (tab < tabs.Count && (item < 0 || item >= tabs[tab].items.Count)) item = tabs[tab].items.Count > 0 ? 0 : -1;

        tabFrame = t.Frame(o.TabFrame) ?? Plate();
        itemFrame = t.Frame(o.ItemFrame) ?? Plate();
        detailsFrame = t.Frame(o.DetailsFrame) ?? new FrameOptions
        {
            Name = "menu details", Fill = "none", Border = "none", Header = "bar", HeaderFill = o.ActiveColor, TitleColor = o.ActiveText,
            TitleBold = false, TitleSpacing = 1, Padding = 10, PaddingX = 12,
        };
    }

    FrameOptions Plate() => new()
    {
        Name = "menu plate", Fill = o.TabColor, FillOpacity = 1, Border = "none", HoverFill = o.ActiveColor, HoverWipe = o.WipeMs,
        Header = "none", Padding = 0, PaddingX = 0, Radius = 0,
    };

    string Up(string s) => o.Upper ? s.ToUpperInvariant() : s;

    public override bool Animated => moving;

    float Measure(WidgetEnv e, string s, float size, float spacing) =>
        e.Canvas == null ? s.Length * size * (0.6f + spacing / Math.Max(1, size)) : FramePainter.MeasureText(e.Canvas, s, font, size, false, spacing);

    public override void Layout(WidgetEnv e)
    {
        font = e.Painter.Font; fontSize = e.BaseFontSize;
        float w = WantW(o, 900, e), h = WantH(o, 600, e);
        var pt = PlaceFor(o, w, h, e.Area);
        Bounds = new RectangleF(pt.X, pt.Y, w, h);
        float x0 = Bounds.X, y0 = Bounds.Y;

        // tabs
        tabRects.Clear();
        var widths = new List<float>();
        foreach (var tb in tabs)
        {
            float tw = o.TabPadding * 2 + Measure(e, TabText(tb), fontSize, o.TabSpacing);
            if (tb.icon.Length > 0) tw += (o.IconBox > 0 ? o.IconBox : fontSize * 1.2f) + 8;
            widths.Add(tw);
        }
        float rowW = Bounds.Width - 2 * o.TabInset, sum = widths.Sum();
        float gap = o.TabLayout == "spread" && tabs.Count > 1 ? Math.Max(o.TabGap, (rowW - sum) / (tabs.Count - 1)) : o.TabGap;
        float x = x0 + o.TabInset;
        foreach (var tw in widths) { tabRects.Add(new RectangleF(x, y0, tw, o.TabHeight)); x += tw + gap; }
        ruleY = y0 + o.TabHeight + o.RuleGap;
        float below = ruleY + (o.Rule == "band" ? 23 : o.Rule == "solid" ? 2 : 0);

        // heading
        headY = below + o.HeadingGap;
        float y = headY;
        if (o.Heading && tab < tabs.Count)
        {
            y += fontSize * o.HeadingScale * 1.36f + 8;
            if (tabs[tab].sub.Length > 0) y += fontSize * 1.5f;
            if (tabs[tab].desc.Length > 0) y += fontSize * 1.4f;
        }
        else headY = -1;
        headRect = headY < 0 ? RectangleF.Empty : new RectangleF(x0 + o.Inset, headY, Math.Min(700, Bounds.Width - o.Inset), y - headY);

        // list
        itemRects.Clear();
        float ly = (headY < 0 ? below : y) + o.ListGap, lx = x0 + o.Inset + (o.RailColor != "none" ? o.RailWidth + 18 : 0);
        listArea = new RectangleF(lx, ly, o.ListWidth, Math.Max(o.ItemHeight, Bounds.Bottom - ly - 10));
        int count = tab < tabs.Count ? tabs[tab].items.Count : 0;
        float step = o.ItemHeight + o.ItemGap;
        visibleItems = Math.Max(1, (int)((listArea.Height + o.ItemGap) / step));
        scroll = Math.Clamp(scroll, 0, Math.Max(0, count - visibleItems));
        for (int i = 0; i < count; i++)
        {
            int k = i - scroll;
            itemRects.Add(k < 0 || k >= visibleItems ? RectangleF.Empty : new RectangleF(lx, ly + k * step, o.ListWidth, o.ItemHeight));
        }

        // details
        buttons.Clear();
        detailsRect = RectangleF.Empty;
        if (o.Details && tab < tabs.Count && item >= 0 && item < tabs[tab].items.Count)
        {
            var s = tabs[tab].items[item];
            float dx = lx + o.ListWidth + 24 + (count > visibleItems ? 10 : 0);
            float rowH = fontSize * 2.1f;
            var ins = FramePainter.Space(detailsFrame, t, fontSize, true);
            int rows = Rows(s).Count;
            float bh = o.ItemHeight * 0.85f;
            float dh = ins.T + rows * rowH + 10 + 2 * (bh + 6) + ins.B;
            detailsRect = new RectangleF(dx, ly, Math.Min(o.DetailsWidth, Math.Max(100, Bounds.Right - dx - 10)), dh);
            float by = ly + ins.T + rows * rowH + 10;
            var target = s;
            buttons.Add((new RectangleF(dx + ins.L, by, detailsRect.Width - ins.L - ins.R, bh), "Open", env => env.Launch?.Invoke(target)));
            var folder = FolderOf(s);
            if (folder.Length > 0)
                buttons.Add((new RectangleF(dx + ins.L, by + bh + 6, detailsRect.Width - ins.L - ins.R, bh), "Show in folder",
                    env => env.Do?.Invoke(File.Exists(folder) ? $"run:explorer.exe /select,\"{folder}\"" : folder)));
        }
    }

    string TabText((string name, string label, string icon, string sub, string desc, List<Shortcut> items) tb) =>
        Up(tb.label) + (o.TabCounts ? $" ({tb.items.Count})" : "");

    static string Kind(Shortcut s)
    {
        var tg = s.Target;
        if (tg.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return "System folder";
        if (tg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "Website";
        if (Directory.Exists(tg)) return "Folder";
        return Path.GetExtension(tg).ToLowerInvariant() switch
        {
            ".lnk" => "Shortcut", ".url" => "Internet shortcut", ".exe" => "Program", ".bat" or ".cmd" or ".ps1" => "Script", "" => "Item",
            var x => x.TrimStart('.').ToUpperInvariant() + " file",
        };
    }

    static string FolderOf(Shortcut s)
    {
        var tg = s.Target;
        if (tg.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || tg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "";
        if (Directory.Exists(tg)) return tg;
        return File.Exists(tg) ? tg : "";
    }

    List<(string label, string value)> Rows(Shortcut s)
    {
        var rows = new List<(string, string)> { ("TYPE", Kind(s)) };
        if (s.Description.Length > 0) rows.Insert(0, ("ABOUT", s.Description));
        rows.Add(("TARGET", s.Target));
        if (s.Arguments.Length > 0) rows.Add(("ARGUMENTS", s.Arguments));
        if (s.Group.Length > 0) rows.Add(("GROUP", s.Group));
        return rows;
    }

    public override IEnumerable<Part> Parts()
    {
        yield return new Part("Menu", Bounds, null);
        foreach (var r in tabRects) yield return new Part("Tab", r, new[] { "Tabs", "Menu" }, "TabFrame");
        if (o.Rule != "none" && tabRects.Count > 0) yield return new Part("Rule under the tabs", new RectangleF(Bounds.X, ruleY - 3, Bounds.Width, 26), new[] { "Tabs" });
        if (!headRect.IsEmpty) yield return new Part("Heading", headRect, new[] { "Heading" });
        if (o.RailColor != "none") yield return new Part("Rail", new RectangleF(listArea.X - 18 - o.RailWidth - 4, listArea.Y, o.RailWidth + 8, Math.Min(listArea.Height, itemRects.Count * (o.ItemHeight + o.ItemGap))), new[] { "List" });
        foreach (var r in itemRects) if (!r.IsEmpty) yield return new Part("List item", r, new[] { "List", "Menu" }, "ItemFrame");
        if (!detailsRect.IsEmpty) yield return new Part("Details", detailsRect, new[] { "Details" }, "DetailsFrame");
        foreach (var b in buttons) yield return new Part("Details button", b.r, new[] { "Details", "List" }, "ItemFrame");
    }

    // ───────── input ─────────

    public override int HitTest(float x, float y)
    {
        for (int i = 0; i < tabRects.Count; i++) if (tabRects[i].Contains(x, y)) return i;
        for (int i = 0; i < itemRects.Count; i++) if (itemRects[i].Contains(x, y)) return ItemBase + i;
        for (int i = 0; i < buttons.Count; i++) if (buttons[i].r.Contains(x, y)) return ButtonBase + i;
        return -1;
    }

    /// The parts that are really there (so dragging a selection box still works on the empty screen around them).
    public override bool Covers(float x, float y) =>
        HitTest(x, y) >= 0 || detailsRect.Contains(x, y) || listArea.Contains(x, y) || (y >= Bounds.Y && y <= ruleY && x >= Bounds.X && x <= Bounds.Right);

    public override void Click(int hit, int count, WidgetEnv e)
    {
        if (hit < 0) return;
        if (hit < ItemBase)
        {
            if (hit < tabs.Count && hit != tab) { tab = hit; item = tabs[tab].items.Count > 0 ? 0 : -1; scroll = 0; }
        }
        else if (hit < ButtonBase)
        {
            int i = hit - ItemBase;
            if (tab >= tabs.Count || i >= tabs[tab].items.Count) return;
            item = i;
            bool go = o.Launch == "click" ? count == 1 : count == 2;
            if (go) e.Launch?.Invoke(tabs[tab].items[i]);
        }
        else if (count == 1)
        {
            int i = hit - ButtonBase;
            if (i < buttons.Count) buttons[i].run(e);
        }
        remembered[key] = (tab, item);
    }

    public override bool Wheel(float x, float y, int delta, WidgetEnv e)
    {
        if (listArea.Contains(x, y))
        {
            int count = tab < tabs.Count ? tabs[tab].items.Count : 0;
            scroll = Math.Clamp(scroll + (delta > 0 ? -1 : 1), 0, Math.Max(0, count - visibleItems));
            return true;
        }
        if (tabRects.Count > 0 && y <= ruleY && y >= Bounds.Y)
        {
            int next = Math.Clamp(tab + (delta > 0 ? -1 : 1), 0, Math.Max(0, tabs.Count - 1));
            if (next != tab) { tab = next; item = tabs[tab].items.Count > 0 ? 0 : -1; scroll = 0; remembered[key] = (tab, item); }
            return true;
        }
        return false;
    }

    // ───────── animation ─────────

    /// Moves each element's fill towards 1 (hovered or chosen) or 0 and says how far it is.
    float Amount(int id, bool on, float dt)
    {
        float target = on ? 1 : 0;
        float a = amount.TryGetValue(id, out var v) ? v : target;
        if (o.WipeMs <= 0) a = target;
        else if (a != target)
        {
            float step = dt * 1000f / o.WipeMs;
            a = on ? Math.Min(1, a + step) : Math.Max(0, a - step);
            if (a != target) moving = true;
        }
        amount[id] = a;
        return a;
    }

    float Ease(float a) => a <= 0 ? 0 : a >= 1 ? 1 : Math.Clamp(Easing.Ease(o.WipeStyle, o.WipeDirection, a), 0, 1.15f);

    public override string Signature(WidgetEnv e)
    {
        var sb = new StringBuilder();
        sb.Append(Hover).Append('|').Append(tab).Append('|').Append(item).Append('|').Append(scroll).Append('|').Append(tabs.Count);
        return sb.ToString();
    }

    // ───────── drawing ─────────

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        float dt = lastTime < 0 ? 1 : Math.Clamp(e.Time - lastTime, 0, 0.2f);
        lastTime = e.Time;
        moving = false;
        var text = t[o.TabText]; var active = t[o.ActiveText]; var fill = t[o.ActiveColor];

        // tabs
        for (int i = 0; i < tabs.Count && i < tabRects.Count; i++)
        {
            var r = tabRects[i];
            float a = Ease(Amount(i, Hover == i || tab == i, dt));
            FramePainter.Draw(c, tabFrame, t, r, "", "", fontSize, a > 0, true, a);
            float x = r.X + o.TabPadding;
            var tc = Rgba.Lerp(text, active, a);
            var tb = tabs[i];
            if (tb.icon.Length > 0)
            {
                float box = o.IconBox > 0 ? o.IconBox : fontSize * 1.2f;
                var br = new RectangleF(x - 4, r.Y + (r.Height - box) / 2, box, box);
                if (o.IconBox > 0)
                {
                    c.Fill(br, Rgba.Lerp(fill, t[o.TabColor], a));
                    c.Text(tb.icon, new RectangleF(br.X, br.Y + (br.Height - fontSize * 1.45f) / 2, br.Width, fontSize * 1.6f), Rgba.Lerp(active, fill, a), font, fontSize * 1.1f, false, "center");
                }
                else c.Text(tb.icon, br, tc, font, fontSize, false, "center");
                x += box + 8 - 4;
            }
            FramePainter.DrawText(c, TabText(tb), x, r.Y, r.Height, tc, font, fontSize, false, o.TabSpacing);
        }

        // rule
        if (o.Rule != "none" && tabRects.Count > 0)
        {
            var rc = t[o.RuleColor];
            var br = c.Brush(rc);
            c.Dc.FillRectangle(new Rect(Bounds.X, ruleY, Bounds.Width, 2), br);
            if (o.Rule == "band")
            {
                float y = ruleY + 2 + 6;
                for (float x = Bounds.X; x < Bounds.Right; x += 18) c.Dc.FillRectangle(new Rect(x, y, Math.Min(14, Bounds.Right - x), 2), br);
                y += 7;
                for (float x = Bounds.X + 18; x < Bounds.Right - 8; x += 36)
                {
                    c.Dc.FillRectangle(new Rect(x + 3, y, 2, 2), br);
                    c.Dc.FillRectangle(new Rect(x, y + 4, 2, 2), br);
                    c.Dc.FillRectangle(new Rect(x + 6, y + 4, 2, 2), br);
                }
            }
        }
        if (tab >= tabs.Count) return;
        var cur = tabs[tab];

        // heading
        if (headY >= 0)
        {
            float hs = fontSize * o.HeadingScale, hx = Bounds.X + o.Inset, y = headY;
            string ht = Up(cur.label);
            if (o.HeadingShadow != "none") FramePainter.DrawText(c, ht, hx + 3, y + 3, hs * 1.36f, t[o.HeadingShadow], font, hs, false, o.HeadingSpacing, 0.6f);
            FramePainter.DrawText(c, ht, hx, y, hs * 1.36f, t[o.HeadingColor], font, hs, false, o.HeadingSpacing);
            y += hs * 1.36f + 8;
            if (cur.sub.Length > 0) { FramePainter.DrawText(c, cur.sub.ToUpperInvariant(), hx, y, fontSize * 1.4f, t[o.SubtitleColor], font, fontSize, false, o.SubtitleSpacing); y += fontSize * 1.5f; }
            if (cur.desc.Length > 0) FramePainter.DrawText(c, cur.desc, hx, y, fontSize * 1.3f, t[o.SubtitleColor], font, fontSize * 0.86f, false, 0, 0.85f);
        }

        // rail + scroll bar
        int count = cur.items.Count;
        if (o.RailColor != "none" && count > 0)
        {
            int shown = Math.Min(count, visibleItems);
            float rx = listArea.X - 18 - o.RailWidth, top = listArea.Y, h = shown * (o.ItemHeight + o.ItemGap) - o.ItemGap;
            c.Fill(new RectangleF(rx, top, o.RailWidth, h), t[o.RailColor]);
            if (item >= 0 && item < itemRects.Count && !itemRects[item].IsEmpty)
            {
                var ir = itemRects[item];
                float my = ir.Y + ir.Height / 2, m = Math.Max(3, o.RailWidth * 0.6f);
                using var tri = Tri(c, rx - 3, my, m);
                c.Dc.FillGeometry(tri, c.Brush(fill));
            }
            if (count > visibleItems)
            {
                float sx = listArea.Right + 8, frac = visibleItems / (float)count;
                c.Fill(new RectangleF(sx, top, 4, h), t[o.RailColor]);
                c.Fill(new RectangleF(sx, top + h * scroll / count, 4, Math.Max(10, h * frac)), fill);
            }
        }
        if (count == 0) FramePainter.DrawText(c, "no shortcuts in this group yet (Shortcuts page > Group)", listArea.X, listArea.Y, o.ItemHeight, t[o.LabelColor], font, fontSize, false, 0);

        // items
        for (int i = 0; i < count; i++)
        {
            var r = itemRects[i];
            if (r.IsEmpty) continue;
            float a = Ease(Amount(ItemBase + i, Hover == ItemBase + i || item == i, dt));
            FramePainter.Draw(c, itemFrame, t, r, "", "", fontSize, a > 0, true, a);
            var tc = Rgba.Lerp(text, active, a);
            float x = r.X + 16;
            if (o.Bullet > 0)
            {
                c.Fill(new RectangleF(x, r.Y + (r.Height - o.Bullet) / 2, o.Bullet, o.Bullet), tc);
                x += o.Bullet + 10;
            }
            var name = cur.items[i].ToString();
            c.Text(name, new RectangleF(x, r.Y + (r.Height - fontSize * 1.36f) / 2, r.Right - x - 6, fontSize * 1.5f), tc, font, fontSize, false, "left");
        }

        // details
        if (!detailsRect.IsEmpty && item >= 0 && item < count)
        {
            var s = cur.items[item];
            FramePainter.Draw(c, detailsFrame, t, detailsRect, s.ToString(), "", fontSize);
            var ins = FramePainter.Space(detailsFrame, t, fontSize, true);
            float rowH = fontSize * 2.1f, y = detailsRect.Y + ins.T, x0 = detailsRect.X + ins.L, x1 = detailsRect.Right - ins.R;
            var lc = t[o.LabelColor];
            foreach (var (label, value) in Rows(s))
            {
                float lw = Measure(e, label, fontSize, 0) + fontSize;
                c.Text(label, new RectangleF(x0, y, lw, rowH), lc, font, fontSize, false, "left");
                c.Text(Fit(c, value, x1 - x0 - lw - 6), new RectangleF(x0 + lw, y, x1 - x0 - lw, rowH), text, font, fontSize, false, "right");
                y += rowH;
                if (o.RowLine != "none") c.Fill(new RectangleF(x0 - 2, y - 4, x1 - x0 + 4, 1), t[o.RowLine], 0, 0.8f);
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                var (r, label, _) = buttons[i];
                float a = Ease(Amount(ButtonBase + i, Hover == ButtonBase + i, dt));
                FramePainter.Draw(c, itemFrame, t, r, "", "", fontSize, a > 0, true, a);
                var tc = Rgba.Lerp(text, active, a);
                float bx = r.X + 10;
                float b = Math.Max(6, o.Bullet * 0.8f);
                c.Fill(new RectangleF(bx, r.Y + (r.Height - b) / 2, b, b), tc);
                c.Text(label, new RectangleF(bx + b + 10, r.Y + (r.Height - fontSize * 1.36f) / 2, r.Width, fontSize * 1.5f), tc, font, fontSize, false, "left");
            }
        }
    }

    string Fit(Canvas c, string s, float w)
    {
        if (w <= 10 || c.Measure(s, font, fontSize, false) <= w) return s;
        // keep the end of long paths (the file name matters most)
        for (int cut = 4; cut < s.Length; cut += 2)
        {
            var t2 = "…" + s[cut..];
            if (c.Measure(t2, font, fontSize, false) <= w) return t2;
        }
        return "…";
    }

    static Vortice.Direct2D1.ID2D1PathGeometry Tri(Canvas c, float x, float y, float m)
    {
        var geo = c.Dc.Factory.CreatePathGeometry();
        using var sink = geo.Open();
        sink.BeginFigure(new System.Numerics.Vector2(x - m, y), Vortice.Direct2D1.FigureBegin.Filled);
        sink.AddLine(new System.Numerics.Vector2(x, y - m));
        sink.AddLine(new System.Numerics.Vector2(x, y + m));
        sink.EndFigure(Vortice.Direct2D1.FigureEnd.Closed);
        sink.Close();
        return geo;
    }

    // terminal themes: tab names in a row, the list under them
    public override void DrawCells(Grid g, WidgetEnv e)
    {
        if (e.Smooth) return;
        int cx = (int)(Bounds.X / e.CellW) + 2, cy = (int)(Bounds.Y / e.CellH);
        uint tc = t[o.TabText].Packed, ac = t[o.ActiveColor].Packed, bg = t[o.ActiveText].Packed;
        tabRects.Clear(); itemRects.Clear(); buttons.Clear();
        for (int i = 0; i < tabs.Count; i++)
        {
            var s = $" {TabText(tabs[i])} ";
            bool on = i == tab || Hover == i;
            for (int k = 0; k < s.Length; k++) { g.Put(cx + k, cy, s[k], on ? bg : tc); if (on) g.PutBg(cx + k, cy, ac); }
            tabRects.Add(new RectangleF(cx * e.CellW, cy * e.CellH, s.Length * e.CellW, e.CellH));
            cx += s.Length + 2;
        }
        if (tab >= tabs.Count) return;
        int ly = cy + 3, lx = (int)(Bounds.X / e.CellW) + 2;
        for (int i = 0; i < tabs[tab].items.Count && ly + i * 2 < g.H; i++)
        {
            bool on = i == item || Hover == ItemBase + i;
            var s = $" ■ {tabs[tab].items[i]} ".PadRight(Math.Max(10, (int)(o.ListWidth / e.CellW)));
            for (int k = 0; k < s.Length; k++) { g.Put(lx + k, ly + i * 2, s[k], on ? bg : tc); if (on) g.PutBg(lx + k, ly + i * 2, ac); }
            itemRects.Add(new RectangleF(lx * e.CellW, (ly + i * 2) * e.CellH, s.Length * e.CellW, e.CellH));
        }
    }
}
