using System.Runtime.InteropServices;

namespace Glyph.Data;

/// Master volume of the default speakers (Windows Core Audio).
internal static class Audio
{
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
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

    static IAudioEndpointVolume? Endpoint()
    {
        IMMDeviceEnumerator? en = null; IMMDevice? dev = null;
        try
        {
            en = (IMMDeviceEnumerator)Mixer.NewEnumerator();
            if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return null;
            var iid = typeof(IAudioEndpointVolume).GUID;
            if (dev.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var o) != 0) return null;
            return (IAudioEndpointVolume)o;
        }
        catch { return null; }
        finally
        {
            if (dev != null) Marshal.ReleaseComObject(dev);
            if (en != null) Marshal.ReleaseComObject(en);
        }
    }

    /// Volume 0..1 (or -1 if there's no speaker) and mute.
    public static (float, bool) Get()
    {
        var ep = Endpoint();
        if (ep == null) return (-1, false);
        try
        {
            ep.GetMasterVolumeLevelScalar(out var v);
            ep.GetMute(out var m);
            return (v, m);
        }
        catch { return (-1, false); }
        finally { Marshal.ReleaseComObject(ep); }
    }

    public static void Set(float v)
    {
        var ep = Endpoint(); if (ep == null) return;
        try { var g = Guid.Empty; ep.SetMasterVolumeLevelScalar(Math.Clamp(v, 0, 1), ref g); } catch { }
        finally { Marshal.ReleaseComObject(ep); }
    }

    public static void Change(float delta) { var (v, _) = Get(); if (v >= 0) Set(v + delta); }

    public static void ToggleMute()
    {
        var ep = Endpoint(); if (ep == null) return;
        try { ep.GetMute(out var m); var g = Guid.Empty; ep.SetMute(!m, ref g); } catch { }
        finally { Marshal.ReleaseComObject(ep); }
    }
}
