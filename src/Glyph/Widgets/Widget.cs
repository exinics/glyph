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
}

/// A panel on the desktop. Terminal themes draw it with characters (DrawCells),
/// smooth themes with shapes and text (DrawVector).
internal abstract class Widget : IDisposable
{
    public RectangleF Bounds;      // pixels, worked out by Layout
    public int Hover = -1;          // item under the mouse
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
        _ => throw new ArgumentException("unknown widget " + o.Type),
    };
}
