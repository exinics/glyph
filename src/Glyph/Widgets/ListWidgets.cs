using System.ComponentModel;
using System.Drawing.Design;
using Glyph.Data;
using Glyph.Options;
using Glyph.Ui;

namespace Glyph.Widgets;

internal sealed class ListOptions : PanelOptions
{
    public override string Type => "list";
    public ListOptions() { Title = "to do"; Anchor = "top_left"; OffsetX = 40; OffsetY = 300; Width = 380; }
    [Category("List"), DisplayName("List"), Description("Which list it shows: its name (config\\lists\\<name>.txt). Click … to edit its items: one per line; [x] at the start = done. Agenda: 19:30 | What | When. Lines starting with # are notes. Several widgets can show the same list."), Editor(typeof(ListItemsEditor), typeof(UITypeEditor))]
    public string List { get; set; } = "todo";
    [Category("List"), Description("checklist = a mark to tick before each item (click an item to tick it). agenda = a time, the item and a note under it. bullets = plain lines."), Choices("checklist", "agenda", "bullets"), TypeConverter(typeof(ChoicesConverter))]
    public string Style { get; set; } = "checklist";
    [Category("List"), DisplayName("Click ticks"), Description("checklist: clicking an item ticks it (and clicking again unticks it).")] public bool ClickTicks { get; set; } = true;
    [Category("List"), DisplayName("Done items"), Description("How ticked items look: strike = crossed out, dim = faded, both = crossed out and faded, hide = not shown."), Choices("strike", "dim", "both", "hide"), TypeConverter(typeof(ChoicesConverter))]
    public string DoneStyle { get; set; } = "both";
    [Category("List"), DisplayName("To-do mark"), Description("checklist: what goes before an item still to do. Text or an icon like {icon:radio_button_unchecked}.")] public string OpenMark { get; set; } = "{icon:radio_button_unchecked}";
    [Category("List"), DisplayName("Done mark"), Description("checklist: before a ticked item, e.g. {icon:check_circle} or {icon:circle}.")] public string DoneMark { get; set; } = "{icon:check_circle}";
    [Category("List"), DisplayName("Bullet"), Description("bullets: what goes before each line.")] public string Bullet { get; set; } = "•";
    [Category("List"), DisplayName("Mark colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string MarkColor { get; set; } = "dim";
    [Category("List"), DisplayName("Done mark colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string DoneMarkColor { get; set; } = "accent";
    [Category("List"), DisplayName("Time colour"), Description("agenda: the time's colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string TimeColor { get; set; } = "bright";
    [Category("List"), DisplayName("Time width"), Description("agenda: pixels kept for the time, so the items line up."), Range(0, 400)] public float TimeWidth { get; set; } = 60;
    [Category("List"), DisplayName("Max items"), Description("Show at most this many (0 = all)."), Range(0, 200)] public int MaxItems { get; set; }
    [Category("List"), DisplayName("Item gap"), Description("Space between items, in lines of text."), Range(0, 5)] public float ItemGap { get; set; } = 0.35f;
    [Category("List"), DisplayName("When empty"), Description("Shown when the list has no items.")] public string EmptyText { get; set; } = "nothing here";
    [Category("List"), DisplayName("Edit on the desktop"), Description("Click an item's text on the desktop to change or delete it (a small box opens at the mouse); the add line adds one.")] public bool DesktopEdit { get; set; } = true;
    [Category("List"), DisplayName("Add line"), Description("The line under the items that adds a new one when clicked. Empty = none.")] public string AddText { get; set; } = "{icon:add} add";
}

/// A to-do list, an agenda or plain lines from a list file; items are ticked by clicking them.
internal sealed class ListWidget : PanelWidget
{
    readonly ListOptions o;
    public ListWidget(ListOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        var col = new ColEl();
        var items = Lists.Get(o.List).Select((it, i) => (it, i)).Where(x => !(x.it.Done && o.DoneStyle == "hide")).ToList();
        if (o.MaxItems > 0) items = items.Take(o.MaxItems).ToList();
        if (items.Count == 0) col.Items.Add(new TextEl(o.EmptyText, Dim));
        bool term = e.Painter.Terminal;
        bool first = true;
        foreach (var (it, index) in items)
        {
            if (!first && o.ItemGap > 0 && !term) col.Items.Add(new GapEl(o.ItemGap));
            first = false;
            bool done = it.Done;
            bool strike = done && o.DoneStyle is "strike" or "both";
            var textColor = done && o.DoneStyle is "dim" or "both" ? Dim : Text;
            El el;
            switch (o.Style)
            {
                case "agenda":
                    {
                        var parts = it.Text.Split('|').Select(p => p.Trim()).ToArray();
                        string time = parts.Length > 1 ? parts[0] : "", what = parts.Length > 1 ? parts[1] : parts[0], note = parts.Length > 2 ? parts[2] : "";
                        var row = new RowEl { Gap = term ? 1 : 1.2f };
                        var tt = new TextEl { Spans = S(time, T[o.TimeColor]), Bold = true };
                        row.Items.Add(term ? tt : new BoxEl { Child = tt, MinW = o.TimeWidth });
                        var body = new ColEl();
                        body.Items.Add(new TextEl { Spans = S(what, textColor), Strike = strike });
                        if (note.Length > 0) body.Items.Add(new TextEl { Spans = S(note, Dim), Size = TextSize.Small, Tight = true });
                        row.Items.Add(body);
                        if (o.DesktopEdit) row.Action = Edit(index);
                        el = row;
                        break;
                    }
                case "bullets":
                    {
                        var row = new RowEl { Gap = 0.8f };
                        row.Items.Add(new TextEl { Spans = S(o.Bullet, T[o.MarkColor]) });
                        row.Items.Add(new TextEl { Spans = S(it.Text, textColor), Strike = strike, Action = o.DesktopEdit ? Edit(index) : "" });
                        el = row;
                        break;
                    }
                default:
                    {
                        var row = new RowEl { Gap = 0.9f };
                        // the mark ticks the item, the words open the edit box
                        row.Items.Add(new TextEl { Spans = S(done ? o.DoneMark : o.OpenMark, T[done ? o.DoneMarkColor : o.MarkColor]), Action = o.ClickTicks ? $"list:toggle:{o.List}:{index}" : "" });
                        row.Items.Add(new TextEl { Spans = S(it.Text, textColor), Strike = strike, Action = o.DesktopEdit ? Edit(index) : o.ClickTicks ? $"list:toggle:{o.List}:{index}" : "" });
                        el = row;
                        break;
                    }
            }
            col.Items.Add(el);
        }
        if (o.DesktopEdit && o.AddText.Length > 0)
        {
            if (!term) col.Items.Add(new GapEl(Math.Max(0.3f, o.ItemGap)));
            col.Items.Add(new TextEl { Spans = S(o.AddText, Dim), Size = TextSize.Small, Action = Edit(-1) });
        }
        return col;
    }

    string Edit(int n) => $"list:edit:{o.Style}:{n}:{o.List}";
}

internal sealed class LyricsOptions : PanelOptions
{
    public override string Type => "lyrics";
    public LyricsOptions() { Anchor = "bottom_left"; OffsetX = 40; OffsetY = 200; Width = 380; Panel = false; }
    [Category("Lyrics"), DisplayName("Lines before"), Description("How many sung lines to show before the current one."), Range(0, 10)] public int Before { get; set; } = 1;
    [Category("Lyrics"), DisplayName("Lines after"), Description("How many lines to show after the current one."), Range(0, 20)] public int After { get; set; } = 2;
    [Category("Lyrics"), DisplayName("Current colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string CurrentColor { get; set; } = "bright";
    [Category("Lyrics"), DisplayName("Other lines colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string OtherColor { get; set; } = "dim";
    [Category("Lyrics"), DisplayName("Current size"), Choices("normal", "large"), TypeConverter(typeof(ChoicesConverter))] public string CurrentSize { get; set; } = "large";
    [Category("Lyrics"), DisplayName("Current bold")] public bool CurrentBold { get; set; } = true;
    [Category("Lyrics"), DisplayName("Fade far lines"), Description("Lines further from the current one get fainter.")] public bool Fade { get; set; } = true;
    [Category("Lyrics"), Description("Seconds to shift the lyrics by if they come early (negative) or late (positive)."), Range(-10, 10)] public float Offset { get; set; }
    [Category("Lyrics"), DisplayName("Hide when nothing plays"), Description("(The widget is empty anyway while nothing plays or the words are being looked up.)")] public bool HideWhenIdle { get; set; } = true;
}

/// The words of the song playing, the line being sung highlighted (time-synced lyrics from lrclib.net).
internal sealed class LyricsWidget : PanelWidget
{
    readonly LyricsOptions o;
    public LyricsWidget(LyricsOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        Lyrics.Use();
        if (!Media.HasSession && o.HideWhenIdle) return null;
        var col = new ColEl();
        var (lines, cur) = Lyrics.Around(o.Before, o.After, o.Offset);
        if (lines.Count == 0) return null;   // nothing playing, looking them up, or none found: just empty
        float maxW = o.Width > 0 ? o.Width - 4 : 360;
        for (int i = 0; i < lines.Count; i++)
        {
            bool now = i == cur;
            var size = now && o.CurrentSize == "large" ? TextSize.Large : TextSize.Normal;
            var c = now ? T[o.CurrentColor] : T[o.OtherColor];
            if (!now && o.Fade && cur >= 0) c = c.WithAlpha(Math.Clamp(1f - 0.3f * (Math.Abs(i - cur) - 1), 0.25f, 1f));
            foreach (var part in Wrap(e, lines[i], maxW, size, now && o.CurrentBold))
                col.Items.Add(new TextEl { Spans = { new Span(part, c) }, Size = size, Bold = now && o.CurrentBold, Tight = true });
            if (i < lines.Count - 1 && !e.Painter.Terminal) col.Items.Add(new GapEl(0.25f));
        }
        return col;
    }

    /// Splits a line into pieces that fit `width` pixels (at spaces).
    static List<string> Wrap(WidgetEnv e, string text, float width, TextSize size, bool bold)
    {
        var p = e.Painter;
        if (p.Terminal || p.Canvas == null) return new List<string> { text };
        float px = p.Px(size);
        float W(string s) => Glyph.Render.FramePainter.MeasureText(p.Canvas!, s, p.Font, px, bold, 0);
        var res = new List<string>();
        var cur = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && W(next) > width) { res.Add(cur); cur = word; }
            else cur = next;
        }
        if (cur.Length > 0) res.Add(cur);
        return res.Count > 0 ? res : new List<string> { text };
    }
}

/// The settings window's editor for a list widget's items: the list file as text, one item per line.
internal sealed class ListItemsEditor : UITypeEditor
{
    public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.Modal;

    public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
    {
        var name = (value as string ?? "").Trim();
        if (name.Length == 0) name = "todo";
        using var f = new Form
        {
            Text = $"List \"{name}\"", Width = 560, Height = 480, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            BackColor = Color.FromArgb(24, 24, 24), ForeColor = Color.FromArgb(230, 230, 230), Font = new Font("Segoe UI", 10f), MinimizeBox = false, MaximizeBox = false,
        };
        var hint = new Label
        {
            Dock = DockStyle.Top, Height = 58, Padding = new Padding(10, 8, 10, 0), ForeColor = Color.FromArgb(160, 160, 160),
            Text = "One item per line. [x] at the start = done (clicking an item on the desktop ticks it).\nAgenda: 19:30 | Maths class | Today.   Lines starting with # are notes.",
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true, WordWrap = false,
            BackColor = Color.FromArgb(14, 14, 14), ForeColor = Color.FromArgb(230, 230, 230), BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 10.5f),
            Text = Lists.Text(name).Replace("\r\n", "\n").Replace("\n", "\r\n"),
        };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        Button B(string t, DialogResult r) => new() { Text = t, DialogResult = r, Width = 96, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.FromArgb(230, 230, 230) };
        var ok = B("Save", DialogResult.OK); var cancel = B("Cancel", DialogResult.Cancel);
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
        f.CancelButton = cancel;
        f.Controls.Add(box); f.Controls.Add(hint); f.Controls.Add(buttons);
        if (f.ShowDialog() == DialogResult.OK) Lists.Save(name, box.Text);
        return value;
    }
}
