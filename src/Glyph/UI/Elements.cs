using System.Text;
using Glyph.Data;

namespace Glyph.Ui;

internal enum TextSize { Small, Normal, Large, Title }

/// A piece of a widget or the bar. Containers (RowEl, ColEl) lay out their children; leaves draw.
/// Sizes are worked out by the Painter in cells (terminal style) or pixels (smooth style).
internal abstract class El
{
    public string Action = "";          // what clicking it does (see Data.Actions)
    public string RightAction = "", WheelUp = "", WheelDown = "";
    public object? Tag;                 // what it was made from (the editor uses it)
    public virtual bool Clickable => Action.Length > 0 || RightAction.Length > 0 || WheelUp.Length > 0 || WheelDown.Length > 0;
    public bool Hot;                    // under the mouse
    public float X, Y, W, H;            // set by the painter (cells or pixels)
    public virtual bool Stretch => false;   // takes all the width it's given
    public virtual IEnumerable<El> Children => Array.Empty<El>();
    public abstract void Sig(StringBuilder sb);   // describes what it shows (to spot changes)
}

internal sealed class TextEl : El
{
    public List<Span> Spans = new();
    public TextSize Size = TextSize.Normal;
    public string Align = "left";
    public bool Bold, Mono, Tight;
    public float Spacing;               // smooth style: extra pixels between letters
    public Rgba? Background;            // a pill behind the text (bar modules)
    public TextEl() { }
    public TextEl(string text, Rgba color, TextSize size = TextSize.Normal) { Spans.Add(new Span(text, color)); Size = size; }
    public string Plain => string.Concat(Spans.Select(s => s.Text));
    public override void Sig(StringBuilder sb) { foreach (var s in Spans) sb.Append(s.Text).Append(s.Color.Packed); sb.Append(Hot).Append('|'); }
}

/// Large text, e.g. a clock. Terminal style draws it with a block font.
internal sealed class BigEl : El
{
    public string Text = "";
    public Rgba Color, Shadow;
    public bool HasShadow;
    public string Pixel = "█";
    public float Scale = 4f;
    public string Font = "";
    public string Align = "center";
    public override void Sig(StringBuilder sb) => sb.Append("B").Append(Text).Append(Color.Packed).Append('|');
}

internal sealed class MeterEl : El
{
    public string Label = "", ValueText = "";
    public float Value;
    public Rgba Fill, Track, LabelColor, ValueColor;
    public string Chars = "█░";
    public int Cells;                   // bar length in characters (0 = fill the space)
    public int Segments;                // smooth style: the bar is this many separate blocks (0 = one smooth bar)
    public float BarHeight;             // smooth style: bar thickness in pixels (0 = from the font)
    public string ValueAlign = "";      // "around": label, bar, value (inline). Widget meters: "sides" = label left of the bar, value right
    public bool Inline;                 // smooth style: label, bar and value on one line (the bar)
    public string SetAction = "";       // clicking the bar runs this with {v} = the clicked level, 0-100
    public string LabelAction = "";     // clicking the label runs this (e.g. mute)
    public override bool Clickable => base.Clickable || SetAction.Length > 0 || LabelAction.Length > 0;
    public override bool Stretch => Cells == 0;
    public override void Sig(StringBuilder sb) => sb.Append("M").Append(Label).Append(ValueText).Append((int)(Value * 200)).Append('|');
}

internal sealed class GraphEl : El
{
    public float[] Values = Array.Empty<float>();
    public float Max = 100;             // 0 = scale to the highest value
    public Rgba Color;
    public int Rows = 2;
    public int Cells;                   // width in characters (0 = fill the space)
    public override bool Stretch => Cells == 0;
    public override void Sig(StringBuilder sb) { sb.Append("G").Append(Values.Length); if (Values.Length > 0) sb.Append((int)Values[^1]); sb.Append('|'); }
}

internal sealed class ImageEl : El
{
    public string Key = "";
    public Func<Bitmap?> Source = () => null;
    public int Rows = 6;
    public float Px;                    // smooth style: size in pixels (0 = Rows lines of text)
    public string Style = "original";   // original, tint, mono, ascii
    public string AsciiMode = "braille";
    public Gradient? Tint;
    public Rgba Mono;
    public override void Sig(StringBuilder sb) => sb.Append("I").Append(Key).Append(Style).Append('|');
}

