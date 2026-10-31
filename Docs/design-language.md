# Design language

Which shape to reach for and why. Ids are append-only (see CLAUDE.md). The design canvas holds the drawn boards;
this file holds the rules the app follows.

**D1 — Tokens.** Sizes, type and radii live in `src/Soundmote.App/Assets/AppResources.axaml` (StaticResource);
colour lives in `Assets/Themes/*.axaml` (DynamicResource), brushes built in the same file as their colours. Type
roles are style classes in `Styles/TextBlocks.axaml`: `title`, `row`, `meta`, `brand`, `clock`, `dock`, `number`
(tabular figures). Primary ink is inherited from the window, never set by a global TextBlock style, so text inside
a filled button keeps the button's foreground. The font is Inter for now (N8).

**D2 — The dial screen.** Three columns over a dock: the scene and its phases on the left (268 wide), the dial in the
middle taking all the room left, one row per ring on the right (280), and a floating dock centred at the bottom
(640 × 62, fully rounded). A 52 px strip on top belongs to the window, not the screen: it carries the brand, on
a dial "‹ Scenes" back to the library (D13), and Settings at the right (D18). On macOS the content extends under the
title bar and the strip starts right of the window buttons. On Windows it extends too, the brand sits at the left
edge and the strip stops short of the three caption buttons at the right. On Linux the desktop draws its own title
bar with the name, so the strip drops the brand and is 44 high. Panels are the scene's panel colour with a faint edge, radius 18.

**D3 — Colour belongs to the sound, ground belongs to the scene.** A ring's colour is the layer's own (`Layer.Colour`)
and is the same on the ring, the row swatch and the selected row's tint (16 %). The ground (D12) colours
only the ground, the panels, the ink and the dial's furniture. A scene for work gets a light ground with the same
hues deepened, not other hues.

**D4 — Dial anatomy.** Every measure is a share of the tick radius R, so the dial scales with the window:

| Part | Size |
|---|---|
| Ticks | R × 0.95 (labelled, every 5 min) or R × 0.975 to R |
| Labels | R + 16 |
| First ring | R × 0.842; then one lane apart |
| Lane | R × 0.1, narrowed if many rings would reach the face |
| Ring at full amount | 78 % of a lane |
| Face | R × 0.36, panel colour |

Behind the hand a ring is solid; ahead of it, 30 % (`Dial.FutureOpacity`). A muted ring is drawn at 25 % of that.
While a ring is selected the others drop to 45 %, and the selected one gets a thin line in its own colour along its
outer edge, with its points on it (D8). The hand runs from the face to just past R and ends in a warm dot.

**D5 — Dock.** Play is the only filled control on the screen. Next to it the scene's name and one status line. The
preview speed (×1, ×10, ×60) is a pill track with the checked segment filled; it exists so a scene can be heard
whole in minutes while shaping it. Volume is the device volume, not a ring's level.

**D6 — Words for a curve.** A row says what its ring does in a few words, worked out from where the curve peaks:
"Steady", "Dies down slowly", "Builds to the end", "Settles after 35 min", "Swells through the middle". The words
follow the curve, so they change when the ring is reshaped. Other ways to play have their own words (D11).

**D7 — Keys.** Space plays and pauses, ← → move the hand a minute, 1–9 mute a ring. Escape closes the add panel,
else clears the selection, else goes back to the library. In the library only space works, on the open scene. ⌘N
(Ctrl+N on Windows and Linux) opens New scene, ⌘D duplicates the open scene, ⌘, opens Settings and Escape closes it; on the sheet Return creates and Escape
cancels. ⌘E opens Export for the open scene (D24), where Escape cancels a render, else closes the sheet. Handled on the window in the tunnel phase; single keys only with no modifier held, and left alone while a
text box has focus.

**D8 — Shaping a ring.** A ring is shaped where it is drawn. Selecting it shows its curve's points on its outer edge;
the point under the pointer follows it, so distance from the ring's centre line is the amount (the outer edge sits at
radius + amount × width / 2) and the angle is the time. The first and last point keep their time, and a point never
passes its neighbours (`Curve.MinGap`). Double-click the ring to add a point where the curve already is, double-click
a point to remove it. While dragging, the shape before the drag stays as a faint dashed line and a tip shows
"30:00 · 85 %". Below the rows the same curve lies flat for precision, with the phases as faint lines, the hand as a
playhead and four presets (Steady, Rises, Fades, Swells) scaled to the ring's own peak, so a quiet ring stays quiet.
Reset returns to the scene's shape. Every edit is heard from the next block and the row's words follow (D6).

