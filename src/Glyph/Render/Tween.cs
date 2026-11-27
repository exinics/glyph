using System.ComponentModel;
using System.Numerics;
using System.Text;
using Glyph.Options;
using Glyph.Widgets;

namespace Glyph.Render;

/// Easing curves, the same set (and names) as Roblox's TweenService: Linear, Sine, Quad, Cubic, Quart, Quint,
/// Exponential, Circular, Back, Elastic and Bounce, each In, Out or InOut.
internal static class Easing
{
    public static readonly string[] Styles = { "linear", "sine", "quad", "cubic", "quart", "quint", "exponential", "circular", "back", "elastic", "bounce" };
    public static readonly string[] Directions = { "in", "out", "inout" };

    static float In(string style, float t) => style switch
    {
        "sine" => 1 - MathF.Cos(t * MathF.PI / 2),
        "quad" => t * t,
        "cubic" => t * t * t,
        "quart" => t * t * t * t,
        "quint" => t * t * t * t * t,
        "exponential" => t <= 0 ? 0 : MathF.Pow(2, 10 * t - 10),
        "circular" => 1 - MathF.Sqrt(Math.Max(0, 1 - t * t)),
        "back" => 2.70158f * t * t * t - 1.70158f * t * t,
        "elastic" => t <= 0 ? 0 : t >= 1 ? 1 : -MathF.Pow(2, 10 * t - 10) * MathF.Sin((t * 10 - 10.75f) * (2 * MathF.PI / 3)),
        "bounce" => 1 - BounceOut(1 - t),
        _ => t,
    };

    static float BounceOut(float t)
    {
        const float n = 7.5625f, d = 2.75f;
        if (t < 1 / d) return n * t * t;
        if (t < 2 / d) { t -= 1.5f / d; return n * t * t + 0.75f; }
        if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + 0.9375f; }
        t -= 2.625f / d; return n * t * t + 0.984375f;
    }

    /// The eased value of `t` (0..1). Back and Elastic go a little past 0 and 1 on purpose.
    public static float Ease(string style, string direction, float t)
    {
        t = Math.Clamp(t, 0, 1);
        style = style.ToLowerInvariant(); direction = direction.ToLowerInvariant();
        return direction switch
        {
            "in" => In(style, t),
            "inout" => t < 0.5f ? In(style, t * 2) / 2 : 1 - In(style, 2 - t * 2) / 2,
            _ => 1 - In(style, 1 - t),
        };
    }
}

/// animations.toml in a theme folder: tweens that move, scale, turn or fade widgets. Like a TweenService tween:
/// a target, how long, an easing style and direction, a delay, repeats, and the goal it tweens to.
internal sealed class TweenOptions
{
    [Category("1. Tween"), Description("A name for this animation (only for you).")] public string Name { get; set; } = "tween";
    [Category("1. Tween"), Description("Which widget(s) it plays on: a widget's Name, type:clock for every widget of a type, or * for every widget.")] public string Target { get; set; } = "";
    [Category("1. Tween"), Description("When it plays. appear = when the theme loads, coming in FROM the goal (e.g. Move Y 40 + Opacity 0 = rises and fades in). hover = goes to the goal while the mouse is over it, back when it leaves. click = to the goal and back once when clicked. loop = keeps playing (see Repeat and Reverses)."),
     Choices("appear", "hover", "click", "loop"), TypeConverter(typeof(ChoicesConverter))]
    public string Trigger { get; set; } = "appear";
    [Category("1. Tween"), DisplayName("Time"), Description("How long one play takes, in seconds."), Range(0.01, 60)] public float Time { get; set; } = 0.4f;
    [Category("1. Tween"), DisplayName("Easing style"), Description("The shape of the motion: linear = steady, sine/quad/cubic/quart/quint = gentle to strong slow-downs, exponential and circular = sharp, back = overshoots a little, elastic = springs, bounce = bounces."),
     Choices("linear", "sine", "quad", "cubic", "quart", "quint", "exponential", "circular", "back", "elastic", "bounce"), TypeConverter(typeof(ChoicesConverter))]
    public string Style { get; set; } = "quad";
    [Category("1. Tween"), DisplayName("Easing direction"), Description("out = fast start, gentle end (most natural for things arriving). in = gentle start, fast end. inout = gentle at both ends."),
     Choices("in", "out", "inout"), TypeConverter(typeof(ChoicesConverter))]
    public string Direction { get; set; } = "out";
    [Category("1. Tween"), Description("Seconds to wait before it starts."), Range(0, 60)] public float Delay { get; set; }
    [Category("1. Tween"), Description("With several targets (type:… or *): each one starts this many seconds after the one before, so they come in one after another."), Range(0, 10)] public float Stagger { get; set; }
    [Category("1. Tween"), Description("loop: how many more times it plays after the first. -1 = for ever."), Range(-1, 1000)] public int Repeat { get; set; } = -1;
    [Category("1. Tween"), Description("loop: play back to the start after reaching the goal (each play goes there and back).")] public bool Reverses { get; set; } = true;

