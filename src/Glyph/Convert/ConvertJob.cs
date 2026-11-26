using System.Diagnostics;
using Glyph.Render;

namespace Glyph.Convert;

/// Runs a conversion: ffmpeg decodes the video at exactly the size the converter samples, and
/// each picture becomes one clip frame. Also makes previews (PNG) and video exports (MP4).
internal static class ConvertJob
{
    public sealed record Layout(int Cols, int Rows, double Fps, int Frames, int CellW, int CellH, FFmpeg.Info Info);

    public sealed record Result(string Path, int Frames, int Cols, int Rows, long Bytes, TimeSpan Took);

    /// Cell size for the current font (same measurement the wallpaper uses).
    public static (int w, int h) CellSize(Theme s)
    {
        using var r = new Renderer(IntPtr.Zero, 8, 8);
        r.SetFont(s.Options.Font);
        return (r.Atlas!.CellW, r.Atlas.CellH);
    }

    public static Layout Plan(ConvertOptions o, Theme s)
    {
        if (!File.Exists(o.Input)) throw new FileNotFoundException("file not found: " + o.Input);
        var info = FFmpeg.Probe(o.Input);
        var (cw, ch) = CellSize(s);
        int cols = o.Columns > 0 ? o.Columns : (int)Math.Ceiling(SystemInformation.VirtualScreen.Width / (double)cw);
        int rows = o.Rows > 0 ? o.Rows : Math.Max(1, (int)Math.Round(cols * (info.Height / (double)info.Width) * (cw / (double)ch)));
        double fps = info.IsImage ? 1 : o.Fps > 0 ? o.Fps : Math.Min(30, info.Fps);
        double len = info.IsImage ? 1 : o.Length > 0 ? o.Length : Math.Max(0.04, info.Duration - o.Start);
        int frames = info.IsImage ? 1 : Math.Max(1, (int)Math.Round(len * fps));
        return new Layout(cols, rows, fps, frames, cw, ch, info);
    }

    static List<string> DecodeArgs(ConvertOptions o, Layout l, double? at = null, double? from = null, double? length = null)
    {
        var a = new List<string> { "-v", "error", "-nostdin" };
        double start = at ?? from ?? o.Start;
        double len = length ?? o.Length;
        if (!l.Info.IsImage && start > 0) { a.Add("-ss"); a.Add(FFmpeg.F(start)); }
        if (!l.Info.IsImage && at == null && len > 0) { a.Add("-t"); a.Add(FFmpeg.F(len)); }
        a.Add("-i"); a.Add(o.Input);
        a.AddRange(new[] { "-an", "-sn" });
        string scale = $"scale={l.Cols * AsciiConverter.SX}:{l.Rows * AsciiConverter.SY}:flags=area,format=rgb24";
        a.Add("-vf"); a.Add(l.Info.IsImage || at != null ? scale : $"fps={FFmpeg.F(l.Fps)},{scale}");
        if (l.Info.IsImage || at != null) { a.Add("-frames:v"); a.Add("1"); }
        a.AddRange(new[] { "-f", "rawvideo", "-pix_fmt", "rgb24", "-" });
        return a;
    }

    static bool ReadExactly(Stream s, byte[] buf)
    {
        int off = 0;
        while (off < buf.Length)
        {
            int n = s.Read(buf, off, buf.Length - off);
            if (n <= 0) return false;
            off += n;
        }
        return true;
    }

    public static string DefaultOutput(ConvertOptions o) =>
        o.Output.Length > 0 ? Paths.Resolve(o.Output.EndsWith(".gclip", StringComparison.OrdinalIgnoreCase) ? o.Output : o.Output + ".gclip", Paths.Clips)
                            : Path.Combine(Paths.Clips, Path.GetFileNameWithoutExtension(o.Input) + ".gclip");

