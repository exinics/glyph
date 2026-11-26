using System.Runtime.InteropServices;
using System.Text;

namespace Glyph.Data;

/// Wi-Fi name and signal, Bluetooth on/off and the keyboard language: {wifi}, {wifi_signal}, {network},
/// {bluetooth}, {lang}. Network and Bluetooth are read in the background every few seconds, only while shown.
internal static class Status
{
    static volatile string wifi = "", network = "", bluetooth = "";
    static volatile int signal;
    static long lastUse, lastRead = -100000;
    static int busy;

    static volatile bool netChanged = true;
    static bool watching;
    static Windows.Devices.Radios.Radio? btRadio;
    static long lastBtLookup = -100000;

    static void Use()
    {
        long now = Environment.TickCount64;
        lastUse = now;
        // the network is read when Windows says it changed, and every 20 s for the signal strength
        if ((!netChanged && now - lastRead < 20000) || Interlocked.Exchange(ref busy, 1) == 1) return;
        lastRead = now; netChanged = false;
        Task.Run(() =>
        {
            try
            {
                if (!watching) { watching = true; Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += _ => netChanged = true; }
                ReadNetwork();
            }
            catch (Exception e) { Log.D("network status: " + e.Message); }
            try { ReadBluetooth(); } catch (Exception e) { Log.D("bluetooth status: " + e.Message); }
            finally { busy = 0; }
        });
    }

    static void ReadNetwork()
    {
        var p = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
        if (p == null) { wifi = ""; network = "none"; signal = 0; return; }
        if (p.IsWlanConnectionProfile)
        {
            wifi = p.WlanConnectionProfileDetails?.GetConnectedSsid() ?? "";
            network = "wifi";
            var bars = p.GetSignalBars();
            signal = bars.HasValue ? bars.Value * 20 : 0;
        }
        else
        {
            wifi = ""; signal = 100;
            network = p.IsWwanConnectionProfile ? "mobile" : "ethernet";
        }
    }

    static void ReadBluetooth()
    {
        // finding the radio is slow, so it's done once (again after a minute if there was none); its state is cheap
        if (btRadio == null && Environment.TickCount64 - lastBtLookup > 60000)
        {
            lastBtLookup = Environment.TickCount64;
            var radios = Windows.Devices.Radios.Radio.GetRadiosAsync().AsTask().GetAwaiter().GetResult();
            btRadio = radios.FirstOrDefault(r => r.Kind == Windows.Devices.Radios.RadioKind.Bluetooth);
            if (btRadio != null) btRadio.StateChanged += (r, _) => bluetooth = r.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
        }
        bluetooth = btRadio == null ? "" : btRadio.State == Windows.Devices.Radios.RadioState.On ? "on" : "off";
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int LCIDToLocaleName(uint lcid, StringBuilder name, int size, uint flags);

    static string Lang(bool full)
    {
        var hkl = GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        uint lang = (uint)hkl.ToInt64() & 0xFFFF;
        var sb = new StringBuilder(85);
        if (LCIDToLocaleName(lang, sb, sb.Capacity, 0) <= 0) return "";
        var s = sb.ToString();
        return full ? s : s.Split('-')[0].ToUpperInvariant();
    }

    public static string? Value(string name, string spec)
    {
        switch (name)
        {
            case "wifi": Use(); return Fmt.Trunc(wifi, spec);
            case "wifi_signal": Use(); return Fmt.Num(signal, spec);
            case "network": Use(); return network;
            case "bluetooth": Use(); return bluetooth;
            case "lang": return Lang(false);
            case "lang_full": return Lang(true);
        }
        return null;
    }

    public static float Signal { get { Use(); return signal / 100f; } }
}
