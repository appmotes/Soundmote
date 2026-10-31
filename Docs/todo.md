# Todo

What must be done. `N` is need-to-have, `W` nice-to-have. Ids are append-only (see CLAUDE.md).

**N1 — Live output on Windows and Linux.** Only macOS plays live (A7). Decide between a thin own binding to
miniaudio and a .NET wrapper of it; confirm the licence and record it in dependencies.md. Confirm: `play` works
on both OSes and `Rendering_does_not_allocate` still passes.

**N5 — Breathe with it.** A layer can follow a breath cycle (for example 4 s in, 6 s out) on top of its curve.
Confirm: a steady sound (wind, the sea) swells in time with the cycle shown in the app.

**W2 — Seamless laps.** A loop's curves join at the turn, but the sounds' own walks do not know about laps. Check
that the turn of a lap is inaudible.

**N8 — Manrope.** The canvas is drawn in Manrope; the app uses Inter until the font files (OFL) are added to
`Assets/Fonts`. Confirm: the snapshot matches the canvas's type.

**W3 — The two ends of a journey share the top.** A journey's first and last point sit at the same angle, so their
handles overlap on the dial and a press takes the first. The flat view reaches both. Consider drawing the last
point just inside the first, or a small gap at the top. Confirm: both ends can be dragged on the dial.

**W4 — Undo.** Reset returns to the scene's shape, but a single drag cannot be undone. Add ⌘Z for the last edit.

**W5 — A sound's own knobs in the add panel.** The canvas shows sliders per sound (distance, swell). A recipe could
declare one or two `knobs` (a name, a range, a default) that its expressions read like signals, set from the panel
and kept with the layer.

**N11 — Describe a sound.** Recipes exist (S10); the next step is "describe one" in the add panel. A language model
gets S10 as its instructions and the user's sentence, and returns a recipe; the app parses it, sends the parse error
back once if it fails, measures its loudness (S4) and plays it as a trial ring. Needs network and an API key, so it is
optional, off by default and says so. Keep described recipes with the scene file (A12). Confirm: "rain on a tin roof with
distant thunder" plays within a few seconds, and a broken answer ends in a clear message, never a crash.

**N12 — Sounds that are good enough.** Nature, places, voices and bells are recordings (S13); pads and rhythms are
made (S16, S18). 96 sounds from 140 samples, 105 MB of Ogg. Every family (S14) has at least five. Chosen and made by
measurement only; recordings lifted more than 30 dB were dropped as hissy. Listen first for: the pads and rhythms
(fat enough, in tune together, the Waver knob, the rhythms as calm pulses at 50–70 beats a minute), the Gregorian chant (an organ under it, a basilica's
room; it sings on D's notes, its reciting tone A, so it fits the pads; no other CC0 Gregorian chant exists), the window rain, the tent (a few clipped samples),
lapping water (one thump), spring woodland (a room's hum), throat singing (long pauses), the distant thunder's 40 s
takes, the wall clock (once a loop, every 45 s, one tick may fall early or late), and the dense sounds (crickets,
cicadas, rain) whose hiss the measure cannot tell from the sound. Confirm: the user signs off by ear.

**W7 — What saving leaves out.** Saving keeps every change (A12), but three things the Saving board shows are not
built: ⌘Z on the dial (W4), Delete moving the file to the system Trash after the Undo bar has gone (it is deleted
now), and an "Other" length. The length and kind of time cannot be changed after New scene either. Confirm: a deleted
scene can be found in the Trash, and a 25-minute scene can be made.

**W8 — What the menu bar leaves out.** The board draws a popover under the icon with a larger dial, a volume slider
and "Lower during calls"; the tray cannot anchor one, so the menu is the system's own and has neither. A popover needs
the icon's place on screen from each system (a status item on macOS). A scene's small dial in the menu is drawn when
the menu is built and does not follow later edits. On Linux there is no tray yet. Confirm: the popover opens under
the icon on macOS and Windows, and a reshaped ring shows in the menu.

**W9 — A realer morning.** The dawn chorus leads All Day's morning, from 05:00 to its height at 07:00, and the
stream follows to 08:00. Still open: the board's picture that turns to day is the window's ground changing to Sky at
once, not over the rise, and the bedside clock cannot be turned off until Settings exists (its "dim at night").
Confirm: the ground follows the rise.

**W10 — What Settings leaves out.** The board also has which output to play through, quieter during calls (needs to
know when the microphone is in use) and open at login (a login item, a startup entry, an autostart file). Each needs
code for each system. ⌘Z waits for W4. Confirm: each works on macOS and Windows, and Linux says where it cannot.

**W11 — Export beyond this Mac.** Export (A17, D24) is tried on macOS only. The Windows M4A writer calls Media
Foundation by its interfaces' slots, checked against the headers but never run, and writes no tags (title, artist,
album); Linux writes WAV only. Confirm: on
Windows a 45-minute journey exports to an M4A that plays its whole length, and on Linux the sheet offers WAV alone.
Decide whether Linux gets Ogg Vorbis through the encoder the sandbox already uses (MIT), as a compressed choice.