    [Category("2. Goal"), DisplayName("Move X"), Description("Pixels to the right at the goal (negative = left)."), Range(-4000, 4000)] public float MoveX { get; set; }
    [Category("2. Goal"), DisplayName("Move Y"), Description("Pixels down at the goal (negative = up)."), Range(-4000, 4000)] public float MoveY { get; set; }
    [Category("2. Goal"), Description("Size at the goal: 1 = as it is, 1.05 = 5% bigger, 0.8 = smaller. From the widget's centre."), Range(0, 10)] public float Scale { get; set; } = 1;
    [Category("2. Goal"), Description("See-through-ness at the goal: 1 = as it is, 0 = invisible."), Range(0, 1)] public float Opacity { get; set; } = 1;
    [Category("2. Goal"), DisplayName("Rotate"), Description("Degrees turned at the goal (clockwise)."), Range(-3600, 3600)] public float Rotate { get; set; }

    public override string ToString() => $"{Name}  ({Trigger}{(Target.Length > 0 ? " · " + Target : "")})";

    /// Ready-made tweens for the settings window's Add menu.
    public static readonly (string name, Func<TweenOptions> make)[] Presets =
    {
        ("Fade in (appear)", () => new TweenOptions { Name = "fade in", Trigger = "appear", Time = 0.6f, Style = "sine", Opacity = 0 }),
        ("Rise in (appear)", () => new TweenOptions { Name = "rise in", Trigger = "appear", Time = 0.55f, Style = "quart", MoveY = 40, Opacity = 0 }),
        ("Slide in from the left (appear)", () => new TweenOptions { Name = "slide in", Trigger = "appear", Time = 0.6f, Style = "quint", MoveX = -80, Opacity = 0 }),
        ("Pop in (appear)", () => new TweenOptions { Name = "pop in", Trigger = "appear", Time = 0.5f, Style = "back", Scale = 0.85f, Opacity = 0 }),
        ("Lift on hover", () => new TweenOptions { Name = "lift", Trigger = "hover", Time = 0.18f, Style = "quad", MoveY = -4 }),
        ("Grow on hover", () => new TweenOptions { Name = "grow", Trigger = "hover", Time = 0.2f, Style = "back", Scale = 1.04f }),
        ("Bounce on click", () => new TweenOptions { Name = "press", Trigger = "click", Time = 0.12f, Style = "quad", Scale = 0.95f }),
        ("Bob up and down (loop)", () => new TweenOptions { Name = "bob", Trigger = "loop", Time = 1.6f, Style = "sine", Direction = "inout", MoveY = -6 }),
        ("Breathe (loop)", () => new TweenOptions { Name = "breathe", Trigger = "loop", Time = 2.2f, Style = "sine", Direction = "inout", Opacity = 0.65f }),
        ("Spin (loop)", () => new TweenOptions { Name = "spin", Trigger = "loop", Time = 6f, Style = "linear", Rotate = 360, Reverses = false }),
    };
}