**D9 — Adding a sound.** "Add a sound" (under the rows, and + in the dock) replaces both side panels with one 440-wide
panel; the dial moves left into the room. Search, the place and mood menus and the families (D20), then, in a family
or for a search, tiles in two columns: each tile shows the
sound's signature (its loudness over six seconds, rendered from the sound itself, in its colour), its name and one
line of character, or "Already in this scene". Clicking a tile plays the sound at once as a trial ring on the
innermost lane: its whole shape faint, edged with a dashed line in its colour, and "Trying Distant sea" under the
clock. Trying another replaces it; the starting shape (Steady, Rises, Fades, Swells) reshapes it live. "Add as a
ring" keeps it and selects it for shaping (D8); Cancel or Escape drops it. If the scene was paused, trying plays it.
A ring leaves through Remove in its shape panel.

**D10 — Sound glyphs.** Every sound has a line glyph on a 24 grid (1.8 stroke, round ends, never filled), set in a
rounded square tinted 16 % with the sound's colour: 38 px in the add panel, 28 px in the rows and the ring panel. On
the dial each ring carries its glyph in a small disc just before the top, where the ring begins. One glyph per sound,
not per recording; a recipe names its glyph and one without gets its family's (`SoundIcons`). Never a waveform:
two kinds of noise look alike as waves, a flame and a bell never do.

**D11 — How a ring plays.** The ring panel opens with four ways on a pill track: Shape, On/off, Every, Random. The ring
draws the choice: a shape is a width; on/off is thick arcs at its level, their ends draggable on the ring, a
double-click on the ring adding a five-minute stretch and on an end removing it; every and random are dots sized by
strength, filled once the hand has passed them and hollow before; random also shows how busy it gets as a faint band.
Below the track, only that way's settings: stretches with fade and level; interval, first strike and strength; rate,
gap, variation, busiest part and Shuffle. The row's words follow ("On twice · 20 min in all", "Every 5 min",
"6 per 10 min, random"), and its right column says what is next ("next 15:00", "on at 15:00"). Switching back to a
way brings back its last settings.

**D12 — Grounds.** Four grounds, one per kind of doing: Night (sleep), Ember (meditation and unwinding), Paper (focus, and the
library) and Sky (a background that follows the day). Each is a theme file in `Assets/Themes` with the same keys,
registered as a `ThemeVariant` in `Grounds`: Night and Ember build on Fluent's dark variant, Paper and Sky on its light
one. A view takes a ground by setting `RequestedThemeVariant`; the window follows the open scene, a library card
follows its own scene through a `GroundScope` (L1). On a light ground every ring colour is drawn 0.15 darker in lightness (`Ring.Shade`), so the
same sound keeps its hue and still holds on paper; the dial, flat curve, badges and thumbnails all read it.

**D13 — The library.** "What are you doing?" over filter chips (All, then only the intents some scene has) and a wrap
of 292 × 256 cards. A card is its scene's ground with the intent in small capitals, the scene as a small dial (rings
only: no ticks, face or badges), the name, and the kind of time with its glyph. Clicking a card opens its dial and
plays it; another scene that was playing stops, and the volume carries over. The playing card is outlined in the
library's ink, shows PLAYING and its hand moves. Going back keeps the scene playing in a bar along the bottom, shaped
like the dock: play, name, status and Open dial.

The library has two rows of cards: Yours, ending with a dashed New scene card, then From Soundmote. A card's menu
(right-click, or its … button) has Open, Rename, Duplicate, Export… (D24), Ground, Show in Finder (Explorer, Files), and Delete for
the user's own or Reset to original for a changed built-in. Built-in scenes cannot be deleted. Rename happens in
place on the card: Return keeps, Escape leaves, an empty name changes nothing. Duplicate makes "Sit 2" among Yours and
opens it. A pill in the card's corner says Just made or Changed and gives way to PLAYING. A dark bar above the dock
says what just happened, with Undo for 8 seconds after a delete. Every change is kept as it is made (A12).

