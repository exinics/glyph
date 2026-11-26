using System.ComponentModel;
using System.Reflection;
using System.Text;
using Glyph.Bar;
using Glyph.Options;
using Glyph.Wallpaper;
using Glyph.Widgets;

namespace Glyph;

/// Writes config\GUIDE.md: a hand-written introduction plus option tables generated from the
/// options classes (so the guide always matches what Glyph and the settings window know).
/// Run: Glyph.exe --write-guide
internal static class Docs
{
    public static void WriteGuide()
    {
        var sb = new StringBuilder(Intro);
        sb.AppendLine("## Every option").AppendLine();
        sb.AppendLine("The settings window shows all of these with the same descriptions. In the files, the option name is the");
        sb.AppendLine("lower-case name in the first column; a section like `[ui]` is the part of the file it lives in.").AppendLine();

        Section(sb, "glyph.toml (Glyph itself)", new AppSettings(), "");
        Section(sb, "glyph.toml [performance]", new PerformanceOptions(), "");
        Section(sb, "glyph.toml [launcher] (what the launcher finds)", new Glyph.Launcher.LauncherSettings(), "See 6e.");
        Section(sb, "glyph.toml [tiling]", new Glyph.Tiling.TilingOptions(), "See 6f.");
        Section(sb, "hotkeys.toml [[hotkey]]", new HotkeyOptions(), "See 6g.");
        Section(sb, "theme.toml [info]", new InfoOptions(), "");
        Section(sb, "theme.toml [font] (characters)", new FontOptions(), "");
        Section(sb, "theme.toml [ui] (smooth style)", new UiOptions(), "");
        Section(sb, "theme.toml [effects]", new EffectSettings(), "");
        Section(sb, "theme.toml [sync] (Windows Terminal, accent colour, dark mode)", new SyncOptions(), "What this theme also changes outside Glyph (see 4b).");
        Section(sb, "theme.toml [launcher] (how the launcher looks)", new Glyph.Launcher.LauncherLook(), "See 6e.");
        Section(sb, "theme.toml [windows] (window frames)", new Glyph.Desktop.WindowFrameOptions(), "See 6h.");
        Section(sb, "frames.toml [[frame]] (in a theme folder)", new Glyph.Render.FrameOptions(), "Box designs, picked by name (see 6b5).");
        Section(sb, "wallpaper.toml (top)", new WallpaperOptions(), "");
        Section(sb, "wallpaper.toml [backdrop]", new BackdropOptions(), "");
        Section(sb, "wallpaper.toml [cursor] (mouse effect)", new CursorOptions(), "");
        Section(sb, "wallpaper.toml [interaction] (clicks, scrolling, ripples, trail)", new InteractionOptions(), "What empty desktop does when you click, double-click or scroll it, and the click ripple and mouse trail.");
        foreach (var (type, make) in WallpaperOptions.Types)
            Section(sb, $"wallpaper.toml [[layer]] type = \"{type}\"", make(), LayerHelp.GetValueOrDefault(type, ""));
        Section(sb, "bar.toml (the bar)", new BarOptions(), "The bar along the top (or bottom) of the screen.");
        foreach (var (type, make) in BarFile.Types)
            Section(sb, $"bar.toml [[left]] / [[center]] / [[right]] type = \"{type}\"", make(), type switch
            {
                "text" => "Text with {values}. Most modules are this: clock, CPU, media, volume… (the settings window's Add module menu has ready-made ones).",
                "meter" => "A small bar showing a value from 0 to 100%.",
                "graph" => "A tiny history graph.",
                "workspaces" => "Windows' virtual desktops; click one to switch, scroll to move between them.",
                _ => "Empty space.",
            });
        foreach (var (type, make) in WidgetsFile.Types)
            Section(sb, $"widgets.toml [[widget]] type = \"{type}\"", make(), WidgetsFile.Help.GetValueOrDefault(type, ""));
        Section(sb, "shortcuts.toml [[shortcut]]", new Shortcut(), "Your shortcuts, shared by every theme.");
        Section(sb, "variables.toml [[variable]] (in a theme folder)", new Glyph.Data.VariableOptions(), "Your own values, used in any text as {name}.");
        sb.Append(Outro);
        SafeFile.Write(Paths.Guide, sb.ToString());
    }

