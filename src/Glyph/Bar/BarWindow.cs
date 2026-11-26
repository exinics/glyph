using System.Runtime.InteropServices;

namespace Glyph.Bar;

/// The bar's window: an "app bar" (like the taskbar) so maximised windows stop at it, always on top,
/// never takes focus, and see-through where the bar isn't drawn. Lives on the UI thread; the render
/// thread draws into it.
internal sealed class BarWindow : NativeWindow, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge; public Native.RECT rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT t);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref Native.POINT p);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref WINCOMPATTRDATA data);
    [StructLayout(LayoutKind.Sequential)] struct TRACKMOUSEEVENT { public int cbSize; public uint dwFlags; public IntPtr hwndTrack; public uint dwHoverTime; }
    [StructLayout(LayoutKind.Sequential)] struct ACCENTPOLICY { public int AccentState, AccentFlags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)] struct WINCOMPATTRDATA { public int Attribute; public IntPtr Data; public int SizeOfData; }

    const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABM_ACTIVATE = 6, ABM_WINDOWPOSCHANGED = 9;
    const uint ABE_TOP = 1, ABE_BOTTOM = 3;
    const uint CallbackMsg = 0x0400 + 77;
    const int WS_POPUP = unchecked((int)0x80000000), WS_EX_TOPMOST = 0x8, WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

    public BarOptions O;
    bool registered, tracking, hiddenForFullscreen;
    readonly object gate = new();
    int generation;
    Native.RECT rect;

    /// Mouse events in window pixels: kind 0 = move (x = -1 when it leaves), 1 = left down, 2 = left up, 3 = right up, 4 = wheel (delta).
    public event Action<int, int, int, int>? Mouse;

    public (IntPtr hwnd, int gen, int w, int h) Current { get { lock (gate) return (Handle, generation, rect.Width, rect.Height); } }

    public BarWindow(BarOptions o)
    {
        O = o;
        CreateHandle(new CreateParams
        {
            Caption = "Glyph bar", Style = WS_POPUP,
            ExStyle = Native.WS_EX_TOOLWINDOW | WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP,
            X = 0, Y = 0, Width = 10, Height = 10,
        });
        Apply(o);
        ShowWindow(Handle, 4 /* SW_SHOWNOACTIVATE */);
    }

    /// Applies (possibly changed) options: size, position, space reservation, blur, corners.
    public void Apply(BarOptions o)
    {
        O = o;
        if (registered && !o.ReserveSpace) { Unregister(); }
        Place();
        int corner = o.Blur && o.CornerRadius > 0 ? 2 : 1;   // DWM rounds a blurred bar's corners; otherwise we draw them
        DwmSetWindowAttribute(Handle, 33, ref corner, 4);
        SetBlur(o.Blur);
    }

    void SetBlur(bool on)
    {
        var accent = new ACCENTPOLICY { AccentState = on ? 4 : 0, AccentFlags = 2, GradientColor = 0x01000000 };
        int size = Marshal.SizeOf<ACCENTPOLICY>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WINCOMPATTRDATA { Attribute = 19, Data = ptr, SizeOfData = size };
            SetWindowCompositionAttribute(Handle, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    void Place()
    {
        var screen = Screen.PrimaryScreen!.Bounds;
        int total = O.Height + O.Margin * 2;
        bool top = O.Position != "bottom";
        var strip = new Native.RECT { Left = screen.Left, Right = screen.Right, Top = top ? screen.Top : screen.Bottom - total, Bottom = top ? screen.Top + total : screen.Bottom };
        if (O.ReserveSpace)
        {
            var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = Handle, uCallbackMessage = CallbackMsg };
            if (!registered) { SHAppBarMessage(ABM_NEW, ref abd); registered = true; }
            abd.uEdge = top ? ABE_TOP : ABE_BOTTOM;
            abd.rc = strip;
            SHAppBarMessage(ABM_QUERYPOS, ref abd);
            if (top) abd.rc.Bottom = abd.rc.Top + total; else abd.rc.Top = abd.rc.Bottom - total;
            SHAppBarMessage(ABM_SETPOS, ref abd);
            strip = abd.rc;
        }
        var r = new Native.RECT { Left = strip.Left + O.Margin, Right = strip.Right - O.Margin, Top = strip.Top + O.Margin, Bottom = strip.Bottom - O.Margin };
        SetWindowPos(Handle, (IntPtr)(-1) /* HWND_TOPMOST */, r.Left, r.Top, r.Width, r.Height, 0x0010 /* NOACTIVATE */);
        lock (gate) { rect = r; generation++; }
    }

    void Unregister()
    {
        var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = Handle };
        SHAppBarMessage(ABM_REMOVE, ref abd);
        registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case 0x0021: m.Result = (IntPtr)3; return;                     // WM_MOUSEACTIVATE: MA_NOACTIVATE
            case 0x0084: m.Result = (IntPtr)1; return;                     // WM_NCHITTEST: HTCLIENT
            case 0x0200:                                                    // WM_MOUSEMOVE
                if (!tracking) { var t = new TRACKMOUSEEVENT { cbSize = Marshal.SizeOf<TRACKMOUSEEVENT>(), dwFlags = 2, hwndTrack = Handle }; TrackMouseEvent(ref t); tracking = true; }
                Mouse?.Invoke(0, (short)(m.LParam.ToInt64() & 0xFFFF), (short)((m.LParam.ToInt64() >> 16) & 0xFFFF), 0);
                return;
            case 0x02A3: tracking = false; Mouse?.Invoke(0, -1, -1, 0); return; // WM_MOUSELEAVE
            case 0x0201: Mouse?.Invoke(1, X(m), Y(m), 0); return;          // WM_LBUTTONDOWN
            case 0x0202: Mouse?.Invoke(2, X(m), Y(m), 0); return;          // WM_LBUTTONUP
            case 0x0205: Mouse?.Invoke(3, X(m), Y(m), 0); return;          // WM_RBUTTONUP
            case 0x020A:                                                    // WM_MOUSEWHEEL (screen coordinates)
                {
                    var p = new Native.POINT { X = (short)(m.LParam.ToInt64() & 0xFFFF), Y = (short)((m.LParam.ToInt64() >> 16) & 0xFFFF) };
                    ScreenToClient(Handle, ref p);
                    Mouse?.Invoke(4, p.X, p.Y, (short)((m.WParam.ToInt64() >> 16) & 0xFFFF));
                    return;
                }
            case 0x007E: Place(); break;                                    // WM_DISPLAYCHANGE
            case 0x0006:                                                    // WM_ACTIVATE
            case 0x0047:                                                    // WM_WINDOWPOSCHANGED
                if (registered) { var abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>(), hWnd = Handle }; SHAppBarMessage(m.Msg == 0x0006 ? ABM_ACTIVATE : ABM_WINDOWPOSCHANGED, ref abd); }
                break;
        }
        if (m.Msg == (int)CallbackMsg)
        {
            int code = (int)m.WParam;
            if (code == 1) Place();                                         // ABN_POSCHANGED
            if (code == 2)                                                  // ABN_FULLSCREENAPP: get out of the way of fullscreen apps
            {
                bool full = m.LParam != IntPtr.Zero;
                if (full != hiddenForFullscreen) { hiddenForFullscreen = full; ShowWindow(Handle, full ? 0 : 4); }
            }
            return;
        }
        base.WndProc(ref m);
    }

    static int X(Message m) => (short)(m.LParam.ToInt64() & 0xFFFF);
    static int Y(Message m) => (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);

    public bool HiddenForFullscreen => hiddenForFullscreen;

    public void Dispose()
    {
        if (registered) Unregister();
        DestroyHandle();
    }
}
