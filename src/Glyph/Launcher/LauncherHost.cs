using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glyph.Launcher;

/// The launcher: a pop-up search box (Alt+Space). Keys and the mouse are handled here on the UI
/// thread; the render thread draws it (Engine.LauncherStep) and calls Drawn, and then it's shown.
internal sealed class LauncherHost : NativeWindow, IDisposable, IMessageFilter
{
    public readonly Search Search = new();
    public readonly LauncherScene Scene = new();
    volatile LauncherState state = new();
    readonly Action<Item, bool> run;
    readonly Action wake;
    readonly object gate = new();
    int generation, width = 680, height = 200;
    volatile bool wanted;
    int showId, shownId = -1;
    readonly System.Windows.Forms.Timer fallback = new() { Interval = 400 };
    readonly Control ui;
    public double HiddenAt { get; private set; } = double.MaxValue;
    List<Item> all = new();

    const int WS_POPUP = unchecked((int)0x80000000), WS_EX_TOPMOST = 0x8, WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

    public LauncherHost(Control ui, Action<Item, bool> run, Action wake)
    {
        this.ui = ui; this.run = run; this.wake = wake;
        CreateHandle(new CreateParams
        {
            Caption = "Glyph launcher", Style = WS_POPUP,
            ExStyle = Native.WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP,
            X = -10000, Y = -10000, Width = width, Height = height,
        });
        Application.AddMessageFilter(this);
        fallback.Tick += (_, _) => { fallback.Stop(); if (wanted && shownId != showId) Reveal(); };
        Search.AppsLoaded += () => { try { ui.BeginInvoke(() => { if (wanted) Update(state.Query, keepSelection: true); }); } catch { } };
        IconCache.Loaded += () => wake();
    }

    public bool Visible => wanted;
    public LauncherState State => state;
    public (IntPtr hwnd, int gen, int w, int h, bool want) Surface() { lock (gate) return (Handle, generation, width, height, wanted); }

    public void Toggle(string query = "") { if (wanted) Hide(); else Show(query); }

    public void Show(string query = "")
    {
        var look = Scene.O;
        Search.Refresh();
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int w = Math.Min(look.Width, area.Width - 40);
        lock (gate) { width = w; generation++; }
        int x = area.X + (area.Width - w) / 2, y = area.Y + (int)(area.Height * look.Top);
        SetWindowPos(Handle, (IntPtr)(-1), x, y, w, height, 0x0010 /* NOACTIVATE */);
        wanted = true;
        showId++;
        HiddenAt = double.MaxValue;
        Update(query);
        fallback.Stop(); fallback.Start();
    }

    /// The render thread drew a frame: grow the window if the content needs more room, else show it.
    public void Drawn(float contentHeight)
    {
        try
        {
            ui.BeginInvoke(() =>
            {
                if (!wanted) return;
                int need = (int)Math.Ceiling(contentHeight);
                if (need > height)
                {
                    Native.GetWindowRect(Handle, out var r);
                    lock (gate) { height = need + 40; generation++; }
                    SetWindowPos(Handle, (IntPtr)(-1), r.Left, r.Top, width, height, 0x0010);
                    wake();
                    return;
                }
                if (shownId != showId) Reveal();
            });
        }
        catch { }
    }

