namespace Glyph;

/// The performance preset in use right now (glyph.toml [performance]: picked by power state every half
/// second). Read by the render thread, the wallpaper layers and the widgets.
internal static class Perf
{
    public static volatile PresetOptions Current = PresetOptions.MakeHighest();
    public static volatile string Name = "highest";

    public static readonly EffectSettings NoEffects = new() { Enabled = false };

    /// How often (seconds) smoke/noise is worked out, given what the theme asks for and the frame's length.
    public static float NoiseInterval(float theme, float dt) => Current.AnimationDetail switch
    {
        "max" => Math.Max(0.001f, Math.Min(theme, dt)),
        "low" => theme * 2,
        _ => theme,
    };

    /// Frames a second for a video, given the video's own rate and the widget's limit.
    public static double VideoFps(double source, int widgetMax) => Math.Min(source, Math.Max(1, Current.AnimationDetail switch
    {
        "max" => source,
        "low" => Math.Min(widgetMax, 15),
        _ => widgetMax,
    }));
}