    static readonly Dictionary<string, string> LayerHelp = new()
    {
        ["noise"] = "Flowing smoke or clouds made of characters.",
        ["rain"] = "Falling (or rising) streams of characters.",
        ["stars"] = "A starfield: flying through it, or drifting sideways.",
        ["static"] = "TV static: random characters that flicker.",
        ["glitch"] = "Jolts the layers *above it in the file* sideways for a moment. Put it after the layers it should glitch.",
        ["art"] = "ASCII art from a text file (logos, banners). For big text, search for a \"text to ASCII art\" generator; the GLYPH logo uses the \"ANSI Shadow\" font.",
        ["clip"] = "Plays an ASCII animation made with the converter.",
    };

    static void Section(StringBuilder sb, string title, object o, string intro)
    {
        sb.AppendLine($"### {title}").AppendLine();
        if (intro.Length > 0) sb.AppendLine(intro).AppendLine();
        sb.AppendLine("| Option | Default | Allowed | What it does |").AppendLine("|---|---|---|---|");
        foreach (var p in Binder.Props(o.GetType()))
        {
            if (p.GetCustomAttribute<SectionAttribute>() != null) continue;
            var key = Binder.Key(p);
            var def = Binder.Value(p.GetValue(o)).Replace("|", "\\|");
            var r = p.GetCustomAttribute<Options.RangeAttribute>();
            var c = p.GetCustomAttribute<ChoicesAttribute>();
            string allowed = c != null ? string.Join(", ", c.Values) : r != null ? $"{r.Min:0.###} to {r.Max:0.###}"
                : p.GetCustomAttribute<ColorAttribute>() != null ? "colour name or hex" : p.PropertyType == typeof(bool) ? "true, false" : "";
            var desc = Binder.Describe(p).Replace("|", "\\|");
            var name = p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
            if (name != null) desc = $"**{name}.** " + desc;
            sb.AppendLine($"| `{key}` | `{def}` | {allowed} | {desc} |");
        }
        sb.AppendLine();
    }