**D14 — New scene.** A sheet of 1072 × 688 over the dimmed window, shrunk to fit a smaller one. On the left, five
choices: what it is for (six chips), how time moves (three cards: Journey, Loop, Clock, each with its glyph and one
line), minutes (10–90 for a journey, 30/60/90 for a loop, none for the clock), start from (Empty, or the sounds of a
scene with the same purpose) and ground (four swatches), then the name, preselected so typing replaces "New scene".
What it is for sets the kind of time and the ground until the user picks those, and always sets the phases: Settle,
Drift, Sleep for sleep; Arrive, Sit, Return for meditation; Work and Break for a focus loop; the four parts of the day
on a clock. On the right the scene's own ground with the dial as it will open and a line of its phases. Create and
open plays it; an empty scene opens the add panel for its first sound.

**D15 — Where a ring is heard.** Below the ring's play panel, a second panel: "Where it is heard" with the words
("A little left", "Wanders around the right", "Crosses left to right", "Flits around the middle"), a live meter, a
slider from Left to Right that snaps to the middle near it, width as Point, Narrow or Wide, and four moves on a pill
track: Stays, Wanders, Crosses, Flits. A moving ring adds how far it goes (A little, Half, Side to side), and its move's
one setting: one swing in 60, 20 or 5 s; over the turn left to right, right to left or there and back; each flit as a
hop, a glide or flying over. The meter is a line from left to right with a band as wide as the sound is spread and a
dot in the sound's colour where it sits now; each row has a small one under its amount, with the words as its tip.

