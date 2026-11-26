using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using D2DFeatureLevel = Vortice.Direct2D1.FeatureLevel;

namespace Glyph.Render;

/// A texture holding every character drawn so far, each in a cell-sized slot.
/// Characters are added the first time they are used. Block, shade, box-drawing and braille
/// characters are drawn as exact shapes (not from the font) so they join up seamlessly.
internal sealed class GlyphAtlas : IDisposable
{
    public const int Cols = 64;
    public readonly int CellW, CellH, Rows;
    public readonly ID3D11ShaderResourceView Srv;
    public string? FontProblem;

    readonly ID3D11Texture2D tex;
    readonly ID2D1RenderTarget rt;
    readonly ID2D1SolidColorBrush brush;
    readonly IDWriteTextFormat format;
    readonly ID2D1Factory d2d;
    readonly float textTop;
    readonly ushort[] bmp = new ushort[65536];          // code point -> slot + 1 (0 = not yet drawn)
    readonly Dictionary<int, int> astral = new();       // code points above U+FFFF
    readonly List<(int cp, int slot)> pending = new();
    int count;

    public GlyphAtlas(ID3D11Device device, ID2D1Factory d2d, IDWriteFactory dw, FontOptions s)
    {
        this.d2d = d2d;
        var family = s.Font;
        using (var fonts = dw.GetSystemFontCollection(false))
        {
            if (!fonts.FindFamilyName(family, out _))
            {
                FontProblem = $"theme: font \"{family}\" isn't installed; using Consolas";
                family = "Consolas";
            }
        }
        format = dw.CreateTextFormat(family, null, Weight(s.FontWeight), Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, s.FontSize, "en-us");
        format.WordWrapping = WordWrapping.NoWrap;

        float advance, lineH;
        using (var layout = dw.CreateTextLayout("0000000000", format, 10000, 10000))
        {
            var m = layout.Metrics;
            advance = m.Width / 10f;
            lineH = m.Height;
        }
        CellW = s.CellWidth > 0 ? s.CellWidth : Math.Max(2, (int)Math.Ceiling(advance - 0.05f));
        CellH = s.CellHeight > 0 ? s.CellHeight : Math.Max(4, (int)Math.Ceiling(lineH * s.LineSpacing - 0.05f));
        textTop = MathF.Floor((CellH - lineH) / 2f);
        Rows = Math.Clamp(16384 / CellH, 8, 64);

        tex = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)(Cols * CellW), Height = (uint)(Rows * CellH), MipLevels = 1, ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm, SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default, BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
        });
        Srv = device.CreateShaderResourceView(tex);
        using (var surface = tex.QueryInterface<IDXGISurface>())
        {
            var props = new RenderTargetProperties(RenderTargetType.Default,
                new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96,
                RenderTargetUsage.None, D2DFeatureLevel.Default);
            rt = d2d.CreateDxgiSurfaceRenderTarget(surface, props);
        }
        rt.TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode.Grayscale;
        brush = rt.CreateSolidColorBrush(new Color4(1, 1, 1, 1));

        rt.BeginDraw();
        rt.Clear(new Color4(0, 0, 0, 0));
        rt.EndDraw();
        bmp[32] = 1; count = 1; // slot 0 = space
    }

    static FontWeight Weight(string w) => w switch
    {
        "thin" => FontWeight.Thin, "light" => FontWeight.Light, "medium" => FontWeight.Medium,
        "semibold" => FontWeight.SemiBold, "bold" => FontWeight.Bold, "black" => FontWeight.Black, _ => FontWeight.Normal
    };

    /// Slot index for a code point, adding it if new.
    public int Index(int cp)
    {
        if ((uint)cp < 65536)
        {
            int v = bmp[cp];
            if (v != 0) return v - 1;
        }
        else if (astral.TryGetValue(cp, out var a)) return a;

        if (count >= Cols * Rows) return 0; // full: show as space
        if (cp < 32 || cp == 0x7F) { Remember(cp, 0); return 0; }
        int slot = count++;
        Remember(cp, slot);
        pending.Add((cp, slot));
        return slot;
    }

    void Remember(int cp, int slot)
    {
        if ((uint)cp < 65536) bmp[cp] = (ushort)(slot + 1); else astral[cp] = slot;
    }

    /// Draws any characters added since the last call. Call before rendering.
    public void Flush()
    {
        if (pending.Count == 0) return;
        rt.BeginDraw();
        foreach (var (cp, slot) in pending)
        {
            float x = (slot % Cols) * CellW, y = (slot / Cols) * CellH;
            var r = new Rect(x, y, CellW, CellH);
            rt.PushAxisAlignedClip(r, AntialiasMode.Aliased);
            rt.Clear(new Color4(0, 0, 0, 0));
            if (!Shapes(cp, x, y))
            {
                rt.AntialiasMode = AntialiasMode.PerPrimitive;
                string s = char.ConvertFromUtf32(cp);
                rt.DrawText(s, format, new Rect(x, y + textTop, CellW * 3, CellH * 2), brush, DrawTextOptions.None, Vortice.DCommon.MeasuringMode.Natural);
            }
            rt.PopAxisAlignedClip();
        }
        pending.Clear();
        rt.EndDraw();
    }

    // ---------- drawn shapes ----------

    void Fill(float x, float y, float w, float h, float opacity = 1f)
    {
        if (w <= 0 || h <= 0) return;
        brush.Opacity = opacity;
        rt.FillRectangle(new Rect(x, y, w, h), brush);
        brush.Opacity = 1f;
    }

    bool Shapes(int cp, float ox, float oy)
    {
        if (cp >= 0x2580 && cp <= 0x259F) { rt.AntialiasMode = AntialiasMode.Aliased; Block(cp, ox, oy); return true; }
        if (cp >= 0x2500 && cp <= 0x257F) { rt.AntialiasMode = AntialiasMode.Aliased; Box(cp, ox, oy); return true; }
        if (cp >= 0x2800 && cp <= 0x28FF) { rt.AntialiasMode = AntialiasMode.PerPrimitive; Braille(cp, ox, oy); return true; }
        return false;
    }

    void Block(int cp, float ox, float oy)
    {
        int w = CellW, h = CellH, hw = w / 2, hh = h / 2;
        float E(int n) => MathF.Round(h * n / 8f);
        float Ew(int n) => MathF.Round(w * n / 8f);
        switch (cp)
        {
            case 0x2580: Fill(ox, oy, w, hh); break;
            case >= 0x2581 and <= 0x2588: { float hgt = E(cp - 0x2580); Fill(ox, oy + h - hgt, w, hgt); break; }
            case >= 0x2589 and <= 0x258F: { float wid = Ew(8 - (cp - 0x2588)); Fill(ox, oy, wid, h); break; }
            case 0x2590: Fill(ox + hw, oy, w - hw, h); break;
            case 0x2591: Fill(ox, oy, w, h, 0.25f); break;
            case 0x2592: Fill(ox, oy, w, h, 0.5f); break;
            case 0x2593: Fill(ox, oy, w, h, 0.75f); break;
            case 0x2594: Fill(ox, oy, w, E(1)); break;
            case 0x2595: { float wid = Ew(1); Fill(ox + w - wid, oy, wid, h); break; }
            default:
                {
                    // quadrants: bit 1 = upper left, 2 = upper right, 4 = lower left, 8 = lower right
                    int q = cp switch
                    {
                        0x2596 => 4, 0x2597 => 8, 0x2598 => 1, 0x2599 => 1 | 4 | 8, 0x259A => 1 | 8,
                        0x259B => 1 | 2 | 4, 0x259C => 1 | 2 | 8, 0x259D => 2, 0x259E => 2 | 4, 0x259F => 2 | 4 | 8, _ => 0
                    };
                    if ((q & 1) != 0) Fill(ox, oy, hw, hh);
                    if ((q & 2) != 0) Fill(ox + hw, oy, w - hw, hh);
                    if ((q & 4) != 0) Fill(ox, oy + hh, hw, h - hh);
                    if ((q & 8) != 0) Fill(ox + hw, oy + hh, w - hw, h - hh);
                    break;
                }
        }
    }

    void Braille(int cp, float ox, float oy)
    {
        int bits = cp - 0x2800;
        // dot number -> (column, row): 1..3 left rows 0..2, 4..6 right rows 0..2, 7 left row 3, 8 right row 3
        ReadOnlySpan<int> col = stackalloc int[] { 0, 0, 0, 1, 1, 1, 0, 1 };
        ReadOnlySpan<int> row = stackalloc int[] { 0, 1, 2, 0, 1, 2, 3, 3 };
        float r = MathF.Max(0.8f, MathF.Min(CellW / 4f, CellH / 8f) * 0.75f);
        for (int i = 0; i < 8; i++)
        {
            if ((bits & (1 << i)) == 0) continue;
            float cx = ox + CellW * (0.25f + 0.5f * col[i]);
            float cy = oy + CellH * (0.125f + 0.25f * row[i]);
            rt.FillEllipse(new Ellipse(new Vector2(cx, cy), r, r), brush);
        }
    }

    // Box drawing, U+2500..U+257F. Each entry is weights for Left, Right, Up, Down
    // (0 none, 1 light, 2 heavy, 3 double), or a special: Dnd = dashed (n dashes, H/V, weight), A = arc, X = diagonal.
    static readonly string[] BoxTable =
    {
        "1100","2200","0011","0022","D3H1","D3H2","D3V1","D3V2","D4H1","D4H2","D4V1","D4V2","0101","0201","0102","0202",
        "1001","2001","1002","2002","0110","0210","0120","0220","1010","2010","1020","2020","0111","0211","0121","0112",
        "0122","0221","0212","0222","1011","2011","1021","1012","1022","2021","2012","2022","1101","2101","1201","2201",
        "1102","2102","1202","2202","1110","2110","1210","2210","1120","2120","1220","2220","1111","2111","1211","2211",
        "1121","1112","1122","2121","1221","2112","1212","2221","2212","2122","1222","2222","D2H1","D2H2","D2V1","D2V2",
        "3300","0033","0301","0103","0303","3001","1003","3003","0310","0130","0330","3010","1030","3030","0311","0133",
        "0333","3011","1033","3033","3301","1103","3303","3310","1130","3330","3311","1133","3333","ADR","ADL","AUL",
        "AUR","X/","X\\","XX","1000","0010","0100","0001","2000","0020","0200","0002","1200","0012","2100","0021",
    };

    void Box(int cp, float ox, float oy)
    {
        string code = BoxTable[cp - 0x2500];
        int w = CellW, h = CellH;
        int t1 = Math.Max(1, (int)MathF.Round(h / 18f));
        int t2 = Math.Max(t1 + 1, t1 * 2);
        int g = Math.Max(t1, (int)MathF.Round(w / 5f));
        int cx = w / 2, cy = h / 2;

        if (code[0] == 'D')
        {
            int n = code[1] - '0'; bool horiz = code[2] == 'H'; int t = code[3] == '1' ? t1 : t2;
            float len = horiz ? w : h, seg = len / n;
            for (int i = 0; i < n; i++)
            {
                float a = i * seg + seg * 0.15f, b = seg * 0.6f;
                if (horiz) Fill(ox + MathF.Round(a), oy + cy - t / 2, MathF.Round(b), t);
                else Fill(ox + cx - t / 2, oy + MathF.Round(a), t, MathF.Round(b));
            }
            return;
        }
        if (code[0] == 'A' || code[0] == 'X')
        {
            rt.AntialiasMode = AntialiasMode.PerPrimitive;
            float fx = ox + cx - t1 / 2 + t1 / 2f, fy = oy + cy - t1 / 2 + t1 / 2f;
            if (code[0] == 'X')
            {
                if (code[1] != '\\') rt.DrawLine(new Vector2(ox, oy + h), new Vector2(ox + w, oy), brush, t1);
                if (code[1] != '/') rt.DrawLine(new Vector2(ox, oy), new Vector2(ox + w, oy + h), brush, t1);
                return;
            }
            bool down = code[1] == 'D', right = code[2] == 'R';
            float r = MathF.Min(w, h) / 2f;
            var vEnd = new Vector2(fx, down ? oy + h : oy);
            var hEnd = new Vector2(right ? ox + w : ox, fy);
            var a1 = new Vector2(fx, fy + (down ? r : -r));
            var a2 = new Vector2(fx + (right ? r : -r), fy);
            using var geo = d2d.CreatePathGeometry();
            using (var sink = geo.Open())
            {
                sink.BeginFigure(vEnd, FigureBegin.Hollow);
                sink.AddLine(a1);
                sink.AddQuadraticBezier(new QuadraticBezierSegment { Point1 = new Vector2(fx, fy), Point2 = a2 });
                sink.AddLine(hEnd);
                sink.EndFigure(FigureEnd.Open);
                sink.Close();
            }
            rt.DrawGeometry(geo, brush, t1);
            return;
        }

        int L = code[0] - '0', R = code[1] - '0', U = code[2] - '0', D = code[3] - '0';
        int T(int wgt) => wgt == 2 ? t2 : t1;
        void H(int xa, int xb, int yc, int t) { if (xb > xa) Fill(ox + xa, oy + yc - t / 2, xb - xa, t); }
        void V(int ya, int yb, int xc, int t) { if (yb > ya) Fill(ox + xc - t / 2, oy + ya, t, yb - ya); }

        bool vD = U == 3 || D == 3, hD = L == 3 || R == 3;
        int vt = Math.Max(U > 0 ? T(U) : 0, D > 0 ? T(D) : 0), ht = Math.Max(L > 0 ? T(L) : 0, R > 0 ? T(R) : 0);
        int vLeft = vD ? g + t1 / 2 : vt / 2, vRight = vD ? g + (t1 - t1 / 2) : vt - vt / 2;
        int hTop = hD ? g + t1 / 2 : ht / 2, hBottom = hD ? g + (t1 - t1 / 2) : ht - ht / 2;
        bool vSingle = (U is 1 or 2) || (D is 1 or 2), hSingle = (L is 1 or 2) || (R is 1 or 2);
        int lo = t1 / 2, hi = t1 - t1 / 2; // half-thickness of a double line's strokes

        // horizontal arms
        if (L is 1 or 2)
        {
            int t = T(L), end;
            if (vD) end = (U > 0 && D > 0) ? cx - g + hi : cx + g + hi;
            else end = cx + Math.Max(vRight, t - t / 2);
            H(0, end, cy, t);
        }
        if (R is 1 or 2)
        {
            int t = T(R), start;
            if (vD) start = (U > 0 && D > 0) ? cx + g - lo : cx - g - lo;
            else start = cx - Math.Max(vLeft, t / 2);
            H(start, w, cy, t);
        }
        if (L == 3)
        {
            int up = U == 3 ? cx - g + hi : D == 3 ? cx + g + hi : vSingle ? cx + vRight : cx + hi;
            int dn = D == 3 ? cx - g + hi : U == 3 ? cx + g + hi : vSingle ? cx + vRight : cx + hi;
            H(0, up, cy - g, t1); H(0, dn, cy + g, t1);
        }
        if (R == 3)
        {
            int up = U == 3 ? cx + g - lo : D == 3 ? cx - g - lo : vSingle ? cx - vLeft : cx - lo;
            int dn = D == 3 ? cx + g - lo : U == 3 ? cx - g - lo : vSingle ? cx - vLeft : cx - lo;
            H(up, w, cy - g, t1); H(dn, w, cy + g, t1);
        }
        // vertical arms
        if (U is 1 or 2)
        {
            int t = T(U), end;
            if (hD) end = (L > 0 && R > 0) ? cy - g + hi : cy + g + hi;
            else end = cy + Math.Max(hBottom, t - t / 2);
            V(0, end, cx, t);
        }
        if (D is 1 or 2)
        {
            int t = T(D), start;
            if (hD) start = (L > 0 && R > 0) ? cy + g - lo : cy - g - lo;
            else start = cy - Math.Max(hTop, t / 2);
            V(start, h, cx, t);
        }
        if (U == 3)
        {
            int left = L == 3 ? cy - g + hi : R == 3 ? cy + g + hi : hSingle ? cy + hBottom : cy + hi;
            int right = R == 3 ? cy - g + hi : L == 3 ? cy + g + hi : hSingle ? cy + hBottom : cy + hi;
            V(0, left, cx - g, t1); V(0, right, cx + g, t1);
        }
        if (D == 3)
        {
            int left = L == 3 ? cy + g - lo : R == 3 ? cy - g - lo : hSingle ? cy - hTop : cy - lo;
            int right = R == 3 ? cy + g - lo : L == 3 ? cy - g - lo : hSingle ? cy - hTop : cy - lo;
            V(left, h, cx - g, t1); V(right, h, cx + g, t1);
        }
    }

    public void Dispose()
    {
        brush.Dispose(); rt.Dispose(); Srv.Dispose(); tex.Dispose(); format.Dispose();
    }
}
