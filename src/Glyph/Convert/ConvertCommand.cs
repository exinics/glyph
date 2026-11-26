using System.Globalization;
using System.Runtime.InteropServices;

namespace Glyph.Convert;

/// Glyph.exe --convert <video or image> [options]   (see GUIDE.md, "The converter")
internal static class ConvertCommand
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    public const string Help = """
Glyph converter: turns a video, GIF or picture into an ASCII clip for the wallpaper.

  Glyph.exe --convert <file> [options]

  --out NAME          clip name (saved in config\clips). Default: the file's name
  --mode MODE         ramp, shape, edges, blocks, braille   (default shape)
  --width N           width in characters (default: your screen's width)
  --rows N            height in characters (default: keeps the video's shape)
  --fps N             frames per second (default: the video's, at most 30)
  --start S           start at S seconds
  --length S          only convert S seconds
  --chars "..."       characters to use, faintest first (ramp/edges/shape)
  --brightness X      -1 to 1 (default 0)
  --contrast X        default 1
  --gamma X           default 1 (above 1 brightens the dark parts)
  --invert            swap light and dark
  --no-auto-levels    don't stretch each frame's brightness to the full range
  --color             also keep the original colours
  --dither none       turn off dithering (default ordered)
  --edge X            edges mode: how strong an edge must be (default 0.08)
  --loop KIND         crossfade = the end fades into the start (default), bounce = plays forwards
                      then backwards, off = as it is (clips repeat on the wallpaper)
  --blend S           crossfade: seconds of fade (default 1)
  --preview FILE.png  only make a preview picture of one frame (at --start)
  --mp4 FILE.mp4      also export a video of the result
""";

    public static int Run(string[] args)
    {
        AttachConsole(-1);
        Console.WriteLine();
        if (args.Length < 2 || args[1] is "-h" or "--help") { Console.WriteLine(Help); return 0; }
        var o = new ConvertOptions { Input = Path.GetFullPath(args[1]) };
        string? preview = null, mp4 = null;
        for (int i = 2; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new Exception($"{args[i]} needs a value");
            float Fl() => float.Parse(Next(), CultureInfo.InvariantCulture);
            switch (args[i])
            {
                case "--out": o.Output = Next(); break;
                case "--mode": o.Mode = Next().ToLowerInvariant(); if (!ConvertOptions.Modes.Contains(o.Mode)) throw new Exception("mode must be one of: " + string.Join(", ", ConvertOptions.Modes)); break;
                case "--width": o.Columns = int.Parse(Next()); break;
                case "--rows": o.Rows = int.Parse(Next()); break;
                case "--fps": o.Fps = Fl(); break;
                case "--start": o.Start = Fl(); break;
                case "--length": o.Length = Fl(); break;
                case "--chars": o.Chars = Next(); break;
                case "--brightness": o.Brightness = Fl(); break;
                case "--contrast": o.Contrast = Fl(); break;
                case "--gamma": o.Gamma = Fl(); break;
                case "--invert": o.Invert = true; break;
                case "--loop": o.Loop = Next().ToLowerInvariant(); if (!ConvertOptions.Loops.Contains(o.Loop)) throw new Exception("loop must be one of: " + string.Join(", ", ConvertOptions.Loops)); break;
                case "--blend": o.LoopBlend = Fl(); break;
                case "--no-auto-levels": o.AutoLevels = false; break;
                case "--color": o.Color = true; break;
                case "--dither": o.Dither = Next(); break;
                case "--edge": o.EdgeThreshold = Fl(); break;
                case "--preview": preview = Next(); break;
                case "--mp4": mp4 = Next(); break;
                default: throw new Exception($"unknown option {args[i]} (try --help)");
            }
        }
        var problems = new List<string>();
        var theme = Theme.Load(AppSettings.Load(problems).Theme, problems);
        if (preview != null)
        {
            using var bmp = ConvertJob.Preview(o, theme, o.Start, out var l);
            bmp.Save(preview, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"preview saved: {preview} ({l.Cols}x{l.Rows} characters)");
            return 0;
        }
        int lastPct = -1;
        var res = ConvertJob.Run(o, theme, (d, t) =>
        {
            int pct = d * 100 / Math.Max(1, t);
            if (pct != lastPct && pct % 5 == 0) { lastPct = pct; Console.Write($"\r  converting... {pct}%   "); }
        }, CancellationToken.None);
        Console.WriteLine($"\rsaved {res.Path}\n  {res.Frames} frames, {res.Cols}x{res.Rows} characters, {res.Bytes / 1024.0:0} KB, took {res.Took.TotalSeconds:0.0} s");
        Console.WriteLine($"  use it: add a [[layer]] with type = \"clip\" and file = \"{Path.GetFileName(res.Path)}\" to wallpaper.toml");
        if (mp4 != null)
        {
            ConvertJob.ExportVideo(res.Path, Path.GetFullPath(mp4), theme, o.Color ? "original" : "gradient", null, CancellationToken.None);
            Console.WriteLine($"  video saved: {mp4}");
        }
        return 0;
    }
}