    void Reveal()
    {
        shownId = showId;
        ShowWindow(Handle, 5 /* SW_SHOW */);
        if (!SetForegroundWindow(Handle))
        {
            keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero);
            SetForegroundWindow(Handle);
        }
        SetFocus(Handle);
    }

    public void Hide()
    {
        if (!wanted) return;
        wanted = false;
        fallback.Stop();
        ShowWindow(Handle, 0);
        HiddenAt = Environment.TickCount64 / 1000.0;
        wake();
    }

    void Update(string query, bool keepSelection = false)
    {
        var look = Scene.O;
        all = Search.Find(query, Math.Max(look.MaxResults, 40));
        var s = new LauncherState { Query = query, Results = all, Selected = keepSelection ? Math.Min(state.Selected, Math.Max(0, all.Count - 1)) : 0 };
        Fit(s);
        state = s;
        if (Scene.Theme.Smooth && look.Icons) IconCache.Want(all.Skip(s.Top).Take(look.MaxResults).Select(i => i.Icon));
        wake();
    }

    void Fit(LauncherState s)
    {
        int max = Scene.O.MaxResults;
        if (s.Selected < s.Top) s.Top = s.Selected;
        if (s.Selected >= s.Top + max) s.Top = s.Selected - max + 1;
        s.Top = Math.Clamp(s.Top, 0, Math.Max(0, s.Results.Count - max));
    }

    void Move(int d)
    {
        var cur = state;
        if (cur.Results.Count == 0) return;
        var s = new LauncherState { Query = cur.Query, Results = cur.Results, Selected = (cur.Selected + d + cur.Results.Count) % cur.Results.Count, Top = cur.Top, Hover = cur.Hover };
        Fit(s);
        state = s;
        if (Scene.Theme.Smooth && Scene.O.Icons) IconCache.Want(s.Results.Skip(s.Top).Take(Scene.O.MaxResults).Select(i => i.Icon));
        wake();
    }

    void SetHover(int h)
    {
        var cur = state;
        if (cur.Hover == h) return;
        state = new LauncherState { Query = cur.Query, Results = cur.Results, Selected = cur.Selected, Top = cur.Top, Hover = h };
        wake();
    }

    void Activate(int index, bool admin)
    {
        var cur = state;
        if (index < 0 || index >= cur.Results.Count) return;
        var it = cur.Results[index];
        Hide();
        Search.Used(it);
        run(it, admin);
    }

    int RowAt(int x, int y)
    {
        var rects = Scene.RowRects;
        for (int i = 0; i < rects.Length; i++) if (rects[i].Contains(x, y)) return state.Top + i;
        return -1;
    }

    /// Keys are caught here, before Windows Forms: it keeps Enter, Esc, Tab and the arrows for
    /// its own dialogs and never makes typed characters for a window that isn't one of its controls.
    public bool PreFilterMessage(ref Message m)
    {
        if (m.HWnd != Handle || m.Msg is not (0x0100 or 0x0104 or 0x0102 or 0x0101 or 0x0105)) return false;
        if (m.Msg is 0x0100 or 0x0104) Key((int)m.WParam, m.LParam);
        return true;
    }

    void Key(int vk, IntPtr lParam)
    {
        bool shift = (GetKeyState(0x10) & 0x8000) != 0, ctrl = (GetKeyState(0x11) & 0x8000) != 0;
        switch (vk)
        {
            case 0x1B: Hide(); return;                                    // Esc
            case 0x0D: Activate(state.Selected, shift || ctrl); return;   // Enter (Shift/Ctrl+Enter = as administrator)
            case 0x26: Move(-1); return;                                  // Up
            case 0x28: Move(1); return;                                   // Down
            case 0x09: if (!Complete()) Move(shift ? -1 : 1); return;     // Tab: finish a path, else next
            case 0x21: Move(-Scene.O.MaxResults); return;                 // Page up
            case 0x22: Move(Scene.O.MaxResults); return;                  // Page down
        }
        var ks = new byte[256];
        GetKeyboardState(ks);
        var buf = new System.Text.StringBuilder(8);
        int n = ToUnicode((uint)vk, (uint)((lParam.ToInt64() >> 16) & 0xFF), ks, buf, 8, 0);
        if (n > 0) foreach (var ch in buf.ToString(0, n)) Typed(ch);
    }

    protected override void WndProc(ref Message m)
    {

        switch (m.Msg)
        {
            case 0x0006:   // WM_ACTIVATE: clicking somewhere else closes it
                if ((m.WParam.ToInt64() & 0xFFFF) == 0 && wanted && shownId == showId) Hide();
                break;
            case 0x0200:   // WM_MOUSEMOVE
                SetHover(RowAt(X(m), Y(m)));
                return;
            case 0x0201:   // WM_LBUTTONDOWN
                {
                    int row = RowAt(X(m), Y(m));
                    if (row >= 0) Activate(row, false);
                    else if (Y(m) > Scene.ContentHeight) Hide();   // the see-through part under it
                    return;
                }
            case 0x020A:   // WM_MOUSEWHEEL
                Move((short)((m.WParam.ToInt64() >> 16) & 0xFFFF) > 0 ? -1 : 1);
                return;
            case 0x0021: m.Result = (IntPtr)1; return;   // WM_MOUSEACTIVATE: MA_ACTIVATE
        }
        base.WndProc(ref m);
    }

    void Typed(char ch)
    {
        var q = state.Query;
        switch (ch)
        {
            case (char)8: if (q.Length > 0) Update(q[..^1]); return;   // Backspace
            case (char)127:   // Ctrl+Backspace: the last word
                {
                    var t = q.TrimEnd();
                    int i = t.LastIndexOfAny(new[] { ' ', (char)92, '/', '.', '-' });
                    Update(i < 0 ? "" : t[..(i + 1)]);
                    return;
                }
            case (char)22:    // Ctrl+V
                {
                    var clip = Clipboard.ContainsText() ? Clipboard.GetText().Replace("\r", "").Replace("\n", " ") : "";
                    if (clip.Length > 0) Update(q + clip);
                    return;
                }
            case (char)21: case (char)1: Update(""); return;   // Ctrl+U / Ctrl+A: clear
        }
        if (ch >= ' ') Update(q + ch);
    }

    /// Tab on a file or folder result: type its path in, to keep going.
    bool Complete()
    {
        var cur = state;
        if (cur.Selected < 0 || cur.Selected >= cur.Results.Count) return false;
        var it = cur.Results[cur.Selected];
        if (it.Kind is not ("folder" or "file") || !it.Id.StartsWith("path:")) return false;
        Update(it.Title);
        return true;
    }

    static int X(Message m) => (short)(m.LParam.ToInt64() & 0xFFFF);
    static int Y(Message m) => (short)((m.LParam.ToInt64() >> 16) & 0xFFFF);

    public void Dispose() { Application.RemoveMessageFilter(this); fallback.Dispose(); DestroyHandle(); }

    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr h);
    [DllImport("user32.dll")] static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ToUnicode(uint vk, uint scan, byte[] state, System.Text.StringBuilder buf, int size, uint flags);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
