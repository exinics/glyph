using System.ComponentModel;
using Glyph.Options;
using Glyph.Render;
using Vortice.Mathematics;

namespace Glyph.Widgets;

internal sealed class DividerOptions : WidgetOptions
{
    public override string Type => "divider";
    public DividerOptions() { Anchor = "top"; OffsetX = 20; OffsetY = 200; Stretch = "width"; }
    [Category("Divider"), Description("Which way the line runs."), Choices("horizontal", "vertical"), TypeConverter(typeof(ChoicesConverter))] public string Direction { get; set; } = "horizontal";
    [Category("Divider"), Description("Length in pixels (when it isn't stretched to the screen's edges with Stretch)."), Range(1, 8000)] public float Length { get; set; } = 400;
    [Category("Divider"), Description("solid = one line. dashed = dashes. dotted = dots. double = two thin lines. band = a line, a row of dashes under it and a row of little dot triangles (a printed-form look)."),
     Choices("solid", "dashed", "dotted", "double", "band"), TypeConverter(typeof(ChoicesConverter))] public string Style { get; set; } = "solid";
    [Category("Divider"), Description("Thickness of the line, in pixels."), Range(0.5, 100)] public float Thickness { get; set; } = 2;
    [Category("Divider"), DisplayName("Line colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string LineColor { get; set; } = "text";
    [Category("Divider"), DisplayName("Line opacity"), Range(0, 1)] public float LineOpacity { get; set; } = 1;
    [Category("Divider"), DisplayName("Dash length"), Description("dashed and band: length of each dash, in pixels."), Range(1, 400)] public float DashLength { get; set; } = 14;
    [Category("Divider"), DisplayName("Dash gap"), Description("Pixels between dashes or dots."), Range(1, 400)] public float DashGap { get; set; } = 4;
    [Category("Divider"), DisplayName("Band spacing"), Description("band: pixels between the dot triangles."), Range(4, 400)] public float BandSpacing { get; set; } = 36;
    [Category("Divider"), DisplayName("Band colour"), Description("band: colour of the dashes and dots. auto = the line colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string BandColor { get; set; } = "auto";
    public override string ToString() => $"divider: {Direction} {Style}{(Enabled ? "" : " (off)")}";
}

/// A line across (or down) the screen: solid, dashed, dotted, double or a "band" (line + dashes + dot triangles).
internal sealed class DividerWidget : Widget
{
    readonly DividerOptions o;
    readonly Theme t;
    public DividerWidget(DividerOptions o, Theme t) { this.o = o; this.t = t; }

    bool Vertical => o.Direction == "vertical";

    /// How thick the whole thing is across the line (band adds its rows).
    float Across => o.Style switch { "band" => o.Thickness + 6 + 2 + 5 + 4, "double" => o.Thickness * 3, _ => o.Thickness };

    public override void Layout(WidgetEnv e)
    {
        float len = Vertical ? (o.StretchH ? WantH(o, 0, e) : o.Length) : (o.StretchW ? WantW(o, 0, e) : o.Length);
        float w = Vertical ? Across : len, h = Vertical ? len : Across;
        var pt = PlaceFor(o, w, h, e.Area);
        Bounds = new RectangleF(pt.X, pt.Y, w, h);
    }

    public override void DrawCells(Grid g, WidgetEnv e)
    {
        if (e.Smooth) return;
        uint col = t[o.LineColor].Packed;
        int x0 = (int)(Bounds.X / e.CellW), y0 = (int)(Bounds.Y / e.CellH);
        int n = (int)((Vertical ? Bounds.Height / e.CellH : Bounds.Width / e.CellW));
        int ch = o.Style switch { "dashed" or "band" => Vertical ? '╎' : '╌', "dotted" => '·', "double" => Vertical ? '║' : '═', _ => Vertical ? '│' : '─' };
        for (int i = 0; i < n; i++) g.Put(Vertical ? x0 : x0 + i, Vertical ? y0 + i : y0, ch, col);
    }

    public override void DrawVector(Canvas c, WidgetEnv e)
    {
        var lc = t[o.LineColor];
        var bc = o.BandColor == "auto" ? lc : t[o.BandColor];
        float op = o.LineOpacity, th = o.Thickness;
        var r = Bounds;
        // draws along the line: `a` = distance along, `b` = distance across, `l` = length along, `w` = thickness across
        void Box(float a, float b, float l, float w, Rgba col)
        {
            var rect = Vertical ? new Rect(r.X + b, r.Y + a, w, l) : new Rect(r.X + a, r.Y + b, l, w);
            c.Dc.FillRectangle(rect, c.Brush(col, op));
        }
        float len = Vertical ? r.Height : r.Width;
        void Dashes(float b, float w, float dash, float gap, Rgba col)
        {
            for (float a = 0; a < len; a += dash + gap) Box(a, b, Math.Min(dash, len - a), w, col);
        }
        switch (o.Style)
        {
            case "dashed": Dashes(0, th, o.DashLength, o.DashGap, lc); break;
            case "dotted": Dashes(0, th, th, Math.Max(o.DashGap, 1), lc); break;
            case "double": Box(0, 0, len, Math.Max(1, th * 0.7f), lc); Box(0, th * 2.3f, len, Math.Max(1, th * 0.7f), lc); break;
            case "band":
                {
                    Box(0, 0, len, th, lc);
                    float y = th + 6;
                    Dashes(y, 2, o.DashLength, o.DashGap, bc);
                    y += 2 + 5;
                    for (float a = o.BandSpacing / 2; a < len - 8; a += o.BandSpacing)
                    {
                        Box(a + 3, y, 2, 2, bc);
                        Box(a, y + 4, 2, 2, bc);
                        Box(a + 6, y + 4, 2, 2, bc);
                    }
                    break;
                }
            default: Box(0, 0, len, th, lc); break;
        }
    }

    public override string Signature(WidgetEnv e) => "";
    public override IEnumerable<Part> Parts() => new[] { new Part("Line", RectangleF.Inflate(Bounds, 0, Math.Max(0, (8 - Bounds.Height) / 2)), new[] { "Divider", "Placement" }) };
}
