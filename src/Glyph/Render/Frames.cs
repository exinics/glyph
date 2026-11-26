using System.ComponentModel;
using System.Numerics;
using System.Text;
using Glyph.Options;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Glyph.Render;

/// frames.toml in a theme folder: named box designs ("frames") that panels, bar modules, buttons, the launcher
/// and windows can use. Each frame is a [[frame]] table; everything else refers to it by name.
internal sealed class FramesFile
{
    public List<FrameOptions> Frames { get; set; } = new();

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "frames.toml");

    public static FramesFile Load(Theme theme, List<string> problems)
    {
        var f = new FramesFile();
        var path = FilePath(theme.Name);
        if (!File.Exists(path)) return f;
        var file = ConfigFile.Load(path, problems);
        foreach (var n in file.Top.Array("frame"))
        {
            var o = new FrameOptions();
            Binder.Read(o, n, theme);
            if (o.Name.Trim().Length == 0) { problems.Add($"{file.Name}: {n.Label} needs a name, e.g. name = \"panel\""); continue; }
            f.Frames.Add(o);
        }
        file.ReportUnknown();
        return f;
    }

    public void Save(string theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Frames: named box designs for panels, bar modules, buttons, the launcher and windows.");
        sb.AppendLine("# Easiest to change in the settings window (Frames page, with a live preview).");
        sb.AppendLine("# Widgets, modules etc. pick one by name (their Frame option). Smooth style only.");
        foreach (var o in Frames) Binder.WriteTable(sb, "frame", o);
        SafeFile.Write(FilePath(theme), sb.ToString());
    }
}

/// One box design. All sizes are in pixels.
internal sealed class FrameOptions
{
    [Category("1. Frame"), Description("The name other things use to pick this frame.")] public string Name { get; set; } = "frame";
    [Category("1. Frame"), Description("Space between the frame and what's inside, in pixels. -1 = the theme's panel padding."), Range(-1, 200)] public float Padding { get; set; } = -1;
    [Category("1. Frame"), DisplayName("Padding across"), Description("Space at the left and right if different. -1 = same as Padding."), Range(-1, 200)] public float PaddingX { get; set; } = -1;

    [Category("2. Shape"), DisplayName("Cut top-left"), Description("Cut this corner off at 45°, this many pixels. 0 = a normal corner."), Range(0, 400)] public float CutTopLeft { get; set; }
    [Category("2. Shape"), DisplayName("Cut top-right"), Range(0, 400)] public float CutTopRight { get; set; }
    [Category("2. Shape"), DisplayName("Cut bottom-right"), Range(0, 400)] public float CutBottomRight { get; set; }
    [Category("2. Shape"), DisplayName("Cut bottom-left"), Range(0, 400)] public float CutBottomLeft { get; set; }
    [Category("2. Shape"), DisplayName("Corner radius"), Description("Round the corners that aren't cut. -1 = the theme's."), Range(-1, 200)] public float Radius { get; set; }
    [Category("2. Shape"), Description("Lean the whole box sideways (like / or \\), in pixels across its height. Text stays upright."), Range(-200, 200)] public float Slant { get; set; }

