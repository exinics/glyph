using System.Diagnostics;
using System.Runtime.InteropServices;
using Glyph.Desktop;
using Microsoft.Win32;

namespace Glyph;

/// The tray app: owns the desktop window, the render thread, config watching and the tray menu.
/// The settings window and the converter run as separate programs, so nothing of them stays
/// in memory while you just use the desktop.
internal sealed class App : ApplicationContext
{
    readonly DesktopHost host = new();
    readonly Engine engine;
    readonly NotifyIcon tray;
    readonly ToolStripMenuItem pauseItem;
    readonly FileSystemWatcher watcher;
    readonly System.Windows.Forms.Timer checkTimer = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer activityTimer = new() { Interval = 500 };
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 250 };
    readonly ShellListener shell;
    readonly Control ui = new();   // hidden control used to run things on the UI thread
    IntPtr iconHandle;
    readonly ToolStripMenuItem updateItem;
    readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 30_000 };
    readonly Dictionary<int, (Glyph.Bar.BarWindow win, string key)> bars = new();
    string screensKey = Glyph.Desktop.Screens.Key();
    readonly Glyph.Tiling.TileManager tiles;
    readonly Glyph.Desktop.WindowFrames frames;
    readonly Glyph.Launcher.LauncherHost launcher;
    readonly ToolStripMenuItem tilingItem;

    public App()
    {
        ui.CreateControl();
        _ = ui.Handle;
        engine = new Engine(host);
        var s = AppSettings.Load(new List<string>());
        if (s.CloseWallpaperEngine) CloseWallpaperEngine();
        ApplyAutostart(s.StartWithWindows);
        DesktopIcons.Apply(s.HideDesktopIcons);
        Taskbar.Apply(s.Taskbar);

        host.Attach();
        engine.Reloaded += () => { try { ui.BeginInvoke(OnEngineReloaded); } catch (Exception e) { Log.Write("reload notify: " + e.Message); } };
        engine.Launch += sc => { try { ui.BeginInvoke(() => sc.Launch()); } catch (Exception e) { Log.Write("launch: " + e.Message); } };
        engine.DoAction += a => { try { ui.BeginInvoke(() => Glyph.Data.Actions.Run(a)); } catch (Exception e) { Log.Write("action: " + e.Message); } };
        Glyph.Data.Actions.Internal = Internal;
        tiles = new Glyph.Tiling.TileManager(() => engine.CurrentTheme);
        frames = new Glyph.Desktop.WindowFrames(() => engine.CurrentTheme, () => tiles.TiledWindows);
        tiles.Retiled += frames.Kick;
        launcher = new Glyph.Launcher.LauncherHost(ui, RunItem, engine.Wake);
        launcher.Search.Commands = LauncherCommands;
        engine.Launcher = launcher;
        engine.Start();

        pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Glyph") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => RunSelf("--settings")) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add("Convert a video…", null, (_, _) => RunSelf("--convert-gui"));
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Launcher", null, (_, _) => launcher.Show());
        tilingItem = new ToolStripMenuItem("Tiling", null, (_, _) => SetTiling(!tiles.Enabled));
        menu.Items.Add(tilingItem);
        menu.Opening += (_, _) => { tilingItem.Checked = tiles.Enabled; pauseItem.Text = engine.UserPaused ? "Resume" : "Pause"; };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reload", null, (_, _) => engine.RequestReload());
        menu.Items.Add("Open config folder", null, (_, _) => Open(Paths.Config));
        menu.Items.Add("Open guide", null, (_, _) => Open(Paths.Guide));
        menu.Items.Add("Open log", null, (_, _) => Open(Path.Combine(Paths.Logs, "glyph.log")));
        menu.Items.Add(new ToolStripSeparator());
        updateItem = new ToolStripMenuItem("Update", null, async (_, _) => await InstallUpdate()) { Visible = false, Font = new Font(menu.Font, FontStyle.Bold) };
        menu.Items.Add(updateItem);
        menu.Items.Add($"Check for updates (this is {Updater.CurrentText})", null, (_, _) => CheckUpdates(true));
        menu.Items.Add("Quit Glyph", null, (_, _) => Quit());

        tray = new NotifyIcon { Text = "Glyph", ContextMenuStrip = menu, Visible = true };
        SetIcon(s.Theme);
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) RunSelf("--settings"); };
        tray.BalloonTipClicked += async (_, _) => { if (pendingUpdate != null && !updating) await InstallUpdate(); };
        // updates: first look 30 s after starting, then every 6 hours
        updateTimer.Tick += (_, _) => { updateTimer.Interval = 6 * 3600 * 1000; CheckUpdates(false); };
        updateTimer.Start();

        watcher = new FileSystemWatcher(Paths.Config) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.DirectoryName };
        watcher.Changed += OnFileEvent; watcher.Created += OnFileEvent; watcher.Renamed += OnFileEvent; watcher.Deleted += OnFileEvent;
        watcher.EnableRaisingEvents = true;
        debounce.Tick += (_, _) => { debounce.Stop(); engine.RequestReload(); };

        checkTimer.Tick += (_, _) =>
        {
            // a display added, removed or moved: work out again what goes on each screen
            var k = Glyph.Desktop.Screens.Key();
            if (k != screensKey) { screensKey = k; Log.Write("displays changed"); engine.RequestReload(); }
            host.Check(); UpdateTooltip(); Taskbar.Enforce(engine.CurrentSettings.Taskbar);
        };
        checkTimer.Start();
        activityTimer.Tick += (_, _) =>
        {
            var perf = engine.CurrentSettings.Performance;
            engine.PauseReason = Glyph.Desktop.Activity.Reason(perf, out var mode);
            engine.Covered = Glyph.Desktop.Activity.Covered(perf);
            if (perf.Preset(mode) is { } preset && (preset != Perf.Current || mode != Perf.Name))
            {
                Perf.Current = preset; Perf.Name = mode;
                engine.Redraw();   // redraw now with the new preset
            }
        };
        activityTimer.Start();

        // the settings window asks Glyph to quit through this event (so it can tidy up first)
        var quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Glyph.Quit");
        ThreadPool.RegisterWaitForSingleObject(quitEvent, (_, _) => { try { ui.BeginInvoke(Quit); } catch { } }, null, -1, true);

        SystemEvents.SessionSwitch += OnSessionSwitch;
        shell = new ShellListener(
            onRestart: () => { if (Glyph.Data.Tray.OwnBroadcast) return; Log.Write("explorer restarted; reattaching"); host.Attach(); DesktopIcons.Apply(engine.CurrentSettings.HideDesktopIcons); },
            onClick: (x, y, down) =>
            {
                bool desk = Engine.DesktopAt(x, y);
                Log.D($"button {(down ? "down" : "up")} at {x},{y}, desktop under it: {desk} ({Native.ClassOf(Native.WindowFromPoint(new Native.POINT { X = x, Y = y }))})");
                if (desk) engine.PostClick(x, y, down);
            },
            onWheel: (x, y, d) => { if (Engine.DesktopAt(x, y)) engine.PostWheel(x, y, d); },
            onHotkey: id => { if (Hotkeys.ActionFor(id) is string a) Glyph.Data.Actions.Run(a); },
            onMessage: a => Glyph.Data.Actions.Run(a));
    }

    // ───────────── launcher, tiling and other actions that need the running Glyph ─────────────

    /// launcher[:text] | tile:… | theme:name | glyph:reload|pause|quit|convert | shortcut:target | copy:text | runas:target | window:close|minimize|maximize
    bool Internal(string kind, string arg)
    {
        switch (kind)
        {
            case "launcher": if (launcher.Visible) launcher.Hide(); else launcher.Show(arg); return true;
            case "tile": tiles.Command(arg, SetTiling); return true;
            case "theme": SetTheme(arg); return true;
            case "glyph":
                switch (arg)
                {
                    case "reload": engine.RequestReload(); break;
                    case "pause": TogglePause(); break;
                    case "quit": Quit(); break;
                    case "convert": RunSelf("--convert-gui"); break;
                    case "update": CheckUpdates(true); break;
                }
                return true;
            case "shortcut":
                {
                    var sc = Glyph.Widgets.ShortcutStore.Load(new List<string>()).FirstOrDefault(x => string.Equals(x.Target, arg, StringComparison.OrdinalIgnoreCase));
                    if (sc != null) sc.Launch(); else Open(arg);
                    return true;
                }
            case "task":
                Glyph.Data.Tasks.Toggle(arg.Split(',').Select(x => long.TryParse(x, out var v) ? new IntPtr(v) : IntPtr.Zero).Where(x => x != IntPtr.Zero).ToList());
                return true;
            case "taskop":
                {
                    var bits = arg.Split(':', 2);
                    if (bits.Length == 2) Glyph.Data.Tasks.Do(bits[0], bits[1].Split(',').Select(x => long.TryParse(x, out var v) ? new IntPtr(v) : IntPtr.Zero).Where(x => x != IntPtr.Zero).ToList());
                    return true;
                }
            case "trayicon":
                {
                    int bar = arg.LastIndexOf('|');
                    if (bar > 0) Glyph.Data.Tray.Click(arg[..bar], arg[(bar + 1)..]);
                    return true;
                }
            case "copy": try { Clipboard.SetText(arg); } catch (Exception e) { Log.Write("copy: " + e.Message); } return true;
            case "runas":
                try { Process.Start(new ProcessStartInfo(arg) { UseShellExecute = true, Verb = "runas" }); }
                catch (Exception e) { Log.Write($"run as administrator '{arg}': {e.Message}"); }
                return true;
            case "window":
                {
                    var fg = Native.GetForegroundWindow();
                    if (fg == IntPtr.Zero) return true;
                    if (arg == "close") Native.PostMessage(fg, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                    else if (arg is "minimize" or "minimise") Native.ShowWindow(fg, 6);
                    else if (arg is "maximize" or "maximise") Native.ShowWindow(fg, Native.IsZoomed(fg) ? 9 : 3);
                    return true;
                }
        }
        return false;
    }

    void RunItem(Glyph.Launcher.Item it, bool admin)
    {
        var a = it.Action;
        if (admin && (it.Kind is "app" or "shortcut" or "file"))
        {
            var target = a.StartsWith("shortcut:") ? a[9..] : a;
            Glyph.Data.Actions.Run("runas:" + target);
            return;
        }
        Glyph.Data.Actions.Run(a);
    }

    void SetTiling(bool on)
    {
        try
        {
            var s = AppSettings.Load(new List<string>());
            s.Tiling.Enabled = on;
            s.Save();   // the reload that follows applies it too; this makes it instant
            tiles.Apply(s.Tiling);
        }
        catch (Exception e) { Log.Write("tiling: " + e.Message); }
    }

    void SetTheme(string name)
    {
        if (!Directory.Exists(Paths.ThemeDir(name))) { Log.Write($"theme '{name}' not found"); return; }
        try { var s = AppSettings.Load(new List<string>()); s.Theme = name; s.Save(); }
        catch (Exception e) { Log.Write("theme: " + e.Message); }
    }

    List<Glyph.Launcher.Item> LauncherCommands()
    {
        var l = new List<Glyph.Launcher.Item>
        {
            new("cmd:settings", "Glyph settings", "glyph", "settings"),
            new("cmd:reload", "Reload Glyph", "glyph", "glyph:reload"),
            new("cmd:pause", engine.UserPaused ? "Resume Glyph" : "Pause Glyph", "glyph", "glyph:pause"),
            new("cmd:convert", "Convert a video to ASCII art", "glyph", "glyph:convert"),
            new("cmd:config", "Open Glyph's config folder", "glyph", Paths.Config),
            new("cmd:guide", "Open Glyph's guide", "glyph", Paths.Guide),
            new("cmd:quit", "Quit Glyph", "glyph", "glyph:quit"),
            new("cmd:tiling", tiles.Enabled ? "Turn tiling off" : "Turn tiling on", "tiling", "tile:toggle"),
            new("cmd:mute", "Mute or unmute the sound", "sound", "volume:mute"),
            new("cmd:mixer", "Volume mixer", "sound", "audio:mixer"),
            new("cmd:playpause", "Play / pause music", "media", "media:toggle"),
            new("cmd:next", "Next track", "media", "media:next"),
            new("cmd:lock", "Lock the PC", "windows", "run:rundll32.exe user32.dll,LockWorkStation"),
            new("cmd:winsettings", "Windows settings", "windows", "ms-settings:"),
        };
        foreach (var lay in new[] { "dwindle", "master", "columns", "rows", "monocle" })
            l.Add(new($"cmd:layout:{lay}", $"Tiling layout: {lay}", "tiling", $"tile:layout:{lay}"));
        try
        {
            foreach (var d in Directory.GetDirectories(Paths.Themes))
            {
                var n = Path.GetFileName(d);
                if (File.Exists(Path.Combine(d, "theme.toml"))) l.Add(new($"cmd:theme:{n}", $"Theme: {n}", "theme", $"theme:{n}"));
            }
        }
        catch { }
        try { foreach (var d in Glyph.Data.Mixer.Read().Devices) l.Add(new($"cmd:out:{d.Name}", $"Sound output: {d.Name}", d.IsDefault ? "in use" : "sound", $"audio:device:{d.Id}")); }
        catch { }
        return l;
    }

    static void RunSelf(string arg)
    {
        try { Process.Start(new ProcessStartInfo(Paths.Exe, arg) { UseShellExecute = false }); }
        catch (Exception e) { Log.Write($"start {arg}: {e.Message}"); }
    }

    void OnFileEvent(object? sender, FileSystemEventArgs e)
    {
        var n = e.Name ?? "";
        if (n.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || n.EndsWith("~") || n.EndsWith(".tmp") || n.EndsWith(".part") || Path.GetFileName(n).StartsWith(".")
            || n.StartsWith("icons\\cache", StringComparison.OrdinalIgnoreCase))
            return;
        try { ui.BeginInvoke(() => { debounce.Stop(); debounce.Start(); }); } catch (Exception ex) { Log.Write("watch: " + ex.Message); }
    }

    void OnEngineReloaded()
    {
        ThemeSync.Apply(engine.CurrentTheme, engine.CurrentSettings.ThemeSync);
        if (tray == null) return;
        var s = engine.CurrentSettings;
        ApplyAutostart(s.StartWithWindows);
        if (s.CloseWallpaperEngine) CloseWallpaperEngine();
        DesktopIcons.Apply(s.HideDesktopIcons);
        Taskbar.Apply(s.Taskbar);
        SetIcon(s.Theme);
        var plan = engine.Plan;
        host.SetScreens(plan.Select(p => p.screen).ToList());
        ApplyBars(plan);
        launcher.Search.S = s.Launcher;
        frames.Apply(engine.CurrentTheme);
        tiles.Apply(s.Tiling);
        Hotkeys.Apply(shell.Handle, HotkeyStore.Load(new List<string>()));
    }

    /// Creates, updates or removes each screen's bar window to match its theme.
    void ApplyBars(IReadOnlyList<(Glyph.Desktop.ScreenInfo screen, Glyph.Bar.BarOptions? bar)> plan)
    {
        try
        {
            foreach (var n in bars.Keys.ToList())
                if (!plan.Any(p => p.screen.Number == n && p.bar is { Enabled: true })) { bars[n].win.Dispose(); bars.Remove(n); }
            foreach (var (screen, o) in plan)
            {
                if (o == null || !o.Enabled) continue;
                var sb = new System.Text.StringBuilder();
                Glyph.Options.Binder.WriteValues(sb, o);
                float sc = Glyph.Desktop.Screens.Use(screen.Scale);
                var key = sb + "|" + screen.Bounds + "|" + sc;
                int n = screen.Number;
                if (!bars.TryGetValue(n, out var b))
                {
                    var w = new Glyph.Bar.BarWindow(o, screen.Bounds, sc);
                    w.Mouse += (k, x, y, d) => engine.PostBarMouse(n, k, x, y, d);
                    bars[n] = (w, key);
                }
                else if (key != b.key) { b.win.Apply(o, screen.Bounds, sc); bars[n] = (b.win, key); }
            }
            engine.BarSources = bars.ToDictionary(kv => kv.Key, kv => { var w = kv.Value.win; return (Func<(IntPtr, int, int, int)>)(() => w.Current); });
        }
        catch (Exception e) { Log.Write("bar: " + e); }
    }

    void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock) Glyph.Desktop.Activity.Locked = true;
        if (e.Reason == SessionSwitchReason.SessionUnlock) Glyph.Desktop.Activity.Locked = false;
    }

    void TogglePause()
    {
        engine.UserPaused = !engine.UserPaused;
        pauseItem.Text = engine.UserPaused ? "Resume" : "Pause";
        UpdateTooltip();
    }

    void UpdateTooltip()
    {
        string state = engine.UserPaused ? "paused" : engine.PauseReason.Length > 0 ? $"resting ({engine.PauseReason})" : engine.Fps < 0.5f ? "idle (nothing moving)" : $"{engine.Fps:0} fps, {Perf.Name}";
        var t = $"Glyph: {state}";
        tray.Text = t.Length > 63 ? t[..63] : t;
    }

    /// The tray shows the app's own icon (the G).
    void SetIcon(string themeName)
    {
        if (tray.Icon != null) return;
        try { tray.Icon = Updater.AppIcon(SystemInformation.SmallIconSize.Width); }
        catch (Exception e) { Log.Write("icon: " + e.Message); }
    }

    // ───────────── updates ─────────────

    Updater.Release? pendingUpdate;
    bool updating;

    async void CheckUpdates(bool manual)
    {
        var s = AppSettings.Load(new List<string>());
        if (!manual && s.Updates == "off") return;
        try
        {
            var r = await Updater.Check(s.UpdateRepo);
            if (r == null)
            {
                if (manual) tray.ShowBalloonTip(4000, "Glyph is up to date", $"You have the newest version ({Updater.CurrentText}).", ToolTipIcon.Info);
                return;
            }
            pendingUpdate = r;
            updateItem.Text = $"Update to {r.Tag}";
            updateItem.Visible = true;
            Log.Write($"update available: {r.Tag} (this is {Updater.CurrentText})");
            if (s.Updates == "auto" && !manual) await InstallUpdate();
            else tray.ShowBalloonTip(6000, $"Glyph {r.Tag} is available", "Click here (or Update in the tray menu) to update. Glyph restarts by itself.", ToolTipIcon.Info);
        }
        catch (Exception e)
        {
            Log.Write("update check: " + e.Message);
            if (manual) tray.ShowBalloonTip(4000, "Couldn't check for updates", e.Message, ToolTipIcon.Warning);
        }
    }

    async Task InstallUpdate()
    {
        if (pendingUpdate is not { } r || updating) return;
        updating = true;
        try
        {
            tray.ShowBalloonTip(4000, $"Updating Glyph to {r.Tag}", "It restarts by itself in a moment.", ToolTipIcon.Info);
            var dir = await Updater.Download(r);
            Log.Write($"updating to {r.Tag} from {dir}");
            Updater.Apply(dir);
            Quit();
        }
        catch (Exception e)
        {
            updating = false;
            Log.Write("update: " + e.Message);
            tray.ShowBalloonTip(5000, "Update failed", e.Message, ToolTipIcon.Warning);
        }
    }

    static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { Log.Write($"open {path}: {e.Message}"); }
    }

    static void CloseWallpaperEngine()
    {
        foreach (var name in new[] { "wallpaper64", "wallpaper32", "webwallpaper32" })
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(); p.WaitForExit(2000); Log.Write($"closed Wallpaper Engine ({name})"); }
                catch (Exception e) { Log.Write($"couldn't close {name}: {e.Message}"); }
            }
    }

    static void ApplyAutostart(bool on)
    {
        if (Environment.GetEnvironmentVariable("GLYPH_DEV") == "1") return;   // a test build: leave the real Glyph's autostart alone
        Glyph.Desktop.Autostart.Apply(on);
        Glyph.Desktop.Autostart.RememberHome();
    }

    void Quit()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        checkTimer.Stop(); activityTimer.Stop();
        watcher.Dispose();
        engine.Stop();
        host.Dispose();
        engine.BarSources = new Dictionary<int, Func<(IntPtr, int, int, int)>>();
        foreach (var b in bars.Values) b.win.Dispose();
        bars.Clear();
        engine.Launcher = null;
        launcher.Dispose();
        tiles.Dispose();
        frames.Dispose();
        Hotkeys.Clear();
        DesktopIcons.Restore();
        Taskbar.Restore();
        Glyph.Data.Tray.Stop();
        shell.DestroyHandle();
        tray.Visible = false;
        tray.Dispose();
        updateTimer.Stop();
        if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
        Log.Write("quit");
        ExitThread();
    }

    /// Hidden top-level window that hears when Explorer restarts (TaskbarCreated) and receives
    /// mouse button events from anywhere (raw input), so desktop widgets can be clicked.
    sealed class ShellListener : NativeWindow
    {
        readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        readonly Action onRestart;
        readonly Action<int, int, bool> onClick;
        readonly Action<int, int, int> onWheel;
        readonly Action<int> onHotkey;
        readonly Action<string> onMessage;

        public ShellListener(Action onRestart, Action<int, int, bool> onClick, Action<int, int, int> onWheel, Action<int> onHotkey, Action<string> onMessage)
        {
            this.onRestart = onRestart; this.onClick = onClick; this.onWheel = onWheel; this.onHotkey = onHotkey; this.onMessage = onMessage;
            CreateHandle(new CreateParams { Caption = "Glyph shell listener", Style = 0, ExStyle = Native.WS_EX_TOOLWINDOW });
            var dev = new[] { new Native.RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = Native.RIDEV_INPUTSINK, Target = Handle } };
            if (!Native.RegisterRawInputDevices(dev, 1, (uint)Marshal.SizeOf<Native.RAWINPUTDEVICE>())) Log.Write("raw input: couldn't register (widget clicks won't work)");
        }

        // runs once the sending program's SendMessage has returned
        void Later(string a) { var t = new System.Windows.Forms.Timer { Interval = 1 }; t.Tick += (_, _) => { t.Stop(); t.Dispose(); onMessage(a); }; t.Start(); }

        unsafe protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)taskbarCreated) onRestart();
            else if (m.Msg == Hotkeys.WM_HOTKEY) { onHotkey((int)m.WParam); return; }
            else if (m.Msg == 0x004A)   // WM_COPYDATA: an action from "Glyph.exe --action ..."
            {
                var cds = Marshal.PtrToStructure<Native.COPYDATASTRUCT>(m.LParam);
                if (cds.dwData == (IntPtr)0x474C && cds.lpData != IntPtr.Zero && cds.cbData > 0)
                {
                    var text = Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2);
                    Later(text);
                }
                m.Result = (IntPtr)1;
                return;
            }
            else if (m.Msg == Native.WM_INPUT)
            {
                uint size = 0;
                uint header = (uint)(8 + IntPtr.Size * 2);
                Native.GetRawInputData(m.LParam, Native.RID_INPUT, IntPtr.Zero, ref size, header);
                if (size > 0 && size <= 128)
                {
                    byte* buf = stackalloc byte[(int)size];
                    if (Native.GetRawInputData(m.LParam, Native.RID_INPUT, (IntPtr)buf, ref size, header) == size && *(uint*)buf == 0 /* RIM_TYPEMOUSE */)
                    {
                        ushort flags = *(ushort*)(buf + header + 4);
                        if ((flags & Native.RI_MOUSE_WHEEL) != 0)
                        {
                            short delta = *(short*)(buf + header + 6);
                            Native.GetCursorPos(out var wp);
                            onWheel(wp.X, wp.Y, delta);
                        }
                        if ((flags & (Native.RI_MOUSE_LEFT_BUTTON_DOWN | Native.RI_MOUSE_LEFT_BUTTON_UP)) != 0)
                        {
                            Native.GetCursorPos(out var p);
                            if ((flags & Native.RI_MOUSE_LEFT_BUTTON_DOWN) != 0) onClick(p.X, p.Y, true);
                            if ((flags & Native.RI_MOUSE_LEFT_BUTTON_UP) != 0) onClick(p.X, p.Y, false);
                        }
                    }
                }
            }
            base.WndProc(ref m);
        }
    }
}
