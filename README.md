# Glyph

A fully customisable desktop for Windows 11: themes (terminal-style ASCII or smooth panels),
an animated wallpaper behind the desktop icons, shortcut panels that replace the desktop icons,
a settings window, and a video→ASCII converter.

**User guide:** `config\GUIDE.md` (generated: `bin\Glyph.exe --write-guide`).

## Install

1. `winget install Microsoft.DotNet.DesktopRuntime.9` (and `winget install Gyan.FFmpeg.Essentials` for videos).
2. Download `Glyph-win-x64.zip` from [Releases](https://github.com/exinics/glyph/releases), unzip, run `Glyph\bin\Glyph.exe`.
3. It updates itself from the newest release (Settings > General > Updates).

## Building and releasing

- `powershell -File build.ps1` builds into `bin\` (and restarts Glyph if it was running).
- `powershell -File release.ps1 [-Version 1.2.0] [-Notes "..."]` bumps the version in `Glyph.csproj`, builds
  `out\Glyph-win-x64.zip` (`Glyph\bin\` with `themes\` inside, plus an empty `Glyph\config\`), commits, tags,
  pushes and creates the GitHub release every Glyph updates from (`Updater.cs`).
- `themes\` holds themes that ship with Glyph (copied into `config\themes` once, the first time a build sees them).
  `config\` itself is personal and not in git (only `config\GUIDE.md` is).

## Layout

```
Glyph\
  bin\                 built program (Glyph.exe); made by build.ps1 (not in git)
  themes\              themes that ship with Glyph (copied into config\themes once)
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
- Also: `--snapshot out.png [--seconds N] [--size WxH] [--cursor x,y] [--theme NAME] [--bar]`, `--snapshot-bar out.png`,
  `--snapshot-launcher out.png [query]`, `--write-guide`, `--reset-example <red-mono|soft>`.
  Set `GLYPH_HOME` to a folder with its own `config\` to test with other files; `GLYPH_DEBUG=1` logs clicks.

## How it works

- **Options** (`Options\Options.cs`): every setting is a property with `[Description]`, `[Range]`, `[Choices]`,
  `[Color]`, `[Section]`. The same classes are read from TOML (with typo/range/colour checks), shown in the
  settings window's PropertyGrids, written back with comments, and turned into the guide's tables.
- **Desktop host** (`Desktop\DesktopHost.cs`): 0x052C to Progman makes the WorkerW under the icons (24H2+: a child
  of Progman); our child window lives there. Re-attaches on Explorer restart.
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