internal sealed class AnimationsFile
{
    public List<TweenOptions> Tweens { get; set; } = new();

    public static string FilePath(string theme) => Path.Combine(Paths.ThemeDir(theme), "animations.toml");

    public static AnimationsFile Load(Theme theme, List<string> problems)
    {
        var f = new AnimationsFile();
        var path = FilePath(theme.Name);
        if (!File.Exists(path)) return f;
        var file = ConfigFile.Load(path, problems);
        foreach (var n in file.Top.Array("tween"))
        {
            var o = new TweenOptions();
            Binder.Read(o, n, theme);
            f.Tweens.Add(o);
        }
        file.ReportUnknown();
        return f;
    }

    public void Save(string theme)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Animations (tweens) for this theme's widgets. Easiest to change in the settings window");
        sb.AppendLine("# (Design page: pick a widget > Animations). Each [[tween]] plays on its Target widget(s).");
        foreach (var o in Tweens) Binder.WriteTable(sb, "tween", o);
        SafeFile.Write(FilePath(theme), sb.ToString());
    }
}

/// Plays the theme's tweens on the desktop's widgets (smooth style): works out each widget's move, size, turn and
/// see-through-ness for the frame, and says when something is still moving (so frames keep coming).
internal sealed class Animator
{
    sealed class Live
    {
        public TweenOptions T = null!;
        public Widget W = null!;
        public int Index;                // place among the tween's targets (for Stagger)
        public float Hover;              // hover: 0..1 linear progress towards the goal
        public float ClickAt = -1;       // click: when it was clicked
    }

    readonly List<Live> live = new();
    float startAt = -1, now;
    public bool Running { get; private set; }
    public bool HasHover => live.Any(l => l.T.Trigger == "hover");

    string bound = "";

    /// Attaches the tweens to the widgets. Appear tweens play again only when the tweens themselves changed (or
    /// Glyph just started): saving any other setting reloads the theme, and that shouldn't replay every intro.
    public void Bind(List<TweenOptions> tweens, IReadOnlyList<Widget> widgets)
    {
        var sb = new StringBuilder();
        foreach (var t in tweens) Binder.WriteValues(sb, t);
        var sig = sb.ToString();
        bool same = sig == bound && bound.Length > 0;
        bound = sig;
        float keepStart = startAt;
        live.Clear();
        foreach (var t in tweens)
        {
            var target = t.Target.Trim();
            if (target.Length == 0) continue;
            int i = 0;
            foreach (var w in widgets)
            {
                var o = w.Opt;
                if (o == null) continue;
                bool hit = target == "*"
                    || (target.StartsWith("type:", StringComparison.OrdinalIgnoreCase) && o.Type.Equals(target[5..].Trim(), StringComparison.OrdinalIgnoreCase))
                    || (o.Name.Length > 0 && o.Name.Equals(target, StringComparison.OrdinalIgnoreCase));
                if (hit) live.Add(new Live { T = t, W = w, Index = i++ });
            }
        }
        Restart();
        if (same && keepStart >= 0) startAt = keepStart;   // carry on where it was (intros stay finished)
    }

    /// Plays the appear and loop tweens again from the start (theme loaded, or the editor's Play button).
    public void Restart() { startAt = -1; foreach (var l in live) { l.ClickAt = -1; } Running = live.Count > 0; }

    public void Clicked(Widget w) { foreach (var l in live) if (l.W == w && l.T.Trigger == "click") { l.ClickAt = now; Running = true; } }

