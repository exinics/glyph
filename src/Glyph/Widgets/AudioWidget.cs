using System.ComponentModel;
using Glyph.Data;
using Glyph.Options;
using Glyph.Ui;

namespace Glyph.Widgets;

internal sealed class AudioOptions : PanelOptions
{
    public override string Type => "audio";
    public AudioOptions() { Title = "sound"; Anchor = "bottom_right"; OffsetX = 40; OffsetY = 220; Width = 340; }

    [Category("Sound"), DisplayName("Output device"), Description("current = the device in use (click it to switch to the next one, scroll to go back and forth). list = every output, click one to use it. none = hide."),
     Choices("current", "list", "none"), TypeConverter(typeof(ChoicesConverter))]
    public string Devices { get; set; } = "current";
    [Category("Sound"), DisplayName("Master volume"), Description("A slider for the whole volume: click to set, scroll to change, click its name to mute.")] public bool Master { get; set; } = true;
    [Category("Sound"), DisplayName("App volumes"), Description("A slider for every app making sound (like the Windows volume mixer).")] public bool Apps { get; set; } = true;
    [Category("Sound"), DisplayName("Only playing apps"), Description("Hide apps that are open but silent right now.")] public bool OnlyPlaying { get; set; }
    [Category("Sound"), DisplayName("Max apps"), Range(0, 30)] public int MaxApps { get; set; } = 6;
    [Category("Sound"), DisplayName("Hide system sounds")] public bool HideSystemSounds { get; set; } = true;
    [Category("Sound"), DisplayName("Scroll step"), Description("How much one notch of the mouse wheel changes a volume, in %."), Range(1, 50)] public int Step { get; set; } = 5;
    [Category("Sound"), DisplayName("Meter characters"), Description("Terminal style: the filled and empty characters of a slider, e.g. █░, ▮▯, =-, ■□.")] public string MeterChars { get; set; } = "█░";
    [Category("Sound"), DisplayName("Muted colour"), Description("Slider colour while muted."), Color, TypeConverter(typeof(ColorNameConverter))] public string MutedColor { get; set; } = "dim";
}

/// Sound controls: the output device, master volume and each app's volume. Click a slider to set it,
/// scroll over it to nudge it, click a name to mute.
internal sealed class AudioWidget : PanelWidget
{
    readonly AudioOptions o;
    readonly Rgba muted;
    public AudioWidget(AudioOptions o, Theme t) : base(o, t) { this.o = o; muted = t[o.MutedColor]; }

    static string Fit(string s, int n) => s.Length <= n ? s : s[..Math.Max(1, n - 1)] + "…";

    protected override ColEl? Build(WidgetEnv e)
    {
        var m = Mixer.Now;
        bool term = e.Painter.Terminal;
        var col = new ColEl();
        if (m.Volume < 0 && m.Devices.Count == 0) { col.Items.Add(new TextEl("no sound device", Dim)); return col; }
        string up = "+" + o.Step, down = "-" + o.Step;

        if (o.Devices == "current" && m.DefaultName.Length > 0)
            col.Items.Add(new TextEl
            {
                Spans = { new Span(term ? "out  " : "Output  ", Dim), new Span(Fit(m.DefaultName, 40), Text), new Span(m.Devices.Count > 1 ? (term ? "  ›" : "  ›") : "", Dim) },
                Action = "audio:device:next", WheelUp = "audio:device:prev", WheelDown = "audio:device:next",
            });
        else if (o.Devices == "list")
            foreach (var d in m.Devices)
                col.Items.Add(new TextEl
                {
                    Spans = { new Span(d.IsDefault ? "● " : "○ ", d.IsDefault ? Accent : Dim), new Span(Fit(d.Name, 40), d.IsDefault ? Text : Dim) },
                    Action = "audio:device:" + d.Id,
                });

        var apps = !o.Apps ? new List<Mixer.App>() : m.Apps
            .Where(a => (!o.OnlyPlaying || a.Playing) && !(o.HideSystemSounds && a.Name == "System sounds"))
            .Take(o.MaxApps).ToList();
        // terminal: line the sliders up by padding every name to the longest
        int nameW = term ? Math.Min(14, Math.Max(6, apps.Select(a => a.Name.Length).DefaultIfEmpty(0).Max())) : 0;
        string Label(string s) => term ? Fit(s, nameW).PadRight(nameW) : s;

        MeterEl Slider(string label, float v, bool isMuted, string set, string mute, string wheelUp, string wheelDown) => new()
        {
            Label = label, Value = v, ValueText = isMuted ? "muted" : $"{v * 100:0}%",
            Fill = isMuted ? muted : Accent, Track = Line, LabelColor = isMuted ? Dim : Text, ValueColor = isMuted ? Dim : Text, Chars = o.MeterChars,
            SetAction = set, LabelAction = mute, WheelUp = wheelUp, WheelDown = wheelDown,
        };

        if (o.Master && m.Volume >= 0)
        {
            if (col.Items.Count > 0) col.Items.Add(new GapEl(term ? 0.6f : 0.45f));
            col.Items.Add(Slider(Label(term ? "master" : "Master"), m.Volume, m.Muted, "volume:{v}", "volume:mute", "volume:" + up, "volume:" + down));
        }
        if (apps.Count > 0)
        {
            col.Items.Add(new GapEl(term ? 0.6f : 0.45f));
            for (int i = 0; i < apps.Count; i++)
            {
                var a = apps[i];
                if (i > 0 && !term) col.Items.Add(new GapEl(0.3f));
                string key = "audio:app:" + a.Name + ":";
                var s = Slider(Label(a.Name), a.Volume, a.Muted, key + "{v}", key + "mute", key + up, key + down);
                if (!a.Playing && !a.Muted) s.LabelColor = Dim;
                col.Items.Add(s);
            }
        }
        else if (o.Apps && o.OnlyPlaying)
        {
            col.Items.Add(new GapEl(term ? 0.6f : 0.45f));
            col.Items.Add(new TextEl("nothing playing", Dim, TextSize.Small));
        }
        return col;
    }
}
