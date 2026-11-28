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

1. Download `Glyph-Setup-x.y.z.exe` from the newest release on GitHub (github.com/exinics/glyph/releases) and run
   it: pick the folder, Start menu shortcuts, "start when I sign in". It gets the .NET 9 desktop runtime if needed,
   and offers to bring the settings of a Glyph you used before. (Or the zip: unzip anywhere, run `Glyph\bin\Glyph.exe`;
   needs `winget install Microsoft.DotNet.DesktopRuntime.9`.)
2. For videos and the converter: `winget install Gyan.FFmpeg.Essentials`.
3. The first start sets up the **operator** theme and imports your desktop shortcuts into `config`.
   Settings > General > *Start with Windows* turns starting by itself on or off.

**Updates** (General > Updates): `auto` looks on GitHub 30 seconds after Glyph starts and every 6 hours, downloads a
newer version and restarts into it (a few seconds); `ask` shows a note and an *Update* item in the tray menu; `off`
never looks. *Check for updates* in the tray menu looks straight away (`Glyph.exe --update` from a terminal).
Updates only replace `bin\`: your `config` folder (settings, themes, shortcuts) is never touched. What happened is in
`logs\update-result.txt`.

## 1c. More than one screen

General > **Screens** says what goes on each display:

- **Mode** `same`: the theme on every screen (each gets its own wallpaper, widgets and bar). `each`: a different
  theme on each screen (*Screen 1 theme* … *Screen 4 theme*; `auto` = the theme in use, `none` = leave that screen
  alone). `one`: only the *Main screen*; the others keep Windows' own wallpaper.
- **Main screen**: `primary` (Windows' main display) or a display number, as in Windows' Settings > System >
  Display (*Identify* shows them).
- **Bar on** / **Widgets on**: `every` screen, or the `main` one only.
- **Follow Windows scaling** (on): each screen is drawn at its own Windows scale (Settings > System > Display >
  Scale), so a 150% screen gets everything half as big again: panels, text, the bar, the launcher and window frames.
  Off = theme sizes are exact pixels everywhere.

Each screen rests on its own: a maximised window or a game on one screen doesn't stop the other. Plugging a display
in or out is picked up within a second. Theme sync, window frames and the launcher follow the main screen's theme.

## 2. The settings window

| Page | What's there |
|---|---|
| **Design** | A live preview of the theme you're editing. Click a **part** of a widget (a tab, a list item, a panel's title strip, its contents, a button…), the bar, a bar module or the background to change it on the right: **Properties** shows just that part's options (*Whole widget* shows them all), **Look** the frame the part is drawn with (edit it there; it tells you what else uses that frame, and *Give this part its own copy* splits it off), **Animations** the widget's tweens (6i). Then drag widgets to move them, drag the corner square to resize, and *Draw box ▾* to drag out a new box and choose what's in it. *Animate* lets the wallpaper and mouse effects play. The colour squares at the bottom change the theme's colours. *Pick colour* is an eyedropper: click anywhere on the preview to put that colour in the palette (as the accent with matching shades, or any colour). *Wallpaper colours…* finds the wallpaper's main colours and offers an accent or a whole palette made from them. |
| **Frames** | The theme's box designs (see 6b5), with a live preview. *New frame ▾* starts from a ready-made design. |
| **Themes** | Your themes. *Use this theme* shows it on the desktop. *New theme…* and *Duplicate…* make a copy to change, so you never break a theme you like. Rename, delete, open the folder, or *Restore the built-in theme* to get operator back as it came. Clicking a theme picks it for editing in the other tabs (the line at the top says which one you're editing). |
| **Colours & look** | The **palette** (every colour in the theme; double-click to change one, or type a hex code) and the theme's options: its **style**, the **character font**, the **smooth style** (panel colour and see-through-ness, corner roundness, border, shadow, font) and the **screen effects** (glow, scanlines…). |
| **Wallpaper** | The **backdrop** (a colour, a gradient or a picture), the **mouse effect**, and the **layers**: add, remove, duplicate and reorder them, and change the selected one below. |
| **Shortcuts** | Your shortcuts (shared by every theme). Add programs, files, folders or websites, *Import desktop icons*, reorder them, give one *your own picture*, or go back to the app's icon. Also: *Hide Windows' own desktop icons*. |
| **Bar** | The bar: turn it on, where it sits, its size and look, and its modules (left, centre, right) with what clicking or scrolling them does. |
| **Variables** | Your own values (built from other values, a command's output or a file), plus a box to try any format and see the live result. |
| **Widgets** | Everything on the desktop: shortcut panels, clock, system monitor, music, calendar, weather, text, command output, system info, pictures, videos, sound controls. *Add widget ▾*, then change the selected one. |
| **General** | Start with Windows, closing Wallpaper Engine, when to rest to save power; start / quit / restart Glyph, the converter, folders. |

At the bottom of each list of options there's a short explanation of whichever option you click. Options are grouped
with where a thing sits first and its box and links last; switches read On / Off, and every colour option shows its
colour beside the name.

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

**The built-in theme:** `operator` (smooth: a dark navy HUD with teal lines, cut-corner panels, hex feeds, a framed
bar, a framed launcher and window frames). It's set up the first time Glyph starts; make your own from it with
*Duplicate…*. Updates never change your themes.

## 4. Colours

Colours are defined once, in the theme's palette. Everywhere else uses their **names**, so changing one
hex code recolours everything that uses it. Where an option asks for a colour you can give:

- a palette name: `accent`
- a hex code: `#E0202F`, or `#E0202F80` where the last two digits are see-through (00 = invisible, FF = solid)
- `none`: fully transparent
- for a gradient, a list from faint/dark to strong/bright: `bg, accent_dim, accent, bright`

### operator palette (the built-in theme)

bg `#060C12` · surface `#0A1620` · line `#173039` · dim `#4C6B74` · text `#9DBBC0` · bright `#E4F2F2` ·
accent `#5DB4BC` · accent_dim `#1F4A51` · accent_bright `#93E6EA` · alert `#E5363E` · warn `#E3A847` · glow `#0D2230`

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
- **Groups**: give shortcuts a *Group* (e.g. Games, Study) and a shortcuts panel can show one group, a bar *icons*
  module can show one, and a **menu** widget shows every group as a tab (6c). *Groups (menu tabs)…* on the Shortcuts
  page sets each group's tab order, icon (`{icon:name}`), subtitle and a sentence about it (`[[group]]` tables in
  `shortcuts.toml`). A shortcut's *Description* is shown by menus.

## 6. The wallpaper

The wallpaper is the **backdrop** with **layers** of characters on top, drawn in order (later layers on
top; empty parts let the ones below show). The **mouse effect** lights up characters near the mouse.

If nothing in the wallpaper moves (no animated layers, mouse effect off), Glyph stops drawing frames and
only redraws when something changes, like the mouse moving over a shortcut.

Smooth themes can lay up to two **patterns** over the whole backdrop (Wallpaper > Backdrop > *Pattern* and
*Pattern 2*): dots, crosses, scanlines, a grid, vertical or diagonal lines, crosshatch, each with its own colour,
see-through-ness, spacing and thickness. A dot grid with faint scanlines over it gives a printed-paper look.
*Tint* lays a colour over the picture or gradient (*Tint amount*, and *Tint fade* to make it strongest at the bottom
or top), e.g. a red-violet wash over a city photo at night.

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

**Glyph's bar as the taskbar.** Set General > *Windows taskbar* to `hide` and put the bar at the bottom: it then
does the taskbar's jobs, each a module you can style like any other:
- **Start**: the *Start menu button* preset (`keys:win`; right-click `keys:win+x`), plus *Notifications and calendar*
  (`keys:win+n`), *Quick settings* (`keys:win+a`) and *Show desktop* (`keys:win+d`) presets.
- **Pinned apps**: an *icons* module with your shortcuts' names (or a shortcuts group).
- **Open windows**: the **tasks** module (below).
- **Tray icons**: the **tray** module shows the notification-area icons (Discord, a VPN, Glyph's own…), which are
  otherwise out of sight while Windows' taskbar is hidden. Left-click, double-click and right-click reach the program
  just as on Windows' taskbar. *Leave out* hides icons by program name or tooltip. While a bar shows a tray module,
  Glyph keeps a hidden tray window above Explorer's so programs send their icons to Glyph too; everything is passed
  on to Explorer, so its own tray stays complete and nothing changes when Glyph quits.

The **tasks** module lists the open windows like the taskbar does (icon and program name or window title); click
one to bring it to the front, click the one in front to minimise it. *Group by program* gives one button per
program (`{count}` = its windows; clicking again goes to its next window), *Right-click* can close, minimise or
maximise, and *Leave out* skips programs by name. Any module can have a **Caption**: a small
label above (or under) it, e.g. `APPS`, or `{window_app}` under the window title. Give a module a frame with only a
left border (*Border sides* = left) to split the bar into sections with thin lines.

## 6b2. Boxes and the background

Every widget is a box with its own **Box** options: colour, opacity, corner radius, border, shadow and a hover
colour (if it does something on click). Text, clock, system and the other info widgets also take a fixed
**Width** and **Height** and **Vertical align**, so you can draw an exact box on the Design page and fill it.

The **background** can be interactive too (Wallpaper > Interaction, or click the background on the Design page):
what clicking, double-clicking and scrolling on empty desktop does (e.g. scroll for volume, double-click to open a
folder), a **ripple** where you click, and a **mouse trail**. Wallpaper layers can have **Parallax X / Y** so they
shift against the mouse for a feeling of depth. Dragging on empty desktop draws a **selection box** like
Windows' blue one (Windows only draws its own while its icons are showing): Wallpaper > Interaction > *Selection box*
sets its colour, see-through-ness and border, or *Box frame* draws it with a frame from the Frames page.

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
  diagonal lines, crosshatch, vertical lines, crosses; *Pattern size* = how thick) and a different fill under the
  mouse (*Hover fill*). *Hover wipe* makes that fill slide in from the left (menus animate it). *Text colour* and
  *Hover text* set the colour of the text inside (bar modules, buttons), e.g. light text on a dark plate.
- **Border**: colour, width, dashes, only some sides, and a second **inner border**.
- **Title**: *plain* (top-left inside), *bar* (a strip across the top), *tab* (a tab sticking up from the top edge,
  with a slanted side), *line* (the title with a line running to the right) or *above* (over the box). Its font,
  size, capitals and letter spacing, and a second text on the right (*Title right* on a widget, e.g. `LIVE`). *Line
  ticks* puts small ruler marks along a line or above title's line.
- **Marks**: corner brackets ⌜ ⌟ on some or all corners, small filled **corner squares** on all four corners, a
  filled triangle in one corner, a thick **stripe** on one
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

*Stretch* (Placement) makes a box reach the screen's edges: `width` runs from *Offset X* on the left to *Offset X*
on the right, `height` from *Offset Y* at the top to *Offset Y* at the bottom, `both` does both. An empty text widget
stretched to `both` with a frame is a border around the whole screen. Text widgets also have *Letter spacing* and a
*Text shadow* (the text printed again a little lower right); the clock has letter spacing for both lines.

Every widget has **Transform** options (smooth style): *Tilt X / Y* lean it in 3D (Tilt Y 14 = its right side goes
back, -14 = its left side; *Depth* = how strong the perspective is), *Rotate* turns it flat (e.g. -90 for sideways
text), and *Opacity* fades the whole widget (0.12 = a faint watermark). *Tilt around* names another widget: widgets
that share a Tilt and the same Tilt around lean together as one surface (e.g. cards on a tilted board).

**Things that answer the mouse** (smooth style), on any widget or theme:
- *Tilt to mouse* (Transform): the widget leans towards the mouse while it's over it, as if pressed, and settles back.
- A frame's *Spotlight*: a soft light follows the mouse across the box and lights up its edge near the mouse
  (*Spotlight size / strength / edge*). Every box drawn with that frame gets it.
- *Hover text* (info panels): extra lines, with {values}, that the panel grows to show while the mouse is over it
  (*Reveal time*); a growing panel is drawn over its neighbours.
- Shortcuts panels: *Magnify* swells the icons under the mouse like a dock, and *Name on hover* shows the name in a
  small tag above the icon.