    const string Intro = """
# Glyph guide

Glyph turns your desktop into whatever theme you make: an animated ASCII terminal, a calm smooth
desktop with rounded panels, or anything in between. Your shortcuts sit on the desktop in a panel,
drawn in the theme's style.

**The easy way to change things is the settings window** (left-click the tray icon, or right-click
it > *Settings…*). Every change is saved and shown on the desktop straight away. The settings window
is a separate program: when you close it, nothing of it stays in memory.

Everything is also in plain text files in this folder, if you prefer editing them. Save a file and the
change appears within about a second.

---

## 1. Starting and the tray icon

- Start Glyph with `Glyph.lnk` in the Glyph folder (or `bin\Glyph.exe`).
- A `>_` icon appears in the system tray (bottom right; maybe under the `^` arrow).
  - **Left-click**: open the settings window.
  - **Right-click**: *Settings…*, *Convert a video…*, *Pause*, *Reload*, open the config folder, this guide or the log, *Quit*.
- Hover over the icon to see the frame rate, "idle" (nothing is moving, so nothing is being drawn), or why it's resting.
- Quitting puts Windows' own wallpaper and desktop icons back.

## 1b. Installing on another PC, and updates

1. Install the .NET 9 desktop runtime once: `winget install Microsoft.DotNet.DesktopRuntime.9`
   (and ffmpeg for videos and the converter: `winget install Gyan.FFmpeg.Essentials`).
2. Download `Glyph-win-x64.zip` from the newest release on GitHub (github.com/exinics/glyph/releases),
   unzip it anywhere you like (e.g. `C:\Glyph`) and run `Glyph\bin\Glyph.exe`. The first start makes the example
   themes (and the **operator** theme) and imports your desktop shortcuts into `Glyph\config`.
3. Settings > General > *Start with Windows* to have it start by itself.

**Updates** (General > Updates): `auto` looks on GitHub 30 seconds after Glyph starts and every 6 hours, downloads a
newer version and restarts into it (a few seconds); `ask` shows a note and an *Update* item in the tray menu; `off`
never looks. *Check for updates* in the tray menu looks straight away (`Glyph.exe --update` from a terminal).
Updates only replace `bin\`: your `config` folder (settings, themes, shortcuts) is never touched. What happened is in
`logs\update-result.txt`.

## 2. The settings window

| Page | What's there |
|---|---|
| **Design** | A live preview of the theme you're editing. Click a widget, the bar, a bar module or the background to change it on the right; drag widgets to move them, drag the corner square to resize, and *Draw box ▾* to drag out a new box and choose what's in it. *Animate* lets the wallpaper and mouse effects play. The colour squares at the bottom change the theme's colours. *Pick colour* is an eyedropper: click anywhere on the preview to put that colour in the palette (as the accent with matching shades, or any colour). *Wallpaper colours…* finds the wallpaper's main colours and offers an accent or a whole palette made from them. |
| **Frames** | The theme's box designs (see 6b5), with a live preview. *New frame ▾* starts from a ready-made design. |
| **Themes** | Your themes. *Use this theme* shows it on the desktop. *New theme…* and *Duplicate…* make a copy to change, so you never break a theme you like. Rename, delete, open the folder, or *Add example themes* to get back the two that came with Glyph. Clicking a theme picks it for editing in the other tabs (the line at the top says which one you're editing). |
| **Colours & look** | The **palette** (every colour in the theme; double-click to change one, or type a hex code) and the theme's options: its **style**, the **character font**, the **smooth style** (panel colour and see-through-ness, corner roundness, border, shadow, font) and the **screen effects** (glow, scanlines…). |
| **Wallpaper** | The **backdrop** (a colour, a gradient or a picture), the **mouse effect**, and the **layers**: add, remove, duplicate and reorder them, and change the selected one below. |
| **Shortcuts** | Your shortcuts (shared by every theme). Add programs, files, folders or websites, *Import desktop icons*, reorder them, give one *your own picture*, or go back to the app's icon. Also: *Hide Windows' own desktop icons*. |
| **Bar** | The bar: turn it on, where it sits, its size and look, and its modules (left, centre, right) with what clicking or scrolling them does. |
| **Variables** | Your own values (built from other values, a command's output or a file), plus a box to try any format and see the live result. |
| **Widgets** | Everything on the desktop: shortcut panels, clock, system monitor, music, calendar, weather, text, command output, system info, pictures, videos, sound controls. *Add widget ▾*, then change the selected one. |
| **General** | Start with Windows, closing Wallpaper Engine, when to rest to save power; start / quit / restart Glyph, the converter, folders. |

At the bottom of each list of options there's a short explanation of whichever option you click.

## 3. Themes and styles

A theme is a folder in `themes\` holding everything you see:

| File | What it holds |
|---|---|
| `theme.toml` | the palette (the **only** place hex codes live), the style, fonts, smooth-style look, screen effects |
| `wallpaper.toml` | backdrop, mouse effect, wallpaper layers |
| `widgets.toml` | the panels on the desktop |

Files a theme uses (ASCII art, clips, pictures) are looked for in the theme's own folder first, then in
the shared `art\`, `clips\`, `images\` and `icons\` folders here.

**Style** decides how panels are drawn:

- **terminal**: everything is characters in a grid. Panels are boxes drawn with line characters; icons
  are drawn with braille dots or blocks in the theme's colours.
- **smooth**: rounded, see-through panels with shadows, normal fonts, real (or recoloured) icons, and
  picture or gradient backdrops. ASCII layers still work on top of the backdrop if you want them.

**Icon styles** (Panels tab): *auto* (characters in terminal themes, the app's own icon in smooth ones),
*original*, *tint* (recoloured with the tint colours: dark parts to the first colour, bright parts to
the last), *mono* (a flat silhouette in one colour) and *ascii* (drawn with characters). Your own
picture for a shortcut is styled the same way, so it always fits the theme.

**The two example themes:** `red-mono` (terminal: black and white with red, CRT glow) and `soft`
(smooth: gradient backdrop, rounded panels; nothing moves, so it uses almost no power).

## 4. Colours

Colours are defined once, in the theme's palette. Everywhere else uses their **names**, so changing one
hex code recolours everything that uses it. Where an option asks for a colour you can give:

- a palette name: `accent`
- a hex code: `#E0202F`, or `#E0202F80` where the last two digits are see-through (00 = invisible, FF = solid)
- `none`: fully transparent
- for a gradient, a list from faint/dark to strong/bright: `bg, accent_dim, accent, bright`

### red-mono palette

| Name | Hex | Used for |
|---|---|---|
| `bg` | `#0A0A0A` | background (near-black) |
| `surface` | `#141414` | panels, the problems box |
| `line` | `#2A2A2A` | borders, very dim characters |
| `dim` | `#8A8A8A` | secondary text |
| `text` | `#E6E6E6` | main text (off-white) |
| `bright` | `#FFFFFF` | highlights, the brightest characters |
| `accent` | `#E0202F` | the red |
| `accent_dim` | `#6E0E16` | dark red |
| `accent_bright` | `#FF4D57` | bright red: hover, alerts, the mouse glow |

Terminal colours: black `#0A0A0A` · red `#E0202F` · green `#A3B8A0` · yellow `#D9C9A3` · blue `#8E9BB0` ·
magenta `#C97A86` · cyan `#9FB5B5` · white `#E6E6E6` · bright_black `#5A5A5A` · bright_red `#FF4D57` ·
bright_green `#BFD1BC` · bright_yellow `#EDE0C2` · bright_blue `#AEB9CB` · bright_magenta `#E39BA6` ·
bright_cyan `#BDD0D0` · bright_white `#FFFFFF`

### soft palette

bg `#14171C` · surface `#1E232B` · line `#2D3440` · dim `#8C96A5` · text `#E7EBF0` · bright `#FFFFFF` ·
accent `#6FA8DC` · accent_dim `#2F4A63` · accent_bright `#9CC7F0` · glow `#232B3A`

## 4b. Theme sync: Windows Terminal and Windows colours

A theme can also colour things outside Glyph. Its **Theme sync** options (Colours & look page, or
`[sync]` in theme.toml) say what; switching theme switches them too.

- **Windows Terminal:** a colour scheme called *Glyph* made from the theme's 16 terminal colours (plus
  background, text, cursor and selection colours you pick from the palette), used by every profile, and
  tabs to match. Optional: font (`theme` = the theme's character font), font size, opacity and blur, cursor
  shape, and a **CRT effect**: `retro` is Terminal's own glow and scanlines; `effects` turns this theme's
  screen effects (scanlines, vignette, curvature, colour fringing, brightness, contrast) into a Terminal
  shader. It's still (no flicker or grain), so Terminal only redraws when its text changes.
- **Windows accent colour:** Start, toggles, buttons, links and focus outlines. *Accent shades* `theme`
  makes the lighter and darker shades Windows also uses from *Accent light* / *Accent dark*; `windows` lets
  Windows work them out. Accent on Start/taskbar and on title bars can be turned on or off (or left alone).
- **Dark mode:** `auto` picks dark or light from the theme's background, or force `dark` / `light`, or `keep`.

Before Glyph changes anything it notes the old value (in `logs\sync.json`), so turning a part off puts it
back exactly. `theme_sync = false` in glyph.toml (General page) turns it all off and restores everything.
Glyph only writes when what the theme asks for changes, so changes you make in Terminal yourself stay until
you change the theme. A copy of Terminal's settings from before Glyph first touched them is kept next to
them as `settings.before-glyph.json`.

## 5. Shortcuts on the desktop

- **Open** a shortcut with a double-click (or a single click if the panel is set to *click*). The mouse
  highlights the one under it.
- While Glyph runs, Windows' own desktop icons are hidden (General / Shortcuts tab to turn that off).
  Right-clicking the desktop still gives Windows' usual menu.
- *Import desktop icons* adds everything on your desktop (and the Recycle Bin). Icons are taken from
  Windows once and kept in `icons\cache`.
- *Set picture…* copies your image into `icons\` and uses it; it's recoloured to match the theme
  unless the panel's icon style is *original*.

## 6. The wallpaper

The wallpaper is the **backdrop** with **layers** of characters on top, drawn in order (later layers on
top; empty parts let the ones below show). The **mouse effect** lights up characters near the mouse.

If nothing in the wallpaper moves (no animated layers, mouse effect off), Glyph stops drawing frames and
only redraws when something changes, like the mouse moving over a shortcut.

## 6b. The bar

The bar sits along the top (or bottom) of the screen, above every window. With *Reserve space* on,
windows (and maximised windows) stop at its edge. Give it a *Margin* and *Corner radius* to make it float,
*Opacity* below 1 to see through it, and *Blur* for frosted glass.

Its contents are **modules** in three groups: left, centre and right. On the settings window's Bar page,
pick the group, then *Add module ▾* for ready-made ones (clock, workspaces, media, CPU meter, volume,
battery, start button…). Every module can say what happens when you click, right-click or scroll it.
The bar pauses only for fullscreen apps and the lock screen. Set *Position* to bottom to put it in place of the
taskbar, and General > *Windows taskbar* to `hide` to remove Windows' own taskbar completely (it comes back when
Glyph quits). With the taskbar hidden, its tray icons (including Glyph's) aren't visible: open Glyph's settings
with a bar button (`settings`), `Glyph.lnk`, or the Start menu.

## 6b2. Boxes and the background

Every widget is a box with its own **Box** options: colour, opacity, corner radius, border, shadow and a hover
colour (if it does something on click). Text, clock, system and the other info widgets also take a fixed
**Width** and **Height** and **Vertical align**, so you can draw an exact box on the Design page and fill it.

The **background** can be interactive too (Wallpaper > Interaction, or click the background on the Design page):
what clicking, double-clicking and scrolling on empty desktop does (e.g. scroll for volume, double-click to open a
folder), a **ripple** where you click, and a **mouse trail**. Wallpaper layers can have **Parallax X / Y** so they
shift against the mouse for a feeling of depth.

## 6b3. Videos on the desktop

A **video** widget is a box that plays a video (mp4, webm, mkv, mov, gif…) or an ASCII clip from the converter
(`.gclip`). Put files in `config\videos` and `config\clips` (or pick any file). Draw one on the Design page with
*Draw box ▾ > video*. *Style* shows a normal video as the real picture or turns it into characters live
(terminal themes do that by default); *Colors*, *Characters* and *Gradient* set how the characters look. Click the
video to pause it. Normal videos need ffmpeg (`winget install Gyan.FFmpeg`); the graphics chip decodes them
(*Hardware decoding*), only at the size they're shown and only while they're on screen. Videos have no sound.

## 6b4. Sound controls

An **audio** widget is a small volume mixer: the output device in use (click it to switch to the next one, e.g.
speakers to headphones, or set *Output device* to `list` to pick from all of them), a **master** slider and a
slider for **every app** making sound. Click a slider to set it, scroll over it to nudge it (*Scroll step*), and
click a name to mute it. The same things work as click actions anywhere, e.g. on a bar module:
`audio:device:next`, `audio:device:Headphones` (part of a name), `audio:app:Spotify:mute`, `audio:app:Spotify:+10`,
`volume:40`, `volume:+5`, `audio:mixer` (Windows' own mixer). `{audio_device}` is the output's name; the bar has a
ready-made *Sound output* module.

Actions also run from the command line, so you can put them on hotkeys (e.g. with PowerToys):
`Glyph.exe --action audio:device:next`.

## 6b5. Frames: box designs

A **frame** is a named box design kept with the theme (`frames.toml`, or the **Frames** page, which shows a live
preview: type a sample title and pick *Show as* panel, window, bar module or button). A frame can have:

- **Shape**: any corner cut off at 45° (*Cut top-left* … *Cut bottom-left*), rounded corners, or the whole box
  leaning sideways (*Slant*).
- **Fill**: a colour and see-through-ness, a fade to a second colour, a **pattern** over it (scanlines, grid, dots,
  diagonal lines, crosshatch, vertical lines) and a different fill under the mouse (*Hover fill*).
- **Border**: colour, width, dashes, only some sides, and a second **inner border**.
- **Title**: *plain* (top-left inside), *bar* (a strip across the top), *tab* (a tab sticking up from the top edge,
  with a slanted side), *line* (the title with a line running to the right) or *above* (over the box). Its font,
  size, capitals and letter spacing, and a second text on the right (*Title right* on a widget, e.g. `LIVE`).
- **Marks**: corner brackets ⌜ ⌟ on some or all corners, a filled triangle in one corner, a thick **stripe** on one
  side, a block of **hatch** lines ////// at a corner, **ruler ticks** along a side, a soft **glow** and a shadow.

Things pick a frame by name: a widget's *Frame* (or every panel through Colours & look > Smooth style > *Panel
frame*), buttons inside panels (*Button frame*), the bar (*Frame*, and *Module frame* for every module; a module's
own *Frame*, and *Join* to put a label, a meter and a value in one box), the current desktop's number (*Active
frame*), the launcher (Launcher look > Frames) and windows (6h). Frames are drawn in the smooth style; terminal
themes keep their character boxes.

Panels can also point at each other: give one a *Name* and another *Link to* that name, and a line (or two, with a
45° bend) joins them, like a callout. The **feed** widget shows lines that keep coming in, made from patterns with
made-up values (`{hex:4}`, `{num:10-99}`, `{pick:OK,LOCKED,ACK}`, `{seq:X4}`, `{spaces:0-6}`) and real ones, the
newest bright and older ones fading: hex dumps, logs and live-looking readouts.

The **operator** theme is built this way: cut-corner panels with title strips and brackets, hex feeds, a callout
tag, framed bar modules with block meters, a radio-style music panel with a visualiser, a framed launcher and a
tab frame around the window you're using.

## 6c. Widgets

The Widgets page lists everything on the desktop: shortcut panels, clock, system monitor, music player,
calendar, weather, text, command output, system info and pictures. Each sits against a corner or edge
(*Anchor*) and moves with *Offset X / Y*. They stay clear of the bar and the taskbar. In terminal themes
they're drawn with characters (big block digits, braille graphs, ASCII album art), in smooth themes as
panels with normal text, smooth bars and pictures.

## 6d. Values, colours and actions in text

Anywhere you can type text for the bar or a widget, you can use:

| Write | Shows |
|---|---|
| `{time}` `{time:HH:mm:ss}` `{time:h:mm tt}` | the time (any .NET time format) |
| `{date}` `{date:dddd d MMMM}` | the date |
| `{cpu}` `{cpu:0.0}` | CPU use in %; `{ram}` memory %, `{ram_used}`, `{ram_total}` |
| `{disk}` `{disk_free}` `{disk_total}` | the C: drive |
| `{net_down}` `{net_up}` | network speed |
| `{battery}` `{battery_state}` `{battery_icon}` | battery |
| `{volume}` `{muted}` `{volume_icon}` | speaker volume |
| `{media_title}` `{media_artist}` `{media_album}` `{media_app}` `{media_state}` `{media_icon}` `{media_position}` `{media_length}` | what's playing |
| `{window}` `{window:40}` | the active window's title (`:40` = at most 40 characters) |
| `{desktop}` `{desktops}` `{desktop_name}` | virtual desktop number, count, name |
| `{tiling}` | the tiling layout (dwindle, master, columns, rows, monocle) or off |
| `{wifi}` `{wifi_signal}` `{network}` | the Wi-Fi network's name, its signal 0–100, and wifi / ethernet / mobile / none |
| `{bluetooth}` | on, off, or empty without Bluetooth |
| `{lang}` `{lang_full}` | the keyboard language (EN, or en-GB) |
| `{user}` `{host}` `{os}` `{cpu_name}` `{gpu_name}` `{uptime}` | about the computer |
| `{temp}` `{feels}` `{weather}` `{weather_icon}` `{wind}` `{humidity}` `{high}` `{low}` `{city}` | weather (needs a weather widget with a city) |
| `{cpu|pad:3}` `{window|upper|max:30}` `{ram|bar:12}` | **filters** after `|` change how a value looks (see below) |
| `{my_value}` | one of your own variables (Variables page) |
| `[accent]text[/]` | colours "text" with a palette colour (or a hex code); `[/]` goes back |
| `\n` | a new line (in widgets) |

**Filters** (any number, one after another): `upper`, `lower`, `title`, `pad:N` (right-align in N characters),
`rpad:N`, `center:N`, `max:N` (cut with …), `fmt:0.0` (number format), `prefix:TEXT`, `suffix:TEXT`,
`default:TEXT` (when empty), `bar:N` or `bar:N:■□` (a text bar from a 0–100 value), `map:playing=▶,paused=❚❚`,
`replace:a=b`, `color>80:accent_bright` (colour the value when it's above 80; also `<`, `>=`, `<=`, `=`, `!=`),
`when<20:LOW` (replace the value when it's below 20). Example: `{cpu|pad:3|color>80:accent_bright}%`.

**Click actions** (On click, On right-click, On scroll up/down):

| Write | Does |
|---|---|
| `media:toggle` `media:next` `media:prev` | play/pause, next, previous track |
| `volume:mute` `volume:up` `volume:down` | volume |
| `desktop:next` `desktop:prev` `desktop:2` | switch virtual desktop |
| `keys:win+n` | presses keys (e.g. `keys:win` opens Start, `keys:win+n` the calendar and notifications) |
| `run:program arguments` | starts a program with arguments |
| `settings` | opens Glyph's settings |
| a path, website or `ms-settings:` page | opens it, like double-clicking |

## 6e. The launcher (Alt+Space)

**Alt+Space** opens a search box in the middle of the screen (press it again, or Esc, to close it). Type and
press **Enter** to open the top result; **Up/Down**, **Tab** or the mouse wheel pick another; click one to open
it. What it finds (General > Launcher):

- **apps** from the Start menu (including Store apps) and your **shortcuts**; letters can be initials
  (`vsc` finds Visual Studio Code) or a few letters in order. **Shift+Enter** opens an app as administrator.
- **Glyph commands:** `theme` (switch theme), `sound output` (speakers / headphones), `tiling`, `settings`,
  `pause`, `quit`, `lock`, `mute`, `next track`…
- **maths:** `12*7`, `(3+4)^2`, `sqrt(2)`, `15% of 80`. Enter copies the answer.
- **files and folders:** start with `C:\`, `~\` or `%appdata%\`; it lists what's in the folder as you type,
  **Tab** fills in the selected one.
- **commands:** start with `>`, e.g. `>cmd /k ipconfig`. **websites:** `github.com`.
- **web search** for anything else (General > Launcher > Web search).

Things you open often come first, and an empty search shows them. Ctrl+Backspace deletes a word, Ctrl+V pastes,
Ctrl+A clears. The launcher follows the theme (terminal or smooth); its size, colours and prompt are in each
theme's *Launcher look* (Colours & look page, or `[launcher]` in theme.toml). The Start menu is read by a small
helper program (`Glyph.exe --scan-apps`) that saves the app list and icons in `config\icons\cache`, so the
Windows code for that never stays in Glyph's memory. Closed for 20 seconds, the launcher gives its memory back.

## 6f. Tiling windows

With tiling on (**Win+Ctrl+Alt+T**, the tray menu, the launcher or General > Tiling), windows are arranged side
by side automatically and re-arranged whenever one opens, closes, is minimised or moves to another virtual
desktop. Layouts (**Win+Ctrl+Alt+N** for the next one):

- **dwindle**: each window splits the last one in half. **master**: one big window on the left, the rest stacked
  on the right. **columns** / **rows**: equal strips. **monocle**: every window full size.

| Keys | Does |
|---|---|
| Win+Ctrl+Alt+arrows | move the focus to the window in that direction (in monocle: the next / previous window) |
| Win+Alt+Shift+arrows | swap the window with the one in that direction |
| Win+Ctrl+Alt+Enter | make the window the main one |
| Win+Ctrl+Alt+F | float it (it comes to the middle and stays where you put it); again to tile it |
| Win+Ctrl+Alt+minus / equals | give the main window less / more room |

With the mouse: drag a window onto another to swap them; drag the main window's edge to change its share.
Dialogs, windows that can't be resized and always-on-top windows float by themselves, and so does a window that
can't get small enough for its tile. *Float these* / *Ignore these* take patterns like `process:steam.exe`,
`class:#32770` or `title:*Picture-in-Picture*`. The window you're using gets a border in the theme's accent
(Active border), and *Corners* can make windows square. Turning tiling off leaves windows where they are and puts
their borders back. Apps run as administrator can't be moved by Glyph (it isn't one): they float.

## 6g. Hotkeys

The Hotkeys page lists key combinations that work anywhere (also in `config\hotkeys.toml`). **Add hotkey…**
records the keys you press; *Does* is any action: `launcher`, the `tile:` ones, `theme:soft`, `volume:+5`,
`audio:device:next`, `media:toggle`, `window:close`, `run:notepad`, a website… A hotkey another program already
uses shows *taken by another program*: pick other keys. Other programs can run actions too:
`Glyph.exe --action tile:layout:master`.

## 6h. Window frames

A theme can draw a frame around your windows (Colours & look > *Window frames*): *Mode* `focused` frames the window
you're using, `tiled` every window tiling arranges. *Frame* (and *Other windows' frame*) pick designs from the Frames
page; a frame with a **tab** title shows the window's name in it. *Title* is a format with `{process}` (the program,
e.g. WindowsTerminal), `{title}` and `{n}` (the window's number), so
`{process|map:WindowsTerminal=TTY,firefox=NET|upper}.{n:00}` shows `TTY.01`. Windows' own border can be hidden and
their corners made square. Maximised and full-screen windows have no frame (there's no room). With tiling on, each
tile leaves room for its frame (*Room for frames when tiling*). Frames are click-through and only redrawn when a
window changes size or focus.

## 7. The converter

Turns a video, GIF or picture into an ASCII clip for a *clip* layer. Open it from the tray menu,
the General tab, or drop a file on `Glyph Converter.lnk`.

1. Pick or drop a file. A preview appears in your theme's colours.
2. Choose a mode: **shape** (best detail), **ramp** (classic), **edges** (outlines), **blocks** (chunky pixels), **braille** (finest dots).
3. Adjust brightness, contrast, gamma; *Invert* if the subject is dark on light. Drag *Preview at* to check other moments.
4. *Loop*: clips repeat on the wallpaper, so by default the last second fades into the start (**crossfade**; *Fade*
   sets how long) and the jump back to the beginning disappears. **bounce** plays it forwards, then backwards; **off**
   keeps it as it is.
5. *Convert*. The clip goes in `clips\`. Tick *Also save an MP4* for a video you can share.
6. *Add to wallpaper* adds a clip layer to the theme in use.

From a terminal: `bin\Glyph.exe --convert myvideo.mp4 --mode braille --length 10` (and `--help`).
It needs ffmpeg (`winget install Gyan.FFmpeg.Essentials` if it's ever missing).

## 8. Editing the files by hand

Every file is plain TOML with a note next to each option:

```toml
# a note
name = "text"        # text in quotes
size = 14            # a number
on = true            # true or false
colors = ["bg", "accent"]   # a list
[section]            # options below belong to this section
[[layer]]            # one of many (each [[layer]] is another layer)
```

If something's wrong, a red box at the top of the desktop says the file, the line and what's wrong
(misspelt option names are caught too). Fix it and save; until then, anything unreadable uses its default.
The settings window rewrites the files it changes (with the notes), so your own notes in those files may
be replaced.

## 9. Performance

- Glyph draws with the graphics card, rests when the desktop can't be seen (fullscreen games,
  maximised windows, lock screen, battery saver) and stops drawing entirely when nothing moves.
- Measured here: red-mono (animated) about 6% of one CPU core at 30 fps; soft (still) under 1%.
  About 70 MB of its own memory.
- To save more: lower the wallpaper's frames per second, lower *update fps* on noise layers, or use fewer layers.

## 10. If something's wrong

- **Desktop icons gone and Glyph isn't running:** right-click the desktop > View > Show desktop icons.
- **Wallpaper black or missing:** tray menu > *Quit*, then start Glyph again. (It also reattaches by itself when Explorer restarts.)
- **Nothing changes when I save:** tray menu > *Reload*, and check *Open log*.
- **Want Wallpaper Engine back:** quit Glyph (or turn off *Close Wallpaper Engine*) and start it.


""";

    const string Outro = """
---

*This guide's option tables are generated from Glyph itself (`bin\Glyph.exe --write-guide`).*
""";
}