    public static Result Run(ConvertOptions o, Theme s, Action<int, int>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var ffmpeg = FFmpeg.Find() ?? throw new Exception("ffmpeg not found. Install it with:  winget install Gyan.FFmpeg.Essentials");
        var l = Plan(o, s);
        var conv = new AsciiConverter(o, l.Cols, l.Rows, s.Options.Font.Font, s.Options.Font.FontSize, l.CellW, l.CellH);
        var outPath = DefaultOutput(o);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        var tmp = outPath + ".part";
        var rgb = new byte[l.Cols * AsciiConverter.SX * l.Rows * AsciiConverter.SY * 3];
        var cells = new byte[l.Cols * l.Rows * (o.Color ? 5 : 2)];
        int done = 0;

        // looping: a clip on the wallpaper plays over and over, so its end should run straight into its start
        string loop = l.Info.IsImage || l.Frames < 6 ? "off" : o.Loop;
        int blend = loop == "crossfade" ? Math.Clamp((int)Math.Round(o.LoopBlend * l.Fps), 0, l.Frames / 3) : 0;
        if (loop == "crossfade" && blend < 2) loop = "off";
        double total = l.Frames / l.Fps, blendS = blend / l.Fps;
        var bounce = loop == "bounce" && l.Frames <= 4000 ? new List<byte[]>() : null;
        int expect = loop == "crossfade" ? l.Frames - blend : bounce != null ? l.Frames * 2 - 2 : l.Frames;
        try
        {
        // crossfade: the clip is the video minus its last `blend` frames; those are faded into the first ones
        using (var p = FFmpeg.Start(ffmpeg, loop == "crossfade" ? DecodeArgs(o, l, null, o.Start, total - blendS) : DecodeArgs(o, l), false, true))
        using (var tail = loop == "crossfade" ? FFmpeg.Start(ffmpeg, DecodeArgs(o, l, null, o.Start + total - blendS, blendS + 0.5 / l.Fps), false, true) : null)
        {
            try
            {
                using var w = new ClipWriter(tmp, l.Cols, l.Rows, (float)l.Fps, l.CellH / (float)l.CellW, conv.CharTable, o.Color);
                var stdout = p.StandardOutput.BaseStream;
                var rgb2 = tail != null ? new byte[rgb.Length] : null;
                int tailFrames = 0;
                while (ReadExactly(stdout, rgb))
                {
                    ct.ThrowIfCancellationRequested();
                    if (tail != null && done < blend && ReadExactly(tail.StandardOutput.BaseStream, rgb2!))
                    {
                        // frame `done` of the start, mixed with frame `done` of the end: the end at first, the start by the last one
                        int t = (int)Math.Round((done + 1) * 256.0 / (blend + 1));
                        for (int i = 0; i < rgb.Length; i++) rgb[i] = (byte)((rgb2![i] * (256 - t) + rgb[i] * t) >> 8);
                        tailFrames++;
                    }
                    conv.Convert(rgb, cells);
                    w.Add(cells);
                    bounce?.Add((byte[])cells.Clone());
                    done++;
                    progress?.Invoke(done, Math.Max(done, expect));
                }
                if (bounce != null)
                {
                    // then backwards, leaving out both ends so neither shows twice in a row
                    for (int i = bounce.Count - 2; i >= 1; i--) { ct.ThrowIfCancellationRequested(); w.Add(bounce[i]); done++; progress?.Invoke(done, Math.Max(done, expect)); }
                    bounce.Clear();
                }
                if (tail != null && tailFrames < blend) Log.Write($"converter: loop blend used {tailFrames} of {blend} frames");
            }
            finally
            {
                if (!p.HasExited) { try { p.Kill(); } catch { } } p.WaitForExit();
                if (tail != null) { if (!tail.HasExited) { try { tail.Kill(); } catch { } } tail.WaitForExit(); }
            }
            if (done == 0)
            {
                File.Delete(tmp);
                throw new Exception("ffmpeg gave no frames. " + FFmpeg.Errors(p));
            }
        }
        }
        catch { try { File.Delete(tmp); } catch { } throw; }
        try { File.Move(tmp, outPath, true); }
        catch (Exception e)
        {
            try { File.Delete(tmp); } catch { }
            throw new Exception($"couldn't save {Path.GetFileName(outPath)} ({e.Message}). Try another name.");
        }
        return new Result(outPath, done, l.Cols, l.Rows, new FileInfo(outPath).Length, sw.Elapsed);
    }

