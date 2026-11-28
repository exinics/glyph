# Glyph

A fully customisable desktop for Windows 11: themes (terminal-style ASCII or smooth panels),
an animated wallpaper behind the desktop icons, shortcut panels that replace the desktop icons,
a settings window, and a video→ASCII converter.

**User guide:** `config\GUIDE.md` (generated: `bin\Glyph.exe --write-guide`).

## Install

1. Download `Glyph-Setup-x.y.z.exe` from [Releases](https://github.com/exinics/glyph/releases) and run it: pick the
   folder (default `%LOCALAPPDATA%\Programs\Glyph`, no administrator needed), Start menu shortcuts (Glyph, Glyph
   settings, Glyph video converter), optional desktop shortcut, "start when I sign in". It fetches the .NET 9 Desktop
   Runtime if the PC hasn't got it, and offers to copy settings from a Glyph used before (registry `HKCU\Software\Glyph`
   `Home`). Uninstall from Windows' Apps list: `config\` (your settings) is kept.
   Or: the zip `Glyph-win-x64.zip` (unzip, run `Glyph\bin\Glyph.exe`; needs `winget install Microsoft.DotNet.DesktopRuntime.9`).
2. For videos: `winget install Gyan.FFmpeg.Essentials`.
3. It updates itself from the newest release (Settings > General > Updates).
4. Start with Windows is a Task Scheduler task "Glyph" (runs `Glyph.exe --autostart` at sign-in, straight away; the
   Run key, which Windows holds back for 30 s to 2 min behind other startup programs, is only the fallback).

## Building and releasing

- `powershell -File build.ps1` builds into `bin\` (and restarts Glyph if it was running).
- `powershell -File release.ps1 [-Version 1.2.0] [-Notes "..."]` bumps the version in `Glyph.csproj`, builds
  `out\Glyph-win-x64.zip` (`Glyph\bin\` with `themes\` inside, plus an empty `Glyph\config\`), commits, tags,
  pushes and creates the GitHub release every Glyph updates from (`Updater.cs`). It also builds the installer
  `out\Glyph-Setup-<version>.exe` from `installer\Glyph.iss` (Inno Setup 6.5+: `winget install JRSoftware.InnoSetup`)
  and attaches it. `-LocalOnly` builds `out\` and the installer without bumping, committing or publishing.
- `build.ps1` only restarts this folder's Glyph; while an installed Glyph exists it restarts it with `GLYPH_DEV=1`
  (then it doesn't take over start with Windows).
- `themes\` holds the theme that ships with Glyph (operator). The build puts it in `bin\themes`; it's copied into
  `config\themes` only when there are no themes yet (first run). Updates never touch themes.
  `config\` itself is personal and not in git (only `config\GUIDE.md` is).

## Layout

```
Glyph\
  bin\                 built program (Glyph.exe); made by build.ps1 (not in git)
  themes\              the theme that ships with Glyph (operator); copied into config\themes on first run only
  config\              everything the user edits (TOML, hot-reloaded)
    glyph.toml           app settings (active theme, startup, performance, hide desktop icons)
    shortcuts.toml       shortcuts, shared by all themes
    themes\<name>\       theme.toml (palette, style, fonts, ui, effects), wallpaper.toml, widgets.toml
    art\ clips\ images\ icons\   shared assets (icons\cache = extracted app icons)
  logs\                glyph.log, state.txt (desktop icons hidden by Glyph), shaders\ (compiled shader cache)
  src\Glyph\           C# source (.NET 9, WinForms, Direct3D 11 + Direct2D via Vortice)
  build.ps1            build into bin\ (restarts Glyph if it was running, even if the build fails)
