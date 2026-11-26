using System.IO.Compression;
using System.Text;

namespace Glyph;

/// The .gclip file: an ASCII animation. Each cell stores a character and a brightness
/// (and optionally its original colour), so the theme can colour clips.
///
/// Layout: "GCLP", u16 version, u16 flags (1 = has colour), u16 width, u16 height, f32 fps,
/// u32 frame count, f32 cell aspect (cell height / width it was made for), u16 char count,
/// u32[char count] characters, then per frame: u32 byte length + Brotli data. Frame data is
/// XOR-ed with the previous frame first, so still parts of a clip take almost no space.
internal static class ClipFormat
{
    public static readonly byte[] Magic = Encoding.ASCII.GetBytes("GCLP");
    public const ushort Version = 1;
}

internal sealed class ClipWriter : IDisposable
{
    readonly BinaryWriter w;
    readonly long frameCountPos;
    readonly byte[] prev;
    readonly byte[] work;
    public readonly int Width, Height, BytesPerCell;
    public int Frames { get; private set; }

    public ClipWriter(string path, int width, int height, float fps, float cellAspect, int[] chars, bool color)
    {
        if (chars.Length > 256) throw new ArgumentException("at most 256 different characters");
        Width = width; Height = height; BytesPerCell = color ? 5 : 2;
        var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        w = new BinaryWriter(fs);
        w.Write(ClipFormat.Magic);
        w.Write(ClipFormat.Version);
        w.Write((ushort)(color ? 1 : 0));
        w.Write((ushort)width); w.Write((ushort)height);
        w.Write(fps);
        frameCountPos = fs.Position;
        w.Write(0u);
        w.Write(cellAspect);
        w.Write((ushort)chars.Length);
        foreach (var c in chars) w.Write((uint)c);
        prev = new byte[width * height * BytesPerCell];
        work = new byte[prev.Length];
    }

    /// Adds a frame. `data` is width*height cells of [charIndex, brightness(, r, g, b)].
    public void Add(byte[] data)
    {
        for (int i = 0; i < data.Length; i++) { work[i] = (byte)(data[i] ^ prev[i]); }
        Buffer.BlockCopy(data, 0, prev, 0, data.Length);
        using var ms = new MemoryStream();
        using (var br = new BrotliStream(ms, CompressionLevel.Optimal, true)) br.Write(work, 0, work.Length);
        w.Write((uint)ms.Length);
        ms.Position = 0;
        ms.CopyTo(w.BaseStream);
        Frames++;
    }

    public void Dispose()
    {
        w.BaseStream.Position = frameCountPos;
        w.Write((uint)Frames);
        w.Dispose();
    }
}

internal sealed class ClipReader : IDisposable
{
    readonly FileStream fs;
    readonly BinaryReader r;
    readonly long firstFrame;
    readonly byte[] packed, buf;
    public readonly int Width, Height, FrameCount, BytesPerCell;
    public readonly float Fps, CellAspect;
    public readonly bool HasColor;
    public readonly int[] Chars;
    public int Index { get; private set; } = -1;

    /// The current frame: per cell, char code point, brightness 0..255, colour (packed, if any).
    public readonly int[] Ch;
    public readonly byte[] Bright;
    public readonly uint[] Color;

    public ClipReader(string path)
    {
        fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);   // a newer conversion may replace it while it plays
        r = new BinaryReader(fs);
        var magic = r.ReadBytes(4);
        if (!magic.SequenceEqual(ClipFormat.Magic)) throw new InvalidDataException("not a .gclip file");
        var ver = r.ReadUInt16();
        if (ver != ClipFormat.Version) throw new InvalidDataException($"unsupported clip version {ver}");
        HasColor = (r.ReadUInt16() & 1) != 0;
        Width = r.ReadUInt16(); Height = r.ReadUInt16();
        Fps = r.ReadSingle();
        FrameCount = (int)r.ReadUInt32();
        CellAspect = r.ReadSingle();
        int n = r.ReadUInt16();
        Chars = new int[n];
        for (int i = 0; i < n; i++) Chars[i] = (int)r.ReadUInt32();
        firstFrame = fs.Position;
        BytesPerCell = HasColor ? 5 : 2;
        packed = new byte[Width * Height * BytesPerCell];
        buf = new byte[packed.Length];
        Ch =new int[Width * Height]; Bright = new byte[Width * Height]; Color = new uint[Width * Height];
        if (FrameCount == 0) throw new InvalidDataException("clip has no frames");
    }

    /// Moves to the next frame (wrapping to the start). Returns false on a broken file.
    public bool Next()
    {
        if (Index + 1 >= FrameCount) Rewind();
        uint len = r.ReadUInt32();
        var comp = r.ReadBytes((int)len);
        using (var br = new BrotliStream(new MemoryStream(comp), CompressionMode.Decompress))
        {
            int off = 0;
            while (off < buf.Length)
            {
                int got = br.Read(buf, off, buf.Length - off);
                if (got <= 0) return false;
                off += got;
            }
            for (int i = 0; i < packed.Length; i++) packed[i] ^= buf[i];
        }
        Index++;
        int bpc = BytesPerCell;
        for (int i = 0, p = 0; i < Ch.Length; i++, p += bpc)
        {
            int ci = packed[p];
            Ch[i] = ci < Chars.Length ? Chars[ci] : 32;
            Bright[i] = packed[p + 1];
            if (HasColor) Color[i] = (uint)(packed[p + 2] | (packed[p + 3] << 8) | (packed[p + 4] << 16) | (255 << 24));
        }
        return true;
    }

    public void Rewind()
    {
        fs.Position = firstFrame;
        Array.Clear(packed);
        Index = -1;
    }

    public void Dispose() { r.Dispose(); }
}
