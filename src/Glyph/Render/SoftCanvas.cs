using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Glyph.Render;

/// Direct2D drawing into a small bitmap in memory (no graphics card), for things drawn now and then:
/// window frames (layered windows) and the settings window's frame preview. Same Canvas as the desktop.
internal sealed class SoftCanvas : IDisposable
{
    readonly ID2D1Factory1 fac;
    readonly Vortice.DirectWrite.IDWriteFactory dw;
    readonly ID2D1DCRenderTarget rt;
    readonly ID2D1DeviceContext dc;
    public readonly Canvas Canvas;
    public readonly IntPtr MemDC;
    IntPtr dib, old, bits;
    public int W { get; private set; }
    public int H { get; private set; }

    public SoftCanvas()
    {
        fac = D2D1.D2D1CreateFactory<ID2D1Factory1>(FactoryType.SingleThreaded);
        dw = Vortice.DirectWrite.DWrite.DWriteCreateFactory<Vortice.DirectWrite.IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
        var props = new RenderTargetProperties(RenderTargetType.Software,
            new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied), 96, 96, RenderTargetUsage.None, FeatureLevel.Default);
        rt = fac.CreateDCRenderTarget(props);
        dc = rt.QueryInterface<ID2D1DeviceContext>();
        Canvas = new Canvas(dc, dw);
        MemDC = CreateCompatibleDC(IntPtr.Zero);
    }

    /// Starts drawing on a clear w × h bitmap.
    public Canvas Begin(int w, int h)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        if (w != W || h != H || dib == IntPtr.Zero)
        {
            if (dib != IntPtr.Zero) { SelectObject(MemDC, old); DeleteObject(dib); }
            var bi = new BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            dib = CreateDIBSection(MemDC, ref bi, 0, out bits, IntPtr.Zero, 0);
            old = SelectObject(MemDC, dib);
            W = w; H = h;
        }
        rt.BindDC(MemDC, new Vortice.RawRect(0, 0, w, h));
        dc.BeginDraw();
        dc.Clear(new Color4(0, 0, 0, 0));
        dc.Transform = Matrix3x2.Identity;
        return Canvas;
    }

    public void End()
    {
        try { dc.EndDraw(); } catch (Exception e) { Log.D("soft canvas: " + e.Message); }
    }

    /// A copy of the picture (premultiplied).
    public Bitmap ToBitmap()
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        var d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
        unsafe { Buffer.MemoryCopy((void*)bits, (void*)d.Scan0, (long)W * H * 4, (long)W * H * 4); }
        bmp.UnlockBits(d);
        return bmp;
    }

    /// Shows the picture in a layered window at (x, y).
    public void UpdateLayered(IntPtr hwnd, int x, int y)
    {
        var pos = new POINT { X = x, Y = y }; var src = new POINT();
        var size = new SIZE { cx = W, cy = H };
        var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        UpdateLayeredWindow(hwnd, IntPtr.Zero, ref pos, ref size, MemDC, ref src, 0, ref blend, 2 /* ULW_ALPHA */);
    }

    public void Dispose()
    {
        Canvas.Dispose();
        dc.Dispose(); rt.Dispose(); dw.Dispose(); fac.Dispose();
        if (dib != IntPtr.Zero) { SelectObject(MemDC, old); DeleteObject(dib); }
        DeleteDC(MemDC);
    }

    [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bi, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref POINT pos, ref SIZE size, IntPtr src, ref POINT srcPos, int key, ref BLENDFUNCTION blend, int flags);
}