```

## Processes

- `Glyph.exe`: the desktop (tray app + render thread). Kept lean: ~70 MB private, idle when nothing moves.
- `Glyph.exe --settings [tab]`: the settings window, a separate process; talks to the desktop only through
  the files (and the `Local\Glyph.Quit` event for "Quit Glyph").
- `Glyph.exe --convert-gui [file]` / `--convert <file> [options]`: the converter.
- Updates: `Updater.cs` checks GitHub's latest release, unzips it into `logs\update`, starts the new exe with
  `--finish-update <bin> <pid>` (waits for Glyph to close, copies itself over `bin\`, restarts it). `--update [--no-start]`
  does the same from a terminal.
- Installer helpers: `--quit` (asks the running Glyph to quit and waits), `--start-with-windows on|off`,
  `--no-autostart` (removes the sign-in task only if it starts this copy).
- Selection box (smooth themes): while dragging, `Engine.DesktopStep` keeps a copy of the finished frame
  (`Renderer.KeepStill`) and draws only the box over it (`DrawOver`) at the screen's refresh rate; the desktop under
  it redraws at its own rate.
- Fonts and icons (`Render\Fonts.cs`): `Fonts.Collection` = Windows' font set + every font file in `bin\fonts`
  (shipped: Material Symbols Outlined, from repo `fonts\`, Apache 2.0) and `config\fonts` (the user's; Settings > Fonts
  adds files or a Google Fonts .zip). Nothing is installed into Windows. `Symbols`: `{icon:name}` (Fmt, and
  `Symbols.Expand` in Canvas) becomes the icon's private-use character; Canvas lays such text out and sets the icon
  font on just those characters (`Symbols.Mark`: family, size × icon_size, FILL axis), so Nerd/Segoe icon fonts can't
  take them over; the glyph atlas draws icons into one cell. Theme `[ui] icon_style/icon_fill/icon_size`; rounded and
  sharp download once into `config\fonts`. Settings > Icons = searchable grid (GDI+ PrivateFontCollection) that copies
  `{icon:name}`. Every font option uses `FontNameConverter` (DirectWrite family list incl. config\fonts); widgets have
  Box > Font / Font size (Composer.UseFont).
- Performance presets (`glyph.toml [performance]`): `plugged_in` / `on_battery` / `battery_saver` each pick
  highest, balanced, low or pause; `[performance.highest|balanced|low]` hold each preset (fps max/theme/number,
  effects, animation_detail max/theme/low, smooth_dragging). `Activity.Reason` picks the mode every 0.5 s and App
  puts the preset in `Perf.Current`, read by the engine (frame rate from `Screens.RefreshRate`, effects), the noise
  layer (`Perf.NoiseInterval`) and video widgets (`Perf.VideoFps`). Old keys (pause_on_battery_saver, battery_fps)
  are read once by `PerformanceOptions.ReadOld`.
- Also: `--snapshot out.png [--seconds N] [--size WxH] [--cursor x,y] [--theme NAME] [--bar]`, `--snapshot-bar out.png`,
  `--snapshot-launcher out.png [query]`, `--write-guide`, `--reset-example operator` (puts the built-in theme back).
  Set `GLYPH_HOME` to a folder with its own `config\` to test with other files; `GLYPH_DEBUG=1` logs clicks.

## How it works

- **Options** (`Options\Options.cs`): every setting is a property with `[Description]`, `[Range]`, `[Choices]`,
  `[Color]`, `[Section]`. The same classes are read from TOML (with typo/range/colour checks), shown in the
  settings window's PropertyGrids, written back with comments, and turned into the guide's tables.
- **Desktop host** (`Desktop\DesktopHost.cs`): 0x052C to Progman makes the WorkerW under the icons (24H2+: a child
  of Progman); one child window per screen Glyph uses lives there, placed over its display (screens without one show
  Windows' wallpaper). Re-attaches on Explorer restart.
- **Screens** (`Desktop\Screens.cs`, glyph.toml `[monitors]`): displays by Windows' number (\\.\DISPLAYn).
  `MonitorOptions.Plan` gives (screen, theme, widgets?, bar?) for modes same / each / one. `Engine` keeps a `Surface` per
  screen (own Composer, theme, desktop and bar renderers sharing one GPU device, pause state); clicks and the wheel go to
  the surface whose bounds hold them; work areas are read live (`Screens.WorkOf`) so bars' reserved space counts.
  `Activity.Covered` rests each screen separately. App makes one BarWindow per planned bar and calls
  `host.SetScreens(plan)` after every reload; a change in `Screens.Key()` (checked every second) triggers a reload.
  `Desktop\DesktopIcons.cs` hides/shows Windows' icons (WM_COMMAND 0x7402 to SHELLDLL_DefView) and only restores
  what it changed.
- **Theme sync** (`Desktop\ThemeSync.cs`, theme `[sync]`, glyph.toml `theme_sync`): Windows Terminal settings.json edited
  as JSON (scheme + WT theme "Glyph", `profiles/defaults` keys; paths split by `/` because WT keys such as
  `experimental.pixelShaderPath` contain dots), CRT shader generated from `[effects]` into `logs\terminal\` (no `Time`,
  so WT doesn't redraw continuously). Accent via uxtheme #122 SetUserColorPreference {start, accent ABGR}, then our
  AccentPalette over it; dark mode via Personalize *UseLightTheme; WM_SETTINGCHANGE "ImmersiveColorSet". Every value is
  recorded in `logs\sync.json` before the first change and restored when a part is turned off; a part is only rewritten
  when its wanted values change. Audio.cs and Mixer.cs share `Mixer.NewEnumerator()` (two typed [ComImport] classes for
  the one MMDeviceEnumerator singleton made the second cast fail).
- **Hotkeys** (`Desktop\Hotkeys.cs`, `config\hotkeys.toml`): RegisterHotKey on the hidden "Glyph shell listener" window;
  WM_HOTKEY runs `Actions.Run`. Ones another program owns are logged and listed in `logs\hotkeys-taken.txt` (the
  settings page shows them). Win+Alt+arrows/T/F/N/K are taken on this PC, hence Win+Ctrl+Alt for tiling.
  `Glyph.exe --action x` sends x to the running Glyph by WM_COPYDATA (dwData 0x474C) to that window.
  `Actions.Internal` (set by App) handles launcher, tile:, theme:, glyph:, shortcut:, copy:, runas:, window:.
- **Launcher** (`Launcher\`): LauncherHost = popup NativeWindow (topmost, toolwindow, NOREDIRECTIONBITMAP) + an
  IMessageFilter: WinForms swallows Enter/Esc/Tab/arrows and never makes WM_CHAR for a non-Control window, so keys
  are caught in PreFilterMessage and turned into text with ToUnicode. Drawn by the render thread
  (`Engine.LauncherStep`, own composition Renderer, `Engine.Wake()` event so typing draws at once); shown only after
  the first frame (`Drawn`). Renderer + icons dropped 20 s after closing. Start menu = AppsFolder via
  SHGetKnownFolderItem + BHID_EnumItems, names SIGDN_NORMALDISPLAY / SIGDN_PARENTRELATIVEPARSING (0x80018001; not
  FILESYSPATH), read by the helper `Glyph.exe --scan-apps` into `config\icons\cache\apps\apps.json` + PNG icons
  (same SHA1 names as shortcut icons), so shell COM never loads in the main process (it left ~10 MB behind).
  `Search.cs`: scoring (prefix > word start > initials > all words > letters in order with <= len+2 gaps),
  history boost (`logs\launcher-history.json`), Calc (recursive descent), paths, URLs, web search.
- **Tiling** (`Tiling\TileManager.cs`): WinEvent hooks (foreground, move/size, minimise, show/hide/destroy,
  cloak/uncloak; NOT location change, which fires on every mouse move: maximise is polled once a second),
  40 ms debounce, layouts per monitor, SetWindowPos ASYNC with the DWM extended-frame margins added back.
  A window still bigger than its tile 700 ms later floats (minimum size), centred. Borders via DWMWA_BORDER_COLOR,
  corners DWMWA_WINDOW_CORNER_PREFERENCE, both reset when tiling goes off.
- **Frames** (`Render\Frames.cs`, theme `frames.toml`): named box designs (cuts, slant, fill/fade/pattern, border sides,
  dashes, inner border, title plain/bar/tab/line/above with letter spacing, brackets, corner mark, stripe, hatch, ruler
  ticks, glow). `FramePainter.Draw` (D2D path geometry; patterns clipped with a geometric-mask layer) and `Space`/`Above`/
  `Outside` (insets). Used by panels (`Frame`, ui `panel_frame`), buttons (ui `button_frame`), the bar (`frame`,
  `module_frame`, per-module `frame` + `join` -> `BoxEl`), workspace numbers, the launcher (`PaintFramed`) and windows.
  `Theme.Frame(name, auto)` resolves names. Settings: Frames page (`SettingsForm.Frames.cs`) previews with `SoftCanvas`
  (D2D DC render target in software, QI'd to ID2D1DeviceContext so the same Canvas works).
- **Window frames** (`Desktop\WindowFrames.cs`, theme `[windows]`): four click-through layered strips per framed window
  (top/bottom/left/right of the DWM extended frame), drawn by `SoftCanvas` + UpdateLayeredWindow only when size, focus or
  title change; moves are SetWindowPos. 200 ms poll (16 ms while dragging). Re-stacked only on focus changes, skipping
  hidden windows above the target. **Never Stop/Start a WinForms Timer per WinEvent**: each Start makes a hidden window
  and each Stop destroys it (EVENT_OBJECT_DESTROY) -> a feedback loop that pinned the UI thread at 90%; events from
  Glyph's own UI thread are ignored (also in TileManager). Tiling shrinks tiles by `WindowFrames.Room`.
- **Feeds / links / visualiser**: `FeedWidget` (patterns with `{hex}` `{num}` `{pick}` `{seq}` `{spaces}` filled once per
  line, ages fade), `Composer.DrawLinks` (Name / Link to, elbow lines), `Data\Spectrum.cs` (WASAPI loopback + 1024 FFT,
  thread runs only while a visualiser shows something playing). `Data\Status.cs`: {wifi} {network} via WinRT
  NetworkInformation (re-read on NetworkStatusChanged + every 20 s), {bluetooth} (Radio found once, then its State),
  {lang} (foreground thread's keyboard layout, LCIDToLocaleName). Bar `Unclash` moves the centre group off the sides.
- **Menu / divider / tasks** (`Widgets\MenuWidget.cs`, `Widgets\DividerWidget.cs`, `Data\Tasks.cs`): the menu widget builds tabs
  from shortcut groups (`ShortcutStore.Groups` = shortcuts.toml `[[group]]`, edited in Settings > Shortcuts > Groups;
  the settings window keeps its own copy because the preview thread reloads the store), lays out tabs / heading / list /
  details itself and animates each plate's fill (`Amount`, eased, `Animated` only while one is moving; frames take a
  `wipe` 0-1 and draw the hover fill that far from the left). `Widget.Covers` = the parts that are really there, so the
  selection box and background clicks still work in the empty space of a stretched widget. `Stretch` (WidgetOptions) +
  `Widget.WantW/WantH/PlaceFor` size a box between its offsets. Bar `tasks`: EnumWindows like the taskbar (no owner,
  not toolwindow/noactivate unless appwindow, not cloaked), cached 500 ms, icons per window; `task:<hwnd>` toggles
  front/minimised (Alt tap when SetForegroundWindow is refused). Module `caption` wraps the module in a ColEl.
  Backdrop `pattern`/`pattern2` and frame patterns share `Render\Patterns.cs`.
- **Tweens** (`Render\Tween.cs`): `Easing` (Roblox's styles and directions), `TweenOptions` (animations.toml), `Animator`
  (bound to widgets by Name / type: / * in `Composer.Load`; `Step` each composed frame, `Apply` wraps each widget's
  DrawVector in a D2D transform about its centre + an opacity layer). Appear tweens keep their start across reloads when
  the tween list is unchanged (signature of the written values). Preview: `UpdateHover` too, ~30 fps while tweens run,
  `Replay()`.
- **Design page parts** (`Widget.Parts()`, `UI\SettingsForm.Parts.cs`, `UI\PropertyView.cs`): widgets list parts (rect,
  option categories, the option naming its frame); the preview sends them in `Info.Parts`; the smallest part under the
  mouse wins. `PartView` = an ICustomTypeDescriptor showing only some categories (owner = the real object). `Friendly`
  registers a TypeDescriptionProvider on every option class in the settings process only: category order (leading tabs),
  spaced names, On/Off, colour swatches (`Swatch.Resolve`), read-only properties hidden.
- **Tray** (`Data\Tray.cs`, bar module `tray`): a hidden top-most `Shell_TrayWnd` window on its own thread ("Glyph tray",
  started by the first tray module, closed on quit) so `Shell_NotifyIcon` (WM_COPYDATA dwData 1: signature, NIM_*,
  NOTIFYICONDATA with 32-bit handles; offsets in the file) reaches Glyph; every message (also appbar dwData 0 and
  WM_USER+) is forwarded with SendMessageTimeout to Explorer's tray, found by process name (explorer.exe) so two
  such windows can't forward to each other. Re-raised every 2 s; broadcasts "TaskbarCreated" at start and when
  Explorer restarts. Clicks: v4 icons get (x,y | event, id) + NIN_SELECT / WM_CONTEXTMENU, older ones (id, event);
  AllowSetForegroundWindow(owner pid) first. Glyph's own FindWindow("Shell_TrayWnd") uses `Tray.ExplorerTray()`.
- **Wallpaper Engine** (`Data\WallpaperEngine.cs`, backdrop type `wallpaper_engine`): finds Steam libraries
  (HKCU SteamPath + libraryfolders.vdf), lists workshop/content/431960 + projects/myprojects + defaultprojects
  (project.json title/type/preview). `Engine.LoadAll` sets `Composer.LiveEngine` per screen; then the desktop renderer
  is a DirectComposition swap chain (premultiplied, cleared to alpha 0, screen effects skipped) on the same WorkerW
  child, so WE's `WPEDesktopDX11Window` sibling shows through. WE raises its window when it opens a wallpaper:
  `DesktopHost.KeepOnTop` (every second) puts ours back above (GW_HWNDPREV). `App.ApplyWallpaperEngine` runs
  `wallpaper64.exe -control openWallpaper -file <project.json> -monitor <index in Screens.All()>` (+ mute/unmute,
  closeWallpaper for screens that stop using it), only when the wanted wallpaper changed; no screen using it = WE closed
  (if close_wallpaper_engine). Previews/snapshots draw the item's preview image instead.
- **Side bar** (bar `position = left/right`, smooth themes): `BarOptions.PlaceIn` (bar rect + remaining area, used by
  the preview and snapshots), appbar ABE_LEFT/RIGHT, groups and icon/workspace/task/tray rows are `RowEl.Vertical`
  (Painter stacks them centred), text modules `rotate = down/up` (Painter draws the TextEl under a D2D rotation).
- **Lists and lyrics** (`Data\Lists.cs`, `Data\Lyrics.cs`, `Widgets\ListWidgets.cs`): list widgets read
  config\lists\<name>.txt (re-read on change; the watcher ignores lists\), click = action `list:toggle:<name>:<n>`
  (runs in the desktop process, rewrites the line). Lyrics: lrclib.net `/api/get` (title, artist, album, duration)
  then `/api/search`; cached per song in logs\lyrics (empty file = none); the current line from the media position.
- **Glow and halo** (`Render\Aura.cs`, theme [ui] halo/glow + per widget "Glow & halo" + bar glow): the widget's
  drawing is recorded (command list), rendered ONCE into a bitmap of its bounds, and effects run on that bitmap: halo =
  Shadow (blur) with its alpha boosted ×3.5 and clamped (thin text blurs too faint otherwise), drawn under; glow =
  GaussianBlur (self) or Shadow (a colour), boosted, added with CompositeMode.Plus at two sizes. Panels apply it to
  their contents (plus frames without fill/pattern), not their glass. Gotchas found: a command list holding a layer
  (clips, legend gaps, fades) fed to several effects fails the frame with D2DERR_LAYER_ALREADY_IN_USE (EndDraw result,
  now logged as "drawing failed"), hence the bitmap; an empty recording has no bounds (skipped); Composer flushes
  after each widget with an aura. Patterns now fill one path geometry instead of hundreds of rectangles.
- **Performance** (measured live on the 680M laptop, "highest" preset, 2026-11-28; before → after): ember 77 → 9 %
  of a core at ~55 fps, grove 8.9 → ~2 %, neon 5.4 → ~4 %; RAM neon 175 → 123 MB. How: (1) widget contents are
  checked for changes 4×/s (`Surface.SigAt`, bar `BarSigAt`), not every tick; (2) while only the wallpaper layers
  move, the widgets are drawn once into a kept full-screen picture and reused (`Renderer.OverlayMode` 1 = draw and
  keep, 2 = reuse; `Surface.WidgetsDirty`, `Composer.OverlayMoving`, layout skipped via `Composer.LayoutWidgets`);
  (3) a frame whose character grid equals the last one (`SameGrid`) isn't drawn or presented; (4) glow/halo widgets
  keep their finished picture while their signature, bounds, hover and transform are unchanged (`Aura.Kept`,
  `Widget.AuraKey`; keeping EVERY widget was tried: +10-20 MB for ~no CPU, reverted); (5) idle mouse polling 10 Hz
  when the mouse is still (refresh rate / 30 Hz while it moves); (6) stats: network adapter list every 15 s, disk
  every 10 s, audio endpoint kept 5 s; bar app names cached per process; font keys only rebuilt on change;
  (6b) preset `wallpaper_fps` (PresetOptions.WallpaperRate, default theme; low 15): while ONLY the scene layers
  move (no widget/tween/ripple/cursor/mouse), the loop runs at it, so "highest" no longer draws embers at 60 Hz
  (ember 9 -> ~7 % of a core, 20 frames/s); (7) pictures decoded at the size they're shown (`Canvas.Picture`); `Renderer.TrimDevice` = D2D ClearResources +
  IDXGIDevice3.Trim on trims. Tools: scratchpad bench.ps1 (CPU/RAM/fps per theme on the real desktop), dotnet-trace
  `--profile dotnet-sampled-thread-time --format Speedscope` + a small analyser, a VirtualQueryEx map (big blocks =
  AMD driver pools).
- **Desktop editing** (`UI\Prompt.cs`): a themed borderless top-most form at the mouse (fonts from config\fonts via
  PrivateFontCollection, Alt tap so it may take the focus), Enter saves / Esc or deactivate cancels. Actions
  `list:edit:<style>:<n>:<name>` (n = -1 adds; agenda asks time/what/note; empty = delete) and `weather:city:<name>`
  (rewrites that widget in widgets.toml).
- **Font weights in names** (`Fonts.Resolve`): "Rubik Black" = family Rubik + weight Black when the family itself
  isn't found (Google Fonts' static files share one family name).
- **Converter loop** (`ConvertJob.Run`): crossfade decodes the clip minus its last `blend` frames plus a second ffmpeg for
  that tail, mixing tail frame i into head frame i (RGB before conversion); bounce stores cells and appends them reversed.
- **Input**: raw input (RIDEV_INPUTSINK) on a hidden window gives left-button events anywhere; if the window under
  the cursor is the desktop, the click goes to the render thread, which hit-tests widgets. Launches run on the UI thread.
- **Frame** (`Composer.cs`, `Engine.cs`, `Render\Renderer.cs`): backdrop (clear colour, or D2D gradient/picture) →
  character grid (wallpaper layers, cursor effect, terminal widgets, problems box) uploaded as a (glyph, fg, bg) texture
  and blended in one pass → smooth widgets via D2D → effects (bloom, scanlines, …) or a plain copy. Frames are only drawn
  when something moves or changes; otherwise the loop just watches the mouse at 15 Hz.
- **Glyph atlas** (`Render\GlyphAtlas.cs`): DirectWrite characters on first use; blocks, shades, box drawing and braille
  drawn as exact shapes.
- **Shortcuts** (`Widgets\`): icons from IShellItemImageFactory (with fixes for .url/Steam icons, framed small icons,
  no-alpha icons), cached as PNG; styled per theme (original / tint / mono / ascii via the converter's braille/blocks).
- **Clips / converter** (`Clip.cs`, `Convert\`): `.gclip` = char table + per-frame Brotli of XOR-delta cells; ffmpeg
  decodes at 4×8 samples per cell; modes shape, ramp, edges, blocks, braille.
