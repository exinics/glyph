namespace Glyph.Desktop;

/// Owns the windows that sit behind the desktop icons: one per screen Glyph draws on, each exactly over its
/// display. Screens without one keep Windows' own wallpaper. On Windows 11 24H2 and later the icons
/// (SHELLDLL_DefView) and a WorkerW both live inside Progman, with the WorkerW underneath; our windows go
/// inside that WorkerW. On older builds the WorkerW is a separate top-level window.
internal sealed class DesktopHost : IDisposable
{
    sealed class HostWindow : NativeWindow
    {
        public ScreenInfo Screen = null!;
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

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    readonly object gate = new();
    readonly Dictionary<int, HostWindow> wins = new();
    readonly Dictionary<int, Native.POINT> origins = new();
    List<ScreenInfo> wanted = new();
    IntPtr parent;
    int generation;
    volatile int acknowledged = -1;
    readonly List<HostWindow> retired = new();

    /// The window on screen `n` and the generation (changes whenever any window is made, moved or removed).
    public (IntPtr hwnd, int gen) Current(int n) { lock (gate) return (wins.TryGetValue(n, out var w) ? w.Handle : IntPtr.Zero, generation); }
    /// Screen position of the top-left of screen `n`'s window.
    public Native.POINT Origin(int n) { lock (gate) return origins.TryGetValue(n, out var p) ? p : default; }
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

    /// Which screens get a window. UI thread only.
    public void SetScreens(List<ScreenInfo> screens)
    {
        lock (gate) wanted = screens.ToList();
        Sync(false);
    }

    /// Finds the wallpaper's parent window again (e.g. after Explorer restarted) and remakes every window.
    public bool Attach() => Sync(true);

    bool Sync(bool remake)
    {
        var p = remake || parent == IntPtr.Zero || !Native.IsWindow(parent) ? FindWallpaperParent() : parent;
        if (p == IntPtr.Zero) { Log.Write("desktop: no Progman window yet"); return false; }
        bool newParent = p != parent;
        var porg = new Native.POINT();
        Native.ClientToScreen(p, ref porg);
        List<ScreenInfo> want;
        lock (gate) want = wanted.ToList();
        bool changed = false;
        lock (gate)
        {
            foreach (var (n, w) in wins.ToList())
                if (remake || newParent || !want.Any(s => s.Number == n) || !Native.IsWindow(w.Handle) || Native.GetParent(w.Handle) != p)
                {
                    retired.Add(w); wins.Remove(n); origins.Remove(n); changed = true;
                }
        }
        foreach (var s in want)
        {
            int x = s.Bounds.X - porg.X, y = s.Bounds.Y - porg.Y;
            HostWindow? w;
            lock (gate) wins.TryGetValue(s.Number, out w);
            if (w == null)
            {
                w = new HostWindow { Screen = s };
                w.CreateHandle(new CreateParams
                {
                    Caption = "Glyph wallpaper", Parent = p,
                    Style = Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_CLIPSIBLINGS | Native.WS_CLIPCHILDREN,
                    X = x, Y = y, Width = Math.Max(1, s.Bounds.Width), Height = Math.Max(1, s.Bounds.Height),
                });
                lock (gate) wins[s.Number] = w;
                changed = true;
                Log.Write($"desktop: screen {Screens.Describe(s)} under {Native.ClassOf(p)} 0x{p.ToInt64():X}");
            }
            else if (w.Screen.Bounds != s.Bounds)
            {
                Native.MoveWindow(w.Handle, x, y, Math.Max(1, s.Bounds.Width), Math.Max(1, s.Bounds.Height), false);
                w.Screen = s; changed = true;
            }
            var pt = new Native.POINT();
            Native.ClientToScreen(w.Handle, ref pt);
            lock (gate) origins[s.Number] = pt;
        }
        lock (gate) { parent = p; if (changed) generation++; }
        return true;
    }

    /// Called every second on the UI thread: reattaches if Explorer restarted, keeps windows over their screens,
    /// and destroys old windows once the render thread has moved off them.
    public void Check()
    {
        bool broken;
        lock (gate) broken = parent == IntPtr.Zero || !Native.IsWindow(parent) || wins.Values.Any(w => !Native.IsWindow(w.Handle) || Native.GetParent(w.Handle) != parent);
        Sync(broken);
        KeepOnTop();
        lock (gate)
        {
            if (acknowledged == generation && retired.Count > 0)
            {
                foreach (var r in retired) { try { r.DestroyHandle(); } catch { } }
                retired.Clear();
            }
        }
    }

    /// Wallpaper Engine puts its own window in the same WorkerW (and raises it when it opens a wallpaper):
    /// keep ours above every other window there, so a see-through desktop shows its wallpaper under the widgets.
    void KeepOnTop()
    {
        List<IntPtr> ours;
        lock (gate) ours = wins.Values.Select(w => w.Handle).ToList();
        foreach (var h in ours)
        {
            var above = GetWindow(h, 3 /* GW_HWNDPREV */);
            if (above == IntPtr.Zero || ours.Contains(above)) continue;
            SetWindowPos(h, IntPtr.Zero /* HWND_TOP */, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /* NOSIZE NOMOVE NOACTIVATE */);
            Log.Write($"desktop: raised over {Native.ClassOf(above)}");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var r in retired) { try { r.DestroyHandle(); } catch { } }
            retired.Clear();
            foreach (var w in wins.Values) { try { w.DestroyHandle(); } catch { } }
            wins.Clear();
        }
    }
}