**D16 — The menu bar.** The window is for building a scene; the menu bar is for living with it, and closing the window
never stops the sound. The icon is a dial on the 24 grid, drawn as a template image on macOS so the system tints it:
a loop's arc fills through the lap with a dot at its last phase (the break), a journey's arc shrinks as its time runs
out, the clock's sun stands at the time of day, two bars mean paused, and a dot in a faint ring means nothing is open.
It is redrawn only when it changes, a 96th of a turn at most. The menu: the scene's name and where it is ("18:40 to
Break · lap 3", "12:00 left · ends 22:47", "14:32 · Afternoon", with "Paused ·" in front when paused), Pause or Play,
"Skip to Break" (the next phase, never on the clock or past a journey's last), every scene with its small dial and
⌘1–⌘9, Stop after (Never, After this lap for a loop, In 30 minutes, In 1 hour), Open Soundmote ⌘O and Quit ⌘Q.
Choosing a scene plays it and leaves the window as it was. Stop after pauses by itself; after a lap it pauses at the
start of the next.

**D17 — Bedside and morning.** Bedside: a playing dial left alone for 15 seconds dims to near black with a faint ring,
the hour in light type and one faint line ("21 min left", "waking at 07:00", or the scene's name for a loop or the
clock), when the scene is for sleep or the hour is between 22 and 7, unless Settings turns it off (D18). Nothing on it is brighter than 30 % white. Any
pointer move or key ends it; the first key and the first click only wake it and do nothing else. Morning: below a
journey's phases, how it ends: "Then silence" with "Wake me instead", or "Then wake at 07:00" with − and + in
quarter hours, a line saying what rises and when, and "End in silence". After the journey the clock shows the time of
day and "waking at 07:00"; when the morning rises it says "good morning", the bedside clock gives way, and the window
turns to Sky.

**D18 — Settings.** A sheet of 960 wide over the dimmed window, on the ground of the scene behind it, opened by the
sliders button in the strip, ⌘, or the menu bar. There is no Save: every change is kept at once. Sound: how long a
scene fades out when Stop after ends it (3, 10 or 30 s; play and pause stay short), and keep playing when the window
closes. Starting: show in the menu bar, and what opening does (the scenes, the last scene, or play it). Look: the
ground for new scenes, from what it is for or one of the four, and dim at night. Scenes: the folder, shown in the
system's file browser, and Reset all, which asks once with how many scenes it changes. Below, the keys. On a system
without a menu bar the menu bar and keep-playing rows are greyed. A switch that is on is the ground's ink. At the top,
beside Done, is About: the icon, "Soundmote" and its version, and who wrote it. On macOS the app menu holds "About
Soundmote" and "Settings…" (⌘,), both opening this sheet, with the system's Hide and Quit; nothing in the app
names the toolkit it is built with.

**D19 — First run.** Until a scene has been picked, the library's place holds "Pick what you are doing.": Soundmote's
five scenes as large cards on their own grounds, each with its small dial, kind of time, sounds and Play. Play opens
the dial and plays at 40 % volume. "Or begin with an empty dial →" opens New scene. Then, once each, a hint above the
dock on the dial: click a ring to shape it, drag the hand to move in time, + brings in another sound. Doing what a
hint says, or Got it, uses it up for good.

**D20 — Many sounds.** The add panel (D9) shows families, not sounds: fifteen rows in two columns (S14), each its
glyph, name, a dot per matching sound in its colour and their count; a family without sounds is faint, "Soon", and
does not open. All fifteen fit without scrolling at 1280 × 800 (rows 40 high), so the panel never grows with the
library. Above them, one line:
"Anywhere ▾" and "Any mood ▾", menus whose chips pop up under them and close on a choice; a menu that narrows is
filled. They list only the words the sounds in view have, and a family drops a choice it has none of. A narrowed
panel hides the families with no match. "In this scene" shows what already plays. A family opens in place (‹ back,
or Escape) into its sounds; a search shows the matching sounds of every family. A sound's second line is its places
and moods ("Indoors · cosy"); its character is the tooltip. Once a sound is on trial, "Goes with" offers one sound
from each of up to four other families that share most places and moods with it and are not yet in the scene. The
starting shapes show only when there are sounds to try or one is on trial.

**D21 — Waver.** A ring whose sound reads the Waver knob (S17), every pad, shows a third panel under "Where it is
heard": "Waver", a slider from Still to Wavers and the swing it reaches in cents ("±15 cents"). A sound that does
not read it shows no panel, so a rain is never given a knob that does nothing. Turning it is heard at the next block
and kept with the scene.

**D22 — A scene is a journey.** A scene holds the ear by changing, not by stacking, as Unwind does: a bed that
carries, and over it sounds that each have their moment, one rising as another falls away over minutes, so the
listener drifts along and looks forward to what comes next. It may use many sounds, but they take turns, at most two
or three leading at once; a single strike (a bell, a bowl) marks a turn rather than a new layer joining the crowd.
Fall Asleep hands soft rain to a breathing pad over a slow heartbeat (56 beats a minute), then to a deep drone with a
lake at its edge, and ends on distant surf. Deep Work lays a low noise under rain on a window, which gives way to a
stream and the stream to wind in the firs, with a bell into a break by the fire and a warm pad. Sit arrives by a
stream, sits with a sung bowl and a breathing pad, and returns to birds and a shimmer. All Day passes through the day
one sound at a time: the sea and an owl's wood at night, birds at dawn, a stream, wind in the grass at noon, rain,
birds by a pond, crickets at dusk. A built-in ring is named after its sound ("Soft rain", "Deep drone"), never a
short name of its own, so the dial always says which rain or which pad is playing.

**D23 — Tempo.** A ring that keeps the beat, every rhythm, shows a "Tempo" panel under "Where it is heard": a slider
from Slow to Quick, 40 to 120 beats a minute, and the tempo in words ("60 beats a minute"). It is the scene's tempo,
not the ring's: every rhythm in the scene follows it and they stay in time, so it shows the same on each rhythm's ring.
A resting pulse, 60, is the default; Unwind moves at 90.

**D24 — Export.** A scene leaves the app as a file through a sheet of 560 wide over the dimmed window, on the ground of
the scene behind it, like Settings (D18). It opens from a card's menu (Export…), from "Export to a file…" under the
dial's left panel, and with ⌘E. "Export Fall Asleep" over one line of what it does, then two rows: Format, a pill
track of M4A and WAV (M4A first: small and played everywhere) with what each is in words, and Length, which is the
whole journey, 1, 2 or 4 laps of a loop, or 1, 2 or 4 hours of a clock scene from an hour set with − and +. A line
under them gives the size and the fade at the end ("About 65 MB · fades out over the last 5 s"). On a system without
an AAC encoder the track holds WAV alone and the line says why. Export… is the sheet's one filled button: it opens the
system's save dialog with the scene's name, and the sheet then shows a thin bar with "34 % · about 2 min left" and
Cancel. Afterwards it says "Saved Fall Asleep.m4a" with Show in Finder and Done; a cancel or a failure says so in the
same place and the choices are there to try again. Sound plays on while a scene exports.