    [Category("3. Fill"), Description("Colour inside. none = see-through."), Color, TypeConverter(typeof(ColorNameConverter))] public string Fill { get; set; } = "surface";
    [Category("3. Fill"), DisplayName("Fill opacity"), Range(0, 1)] public float FillOpacity { get; set; } = 0.9f;
    [Category("3. Fill"), DisplayName("Fill to"), Description("A second colour: the fill fades from Fill to this one. none = one colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string Fill2 { get; set; } = "none";
    [Category("3. Fill"), DisplayName("Fade angle"), Description("Direction of the fade, in degrees (90 = top to bottom)."), Range(-360, 360)] public float FillAngle { get; set; } = 90;
    [Category("3. Fill"), Description("A pattern over the fill."), Choices("none", "scanlines", "grid", "dots", "diagonal", "crosshatch", "vlines"), TypeConverter(typeof(ChoicesConverter))] public string Pattern { get; set; } = "none";
    [Category("3. Fill"), DisplayName("Pattern colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string PatternColor { get; set; } = "line";
    [Category("3. Fill"), DisplayName("Pattern opacity"), Range(0, 1)] public float PatternOpacity { get; set; } = 0.35f;
    [Category("3. Fill"), DisplayName("Pattern spacing"), Description("Pixels between the pattern's lines or dots."), Range(2, 200)] public float PatternSpacing { get; set; } = 4;
    [Category("3. Fill"), DisplayName("Hover fill"), Description("Fill colour under the mouse, when it does something on click. none = no change."), Color, TypeConverter(typeof(ColorNameConverter))] public string HoverFill { get; set; } = "none";

    [Category("4. Border"), Description("Border colour. none = no border."), Color, TypeConverter(typeof(ColorNameConverter))] public string Border { get; set; } = "line";
    [Category("4. Border"), DisplayName("Border width"), Range(0, 50)] public float BorderWidth { get; set; } = 1;
    [Category("4. Border"), DisplayName("Border opacity"), Range(0, 1)] public float BorderOpacity { get; set; } = 1;
    [Category("4. Border"), DisplayName("Border sides"), Description("Which sides get the border."), Choices("all", "top", "bottom", "left", "right", "top_bottom", "left_right", "top_left", "bottom_right"), TypeConverter(typeof(ChoicesConverter))] public string BorderSides { get; set; } = "all";
    [Category("4. Border"), DisplayName("Dashes"), Description("Dash length for a dashed border. 0 = solid."), Range(0, 100)] public float Dash { get; set; }
    [Category("4. Border"), DisplayName("Inner border"), Description("A second line inside the first. none = no second line."), Color, TypeConverter(typeof(ColorNameConverter))] public string InnerBorder { get; set; } = "none";
    [Category("4. Border"), DisplayName("Inner gap"), Description("Pixels between the border and the inner border."), Range(0, 100)] public float InnerGap { get; set; } = 3;
    [Category("4. Border"), DisplayName("Inner opacity"), Range(0, 1)] public float InnerOpacity { get; set; } = 0.6f;

    [Category("5. Title"), Description("Where the title goes. plain = top-left inside. bar = a strip across the top. tab = a tab sticking up from the top edge. line = the title with a line running to the right. above = over the box with a line. none = no title."),
     Choices("plain", "bar", "tab", "line", "above", "none"), TypeConverter(typeof(ChoicesConverter))]
    public string Header { get; set; } = "plain";
    [Category("5. Title"), DisplayName("Title colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string TitleColor { get; set; } = "accent";
    [Category("5. Title"), DisplayName("Right text colour"), Description("For the text on the right of the title (Title right)."), Color, TypeConverter(typeof(ColorNameConverter))] public string TitleRightColor { get; set; } = "dim";
    [Category("5. Title"), DisplayName("Title font"), Description("Empty = the theme's font."), TypeConverter(typeof(FontNameConverter))] public string TitleFont { get; set; } = "";
    [Category("5. Title"), DisplayName("Title size"), Description("In pixels. 0 = the theme's title size."), Range(0, 200)] public float TitleSize { get; set; }
    [Category("5. Title"), DisplayName("Bold title")] public bool TitleBold { get; set; } = true;
    [Category("5. Title"), DisplayName("Capitals"), Description("Show the title in capitals.")] public bool TitleUpper { get; set; }
    [Category("5. Title"), DisplayName("Letter spacing"), Description("Extra pixels between the title's letters."), Range(-5, 50)] public float TitleSpacing { get; set; }
    [Category("5. Title"), DisplayName("Strip colour"), Description("bar and tab: the strip's colour."), Color, TypeConverter(typeof(ColorNameConverter))] public string HeaderFill { get; set; } = "accent_dim";
    [Category("5. Title"), DisplayName("Strip opacity"), Range(0, 1)] public float HeaderOpacity { get; set; } = 1;
    [Category("5. Title"), DisplayName("Strip height"), Description("bar and tab: height of the strip. 0 = fit the title."), Range(0, 200)] public float HeaderHeight { get; set; }
    [Category("5. Title"), DisplayName("Line under title"), Description("A line under the title strip. none = no line."), Color, TypeConverter(typeof(ColorNameConverter))] public string HeaderLine { get; set; } = "none";
    [Category("5. Title"), DisplayName("Tab width"), Description("tab: width of the tab. 0 = fit the title."), Range(0, 2000)] public float TabWidth { get; set; }
    [Category("5. Title"), DisplayName("Tab slant"), Description("tab: how far the tab's right side leans out, in pixels."), Range(0, 200)] public float TabSlant { get; set; } = 10;
    [Category("5. Title"), DisplayName("Tab offset"), Description("tab: distance from the box's left edge."), Range(0, 2000)] public float TabOffset { get; set; }

    [Category("6. Corners & marks"), Description("Corner brackets ⌜ ⌟ drawn over the corners. none = no brackets."), Color, TypeConverter(typeof(ColorNameConverter))] public string Brackets { get; set; } = "none";
    [Category("6. Corners & marks"), DisplayName("Bracket corners"), Choices("all", "top", "bottom", "left", "right", "top_left_bottom_right", "top_right_bottom_left"), TypeConverter(typeof(ChoicesConverter))] public string BracketCorners { get; set; } = "all";
    [Category("6. Corners & marks"), DisplayName("Bracket length"), Range(1, 400)] public float BracketLength { get; set; } = 10;
    [Category("6. Corners & marks"), DisplayName("Bracket width"), Range(0.5, 20)] public float BracketWidth { get; set; } = 2;
    [Category("6. Corners & marks"), DisplayName("Bracket offset"), Description("Move the brackets out from the corners (negative = in)."), Range(-50, 50)] public float BracketOffset { get; set; } = 3;
    [Category("6. Corners & marks"), DisplayName("Corner mark"), Description("A small filled triangle in one corner. none = no mark."), Color, TypeConverter(typeof(ColorNameConverter))] public string CornerMark { get; set; } = "none";
    [Category("6. Corners & marks"), DisplayName("Mark corner"), Choices("top_left", "top_right", "bottom_left", "bottom_right"), TypeConverter(typeof(ChoicesConverter))] public string MarkCorner { get; set; } = "bottom_right";
    [Category("6. Corners & marks"), DisplayName("Mark size"), Range(1, 200)] public float MarkSize { get; set; } = 8;

    [Category("7. Decorations"), Description("A thick stripe along one side. none = no stripe."), Color, TypeConverter(typeof(ColorNameConverter))] public string Stripe { get; set; } = "none";
    [Category("7. Decorations"), DisplayName("Stripe side"), Choices("left", "right", "top", "bottom"), TypeConverter(typeof(ChoicesConverter))] public string StripeSide { get; set; } = "left";
    [Category("7. Decorations"), DisplayName("Stripe width"), Range(0.5, 100)] public float StripeWidth { get; set; } = 3;
    [Category("7. Decorations"), DisplayName("Stripe gap"), Description("Pixels between the box and the stripe (0 = on the edge)."), Range(-50, 100)] public float StripeGap { get; set; }
    [Category("7. Decorations"), Description("A block of diagonal stripes ////// at a corner. none = no hatch."), Color, TypeConverter(typeof(ColorNameConverter))] public string Hatch { get; set; } = "none";
    [Category("7. Decorations"), DisplayName("Hatch corner"), Choices("top_left", "top_right", "bottom_left", "bottom_right"), TypeConverter(typeof(ChoicesConverter))] public string HatchCorner { get; set; } = "top_left";
    [Category("7. Decorations"), DisplayName("Hatch outside"), Description("Put the hatch just outside the box (above or below it) instead of inside.")] public bool HatchOutside { get; set; } = true;
    [Category("7. Decorations"), DisplayName("Hatch width"), Range(2, 1000)] public float HatchWidth { get; set; } = 36;
    [Category("7. Decorations"), DisplayName("Hatch height"), Range(2, 200)] public float HatchHeight { get; set; } = 5;
    [Category("7. Decorations"), Description("Ruler ticks along one side. none = no ticks."), Color, TypeConverter(typeof(ColorNameConverter))] public string Ticks { get; set; } = "none";
    [Category("7. Decorations"), DisplayName("Ticks side"), Choices("top", "bottom", "left", "right"), TypeConverter(typeof(ChoicesConverter))] public string TicksSide { get; set; } = "bottom";
    [Category("7. Decorations"), DisplayName("Tick spacing"), Range(2, 200)] public float TickSpacing { get; set; } = 6;
    [Category("7. Decorations"), DisplayName("Tick length"), Description("Every fifth tick is twice as long."), Range(1, 100)] public float TickLength { get; set; } = 3;
    [Category("7. Decorations"), Description("A soft glow around the box. none = no glow."), Color, TypeConverter(typeof(ColorNameConverter))] public string Glow { get; set; } = "none";
    [Category("7. Decorations"), DisplayName("Glow size"), Range(1, 100)] public float GlowSize { get; set; } = 8;
    [Category("7. Decorations"), DisplayName("Glow strength"), Range(0, 1)] public float GlowOpacity { get; set; } = 0.35f;
    [Category("7. Decorations"), Description("A soft shadow under the box. -1 = the theme's."), Range(-1, 1)] public float Shadow { get; set; }

    public override string ToString() => Name;
}

/// Draws frames (smooth style) and works out the space they take.
internal static class FramePainter
{
    public readonly record struct Insets(float L, float T, float R, float B);

    static float Pad(FrameOptions f, Theme t) => f.Padding >= 0 ? f.Padding : t.Options.Ui.Padding;
    static float PadX(FrameOptions f, Theme t) => f.PaddingX >= 0 ? f.PaddingX : Pad(f, t);

    public static float TitleSize(FrameOptions f, Theme t, float baseSize) => f.TitleSize > 0 ? f.TitleSize : baseSize * 1.05f;
    public static string TitleFont(FrameOptions f, Theme t) => f.TitleFont.Length > 0 ? f.TitleFont : t.Options.Ui.Font;
    static string Cased(FrameOptions f, string s) => f.TitleUpper ? s.ToUpperInvariant() : s;

    /// Height of the title strip (bar / tab).
    public static float StripH(FrameOptions f, Theme t, float baseSize) => f.HeaderHeight > 0 ? f.HeaderHeight : TitleSize(f, t, baseSize) * 1.75f;

    /// Space above the box itself (for tab and above titles).
    public static float Above(FrameOptions f, Theme t, float baseSize, bool hasTitle) =>
        !hasTitle ? 0 : f.Header switch { "tab" => StripH(f, t, baseSize), "above" => TitleSize(f, t, baseSize) * 1.6f, _ => 0 };

    /// Space between the outer edge (including a tab) and the contents.
    public static Insets Space(FrameOptions f, Theme t, float baseSize, bool hasTitle)
    {
        float p = Pad(f, t), px = PadX(f, t), bw = f.Border != "none" ? f.BorderWidth : 0;
        float top = p + bw + Above(f, t, baseSize, hasTitle);
        if (hasTitle)
            top += f.Header switch
            {
                "bar" => StripH(f, t, baseSize),
                "plain" or "line" => TitleSize(f, t, baseSize) * 1.5f,
                _ => 0,
            };
        float slant = Math.Abs(f.Slant);
        return new Insets(px + bw + slant, top, px + bw + slant, p + bw);
    }

    /// Text with extra letter spacing (drawn letter by letter when spaced).
    public static float MeasureText(Canvas c, string s, string font, float size, bool bold, float spacing)
    {
        if (s.Length == 0) return 0;
        if (Math.Abs(spacing) < 0.01f) return c.Measure(s, font, size, bold);
        float w = 0;
        foreach (var ch in s) w += c.Measure(ch.ToString(), font, size, bold) + spacing;
        return w - spacing;
    }

    public static void DrawText(Canvas c, string s, float x, float y, float h, Rgba color, string font, float size, bool bold, float spacing, float opacity = 1)
    {
        if (s.Length == 0) return;
        float ty = y + (h - size * 1.36f) / 2;
        if (Math.Abs(spacing) < 0.01f)
        {
            float w = c.Measure(s, font, size, bold);
            c.Text(s, new RectangleF(x, ty, w + 4, size * 1.5f), color, font, size, bold, "left", false, opacity);
            return;
        }
        foreach (var ch in s)
        {
            var t = ch.ToString();
            float w = c.Measure(t, font, size, bold);
            c.Text(t, new RectangleF(x, ty, w + 4, size * 1.5f), color, font, size, bold, "left", false, opacity);
            x += w + spacing;
        }
    }

    static Vector2 V(float x, float y) => new(x, y);

    /// The box outline: corners cut, rounded or square; leaning by `slant`.
    static ID2D1PathGeometry Outline(ID2D1Factory fac, RectangleF r, FrameOptions f, float radius, float inset = 0)
    {
        if (inset != 0) r = RectangleF.Inflate(r, -inset, -inset);
        float max = Math.Min(r.Width, r.Height) / 2;
        float tl = Math.Min(max, Math.Max(0, f.CutTopLeft - inset * 0.41f)), tr = Math.Min(max, Math.Max(0, f.CutTopRight - inset * 0.41f));
        float br = Math.Min(max, Math.Max(0, f.CutBottomRight - inset * 0.41f)), bl = Math.Min(max, Math.Max(0, f.CutBottomLeft - inset * 0.41f));
        float rad = Math.Min(max, Math.Max(0, radius - inset));
        float s = f.Slant;   // the top moves right by s/2, the bottom left by s/2
        float x0 = r.X + Math.Max(0, s) * 0.5f + Math.Max(0, -s) * 0.5f, y0 = r.Y, x1 = r.Right - Math.Max(0, s) * 0.5f - Math.Max(0, -s) * 0.5f, y1 = r.Bottom;
        float topShift = s / 2, botShift = -s / 2;
        Vector2 P(float x, float y) => V(x + (y - y0) / Math.Max(1, y1 - y0) * (botShift - topShift) + topShift, y);

        var geo = fac.CreatePathGeometry();
        using var sink = geo.Open();
        void Corner(float cut, Vector2 a, Vector2 corner, Vector2 b, bool first)
        {
            // a = point before the corner on the incoming edge (at distance), b = after; corner = the sharp corner
            if (cut > 0) { if (first) sink.BeginFigure(a, FigureBegin.Filled); else sink.AddLine(a); sink.AddLine(b); }
            else if (rad > 0)
            {
                if (first) sink.BeginFigure(a, FigureBegin.Filled); else sink.AddLine(a);
                sink.AddArc(new ArcSegment(b, new Vortice.Mathematics.Size(rad, rad), 0, SweepDirection.Clockwise, ArcSize.Small));
            }
            else { if (first) sink.BeginFigure(corner, FigureBegin.Filled); else sink.AddLine(corner); }
        }
        float dTL = tl > 0 ? tl : rad, dTR = tr > 0 ? tr : rad, dBR = br > 0 ? br : rad, dBL = bl > 0 ? bl : rad;
        Corner(tl, P(x0, y0 + dTL), P(x0, y0), P(x0 + dTL, y0), true);
        Corner(tr, P(x1 - dTR, y0), P(x1, y0), P(x1, y0 + dTR), false);
        Corner(br, P(x1, y1 - dBR), P(x1, y1), P(x1 - dBR, y1), false);
        Corner(bl, P(x0 + dBL, y1), P(x0, y1), P(x0, y1 - dBL), false);
        sink.EndFigure(FigureEnd.Closed);
        sink.Close();
        return geo;
    }

    static ID2D1PathGeometry Poly(ID2D1Factory fac, params Vector2[] pts)
    {
        var geo = fac.CreatePathGeometry();
        using var sink = geo.Open();
        sink.BeginFigure(pts[0], FigureBegin.Filled);
        for (int i = 1; i < pts.Length; i++) sink.AddLine(pts[i]);
        sink.EndFigure(FigureEnd.Closed);
        sink.Close();
        return geo;
    }

    static void PushClip(Canvas c, ID2D1Geometry geo, RectangleF bounds)
    {
        c.Dc.PushLayer(new LayerParameters1
        {
            ContentBounds = new Rect(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4), GeometricMask = geo,
            MaskAntialiasMode = AntialiasMode.PerPrimitive, MaskTransform = Matrix3x2.Identity, Opacity = 1,
        }, null);
    }

    /// Draws the frame in `outer` (which includes a tab or an above-title). `fill` = false skips the inside
    /// (windows: their own contents are there).
    public static void Draw(Canvas c, FrameOptions f, Theme t, RectangleF outer, string title, string titleRight, float baseSize,
                            bool hot = false, bool fill = true)
    {
        var dc = c.Dc;
        var fac = dc.Factory;
        title = Cased(f, title); titleRight = Cased(f, titleRight);
        bool hasTitle = title.Length > 0 && f.Header != "none";
        float above = Above(f, t, baseSize, hasTitle);
        var box = new RectangleF(outer.X, outer.Y + above, outer.Width, Math.Max(2, outer.Height - above));
        float radius = f.Radius >= 0 ? f.Radius : t.Options.Ui.CornerRadius;
        float tsize = TitleSize(f, t, baseSize);
        string tfont = TitleFont(f, t);
        var titleC = t[f.TitleColor];

        using var shape = Outline(fac, box, f, radius);

        // glow and shadow
        if (f.Glow != "none" && f.GlowOpacity > 0)
        {
            var g = t[f.Glow];
            for (int i = 1; i <= 5; i++)
                dc.DrawGeometry(shape, c.Brush(g, f.GlowOpacity * 0.22f * (1 - i / 6f)), f.GlowSize * i / 2.5f);
        }
        float shadow = f.Shadow >= 0 ? f.Shadow : t.Options.Ui.Shadow;
        if (fill && shadow > 0)
        {
            var t0 = dc.Transform;
            dc.Transform = Matrix3x2.CreateTranslation(0, 3) * t0;
            for (int i = 1; i <= 5; i++) dc.DrawGeometry(shape, c.Brush(new Rgba(0, 0, 0), shadow * 0.08f), i * 3);
            dc.Transform = t0;
        }

        // fill + pattern
        if (fill)
        {
            var fc = hot && f.HoverFill != "none" ? t[f.HoverFill] : t[f.Fill];
            if (f.Fill2 != "none" && !(hot && f.HoverFill != "none"))
            {
                var stops = new[] { new GradientStop { Position = 0, Color = C(t[f.Fill], f.FillOpacity) }, new GradientStop { Position = 1, Color = C(t[f.Fill2], f.FillOpacity) } };
                using var coll = dc.CreateGradientStopCollection(stops);
                float a = f.FillAngle * MathF.PI / 180f;
                var dir = V(MathF.Cos(a), MathF.Sin(a));
                var ctr = V(box.X + box.Width / 2, box.Y + box.Height / 2);
                float half = (MathF.Abs(dir.X) * box.Width + MathF.Abs(dir.Y) * box.Height) / 2;
                using var b = dc.CreateLinearGradientBrush(new LinearGradientBrushProperties(ctr - dir * half, ctr + dir * half), coll);
                dc.FillGeometry(shape, b);
            }
            else if (fc.A > 0 && f.FillOpacity > 0) dc.FillGeometry(shape, c.Brush(fc, hot && f.HoverFill != "none" ? Math.Max(0.6f, f.FillOpacity) : f.FillOpacity));

            if (f.Pattern != "none" && f.PatternOpacity > 0)
            {
                PushClip(c, shape, box);
                var br = c.Brush(t[f.PatternColor], f.PatternOpacity);
                float sp = Math.Max(2, f.PatternSpacing);
                switch (f.Pattern)
                {
                    case "scanlines": for (float y = box.Y; y < box.Bottom; y += sp) dc.FillRectangle(new Rect(box.X, y, box.Width, 1), br); break;
                    case "vlines": for (float x = box.X; x < box.Right; x += sp) dc.FillRectangle(new Rect(x, box.Y, 1, box.Height), br); break;
                    case "grid":
                        for (float y = box.Y; y < box.Bottom; y += sp) dc.FillRectangle(new Rect(box.X, y, box.Width, 1), br);
                        for (float x = box.X; x < box.Right; x += sp) dc.FillRectangle(new Rect(x, box.Y, 1, box.Height), br);
                        break;
                    case "dots":
                        for (float y = box.Y + sp / 2; y < box.Bottom; y += sp)
                            for (float x = box.X + sp / 2; x < box.Right; x += sp) dc.FillRectangle(new Rect(x, y, 1, 1), br);
                        break;
                    case "diagonal":
                    case "crosshatch":
                        for (float d = -box.Height; d < box.Width; d += sp)
                        {
                            dc.DrawLine(V(box.X + d, box.Bottom), V(box.X + d + box.Height, box.Y), br, 1);
                            if (f.Pattern == "crosshatch") dc.DrawLine(V(box.X + d, box.Y), V(box.X + d + box.Height, box.Bottom), br, 1);
                        }
                        break;
                }
                dc.PopLayer();
            }
        }

        // title strip (bar)
        float tw = hasTitle ? MeasureText(c, title, tfont, tsize, f.TitleBold, f.TitleSpacing) : 0;
        float p = PadX(f, t), bw = f.Border != "none" ? f.BorderWidth : 0;
        float slantPad = Math.Abs(f.Slant) * 0.5f;
        if (hasTitle && f.Header == "bar")
        {
            float sh = StripH(f, t, baseSize);
            PushClip(c, shape, box);
            dc.FillRectangle(new Rect(box.X, box.Y, box.Width, sh), c.Brush(t[f.HeaderFill], f.HeaderOpacity));
            dc.PopLayer();
            if (f.HeaderLine != "none") dc.FillRectangle(new Rect(box.X, box.Y + sh - 1, box.Width, 1), c.Brush(t[f.HeaderLine]));
            DrawText(c, title, box.X + p + bw + slantPad, box.Y, sh, titleC, tfont, tsize, f.TitleBold, f.TitleSpacing);
            if (titleRight.Length > 0)
            {
                float rw = MeasureText(c, titleRight, tfont, tsize * 0.9f, false, f.TitleSpacing);
                DrawText(c, titleRight, box.Right - p - bw - slantPad - rw, box.Y, sh, t[f.TitleRightColor], tfont, tsize * 0.9f, false, f.TitleSpacing);
            }
        }

        // border
        if (f.Border != "none" && f.BorderWidth > 0 && f.BorderOpacity > 0)
        {
            var bc = c.Brush(t[f.Border], f.BorderOpacity);
            ID2D1StrokeStyle? style = null;
            if (f.Dash > 0)
                style = fac.CreateStrokeStyle(new StrokeStyleProperties { DashStyle = DashStyle.Custom, LineJoin = LineJoin.Miter }, new[] { f.Dash / Math.Max(0.5f, f.BorderWidth), f.Dash / Math.Max(0.5f, f.BorderWidth) });
            if (f.BorderSides == "all")
            {
                using var inner = Outline(fac, box, f, radius, f.BorderWidth / 2);
                if (style != null) dc.DrawGeometry(inner, bc, f.BorderWidth, style); else dc.DrawGeometry(inner, bc, f.BorderWidth);
            }
            else
            {
                float h = f.BorderWidth / 2;
                var r = RectangleF.Inflate(box, -h, -h);
                bool T = f.BorderSides is "top" or "top_bottom" or "top_left", B = f.BorderSides is "bottom" or "top_bottom" or "bottom_right";
                bool L = f.BorderSides is "left" or "left_right" or "top_left", R = f.BorderSides is "right" or "left_right" or "bottom_right";
                void Ln(float ax, float ay, float bx, float by) { if (style != null) dc.DrawLine(V(ax, ay), V(bx, by), bc, f.BorderWidth, style); else dc.DrawLine(V(ax, ay), V(bx, by), bc, f.BorderWidth); }
                if (T) Ln(r.X + f.CutTopLeft, r.Y, r.Right - f.CutTopRight, r.Y);
                if (B) Ln(r.X + f.CutBottomLeft, r.Bottom, r.Right - f.CutBottomRight, r.Bottom);
                if (L) Ln(r.X, r.Y + f.CutTopLeft, r.X, r.Bottom - f.CutBottomLeft);
                if (R) Ln(r.Right, r.Y + f.CutTopRight, r.Right, r.Bottom - f.CutBottomRight);
            }
            style?.Dispose();
        }
        if (f.InnerBorder != "none" && f.InnerOpacity > 0)
        {
            using var inner = Outline(fac, box, f, radius, f.BorderWidth + f.InnerGap + 0.5f);
            dc.DrawGeometry(inner, c.Brush(t[f.InnerBorder], f.InnerOpacity), 1);
        }

        // tab
        if (hasTitle && f.Header == "tab")
        {
            float sh = StripH(f, t, baseSize);
            float w = f.TabWidth > 0 ? f.TabWidth : tw + 2 * Math.Max(8, p * 0.8f);
            float x = box.X + f.TabOffset + f.CutTopLeft * 0.6f;
            float y0 = outer.Y, y1 = box.Y + Math.Max(1, f.BorderWidth);
            using var tab = Poly(fac, V(x, y1), V(x, y0), V(x + w, y0), V(x + w + f.TabSlant, y1));
            dc.FillGeometry(tab, c.Brush(t[f.HeaderFill], f.HeaderOpacity));
            if (f.HeaderLine != "none") dc.DrawGeometry(tab, c.Brush(t[f.HeaderLine]), 1);
            DrawText(c, title, x + (w - tw) / 2, y0, y1 - y0, titleC, tfont, tsize, f.TitleBold, f.TitleSpacing);
            if (titleRight.Length > 0)
            {
                float rw = MeasureText(c, titleRight, tfont, tsize * 0.9f, false, f.TitleSpacing);
                DrawText(c, titleRight, box.Right - rw - p, y0, y1 - y0, t[f.TitleRightColor], tfont, tsize * 0.9f, false, f.TitleSpacing);
            }
        }
        else if (hasTitle && f.Header is "plain" or "line")
        {
            float lh = tsize * 1.5f, x = box.X + p + bw + slantPad, y = box.Y + Pad(f, t) * 0.75f + bw;
            DrawText(c, title, x, y, lh, titleC, tfont, tsize, f.TitleBold, f.TitleSpacing);
            float rw = titleRight.Length > 0 ? MeasureText(c, titleRight, tfont, tsize * 0.9f, false, f.TitleSpacing) : 0;
            if (rw > 0) DrawText(c, titleRight, box.Right - p - bw - slantPad - rw, y, lh, t[f.TitleRightColor], tfont, tsize * 0.9f, false, f.TitleSpacing);
            if (f.Header == "line")
            {
                var lc = t[f.HeaderLine != "none" ? f.HeaderLine : f.Border != "none" ? f.Border : f.TitleColor];
                float lx0 = x + tw + tsize * 0.7f, lx1 = box.Right - p - bw - slantPad - (rw > 0 ? rw + tsize * 0.7f : 0);
                if (lx1 > lx0) dc.FillRectangle(new Rect(lx0, y + lh / 2, lx1 - lx0, 1), c.Brush(lc));
            }
        }
        else if (hasTitle && f.Header == "above")
        {
            float lh = above;
            DrawText(c, title, box.X, outer.Y, lh * 0.8f, titleC, tfont, tsize, f.TitleBold, f.TitleSpacing);
            float rw = titleRight.Length > 0 ? MeasureText(c, titleRight, tfont, tsize * 0.9f, false, f.TitleSpacing) : 0;
            if (rw > 0) DrawText(c, titleRight, box.Right - rw, outer.Y, lh * 0.8f, t[f.TitleRightColor], tfont, tsize * 0.9f, false, f.TitleSpacing);
            var lc = t[f.HeaderLine != "none" ? f.HeaderLine : f.Border != "none" ? f.Border : f.TitleColor];
            float lx0 = box.X + tw + tsize * 0.7f, lx1 = Math.Max(box.Right, lx0 + tsize * 4) - (rw > 0 ? rw + tsize * 0.7f : 0);
            if (lx1 > lx0) dc.FillRectangle(new Rect(lx0, outer.Y + lh * 0.4f, lx1 - lx0, 1), c.Brush(lc));
        }

        // stripe
        if (f.Stripe != "none")
        {
            var sc = c.Brush(t[f.Stripe]);
            float g = f.StripeGap, w = f.StripeWidth;
            var r = f.StripeSide switch
            {
                "right" => new Rect(box.Right + g, box.Y + f.CutTopRight, w, box.Height - f.CutTopRight - f.CutBottomRight),
                "top" => new Rect(box.X + f.CutTopLeft, box.Y - g - w, box.Width - f.CutTopLeft - f.CutTopRight, w),
                "bottom" => new Rect(box.X + f.CutBottomLeft, box.Bottom + g, box.Width - f.CutBottomLeft - f.CutBottomRight, w),
                _ => new Rect(box.X - g - w, box.Y + f.CutTopLeft, w, box.Height - f.CutTopLeft - f.CutBottomLeft),
            };
            if (g == 0)   // on the edge: inside the box
                r = f.StripeSide switch { "right" => r with { X = box.Right - w }, "top" => r with { Y = box.Y }, "bottom" => r with { Y = box.Bottom - w }, _ => r with { X = box.X } };
            dc.FillRectangle(r, sc);
        }

        // brackets
        if (f.Brackets != "none")
        {
            var bc = c.Brush(t[f.Brackets]);
            float o = f.BracketOffset, L = f.BracketLength, w = f.BracketWidth, h = w / 2;
            float x0 = box.X - o + h, y0 = box.Y - o + h, x1 = box.Right + o - h, y1 = box.Bottom + o - h;
            bool all = f.BracketCorners == "all";
            bool tl = all || f.BracketCorners is "top" or "left" or "top_left_bottom_right", tr = all || f.BracketCorners is "top" or "right" or "top_right_bottom_left";
            bool bl = all || f.BracketCorners is "bottom" or "left" or "top_right_bottom_left", brc = all || f.BracketCorners is "bottom" or "right" or "top_left_bottom_right";
            void Br(float cx, float cy, float dx, float dy)
            {
                dc.DrawLine(V(cx - dx * h, cy), V(cx + dx * L, cy), bc, w);
                dc.DrawLine(V(cx, cy - dy * h), V(cx, cy + dy * L), bc, w);
            }
            if (tl) Br(x0, y0, 1, 1);
            if (tr) Br(x1, y0, -1, 1);
            if (bl) Br(x0, y1, 1, -1);
            if (brc) Br(x1, y1, -1, -1);
        }

        // corner mark
        if (f.CornerMark != "none")
        {
            float m = f.MarkSize;
            var pts = f.MarkCorner switch
            {
                "top_left" => new[] { V(box.X, box.Y), V(box.X + m, box.Y), V(box.X, box.Y + m) },
                "top_right" => new[] { V(box.Right, box.Y), V(box.Right, box.Y + m), V(box.Right - m, box.Y) },
                "bottom_left" => new[] { V(box.X, box.Bottom), V(box.X, box.Bottom - m), V(box.X + m, box.Bottom) },
                _ => new[] { V(box.Right, box.Bottom), V(box.Right - m, box.Bottom), V(box.Right, box.Bottom - m) },
            };
            using var tri = Poly(fac, pts);
            dc.FillGeometry(tri, c.Brush(t[f.CornerMark]));
        }

        // hatch
        if (f.Hatch != "none")
        {
            float w = f.HatchWidth, h = f.HatchHeight;
            bool right = f.HatchCorner.EndsWith("right"), bottom = f.HatchCorner.StartsWith("bottom");
            float hx = right ? box.Right - w : box.X;
            float hy = bottom ? (f.HatchOutside ? box.Bottom + 3 : box.Bottom - h - 3) : (f.HatchOutside ? box.Y - h - 3 : box.Y + 3);
            if (!f.HatchOutside) hx += right ? -3 : 3;
            var area = new RectangleF(hx, hy, w, h);
            dc.PushAxisAlignedClip(new Rect(area.X, area.Y, area.Width, area.Height), AntialiasMode.Aliased);
            var hb = c.Brush(t[f.Hatch]);
            for (float d = -h; d < w; d += 4) dc.DrawLine(V(hx + d, hy + h), V(hx + d + h, hy), hb, 1.5f);
            dc.PopAxisAlignedClip();
        }

        // ticks
        if (f.Ticks != "none")
        {
            var tb = c.Brush(t[f.Ticks]);
            float sp = f.TickSpacing, len = f.TickLength;
            bool horiz = f.TicksSide is "top" or "bottom";
            float from = horiz ? box.X : box.Y, to = horiz ? box.Right : box.Bottom;
            int i = 0;
            for (float q = from; q <= to + 0.1f; q += sp, i++)
            {
                float l = i % 5 == 0 ? len * 2 : len;
                switch (f.TicksSide)
                {
                    case "top": dc.FillRectangle(new Rect(q, box.Y - l, 1, l), tb); break;
                    case "bottom": dc.FillRectangle(new Rect(q, box.Bottom, 1, l), tb); break;
                    case "left": dc.FillRectangle(new Rect(box.X - l, q, l, 1), tb); break;
                    default: dc.FillRectangle(new Rect(box.Right, q, l, 1), tb); break;
                }
            }
        }
    }

    static Color4 C(Rgba c, float opacity) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f * opacity);

    /// How far a frame reaches outside the box it surrounds (for windows: brackets, glow, ticks, stripes and hatch
    /// sit outside). Doesn't include a tab or above-title.
    public static Insets Outside(FrameOptions f)
    {
        float l = 0, tp = 0, r = 0, b = 0;
        void All(float v) { l = Math.Max(l, v); tp = Math.Max(tp, v); r = Math.Max(r, v); b = Math.Max(b, v); }
        if (f.Glow != "none") All(f.GlowSize * 2);
        if (f.Brackets != "none") All(Math.Max(0, f.BracketOffset) + f.BracketWidth);
        if (f.Ticks != "none")
        {
            float tl = f.TickLength * 2;
            switch (f.TicksSide) { case "top": tp = Math.Max(tp, tl); break; case "bottom": b = Math.Max(b, tl); break; case "left": l = Math.Max(l, tl); break; default: r = Math.Max(r, tl); break; }
        }
        if (f.Stripe != "none" && f.StripeGap != 0)
        {
            float s = f.StripeGap + f.StripeWidth;
            switch (f.StripeSide) { case "top": tp = Math.Max(tp, s); break; case "bottom": b = Math.Max(b, s); break; case "right": r = Math.Max(r, s); break; default: l = Math.Max(l, s); break; }
        }
        if (f.Hatch != "none" && f.HatchOutside) { if (f.HatchCorner.StartsWith("bottom")) b = Math.Max(b, f.HatchHeight + 3); else tp = Math.Max(tp, f.HatchHeight + 3); }
        return new Insets(l, tp, r, b);
    }
}
