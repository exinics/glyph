using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Glyph.Data;

/// Live values for format strings ({cpu}, {time:HH:mm}, {media_title}…). System numbers are sampled once a
/// second on a background timer, which only starts when something on screen uses them.
internal static class Stats
{
    [StructLayout(LayoutKind.Sequential)] struct FILETIME { public uint Low, High; public ulong V => ((ulong)High << 32) | Low; }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX { public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DISPLAY_DEVICE { public int cb; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString; public int StateFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string? device, uint num, ref DISPLAY_DEVICE dd, uint flags);

    public const int HistoryLength = 120;

    /// One second's values. Replaced as a whole, so readers always see a consistent set.
    public sealed class Sample
    {
        public float Cpu, Ram, Disk;                // percent
        public ulong RamUsed, RamTotal, DiskFree, DiskTotal;
        public double NetDown, NetUp;               // bytes per second
        public int Battery = -1; public bool Charging, HasBattery;
        public float Volume = -1; public bool Muted;
        public int Desktop = 1, Desktops = 1; public string DesktopName = "";
        public float[] CpuHist = Array.Empty<float>(), RamHist = Array.Empty<float>(), DownHist = Array.Empty<float>(), UpHist = Array.Empty<float>();
    }

    static volatile Sample current = new();
    static System.Threading.Timer? timer;
    static readonly object Gate = new();
    static ulong lastIdle, lastTotal;
    static long lastRx, lastTx; static DateTime lastNet;
    static readonly Queue<float> cpuH = new(), ramH = new(), downH = new(), upH = new();
    static string diskDrive = "C";

    /// Bumps every time new numbers arrive (so screens know to redraw).
    public static volatile int Version;
    public static Sample Now => current;

    public static void Use()
    {
        if (timer != null) return;
        lock (Gate)
        {
            if (timer != null) return;
            timer = new System.Threading.Timer(_ => SampleNow(), null, 0, 1000);
        }
    }

    static void Push(Queue<float> q, float v) { q.Enqueue(v); while (q.Count > HistoryLength) q.Dequeue(); }

    static void SampleNow()
    {
        try
        {
            var s = new Sample();
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                ulong total = kernel.V + user.V, id = idle.V;
                if (lastTotal != 0 && total > lastTotal) s.Cpu = 100f * (1f - (float)(id - lastIdle) / (total - lastTotal));
                lastIdle = id; lastTotal = total;
            }
            var m = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref m)) { s.RamTotal = m.TotalPhys; s.RamUsed = m.TotalPhys - m.AvailPhys; s.Ram = m.MemoryLoad; }
            try
            {
                var d = new DriveInfo(diskDrive);
                if (d.IsReady) { s.DiskTotal = (ulong)d.TotalSize; s.DiskFree = (ulong)d.AvailableFreeSpace; s.Disk = 100f * (1 - (float)d.AvailableFreeSpace / d.TotalSize); }
            }
            catch { }
            long rx = 0, tx = 0;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                try { var st = ni.GetIPStatistics(); rx += st.BytesReceived; tx += st.BytesSent; } catch { }
            }
            var now = DateTime.UtcNow;
            if (lastNet != default)
            {
                double secs = Math.Max(0.2, (now - lastNet).TotalSeconds);
                s.NetDown = Math.Max(0, (rx - lastRx) / secs); s.NetUp = Math.Max(0, (tx - lastTx) / secs);
            }
            lastRx = rx; lastTx = tx; lastNet = now;
            if (Native.GetSystemPowerStatus(out var ps) && ps.BatteryFlag != 128 && ps.BatteryLifePercent != 255)
            { s.HasBattery = true; s.Battery = ps.BatteryLifePercent; s.Charging = ps.ACLineStatus == 1; }
            var (vol, muted) = Audio.Get();
            s.Volume = vol; s.Muted = muted;
            (s.Desktop, s.Desktops, s.DesktopName) = VirtualDesktops.Current();

            Push(cpuH, s.Cpu); Push(ramH, s.Ram); Push(downH, (float)s.NetDown); Push(upH, (float)s.NetUp);
            s.CpuHist = cpuH.ToArray(); s.RamHist = ramH.ToArray(); s.DownHist = downH.ToArray(); s.UpHist = upH.ToArray();
            current = s;
            Version++;
        }
        catch (Exception e) { Log.Write("stats: " + e.Message); }
    }

    // ---------------- static facts ----------------

    static readonly Lazy<string> os = new(() =>
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name = k?.GetValue("ProductName") as string ?? "Windows";
            if (Environment.OSVersion.Version.Build >= 22000) name = name.Replace("Windows 10", "Windows 11");
            var ver = k?.GetValue("DisplayVersion") as string;
            return ver != null ? $"{name} {ver}" : name;
        }
        catch { return "Windows"; }
    });
    static readonly Lazy<string> cpuName = new(() =>
    {
        try { using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"); return (k?.GetValue("ProcessorNameString") as string ?? "").Trim(); }
        catch { return ""; }
    });
    static readonly Lazy<string> gpuName = new(() =>
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref dd, 0); i++) { if ((dd.StateFlags & 4) != 0) return dd.DeviceString; dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>(); }
        return "";
    });

    /// The value for {name:spec}, or null if there's no such name.
    public static string? Value(string name, string spec)
    {
        var custom = Variables.Value(name);
        if (custom != null) return custom;
        switch (name)
        {
            case "time": return DateTime.Now.ToString(spec.Length > 0 ? spec : "HH:mm", CultureInfo.CurrentCulture);
            case "date": return DateTime.Now.ToString(spec.Length > 0 ? spec : "ddd d MMM", CultureInfo.CurrentCulture);
            case "user": return Environment.UserName;
            case "host": return Environment.MachineName.ToLowerInvariant();
            case "os": return os.Value;
            case "cpu_name": return cpuName.Value;
            case "gpu_name": return gpuName.Value;
            case "uptime": return Fmt.Duration(TimeSpan.FromMilliseconds(Environment.TickCount64));
            case "window": return Fmt.Trunc(ActiveWindowTitle(), spec);
            case "window_app": return Fmt.Trunc(Tasks.ForegroundApp(), spec);
        }
        if (name.StartsWith("media")) return Media.Value(name, spec);
        if (name is "wifi" or "wifi_signal" or "network" or "bluetooth" or "lang" or "lang_full") return Status.Value(name, spec);
        if (name is "temp" or "weather" or "weather_icon" or "wind" or "humidity" or "high" or "low" or "city" or "feels") return Weather.Value(name, spec);

        Use();
        var s = current;
        return name switch
        {
            "cpu" => Fmt.Num(s.Cpu, spec),
            "ram" => Fmt.Num(s.Ram, spec),
            "ram_used" => Fmt.Bytes(s.RamUsed, spec),
            "ram_total" => Fmt.Bytes(s.RamTotal, spec),
            "disk" => Fmt.Num(s.Disk, spec),
            "disk_free" => Fmt.Bytes(s.DiskFree, spec),
            "disk_total" => Fmt.Bytes(s.DiskTotal, spec),
            "net_down" => Fmt.Bytes(s.NetDown, spec) + "/s",
            "net_up" => Fmt.Bytes(s.NetUp, spec) + "/s",
            "battery" => s.HasBattery ? Fmt.Num(s.Battery, spec) : "",
            "battery_state" => !s.HasBattery ? "" : s.Charging ? (s.Battery >= 99 ? "full" : "charging") : "on battery",
            "battery_icon" => !s.HasBattery ? "" : s.Charging ? "⚡" : s.Battery > 60 ? "▮▮▮" : s.Battery > 25 ? "▮▮▯" : "▮▯▯",
            "volume" => s.Volume < 0 ? "" : Fmt.Num(s.Volume * 100, spec),
            "muted" => s.Muted ? "muted" : "",
            "audio_device" => Mixer.Now.DefaultName,
            "volume_icon" => s.Volume < 0 ? "" : s.Muted || s.Volume == 0 ? "🔇" : s.Volume < 0.34 ? "🔈" : s.Volume < 0.67 ? "🔉" : "🔊",
            "desktop" => s.Desktop.ToString(CultureInfo.InvariantCulture),
            "desktops" => s.Desktops.ToString(CultureInfo.InvariantCulture),
            "desktop_name" => s.DesktopName,
            "tiling" => Glyph.Tiling.TileManager.Status,
            _ => null,
        };
    }

    /// A 0..1 value for meters: cpu, ram, disk, battery, volume.
    public static float Fraction(string name)
    {
        if (name.StartsWith("media")) return Media.Progress;
        if (name == "wifi") return Status.Signal;
        Use();
        var s = current;
        return name switch
        {
            "cpu" => s.Cpu / 100f, "ram" => s.Ram / 100f, "disk" => s.Disk / 100f,
            "battery" => s.HasBattery ? s.Battery / 100f : 0f, "volume" => Math.Max(0, s.Volume),
            _ => 0f,
        };
    }

    /// History for graphs: cpu, ram, net_down, net_up.
    public static float[] History(string name)
    {
        Use();
        var s = current;
        return name switch { "ram" => s.RamHist, "net_down" => s.DownHist, "net_up" => s.UpHist, _ => s.CpuHist };
    }

    static readonly HashSet<string> ShellClasses = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    public static string ActiveWindowTitle()
    {
        var h = Native.GetForegroundWindow();
        if (h == IntPtr.Zero || ShellClasses.Contains(Native.ClassOf(h))) return "";
        var sb = new System.Text.StringBuilder(256);
        Native.GetWindowText(h, sb, 256);
        var t = sb.ToString();
        return t == "Glyph wallpaper" || t == "Glyph bar" ? "" : t;
    }

    /// True if the format string uses anything that changes over time (so a screen can stay still otherwise).
    public static bool IsLive(string format) => format.Contains('{');
}
