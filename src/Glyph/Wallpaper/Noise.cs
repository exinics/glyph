namespace Glyph.Wallpaper;

/// Fast smooth value noise (0..1) for flowing fields.
internal static class Noise
{
    static readonly byte[] Perm = BuildPerm();

    static byte[] BuildPerm()
    {
        var p = new byte[512];
        var r = new Random(1337);
        var a = Enumerable.Range(0, 256).Select(i => (byte)i).OrderBy(_ => r.Next()).ToArray();
        for (int i = 0; i < 512; i++) p[i] = a[i & 255];
        return p;
    }

    static float Fade(float t) => t * t * (3 - 2 * t);
    static float H(int x, int y, int z) => Perm[Perm[Perm[x & 255] + (y & 255)] + (z & 255)] * (1f / 255f);

    public static float Value(float x, float y, float z)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y), iz = (int)MathF.Floor(z);
        float fx = x - ix, fy = y - iy, fz = z - iz;
        float u = Fade(fx), v = Fade(fy), w = Fade(fz);
        float a = Lerp(H(ix, iy, iz), H(ix + 1, iy, iz), u);
        float b = Lerp(H(ix, iy + 1, iz), H(ix + 1, iy + 1, iz), u);
        float c = Lerp(H(ix, iy, iz + 1), H(ix + 1, iy, iz + 1), u);
        float d = Lerp(H(ix, iy + 1, iz + 1), H(ix + 1, iy + 1, iz + 1), u);
        return Lerp(Lerp(a, b, v), Lerp(c, d, v), w);
    }

    static float Quintic(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    /// Gradient (Perlin) noise, mapped to roughly 0..1. Smoother and less blocky than Value.
    public static float Perlin(float x, float y, float z)
    {
        float fx = MathF.Floor(x), fy = MathF.Floor(y), fz = MathF.Floor(z);
        int X = (int)fx & 255, Y = (int)fy & 255, Z = (int)fz & 255;
        x -= fx; y -= fy; z -= fz;
        float u = Quintic(x), v = Quintic(y), w = Quintic(z);
        var p = Perm;
        int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z, B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;
        float r = Lerp(Lerp(Lerp(Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z), u),
                            Lerp(Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z), u), v),
                       Lerp(Lerp(Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1), u),
                            Lerp(Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1), u), v), w);
        return r * 0.5f + 0.5f;
    }

    public static float Fbm(float x, float y, float z, int octaves)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            sum += Perlin(x, y, z) * amp;
            norm += amp;
            x = x * 2.03f + 17.1f; y = y * 2.03f + 3.7f; z *= 1.5f;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// Cheap integer hash -> 0..1, for per-cell randomness that stays stable.
    public static float Hash(int x, int y, int z)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + z * 2147483647);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }
}
