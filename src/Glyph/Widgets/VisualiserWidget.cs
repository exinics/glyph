using System.ComponentModel;
using Glyph.Data;
using Glyph.Options;
using Glyph.Ui;

namespace Glyph.Widgets;

internal sealed class VisualiserOptions : PanelOptions
{
    public override string Type => "visualiser";
    public VisualiserOptions() { Anchor = "bottom_right"; OffsetX = 40; OffsetY = 40; Width = 800; Panel = false; }
    [Category("Visualiser"), Description("How many bars."), Range(4, 256)] public int Bars { get; set; } = 64;
    [Category("Visualiser"), DisplayName("Bar height"), Description("Height of the tallest bars, in lines of text."), Range(0.5, 60)] public float Rows { get; set; } = 8;
    [Category("Visualiser"), DisplayName("Bar gap"), Description("Pixels between bars."), Range(0, 40)] public float BarGap { get; set; } = 3;
    [Category("Visualiser"), DisplayName("Bar colour"), Color, TypeConverter(typeof(ColorNameConverter))] public string BarColor { get; set; } = "accent_dim";
    [Category("Visualiser"), DisplayName("Peak colour"), Description("Colour of the tallest bars."), Color, TypeConverter(typeof(ColorNameConverter))] public string PeakColor { get; set; } = "accent";
    [Category("Visualiser"), Description("Bars grow up and down from the middle instead of up from the bottom.")] public bool Mirror { get; set; }
    [Category("Visualiser"), DisplayName("Frame rate"), Description("Frames per second while it moves (higher = smoother, more CPU)."), Range(5, 60)] public int Fps { get; set; } = 30;
    [Category("Visualiser"), DisplayName("Hide when nothing plays")] public bool HideWhenIdle { get; set; }
}

/// Bars that bounce with whatever your speakers play (a big audio visualiser on its own). It listens only while
/// a media app is playing.
internal sealed class VisualiserWidget : PanelWidget
{
    readonly VisualiserOptions o;
    public VisualiserWidget(VisualiserOptions o, Theme t) : base(o, t) { this.o = o; }

    protected override ColEl? Build(WidgetEnv e)
    {
        Media.Use();
        bool playing = Media.HasSession && Media.Playing;
        if (!playing && o.HideWhenIdle) return null;
        if (playing) Spectrum.Use();
        var col = new ColEl();
        col.Items.Add(new SpectrumEl { Bands = Spectrum.Bands(o.Bars), Color = T[o.BarColor], Peak = T[o.PeakColor], Rows = o.Rows, BarGap = o.BarGap, Mirror = o.Mirror });
        return col;
    }

    public override bool Animated => base.Animated || Spectrum.Moving;
    public int Fps => o.Fps;
}