internal sealed class ButtonEl : El
{
    public string Label = "";
    public Rgba Color, Hover;
    public Glyph.Render.FrameOptions? Frame;   // smooth style: drawn in this frame instead of a plain pill
    public override void Sig(StringBuilder sb) => sb.Append("K").Append(Label).Append(Hot).Append('|');
}

/// A table of cells, columns aligned (e.g. a calendar).
internal sealed class GridEl : El
{
    public List<List<Span>> Rows = new();
    public override void Sig(StringBuilder sb) { foreach (var r in Rows) foreach (var c in r) sb.Append(c.Text).Append(c.Color.Packed); sb.Append('|'); }
}

internal sealed class GapEl : El
{
    public float Rows = 0.5f;
    public GapEl(float rows = 0.5f) { Rows = rows; }
    public override void Sig(StringBuilder sb) { }
}

/// One child in a frame (smooth style), e.g. a bar module or a workspace number.
internal sealed class BoxEl : El
{
    public El Child = null!;
    public Glyph.Render.FrameOptions? Frame;
    public float PadX, PadY;            // pixels (smooth) or cells (terminal) around the child
    public float MinW, MinH;            // at least this big (same units)
    public override IEnumerable<El> Children => new[] { Child };
    public override bool Stretch => Child.Stretch;
    public override void Sig(StringBuilder sb) { sb.Append('['); Child.Sig(sb); sb.Append(Hot).Append(']'); }
}

/// Bars that bounce with the sound playing (an audio visualiser).
internal sealed class SpectrumEl : El
{
    public float[] Bands = Array.Empty<float>();
    public Rgba Color, Peak;
    public float Rows = 2;               // height in lines of text
    public float BarGap = 2;             // pixels between bars (smooth)
    public bool Mirror;
    public override bool Stretch => true;
    public override void Sig(StringBuilder sb) { sb.Append('S'); foreach (var b in Bands) sb.Append((int)(b * 40)).Append(','); }
}

/// Children side by side.
internal sealed class RowEl : El
{
    public List<El> Items = new();
    public float Gap = 1;               // cells (terminal) — smooth uses this × the font size
    public string Align = "left";
    public override IEnumerable<El> Children => Items;
    public override bool Stretch => Items.Any(i => i.Stretch);
    public override void Sig(StringBuilder sb) { foreach (var i in Items) i.Sig(sb); }
}

/// Children stacked.
internal sealed class ColEl : El
{
    public List<El> Items = new();
    public override IEnumerable<El> Children => Items;
    public override bool Stretch => Items.Any(i => i.Stretch);
    public override void Sig(StringBuilder sb) { foreach (var i in Items) i.Sig(sb); }
}

/// The block font for big terminal text (3×5 pixels per character; each pixel is 2 cells wide).
internal static class BigFont
{
    static readonly Dictionary<char, string[]> G = new()
    {
        ['0'] = new[] { "###", "# #", "# #", "# #", "###" }, ['1'] = new[] { " # ", "## ", " # ", " # ", "###" },
        ['2'] = new[] { "###", "  #", "###", "#  ", "###" }, ['3'] = new[] { "###", "  #", "###", "  #", "###" },
        ['4'] = new[] { "# #", "# #", "###", "  #", "  #" }, ['5'] = new[] { "###", "#  ", "###", "  #", "###" },
        ['6'] = new[] { "###", "#  ", "###", "# #", "###" }, ['7'] = new[] { "###", "  #", "  #", "  #", "  #" },
        ['8'] = new[] { "###", "# #", "###", "# #", "###" }, ['9'] = new[] { "###", "# #", "###", "  #", "###" },
        [':'] = new[] { " ", "#", " ", "#", " " }, ['.'] = new[] { " ", " ", " ", " ", "#" }, ['-'] = new[] { "   ", "   ", "###", "   ", "   " },
        ['/'] = new[] { "  #", "  #", " # ", "#  ", "#  " }, [' '] = new[] { "  ", "  ", "  ", "  ", "  " },
        ['A'] = new[] { "###", "# #", "###", "# #", "# #" }, ['P'] = new[] { "###", "# #", "###", "#  ", "#  " }, ['M'] = new[] { "# #", "###", "###", "# #", "# #" },
    };
    public const int Rows = 5;

    public static string[] Glyph(char c) => G.TryGetValue(char.ToUpperInvariant(c), out var g) ? g : G[' '];

    /// Width in cells (2 per pixel, 2 between characters).
    public static int Width(string s) => s.Length == 0 ? 0 : s.Sum(c => Glyph(c)[0].Length * 2) + (s.Length - 1) * 2;
}