    /// Converts the single frame at `time` and renders it as Glyph would show it.
    public static Bitmap Preview(ConvertOptions o, Theme theme, double time, out Layout layout)
    {
        var ffmpeg = FFmpeg.Find() ?? throw new Exception("ffmpeg not found");
        var l = Plan(o, theme);
        layout = l;
        var conv = new AsciiConverter(o, l.Cols, l.Rows, theme.Options.Font.Font, theme.Options.Font.FontSize, l.CellW, l.CellH);
        var rgb = new byte[l.Cols * AsciiConverter.SX * l.Rows * AsciiConverter.SY * 3];
        using (var p = FFmpeg.Start(ffmpeg, DecodeArgs(o, l, l.Info.IsImage ? null : time), false, true))
        {
            bool ok = ReadExactly(p.StandardOutput.BaseStream, rgb);
            p.WaitForExit();
            if (!ok) throw new Exception("couldn't read a frame there. " + FFmpeg.Errors(p));
        }
        int bpc = o.Color ? 5 : 2;
        var cells = new byte[l.Cols * l.Rows * bpc];
        conv.Convert(rgb, cells);
        var grid = new Grid();
        grid.Resize(l.Cols, l.Rows);
        var grad = new Gradient(new[] { "bg", "accent_dim", "accent", "bright" }.Select(n => theme[n]).ToList());
        grid.Clear(0);
        for (int i = 0; i < l.Cols * l.Rows; i++)
        {
            int ci = cells[i * bpc];
            grid.Ch[i] = ci < conv.CharTable.Length ? conv.CharTable[ci] : 32;
            grid.Fg[i] = o.Color ? (uint)(cells[i * bpc + 2] | (cells[i * bpc + 3] << 8) | (cells[i * bpc + 4] << 16) | (255 << 24)) : grad.At(cells[i * bpc + 1] / 255f);
        }
        using var r = new Renderer(IntPtr.Zero, l.Cols * l.CellW, l.Rows * l.CellH);
        r.SetFont(theme.Options.Font);
        r.Draw(grid, theme.Options.Effects, 1f, theme["bg"]);
        return r.ReadBack();
    }

    /// Renders a clip to an MP4 the way Glyph shows it (theme colours and screen effects).
    public static void ExportVideo(string clipPath, string mp4, Theme theme, string colors, Action<int, int>? progress, CancellationToken ct)
    {
        var ffmpeg = FFmpeg.Find() ?? throw new Exception("ffmpeg not found");
        using var clip = new ClipReader(clipPath);
        var (cw, ch) = CellSize(theme);
        int W = (clip.Width * cw) & ~1, H = (clip.Height * ch) & ~1;
        using var r = new Renderer(IntPtr.Zero, W, H);
        r.SetFont(theme.Options.Font);
        var grad = new Gradient(new[] { "bg", "accent_dim", "accent", "bright" }.Select(n => theme[n]).ToList());
        var grid = new Grid();
        grid.Resize(clip.Width, clip.Height);
        var args = new List<string> { "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "bgra", "-s", $"{W}x{H}", "-r", FFmpeg.F(clip.Fps), "-i", "-",
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", "-preset", "medium", mp4 };
        using var p = FFmpeg.Start(ffmpeg, args, true, false);
        var buf = new byte[W * H * 4];
        try
        {
            var stdin = p.StandardInput.BaseStream;
            clip.Rewind();
            for (int f = 0; f < clip.FrameCount; f++)
            {
                ct.ThrowIfCancellationRequested();
                clip.Next();
                grid.Clear(0);
                for (int i = 0; i < clip.Ch.Length; i++)
                {
                    grid.Ch[i] = clip.Ch[i];
                    grid.Fg[i] = colors == "original" && clip.HasColor ? clip.Color[i] : grad.At(clip.Bright[i] / 255f);
                }
                r.Draw(grid, theme.Options.Effects, f / clip.Fps, theme["bg"]);
                r.ReadBackRaw(buf);
                stdin.Write(buf, 0, buf.Length);
                progress?.Invoke(f + 1, clip.FrameCount);
            }
            stdin.Close();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception("ffmpeg couldn't write the video. " + FFmpeg.Errors(p));
        }
        finally { if (!p.HasExited) { try { p.Kill(); } catch { } } }
    }
}
