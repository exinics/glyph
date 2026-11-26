namespace Glyph.Desktop;

/// Owns the window that sits behind the desktop icons. On Windows 11 24H2 and later the icons
/// (SHELLDLL_DefView) and a WorkerW both live inside Progman, with the WorkerW underneath;
/// our window goes inside that WorkerW. On older builds the WorkerW is a separate top-level window.
internal sealed class DesktopHost : IDisposable
{
    sealed class HostWindow : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_ERASEBKGND: m.Result = 1; return;
                case Native.WM_PAINT: Native.ValidateRect(m.HWnd, IntPtr.Zero); m.Result = IntPtr.Zero; return;
                case Native.WM_NCHITTEST: m.Result = Native.HTTRANSPARENT; return;
            }
            base.WndProc(ref m);
        }
    }

    readonly object gate = new();
    HostWindow? win;
    IntPtr parent;
    int generation;
    volatile int acknowledged = -1;
    readonly List<HostWindow> retired = new();
    Native.POINT origin;

    /// The current window and its generation (changes whenever the window is recreated).
    public (IntPtr hwnd, int gen) Current { get { lock (gate) return (win?.Handle ?? IntPtr.Zero, generation); } }
    public Native.POINT Origin { get { lock (gate) return origin; } }
    public void Acknowledge(int gen) => acknowledged = gen;

    public static IntPtr FindWallpaperParent()
    {
        IntPtr progman = Native.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;
        // Ask Explorer to create the WorkerW that sits under the icons.
        Native.SendMessageTimeout(progman, 0x052C, (IntPtr)0xD, (IntPtr)0x1, Native.SMTO_NORMAL, 1000, out _);
        Native.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, Native.SMTO_NORMAL, 1000, out _);

        // 24H2+: WorkerW is a child of Progman.
        var w = Native.FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
        if (w != IntPtr.Zero) return w;

        // Older: the top-level WorkerW right after the one holding the icons.
        IntPtr found = IntPtr.Zero;
        Native.EnumWindows((top, _) =>
        {
            if (Native.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                found = Native.FindWindowEx(IntPtr.Zero, top, "WorkerW", null);
            return true;
        }, IntPtr.Zero);
        return found != IntPtr.Zero ? found : progman;
    }

    /// Creates (or recreates) the window. UI thread only.
    public bool Attach()
    {
        var p = FindWallpaperParent();
        if (p == IntPtr.Zero) { Log.Write("desktop: no Progman window yet"); return false; }
        if (!Native.IsWindowVisible(p)) Log.Write("desktop: note, the wallpaper WorkerW is hidden");
        Native.GetClientRect(p, out var rc);
        var w = new HostWindow();
        w.CreateHandle(new CreateParams
        {
            Caption = "Glyph wallpaper",
            Parent = p,
            Style = Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_CLIPSIBLINGS | Native.WS_CLIPCHILDREN,
            X = 0, Y = 0, Width = Math.Max(1, rc.Width), Height = Math.Max(1, rc.Height),
        });
        var pt = new Native.POINT();
        Native.ClientToScreen(w.Handle, ref pt);
        lock (gate)
        {
            if (win != null) retired.Add(win);
            win = w; parent = p; generation++; origin = pt;
        }
        Log.Write($"desktop: attached to {Native.ClassOf(p)} 0x{p.ToInt64():X} under {Native.ClassOf(Native.GetParent(p))}, {rc.Width}x{rc.Height} at {pt.X},{pt.Y}");
        return true;
    }

    /// Called every second on the UI thread: reattaches if Explorer restarted, follows size changes,
    /// and destroys old windows once the render thread has moved off them.
    public void Check()
    {
        HostWindow? w; IntPtr p;
        lock (gate) { w = win; p = parent; }
        if (w == null || !Native.IsWindow(p) || !Native.IsWindow(w.Handle) || Native.GetParent(w.Handle) != p)
        {
            Attach();
        }
        else
        {
            Native.GetClientRect(p, out var prc);
            Native.GetClientRect(w.Handle, out var wrc);
            if (prc.Width != wrc.Width || prc.Height != wrc.Height)
                Native.MoveWindow(w.Handle, 0, 0, Math.Max(1, prc.Width), Math.Max(1, prc.Height), false);
            var pt = new Native.POINT();
            Native.ClientToScreen(w.Handle, ref pt);
            lock (gate) origin = pt;
        }
        lock (gate)
        {
            if (acknowledged == generation && retired.Count > 0)
            {
                foreach (var r in retired) { try { r.DestroyHandle(); } catch { } }
                retired.Clear();
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var r in retired) { try { r.DestroyHandle(); } catch { } }
            retired.Clear();
            try { win?.DestroyHandle(); } catch { }
            win = null;
        }
    }
}
