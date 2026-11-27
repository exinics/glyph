using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Glyph.Data;

/// The notification-area (tray) icons, for the bar's "tray" module (so they're still there with Windows' taskbar hidden).
/// Programs send their icons with Shell_NotifyIcon to the top-most window of class Shell_TrayWnd. Glyph makes one of
/// those (on its own thread, above Explorer's) and asks programs to send their icons again ("TaskbarCreated"). Every
/// message is passed on to Explorer's own tray as well, so Explorer stays up to date and nothing is lost when Glyph
/// closes its window. Clicks on an icon go back to the program the same way Explorer sends them.
internal static class Tray
{
    public sealed class Icon
    {
        public string Key = "";
        public IntPtr Owner;            // the program's window that gets the clicks
        public uint Id, CallbackMessage, Version;
        public string Tip = "", Process = "";
        public bool Hidden;
        public Bitmap? Picture;
        public int PictureVersion;
        public uint Pid;
    }

    static readonly object gate = new();
    static readonly List<Icon> icons = new();
    static Thread? thread;
    static IntPtr hwnd;
    static volatile bool running;
    static WndProc? proc;               // kept so the delegate isn't collected
    static uint taskbarCreated;
    static long broadcastAt;
    /// Changes whenever an icon is added, changed or removed (the bar redraws).
    public static int Version { get; private set; }

