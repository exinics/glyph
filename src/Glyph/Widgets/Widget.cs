using Glyph.Render;

namespace Glyph.Widgets;

/// What widgets get each frame.
internal sealed class WidgetEnv
{
    public int W, H;               // screen size in pixels
    public RectangleF Area;        // the usable part (without the bar and taskbar), in the same pixels
    public int CellW = 9, CellH = 17;
    public Theme Theme = null!;
    public float Time;
    public bool Smooth => Theme.Smooth;
    public Action<Shortcut>? Launch;
    /// Runs a click action (see Data.Actions) on the UI thread.
    public Action<string>? Do;
    public Glyph.Ui.Painter Painter = new();
    public Canvas? Canvas;
    public float BaseFontSize = 13;
    /// Where the mouse is (same pixels as Bounds) and whether it's over the desktop at all.
    public float MouseX = -1e6f, MouseY = -1e6f;
    public bool MouseOn;
}

/// One clickable part of a widget for the Design page: where it is, which option categories belong to it
/// (null = all of them; "*own" = the widget type's own categories) and which option names its frame
/// ("" = none; "theme:ButtonFrame" = the theme's button frame).
internal sealed record Part(string Name, RectangleF Rect, string[]? Categories, string FrameProp = "");

/// A panel on the desktop. Terminal themes draw it with characters (DrawCells),
/// smooth themes with shapes and text (DrawVector).
internal abstract class Widget : IDisposable
{
    public RectangleF Bounds;      // pixels, worked out by Layout
    public int Hover = -1;          // item under the mouse
    public bool MouseOver;          // the mouse is over the widget (anywhere on it)
    public Glyph.Render.AuraLook Aura;   // halo / glow for this frame (panels put it around their contents only)
    public string? LastSig;              // what it showed at the last check (Composer.Signature)
    public string? AuraKey;              // set when its glow picture may be reused this frame (null = draw afresh)
    /// Drawn after the others this frame (e.g. a panel growing to show more under the mouse).
    public virtual bool OnTop => false;
    /// True if it changes as the mouse moves over it (tilt that follows the mouse, spotlights, magnifying icons):
    /// then every mouse move over it redraws the desktop.
    public virtual bool FollowsMouse => Opt != null && Opt.TiltFollow > 0;
    public int FileIndex = -1;      // position in widgets.toml (the editor uses it)
    public WidgetOptions Opt = null!;
    public virtual bool Animated => false;

    public abstract void Layout(WidgetEnv e);
    public virtual void DrawCells(Grid g, WidgetEnv e) { }
    public virtual void DrawVector(Canvas c, WidgetEnv e) { }
    /// Item at a point (pixels), or -1.
    public virtual int HitTest(float x, float y) => -1;
    /// A finished click on an item. `count` = 2 for a double click.
    public virtual void Click(int item, int count, WidgetEnv e) { }
    /// The mouse wheel over it. Returns true if it used it.
    public virtual bool Wheel(float x, float y, int delta, WidgetEnv e) => false;
    /// The parts the Design page can pick (smallest under the mouse wins).
    public virtual IEnumerable<Part> Parts() => new[] { new Part("Box", Bounds, new[] { "Placement", "Box", "Link", "*own" }, "Frame") };

    /// True if the point is on this panel (empty desktop otherwise: clicks and drags there go to the background).
    public virtual bool Covers(float x, float y) => Bounds.Contains(x, y);
    public virtual void Dispose() { }
    /// Describes what it would show right now; when this changes, the desktop redraws.
    public virtual string Signature(WidgetEnv e) => Hover.ToString();

    /// Top-left of a w*h box placed against the screen by anchor + offset (pixels).
    protected static PointF Place(string anchor, float w, float h, RectangleF a, float ox, float oy)
    {
        float x = anchor.EndsWith("left") || anchor == "left" ? a.X + ox : anchor.EndsWith("right") || anchor == "right" ? a.Right - w - ox : a.X + (a.Width - w) / 2 + ox;
        float y = anchor.StartsWith("top") ? a.Y + oy : anchor.StartsWith("bottom") ? a.Bottom - h - oy : a.Y + (a.Height - h) / 2 + oy;
        return new PointF(x, y);
    }

    /// Width / height the options ask for: Stretch fills the screen between the offsets, else Width/Height (0 = fit).
    protected static float WantW(WidgetOptions o, float own, WidgetEnv e) => o.StretchW ? Math.Max(1, e.Area.Width - 2 * o.OffsetX) : own;
    protected static float WantH(WidgetOptions o, float own, WidgetEnv e) => o.StretchH ? Math.Max(1, e.Area.Height - 2 * o.OffsetY) : own;

    /// Place, keeping a stretched side against both edges.
    protected static PointF PlaceFor(WidgetOptions o, float w, float h, RectangleF a)
    {
        var pt = Place(o.Anchor, w, h, a, o.OffsetX, o.OffsetY);
        if (o.StretchW) pt.X = a.X + o.OffsetX;
        if (o.StretchH) pt.Y = a.Y + o.OffsetY;
        return pt;
    }

    public static Widget Create(WidgetOptions o, Theme theme, List<Shortcut> shortcuts, List<string> problems)
    {
        var w = Make(o, theme, shortcuts, problems);
        w.Opt = o;
        return w;
    }

    static Widget Make(WidgetOptions o, Theme theme, List<Shortcut> shortcuts, List<string> problems) => o switch
    {
        ShortcutsOptions s => new ShortcutsWidget(s, theme, shortcuts),
        ClockOptions c => new ClockWidget(c, theme),
        SystemOptions sy => new SystemWidget(sy, theme),
        MediaOptions m => new MediaWidget(m, theme),
        CalendarOptions ca => new CalendarWidget(ca, theme),
        WeatherOptions w => new WeatherWidget(w, theme),
        TextOptions t => new TextWidget(t, theme),
        CommandOptions cm => new CommandWidget(cm, theme),
        SysInfoOptions si => new SysInfoWidget(si, theme),
        ImageOptions im => new ImageWidget(im, theme),
        VideoOptions v => new VideoWidget(v, theme),
        AudioOptions au => new AudioWidget(au, theme),
        FeedOptions fd => new FeedWidget(fd, theme),
        MenuOptions mn => new MenuWidget(mn, theme, shortcuts),
        DividerOptions dv => new DividerWidget(dv, theme),
        ThemesOptions th => new ThemesWidget(th, theme),
        TogglesOptions tg => new TogglesWidget(tg, theme),
        ListOptions li => new ListWidget(li, theme),
        LyricsOptions ly => new LyricsWidget(ly, theme),
        VisualiserOptions vi => new VisualiserWidget(vi, theme),
        _ => throw new ArgumentException("unknown widget " + o.Type),
    };
}
