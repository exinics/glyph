using Glyph.Render;

namespace Glyph;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 1 && args[0] == "--finish-update") return Updater.Finish(args);   // before anything else: runs from the download
        if (Relaunched(args)) return 0;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("crash: " + e.ExceptionObject);
        try { DefaultThemes.EnsureExist(); } catch (Exception e) { Log.Write("first-run setup: " + e.Message); }
        try { Updater.InstallShippedThemes(); } catch (Exception e) { Log.Write("shipped themes: " + e.Message); }
        try
        {
            if (args.Length > 0 && args[0] == "--snapshot") return Snapshot(args);
            if (args.Length > 1 && args[0] == "--snapshot-bar") return SnapshotBar(args[1]);
            if (args.Length > 1 && args[0] == "--snapshot-launcher") return SnapshotLauncher(args[1], args.Length > 2 ? args[2] : "");
            if (args.Length > 0 && args[0] == "--write-guide") { Docs.WriteGuide(); return 0; }
            if (args.Length > 0 && args[0] == "--update") return Updater.Command(!args.Contains("--no-start"));
            if (args.Length > 0 && args[0] == "--scan-apps") return Glyph.Launcher.AppScan.Run();
            if (args.Length > 1 && args[0] == "--action")   // e.g. for hotkeys from other programs: the running Glyph does it
            {
                var action = string.Join(" ", args.Skip(1));
                if (!Native.SendAction(action)) Glyph.Data.Actions.Run(action);
                return 0;
            }
            if (args.Length > 1 && args[0] == "--reset-example" && DefaultThemes.Names.Contains(args[1])) { DefaultThemes.Write(args[1]); return 0; }
            if (args.Length > 0 && args[0] == "--convert") return Glyph.Convert.ConvertCommand.Run(args);
            if (args.Length > 0 && args[0] == "--settings")
            {
                using var one = new Mutex(true, @"Local\Glyph.Settings", out bool firstSettings);
                if (!firstSettings) { BringToFront("Glyph settings"); return 0; }
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.Run(new Glyph.UI.SettingsForm(args.ElementAtOrDefault(1)));
                return 0;
            }
            if (args.Length > 0 && (args[0] == "--convert-gui" || File.Exists(args[0])))
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                var file = args[0] == "--convert-gui" ? args.ElementAtOrDefault(1) : args[0];
                Application.Run(new Glyph.Convert.ConvertForm(file));
                return 0;
            }
        }
        catch (Exception e) { Log.Write("command failed: " + e); Console.Error.WriteLine("error: " + e.Message); return 1; }

        using var mutex = new Mutex(true, @"Local\Glyph.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("Glyph is already running. Look for the G icon in the system tray.", "Glyph");
            return 0;
        }
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.ThreadException += (_, e) => Log.Write("ui error: " + e.Exception);
        Log.Write($"start, home = {Paths.Home}");
        Application.Run(new App());
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

    static void BringToFront(string title)
    {
        var h = Native.FindWindow(null, title);
        if (h != IntPtr.Zero) SetForegroundWindow(h);
    }

    /// The .NET memory manager keeps room for new objects sized by the CPU's cache (4 MB here), and only reads a
    /// smaller size from the environment, before the program starts. So the desktop and the settings window start
    /// themselves once more with it set: about 4 MB less memory for the whole time Glyph runs.
    static bool Relaunched(string[] args)
    {
        const string Var = "DOTNET_GCgen0size";
        if (Environment.GetEnvironmentVariable(Var) != null) return false;
        if (args.Length > 0 && args[0] != "--settings") return false;   // only the long-running parts
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment[Var] = "0x100000";
            using var p = System.Diagnostics.Process.Start(psi);
            return p != null;
        }
        catch { return false; }
    }

    /// Glyph.exe --snapshot-bar out.png: renders the theme's bar off screen (see-through parts are black).
    static int SnapshotBar(string outPath)
    {
        using var comp = new Composer();
        comp.Load();
        var bar = new Glyph.Bar.BarScene();
        bar.Load(comp.Theme, comp.Problems);
        int w = 1920 - bar.O.Margin * 2, h = bar.O.Height;
        using var r = new Renderer(IntPtr.Zero, w, h);
        r.SetFont(bar.BarFont());
        var a = r.Atlas!;
        var grid = new Grid();
        grid.Resize((w + a.CellW - 1) / a.CellW, (h + a.CellH - 1) / a.CellH);
        bar.Hover = 1;
        bar.Signature(a.CellW, a.CellH, r.Canvas);
        bar.Layout(w, h, a.CellW, a.CellH, r.Canvas);
        bar.PaintCells(grid);
        r.Draw(grid, new EffectSettings { Enabled = false }, 0, bar.ClearColor, null, bar.PaintVector);
        using var bmp = r.ReadBack();
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        File.WriteAllText(outPath + ".txt", string.Join(Environment.NewLine, comp.Problems.Select(p => "problem: " + p)));
        return 0;
    }

    /// Glyph.exe --snapshot-launcher out.png [query]: draws the theme's launcher off screen with some made-up results.
    static int SnapshotLauncher(string outPath, string query)
    {
        using var comp = new Composer();
        comp.Load();
        var scene = new Glyph.Launcher.LauncherScene { Theme = comp.Theme, O = comp.Theme.Options.Launcher };
        var st = new Glyph.Launcher.LauncherState { Query = query, Selected = 0 };
        foreach (var (t, k) in new[] { ("Steam", "app"), ("Aseprite", "app"), ("Spotify", "app"), ("Bluetooth settings", "app"), ("Calculator", "app"), ("Calendar", "app"), ("Visual Studio Code", "app"), ("Firefox", "app"), ("Downloads", "folder"), ("Theme: soft", "theme"), ("Tiling: on", "glyph") })
            st.Results.Add(new Glyph.Launcher.Item(t, t, k, "", ""));
        int w = scene.O.Width + 12, h = 1100;
        using var r = new Renderer(IntPtr.Zero, w, h);
        r.SetFont(comp.Theme.Options.Font);
        var grid = new Grid();
        grid.Resize(Math.Max(1, w / r.Atlas!.CellW), Math.Max(1, h / r.Atlas.CellH));
        scene.PaintCells(grid, st, r.Atlas.CellW, r.Atlas.CellH);
        r.Draw(grid, new EffectSettings { Enabled = false }, 0, new Rgba(0, 0, 0, 0), null, c => scene.PaintVector(c, st, w));
        using var bmp = r.ReadBack();
        using var cut = bmp.Clone(new Rectangle(0, 0, w, Math.Min(h, (int)scene.ContentHeight + 2)), bmp.PixelFormat);
        cut.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        return 0;
    }

    /// Glyph.exe --snapshot out.png [--seconds 3] [--size 1920x1200]
    /// Renders the configured wallpaper off screen and saves the last frame (for testing and previews).
    static int Snapshot(string[] args)
    {
        string outPath = args.Length > 1 ? args[1] : "snapshot.png";
        float seconds = 3; int w = 1920, h = 1200;
        float cx = -1e6f, cy = -1e6f;
        string? themeName = null; bool withBar = args.Contains("--bar");
        for (int i = 2; i < args.Length - 1; i++)
        {
            if (args[i] == "--theme") themeName = args[i + 1];
            if (args[i] == "--seconds") seconds = float.Parse(args[i + 1]);
            if (args[i] == "--size") { var p = args[i + 1].Split('x'); w = int.Parse(p[0]); h = int.Parse(p[1]); }
            if (args[i] == "--cursor") { var p = args[i + 1].Split(','); cx = float.Parse(p[0]); cy = float.Parse(p[1]); }
        }
        using var comp = new Composer();
        comp.Load(themeName);
        Glyph.Bar.BarScene? bar = null;
        RectangleF? area = null;
        if (withBar)
        {
            bar = new Glyph.Bar.BarScene();
            bar.Load(comp.Theme, comp.Problems);
            if (!bar.Enabled) bar = null;
            else if (bar.O.ReserveSpace) area = bar.O.Position == "bottom" ? new RectangleF(0, 0, w, h - bar.O.Height - bar.O.Margin * 2) : new RectangleF(0, bar.O.Height + bar.O.Margin * 2, w, h - bar.O.Height - bar.O.Margin * 2);
        }
        using var r = new Renderer(IntPtr.Zero, w, h);
        var fp = r.SetFont(comp.Font);
        if (fp != null) comp.RenderProblems.Add(fp);
        comp.Scene.Profile = true;
        int fps = comp.Scene.Fps;
        int frames = Math.Max(1, (int)(seconds * fps));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double composeMs = 0;
        for (int i = 0; i < frames; i++)
        {
            var t0 = sw.Elapsed.TotalMilliseconds;
            comp.Env.Canvas = r.Canvas;
            comp.Compose(w, h, r.Atlas!.CellW, r.Atlas.CellH, (i + 1) / (float)fps, 1f / fps, cx, cy, area);
            composeMs += sw.Elapsed.TotalMilliseconds - t0;
            r.Draw(comp.Grid, comp.Effects, (i + 1) / (float)fps, comp.ClearColor, comp.HasBackdrop ? comp.DrawBackdrop : null, comp.Widgets.Count > 0 ? comp.DrawWidgets : null);
            r.Present();
        }
        using var bmp = r.ReadBack();
        if (bar != null)
        {
            int bw = w - bar.O.Margin * 2, bh = bar.O.Height;
            using var br = new Renderer(IntPtr.Zero, bw, bh);
            br.SetFont(bar.BarFont());
            var a = br.Atlas!;
            var grid = new Grid();
            grid.Resize((bw + a.CellW - 1) / a.CellW, (bh + a.CellH - 1) / a.CellH);
            bar.Signature(a.CellW, a.CellH, br.Canvas);
            bar.Layout(bw, bh, a.CellW, a.CellH, br.Canvas);
            bar.PaintCells(grid);
            br.Draw(grid, new EffectSettings { Enabled = false }, 0, bar.ClearColor, null, bar.PaintVector);
            using var bb = br.ReadBack();
            using var g = Graphics.FromImage(bmp);
            g.DrawImage(bb, bar.O.Margin, bar.O.Position == "bottom" ? h - bh - bar.O.Margin : bar.O.Margin);
        }
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
        var report = $"saved {outPath}: {w}x{h}, grid {comp.Grid.W}x{comp.Grid.H}, cell {r.Atlas!.CellW}x{r.Atlas.CellH}, {frames} frames, compose {composeMs / frames:0.00} ms/frame, total {sw.Elapsed.TotalMilliseconds / frames:0.00} ms/frame";
        foreach (var kv in comp.Scene.Timing) report += $"\n  {kv.Key}: {kv.Value / frames:0.000} ms/frame";
        foreach (var p in comp.Problems.Concat(comp.RenderProblems)) report += "\nproblem: " + p;
        File.WriteAllText(outPath + ".txt", report);
        return 0;
    }
}
