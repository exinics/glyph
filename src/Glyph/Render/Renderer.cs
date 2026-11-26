using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Glyph.Render;

/// Draws a frame with the graphics card:
///   1. backdrop (colour, or gradient/picture via Direct2D),
///   2. the character grid, blended over it,
///   3. smooth widgets (Direct2D),
///   4. screen effects (glow, scanlines…) — or a plain copy when effects are off.
/// Works on a window (swap chain) or off screen (snapshots, previews).
internal sealed class Renderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct GridCB { public int CellW, CellH, GridW, GridH, AtlasCols, P0, P1, P2; public Vector4 Clear; }

    [StructLayout(LayoutKind.Sequential)]
    struct PostCB
    {
        public Vector2 SrcTexel, Dir;
        public float Threshold, Bloom, Scan, ScanSize;
        public float Vignette, Flicker, Grain, Aberration;
        public float Curvature, Time, Brightness, Contrast;
        public Vector2 Resolution; public float Radius, P;
    }

    sealed class Target : IDisposable
    {
        public ID3D11Texture2D Tex = null!; public ID3D11RenderTargetView Rtv = null!; public ID3D11ShaderResourceView Srv = null!;
        public int W, H;
        public void Dispose() { Srv?.Dispose(); Rtv?.Dispose(); Tex?.Dispose(); }
    }

    public readonly ID3D11Device Device;
    readonly ID3D11DeviceContext ctx;
    readonly ID2D1Factory1 d2d;
    readonly ID2D1Device d2dDevice;
    readonly ID2D1DeviceContext dc;
    readonly IDWriteFactory dwrite;
    readonly IDXGIFactory2 dxgi;
    IDXGISwapChain1? swap;
    ID3D11RenderTargetView? backRtv;
    ID3D11Texture2D? offscreen;   // used when there is no window
    ID3D11RenderTargetView? offRtv;

    readonly ID3D11VertexShader vs;
    readonly ID3D11PixelShader psGrid, psBright, psBlur, psComposite, psCopy;
    readonly ID3D11SamplerState linear, point;
    readonly ID3D11Buffer gridCb, postCb;
    readonly ID3D11BlendState over;

    ID3D11Texture2D? cellsTex; ID3D11ShaderResourceView? cellsSrv; int cellsW, cellsH;
    Target? scene, bloomA, bloomB;
    ID2D1Bitmap1? sceneBitmap;
    ID2D1Bitmap1? outBitmap;   // D2D view of the window's own buffer (drawing straight into it when there are no effects)

    public GlyphAtlas? Atlas { get; private set; }
    public Canvas Canvas { get; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    IDCompositionDevice? dcomp; IDCompositionTarget? dtarget; IDCompositionVisual? dvisual;
    readonly bool composition;

    readonly Gpu gpu;

    /// The graphics device and everything that doesn't depend on a window. Shared between the desktop and
    /// the bar (one device instead of two saves memory); freed when the last renderer using it goes.
    sealed class Gpu
    {
        public readonly ID3D11Device Device; public readonly ID3D11DeviceContext Ctx;
        public readonly IDXGIFactory2 Dxgi; public readonly ID2D1Factory1 D2d; public readonly ID2D1Device D2dDevice; public readonly ID2D1DeviceContext Dc;
        public readonly IDWriteFactory Dw;
        public readonly ID3D11VertexShader Vs; public readonly ID3D11PixelShader PsGrid, PsBright, PsBlur, PsComposite, PsCopy;
        public readonly ID3D11SamplerState Linear, Point; public readonly ID3D11Buffer GridCb, PostCb; public readonly ID3D11BlendState Over;
        int refs = 1;

        public Gpu()
        {
            var levels = new[] { Vortice.Direct3D.FeatureLevel.Level_11_1, Vortice.Direct3D.FeatureLevel.Level_11_0, Vortice.Direct3D.FeatureLevel.Level_10_1 };
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.BgraSupport, levels, out var dev, out _, out var context).CheckError();
            Device = dev!; Ctx = context!;
            Dxgi = DXGI.CreateDXGIFactory1<IDXGIFactory2>();
            D2d = D2D1.D2D1CreateFactory<ID2D1Factory1>(Vortice.Direct2D1.FactoryType.SingleThreaded);
            using (var dxgiDevice = Device.QueryInterface<IDXGIDevice>()) D2dDevice = D2d.CreateDevice(dxgiDevice);
            Dc = D2dDevice.CreateDeviceContext(DeviceContextOptions.None);
            Dw = Vortice.DirectWrite.DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
            Vs = Device.CreateVertexShader(Compile("VS", "vs_5_0"));
            PsGrid = Device.CreatePixelShader(Compile("PSGrid", "ps_5_0"));
            PsBright = Device.CreatePixelShader(Compile("PSBright", "ps_5_0"));
            PsBlur = Device.CreatePixelShader(Compile("PSBlur", "ps_5_0"));
            PsComposite = Device.CreatePixelShader(Compile("PSComposite", "ps_5_0"));
            PsCopy = Device.CreatePixelShader(Compile("PSCopy", "ps_5_0"));
            Linear = Device.CreateSamplerState(new SamplerDescription(Vortice.Direct3D11.Filter.MinMagMipLinear, TextureAddressMode.Clamp));
            Point = Device.CreateSamplerState(new SamplerDescription(Vortice.Direct3D11.Filter.MinMagMipPoint, TextureAddressMode.Clamp));
            GridCb = Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<GridCB>(), BindFlags.ConstantBuffer));
            PostCb = Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<PostCB>(), BindFlags.ConstantBuffer));
            var bd = new Vortice.Direct3D11.BlendDescription();
            bd.RenderTarget[0] = new RenderTargetBlendDescription
            {
                BlendEnable = true, SourceBlend = Vortice.Direct3D11.Blend.One, DestinationBlend = Vortice.Direct3D11.Blend.InverseSourceAlpha, BlendOperation = Vortice.Direct3D11.BlendOperation.Add,
                SourceBlendAlpha = Vortice.Direct3D11.Blend.One, DestinationBlendAlpha = Vortice.Direct3D11.Blend.InverseSourceAlpha, BlendOperationAlpha = Vortice.Direct3D11.BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All,
            };
            Over = Device.CreateBlendState(bd);
        }

        public Gpu Retain() { Interlocked.Increment(ref refs); return this; }

        public void Release()
        {
            if (Interlocked.Decrement(ref refs) > 0) return;
            Ctx.ClearState();
            Over.Dispose(); GridCb.Dispose(); PostCb.Dispose(); Linear.Dispose(); Point.Dispose();
            Vs.Dispose(); PsGrid.Dispose(); PsBright.Dispose(); PsBlur.Dispose(); PsComposite.Dispose(); PsCopy.Dispose();
            Dc.Dispose(); D2dDevice.Dispose(); Dw.Dispose(); D2d.Dispose(); Dxgi.Dispose();
            Ctx.Dispose(); Device.Dispose();
        }
    }

    /// True if this renderer's graphics device is still usable (another renderer sharing it may have lost it).
    public bool Healthy => gpu.Device.DeviceRemovedReason.Success;

    /// `composition` = a see-through window (the bar): drawn with transparency via DirectComposition.
    /// `share` = use another renderer's graphics device instead of making a new one.
    public Renderer(IntPtr hwnd, int width, int height, bool composition = false, Renderer? share = null)
    {
        this.composition = composition;
        gpu = share != null && share.Healthy ? share.gpu.Retain() : new Gpu();
        Device = gpu.Device; ctx = gpu.Ctx; dxgi = gpu.Dxgi; d2d = gpu.D2d; d2dDevice = gpu.D2dDevice; dc = gpu.Dc; dwrite = gpu.Dw;
        vs = gpu.Vs; psGrid = gpu.PsGrid; psBright = gpu.PsBright; psBlur = gpu.PsBlur; psComposite = gpu.PsComposite; psCopy = gpu.PsCopy;
        linear = gpu.Linear; point = gpu.Point; gridCb = gpu.GridCb; postCb = gpu.PostCb; over = gpu.Over;
        Canvas = new Canvas(dc, dwrite);

        if (hwnd != IntPtr.Zero) CreateSwapChain(hwnd, width, height);
        else CreateOffscreen(width, height);
    }

    /// Compiled shaders are cached on disk, so the shader compiler is only loaded when they change.
    static byte[] Compile(string entry, string profile)
    {
        var hash = System.Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(Shaders.Source + entry + profile)))[..16];
        var cache = Path.Combine(Paths.Logs, "shaders", $"{entry}-{hash}.bin");
        try { if (File.Exists(cache)) return File.ReadAllBytes(cache); } catch { }
        var bytes = Compiler.Compile(Shaders.Source, entry, "glyph.hlsl", profile, ShaderFlags.OptimizationLevel3, EffectFlags.None).ToArray();
        try { Directory.CreateDirectory(Path.GetDirectoryName(cache)!); File.WriteAllBytes(cache, bytes); } catch { }
        return bytes;
    }

    void CreateSwapChain(IntPtr hwnd, int w, int h)
    {
        Width = Math.Max(1, w); Height = Math.Max(1, h);
        var desc = new SwapChainDescription1
        {
            Width = (uint)Width, Height = (uint)Height, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2, SwapEffect = SwapEffect.FlipDiscard, Scaling = Scaling.Stretch, AlphaMode = Vortice.DXGI.AlphaMode.Ignore,
        };
        if (composition)
        {
            desc.AlphaMode = Vortice.DXGI.AlphaMode.Premultiplied; desc.SwapEffect = SwapEffect.FlipSequential;
            swap = dxgi.CreateSwapChainForComposition(Device, desc);
            using (var dxgiDevice = Device.QueryInterface<IDXGIDevice>()) DComp.DCompositionCreateDevice(dxgiDevice, out dcomp).CheckError();
            dcomp!.CreateTargetForHwnd(hwnd, true, out dtarget).CheckError();
            dcomp.CreateVisual(out dvisual).CheckError();
            dvisual!.SetContent(swap);
            dtarget!.SetRoot(dvisual);
            dcomp.Commit();
            using var b0 = swap.GetBuffer<ID3D11Texture2D>(0);
            backRtv = Device.CreateRenderTargetView(b0);
            return;
        }
        try { swap = dxgi.CreateSwapChainForHwnd(Device, hwnd, desc); }
        catch (Exception e)
        {
            Log.Write($"flip swap chain failed ({e.Message}); trying the older kind");
            desc.SwapEffect = SwapEffect.Discard; desc.BufferCount = 1;
            swap = dxgi.CreateSwapChainForHwnd(Device, hwnd, desc);
        }
        dxgi.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAll);
        using var back = swap.GetBuffer<ID3D11Texture2D>(0);
        backRtv = Device.CreateRenderTargetView(back);
    }

    void CreateOffscreen(int w, int h)
    {
        Width = w; Height = h;
        offscreen = Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Default, BindFlags = BindFlags.RenderTarget,
        });
        offRtv = Device.CreateRenderTargetView(offscreen);
    }

    public void Resize(int w, int h)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        if (w == Width && h == Height) return;
        DropScene();
        DropOut();
        if (swap != null)
        {
            ctx.OMSetRenderTargets((ID3D11RenderTargetView)null!);
            backRtv?.Dispose();
            ctx.Flush();
            swap.ResizeBuffers(0, (uint)w, (uint)h, Format.Unknown, SwapChainFlags.None).CheckError();
            using var back = swap.GetBuffer<ID3D11Texture2D>(0);
            backRtv = Device.CreateRenderTargetView(back);
            Width = w; Height = h;
        }
        else
        {
            offRtv?.Dispose(); offscreen?.Dispose(); stagingRaw?.Dispose(); stagingRaw = null;
            CreateOffscreen(w, h);
        }
    }

    void DropOut()
    {
        if (outBitmap == null) return;
        dc.Target = null;
        outBitmap.Dispose(); outBitmap = null;
    }

    /// Frees the full-screen working buffers and cached pictures (e.g. while the desktop is hidden).
    /// They're made again on the next frame.
    public void Trim()
    {
        DropScene();
        Canvas.Reset();
        ctx.Flush();
    }

    void DropScene()
    {
        dc.Target = null;
        sceneBitmap?.Dispose(); sceneBitmap = null;
        scene?.Dispose(); bloomA?.Dispose(); bloomB?.Dispose();
        scene = bloomA = bloomB = null;
    }

    /// Builds the character atlas for a font. Returns any font problem.
    public string? SetFont(FontOptions f)
    {
        Atlas?.Dispose();
        Atlas = new GlyphAtlas(Device, d2d, dwrite, f);
        return Atlas.FontProblem;
    }

    Target MakeTarget(int w, int h)
    {
        var t = new Target { W = w, H = h };
        t.Tex = Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)w, Height = (uint)h, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
        });
        t.Rtv = Device.CreateRenderTargetView(t.Tex);
        t.Srv = Device.CreateShaderResourceView(t.Tex);
        return t;
    }

    unsafe void Upload(Grid g)
    {
        var atlas = Atlas!;
        if (cellsTex == null || cellsW != g.W || cellsH != g.H)
        {
            cellsSrv?.Dispose(); cellsTex?.Dispose();
            cellsW = g.W; cellsH = g.H;
            cellsTex = Device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)g.W, Height = (uint)g.H, MipLevels = 1, ArraySize = 1, Format = Format.R32G32B32A32_UInt,
                SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource, CPUAccessFlags = CpuAccessFlags.Write,
            });
            cellsSrv = Device.CreateShaderResourceView(cellsTex);
        }
        var map = ctx.Map(cellsTex, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        try
        {
            byte* basePtr = (byte*)map.DataPointer;
            for (int y = 0; y < g.H; y++)
            {
                uint* row = (uint*)(basePtr + y * map.RowPitch);
                int i = y * g.W;
                for (int x = 0; x < g.W; x++, i++)
                {
                    row[x * 4] = (uint)atlas.Index(g.Ch[i]);
                    row[x * 4 + 1] = g.Fg[i];
                    row[x * 4 + 2] = g.Bg[i];
                    row[x * 4 + 3] = 0;
                }
            }
        }
        finally { ctx.Unmap(cellsTex, 0); }
        atlas.Flush();
    }

    /// Draws a full-screen triangle into `rtv` reading `inputs`. The target is bound before the
    /// inputs: Direct3D silently drops an input that is still bound as the previous target.
    void Pass(ID3D11RenderTargetView rtv, int w, int h, ID3D11PixelShader ps, params ID3D11ShaderResourceView[] inputs)
    {
        ctx.OMSetRenderTargets(rtv);
        ctx.PSSetShaderResources(0, inputs);
        ctx.RSSetViewport(new Viewport(0, 0, w, h));
        ctx.PSSetShader(ps);
        ctx.Draw(3, 0);
        Unbind();
    }

    void Unbind() => ctx.PSSetShaderResources(0, new ID3D11ShaderResourceView[] { null!, null! });

    void D2D(Action<Canvas> draw, ID2D1Bitmap1 target)
    {
        dc.Target = target;
        dc.BeginDraw();
        try { draw(Canvas); }
        finally { dc.EndDraw(); }
    }

    public void Draw(Grid g, EffectSettings fx, float time, Rgba clear, Action<Canvas>? under = null, Action<Canvas>? overlay = null)
    {
        var atlas = Atlas!;
        Upload(g);

        // Without effects everything is drawn straight into the window's buffer (no full-screen copy).
        bool direct = !fx.Enabled;
        ID3D11RenderTargetView target;
        ID2D1Bitmap1 d2dTarget;
        var outRtv = swap != null ? backRtv! : offRtv!;
        if (direct)
        {
            if (scene != null) DropScene();
            if (outBitmap == null)
            {
                using var tex = swap != null ? swap.GetBuffer<ID3D11Texture2D>(0) : offscreen!.QueryInterface<ID3D11Texture2D>();
                using var surface = tex.QueryInterface<IDXGISurface>();
                outBitmap = dc.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(
                    new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96,
                    BitmapOptions.Target | BitmapOptions.CannotDraw));
            }
            target = outRtv; d2dTarget = outBitmap;
        }
        else
        {
            DropOut();
            scene ??= MakeTarget(Width, Height);
            if (sceneBitmap == null)
            {
                using var surface = scene.Tex.QueryInterface<IDXGISurface>();
                sceneBitmap = dc.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1(
                    new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96,
                    BitmapOptions.Target | BitmapOptions.CannotDraw));
            }
            target = scene.Rtv; d2dTarget = sceneBitmap;
        }

        // 1. backdrop
        float ca = clear.A / 255f;
        ctx.ClearRenderTargetView(target, new Color4(clear.R / 255f * ca, clear.G / 255f * ca, clear.B / 255f * ca, ca));
        if (under != null) D2D(under, d2dTarget);

        // 2. characters over it
        ctx.IASetInputLayout(null);
        ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        ctx.VSSetShader(vs);
        var gcb = new GridCB
        {
            CellW = atlas.CellW, CellH = atlas.CellH, GridW = g.W, GridH = g.H, AtlasCols = GlyphAtlas.Cols,
            Clear = new Vector4(0, 0, 0, 0),
        };
        ctx.UpdateSubresource(in gcb, gridCb);
        ctx.PSSetConstantBuffer(0, gridCb);
        ctx.OMSetBlendState(over);
        Pass(target, Width, Height, psGrid, cellsSrv!, atlas.Srv);
        ctx.OMSetBlendState(null);

        // 3. smooth widgets
        if (overlay != null) D2D(overlay, d2dTarget);
        if (direct) return;

        // 4. effects
        var sceneT = scene!;
        ctx.IASetInputLayout(null);
        ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        ctx.VSSetShader(vs);
        ctx.PSSetConstantBuffer(0, postCb);

        int qw = Math.Max(1, Width / 4), qh = Math.Max(1, Height / 4);
        bloomA ??= MakeTarget(qw, qh);
        bloomB ??= MakeTarget(qw, qh);
        ctx.PSSetSampler(0, linear);
        var p = new PostCB
        {
            Threshold = fx.BloomThreshold, Bloom = fx.Bloom, Scan = fx.Scanlines, ScanSize = fx.ScanlineSize,
            Vignette = fx.Vignette, Flicker = fx.Flicker, Grain = fx.Grain, Aberration = fx.Aberration,
            Curvature = fx.Curvature, Time = time, Brightness = fx.Brightness, Contrast = fx.Contrast,
            Resolution = new Vector2(Width, Height), Radius = fx.BloomRadius,
        };
        if (fx.Bloom > 0)
        {
            p.SrcTexel = new Vector2(1f / Width, 1f / Height);
            ctx.UpdateSubresource(in p, postCb);
            Pass(bloomA.Rtv, qw, qh, psBright, sceneT.Srv);
            p.SrcTexel = new Vector2(1f / qw, 1f / qh); p.Dir = new Vector2(1, 0);
            ctx.UpdateSubresource(in p, postCb);
            Pass(bloomB.Rtv, qw, qh, psBlur, bloomA.Srv);
            p.Dir = new Vector2(0, 1);
            ctx.UpdateSubresource(in p, postCb);
            Pass(bloomA.Rtv, qw, qh, psBlur, bloomB.Srv);
        }
        else ctx.ClearRenderTargetView(bloomA.Rtv, new Color4(0, 0, 0, 1));

        p.SrcTexel = new Vector2(1f / Width, 1f / Height);
        ctx.UpdateSubresource(in p, postCb);
        Pass(outRtv, Width, Height, psComposite, sceneT.Srv, bloomA.Srv);
    }

    /// Shows the frame. Returns false if the graphics device was lost (caller should rebuild).
    public bool Present()
    {
        if (swap == null) { ctx.Flush(); return true; }
        var r = swap.Present(1, PresentFlags.None);
        if (r.Code == unchecked((int)0x887A0005) || r.Code == unchecked((int)0x887A0007)) return false;
        return true;
    }

    /// Copies the current frame into a bitmap (for snapshots / testing).
    public unsafe System.Drawing.Bitmap ReadBack()
    {
        ID3D11Texture2D src = offscreen ?? swap!.GetBuffer<ID3D11Texture2D>(0);
        using var staging = Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)Width, Height = (uint)Height, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Staging, CPUAccessFlags = CpuAccessFlags.Read,
        });
        ctx.CopyResource(staging, src);
        if (offscreen == null) src.Dispose();
        var map = ctx.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        var bmp = new System.Drawing.Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        for (int y = 0; y < Height; y++)
            Buffer.MemoryCopy((byte*)map.DataPointer + y * map.RowPitch, (byte*)data.Scan0 + y * data.Stride, data.Stride, Width * 4);
        bmp.UnlockBits(data);
        ctx.Unmap(staging, 0);
        return bmp;
    }

    /// Copies the off-screen frame into a premultiplied-alpha bitmap of the same size (reused, no allocation).
    public unsafe void ReadBackInto(System.Drawing.Bitmap bmp)
    {
        stagingRaw ??= Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)Width, Height = (uint)Height, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Staging, CPUAccessFlags = CpuAccessFlags.Read,
        });
        ctx.CopyResource(stagingRaw, offscreen!);
        var map = ctx.Map(stagingRaw, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        for (int y = 0; y < Height; y++)
            Buffer.MemoryCopy((byte*)map.DataPointer + y * map.RowPitch, (byte*)data.Scan0 + y * data.Stride, data.Stride, Width * 4);
        bmp.UnlockBits(data);
        ctx.Unmap(stagingRaw, 0);
    }

    ID3D11Texture2D? stagingRaw;

    /// Copies the current off-screen frame into `dst` as tightly packed BGRA (for video export).
    public unsafe void ReadBackRaw(byte[] dst)
    {
        stagingRaw ??= Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)Width, Height = (uint)Height, MipLevels = 1, ArraySize = 1, Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Staging, CPUAccessFlags = CpuAccessFlags.Read,
        });
        ctx.CopyResource(stagingRaw, offscreen!);
        var map = ctx.Map(stagingRaw, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        fixed (byte* d = dst)
            for (int y = 0; y < Height; y++)
                Buffer.MemoryCopy((byte*)map.DataPointer + y * map.RowPitch, d + y * Width * 4, Width * 4, Width * 4);
        ctx.Unmap(stagingRaw, 0);
    }

    public void Dispose()
    {
        stagingRaw?.Dispose();
        ctx.OMSetRenderTargets((ID3D11RenderTargetView)null!);
        DropScene();
        DropOut();
        cellsSrv?.Dispose(); cellsTex?.Dispose();
        Atlas?.Dispose();
        Canvas.Dispose();
        backRtv?.Dispose(); dvisual?.Dispose(); dtarget?.Dispose(); dcomp?.Dispose(); swap?.Dispose(); offRtv?.Dispose(); offscreen?.Dispose();
        ctx.Flush();
        gpu.Release();
    }
}
