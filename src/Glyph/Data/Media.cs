using Windows.Media.Control;

namespace Glyph.Data;

/// What's playing (Spotify, browsers, media players…) via Windows' media sessions, plus the
/// play/pause/next/previous controls. Starts listening only when something shows media.
internal static class Media
{
    static GlobalSystemMediaTransportControlsSessionManager? manager;
    static GlobalSystemMediaTransportControlsSession? session;
    static volatile bool started;
    static readonly object Gate = new();

    public static volatile string Title = "", Artist = "", Album = "", App = "";
    public static volatile bool Playing, HasSession;
    static double posSeconds, lenSeconds; static DateTime posAt;
    /// Album art (or null), and a counter that changes when it does.
    public static volatile Bitmap? Art;
    public static volatile int ArtVersion, Version;

    public static void Use()
    {
        if (started) return;
        lock (Gate)
        {
            if (started) return;
            started = true;
            Task.Run(async () =>
            {
                try
                {
                    manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    manager.CurrentSessionChanged += (_, _) => Attach();
                    Attach();
                }
                catch (Exception e) { Log.Write("media: " + e.Message); }
            });
        }
    }

    static void Attach()
    {
        try
        {
            var s = manager?.GetCurrentSession();
            if (session != null && !ReferenceEquals(s, session))
            {
                session.MediaPropertiesChanged -= OnProps; session.PlaybackInfoChanged -= OnPlayback; session.TimelinePropertiesChanged -= OnTimeline;
            }
            session = s;
            HasSession = s != null;
            if (s == null) { Title = Artist = Album = App = ""; Playing = false; Art = null; ArtVersion++; Version++; return; }
            App = s.SourceAppUserModelId ?? "";
            s.MediaPropertiesChanged += OnProps; s.PlaybackInfoChanged += OnPlayback; s.TimelinePropertiesChanged += OnTimeline;
            OnProps(s, null); OnPlayback(s, null); OnTimeline(s, null);
        }
        catch (Exception e) { Log.Write("media attach: " + e.Message); }
    }

    static async void OnProps(GlobalSystemMediaTransportControlsSession s, object? _)
    {
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            Title = p.Title ?? ""; Artist = p.Artist ?? ""; Album = p.AlbumTitle ?? "";
            Version++;
            Bitmap? art = null;
            if (p.Thumbnail != null)
            {
                using var stream = await p.Thumbnail.OpenReadAsync();
                using var net = System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(stream);
                using var ms = new MemoryStream();
                await net.CopyToAsync(ms);
                ms.Position = 0;
                using var img = Image.FromStream(ms);
                art = new Bitmap(img);
            }
            Art = art; ArtVersion++; Version++;
        }
        catch (Exception e) { Log.Write("media props: " + e.Message); }
    }

    static void OnPlayback(GlobalSystemMediaTransportControlsSession s, object? _)
    {
        try { Playing = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing; Version++; } catch { }
    }

    static void OnTimeline(GlobalSystemMediaTransportControlsSession s, object? _)
    {
        try
        {
            var t = s.GetTimelineProperties();
            posSeconds = t.Position.TotalSeconds; lenSeconds = (t.EndTime - t.StartTime).TotalSeconds; posAt = DateTime.UtcNow;
            Version++;
        }
        catch { }
    }

    public static double Length => lenSeconds;
    public static double Position => Playing ? Math.Min(lenSeconds, posSeconds + (DateTime.UtcNow - posAt).TotalSeconds) : posSeconds;
    public static float Progress { get { Use(); return lenSeconds > 0 ? (float)Math.Clamp(Position / lenSeconds, 0, 1) : 0f; } }

    public static string? Value(string name, string spec)
    {
        Use();
        if (!HasSession && name != "media") return "";
        return name switch
        {
            "media" or "media_title" => Fmt.Trunc(Title, spec),
            "media_artist" => Fmt.Trunc(Artist, spec),
            "media_album" => Fmt.Trunc(Album, spec),
            "media_app" => App.Split('!')[0].Replace(".exe", "", StringComparison.OrdinalIgnoreCase),
            "media_state" => Playing ? "playing" : "paused",
            "media_icon" => Playing ? "▶" : "❚❚",
            "media_position" => Fmt.Clock(TimeSpan.FromSeconds(Position)),
            "media_length" => lenSeconds > 0 ? Fmt.Clock(TimeSpan.FromSeconds(lenSeconds)) : "",
            _ => null,
        };
    }

    public static void Control(string what)
    {
        Use();
        var s = session;
        if (s == null) return;
        _ = what switch
        {
            "next" => s.TrySkipNextAsync().AsTask(),
            "prev" or "previous" => s.TrySkipPreviousAsync().AsTask(),
            "play" => s.TryPlayAsync().AsTask(),
            "pause" => s.TryPauseAsync().AsTask(),
            _ => s.TryTogglePlayPauseAsync().AsTask(),
        };
    }
}