    /// Moves time on and updates hover progress. `over(w)` = the mouse is over that widget.
    public void Step(float time, float dt, Func<Widget, bool> over)
    {
        now = time;
        if (startAt < 0) startAt = time;
        bool running = false;
        foreach (var l in live)
        {
            var t = l.T;
            float local = time - startAt - t.Delay - t.Stagger * l.Index;
            switch (t.Trigger)
            {
                case "hover":
                    {
                        float target = over(l.W) ? 1 : 0;
                        if (l.Hover != target)
                        {
                            float step = dt / Math.Max(0.01f, t.Time);
                            l.Hover = target > l.Hover ? Math.Min(target, l.Hover + step) : Math.Max(target, l.Hover - step);
                            running = true;
                        }
                        break;
                    }
                case "click": if (l.ClickAt >= 0 && time - l.ClickAt - t.Delay < 2 * t.Time) running = true; break;
                case "loop":
                    if (t.Repeat < 0 || local < (t.Repeat + 1) * Period(t)) running = true;
                    break;
                default: if (local < t.Time) running = true; break;   // appear
            }
        }
        Running = running;
    }

    static float Period(TweenOptions t) => Math.Max(0.01f, t.Time) * (t.Reverses ? 2 : 1);

    /// 0 = at rest, 1 = at the goal, for one tween now.
    float Progress(Live l)
    {
        var t = l.T;
        float time = Math.Max(0.01f, t.Time);
        float E(float x) => Easing.Ease(t.Style, t.Direction, x);
        float local = now - startAt - t.Delay - t.Stagger * l.Index;
        switch (t.Trigger)
        {
            case "hover": return E(l.Hover);
            case "click":
                {
                    if (l.ClickAt < 0) return 0;
                    float c = (now - l.ClickAt - t.Delay) / time;
                    return c < 0 ? 0 : c < 1 ? E(c) : c < 2 ? E(2 - c) : 0;
                }
            case "loop":
                {
                    if (local < 0) return 0;
                    float period = Period(t);
                    if (t.Repeat >= 0 && local >= (t.Repeat + 1) * period) return t.Reverses ? 0 : 1;
                    float f = (local % period) / time;
                    return f <= 1 ? E(f) : E(2 - f);
                }
            default:   // appear: starts at the goal, ends at rest
                if (startAt < 0 || local < 0) return 1;
                return local >= time ? 0 : 1 - E(local / time);
        }
    }

    /// The widget's look now: move, size, turn (degrees) and see-through-ness. Identity = (0, 0, 1, 0, 1).
    public (float x, float y, float scale, float rotate, float opacity) For(Widget w)
    {
        float x = 0, y = 0, s = 1, r = 0, o = 1;
        foreach (var l in live)
        {
            if (l.W != w) continue;
            float p = Progress(l);
            if (p == 0) continue;
            var t = l.T;
            x += t.MoveX * p; y += t.MoveY * p; r += t.Rotate * p;
            s *= 1 + (t.Scale - 1) * p;
            o *= Math.Clamp(1 + (t.Opacity - 1) * p, 0, 1);
        }
        return (x, y, s, r, o);
    }

    /// Draws `draw` with the widget's animated look applied (transform and opacity layer).
    public void Apply(Canvas c, Widget w, Action draw)
    {
        var (x, y, s, r, o) = For(w);
        if (o <= 0.001f) return;
        bool moved = x != 0 || y != 0 || s != 1 || r != 0;
        var dc = c.Dc;
        var t0 = dc.Transform;
        if (moved)
        {
            var b = w.Bounds;
            var centre = new Vector2(b.X + b.Width / 2, b.Y + b.Height / 2);
            dc.Transform = Matrix3x2.CreateTranslation(-centre) * Matrix3x2.CreateScale(Math.Max(0.001f, s)) * Matrix3x2.CreateRotation(r * MathF.PI / 180)
                           * Matrix3x2.CreateTranslation(centre + new Vector2(x, y)) * t0;
        }
        bool fade = o < 0.999f;
        if (fade)
            dc.PushLayer(new Vortice.Direct2D1.LayerParameters1
            {
                ContentBounds = new Vortice.Mathematics.Rect(-100000, -100000, 200000, 200000), Opacity = o, MaskTransform = Matrix3x2.Identity,
                MaskAntialiasMode = Vortice.Direct2D1.AntialiasMode.PerPrimitive,
            }, null);
        try { draw(); }
        finally
        {
            if (fade) dc.PopLayer();
            dc.Transform = t0;
        }
    }
}
