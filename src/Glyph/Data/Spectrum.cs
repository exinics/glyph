using System.Runtime.InteropServices;

namespace Glyph.Data;

/// What the speakers are playing, as bouncing bars (for audio visualisers). Reads the default output
/// device's mix (WASAPI loopback) on its own thread, only while something showed it in the last few seconds.
internal static class Spectrum
{
    const int N = 1024;                 // FFT size
    const int Base = 64;                // bands worked out; Bands(n) resamples
    static readonly float[] ring = new float[N];
    static int ringPos;
    static readonly float[] bands = new float[Base];
    static readonly object gate = new();
    static Thread? thread;
    static long lastUse, lastLoud;

    /// Call while showing it: starts the reader (it stops itself 3 s after the last call).
    public static void Use()
    {
        lastUse = Environment.TickCount64;
        if (thread != null) return;
        lock (gate)
        {
            if (thread != null) return;
            thread = new Thread(Run) { IsBackground = true, Name = "Glyph spectrum", Priority = ThreadPriority.BelowNormal };
            thread.Start();
        }
    }

    /// True while there's sound (so the widget keeps redrawing); false after half a second of silence.
    public static bool Moving => Environment.TickCount64 - lastLoud < 600;

    /// `n` bars, 0..1, low notes first.
    public static float[] Bands(int n)
    {
        var res = new float[Math.Max(1, n)];
        lock (bands)
            for (int i = 0; i < res.Length; i++)
            {
                float a = i * Base / (float)res.Length, b = (i + 1) * Base / (float)res.Length;
                float m = 0;
                for (int k = (int)a; k < Math.Min(Base, Math.Max((int)a + 1, (int)MathF.Ceiling(b))); k++) m = Math.Max(m, bands[k]);
                res[i] = m;
            }
        return res;
    }

    static void Run()
    {
        try
        {
            while (Environment.TickCount64 - lastUse < 3000)
            {
                try { Capture(); }
                catch (Exception e) { Log.D("spectrum: " + e.Message); }
                if (Environment.TickCount64 - lastUse < 3000) Thread.Sleep(1000);   // device gone/changed: try again
            }
        }
        finally
        {
            lock (bands) Array.Clear(bands);
            lock (gate) thread = null;
        }
    }

    static void Capture()
    {
        var en = (IMMDeviceEnumerator)Mixer.NewEnumerator();
        IMMDevice? dev = null; IAudioClient? client = null; IAudioCaptureClient? cap = null;
        IntPtr fmtPtr = IntPtr.Zero;
        try
        {
            if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return;
            dev.GetId(out var devId);
            var iid = typeof(IAudioClient).GUID;
            if (dev.Activate(ref iid, 23, IntPtr.Zero, out var o) != 0) return;
            client = (IAudioClient)o;
            if (client.GetMixFormat(out fmtPtr) != 0) return;
            var fmt = Marshal.PtrToStructure<WAVEFORMATEX>(fmtPtr);
            bool isFloat = fmt.wFormatTag == 3 || (fmt.wFormatTag == 0xFFFE && Marshal.PtrToStructure<Guid>(fmtPtr + 24) == new Guid("00000003-0000-0010-8000-00aa00389b71"));
            int ch = fmt.nChannels, bits = fmt.wBitsPerSample, rate = (int)fmt.nSamplesPerSec;
            if (!(isFloat && bits == 32) && bits != 16) return;
            if (client.Initialize(0, 0x00020000, 2_000_000, 0, fmtPtr, IntPtr.Zero) != 0) return;   // shared, loopback, 200 ms
            var iidCap = typeof(IAudioCaptureClient).GUID;
            if (client.GetService(ref iidCap, out var co) != 0) return;
            cap = (IAudioCaptureClient)co;
            client.Start();
            var re = new float[N]; var im = new float[N]; var window = new float[N];
            for (int i = 0; i < N; i++) window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (N - 1));
            long lastFft = 0, checkDev = Environment.TickCount64;
            while (Environment.TickCount64 - lastUse < 3000)
            {
                Thread.Sleep(15);
                while (cap.GetNextPacketSize(out var packet) == 0 && packet > 0)
                {
                    if (cap.GetBuffer(out var data, out var frames, out var flags, out _, out _) != 0) break;
                    bool silent = (flags & 2) != 0;
                    unsafe
                    {
                        for (int f = 0; f < frames; f++)
                        {
                            float s = 0;
                            if (!silent)
                            {
                                if (isFloat) { var p = (float*)data + f * ch; for (int c = 0; c < ch; c++) s += p[c]; }
                                else { var p = (short*)data + f * ch; for (int c = 0; c < ch; c++) s += p[c] / 32768f; }
                                s /= ch;
                            }
                            ring[ringPos] = s; ringPos = (ringPos + 1) & (N - 1);
                        }
                    }
                    cap.ReleaseBuffer(frames);
                }
                long now = Environment.TickCount64;
                if (now - lastFft >= 33)
                {
                    float dt = Math.Min(0.2f, (now - lastFft) / 1000f);
                    lastFft = now;
                    for (int i = 0; i < N; i++) { re[i] = ring[(ringPos + i) & (N - 1)] * window[i]; im[i] = 0; }
                    Fft(re, im);
                    bool loud = false;
                    lock (bands)
                        for (int b = 0; b < Base; b++)
                        {
                            // log-spaced from 40 Hz to 16 kHz
                            double f0 = 40 * Math.Pow(400, b / (double)Base), f1 = 40 * Math.Pow(400, (b + 1) / (double)Base);
                            int k0 = Math.Max(1, (int)(f0 * N / rate)), k1 = Math.Max(k0 + 1, (int)(f1 * N / rate));
                            float m = 0;
                            for (int k = k0; k < Math.Min(N / 2, k1); k++) m = Math.Max(m, re[k] * re[k] + im[k] * im[k]);
                            float db = 10 * MathF.Log10(m + 1e-12f);                  // power in dB
                            float v = Math.Clamp((db + 20 - (b * 0.25f)) / 50f, 0, 1);   // ~-70..-20 dB; highs lifted a little
                            bands[b] = v > bands[b] ? v * 0.7f + bands[b] * 0.3f : Math.Max(v, bands[b] - dt * 1.8f);
                            if (bands[b] > 0.03f) loud = true;
                        }
                    if (loud) lastLoud = now;
                }
                if (now - checkDev > 3000)
                {
                    checkDev = now;
                    if (en.GetDefaultAudioEndpoint(0, 1, out var d2) == 0 && d2 != null)
                    {
                        d2.GetId(out var id2); Marshal.ReleaseComObject(d2);
                        if (id2 != devId) break;   // the output changed: start again on the new one
                    }
                }
            }
            client.Stop();
        }
        finally
        {
            if (fmtPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(fmtPtr);
            if (cap != null) Marshal.ReleaseComObject(cap);
            if (client != null) Marshal.ReleaseComObject(client);
            if (dev != null) Marshal.ReleaseComObject(dev);
            Marshal.ReleaseComObject(en);
        }
    }

    static void Fft(float[] re, float[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            float ang = -2 * MathF.PI / len;
            float wr = MathF.Cos(ang), wi = MathF.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                float cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = a + len / 2;
                    float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti;
                    re[a] += tr; im[a] += ti;
                    float nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long def, out long min);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr h);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out int frames, out int flags, out long devicePos, out long qpcPos);
        [PreserveSig] int ReleaseBuffer(int frames);
        [PreserveSig] int GetNextPacketSize(out int frames);
    }
}
