using System.ComponentModel;
using Glyph.Data;
using Glyph.Options;
using Glyph.Render;
using Vortice.Mathematics;

namespace Glyph.Widgets;

internal sealed class TogglesOptions : WidgetOptions
{
    public override string Type => "toggles";
    public TogglesOptions() { Anchor = "bottom_left"; OffsetX = 40; OffsetY = 40; }
    [Category("Toggles"), Description("Title over the buttons. Empty = none.")] public string Title { get; set; } = "";
    [Category("Toggles"), Description("One button per line: icon | label | what clicking does | when it's on. The icon is {icon:name} (Icons page) or any text; the action is any click action (ms-settings:…, volume:mute, keys:win+l, a program…); 'when it's on' is a {value} — the button lights up when it isn't empty, off or 0 (e.g. {muted}, {bluetooth}, {wifi}). Leave the last part empty for a plain button."),
     TypeConverter(typeof(ListConverter))]
    public string[] Buttons { get; set; } =
    {
        "{icon:wifi} | Wi-Fi | ms-settings:network-wifi | {wifi}",
        "{icon:bluetooth} | Bluetooth | ms-settings:bluetooth | {bluetooth}",
        "{icon:volume_off} | Mute | volume:mute | {muted}",
        "{icon:notifications} | Alerts | keys:win+n | ",
        "{icon:screenshot_region} | Snip | keys:win+shift+s | ",
        "{icon:lock} | Lock | keys:win+l | ",
    };
    [Category("Toggles"), Description("Buttons side by side (then a new row)."), Range(1, 20)] public int Columns { get; set; } = 6;
    [Category("Toggles"), DisplayName("Button size"), Description("Width and height of each button, in pixels."), Range(24, 400)] public float ButtonSize { get; set; } = 72;
    [Category("Toggles"), Description("Pixels between buttons."), Range(0, 100)] public float Gap { get; set; } = 10;
    [Category("Toggles"), DisplayName("Show labels"), Description("The label under each icon.")] public bool Labels { get; set; } = true;
    [Category("Toggles"), DisplayName("Icon size"), Description("Size of the icons compared with normal text."), Range(0.5, 6)] public float IconScale { get; set; } = 1.9f;
    [Category("Toggles"), DisplayName("Button frame"), Description("A frame for the buttons (Frames page). none = plain rounded buttons."), TypeConverter(typeof(FrameNameConverter))] public string ButtonFrame { get; set; } = "none";
    [Category("Toggles"), DisplayName("On frame"), Description("A frame that fades in on buttons that are on (and the one under the mouse, half way). none = the On colour as a glow."), TypeConverter(typeof(FrameNameConverter))] public string OnFrame { get; set; } = "none";
    [Category("Toggles"), DisplayName("Icon colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string IconColor { get; set; } = "text";
    [Category("Toggles"), DisplayName("On colour"), Description("Icon colour of buttons that are on."), Color, TypeConverter(typeof(ColorNameConverter))] public string OnColor { get; set; } = "accent";
    [Category("Toggles"), DisplayName("Label colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string LabelColor { get; set; } = "dim";
    [Category("Toggles"), DisplayName("Hover lift"), Description("Pixels a button rises under the mouse."), Range(0, 40)] public float Lift { get; set; } = 3;
    [Category("Toggles"), DisplayName("Hover time"), Description("Milliseconds the hover rise and glow take."), Range(0, 2000)] public float HoverMs { get; set; } = 140;
    public override string ToString() => $"toggles{(Enabled ? "" : " (off)")}";
}

/// A grid of quick buttons: each has an icon, a label, a click action and (if you like) a value that says it's on.
/// Buttons rise and glow under the mouse; buttons that are on are lit.
internal sealed class TogglesWidget : Widget
{
    readonly TogglesOptions o;
    readonly Theme t;
    readonly List<(string icon, string label, string action, string state)> buttons = new();
    readonly List<RectangleF> rects = new();
    readonly FrameOptions? panel, buttonFrame, onFrame;
    readonly Dictionary<int, float> lift = new();
    float lastTime = -1, fontSize, titleH;
    string font = "";
    bool moving;

    public TogglesWidget(TogglesOptions o, Theme t)
    {
        this.o = o; this.t = t;
        panel = t.Frame(o.Frame, t.Options.Ui.PanelFrame);
        buttonFrame = t.Frame(o.ButtonFrame);
        onFrame = t.Frame(o.OnFrame);
        foreach (var line in o.Buttons)
        {
            var bits = line.Split('|').Select(b => b.Trim()).ToArray();
            if (bits.Length == 0 || bits.All(b => b.Length == 0)) continue;
            buttons.Add((bits.ElementAtOrDefault(0) ?? "", bits.ElementAtOrDefault(1) ?? "", bits.ElementAtOrDefault(2) ?? "", bits.ElementAtOrDefault(3) ?? ""));
        }
    }

    public override bool Animated => moving;

    static bool IsOn(string state)
    {
        if (state.Length == 0) return false;
        var v = Fmt.Text(state, Stats.Value).Trim().ToLowerInvariant();
        return v.Length > 0 && v is not ("off" or "0" or "false" or "no" or "--");
    }

    public override void Layout(WidgetEnv e)
    {
        font = e.Painter.Font; fontSize = e.BaseFontSize;
        bool titled = o.Title.Length > 0;
        var pad = panel != null ? FramePainter.Space(panel, t, fontSize, titled) : new FramePainter.Insets(0, titled ? fontSize * 2 : 0, 0, 0);
        titleH = pad.T;
        int cols = Math.Clamp(o.Columns, 1, Math.Max(1, buttons.Count)), rows = (buttons.Count + cols - 1) / cols;
        float w = pad.L + pad.R + cols * o.ButtonSize + (cols - 1) * o.Gap;
        float h = pad.T + pad.B + rows * o.ButtonSize + Math.Max(0, rows - 1) * o.Gap;
        w = WantW(o, w, e); h = WantH(o, h, e);
        var pt = PlaceFor(o, w, h, e.Area);
        Bounds = new RectangleF(pt.X, pt.Y, w, h);
        rects.Clear();
        for (int i = 0; i < buttons.Count; i++)
            rects.Add(new RectangleF(pt.X + pad.L + (i % cols) * (o.ButtonSize + o.Gap), pt.Y + pad.T + (i / cols) * (o.ButtonSize + o.Gap), o.ButtonSize, o.ButtonSize));
    }

    public override int HitTest(float x, float y)
    {
        for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(x, y)) return i;
        return -1;
    }

    public override void Click(int item, int count, WidgetEnv e)
    {
        if (count == 1 && item >= 0 && item < buttons.Count && buttons[item].action.Length > 0) e.Do?.Invoke(buttons[item].action);
    }

    public override IEnumerable<Part> Parts()
    {
        yield return new Part("Box", Bounds, new[] { "Placement", "Box", "Toggles" }, "Frame");
        foreach (var r in rects) yield return new Part("Button", r, new[] { "Toggles" }, "ButtonFrame");
    }

    public override string Signature(WidgetEnv e) =>
        Hover + "|" + string.Concat(buttons.Select(b => IsOn(b.state) ? '1' : '0'));

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        float dt = lastTime < 0 ? 1 : Math.Clamp(e.Time - lastTime, 0, 0.2f);
        lastTime = e.Time;
        moving = false;
        if (panel != null) FramePainter.Draw(c, panel, t, Bounds, o.Title, "", fontSize);
        else if (o.Title.Length > 0) FramePainter.DrawText(c, o.Title, Bounds.X, Bounds.Y, fontSize * 1.6f, t["dim"], font, fontSize * 0.9f, false, 2);
        var onC = t[o.OnColor]; var iconC = t[o.IconColor];
        for (int i = 0; i < buttons.Count && i < rects.Count; i++)
        {
            var (icon, label, _, state) = buttons[i];
            bool on = IsOn(state), hot = Hover == i;
            // hover: rise and light up, eased
            float target = hot ? 1 : 0, a = lift.TryGetValue(i, out var v) ? v : target;
            if (o.HoverMs <= 0) a = target;
            else if (a != target) { float st = dt * 1000 / o.HoverMs; a = target > a ? Math.Min(1, a + st) : Math.Max(0, a - st); if (a != target) moving = true; }
            lift[i] = a;
            float ease = 1 - (1 - a) * (1 - a);
            var r = rects[i]; r.Offset(0, -o.Lift * ease);
            if (buttonFrame != null) FramePainter.Draw(c, buttonFrame, t, r, "", "", fontSize, hot, true);
            else { c.Fill(r, t["surface"], 12, 0.9f); c.Stroke(r, t["line"], 1, 12); if (hot) c.Fill(r, t["bright"], 12, 0.05f * ease); }
            float glow = on ? 1 : ease * 0.5f;
            if (glow > 0.01f)
            {
                if (onFrame != null)
                {
                    c.Dc.PushLayer(new Vortice.Direct2D1.LayerParameters1 { ContentBounds = new Rect(r.X - 30, r.Y - 30, r.Width + 60, r.Height + 60), Opacity = glow, MaskTransform = System.Numerics.Matrix3x2.Identity }, null);
                    FramePainter.Draw(c, onFrame, t, r, "", "", fontSize, false, true);
                    c.Dc.PopLayer();
                }
                else c.Stroke(r, onC, 1.5f, 12, glow);
            }
            float isz = fontSize * o.IconScale, top = r.Y + (o.Labels ? r.Height * 0.16f : (r.Height - isz * 1.3f) / 2);
            c.Text(icon, new RectangleF(r.X, top, r.Width, isz * 1.4f), on ? onC : Rgba.Lerp(iconC, onC, ease * 0.5f), font, isz, false, "center");
            if (o.Labels && label.Length > 0)
                c.Text(label, new RectangleF(r.X + 2, r.Bottom - fontSize * 1.9f, r.Width - 4, fontSize * 1.5f), on ? t["text"] : t[o.LabelColor], font, fontSize * 0.86f, false, "center");
        }
    }
}
