using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Glyph.Data;

/// The sound mixer (Windows Core Audio): output devices, the master volume and every app's own volume.
/// A snapshot is refreshed once a second while something shows it, and straight after a change.
internal static class Mixer
{
    public sealed record Device(string Id, string Name, bool IsDefault);
    public sealed record App(string Name, float Volume, bool Muted, bool Playing);

    public sealed class Snapshot
    {
        public List<Device> Devices = new();
        public List<App> Apps = new();
        public string DefaultName = "";
        public float Volume = -1;
        public bool Muted;
    }

    static volatile Snapshot current = new();
    static System.Threading.Timer? timer;
    static readonly object Gate = new();
    static readonly Dictionary<uint, string> names = new();
    public static volatile int Version;

    public static Snapshot Now { get { Use(); return current; } }
    /// Reads it once, without keeping it up to date (for the launcher's list of outputs).
    public static Snapshot Read() { if (timer == null) Refresh(); return current; }

    public static void Use()
    {
        if (timer != null) return;
        lock (Gate)
        {
            if (timer != null) return;
            timer = new System.Threading.Timer(_ => Refresh(), null, 0, 1000);
        }
    }

    /// After a change made by Glyph (so the widget shows it straight away).
    public static void RefreshIfUsed() { if (timer != null) Refresh(); }

    // ───────────── reading ─────────────

    public static void Refresh()
    {
        lock (Gate)
        {
            var s = new Snapshot();
            IMMDeviceEnumerator? en = null;
            try
            {
                en = (IMMDeviceEnumerator)NewEnumerator();
                string defId = "";
                if (en.GetDefaultAudioEndpoint(0, 1, out var def) == 0 && def != null)
                {
                    try
                    {
                        def.GetId(out defId);
                        s.DefaultName = Name(def);
                        var iid = typeof(IAudioEndpointVolume).GUID;
                        if (def.Activate(ref iid, 23, IntPtr.Zero, out var ov) == 0 && ov is IAudioEndpointVolume ep)
                        {
                            try { ep.GetMasterVolumeLevelScalar(out var v); ep.GetMute(out var m); s.Volume = v; s.Muted = m; }
                            finally { Marshal.ReleaseComObject(ep); }
                        }
                        ReadApps(def, s);
                    }
                    finally { Marshal.ReleaseComObject(def); }
                }
                if (en.EnumAudioEndpoints(0, 1, out var coll) == 0 && coll != null)
                {
                    try
                    {
                        coll.GetCount(out var n);
                        for (uint i = 0; i < n; i++)
                        {
                            if (coll.Item(i, out var d) != 0 || d == null) continue;
                            try { d.GetId(out var id); s.Devices.Add(new Device(id, Name(d), id == defId)); }
                            finally { Marshal.ReleaseComObject(d); }
                        }
                    }
                    finally { Marshal.ReleaseComObject(coll); }
                }
            }
            catch (Exception e) { Log.D("mixer: " + e.Message); }
            finally { if (en != null) Marshal.ReleaseComObject(en); }
            current = s;
            Version++;
        }
    }

    static void ReadApps(IMMDevice dev, Snapshot s)
    {
        var byName = new Dictionary<string, (float vol, bool muted, bool playing)>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        ForEachSession(dev, (name, vol, ctl) =>
        {
            vol.GetMasterVolume(out var v); vol.GetMute(out var m);
            ctl.GetState(out var state);
            bool playing = state == 1;
            if (byName.TryGetValue(name, out var had)) byName[name] = (Math.Max(had.vol, v), had.muted && m, had.playing || playing);
            else { byName[name] = (v, m, playing); order.Add(name); }
            return false;
        });
        foreach (var n in order) { var a = byName[n]; s.Apps.Add(new App(n, a.vol, a.muted, a.playing)); }
    }

    /// Calls `f` for each live session on the device with its app name; stops when `f` returns true.
    static void ForEachSession(IMMDevice dev, Func<string, ISimpleAudioVolume, IAudioSessionControl2, bool> f)
    {
        var iid = typeof(IAudioSessionManager2).GUID;
        if (dev.Activate(ref iid, 23, IntPtr.Zero, out var om) != 0 || om is not IAudioSessionManager2 mgr) return;
        try
        {
            if (mgr.GetSessionEnumerator(out var list) != 0 || list == null) return;
            try
            {
                list.GetCount(out var count);
                for (int i = 0; i < count; i++)
                {
                    if (list.GetSession(i, out var ctl) != 0 || ctl == null) continue;
                    try
                    {
                        if (ctl is not IAudioSessionControl2 c2 || ctl is not ISimpleAudioVolume vol) continue;
                        c2.GetState(out var state);
                        if (state == 2) continue;   // expired
                        string name;
                        if (c2.IsSystemSoundsSession() == 0) name = "System sounds";
                        else { c2.GetProcessId(out var pid); name = AppName(pid, c2); }
                        if (f(name, vol, c2)) return;
                    }
                    finally { Marshal.ReleaseComObject(ctl); }
                }
            }
            finally { Marshal.ReleaseComObject(list); }
        }
        finally { Marshal.ReleaseComObject(mgr); }
    }

    static string AppName(uint pid, IAudioSessionControl2 c)
    {
        if (names.TryGetValue(pid, out var n)) return n;
        n = "";
        try
        {
            using var p = Process.GetProcessById((int)pid);
            try { n = p.MainModule?.FileVersionInfo.FileDescription ?? ""; } catch { }
            if (string.IsNullOrWhiteSpace(n)) n = p.ProcessName;
        }
        catch { }
        if (string.IsNullOrWhiteSpace(n)) { try { c.GetDisplayName(out var dn); if (!string.IsNullOrWhiteSpace(dn) && !dn.StartsWith('@')) n = dn; } catch { } }
        if (string.IsNullOrWhiteSpace(n)) n = "app " + pid;
        n = n.Trim();
        if (names.Count > 200) names.Clear();
        names[pid] = n;
        return n;
    }

