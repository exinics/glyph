namespace Glyph.Render;

internal static class Shaders
{
    public const string Source = """
struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };

VSOut VS(uint id : SV_VertexID)
{
    VSOut o;
    float2 uv = float2((id << 1) & 2, id & 2);
    o.uv = uv;
    o.pos = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}

// ---------------- character grid ----------------
Texture2D<uint4> Cells : register(t0);
Texture2D Atlas : register(t1);

cbuffer GridCB : register(b0)
{
    int2 CellSize;
    int2 GridSize;
    int AtlasCols;
    int3 _gpad;
    float4 ClearColor;
};

float4 Unpack(uint c) { return float4(c & 255, (c >> 8) & 255, (c >> 16) & 255, (c >> 24) & 255) / 255.0; }

float4 PSGrid(VSOut i) : SV_Target
{
    int2 p = int2(i.pos.xy);
    int2 c = p / CellSize;
    if (c.x >= GridSize.x || c.y >= GridSize.y) return ClearColor;
    uint4 cell = Cells.Load(int3(c, 0));
    int2 l = p - c * CellSize;
    int2 a = int2(cell.x % AtlasCols, cell.x / AtlasCols) * CellSize + l;
    float cov = Atlas.Load(int3(a, 0)).a;
    float4 fg = Unpack(cell.y);
    float4 bg = Unpack(cell.z);
    // premultiplied "character over cell background", blended over the backdrop
    float fa = saturate(cov * fg.a);
    float3 rgb = fg.rgb * fa + bg.rgb * bg.a * (1 - fa);
    return float4(rgb, fa + bg.a * (1 - fa));
}

// ---------------- effects ----------------
Texture2D Src : register(t0);
Texture2D Bloom : register(t1);
SamplerState Lin : register(s0);

float4 PSCopy(VSOut i) : SV_Target
{
    return Src.Load(int3(int2(i.pos.xy), 0));
}


cbuffer PostCB : register(b0)
{
    float2 SrcTexel;
    float2 Dir;
    float Threshold;
    float BloomAmount;
    float Scan;
    float ScanSize;
    float Vignette;
    float Flicker;
    float Grain;
    float Aberration;
    float Curvature;
    float Time;
    float Brightness;
    float Contrast;
    float2 Resolution;
    float Radius;
    float _ppad;
};

float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

// Downsample 4x with a soft threshold (keeps only the bright parts for the glow).
float4 PSBright(VSOut i) : SV_Target
{
    float3 c = 0;
    c += Src.Sample(Lin, i.uv + SrcTexel * float2(-1, -1)).rgb;
    c += Src.Sample(Lin, i.uv + SrcTexel * float2( 1, -1)).rgb;
    c += Src.Sample(Lin, i.uv + SrcTexel * float2(-1,  1)).rgb;
    c += Src.Sample(Lin, i.uv + SrcTexel * float2( 1,  1)).rgb;
    c *= 0.25;
    float l = max(c.r, max(c.g, c.b));
    float k = saturate((l - Threshold) / max(1e-3, 1 - Threshold));
    return float4(c * k, 1);
}

float4 PSBlur(VSOut i) : SV_Target
{
    static const float w[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };
    float2 step = Dir * SrcTexel * Radius;
    float3 c = Src.Sample(Lin, i.uv).rgb * w[0];
    [unroll] for (int k = 1; k < 5; k++)
    {
        c += Src.Sample(Lin, i.uv + step * k).rgb * w[k];
        c += Src.Sample(Lin, i.uv - step * k).rgb * w[k];
    }
    return float4(c, 1);
}

float4 PSComposite(VSOut i) : SV_Target
{
    float2 uv = i.uv;
    if (Curvature > 0)
    {
        float2 cc = uv * 2 - 1;
        cc *= 1 + Curvature * 0.25 * dot(cc, cc);
        uv = cc * 0.5 + 0.5;
        if (any(uv < 0) || any(uv > 1)) return float4(0, 0, 0, 1);
    }
    float2 off = Aberration * SrcTexel * (uv - 0.5) * 2;
    float3 col;
    col.r = Src.Sample(Lin, uv + off).r;
    col.g = Src.Sample(Lin, uv).g;
    col.b = Src.Sample(Lin, uv - off).b;
    col += Bloom.Sample(Lin, uv).rgb * BloomAmount;

    float s = sin(i.pos.y * 6.2831853 / ScanSize) * 0.5 + 0.5;
    col *= 1 - Scan * (1 - s);

    float2 d = uv - 0.5;
    col *= saturate(1 - Vignette * dot(d, d) * 2.2);

    col *= 1 + Flicker * (Hash(float2(floor(Time * 30), 7.1)) - 0.5) * 2;
    col += Grain * (Hash(i.pos.xy + frac(Time) * 97.0) - 0.5);

    col = (col - 0.5) * Contrast + 0.5;
    col *= Brightness;
    float a = saturate(Src.Sample(Lin, uv).a + dot(Bloom.Sample(Lin, uv).rgb, 0.333) * BloomAmount);
    return float4(saturate(col) * (a > 0.999 ? 1 : 1), a);
}
""";
}