- **toggles**: a grid of quick buttons (icon | label | click action | when it's on, e.g. `{muted}`, `{bluetooth}`):
  buttons rise and glow under the mouse (*Hover lift*, *Hover time*) and are lit while on (*On frame*, *On colour*).
- Hover tweens (6i) and the menu's wipe, as before.

- **menu**: a whole menu screen made from your shortcut groups (5). The groups are tabs across the top (with the
  group's icon in a little box, spread across the width or packed), a ruled line under them (`band` = line, dashes
  and dot triangles), the chosen group's name in big letters with its subtitle and sentence, its shortcuts as a list
  of plates, and a details panel about the chosen one (what it is, where it points, *Open* and *Show in folder*).
  A click on a tab or a shortcut chooses it; a double click opens a shortcut (or one click with *Open with* =
  click). The dark fill slides in under the mouse over *Hover wipe* milliseconds. Scroll over the list when it's
  long, or over the tabs to change tab. Tabs and plates use plain colours (Tab colour, Active colour…) or frames
  (*Tab frame*, *Item frame*, *Details frame*), and *Active tab frame* / *Active item frame* fade in a second frame
  over the chosen one (e.g. a bright outline). *Tab layout* `column` puts the tabs in a column down the left and *Tab
  style* `icons` shows just each group's icon; *List layout* `cards` shows the shortcuts as a grid of cards (icon,
  name, description) and *App icons* uses each shortcut's own icon. *Heading text* replaces the tab's name with your
  own words and values, e.g. `{greeting}, {user}`; *Heading line* the small line under it. The menu draws its *Frame*
  (auto = the theme's panel frame) behind everything, with *Width* / *Height* when it isn't stretched.
- **themes**: cards for your themes, each with its wallpaper as a picture, a few of its colours (*Colour dots*) and its
  name; the one in use is marked, and clicking another switches to it.
- **shortcuts** with *Names* shows just those shortcuts, in that order: with no panel, no labels and a single row it's
  a dock.
- **divider**: a line across or down the screen: solid, dashed, dotted, double, or `band`. With *Stretch* it runs
  from edge to edge.

Together these make, for example, a printed field-manual look: a ruled screen border with corner squares, a dotted
ground with scanlines, a search box that opens the launcher, a centred clock, a menu of the shortcut groups, a
status panel, and a ruled bar at the bottom with the window in front, the open windows and a tray section.

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
| `{window_app}` | the program whose window is in front (e.g. Firefox) |
| `{greeting}` | Good morning / Good afternoon / Good evening / Good night, by the time of day |
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

### Icons and fonts in text

- **Icons:** `{icon:name}` anywhere text is shown (bar modules, widget text and titles, frame titles, the media
  buttons' labels) draws an icon from Google's Material Symbols, e.g. `{icon:home}` `{icon:wifi}` `{icon:play_arrow}`
  `[accent]{icon:favorite}[/]`. Settings > **Icons** shows all of them with a search: click one to copy its code.
  Names are the ones on fonts.google.com/icons. Icons take the colour and size of the text around them.
  Each theme's Colours & look sets *Icon style* (outlined, rounded, sharp: rounded and sharp are downloaded once into
  `config\fonts` the first time they're used), *Filled icons* and *Icon size*. In terminal themes an icon fills one
  character cell.
- **Fonts:** every font setting has a list to pick from: Windows' fonts plus any font files in `config\fonts`.
  Settings > **Fonts** > *Add font files…* takes `.ttf`/`.otf` files or a whole download from fonts.google.com (the
  `.zip`). They're only used by Glyph (nothing is installed into Windows). Each widget can have its own font and size
  (Box > Font, Font size; smooth style).

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
theme's *Launcher look* (Colours & look page, or `[launcher]` in theme.toml). Its *Frames* options give a framed look (heading, numbered rows in frames, a curve, small type tags); *Mark boxes* put a character at the end of each result, picked by *Mark rules* like `steam|game=遊` or `kind:folder=庫` (the operator theme has a set of kanji). The Start menu is read by a small
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

## 6i. Animations (tweens)

Widgets can move, grow, turn and fade with **tweens**, the same idea as Roblox's TweenService: a tween goes from
how the widget normally looks to a **goal** (*Move X / Y* in pixels, *Scale*, *Rotate* in degrees, *Opacity*) over
a *Time*, with an **easing style** (`linear`, `sine`, `quad`, `cubic`, `quart`, `quint`, `exponential`, `circular`,
`back`, `elastic`, `bounce`) and **direction** (`in`, `out`, `inout`), after a *Delay*. Goals can also *Tilt X / Y* in
3D, and *Face front* undoes the widget's own tilt (a tilted panel turns to face you on hover). The **trigger** says when:

- `appear`: when Glyph starts (or the tweens change), coming in from the goal: Move Y 40 + Opacity 0 = rises and
  fades in. Saving other settings doesn't replay it.
- `hover`: to the goal while the mouse is over the widget, back when it leaves (e.g. Move Y -4 = lifts).
- `click`: to the goal and back once (Scale 0.95 = a press).
- `loop`: over and over: *Repeat* more times (-1 = for ever), *Reverses* = there and back each time.

Easiest on the Design page: pick a widget > **Animations** > *Add ▾* (fade in, rise in, slide in, pop in, lift or
grow on hover, bounce on click, bob, breathe, spin), change it on the right, *▶ Play* to see it. They're kept per theme
in `animations.toml` (`[[tween]]` tables). *Target* is a widget's *Name* (adding a tween names the widget if it has
none), `type:clock` for every widget of a type, or `*` for every widget; with several, *Stagger* starts each one a
little after the one before. Several tweens on one widget add up. Animations are drawn in smooth themes; clicks and
hover still use the widget's normal place. `loop` tweens keep the desktop drawing frames (a little power), the rest
only while they play. The menu's hover wipe has its own *Wipe easing* and *Wipe direction*.

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
- Measured here: operator about 2% of one CPU core (its feeds and meters keep changing), about 77 MB of its own
  memory. A theme where nothing moves uses under 1%.
- **Presets** (General > More > Performance): *plugged in*, *on battery* and *battery saver* each pick highest,
  balanced, low or pause. Highest = the screen's full refresh rate, effects on, smoke worked out every frame, videos at
  full speed; balanced = each theme as it's made; low = 15 fps, no screen effects, less detail. Every preset can be
  changed (`[performance.highest]` etc.).
- To save more: lower the wallpaper's frames per second, lower *update fps* on noise layers, or use fewer layers.

## 10. If something's wrong

- **Desktop icons gone and Glyph isn't running:** right-click the desktop > View > Show desktop icons.
- **Wallpaper black or missing:** tray menu > *Quit*, then start Glyph again. (It also reattaches by itself when Explorer restarts.)
- **Nothing changes when I save:** tray menu > *Reload*, and check *Open log*.
- **Want Wallpaper Engine back:** quit Glyph (or turn off *Close Wallpaper Engine*) and start it.

## Every option

The settings window shows all of these with the same descriptions. In the files, the option name is the
lower-case name in the first column; a section like `[ui]` is the part of the file it lives in.

### glyph.toml (Glyph itself)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `theme` | `"operator"` |  | Which theme folder in config\themes is shown |
| `start_with_windows` | `false` | true, false | **Start with Windows.** Start Glyph when you sign in. |
| `close_wallpaper_engine` | `true` | true, false | Old setting, no longer used (see Wallpaper Engine for themes / Wallpaper Engine under a theme). |
| `wallpaper_engine_themes` | `"ask"` | ask, open, never | **Wallpaper Engine for themes.** When a theme shows a Wallpaper Engine wallpaper under its widgets and Wallpaper Engine isn't showing it: ask = ask first (the theme shows the wallpaper's still picture until then). open = open it in Wallpaper Engine (starting it if needed) and say so. never = never start or change Wallpaper Engine (the still picture is shown). |
| `wallpaper_engine_hidden` | `"ask"` | ask, close, remove, leave | **Wallpaper Engine under a theme.** When Wallpaper Engine plays a wallpaper on a screen where the theme draws its own background (so you can't see it, but it still uses your graphics card): ask = ask what to do. close = try to close Wallpaper Engine (it's never forced: if it stays open, its hidden wallpapers are taken off instead). remove = take its wallpaper off just those screens (it keeps running for the others). leave = leave it alone. |
| `hide_desktop_icons` | `true` | true, false | **Hide Windows desktop icons.** Hide Windows' own desktop icons while Glyph runs (shortcut panels replace them). They come back when Glyph quits. |
| `taskbar` | `"keep"` | keep, autohide, hide | **Windows taskbar.** keep = leave it as it is. autohide = hide it until the mouse touches the screen edge. hide = hide it completely (Glyph's bar replaces it; the Start menu still opens with the Win key). Restored when Glyph quits. |
| `theme_sync` | `true` | true, false | **Theme sync.** Let themes change Windows Terminal's colours and Windows' accent colour and dark mode (each theme's [sync] says what). Off = put all of it back as it was. |
| `updates` | `"ask"` | ask, auto, off | **Updates.** ask = a notification when there's a new version: click it (or Update in the tray menu) to update. auto = get new versions by itself (Glyph restarts for a moment). off = never look. |
| `update_repo` | `"exinics/glyph"` |  | **Update from.** The GitHub project new versions come from (owner/name). |

### glyph.toml [performance]

Pick a preset for each power state; each preset below can be changed.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `plugged_in` | `"highest"` | highest, balanced, low, pause | **Plugged in.** Preset while the PC runs on mains power (a desktop PC always uses this). pause = don't draw at all. |
| `on_battery` | `"balanced"` | highest, balanced, low, pause | **On battery.** Preset while unplugged. |
| `battery_saver` | `"pause"` | highest, balanced, low, pause | **Battery saver.** Preset while Windows battery saver (energy saver) is on. |
| `pause_when_fullscreen` | `true` | true, false | **Pause when fullscreen.** Stop drawing while a game or video is fullscreen. |
| `pause_when_maximized` | `true` | true, false | **Pause when maximised.** Stop drawing while maximised windows cover the desktop. |
| `pause_when_locked` | `true` | true, false | **Pause when locked.** Stop drawing on the lock screen. |

### glyph.toml [performance.highest]

Everything at the screen's full frame rate and full quality.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `fps` | `"max"` |  | **Frame rate.** max = as fast as the screen shows frames (its refresh rate, e.g. 60 or 144), whatever the theme says. theme = each theme's own frame rate. A number = the theme's frame rate, but no more than this. |
| `wallpaper_fps` | `"theme"` |  | **Wallpaper animation.** Frame rate while the only thing moving is the wallpaper's own animation (ASCII sparks, rain, smoke, stars...). theme = the theme's own frame rate (e.g. 20 or 30: these move a character at a time, so more looks the same and costs far more). max = the screen's refresh rate. A number = no more than this. The mouse, widgets, animations and dragging still use Frame rate above. |
| `effects` | `true` | true, false | **Screen effects.** Draw the theme's screen effects (bloom, scanlines, curvature...). Off saves the most work for the graphics chip. |
| `animation_detail` | `"max"` | max, theme, low | **Animation detail.** max = smoke/noise worked out fresh every frame and videos at their own full frame rate. theme = as each theme sets them. low = smoke worked out half as often, videos at most 15 frames a second. |
| `smooth_dragging` | `true` | true, false | **Smooth dragging.** The selection box and the launcher run at the screen's refresh rate even when the frame rate above is lower (drawing them costs next to nothing). Off = they keep to the frame rate above. |

### glyph.toml [performance.balanced]

Each theme as it's made.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `fps` | `"theme"` |  | **Frame rate.** max = as fast as the screen shows frames (its refresh rate, e.g. 60 or 144), whatever the theme says. theme = each theme's own frame rate. A number = the theme's frame rate, but no more than this. |
| `wallpaper_fps` | `"theme"` |  | **Wallpaper animation.** Frame rate while the only thing moving is the wallpaper's own animation (ASCII sparks, rain, smoke, stars...). theme = the theme's own frame rate (e.g. 20 or 30: these move a character at a time, so more looks the same and costs far more). max = the screen's refresh rate. A number = no more than this. The mouse, widgets, animations and dragging still use Frame rate above. |
| `effects` | `true` | true, false | **Screen effects.** Draw the theme's screen effects (bloom, scanlines, curvature...). Off saves the most work for the graphics chip. |
| `animation_detail` | `"theme"` | max, theme, low | **Animation detail.** max = smoke/noise worked out fresh every frame and videos at their own full frame rate. theme = as each theme sets them. low = smoke worked out half as often, videos at most 15 frames a second. |
| `smooth_dragging` | `true` | true, false | **Smooth dragging.** The selection box and the launcher run at the screen's refresh rate even when the frame rate above is lower (drawing them costs next to nothing). Off = they keep to the frame rate above. |

### glyph.toml [performance.low]

Slow and simple, for the longest battery.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `fps` | `"15"` |  | **Frame rate.** max = as fast as the screen shows frames (its refresh rate, e.g. 60 or 144), whatever the theme says. theme = each theme's own frame rate. A number = the theme's frame rate, but no more than this. |
| `wallpaper_fps` | `"15"` |  | **Wallpaper animation.** Frame rate while the only thing moving is the wallpaper's own animation (ASCII sparks, rain, smoke, stars...). theme = the theme's own frame rate (e.g. 20 or 30: these move a character at a time, so more looks the same and costs far more). max = the screen's refresh rate. A number = no more than this. The mouse, widgets, animations and dragging still use Frame rate above. |
| `effects` | `false` | true, false | **Screen effects.** Draw the theme's screen effects (bloom, scanlines, curvature...). Off saves the most work for the graphics chip. |
| `animation_detail` | `"low"` | max, theme, low | **Animation detail.** max = smoke/noise worked out fresh every frame and videos at their own full frame rate. theme = as each theme sets them. low = smoke worked out half as often, videos at most 15 frames a second. |
| `smooth_dragging` | `false` | true, false | **Smooth dragging.** The selection box and the launcher run at the screen's refresh rate even when the frame rate above is lower (drawing them costs next to nothing). Off = they keep to the frame rate above. |

### glyph.toml [launcher] (what the launcher finds)

See 6e.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `apps` | `true` | true, false | Apps from the Start menu (including Store apps). |
| `shortcuts` | `true` | true, false | Your Glyph shortcuts (Shortcuts page). |
| `commands` | `true` | true, false | Glyph commands: switch theme, sound output, tiling layout, settings, pause, quit… |
| `calculator` | `true` | true, false | Maths: type 12*7, (3+4)^2, sqrt(2), 15% of 80. Enter copies the answer. |
| `paths` | `true` | true, false | **Files and folders.** Type a path (C:\, ~\, %appdata%\…) to open it; shows what's in the folder as you type. |
| `run` | `true` | true, false | **Run commands.** Start with > to run a command, e.g. >notepad or >cmd /k ipconfig. |
| `web_search` | `"https://www.google.com/search?q={q}"` |  | **Web search.** The web search offered under the results; {q} is what you typed. Empty = none. |
| `remember` | `true` | true, false | **Learn favourites.** Things you open often come first, and an empty search shows them. |

### glyph.toml [tiling]

See 6f.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `false` | true, false | **Tiling on.** Arrange windows side by side automatically, like i3 or Hyprland. Win+Ctrl+Alt+T turns it on and off (Hotkeys page). |
| `layout` | `"dwindle"` | dwindle, master, columns, rows, monocle | master = one big window on the left, the rest stacked on the right. dwindle = each new window splits the last one in half. columns / rows = equal strips. monocle = every window full size (switch between them with the focus keys). |
| `ratio` | `0.5` | 0.1 to 0.9 | **Main window share.** How much of the screen the first window gets (master and dwindle). Win+Ctrl+Alt+minus / equals change it, and so does dragging the first window's edge. |
| `gap` | `10` | 0 to 200 | Space between windows, in pixels. |
| `outer_gap` | `10` | 0 to 400 | **Outer gap.** Space between the windows and the edge of the screen, in pixels. |
| `new_windows` | `"after_focused"` | after_focused, end, start | **New windows.** Where a new window goes: after_focused = next to the window you're using, end = after all the others, start = becomes the main window. |
| `active_border` | `"accent"` | colour name or hex | **Active border.** Border colour of the window you're using (Windows 11). none = Windows' own. |
| `inactive_border` | `"none"` | colour name or hex | **Inactive border.** Border colour of the other tiled windows. none = Windows' own. |
| `corners` | `"keep"` | keep, square, round, small | Window corners while tiling: keep = Windows' rounding, square, round, small. |
| `float` | `["class:#32770", "class:OperationStatusWindow", "title:*Picture-in-Picture*", "title:*Picture in picture*", "process:PowerToys*"]` |  | **Float these.** Windows that stay where you put them instead of being tiled. Each line matches class:Name, process:name.exe, title:Text, or just text in the title. * matches anything, e.g. title:*Picture-in-Picture*. |
| `ignore` | `[]` |  | **Ignore these.** Windows tiling never touches at all (no border either). Same patterns as Float these. |

### hotkeys.toml [[hotkey]]

See 6g.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `keys` | `""` |  | The keys, e.g. alt+space, win+alt+left, ctrl+shift+f5. Modifiers: win, ctrl, alt, shift. Keys: a-z, 0-9, f1-f24, left, right, up, down, space, enter, tab, esc, backspace, delete, insert, home, end, pageup, pagedown, minus, equals, comma, period, slash, backslash, semicolon, quote, backtick, [, ], numpad0-9, printscreen, pause. |
| `action` | `""` |  | **Does.** What it does: launcher, tile:toggle, tile:focus:left, tile:move:right, tile:float, tile:layout:next, tile:ratio:+5, theme:soft, plus every click action (media:toggle, volume:+5, audio:device:next, run:program, a file or website…). |
| `enabled` | `true` | true, false | Turn this hotkey on or off. |

### theme.toml [info]

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `description` | `""` |  | A short note about the theme. |
| `style` | `"terminal"` | terminal, smooth | terminal = everything drawn as characters. smooth = rounded panels, normal fonts and pictures. |

### theme.toml [font] (characters)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `font` | `"Cascadia Mono"` |  | Any installed monospace font, e.g. Cascadia Mono, JetBrains Mono, Consolas (or one in config\fonts: the Fonts page). |
| `font_size` | `14` | 6 to 96 | **Font size.** In pixels. Smaller = more, finer characters. |
| `font_weight` | `"normal"` | thin, light, normal, medium, semibold, bold, black | **Font weight.**  |
| `line_spacing` | `1` | 0.5 to 3 | **Line spacing.** 1.0 = normal. |
| `cell_width` | `0` | 0 to 200 | **Cell width.** Force a cell width in pixels. 0 = from the font. |
| `cell_height` | `0` | 0 to 400 | **Cell height.** Force a cell height in pixels. 0 = from the font. |

### theme.toml [ui] (smooth style)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `font` | `"Segoe UI"` |  | Font for text in the smooth style: any installed font, or one in config\fonts (the Fonts page). |
| `font_size` | `13` | 6 to 96 | **Font size.** In pixels. |
| `bold_titles` | `true` | true, false | **Bold titles.** Panel titles in bold. |
| `icon_style` | `"outlined"` | outlined, rounded, sharp | **Icon style.** Look of icons written as {icon:name} anywhere (Google's Material Symbols; browse them on the Icons page). Rounded and sharp are downloaded once, the first time they're used. |
| `icon_fill` | `false` | true, false | **Filled icons.** Solid icons instead of outlines. |
| `icon_size` | `1.15` | 0.5 to 3 | **Icon size.** Size of icons next to the text they're in: 1 = the text's size. |
| `corner_radius` | `12` | 0 to 64 | **Corner radius.** Roundness of panel corners, in pixels. |
| `panel_color` | `"surface"` | colour name or hex | **Panel colour.**  |
| `panel_opacity` | `0.85` | 0 to 1 | **Panel opacity.** 0 = invisible, 1 = solid. |
| `border_color` | `"line"` | colour name or hex | **Border colour.**  |
| `border_width` | `1` | 0 to 16 | **Border width.** In pixels. 0 = no border. |
| `shadow` | `0.35` | 0 to 1 | Soft shadow under panels. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Hover highlights, titles. |
| `padding` | `12` | 0 to 100 | Space inside panels, in pixels. |
| `panel_frame` | `"none"` |  | **Panel frame.** A frame (Frames page) for every panel whose Frame is auto. none = plain panels. |
| `button_frame` | `"none"` |  | **Button frame.** A frame for buttons in panels (e.g. media controls). none = plain buttons. |
| `halo` | `"none"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind every widget's text and shapes, so light text stays readable on a light wallpaper (e.g. black). none = no halo. Each widget can change it (Glow & halo). |
| `halo_size` | `6` | 0.5 to 60 | **Halo size.** How far the halo spreads, in pixels. |
| `halo_strength` | `0.7` | 0 to 2 | **Halo strength.** How dark (strong) it is: 0 = none, 1 = full, up to 2 = a second, tighter layer for very light wallpapers. |
| `glow` | `"none"` |  | **Glow.** Smooth style: things light up like neon or an old screen. self = each thing glows in its own colours, or a colour = everything glows in that colour. none = no glow. Each widget can change it (Glow & halo). |
| `glow_size` | `8` | 0.5 to 80 | **Glow size.** How far the glow spreads, in pixels. |
| `glow_strength` | `0.8` | 0 to 3 | **Glow strength.** How bright the glow is (up to 3 = blinding). |

### theme.toml [effects]

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | **Effects on.** Turn all screen effects on or off. |
| `bloom` | `0.5` | 0 to 5 | Glow around bright things. |
| `bloom_threshold` | `0.35` | 0 to 0.99 | **Bloom threshold.** How bright something must be to glow. |
| `bloom_radius` | `1` | 0.1 to 6 | **Bloom radius.** How far the glow spreads. |
| `scanlines` | `0.12` | 0 to 1 | Dark horizontal lines like an old monitor. |
| `scanline_size` | `3` | 1 to 32 | **Scanline size.** Pixels from one scanline to the next. |
| `vignette` | `0.3` | 0 to 2 | Darker corners. |
| `flicker` | `0.02` | 0 to 1 | Slight brightness flicker. |
| `grain` | `0.03` | 0 to 1 | Film grain. |
| `aberration` | `0.5` | 0 to 20 | Red/blue fringing towards the edges, in pixels. |
| `curvature` | `0` | 0 to 1 | Bulge like a curved CRT screen. |
| `brightness` | `1` | 0 to 4 | Overall brightness. |
| `contrast` | `1` | 0 to 4 | Overall contrast. |

### theme.toml [sync] (Windows Terminal, accent colour, dark mode)

What this theme also changes outside Glyph (see 4b).

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `terminal` | `true` | true, false | **Windows Terminal.** Give Windows Terminal this theme's colours: a colour scheme called "Glyph" that every profile uses, and matching tabs. Turning it off puts Terminal back as it was. |
| `terminal_background` | `"bg"` | colour name or hex | **Terminal background.**  |
| `terminal_text` | `"text"` | colour name or hex | **Terminal text.**  |
| `terminal_cursor` | `"accent"` | colour name or hex | **Terminal cursor.**  |
| `terminal_selection` | `"accent_dim"` | colour name or hex | **Terminal selection.** Background of selected text. |
| `terminal_tab_bar` | `"surface"` | colour name or hex | **Terminal tab bar.** The strip behind the tabs (the open tab takes the terminal's background). |
| `terminal_font` | `""` |  | **Terminal font.** Empty = leave Terminal's font alone. theme = this theme's character font. Or any installed monospace font. |
| `terminal_font_size` | `0` | 0 to 72 | **Terminal font size.** In points. 0 = leave it alone. |
| `terminal_opacity` | `100` | 0 to 100 | **Terminal opacity.** 100 = solid. Lower lets the desktop show through. |
| `terminal_blur` | `false` | true, false | **Terminal blur.** Blur what shows through (acrylic). Needs opacity under 100. |
| `terminal_crt` | `"effects"` | off, retro, effects | **Terminal CRT effect.** off = none. retro = Windows Terminal's own glow and scanlines. effects = this theme's screen effects (scanlines, vignette, curvature, colour fringing, brightness, contrast) while they're on; still, so it costs nothing when nothing changes. |
| `terminal_cursor_shape` | `"keep"` | keep, bar, vintage, underscore, doubleUnderscore, filledBox, emptyBox | **Terminal cursor shape.**  |
| `accent` | `true` | true, false | **Windows accent colour.** Set Windows' accent colour (Start, toggles, buttons, links, focus outlines) from this theme. Turning it off puts the old one back. |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.**  |
| `accent_shades` | `"theme"` | theme, windows | **Accent shades.** Windows uses lighter and darker shades of the accent too. theme = made from Accent light and Accent dark below. windows = Windows works them out (it may change them for readability). |
| `accent_light` | `"accent_bright"` | colour name or hex | **Accent light.** The lightest shade (dark mode shows these on toggles and links). |
| `accent_dark` | `"accent_dim"` | colour name or hex | **Accent dark.** The darkest shade. |
| `accent_on_taskbar` | `"keep"` | keep, on, off | **Accent on Start and taskbar.** keep = leave Windows' setting. Windows only allows it in dark mode. |
| `accent_on_title_bars` | `"keep"` | keep, on, off | **Accent on title bars.** Colour window title bars and borders with the accent. keep = leave Windows' setting. |
| `dark_mode` | `"auto"` | auto, dark, light, keep | **Dark mode.** auto = dark if this theme's background is dark. keep = leave Windows' setting. Sets both apps and Windows itself. |

### theme.toml [launcher] (how the launcher looks)

See 6e.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `width` | `680` | 200 to 3000 | Width in pixels. |
| `max_results` | `8` | 1 to 20 | **Results shown.**  |
| `top` | `0.22` | 0 to 0.8 | **From the top.** Where it opens: 0 = top of the screen, 0.5 = middle. |
| `scale` | `1.15` | 0.5 to 4 | Smooth style: text size compared with the theme's. |
| `prompt` | `""` |  | Shown before what you type. Empty = > in terminal themes, › in smooth ones. |
| `placeholder` | `"Search apps, shortcuts, files, maths…"` |  | Shown while nothing is typed. |
| `icons` | `true` | true, false | Smooth style: the apps' own icons. |
| `background` | `"surface"` | colour name or hex |  |
| `opacity` | `1` | 0 to 1 | 0 = invisible, 1 = solid. |
| `border_color` | `"accent"` | colour name or hex | **Border colour.**  |
| `border_width` | `1` | 0 to 16 | **Border width.**  |
| `corner_radius` | `-1` | -1 to 64 | **Corner radius.** -1 = the theme's panel corners. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** The placeholder and the kind of each result. |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** The prompt, the cursor and the letters that matched. |
| `selected_color` | `"accent_dim"` | colour name or hex | **Selected colour.** Behind the selected result. |
| `selected_text` | `"bright"` | colour name or hex | **Selected text.**  |
| `selected_match` | `"auto"` | colour name or hex | **Selected match colour.** The letters you typed, in the selected result. auto = the accent colour, or the selected text colour when the accent would be hard to see on the selected colour. |
| `frame` | `"none"` |  | Smooth style: a frame (Frames page) for the whole launcher instead of the plain box above. none = plain. |
| `row_frame` | `"none"` |  | **Row frame.** A frame for each result. none = plain rows. |
| `selected_frame` | `"auto"` |  | **Selected frame.** A frame for the selected result. auto = the row frame. |
| `row_gap` | `4` | 0 to 50 | **Row gap.** Pixels between framed rows. |
| `numbers` | `false` | true, false | Numbers in front of the results (1, 2, 3...). |
| `number_frame` | `"none"` |  | **Number frame.** A frame for the numbers. none = just the number. |
| `curve` | `0` | -300 to 300 | Bend the list: rows near the top and bottom move right by up to this many pixels (negative = left), the middle stays. |
| `kind_below` | `false` | true, false | **Kind under the name.** Show what each result is (app, file...) as a small tag under its name instead of on the right. |
| `heading` | `""` |  | A big heading above the search line, e.g. APPS. Empty = none. |
| `heading_note` | `""` |  | **Heading note.** Small text after the heading. |
| `heading_font` | `""` |  | **Heading font.** Empty = the theme's font. |
| `heading_color` | `"auto"` | colour name or hex | **Heading colour.** auto = the selected text colour, or the text colour when that would be hard to see on the background. |
| `top_line` | `""` |  | **Top line.** A small line at the very top. {count} = how many results. Empty = none. |
| `bottom_line` | `""` |  | **Bottom line.** A small line at the bottom, e.g. key hints. {count} = how many results, {selected} = which one is selected. Empty = none. |
| `spacing` | `0` | -5 to 50 | **Letter spacing.** Extra pixels between letters of the heading, top and bottom lines. |
| `marks` | `false` | true, false | **Mark boxes.** A small box at the right end of each result with a character showing what kind of thing it is (e.g. 遊 for games, 音 for music). Smooth style. |
| `mark_rules` | `["kind:folder\|kind:file=▤", "kind:web\|kind:website=◎", "kind:copy=＝", "kind:command=›", "kind:sound\|kind:media=♪", "kind:windows=⊞", "kind:theme\|kind:glyph\|kind:tiling=◆"]` |  | **Mark rules.** Which character each result gets: words=character. The words (split with \|) are looked for in the result's name and what it opens; kind:app, kind:folder, kind:theme… match the kind of result. The first rule that matches wins. E.g. steam\|game\|play=遊, music\|spotify\|audio=音, kind:folder=档. |
| `mark_default` | `"▪"` |  | **Other results.** The character for results no rule matches. Empty = no box for them. |
| `mark_frame` | `"auto"` |  | **Mark frame.** A frame (Frames page) for the boxes. auto = the number frame, none = just the character. |
| `mark_color` | `"dim"` | colour name or hex | **Mark colour.**  |
| `mark_selected_color` | `"bright"` | colour name or hex | **Selected mark colour.**  |

### theme.toml [windows] (window frames)

See 6h.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `mode` | `"off"` | off, focused, tiled | Which windows get a frame. off = none. focused = the window you're using. tiled = every window tiling arranges, and the one you're using. |
| `frame` | `"none"` |  | The frame (Frames page) for the window you're using. |
| `inactive_frame` | `"auto"` |  | **Other windows' frame.** tiled: the frame for the other windows. auto = the same frame, none = no frame on them. |
| `title` | `"{process\|upper}"` |  | The frame's title (in a tab or strip, if the frame has one). {process} = the program (e.g. WindowsTerminal), {title} = the window's title, {n} = the window's number. Filters work, e.g. {process\|map:WindowsTerminal=TTY\|upper}.{n:00} |
| `gap` | `2` | 0 to 50 | Pixels between the window and its frame. |
| `hide_border` | `true` | true, false | **Hide window borders.** Hide Windows' own thin border on framed windows. |
| `square_corners` | `true` | true, false | **Square corners.** Make framed windows square-cornered (Windows rounds them). |
| `tiling_room` | `true` | true, false | **Room for frames when tiling.** Tiling leaves space around each window for its frame (e.g. a title tab above it). |

### frames.toml [[frame]] (in a theme folder)

Box designs, picked by name (see 6b5).

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `name` | `"frame"` |  | The name other things use to pick this frame. |
| `padding` | `-1` | -1 to 200 | Space between the frame and what's inside, in pixels. -1 = the theme's panel padding. |
| `padding_x` | `-1` | -1 to 200 | **Padding across.** Space at the left and right if different. -1 = same as Padding. |
| `cut_top_left` | `0` | 0 to 400 | **Cut top-left.** Cut this corner off at 45°, this many pixels. 0 = a normal corner. |
| `cut_top_right` | `0` | 0 to 400 | **Cut top-right.**  |
| `cut_bottom_right` | `0` | 0 to 400 | **Cut bottom-right.**  |
| `cut_bottom_left` | `0` | 0 to 400 | **Cut bottom-left.**  |
| `radius` | `0` | -1 to 200 | **Corner radius.** Round the corners that aren't cut. -1 = the theme's. |
| `slant` | `0` | -200 to 200 | Lean the whole box sideways (like / or \), in pixels across its height. Text stays upright. |
| `fill` | `"surface"` | colour name or hex | Colour inside. none = see-through. |
| `fill_opacity` | `0.9` | 0 to 1 | **Fill opacity.**  |
| `fill2` | `"none"` | colour name or hex | **Fill to.** A second colour: the fill fades from Fill to this one. none = one colour. |
| `fill_angle` | `90` | -360 to 360 | **Fade angle.** Direction of the fade, in degrees (90 = top to bottom). |
| `pattern` | `"none"` | none, scanlines, grid, dots, crosses, diagonal, crosshatch, vlines | A pattern over the fill. |
| `pattern_color` | `"line"` | colour name or hex | **Pattern colour.**  |
| `pattern_opacity` | `0.35` | 0 to 1 | **Pattern opacity.**  |
| `pattern_spacing` | `4` | 2 to 200 | **Pattern spacing.** Pixels between the pattern's lines or dots. |
| `pattern_size` | `1` | 0.5 to 50 | **Pattern size.** Thickness of the pattern's lines or dots, in pixels. |
| `hover_fill` | `"none"` | colour name or hex | **Hover fill.** Fill colour under the mouse, when it does something on click. none = no change. |
| `text_color` | `"none"` | colour name or hex | **Text colour.** Colour of the text inside (bar modules, buttons). none = the text's own colour. Set it for dark fills on a light theme (or the other way round). |
| `hover_text` | `"none"` | colour name or hex | **Hover text.** Colour of the text inside (bar modules, buttons) while the mouse is over it. none = unchanged. Set it when Hover fill is close to the text colour. |
| `spotlight` | `"none"` | colour name or hex | A soft light that follows the mouse across the box and brightens its edge near the mouse. none = no spotlight. Smooth style. |
| `spotlight_size` | `180` | 10 to 2000 | **Spotlight size.** Radius of the light, in pixels. |
| `spotlight_strength` | `0.14` | 0 to 1 | **Spotlight strength.** How bright the light is inside the box. |
| `spotlight_edge` | `0.8` | 0 to 1 | **Spotlight edge.** How much the box's edge lights up near the mouse. |
| `hover_wipe` | `0` | 0 to 2000 | **Hover wipe.** The hover fill slides in from the left over this many milliseconds instead of appearing at once. 0 = at once. Used by menus and lists that animate. |
| `border` | `"line"` | colour name or hex | Border colour. none = no border. |
| `border_width` | `1` | 0 to 50 | **Border width.**  |
| `border_opacity` | `1` | 0 to 1 | **Border opacity.**  |
| `border_sides` | `"all"` | all, top, bottom, left, right, top_bottom, left_right, top_left, bottom_right | **Border sides.** Which sides get the border. |
| `dash` | `0` | 0 to 100 | **Dashes.** Dash length for a dashed border. 0 = solid. |
| `inner_border` | `"none"` | colour name or hex | **Inner border.** A second line inside the first. none = no second line. |
| `inner_gap` | `3` | 0 to 100 | **Inner gap.** Pixels between the border and the inner border. |
| `inner_opacity` | `0.6` | 0 to 1 | **Inner opacity.**  |
| `header` | `"plain"` | plain, bar, tab, line, underline, legend, above, none | Where the title goes. plain = top-left inside. bar = a strip across the top. tab = a tab sticking up from the top edge. line = the title with a line running to the right. underline = the title with a thin line across the whole box under it (a section heading). legend = the title in the middle of the top border, the border broken around it (a fieldset). above = over the box with a line. none = no title. |
| `title_color` | `"accent"` | colour name or hex | **Title colour.**  |
| `title_right_color` | `"dim"` | colour name or hex | **Right text colour.** For the text on the right of the title (Title right). |
| `title_font` | `""` |  | **Title font.** Empty = the theme's font. |
| `title_size` | `0` | 0 to 200 | **Title size.** In pixels. 0 = the theme's title size. |
| `title_bold` | `true` | true, false | **Bold title.**  |
| `title_upper` | `false` | true, false | **Capitals.** Show the title in capitals. |
| `title_spacing` | `0` | -5 to 50 | **Letter spacing.** Extra pixels between the title's letters. |
| `header_fill` | `"accent_dim"` | colour name or hex | **Strip colour.** bar and tab: the strip's colour. |
| `header_opacity` | `1` | 0 to 1 | **Strip opacity.**  |
| `header_height` | `0` | 0 to 200 | **Strip height.** bar and tab: height of the strip. 0 = fit the title. |
| `header_line` | `"none"` | colour name or hex | **Line under title.** A line under the title strip. none = no line. |
| `line_ticks` | `0` | 0 to 60 | **Line ticks.** line and above titles: this many small upright marks spread along the title's line (the ends get one too), like a ruler. 0 = a plain line. |
| `line_tick_height` | `7` | 1 to 60 | **Tick height.** Height of the line's marks, in pixels. |
| `tab_width` | `0` | 0 to 2000 | **Tab width.** tab: width of the tab. 0 = fit the title. |
| `tab_slant` | `10` | 0 to 200 | **Tab slant.** tab: how far the tab's right side leans out, in pixels. |
| `tab_offset` | `0` | 0 to 2000 | **Tab offset.** tab: distance from the box's left edge. |
| `brackets` | `"none"` | colour name or hex | Corner brackets ⌜ ⌟ drawn over the corners. none = no brackets. |
| `bracket_corners` | `"all"` | all, top, bottom, left, right, top_left_bottom_right, top_right_bottom_left | **Bracket corners.**  |
| `bracket_length` | `10` | 1 to 400 | **Bracket length.**  |
| `bracket_width` | `2` | 0.5 to 20 | **Bracket width.**  |
| `bracket_offset` | `3` | -50 to 50 | **Bracket offset.** Move the brackets out from the corners (negative = in). |
| `corner_mark` | `"none"` | colour name or hex | **Corner mark.** A small filled triangle in one corner. none = no mark. |
| `mark_corner` | `"bottom_right"` | top_left, top_right, bottom_left, bottom_right | **Mark corner.**  |
| `mark_size` | `8` | 1 to 200 | **Mark size.**  |
| `corner_squares` | `"none"` | colour name or hex | **Corner squares.** Small filled squares centred on all four corners. none = no squares. |
| `corner_square_size` | `6` | 1 to 100 | **Square size.**  |
| `stripe` | `"none"` | colour name or hex | A thick stripe along one side. none = no stripe. |
| `stripe_side` | `"left"` | left, right, top, bottom | **Stripe side.**  |
| `stripe_width` | `3` | 0.5 to 100 | **Stripe width.**  |
| `stripe_gap` | `0` | -50 to 100 | **Stripe gap.** Pixels between the box and the stripe (0 = on the edge). |
| `hatch` | `"none"` | colour name or hex | A block of diagonal stripes ////// at a corner. none = no hatch. |
| `hatch_corner` | `"top_left"` | top_left, top_right, bottom_left, bottom_right | **Hatch corner.**  |
| `hatch_outside` | `true` | true, false | **Hatch outside.** Put the hatch just outside the box (above or below it) instead of inside. |
| `hatch_width` | `36` | 2 to 1000 | **Hatch width.**  |
| `hatch_height` | `5` | 2 to 200 | **Hatch height.**  |
| `ticks` | `"none"` | colour name or hex | Ruler ticks along one side. none = no ticks. |
| `ticks_side` | `"bottom"` | top, bottom, left, right | **Ticks side.**  |
| `tick_spacing` | `6` | 2 to 200 | **Tick spacing.**  |
| `tick_length` | `3` | 1 to 100 | **Tick length.** Every fifth tick is twice as long. |
| `glow` | `"none"` | colour name or hex | A soft glow around the box. none = no glow. |
| `glow_size` | `8` | 1 to 100 | **Glow size.**  |
| `glow_opacity` | `0.35` | 0 to 1 | **Glow strength.**  |
| `shadow` | `0` | -1 to 1 | A soft shadow under the box. -1 = the theme's. |

### animations.toml [[tween]] (in a theme folder)

Animations for widgets (see 6i).

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `name` | `"tween"` |  | A name for this animation (only for you). |
| `target` | `""` |  | Which widget(s) it plays on: a widget's Name, type:clock for every widget of a type, or * for every widget. |
| `trigger` | `"appear"` | appear, hover, click, loop | When it plays. appear = when the theme loads, coming in FROM the goal (e.g. Move Y 40 + Opacity 0 = rises and fades in). hover = goes to the goal while the mouse is over it, back when it leaves. click = to the goal and back once when clicked. loop = keeps playing (see Repeat and Reverses). |
| `time` | `0.4` | 0.01 to 60 | **Time.** How long one play takes, in seconds. |
| `style` | `"quad"` | linear, sine, quad, cubic, quart, quint, exponential, circular, back, elastic, bounce | **Easing style.** The shape of the motion: linear = steady, sine/quad/cubic/quart/quint = gentle to strong slow-downs, exponential and circular = sharp, back = overshoots a little, elastic = springs, bounce = bounces. |
| `direction` | `"out"` | in, out, inout | **Easing direction.** out = fast start, gentle end (most natural for things arriving). in = gentle start, fast end. inout = gentle at both ends. |
| `delay` | `0` | 0 to 60 | Seconds to wait before it starts. |
| `stagger` | `0` | 0 to 10 | With several targets (type:… or *): each one starts this many seconds after the one before, so they come in one after another. |
| `repeat` | `-1` | -1 to 1000 | loop: how many more times it plays after the first. -1 = for ever. |
| `reverses` | `true` | true, false | loop: play back to the start after reaching the goal (each play goes there and back). |
| `move_x` | `0` | -4000 to 4000 | **Move X.** Pixels to the right at the goal (negative = left). |
| `move_y` | `0` | -4000 to 4000 | **Move Y.** Pixels down at the goal (negative = up). |
| `scale` | `1` | 0 to 10 | Size at the goal: 1 = as it is, 1.05 = 5% bigger, 0.8 = smaller. From the widget's centre. |
| `opacity` | `1` | 0 to 1 | See-through-ness at the goal: 1 = as it is, 0 = invisible. |
| `rotate` | `0` | -3600 to 3600 | **Rotate.** Degrees turned at the goal (clockwise). |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Extra 3D lean at the goal, in degrees (positive = top goes back). |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Extra 3D turn at the goal, in degrees (positive = right side goes back). |
| `untilt` | `false` | true, false | **Face front.** At the goal the widget's own tilt (Transform > Tilt X / Y) is undone, so a tilted panel turns to face you (e.g. on hover). |

### wallpaper.toml (top)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `fps` | `30` | 1 to 240 | **Frames per second.** How smoothly it animates. Lower = less power. A wallpaper with nothing moving only redraws when needed. |

### wallpaper.toml [backdrop]

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `type` | `"solid"` | solid, gradient, image, wallpaper_engine | solid = one colour, gradient = a blend of colours, image = a picture, wallpaper_engine = a live wallpaper played by Wallpaper Engine (pick it below) with Glyph's widgets and bar on top. |
| `engine` | `""` |  | **Wallpaper Engine wallpaper.** wallpaper_engine: which of your Wallpaper Engine wallpapers plays under this theme (Workshop subscriptions and your own). A Workshop id, a title or a folder can be typed too. Glyph starts Wallpaper Engine for it, and closes it again for themes that don't use one. Previews and snapshots show the wallpaper's preview picture. |
| `engine_mute` | `true` | true, false | **Mute Wallpaper Engine.** wallpaper_engine: silence the wallpaper's own sound. |
| `color` | `"bg"` | colour name or hex | The colour for solid (and behind a picture). |
| `colors` | `["bg", "surface"]` | colour name or hex | Gradient colours, first to last. |
| `angle` | `90` | 0 to 360 | Gradient direction in degrees: 0 = left to right, 90 = top to bottom. |
| `shape` | `"linear"` | linear, radial | Gradient shape. |
| `file` | `""` |  | Picture file (PNG/JPG). A file name is looked for in the theme folder and config\images. |
| `fit` | `"cover"` | cover, contain, stretch, center | How the picture fills the screen. |
| `dim` | `0` | 0 to 1 | Darken the picture (0 = not at all, 1 = black). |
| `tint` | `"none"` | colour name or hex | **Tint.** A colour laid over the picture or gradient (smooth themes), e.g. a red or violet wash for a night mood. none = no tint. |
| `tint_amount` | `0.25` | 0 to 1 | **Tint amount.** How strong the tint is: 0 = none, 1 = only the tint colour. |
| `tint_fade` | `"none"` | none, bottom, top | **Tint fade.** Fade the tint towards the top or bottom of the screen: none = even, bottom = strongest at the bottom, top = strongest at the top. |
| `pattern` | `"none"` | none, scanlines, grid, dots, crosses, diagonal, crosshatch, vlines | **Pattern.** A pattern drawn over the whole backdrop (smooth themes): dots, lines, a grid… none = no pattern. |
| `pattern_color` | `"line"` | colour name or hex | **Pattern colour.**  |
| `pattern_opacity` | `0.5` | 0 to 1 | **Pattern opacity.**  |
| `pattern_spacing` | `24` | 2 to 400 | **Pattern spacing.** Pixels between the pattern's lines or dots. |
| `pattern_size` | `2` | 0.5 to 50 | **Pattern size.** Thickness of the lines or dots, in pixels. |
| `pattern2` | `"none"` | none, scanlines, grid, dots, crosses, diagonal, crosshatch, vlines | **Pattern 2.** A second pattern on top of the first (e.g. faint scanlines over dots). none = no second pattern. |
| `pattern2_color` | `"line"` | colour name or hex | **Pattern 2 colour.**  |
| `pattern2_opacity` | `0.15` | 0 to 1 | **Pattern 2 opacity.**  |
| `pattern2_spacing` | `3` | 2 to 400 | **Pattern 2 spacing.**  |
| `pattern2_size` | `1` | 0.5 to 50 | **Pattern 2 size.**  |

### wallpaper.toml [cursor] (mouse effect)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Turn the mouse effect on or off. |
| `radius` | `8` | 0.5 to 200 | Size of the area around the mouse, in characters. |
| `strength` | `0.6` | 0 to 1 | How much characters near it take its colour. |
| `glow` | `0.12` | 0 to 1 | Tint of the background around it. |
| `scatter` | `0` | 0 to 1 | Chance characters near it scramble. |
| `scatter_chars` | `"#%&*+=-:."` |  | **Scatter characters.** What they scramble into. |
| `color` | `"accent_bright"` | colour name or hex |  |

### wallpaper.toml [interaction] (clicks, scrolling, ripples, trail)

What empty desktop does when you click, double-click or scroll it, and the click ripple and mouse trail.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `on_click` | `""` |  | **On click.** Clicking empty desktop. What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_double_click` | `""` |  | **On double-click.** Double-clicking empty desktop. |
| `on_scroll_up` | `""` |  | **On scroll up.** Scrolling up over empty desktop, e.g. volume:up or desktop:prev. |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `ripple` | `true` | true, false | A ring spreads out where you click. |
| `ripple_color` | `"accent"` | colour name or hex | **Ripple colour.**  |
| `ripple_size` | `220` | 10 to 3000 | **Ripple size.** How far it spreads, in pixels. |
| `ripple_duration` | `0.9` | 0.1 to 10 | **Ripple time.** Seconds it lasts. |
| `ripple_chars` | `"#*+:·"` |  | **Ripple characters.** Terminal style: characters it's drawn with, from start to end. |
| `trail` | `false` | true, false | The mouse leaves a fading trail on the desktop. |
| `trail_color` | `"accent_dim"` | colour name or hex | **Trail colour.**  |
| `trail_duration` | `0.6` | 0.05 to 10 | **Trail time.** Seconds each mark lasts. |
| `trail_chars` | `"*+:·."` |  | **Trail characters.** Terminal style: newest to oldest. |
| `select_box` | `true` | true, false | **Selection box.** Drag on empty desktop to draw a box, like Windows' blue one (Windows only draws its own while its desktop icons are showing). |
| `select_frame` | `"none"` |  | **Box frame.** Draw it with a frame from the Frames page instead of the colours below. none = the colours below. |
| `select_color` | `"accent"` | colour name or hex | **Box colour.** Inside of the box. |
| `select_opacity` | `0.18` | 0 to 1 | **Box opacity.** How see-through the inside is (0 = clear, 1 = solid). |
| `select_border` | `"accent"` | colour name or hex | **Box border.**  |
| `select_border_width` | `1` | 0 to 20 | **Box border width.** In pixels (terminal style: any value draws a line of characters). |

### wallpaper.toml [[layer]] type = "noise"

Flowing smoke or clouds made of characters.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `chars` | `" .:-=+*#%@"` |  | Characters from faintest to strongest. Spaces = empty. |
| `gradient` | `["bg", "accent_dim", "accent", "bright"]` | colour name or hex | Colours from faint to strong. |
| `scale` | `0.05` | 0 to 10 | Size of the shapes: smaller = bigger, softer blobs. |
| `speed` | `0.1` | -100 to 100 | How fast the shapes change. |
| `drift_x` | `0` | -100 to 100 | **Drift X.** Steady sideways movement, characters per second. |
| `drift_y` | `0` | -100 to 100 | **Drift Y.** Steady up/down movement, characters per second. |
| `warp` | `0.5` | 0 to 10 | Swirliness (0 = plain clouds). |
| `octaves` | `3` | 1 to 8 | Detail. |
| `contrast` | `1` | 0 to 20 |  |
| `brightness` | `0` | -1 to 1 |  |
| `threshold` | `0` | 0 to 0.99 | Below this it's empty. Raise it for sparse wisps. |
| `invert` | `false` | true, false | Swap faint and strong. |
| `update_fps` | `6` | 0.5 to 240 | **Update FPS.** Times per second the smoke is worked out; frames in between blend. Lower = less CPU. |

### wallpaper.toml [[layer]] type = "rain"

Falling (or rising) streams of characters.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `chars` | `"0123456789ABCDEF:;<>=+-*/"` |  |  |
| `density` | `0.25` | 0 to 20 | Streams per column (0.25 = 1 in 4 columns). |
| `speed_min` | `8` | 0.1 to 500 | **Speed min.** Characters per second. |
| `speed_max` | `20` | 0.1 to 500 | **Speed max.**  |
| `length_min` | `6` | 1 to 500 | **Length min.**  |
| `length_max` | `24` | 1 to 500 | **Length max.**  |
| `head` | `"bright"` | colour name or hex | Colour of the leading character. |
| `gradient` | `["accent_dim", "accent"]` | colour name or hex | Tail end, then near the head. |
| `change_rate` | `4` | 0 to 120 | **Change rate.** How often characters flip, per second. |
| `direction` | `"down"` | down, up |  |
| `fade` | `true` | true, false | Tails fade out. |

### wallpaper.toml [[layer]] type = "stars"

A starfield: flying through it, or drifting sideways.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `mode` | `"warp"` | warp, drift | warp = fly through the stars, drift = they slide sideways. |
| `direction` | `"left"` | left, right, up, down | Drift mode direction. |
| `count` | `400` | 0 to 20000 |  |
| `speed` | `0.3` | 0 to 50 |  |
| `chars` | `".·+*"` |  | Far to near. |
| `gradient` | `["line", "dim", "text", "bright"]` | colour name or hex | Far to near. |

### wallpaper.toml [[layer]] type = "static"

TV static: random characters that flicker.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `density` | `0.02` | 0 to 1 | Share of cells with static. |
| `rate` | `12` | 0.1 to 240 | Changes per second. |
| `chars` | `".:'`,-"` |  |  |
| `gradient` | `["line", "dim"]` | colour name or hex |  |

### wallpaper.toml [[layer]] type = "glitch"

Jolts the layers *above it in the file* sideways for a moment. Put it after the layers it should glitch.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `rate` | `0.3` | 0 to 100 | Glitches per second. |
| `duration` | `0.15` | 0.01 to 10 | Seconds each lasts. |
| `max_shift` | `6` | 0 to 500 | **Max shift.** How far rows jump, in characters. |
| `band_min` | `1` | 1 to 500 | **Band min.** Rows per glitch. |
| `band_max` | `6` | 1 to 500 | **Band max.**  |
| `scramble` | `0.3` | 0 to 1 | Chance characters turn into the glitch characters. |
| `chars` | `'#%&@$!?/\\|<>'` |  |  |
| `tint` | `0.5` | 0 to 1 | How much glitched characters take the colour. |
| `color` | `"accent_bright"` | colour name or hex |  |

### wallpaper.toml [[layer]] type = "art"

ASCII art from a text file (logos, banners). For big text, search for a "text to ASCII art" generator; the GLYPH logo uses the "ANSI Shadow" font.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `file` | `""` |  | A text file with ASCII art (looked for in the theme folder and config\art). |
| `anchor` | `"center"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right |  |
| `offset_x` | `0` |  | **Offset X.** Move it, in characters (negative = left). |
| `offset_y` | `0` |  | **Offset Y.** Move it, in characters (negative = up). |
| `color` | `"accent"` | colour name or hex | Colour (if no gradient). |
| `gradient` | `[]` | colour name or hex | Optional gradient across the art. Empty = use Color. |
| `gradient_direction` | `"vertical"` | vertical, horizontal, diagonal | **Gradient direction.**  |
| `transparent_spaces` | `true` | true, false | **Transparent spaces.** Spaces in the art let layers below show. |
| `background` | `"none"` | colour name or hex | Fill behind the art, e.g. bg to blank out what's under it. |
| `padding` | `1` | 0 to 50 | Extra blanked space around it (rows; twice as many columns). |
| `pulse` | `0` | 0 to 1 | Gentle breathing. |
| `pulse_speed` | `1` | 0 to 50 | **Pulse speed.** Breaths per second. |
| `glitch` | `0` | 0 to 50 | Lines jump sideways, times per second. |

### wallpaper.toml [[layer]] type = "clip"

Plays an ASCII animation made with the converter.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this layer. |
| `opacity` | `1` | 0 to 1 | Fades the layer's characters (0 to 1). |
| `parallax_x` | `0` | -100 to 100 | **Parallax X.** The layer shifts this many characters against the mouse (left-right), for depth. 0 = still. |
| `parallax_y` | `0` | -100 to 100 | **Parallax Y.** Same, up-down. |
| `file` | `""` |  | A .gclip made by the converter (looked for in the theme folder and config\clips). |
| `fit` | `"cover"` | contain, cover, stretch, none | cover fills the screen (may crop), contain shows all of it, none = actual size. |
| `scale` | `1` | 0.01 to 10 | Size multiplier. |
| `anchor` | `"center"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right |  |
| `offset_x` | `0` |  | **Offset X.**  |
| `offset_y` | `0` |  | **Offset Y.**  |
| `speed` | `1` | 0 to 20 | Playback speed. |
| `loop` | `true` | true, false | Off = play once and stop on the last frame. |
| `colors` | `"gradient"` | gradient, original, single | gradient = theme colours by brightness, original = the video's colours, single = one colour. |
| `gradient` | `["bg", "accent_dim", "accent", "bright"]` | colour name or hex | Dark to bright. |
| `color` | `"accent"` | colour name or hex | For colors = single. |
| `threshold` | `0` | 0 to 1 | Cells darker than this are empty, so layers below show. |
| `brightness` | `1` | 0 to 10 |  |
| `chars` | `""` |  | If set, re-picks every character by brightness from this list (faintest first). |

### bar.toml (the bar)

The bar along the top (or bottom) of the screen.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show the bar. |
| `position` | `"top"` | top, bottom, left, right | Which edge of the screen. left / right = a side bar (smooth themes): the Left modules go at its top, Center in the middle, Right at the bottom, and rows of icons stand in columns. |
| `height` | `34` | 10 to 400 | Height in pixels (terminal themes round this to whole rows of characters). For a side bar: its width. |
| `halo` | `"none"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the bar's modules. auto = the theme's, none = off. |
| `glow` | `"none"` |  | **Glow.** Smooth style: the modules light up: self = in their own colours, a colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `reserve_space` | `true` | true, false | **Reserve space.** Windows (and maximised windows) stop at the bar instead of going under it. |
| `margin` | `0` | 0 to 200 | Gap between the bar and the screen edges, in pixels. Above 0 the bar floats. |
| `corner_radius` | `0` | 0 to 100 | **Corner radius.** Round the bar's corners (pixels). Looks best with a margin. |
| `background` | `"surface"` | colour name or hex | Bar colour. |
| `opacity` | `1` | 0 to 1 | 0 = see-through, 1 = solid. |
| `blur` | `false` | true, false | Blur what's behind the bar (frosted glass). Use with opacity below 1. |
| `border_color` | `"line"` | colour name or hex | **Border colour.**  |
| `border_width` | `0` | 0 to 20 | **Border width.** A line along the bar's inner edge, in pixels (terminal: any value draws a line of characters). |
| `font` | `""` |  | Font for the bar. Empty = the theme's (character font in terminal themes). |
| `font_size` | `0` | 0 to 96 | **Font size.** 0 = the theme's. |
| `padding` | `12` | 0 to 400 | Space at the left and right ends, in pixels. |
| `gap` | `18` | 0 to 400 | Space between modules, in pixels. |
| `module_background` | `"none"` | colour name or hex | **Module background.** A pill behind every module. none = no pills (a module's own Background wins). |
| `module_radius` | `8` | 0 to 100 | **Module radius.** Roundness of module pills (smooth style). |
| `effects` | `true` | true, false | **Screen effects.** Use the theme's glow, scanlines and grain on the bar too. |
| `frame` | `"none"` |  | A frame (Frames page) for the whole bar instead of the plain colour above. none = plain. Smooth style. |
| `module_frame` | `"none"` |  | **Module frame.** A frame drawn around every module (or group of joined modules). none = no frames. Smooth style. |
| `module_padding` | `8` | 0 to 100 | **Module padding.** Space inside a module frame, left and right, in pixels. |
| `module_inset` | `3` | 0 to 100 | **Module inset.** Space between a module frame and the top and bottom of the bar, in pixels. |
| `join_gap` | `8` | 0 to 100 | **Joined gap.** Space between modules that share a frame (Join), in pixels. |
| `hover_color` | `"accent"` | colour name or hex | **Hover colour.** The highlight under the mouse on modules that do something on click (without a frame). |

### bar.toml [[left]] / [[center]] / [[right]] type = "text"

Text with {values}. Most modules are this: clock, CPU, media, volume… (the settings window's Add module menu has ready-made ones).

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `format` | `"{time:HH:mm}"` |  | What it shows: text with {values} like {time:HH:mm}, {cpu}, {media_title}, and [colour]…[/] markup. |
| `max_length` | `0` | 0 to 1000 | **Max length.** Cut longer text with …  (0 = no limit). |
| `hide_when_empty` | `true` | true, false | **Hide when empty.** Hide the module when all its values are empty (e.g. media with nothing playing). |
| `size` | `"normal"` | small, normal, large | Text size (smooth style). |
| `spacing` | `0` | -5 to 50 | **Letter spacing.** Smooth style: extra pixels between letters. |
| `rotate` | `"none"` | none, down, up | Smooth style: turn the text on its side (for a side bar): down = reads top to bottom, up = bottom to top. none = flat. |

### bar.toml [[left]] / [[center]] / [[right]] type = "meter"

A small bar showing a value from 0 to 100%.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `value` | `"cpu"` | cpu, ram, disk, battery, volume, media, wifi | What it measures. |
| `label` | `"cpu"` |  | Text before the bar. |
| `format` | `"{cpu:0}%"` |  | Text after the bar (with {values}). |
| `width` | `8` | 2 to 100 | Length of the bar, in characters. |
| `fill_color` | `"accent"` | colour name or hex | **Fill colour.**  |
| `chars` | `"▮▯"` |  | **Meter characters.** Terminal style: filled and empty characters, e.g. █░, ▮▯, ■□. |
| `segments` | `0` | 0 to 100 | **Blocks.** Smooth style: draw the bar as this many separate blocks. 0 = one smooth bar. |
| `bar_height` | `0` | 0 to 100 | **Bar height.** Smooth style: thickness of the bar in pixels. 0 = from the font. |
| `track_color` | `"line"` | colour name or hex | **Track colour.** The empty part of the bar. |
| `label_color` | `"auto"` | colour name or hex | **Label colour.** auto = the module's colour. |

### bar.toml [[left]] / [[center]] / [[right]] type = "graph"

A tiny history graph.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `value` | `"cpu"` | cpu, ram, net_down, net_up | What it plots over the last two minutes. |
| `width` | `12` | 2 to 120 | Width, in characters. |
| `label` | `""` |  | Text before the graph. |

### bar.toml [[left]] / [[center]] / [[right]] type = "workspaces"

Windows' virtual desktops; click one to switch, scroll to move between them.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `style` | `"numbers"` | numbers, dots, names | numbers = 1 2 3, dots = ● ○ ○, names = the desktops' names. |
| `active_color` | `"accent"` | colour name or hex | **Active colour.**  |
| `inactive_color` | `"dim"` | colour name or hex | **Inactive colour.**  |
| `active_text` | `"auto"` | colour name or hex | **Active text.** Text colour of the current desktop's number. auto = the bar's colour. |
| `digits` | `1` | 1 to 4 | numbers style: show at least this many digits (2 = 01 02 03). |
| `min_count` | `1` | 1 to 20 | **Show at least.** Always show this many numbers; clicking one that doesn't exist yet makes the desktops up to it. |
| `active_frame` | `"none"` |  | **Active frame.** Smooth style: a frame around the current desktop's number (instead of a pill). none = a pill. |
| `inactive_frame` | `"none"` |  | **Inactive frame.** Smooth style: a frame around the other numbers. none = no frame. |

### bar.toml [[left]] / [[center]] / [[right]] type = "spacer"

Empty space.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `width` | `2` | 0 to 200 | Width, in characters. |

### bar.toml [[left]] / [[center]] / [[right]] type = "icons"

Empty space.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `names` | `[]` |  | Which shortcuts to show: their names, in order (as on the Shortcuts page). Empty = use Group instead. |
| `group` | `"bar"` |  | Or: the shortcuts in this group (Shortcuts page). Both empty = all of them. |
| `icon_size` | `0` | 0 to 128 | **Icon size.** In pixels. 0 = a bit bigger than the text. |
| `style` | `"original"` | original, tint, mono | **Icon style.** original = the app's own icon, tint = recoloured, mono = a flat silhouette in the module's colour. |
| `spacing` | `6` | 0 to 100 | Space between icons, in pixels. |

### bar.toml [[left]] / [[center]] / [[right]] type = "tasks"

Empty space.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `format` | `"{app}"` |  | What each window shows: {title} = the window's title, {app} = the program's name. Empty = just the icon. |
| `max_length` | `18` | 0 to 200 | **Max length.** Cut longer text with … (0 = no limit). |
| `icons` | `true` | true, false | **Show icons.**  |
| `icon_size` | `0` | 0 to 128 | **Icon size.** In pixels. 0 = a bit bigger than the text. |
| `style` | `"original"` | original, tint, mono | **Icon style.** original = the program's own icon, mono = a flat silhouette in the module's colour. |
| `task_frame` | `"none"` |  | **Task frame.** A frame around each window's button. none = no frame. |
| `active_frame` | `"none"` |  | **Active frame.** A frame for the window in front. none = the same as Task frame. |
| `active_color` | `"auto"` | colour name or hex | **Active colour.** Text colour of the window in front. |
| `minimised_color` | `"dim"` | colour name or hex | **Minimised colour.** Text colour of minimised windows. |
| `max_count` | `12` | 1 to 60 | **Most shown.** Show at most this many windows. |
| `spacing` | `6` | 0 to 100 | Space between windows, in pixels. |
| `combine` | `false` | true, false | **Group by program.** One button per program instead of per window ({count} = how many windows). Clicking it again brings its next window to the front. |
| `right_click` | `"none"` | none, close, minimise, maximise | **Right-click.** What right-clicking a window's button does. |
| `hide` | `[]` |  | **Leave out.** Programs not to show: parts of their names (e.g. Settings). |

### bar.toml [[left]] / [[center]] / [[right]] type = "tray"

Empty space.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this module. |
| `color` | `"text"` | colour name or hex | Text colour (colour markup in the format overrides it). |
| `background` | `"auto"` | colour name or hex | A pill behind this module. auto = the bar's Module background, none = no pill. |
| `bold` | `false` | true, false | Bold text (smooth style). |
| `frame` | `"auto"` |  | A frame around this module. auto = the bar's Module frame, none = no frame. Smooth style. |
| `join` | `false` | true, false | Share one frame with the next module (e.g. a label, a meter and a number in one box). |
| `caption` | `""` |  | A small label above the module (e.g. APPS), with {values}. Empty = none. Smooth style. |
| `caption_color` | `"dim"` | colour name or hex | **Caption colour.**  |
| `caption_spacing` | `2` | -5 to 50 | **Caption spacing.** Extra pixels between the caption's letters. |
| `caption_below` | `false` | true, false | **Caption below.** Put the caption under the module instead of above it. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `on_right_click` | `""` |  | **On right-click.**  |
| `on_scroll_up` | `""` |  | **On scroll up.**  |
| `on_scroll_down` | `""` |  | **On scroll down.**  |
| `icon_size` | `0` | 0 to 128 | **Icon size.** In pixels. 0 = a bit bigger than the text. |
| `style` | `"original"` | original, tint, mono | **Icon style.** original = the programs' own icons, tint = recoloured, mono = flat silhouettes in the module's colour. |
| `spacing` | `6` | 0 to 100 | Space between icons, in pixels. |
| `icon_frame` | `"none"` |  | **Icon frame.** A frame around each icon. none = no frame. |
| `hide` | `[]` |  | **Leave out.** Icons not to show: parts of their program's name or tooltip (e.g. OneDrive). |
| `max_count` | `16` | 1 to 60 | **Most shown.**  |
| `show_hidden` | `false` | true, false | **Show hidden.** Also show icons their programs asked to hide. |

### widgets.toml [[widget]] type = "shortcuts"

your shortcuts in a grid

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `16` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. |
| `panel` | `true` | true, false | Draw a panel behind the shortcuts. |
| `group` | `""` |  | Only show shortcuts in this group. Empty = all shortcuts. |
| `names` | `[]` |  | Or: just these shortcuts, by name, in this order (e.g. for a dock). Empty = use Group. |
| `columns` | `3` | 1 to 50 | Number of columns. |
| `launch` | `"double_click"` | double_click, click | **Open with.** double_click like Windows, or a single click. |
| `icon_size` | `48` | 12 to 512 | **Icon size.** In pixels. |
| `item_width` | `84` | 16 to 1000 | **Item width.** Width of each shortcut including its label, in pixels. |
| `spacing` | `8` | 0 to 200 | Gap between shortcuts, in pixels. |
| `icon_style` | `"auto"` | auto, original, tint, mono, ascii | **Icon style.** auto = ascii in terminal themes, original in smooth ones. original = the app's own icon. tint = recoloured with the tint colours. mono = a flat silhouette. ascii = drawn with characters. |
| `ascii_mode` | `"braille"` | braille, blocks | **ASCII kind.** For the ascii style: braille = fine dots, blocks = chunky pixels. |
| `tint_colors` | `["accent_dim", "accent", "bright"]` | colour name or hex | **Tint colours.** Dark to bright, for the tint and ascii styles. |
| `icon_color` | `"accent"` | colour name or hex | **Mono colour.** For the mono style. |
| `magnify` | `1` | 1 to 4 | Smooth style: icons swell under the mouse, the nearest most (like a dock): 1 = off, 1.6 = up to 60% bigger. |
| `magnify_range` | `110` | 20 to 1000 | **Magnify reach.** How far from the mouse icons still grow, in pixels. |
| `hover_label` | `false` | true, false | **Name on hover.** Smooth style: show the name of the icon under the mouse in a little tag above it (handy with labels off). |
| `tag_color` | `"surface"` | colour name or hex | **Tag colour.** Background of the hover tag. |
| `tag_border` | `"accent"` | colour name or hex | **Tag border.**  |
| `show_labels` | `true` | true, false | **Show labels.**  |
| `label_lines` | `2` | 1 to 3 | **Label lines.** How many lines a long name may take. |
| `label_color` | `"text"` | colour name or hex | **Label colour.**  |
| `hover_color` | `"accent"` | colour name or hex | **Hover colour.** Highlight under the mouse. |

### widgets.toml [[widget]] type = "clock"

a big clock and date

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `false` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `format` | `"{time:HH:mm}"` |  | The big line. {time:HH:mm} = 24-hour, {time:h:mm tt} = 12-hour, add :ss for seconds. |
| `subtitle` | `"{date:dddd d MMMM}"` |  | The line under it. Empty = none. |
| `align` | `"center"` | left, center, right |  |
| `pixel` | `"█"` |  | **Terminal pixel.** Terminal style: the character(s) the big digits are built from, e.g. █, ▓, # or []. |
| `shadow` | `"none"` | colour name or hex | **Shadow colour.** A shadow behind the big digits. none = no shadow. |
| `big_scale` | `5` | 1 to 20 | **Big size.** Smooth style: size of the big line compared with normal text. |
| `big_font` | `"Segoe UI Light"` |  | **Big font.** Smooth style: font of the big line. Empty = the theme's font. |
| `spacing` | `0` | -5 to 80 | **Letter spacing.** Smooth style: extra pixels between the big line's letters. |
| `subtitle_spacing` | `0` | -5 to 50 | **Subtitle spacing.** Smooth style: extra pixels between the subtitle's letters. |
| `subtitle_color` | `"auto"` | colour name or hex | **Subtitle colour.** auto = the text colour. |
| `subtitle_size` | `"large"` | small, normal, large | **Subtitle size.**  |

### widgets.toml [[widget]] type = "system"

CPU, memory, disk, network with meters and graphs

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `260` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"system"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `300` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `show` | `["cpu", "ram", "disk", "net"]` |  | What to show, in order: cpu, ram, disk, net, battery, volume, uptime, netup (network speeds and uptime on one small line). |
| `graphs` | `true` | true, false | Draw a history graph under CPU and network. |
| `graph_rows` | `2` | 1 to 10 | **Graph rows.** Height of the graphs, in lines of text. |
| `meter_chars` | `"█░"` |  | **Meter characters.** Terminal style: the filled and empty characters of a meter, e.g. █░, ▮▯, =-, ■□. |
| `segments` | `0` | 0 to 100 | **Meter blocks.** Smooth style: draw meters as this many separate blocks. 0 = smooth bars. |
| `one_line` | `false` | true, false | **Meters on one line.** Smooth style: label, meter and value on one line instead of the meter under the label. |
| `label_width` | `0` | 0 to 400 | **Label width.** Meters on one line: pixels kept for the labels, so the bars line up. 0 = each label's own width. |
| `value_width` | `0` | 0 to 400 | **Value width.** Meters on one line: pixels kept for the values on the right, so the bars end together. 0 = each value's own width. |
| `drive` | `"C"` |  | Which drive the disk line shows. |

### widgets.toml [[widget]] type = "media"

what's playing, with album art and controls

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"now playing"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `360` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `line0` | `""` |  | **Line above.** A small line above the first line, e.g. [dim]{media_app\|upper} // ON AIR[/]. Empty = none. |
| `line1` | `"{media_title}"` |  | **First line.** Can use {media_title}, {media_artist}, {media_album}, {media_app}, {media_state}. |
| `line2` | `"{media_artist}"` |  | **Second line.**  |
| `art` | `true` | true, false | **Album art.** Show the album art (as characters in terminal themes). |
| `art_rows` | `5` | 2 to 30 | **Art size.** Height of the album art, in lines of text. |
| `art_style` | `"auto"` | auto, original, tint, mono, ascii | **Art style.**  |
| `progress` | `true` | true, false | Show the progress bar and times. |
| `times_beside` | `false` | true, false | **Times beside the bar.** Smooth style: position on the left of the progress bar, length on the right (instead of both after it). |
| `progress_segments` | `0` | 0 to 200 | **Progress blocks.** Smooth style: draw the progress bar as this many blocks. 0 = a smooth bar. |
| `visualiser` | `false` | true, false | **Visualiser.** Bars that bounce with the sound playing (reads what your speakers play; costs a little CPU while music plays). |
| `visualiser_bars` | `32` | 4 to 128 | **Visualiser bars.**  |
| `visualiser_rows` | `2` | 0.5 to 20 | **Visualiser height.** In lines of text. |
| `visualiser_color` | `"accent_dim"` | colour name or hex | **Visualiser colour.**  |
| `visualiser_peak` | `"accent"` | colour name or hex | **Visualiser peak colour.** Colour of the tallest bars. |
| `visualiser_fps` | `24` | 5 to 60 | **Visualiser rate.** Frames per second while it moves (higher = smoother, more CPU). |
| `controls` | `true` | true, false | Show previous / play-pause / next buttons. |
| `prev_label` | `""` |  | **Previous button.** What the button shows: text or an icon, e.g. {icon:skip_previous} (Icons page). Empty = the built-in symbol. |
| `play_label` | `""` |  | **Play button.** e.g. {icon:play_arrow}. Empty = the built-in symbol. |
| `pause_label` | `""` |  | **Pause button.** e.g. {icon:pause}. Empty = the built-in symbol. |
| `next_label` | `""` |  | **Next button.** e.g. {icon:skip_next}. Empty = the built-in symbol. |
| `hide_when_idle` | `true` | true, false | **Hide when nothing plays.** Hide the whole panel when no media app is open. |

### widgets.toml [[widget]] type = "calendar"

this month

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `360` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"{date:MMMM yyyy}"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `first_day` | `"monday"` | monday, sunday | **First day.**  |
| `today_color` | `"accent"` | colour name or hex | **Today colour.**  |
| `weekend_color` | `"dim"` | colour name or hex | **Weekend colour.**  |
| `week_numbers` | `false` | true, false | **Week numbers.**  |

### widgets.toml [[widget]] type = "weather"

weather and forecast for a city

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `420` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"{city}"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `city` | `""` |  | City name (looked up online). Or set latitude and longitude instead. |
| `latitude` | `0` | -90 to 90 |  |
| `longitude` | `0` | -180 to 180 |  |
| `units` | `"celsius"` | celsius, fahrenheit |  |
| `line` | `"{weather_icon} {temp:0}°  {weather}"` |  | **Main line.** Can use {temp}, {feels}, {weather}, {weather_icon}, {wind}, {humidity}, {high}, {low}, {city}. |
| `detail` | `"feels {feels:0}° · wind {wind:0} · {humidity:0}%"` |  | **Detail line.**  |
| `days` | `4` | 0 to 7 | **Forecast days.**  |

### widgets.toml [[widget]] type = "text"

any text, with live values like {cpu} and colours

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"center"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `0` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `text` | `'[accent]{user}[/]@{host}\nup {uptime}'` |  | The text. Use {values} like {cpu}, [colour]…[/] for colours, and \n for a new line. |
| `size` | `"normal"` | small, normal, large, title |  |
| `align` | `"left"` | left, center, right |  |
| `mono` | `false` | true, false | Use the theme's monospace font (lines up columns and ASCII art). |
| `spacing` | `0` | -5 to 50 | **Letter spacing.** Smooth style: extra pixels between letters (wide, typed-looking headings). |
| `text_shadow` | `"none"` | colour name or hex | **Text shadow.** Smooth style: the text printed again a little lower and to the right, in this colour, under it. none = no shadow. |
| `shadow_offset` | `2` | 0 to 40 | **Shadow offset.** How far the text shadow sits from the text, in pixels. |

### widgets.toml [[widget]] type = "command"

the output of a command or script

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `16` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"command"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `command` | `"powershell -NoProfile -Command \"Get-Process \| Sort-Object CPU -Descending \| Select-Object -First 5 -ExpandProperty Name\""` |  | A program and its arguments; its printed output is shown. E.g. powershell -NoProfile -Command "Get-Date". |
| `interval` | `30` | 1 to 86400 | Run it again every this many seconds. |
| `max_lines` | `12` | 1 to 200 | **Max lines.**  |
| `mono` | `true` | true, false | Use the theme's monospace font. |

### widgets.toml [[widget]] type = "sysinfo"

ASCII logo with system info (neofetch style)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"center"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `200` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `false` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `art` | `""` |  | ASCII art file (in the theme folder or config\art). Empty = a built-in Windows logo. |
| `art_color` | `"accent"` | colour name or hex | **Art colour.**  |
| `lines` | `["[accent]{user}[/]@[accent]{host}[/]", "[dim]───────────────[/]", "[accent]os[/]      {os}", "[accent]cpu[/]     {cpu_name}", "[accent]gpu[/]     {gpu_name}", "[accent]memory[/]  {ram_used} / {ram_total}", "[accent]uptime[/]  {uptime}"]` |  | The lines beside the art. Use {values} and [colour]…[/] markup. |

### widgets.toml [[widget]] type = "image"

a picture

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `16` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `false` | true, false | Draw a panel behind it. |
| `width` | `0` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `file` | `""` |  | A picture (PNG/JPG), looked for in the theme folder and config\images. |
| `rows` | `10` | 1 to 100 | Height, in lines of text. |
| `style` | `"auto"` | auto, original, tint, mono, ascii | auto = characters in terminal themes, the picture in smooth ones. |
| `ascii_mode` | `"braille"` | braille, blocks | **ASCII kind.**  |

### widgets.toml [[widget]] type = "video"

plays a video or an ASCII clip (as characters or the real picture)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"center"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `0` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `0` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `file` | `""` |  | A video (mp4, webm, mkv, mov, gif…; looked for in the theme folder and config\videos) or an ASCII clip made by the converter (.gclip, config\clips). Normal videos need ffmpeg. |
| `fit` | `"contain"` | contain, cover, stretch | contain shows all of it, cover fills the box (may crop), stretch fills it exactly. |
| `speed` | `1` | 0.05 to 10 | Playback speed. |
| `loop` | `true` | true, false | Off = play once and stop on the last frame. |
| `click_to_pause` | `true` | true, false | **Click to pause.** Clicking the video pauses and resumes it (when On click is empty). |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `max_fps` | `30` | 1 to 120 | **Max frames per second.** Videos faster than this skip frames (saves power). The desktop's own frame rate also limits it. |
| `decoder` | `"auto"` | auto, gpu, cpu | Who decodes the video. gpu = the graphics chip: much less CPU but ~50-70 MB more memory. cpu = less memory, fine for small videos. auto = gpu for videos bigger than 640×480. Try cpu if a video won't play. |
| `style` | `"auto"` | auto, original, ascii | auto = characters in terminal themes, the real picture in smooth ones. ascii = turn the video into characters. original = the real picture. (.gclip clips are always characters.) |
| `colors` | `"gradient"` | gradient, original, single | Characters: gradient = theme colours by brightness, original = the video's colours, single = one colour. |
| `gradient` | `["accent_dim", "accent", "bright"]` | colour name or hex | Dark to bright, for colors = gradient. |
| `color` | `"accent"` | colour name or hex | For colors = single. |
| `chars` | `" .:-=+*#%@"` |  | **Characters.** Characters, faintest first, picked by brightness. For a .gclip, empty = the clip's own characters. |
| `char_scale` | `1` | 0.3 to 6 | **Character size.** Smooth style: size of the characters compared with the theme's character font. |
| `threshold` | `0.04` | 0 to 1 | Cells darker than this are left empty, so the box shows through. |
| `brightness` | `1` | 0 to 10 |  |
| `video_opacity` | `1` | 0 to 1 | **Video opacity.** 0 = invisible, 1 = solid. |
| `panel` | `true` | true, false | Draw a box behind the video. |
| `width` | `480` | 16 to 8000 | Width in pixels. |
| `height` | `270` | 16 to 8000 | Height in pixels. |
| `padding` | `0` | 0 to 400 | Space between the box edge and the video, in pixels. |

### widgets.toml [[widget]] type = "audio"

sound controls: output device, master and per-app volume

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `220` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"sound"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `340` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `devices` | `"current"` | current, list, none | **Output device.** current = the device in use (click it to switch to the next one, scroll to go back and forth). list = every output, click one to use it. none = hide. |
| `master` | `true` | true, false | **Master volume.** A slider for the whole volume: click to set, scroll to change, click its name to mute. |
| `apps` | `true` | true, false | **App volumes.** A slider for every app making sound (like the Windows volume mixer). |
| `only_playing` | `false` | true, false | **Only playing apps.** Hide apps that are open but silent right now. |
| `max_apps` | `6` | 0 to 30 | **Max apps.**  |
| `hide_system_sounds` | `true` | true, false | **Hide system sounds.**  |
| `step` | `5` | 1 to 50 | **Scroll step.** How much one notch of the mouse wheel changes a volume, in %. |
| `meter_chars` | `"█░"` |  | **Meter characters.** Terminal style: the filled and empty characters of a slider, e.g. █░, ▮▯, =-, ■□. |
| `muted_color` | `"dim"` | colour name or hex | **Muted colour.** Slider colour while muted. |

### widgets.toml [[widget]] type = "feed"

lines that keep coming in: made-up hex, logs or values (a live-looking readout)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `16` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `60` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"feed"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `280` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `lines` | `["0x{seq:X4}  {pick:SYN,ACK,PKT,AUTH,ROUTE}  {hex:2}:{hex:2}  {pick:OK,OK,OK,LOCKED,PASS}  {hex:4}-{hex:4}"]` |  | Patterns for new lines (one is picked at random each time). Besides {values}: {hex:4} = random hex digits, {num:10-99} = a random number, {pick:a,b,c} = one of these, {seq:X4} = a counting number (X4 = 4 hex digits), {spaces:0-6} = random spaces. [colour]…[/] works too. |
| `interval` | `1.5` | 0.05 to 3600 | Seconds between new lines. |
| `jitter` | `0.5` | 0 to 1 | **Random wait.** Make the wait between lines vary by up to this fraction (0.5 = ±50%). |
| `max_lines` | `8` | 1 to 200 | **Lines shown.**  |
| `newest_first` | `false` | true, false | **Newest at the top.**  |
| `newest_color` | `"bright"` | colour name or hex | **Newest colour.** Colour of the newest line. |
| `old_color` | `"dim"` | colour name or hex | **Older colour.** Older lines fade from Text colour to this one. |
| `mono` | `true` | true, false | Use the theme's monospace font (lines up columns). |
| `size` | `"small"` | small, normal, large |  |

### widgets.toml [[widget]] type = "menu"

a menu screen: your shortcut groups as tabs, the chosen group's shortcuts as a list, details of the chosen one

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `20` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `140` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"both"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `width` | `900` | 100 to 8000 | Width in pixels when it isn't stretched (Stretch). |
| `height` | `600` | 100 to 8000 | Height in pixels when it isn't stretched. |
| `tabs` | `[]` |  | **Tabs.** The shortcut groups shown as tabs, in order (names as in the shortcuts' Group). Empty = every group: the Shortcuts page's groups first, then any other group a shortcut names. |
| `ungrouped_tab` | `"Other"` |  | **Ungrouped tab.** A tab for the shortcuts that have no group, with this name. Empty = leave them out. |
| `launch` | `"double_click"` | double_click, click | **Open with.** double_click = a click selects (and shows the details), a double click opens. click = a click opens. |
| `wipe_ms` | `160` | 0 to 2000 | **Hover wipe.** Milliseconds for the dark fill to slide in under the mouse (tabs and list). 0 = at once. |
| `wipe_style` | `"cubic"` | linear, sine, quad, cubic, quart, quint, exponential, circular, back, elastic, bounce | **Wipe easing.** The shape of the hover wipe's motion (as in animations): cubic = quick then settling, linear = steady, back = overshoots, bounce, elastic… |
| `wipe_direction` | `"out"` | in, out, inout | **Wipe direction.** out = fast start, gentle end. in = gentle start, fast end. inout = gentle at both ends. |
| `upper` | `true` | true, false | **Capitals.** Tabs and the heading in capitals. |
| `tab_layout` | `"spread"` | spread, packed, column | **Tab layout.** spread = the tabs fill the width with even gaps. packed = side by side from the left. column = a column down the left side (good with Tab style = icons). |
| `tab_style` | `"full"` | full, icons, text | **Tab style.** full = icon and name. icons = just the group's icon (a square button). text = just the name. |
| `tab_active_frame` | `"none"` |  | **Active tab frame.** A frame that fades in over the chosen tab and the one under the mouse (e.g. a bright outline). none = the tab frame's Hover fill only. |
| `tab_icon_color` | `"auto"` | colour name or hex | **Icon colour.** Colour of the tab icons when there's no icon box. auto = the tab text colour. |
| `tab_active_icon` | `"auto"` | colour name or hex | **Active icon colour.** Icon colour on the chosen tab. auto = the active text colour. |
| `tab_height` | `28` | 12 to 200 | **Tab height.**  |
| `tab_gap` | `18` | 0 to 400 | **Tab gap.** Pixels between tabs (packed), or the least gap (spread). |
| `tab_inset` | `40` | 0 to 1000 | **Tab inset.** Space at the left and right ends of the tab row, in pixels. |
| `tab_padding` | `12` | 0 to 100 | **Tab padding.** Space inside a tab, left and right. |
| `tab_spacing` | `2` | -5 to 50 | **Tab letter spacing.**  |
| `icon_box` | `20` | 0 to 100 | **Icon box.** Size of the square behind a tab's icon (the group's Icon). 0 = no box, just the icon. |
| `tab_counts` | `false` | true, false | **Show counts.** Show how many shortcuts each tab holds, like (5). |
| `tab_frame` | `"none"` |  | **Tab frame.** A frame (Frames page) for the tabs. none = a plain plate in the colours below (its Hover fill becomes Active colour). |
| `tab_color` | `"surface"` | colour name or hex | **Tab colour.**  |
| `tab_text` | `"text"` | colour name or hex | **Tab text.**  |
| `active_color` | `"text"` | colour name or hex | **Active colour.** The fill of the chosen tab / item and the one under the mouse. |
| `active_text` | `"bg"` | colour name or hex | **Active text.**  |
| `rule` | `"band"` | none, solid, band | **Rule.** The line under the tab row. band = a line, dashes and little dot triangles. |
| `rule_color` | `"text"` | colour name or hex | **Rule colour.**  |
| `rule_gap` | `14` | 0 to 200 | **Rule gap.** Pixels between the tabs and the rule. |
| `heading` | `true` | true, false | **Show heading.** The chosen tab's name in big letters, with the group's Subtitle and Description under it. |
| `heading_text` | `""` |  | **Heading text.** What the big heading says, with {values}: e.g. {greeting}, {user}. Empty = the chosen tab's name. |
| `heading_sub` | `""` |  | **Heading line.** The small line under the heading, with {values}. Empty = the group's Subtitle. |
| `heading_bold` | `false` | true, false | **Heading bold.**  |
| `heading_gap` | `36` | 0 to 400 | **Heading gap.** Pixels between the rule and the heading. |
| `heading_scale` | `2.3` | 1 to 8 | **Heading size.** Size of the big name compared with normal text. |
| `heading_spacing` | `4` | -5 to 60 | **Heading spacing.**  |
| `heading_shadow` | `"none"` | colour name or hex | **Heading shadow.** The heading printed again a little lower right, in this colour. none = no shadow. |
| `heading_color` | `"text"` | colour name or hex | **Heading colour.**  |
| `subtitle_color` | `"dim"` | colour name or hex | **Subtitle colour.**  |
| `subtitle_spacing` | `3` | -5 to 50 | **Subtitle spacing.**  |
| `inset` | `40` | 0 to 1000 | **Heading inset.** Space from the left edge to the heading and the list. |
| `list_layout` | `"list"` | list, cards | **List layout.** list = one row per shortcut. cards = a grid of cards with the shortcut's icon, name and description. |
| `card_columns` | `3` | 1 to 12 | **Card columns.** cards: how many side by side. |
| `card_height` | `100` | 30 to 600 | **Card height.**  |
| `icons` | `false` | true, false | **App icons.** Show each shortcut's own icon (instead of the bullet square). |
| `item_active_frame` | `"none"` |  | **Active item frame.** A frame that fades in over the chosen item and the one under the mouse. none = the item frame's Hover fill only. |
| `list_width` | `240` | 0 to 3000 | **List width.** list: width of the rows. cards: width of the grid (0 = all the room there is). |
| `item_height` | `32` | 12 to 200 | **Item height.**  |
| `item_gap` | `12` | 0 to 100 | **Item gap.** Pixels between items. |
| `list_gap` | `36` | 0 to 400 | **List gap.** Pixels between the heading and the list. |
| `item_frame` | `"none"` |  | **Item frame.** A frame for the items. none = a plain plate in the tab colours. |
| `bullet` | `10` | 0 to 60 | **Bullet size.** The little square before each name. 0 = none. |
| `rail_color` | `"line"` | colour name or hex | **Rail colour.** A thick line down the left of the list, with a mark at the chosen item. none = no rail. |
| `rail_width` | `6` | 1 to 40 | **Rail width.**  |
| `details` | `true` | true, false | **Show details.** A panel beside the list about the chosen shortcut: what it is, where it points, open buttons. |
| `details_width` | `420` | 100 to 3000 | **Details width.**  |
| `details_frame` | `"none"` |  | **Details frame.** A frame for the panel (its title strip holds the name). none = a dark title strip and no box. |
| `label_color` | `"dim"` | colour name or hex | **Label colour.**  |
| `row_line` | `"line"` | colour name or hex | **Row lines.** A faint line between rows. none = no lines. |

### widgets.toml [[widget]] type = "divider"

a line across or down the screen: solid, dashed, dotted, double or a printed-form band

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `20` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `200` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"width"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `direction` | `"horizontal"` | horizontal, vertical | Which way the line runs. |
| `length` | `400` | 1 to 8000 | Length in pixels (when it isn't stretched to the screen's edges with Stretch). |
| `style` | `"solid"` | solid, dashed, dotted, double, band | solid = one line. dashed = dashes. dotted = dots. double = two thin lines. band = a line, a row of dashes under it and a row of little dot triangles (a printed-form look). |
| `thickness` | `2` | 0.5 to 100 | Thickness of the line, in pixels. |
| `line_color` | `"text"` | colour name or hex | **Line colour.**  |
| `line_opacity` | `1` | 0 to 1 | **Line opacity.**  |
| `dash_length` | `14` | 1 to 400 | **Dash length.** dashed and band: length of each dash, in pixels. |
| `dash_gap` | `4` | 1 to 400 | **Dash gap.** Pixels between dashes or dots. |
| `band_spacing` | `36` | 4 to 400 | **Band spacing.** band: pixels between the dot triangles. |
| `band_color` | `"auto"` | colour name or hex | **Band colour.** band: colour of the dashes and dots. auto = the line colour. |

### widgets.toml [[widget]] type = "themes"

cards for your themes (wallpaper picture, colours, name); click one to switch to it

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `0` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"THEMES"` |  | Title over the cards. Empty = none. |
| `show` | `[]` |  | Which themes, in order (folder names). Empty = all of them. |
| `width` | `260` | 80 to 3000 | Width of the panel, in pixels. |
| `columns` | `1` | 1 to 10 | Cards side by side. |
| `card_height` | `72` | 24 to 600 | **Card height.**  |
| `card_gap` | `10` | 0 to 100 | **Card gap.**  |
| `dots` | `["accent", "accent_bright", "bright"]` |  | **Colour dots.** Which of each theme's colours to show as little squares on its card. |
| `dim` | `0.35` | 0 to 1 | **Picture dim.** Darken each card's picture (the theme's wallpaper) so the name stays readable: 0 = not at all, 1 = black. |
| `card_frame` | `"none"` |  | **Card frame.** A frame for the cards (Frames page). none = plain rounded cards. |
| `current_frame` | `"none"` |  | **Current frame.** A frame over the card of the theme in use (and the one under the mouse). none = an outline in the accent colour. |
| `name_color` | `"bright"` | colour name or hex | **Name colour.**  |
| `current_color` | `"accent"` | colour name or hex | **Current colour.** The outline and tick on the theme in use (when Current frame is none). |
| `radius` | `10` | 0 to 60 | **Corner radius.**  |

### widgets.toml [[widget]] type = "toggles"

quick buttons (Wi-Fi, Bluetooth, mute, snip, lock…) that light up when on and rise under the mouse

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title over the buttons. Empty = none. |
| `buttons` | `["{icon:wifi} \| Wi-Fi \| ms-settings:network-wifi \| {wifi}", "{icon:bluetooth} \| Bluetooth \| ms-settings:bluetooth \| {bluetooth}", "{icon:volume_off} \| Mute \| volume:mute \| {muted}", "{icon:notifications} \| Alerts \| keys:win+n \| ", "{icon:screenshot_region} \| Snip \| keys:win+shift+s \| ", "{icon:lock} \| Lock \| keys:win+l \| "]` |  | One button per line: icon \| label \| what clicking does \| when it's on. The icon is {icon:name} (Icons page) or any text; the action is any click action (ms-settings:…, volume:mute, keys:win+l, a program…); 'when it's on' is a {value} — the button lights up when it isn't empty, off or 0 (e.g. {muted}, {bluetooth}, {wifi}). Leave the last part empty for a plain button. |
| `columns` | `6` | 1 to 20 | Buttons side by side (then a new row). |
| `button_size` | `72` | 24 to 400 | **Button size.** Width and height of each button, in pixels. |
| `gap` | `10` | 0 to 100 | Pixels between buttons. |
| `labels` | `true` | true, false | **Show labels.** The label under each icon. |
| `icon_scale` | `1.9` | 0.5 to 6 | **Icon size.** Size of the icons compared with normal text. |
| `button_frame` | `"none"` |  | **Button frame.** A frame for the buttons (Frames page). none = plain rounded buttons. |
| `on_frame` | `"none"` |  | **On frame.** A frame that fades in on buttons that are on (and the one under the mouse, half way). none = the On colour as a glow. |
| `icon_color` | `"text"` | colour name or hex | **Icon colour.**  |
| `on_color` | `"accent"` | colour name or hex | **On colour.** Icon colour of buttons that are on. |
| `label_color` | `"dim"` | colour name or hex | **Label colour.**  |
| `lift` | `3` | 0 to 40 | **Hover lift.** Pixels a button rises under the mouse. |
| `hover_ms` | `140` | 0 to 2000 | **Hover time.** Milliseconds the hover rise and glow take. |

### widgets.toml [[widget]] type = "list"

a to-do list you tick by clicking, an agenda (time, item, note) or plain lines; edit its items in the settings window

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"top_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `300` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `"to do"` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `true` | true, false | Draw a panel behind it. |
| `width` | `380` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `list` | `"todo"` |  | **List.** Which list it shows: its name (config\lists\<name>.txt). Click … to edit its items: one per line; [x] at the start = done. Agenda: 19:30 \| What \| When. Lines starting with # are notes. Several widgets can show the same list. |
| `style` | `"checklist"` | checklist, agenda, bullets | checklist = a mark to tick before each item (click an item to tick it). agenda = a time, the item and a note under it. bullets = plain lines. |
| `click_ticks` | `true` | true, false | **Click ticks.** checklist: clicking an item ticks it (and clicking again unticks it). |
| `done_style` | `"both"` | strike, dim, both, hide | **Done items.** How ticked items look: strike = crossed out, dim = faded, both = crossed out and faded, hide = not shown. |
| `open_mark` | `"{icon:radio_button_unchecked}"` |  | **To-do mark.** checklist: what goes before an item still to do. Text or an icon like {icon:radio_button_unchecked}. |
| `done_mark` | `"{icon:check_circle}"` |  | **Done mark.** checklist: before a ticked item, e.g. {icon:check_circle} or {icon:circle}. |
| `bullet` | `"•"` |  | **Bullet.** bullets: what goes before each line. |
| `mark_color` | `"dim"` | colour name or hex | **Mark colour.**  |
| `done_mark_color` | `"accent"` | colour name or hex | **Done mark colour.**  |
| `time_color` | `"bright"` | colour name or hex | **Time colour.** agenda: the time's colour. |
| `time_width` | `60` | 0 to 400 | **Time width.** agenda: pixels kept for the time, so the items line up. |
| `max_items` | `0` | 0 to 200 | **Max items.** Show at most this many (0 = all). |
| `item_gap` | `0.35` | 0 to 5 | **Item gap.** Space between items, in lines of text. |
| `empty_text` | `"nothing here"` |  | **When empty.** Shown when the list has no items. |
| `desktop_edit` | `true` | true, false | **Edit on the desktop.** Click an item's text on the desktop to change or delete it (a small box opens at the mouse); the add line adds one. |
| `add_text` | `"{icon:add} add"` |  | **Add line.** The line under the items that adds a new one when clicked. Empty = none. |

### widgets.toml [[widget]] type = "lyrics"

the words of the song playing, the line being sung highlighted (synced lyrics from lrclib.net)

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_left"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `200` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `false` | true, false | Draw a panel behind it. |
| `width` | `380` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `before` | `1` | 0 to 10 | **Lines before.** How many sung lines to show before the current one. |
| `after` | `2` | 0 to 20 | **Lines after.** How many lines to show after the current one. |
| `current_color` | `"bright"` | colour name or hex | **Current colour.**  |
| `other_color` | `"dim"` | colour name or hex | **Other lines colour.**  |
| `current_size` | `"large"` | normal, large | **Current size.**  |
| `current_bold` | `true` | true, false | **Current bold.**  |
| `fade` | `true` | true, false | **Fade far lines.** Lines further from the current one get fainter. |
| `offset` | `0` | -10 to 10 | Seconds to shift the lyrics by if they come early (negative) or late (positive). |
| `hide_when_idle` | `true` | true, false | **Hide when nothing plays.** (The widget is empty anyway while nothing plays or the words are being looked up.) |

### widgets.toml [[widget]] type = "visualiser"

big audio bars that bounce with whatever is playing

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `enabled` | `true` | true, false | Show this panel. |
| `name` | `""` |  | A name for this panel, so another panel can draw a line to it (Link to). |
| `anchor` | `"bottom_right"` | top_left, top, top_right, left, center, right, bottom_left, bottom, bottom_right | Which corner or edge of the screen it sits against. |
| `offset_x` | `40` |  | **Offset X.** Move it right (or left if negative), in pixels. |
| `offset_y` | `40` |  | **Offset Y.** Move it down (or up if negative), in pixels. |
| `stretch` | `"none"` | none, width, height, both | Stretch it to the edges of the screen (less the bar): width = from Offset X on the left to Offset X on the right, height = from Offset Y at the top to Offset Y at the bottom. none = its own size. |
| `frame` | `"auto"` |  | A frame from the Frames page: cut corners, title strips, brackets, patterns… auto = the theme's panel frame (Colours & look), none = a plain box with the settings below. Smooth style. |
| `background` | `"auto"` | colour name or hex | **Box colour.** Colour of this panel. auto = the theme's panel colour. |
| `opacity` | `-1` | -1 to 1 | **Box opacity.** 0 = see-through, 1 = solid, -1 = the theme's. |
| `corner_radius` | `-1` | -1 to 200 | **Corner radius.** Smooth style, in pixels. -1 = the theme's. |
| `border_color` | `"auto"` | colour name or hex | **Border colour.** auto = the theme's. |
| `border_width` | `-1` | -1 to 50 | **Border width.** Smooth style, in pixels. -1 = the theme's. Terminal style: 0 hides the box lines. |
| `panel_shadow` | `-1` | -1 to 1 | **Box shadow.** Smooth style. -1 = the theme's. |
| `font` | `""` |  | Font for this box's text (smooth style). Empty = the theme's font. Fonts in config\fonts are listed too. |
| `font_size` | `0` | 0 to 96 | **Font size.** Text size for this box, in pixels. 0 = the theme's. |
| `box_hover_color` | `"none"` | colour name or hex | **Hover colour.** If the box does something on click, it lights up in this colour under the mouse. none = no highlight. |
| `tilt_x` | `0` | -80 to 80 | **Tilt X.** Smooth style: lean the panel back (positive) or forward (negative) in 3D, in degrees, like a sign seen from below or above. |
| `tilt_y` | `0` | -80 to 80 | **Tilt Y.** Smooth style: turn the panel in 3D around its upright middle, in degrees: positive = its right side goes back (good for a panel on the left of the screen), negative = its left side goes back. |
| `depth` | `1600` | 100 to 20000 | **Depth.** How strong the 3D look of a tilt is: smaller = stronger perspective, in pixels of distance. |
| `tilt_follow` | `0` | 0 to 45 | **Tilt to mouse.** Smooth style: while the mouse is over it, the panel tilts in 3D towards where the mouse is, up to this many degrees (like a card you press on), and settles back when the mouse leaves. 0 = off. |
| `tilt_around` | `""` |  | **Tilt around.** Tilt as part of another widget (its Name): the tilt turns around that widget's middle, so several widgets lean together like one surface (give them all the same Tilt). That widget's animations (and its Tilt to mouse) carry this one along too. Empty = around its own middle. |
| `rotate` | `0` | -360 to 360 | Turn the panel flat on the screen, in degrees (e.g. 90 or -90 for sideways text). |
| `alpha` | `1` | 0 to 1 | **Opacity.** See-through-ness of the whole panel and everything in it: 1 = solid, 0.1 = a faint watermark. |
| `halo` | `"auto"` | colour name or hex | **Halo.** Smooth style: a soft blur of this colour behind the widget, so its text stays readable on a light wallpaper. auto = the theme's (Colours & look), none = off. |
| `halo_size` | `0` | 0 to 60 | **Halo size.** Pixels. 0 = the theme's. |
| `halo_strength` | `-1` | -1 to 2 | **Halo strength.** -1 = the theme's. Up to 2 for very light wallpapers. |
| `glow` | `"auto"` |  | **Glow.** Smooth style: the widget lights up. self = in its own colours, a colour = in that colour, auto = the theme's, none = off. |
| `glow_size` | `0` | 0 to 80 | **Glow size.** Pixels. 0 = the theme's. |
| `glow_strength` | `-1` | -1 to 3 | **Glow strength.** -1 = the theme's. |
| `link_to` | `""` |  | **Link to.** Draw a line from this panel to the panel with this Name (like a callout). Empty = no line. Smooth style. |
| `link_color` | `"accent_dim"` | colour name or hex | **Link colour.**  |
| `link_width` | `1` | 0.5 to 20 | **Link width.**  |
| `link_lines` | `1` | 1 to 5 | **Link lines.** How many lines side by side. |
| `link_gap` | `5` | 1 to 50 | **Link gap.** Pixels between the lines. |
| `link_shape` | `"elbow"` | elbow, straight | **Link shape.** elbow = a 45° bend then straight, straight = one straight line. |
| `link_ends` | `"dot"` | none, dot, both | **Link ends.** A small square where the line meets the panels. |
| `title` | `""` |  | Title shown on the panel. Empty = none. Can use {values}. |
| `title_right` | `""` |  | **Title right.** Text at the right end of the title (e.g. a status like LIVE). Can use {values}. |
| `panel` | `false` | true, false | Draw a panel behind it. |
| `width` | `800` | 0 to 4000 | Width in pixels. 0 = fit the contents. |
| `height` | `0` | 0 to 4000 | Height in pixels. 0 = fit the contents. |
| `vertical_align` | `"top"` | top, center, bottom | **Vertical align.** Where the contents sit when the panel is taller than them. |
| `scale` | `1` | 0.3 to 6 | Make the text bigger or smaller (1 = the theme's size). Smooth style. |
| `text_color` | `"text"` | colour name or hex | **Text colour.**  |
| `accent_color` | `"accent"` | colour name or hex | **Accent colour.** Highlights, meters, graphs. |
| `dim_color` | `"dim"` | colour name or hex | **Dim colour.** Labels and less important text. |
| `on_click` | `""` |  | **On click.** What happens on click: launcher, tile:toggle, tile:layout:next, theme:soft, media:toggle, media:next, media:prev, volume:mute, volume:up, volume:down, volume:+10, volume:40, audio:device:next (switch speakers/headphones), audio:app:Spotify:mute, audio:mixer, desktop:next, desktop:prev, desktop:2, keys:win+n, run:program args, settings, or a file / folder / program / website to open. |
| `hover_text` | `""` |  | **Hover text.** Extra lines the panel grows to show while the mouse is over it, with {values} and [colour]…[/] markup (\n = new line). Empty = nothing extra. |
| `reveal_time` | `220` | 0 to 3000 | **Reveal time.** Milliseconds the panel takes to grow (and shrink back). |
| `bars` | `64` | 4 to 256 | How many bars. |
| `rows` | `8` | 0.5 to 60 | **Bar height.** Height of the tallest bars, in lines of text. |
| `bar_gap` | `3` | 0 to 40 | **Bar gap.** Pixels between bars. |
| `bar_color` | `"accent_dim"` | colour name or hex | **Bar colour.**  |
| `peak_color` | `"accent"` | colour name or hex | **Peak colour.** Colour of the tallest bars. |
| `mirror` | `false` | true, false | Bars grow up and down from the middle instead of up from the bottom. |
| `fps` | `30` | 5 to 60 | **Frame rate.** Frames per second while it moves (higher = smoother, more CPU). |
| `hide_when_idle` | `false` | true, false | **Hide when nothing plays.**  |

### shortcuts.toml [[shortcut]]

Your shortcuts, shared by every theme.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `name` | `""` |  | The name shown under the icon. |
| `target` | `""` |  | What it opens: a program, file, folder, shortcut (.lnk), website or shell: location. |
| `arguments` | `""` |  | Extra command-line arguments (optional). |
| `start_in` | `""` |  | **Start in.** Working folder (optional). |
| `group` | `""` |  | Optional group name; a panel can show just one group, and a menu shows each group as a tab. |
| `description` | `""` |  | A short line about it (optional), shown by menus under the name. |
| `image` | `""` |  | Your own picture for the icon (PNG/JPG/ICO). Empty = the app's own icon. |

### variables.toml [[variable]] (in a theme folder)

Your own values, used in any text as {name}.

| Option | Default | Allowed | What it does |
|---|---|---|---|
| `name` | `"my_value"` |  | The name to use in text: a variable called mycpu is written {mycpu}. Letters, numbers and _. |
| `kind` | `"text"` | text, command, file | text = built from other values; command = the output of a program; file = the contents of a text file. |
| `text` | `"{cpu\|fmt:0}%"` |  | For text: what it's made of, e.g. {cpu\|pad:3}% or {media_artist} – {media_title}. Filters: upper, lower, title, pad:N, rpad:N, center:N, max:N, fmt:0.0, prefix:TEXT, suffix:TEXT, default:TEXT, bar:N (or bar:N:█░), map:a=b,c=d, replace:a=b, color>80:colour, when>80:TEXT (also <, >=, <=, =, !=) |
| `command` | `""` |  | For command: a program and its arguments; what it prints becomes the value. |
| `file` | `""` |  | For file: a text file whose contents become the value. |
| `interval` | `30` | 1 to 86400 | For command and file: read it again every this many seconds. |
| `first_line` | `true` | true, false | For command and file: keep only the first line. |

---

*This guide's option tables are generated from Glyph itself (`bin\Glyph.exe --write-guide`).*