    static string Name(IMMDevice d)
    {
        try
        {
            if (d.OpenPropertyStore(0, out var store) != 0 || store == null) return "";
            try
            {
                var key = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };   // PKEY_Device_FriendlyName
                if (store.GetValue(ref key, out var pv) != 0) return "";
                try { return pv.vt == 31 ? Marshal.PtrToStringUni(pv.p) ?? "" : ""; }
                finally { PropVariantClear(ref pv); }
            }
            finally { Marshal.ReleaseComObject(store); }
        }
        catch { return ""; }
    }

    // ───────────── changing ─────────────

    /// audio:device:next / prev / &lt;part of a name&gt;, audio:app:&lt;name&gt;:up|down|mute|+5|-5|40, audio:mixer, audio:settings
    public static void Action(string arg)
    {
        var parts = arg.Split(':', 2);
        var what = parts[0].Trim().ToLowerInvariant();
        var rest = parts.Length > 1 ? parts[1].Trim() : "";
        switch (what)
        {
            case "device": SetDevice(rest); break;
            case "app":
                {
                    int c = rest.LastIndexOf(':');
                    if (c > 0) SetApp(rest[..c], rest[(c + 1)..].Trim());
                    break;
                }
            case "mixer": Process.Start(new ProcessStartInfo("ms-settings:apps-volume") { UseShellExecute = true }); return;
            case "settings": Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); return;
        }
        Refresh();
    }

    static void SetDevice(string which)
    {
        Refresh();
        var devs = current.Devices;
        if (devs.Count == 0) return;
        int cur = devs.FindIndex(d => d.IsDefault);
        Device? target = which.ToLowerInvariant() switch
        {
            "next" or "" => devs[(cur + 1 + devs.Count) % devs.Count],
            "prev" or "previous" => devs[(cur - 1 + 2 * devs.Count) % devs.Count],
            _ => devs.FirstOrDefault(d => d.Id == which) ?? devs.FirstOrDefault(d => d.Name.Contains(which, StringComparison.OrdinalIgnoreCase)),
        };
        if (target == null || target.IsDefault) return;
        try
        {
            var pc = (IPolicyConfig)new CPolicyConfigClient();
            try { for (int role = 0; role < 3; role++) pc.SetDefaultEndpoint(target.Id, role); }
            finally { Marshal.ReleaseComObject(pc); }
            Log.Write("sound output: " + target.Name);
        }
        catch (Exception e) { Log.Write("can't change the sound output: " + e.Message); }
    }

    static void SetApp(string name, string op)
    {
        IMMDeviceEnumerator? en = null;
        try
        {
            en = (IMMDeviceEnumerator)NewEnumerator();
            if (en.GetDefaultAudioEndpoint(0, 1, out var def) != 0 || def == null) return;
            try
            {
                var g = Guid.Empty;
                bool? mute = null;
                ForEachSession(def, (n, vol, _) =>
                {
                    if (!n.Equals(name, StringComparison.OrdinalIgnoreCase)) return false;
                    vol.GetMasterVolume(out var v); vol.GetMute(out var m);
                    switch (op.ToLowerInvariant())
                    {
                        case "mute": mute ??= !m; vol.SetMute(mute.Value, ref g); break;
                        case "up": vol.SetMasterVolume(Math.Clamp(v + 0.05f, 0, 1), ref g); break;
                        case "down": vol.SetMasterVolume(Math.Clamp(v - 0.05f, 0, 1), ref g); break;
                        default:
                            if (float.TryParse(op, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                            {
                                float nv = op.StartsWith('+') || op.StartsWith('-') ? v + x / 100f : x / 100f;
                                vol.SetMasterVolume(Math.Clamp(nv, 0, 1), ref g);
                                if (m && nv > 0 && !op.StartsWith('-')) vol.SetMute(false, ref g);
                            }
                            break;
                    }
                    return false;
                });
            }
            finally { Marshal.ReleaseComObject(def); }
        }
        catch (Exception e) { Log.Write("app volume: " + e.Message); }
        finally { if (en != null) Marshal.ReleaseComObject(en); }
    }

    // ───────────── Core Audio interfaces ─────────────

    /// The device enumerator. Windows hands every caller in a process the same object, so it is made
    /// untyped here: two typed [ComImport] classes for it (Audio and Mixer had one each) clash, and
    /// whichever was made second failed with "can't cast MMDeviceEnumerator to MMDeviceEnumerator".
    static readonly Type EnumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), true)!;
    internal static object NewEnumerator() => Activator.CreateInstance(EnumeratorType)!;
    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] class CPolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [StructLayout(LayoutKind.Sequential)] struct PROPERTYKEY { public Guid fmtid; public int pid; }
    [StructLayout(LayoutKind.Sequential)] struct PROPVARIANT { public ushort vt, r1, r2, r3; public IntPtr p, p2; }
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT pv);

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint i, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint count);
        int SetMasterVolumeLevel(float levelDB, ref Guid ctx);
        int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        int GetMasterVolumeLevel(out float levelDB);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint ch, float levelDB, ref Guid ctx);
        int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        int GetChannelVolumeLevel(uint ch, out float levelDB);
        int GetChannelVolumeLevelScalar(uint ch, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator list);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int i, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid ctx);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid ctx);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    /// Undocumented but stable since Windows 7: the only way to change the default sound device.
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int def, IntPtr defPeriod, IntPtr minPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }
}