    /// Starts the tray window (once). Called when a bar shows a tray module.
    public static void Use()
    {
        if (running) return;
        running = true;
        thread = new Thread(Run) { IsBackground = true, Name = "Glyph tray" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// Takes the tray window away (Glyph quitting). Programs go back to sending to Explorer, which has everything.
    public static void Stop()
    {
        if (!running) return;
        running = false;
        if (hwnd != IntPtr.Zero) PostMessage(hwnd, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
        thread?.Join(2000);
    }

    /// The icons now, in the order they were added.
    public static List<Icon> Now() { lock (gate) return icons.ToList(); }

    /// A copy of an icon's picture (the bar draws it on another thread), or null.
    public static Bitmap? CopyPicture(string key)
    {
        lock (gate) { var b = icons.FirstOrDefault(x => x.Key == key)?.Picture; return b == null ? null : new Bitmap(b); }
    }

    static IntPtr explorerTray; static uint explorerPid;

    /// Explorer's own taskbar window: the Shell_TrayWnd that belongs to explorer.exe (never Glyph's tray window,
    /// nor another program's that does the same trick, so messages can't go round in a circle).
    public static IntPtr ExplorerTray()
    {
        var known = explorerTray;
        if (known != IntPtr.Zero && Native.IsWindow(known)) return known;
        IntPtr h = IntPtr.Zero;
        while ((h = Native.FindWindowEx(IntPtr.Zero, h, "Shell_TrayWnd", null)) != IntPtr.Zero)
        {
            if (h == hwnd) continue;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == Environment.ProcessId) continue;
            if (pid != explorerPid)
            {
                try { using var p = System.Diagnostics.Process.GetProcessById((int)pid); if (!p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)) continue; }
                catch { continue; }
                explorerPid = pid;
            }
            explorerTray = h;
            return h;
        }
        return IntPtr.Zero;
    }

    // ───────── the window ─────────

    delegate IntPtr WndProc(IntPtr h, uint msg, IntPtr w, IntPtr l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEX
    {
        public int cbSize; public uint style; public IntPtr lpfnWndProc; public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground; public string? lpszMenuName; public string lpszClassName; public IntPtr hIconSm;
    }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX c);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetTimer(IntPtr h, IntPtr id, uint ms, IntPtr fn);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("user32.dll")] static extern IntPtr CopyIcon(IntPtr h);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);

    static readonly IntPtr HWND_TOPMOST = new(-1), HWND_BROADCAST = new(0xFFFF);

    static void Run()
    {
        try
        {
            proc = WindowProc;
            var wc = new WNDCLASSEX { cbSize = Marshal.SizeOf<WNDCLASSEX>(), lpfnWndProc = Marshal.GetFunctionPointerForDelegate(proc), hInstance = GetModuleHandle(null), lpszClassName = "Shell_TrayWnd" };
            RegisterClassEx(ref wc);
            // a tool window: never in Alt+Tab or the taskbar, never drawn (0 × 0)
            hwnd = CreateWindowEx(0x80 /* WS_EX_TOOLWINDOW */ | 0x08 /* WS_EX_TOPMOST */, "Shell_TrayWnd", "", 0x80000000 /* WS_POPUP */, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero) { Log.Write("tray: couldn't make the tray window"); running = false; return; }
            taskbarCreated = RegisterWindowMessage("TaskbarCreated");
            Raise();
            SetTimer(hwnd, (IntPtr)1, 2000, IntPtr.Zero);
            Broadcast();
            Log.Write("tray: collecting the notification-area icons");
            while (GetMessage(out var m, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessage(ref m); }
        }
        catch (Exception e) { Log.Write("tray: " + e.Message); }
        finally
        {
            hwnd = IntPtr.Zero; running = false;
            lock (gate) { foreach (var i in icons) i.Picture?.Dispose(); icons.Clear(); Version++; }
        }
    }

    /// Keep Glyph's window above Explorer's, so programs find it first.
    static void Raise() => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0200 /* NOSIZE NOMOVE NOACTIVATE NOOWNERZORDER */);

    /// Asks every program to send its tray icons again (to us now).
    static void Broadcast() { broadcastAt = Environment.TickCount64; PostMessage(HWND_BROADCAST, taskbarCreated, IntPtr.Zero, IntPtr.Zero); }

    [DllImport("user32.dll")] static extern bool InSendMessage();

    /// True for a few seconds after Glyph asked programs to send their icons again (so Glyph itself doesn't take
    /// that for Explorer restarting).
    public static bool OwnBroadcast => broadcastAt > 0 && Environment.TickCount64 - broadcastAt < 4000;

    /// Passes a message on to Explorer's tray: sent messages are sent (and their answer returned), posted ones posted.
    static IntPtr Forward(uint msg, IntPtr w, IntPtr l)
    {
        var ex = ExplorerTray();
        if (ex == IntPtr.Zero) return IntPtr.Zero;
        if (msg != 0x004A && !InSendMessage()) { PostMessage(ex, msg, w, l); return IntPtr.Zero; }
        SendMessageTimeout(ex, msg, w, l, 0x0002 /* SMTO_ABORTIFHUNG */, 3000, out var r);
        return r;
    }

    static IntPtr WindowProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        try
        {
            if (msg == 0x004A /* WM_COPYDATA */)
            {
                var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(l);
                var fwd = Forward(msg, w, l);
                if (cds.dwData == (IntPtr)1 && cds.cbData >= 8 + 24) { Notify(cds); return (IntPtr)1; }
                return fwd;
            }
            if (msg == 0x0113 /* WM_TIMER */) { Raise(); Prune(); return IntPtr.Zero; }
            if (msg == taskbarCreated && taskbarCreated != 0)
            {
                // Explorer restarted (or someone else asked): get back on top and ask again, unless it was our own ask
                if (Environment.TickCount64 - broadcastAt > 3000) { Raise(); Broadcast(); }
                return IntPtr.Zero;
            }
            if (msg == 0x0010 /* WM_CLOSE */) { if (!running) DestroyWindow(h); return IntPtr.Zero; }   // only Glyph closes it
            if (msg == 0x0002 /* WM_DESTROY */) { PostQuitMessage(0); return IntPtr.Zero; }
            // what other programs ask the taskbar: WM_COMMAND (e.g. 419 = minimise all windows) and WM_USER messages
            if (msg == 0x0111 || (msg >= 0x0400 /* WM_USER */ && msg < 0xC000)) return Forward(msg, w, l);
        }
        catch (Exception e) { Log.Write("tray: " + e.Message); }
        return DefWindowProc(h, msg, w, l);
    }

    // ───────── icons ─────────

    // NOTIFYICONDATA as the shell sends it to the tray (handles are 32 bits even in 64-bit Windows)
    const int OffWnd = 4, OffId = 8, OffFlags = 12, OffMsg = 16, OffIcon = 20, OffTip = 24, OffState = 280, OffMask = 284, OffVersion = 800, OffGuid = 936;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_STATE = 8, NIF_GUID = 0x20;

    static void Notify(COPYDATASTRUCT cds)
    {
        int len = cds.cbData;
        var data = new byte[len];
        Marshal.Copy(cds.lpData, data, 0, len);
        uint message = BitConverter.ToUInt32(data, 4);
        int b = 8;                       // the NOTIFYICONDATA starts after the signature and the message
        uint U(int off) => b + off + 4 <= len ? BitConverter.ToUInt32(data, b + off) : 0;
        var owner = new IntPtr(U(OffWnd));
        uint id = U(OffId), flags = U(OffFlags);
        var guid = (flags & NIF_GUID) != 0 && b + OffGuid + 16 <= len ? new Guid(data.AsSpan(b + OffGuid, 16)) : Guid.Empty;
        string key = guid != Guid.Empty ? guid.ToString("N") : $"{owner.ToInt64()}_{id}";
        lock (gate)
        {
            var ic = icons.FirstOrDefault(x => x.Key == key);
            switch (message)
            {
                case 0: // NIM_ADD
                case 1: // NIM_MODIFY
                    if (ic == null)
                    {
                        if (message == 1 && owner == IntPtr.Zero) return;
                        ic = new Icon { Key = key };
                        icons.Add(ic);
                    }
                    ic.Owner = owner == IntPtr.Zero ? ic.Owner : owner; ic.Id = id;
                    if (ic.Pid == 0 && ic.Owner != IntPtr.Zero) { GetWindowThreadProcessId(ic.Owner, out ic.Pid); ic.Process = ProcessName(ic.Pid); }
                    if ((flags & NIF_MESSAGE) != 0) ic.CallbackMessage = U(OffMsg);
                    if ((flags & NIF_TIP) != 0 && b + OffTip + 256 <= len) ic.Tip = Encoding.Unicode.GetString(data, b + OffTip, 256).Split('\0')[0];
                    if ((flags & NIF_STATE) != 0)
                    {
                        uint state = U(OffState), mask = U(OffMask);
                        if ((mask & 1) != 0) ic.Hidden = (state & 1) != 0;
                    }
                    if ((flags & NIF_ICON) != 0)
                    {
                        var pic = Picture(new IntPtr(U(OffIcon)));
                        if (pic != null) { ic.Picture?.Dispose(); ic.Picture = pic; ic.PictureVersion++; }
                    }
                    break;
                case 2: // NIM_DELETE
                    if (ic != null) { ic.Picture?.Dispose(); icons.Remove(ic); }
                    break;
                case 4: // NIM_SETVERSION
                    if (ic != null) ic.Version = U(OffVersion);
                    break;
            }
            Version++;
        }
    }

    static Bitmap? Picture(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero) return null;
        var copy = CopyIcon(hIcon);
        if (copy == IntPtr.Zero) return null;
        try { using var ic = System.Drawing.Icon.FromHandle(copy); return new Bitmap(ic.ToBitmap()); }
        catch { return null; }
        finally { DestroyIcon(copy); }
    }

    static string ProcessName(uint pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById((int)pid); return p.ProcessName; } catch { return ""; }
    }

    /// Icons of programs that closed without taking them away.
    static void Prune()
    {
        lock (gate)
        {
            int n = icons.RemoveAll(i => i.Owner != IntPtr.Zero && !Native.IsWindow(i.Owner));
            if (n > 0) Version++;
        }
    }

    // ───────── clicks ─────────

    static string lastClick = ""; static long lastClickAt;

    /// A click on an icon in the bar: "left" or "right" (a second left click soon after is a double click).
    public static void Click(string key, string button)
    {
        Icon? ic;
        lock (gate) ic = icons.FirstOrDefault(x => x.Key == key);
        if (ic == null || ic.Owner == IntPtr.Zero || ic.CallbackMessage == 0) return;
        if (ic.Pid != 0) AllowSetForegroundWindow(ic.Pid);   // so its menu or window can come to the front
        Native.GetCursorPos(out var p);
        bool v4 = ic.Version >= 4;
        long now = Environment.TickCount64;
        bool dbl = button == "left" && lastClick == key && now - lastClickAt <= SystemInformation.DoubleClickTime;
        lastClick = dbl ? "" : key; lastClickAt = now;
        void Send(uint m)
        {
            IntPtr w = v4 ? (IntPtr)((p.Y << 16) | (p.X & 0xFFFF)) : (IntPtr)ic.Id;
            IntPtr l = v4 ? (IntPtr)((long)((ic.Id & 0xFFFF) << 16) | m) : (IntPtr)m;
            PostMessage(ic.Owner, ic.CallbackMessage, w, l);
        }
        if (button == "right")
        {
            Send(0x0204); Send(0x0205);              // WM_RBUTTONDOWN, WM_RBUTTONUP
            if (v4) Send(0x007B);                    // WM_CONTEXTMENU
        }
        else if (dbl) { Send(0x0203); Send(0x0202); } // WM_LBUTTONDBLCLK, WM_LBUTTONUP
        else
        {
            Send(0x0201); Send(0x0202);              // WM_LBUTTONDOWN, WM_LBUTTONUP
            if (v4) Send(0x0400);                    // NIN_SELECT
        }
    }
}
