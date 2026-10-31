# Architecture

How Soundmote is put together. Ids are append-only (see CLAUDE.md).

**A1 — Projects.**

```
Soundmote.slnx
Directory.Build.props        net10.0, Nullable, implicit usings, warnings as errors in Release
Directory.Packages.props     central package versions (see dependencies.md)
NuGet.config                 nuget.org only
src/Soundmote.Core           the sound engine: DSP, sounds, scenes, renderer, export, WAV writer — no Avalonia, no OS calls
src/Soundmote.Audio          live output per OS behind IAudioOutput (A7), the system's AAC encoders (A17) — no Avalonia
src/Soundmote.App            shared Avalonia UI with no screens: the Dial control, view-models, styles, tokens (A9)
src/Soundmote.Desktop        the desktop executable and its screens: Program, Shell/MainWindow, Views/LibraryView, Views/PlayerView,
                             Views/NewSceneView
tools/Soundmote.Sandbox      console prototype: play, render to WAV, measure levels, prepare recordings (S13),
                             make the pads and the rhythms' hits (S16, S18)
tests/Soundmote.Core.Tests   curves, sounds, renderer, determinism, no allocation on the audio thread, export (xUnit v3)
tests/Soundmote.App.Tests    view-models, curve words, dial geometry and input, headless (Skia)
tests/Soundmote.Desktop.Tests the window rendered at 1280 × 800 into bin/…/snapshots for review
Docs/
```

Dependencies point one way: `Desktop → App → Core`, `Desktop → Audio → Core`, `Sandbox → Audio`. App never
references Audio; the head hands it an output (A9). Core and Audio never reference Avalonia. A `.axaml` with
`x:DataType` on a view-model is a screen and lives in Desktop; controls and styles live in App.

Every sound plays a sample: a recording (S13), or a pad or a rhythm's hit the sandbox makes (S16, S18). The samples live in
`src/Soundmote.Core/Recipes/Samples` as Ogg Vorbis and are copied beside the app the ordinary way; the recordings'
originals and the made sounds' WAVs stay outside the repository.

**A2 — Folders in Core.**

| Folder | Holds |
|---|---|
| `Dsp/` | Building blocks with no idea of sounds: `Rng`, noise, `Svf`, `OnePole`, `Smoother`, `RandomWalk`, `Reverb` |
| `Sounds/` | `Sound`, the base of every sound; `Choir`, the one hand-written voice engine recipes call; `Sample`, an audio file in memory (S12) |
| `Recipes/` | `Recipe` (the JSON format), `Expr`, `RecipeSound` (plays a recipe), `RecipeLibrary` and `Library/*.json` (S10) |
| `Scenes/` | `Curve`, `Layer`, `Scene`, `TimeKind`, `Intent`, `Ground`, `Play`, `Place`, `SceneRenderer`, `BuiltInScenes`, `SceneFile`, `SceneStore`, `SceneExport` |
| `Output/` | `IAudioSource`, `IAudioOutput`, `AudioFormat`, `IAudioFileWriter`, `IAudioFiles`, `WavWriter` |

**A3 — A scene is a dial.** A `Scene` has a `TimeKind`, a length and layers. A `Layer` is one ring: a sound kind,
a `Curve` over the turn (position 0–1, amount 0–1), a colour, drift, reverb send and level.

| TimeKind | One turn is | Position | When it ends |
|---|---|---|---|
| `Journey` | the scene's length | elapsed / length, held at 1 | every ring falls to 0; `Finished` |
| `Loop` | the scene's length | elapsed mod length; `Lap` counts | never |
| `Clock` | the real day | local time of day / 24 h; speed and seek are ignored | never |

The amount is not a volume. Each sound decides what more means: rain gets denser, wind faster, the choir gains
voices. The layer's `Level` is the volume, applied after the sound.

**A4 — One mix, one room.** Per block of up to 512 frames, `SceneRenderer` reads each curve at the hand, multiplies
by the layer's drift (a 25 s random walk, ±`Drift`), renders the sound, applies the smoothed level, and sums into a
dry bus (placed between left and right, A13) and a reverb send. One `Reverb` serves all layers, so the scene shares a space. The sum passes a soft
limiter: linear to 0.7, then a tanh bend that never reaches 1.

**A5 — The audio thread.** `IAudioSource.Render` is called on the output's own thread. Inside it: no allocation,
no locks, no I/O, no exceptions. Every buffer is allocated in the constructor. Controls set from other threads
(`Speed`, `Paused`, `Seek`, mute) are single fields written with `Volatile` or `volatile` and read once per block.
`Rendering_does_not_allocate` guards this.

**A6 — Same seed, same sound.** All randomness comes from `Rng` (SplitMix64) seeded from `Scene.Seed`; each layer
forks its own stream in layer order. A render is a pure function of scene, seed, start and speed, so a scene can
be reproduced exactly, and changing the seed gives a new variation of the same scene. Variation inside a play
comes from `RandomWalk`s, never from the clock.

**A7 — Output seam.** `IAudioOutput` pulls blocks from an `IAudioSource`. On macOS, `AudioQueueOutput` calls
AudioToolbox directly through `LibraryImport`: 32-bit float stereo at the scene's rate, three buffers of 1024
frames, filled on the queue's thread. An exception in the source plays silence instead of reaching native code.
Windows and Linux have no live output yet (N1); `WavWriter` renders offline on every OS.

**A8 — Speed.** `Speed` is scene seconds per real second. Curves follow scene time; the sound's own movement (gusts,
swells, chord changes) follows real time. A journey heard at ×30 therefore sounds like the real thing with its
curves compressed, not like a fast-forward.

**A9 — The app around the engine.** `App` asks its `IAppHost` (the head) for a window and an `IAudioOutput`, which
is null where the OS has no live output (N1). `PlayerViewModel` owns one scene: it builds the `SceneRenderer`,
starts the output at once with the renderer paused, and plays by clearing `Paused`, so play and pause fade instead of
opening and closing the device. The engine never calls the UI: a `DispatcherTimer` at about 30 frames a second calls
`PlayerViewModel.Update`, which copies the hand, the amounts and the clock text into bindable properties. Commands
go the other way through the renderer's thread-safe members (A5). A reshaped ring is a new immutable `Curve` handed
to `SceneRenderer.SetCurve`, one reference swap the audio thread reads at the next block; the scene record keeps the
original for Reset. Each launch picks a new seed (A6).

Above the player sits `ShellViewModel`, the window's view-model: the library of scenes and at most one open
`PlayerViewModel`. The head hands it a way to make outputs, not one output; each opened scene gets its own, and the
player stops and disposes it when another scene is opened. The timer calls `ShellViewModel.Update`, which also moves
the open scene's library card. The player hands its `TimeProvider` to the renderer, so a clock scene and the end
time read the same clock, and tests can stop it. The shell also keeps scenes (A12): each library card holds its scene
as it is now, and the player raises `Edited` when a ring is shaped, added, removed or plays another way.

`MenuBarViewModel` sits beside the shell and reads the same open player; the timer calls its `Update` after the
shell's. The head turns it into a `TrayIcon` with a native menu (`Desktop/Shell/MenuBar`), since a tray cannot say
where it is and so cannot anchor a window of our own. While the menu bar exists the app shuts down only on Quit or ⌘Q:
closing the window hides it, a click on the dock icon (macOS) or the tray icon (Windows) shows it again, and the scene
plays on. On Linux, where a desktop may have no tray, there is no menu bar and closing the window quits.

**A10 — Rings come and go while playing.** `SceneRenderer` keeps two arrays of tracks: the visible ones, which
indexes refer to, and the playing ones, which the audio thread reads once per block. `AddLayer` builds the sound on
the caller's thread and publishes new arrays; the ring fades in. `RemoveLayer` drops the track from the visible array
at once and marks it removed: it fades out over 50 ms, is skipped once silent, and leaves the playing array at the
next edit. The audio thread never allocates or waits for either. Seeds for added rings come from their own generator,
so the audio thread's stays untouched (A6). A sound on trial in the add panel is an ordinary added ring that the view
marks as a trial.

**A11 — How a ring plays.** A layer's `Play` is a `ShapePlay` (its curve gives the amount), an `OnOffPlay` (stretches
at one level, with a fade at each end in scene time) or strikes: `EveryPlay` (a fixed interval from a first time) and
`RandomPlay` (about n per 10 minutes, busier where its `Busy` curve is high, never closer than a gap). Strikes are
worked out once per play on the caller's thread, from a seed, so the dial draws exactly the strikes that will sound;
Shuffle moves the seed on. The audio thread fires each strike when the hand passes it while playing, never across a
seek or a jump; a strike swells in over 0.15 s and rings away over about 1.6 s, in real time, whatever the preview
speed. `SetPlay` swaps the play and its strikes as one reference, as `SetCurve` does for a curve.

**A12 — Scenes are files.** `SceneFile` writes a scene as JSON and reads it back: name, intent, ground, kind, length
in seconds, seed, room, phases, and per ring its name, its sound as a recipe id, colour, drift, send, level, waver (S17), curve
as `[at, amount]` pairs and play. The id is the file name. A file read back renders the same samples (A6); a file from a
newer Soundmote or a missing field is a `SceneFileException` that says what is wrong. A ring whose sound has left
the library is left out, so a scene still opens after a sound is retired.
`SceneStore` is the folder: Application Support on macOS, AppData on Windows, the XDG data folder on Linux. A write
goes to a `.tmp` file and replaces the old one in one step. A file that does not read is skipped and named, never
deleted.

There is no Save button. `ShellViewModel` marks a changed scene and writes it once it has rested a second, so a drag
writes once; another scene changing, or the app closing, writes it at once. Built-in scenes have fixed ids; a changed
one is stored under its id and shown instead of the code's version, marked Changed, and Reset deletes the file. The
user's own scenes get an id from their first name, which a rename keeps. Each launch gives every scene a new seed
(A9), so the seed in a file matters only for a scene read back in the same launch.

**A13 — Where a ring is heard.** A layer's `Place` has a pan (-1 left to 1 right), a width (0 a point, 1 as wide as
the sound) and a `Move`: Still; Wander, a slow random walk within a range of the pan; Cross, a `Path` curve over the
turn; Flit, a new spot within the range, glided to over `Seconds`, at each strike for a ring of strikes and otherwise
every 6 to 24 s. Per block the renderer narrows the stereo sound to its width (mid and side), then pans it with equal
power scaled so the middle is unity: left² + right² stays 2, so moving never changes loudness, and the middle and
both ends are exact, so a centred ring sounds as it did before places existed and a hard pan is silent on the far side.
The gains glide across each block. Only the dry sound is placed; the reverb send hears the sound as it is, so the room
stays wide. Each ring's `PlaceMover` has its own generator, seeded beside its strikes, so a still ring takes nothing
from the scene's (A6). `SetPlace` swaps the place as one reference; `PanOf` reads where it is now.

**A14 — A journey can end by waking.** A journey's `Wake` has a time of day, a rise (10 minutes) and rings of its
own. While one is set the journey is never `Finished`: its rings fall silent at the end as before, the hand stays at
the end, and the player keeps the output running. Each block the renderer reads the local time, as a clock scene
does: the morning's rings are at 0 until the rise begins, climb to full at the wake time, and stay full until stopped.
A rise that was missed, because the scene started after it, stays silent until the time comes round again; a rise
across midnight is found. The morning's rings are built with their own seed and drift generator, so setting a wake
never changes how the journey sounds (A6). `SetWake` swaps the morning as one reference; `WakeAmount` says how far
it has risen.

**A15 — Settings are a file.** `Preferences` is an immutable record kept as `settings.json` beside the scenes folder
by `PreferencesStore`, written in one step like a scene (A12). A missing or broken file reads as a first run. The shell
holds it, writes each change at once and raises `PreferencesChanged`; the app answers by creating or disposing the
menu bar and setting what closing the window does, and the player takes the fade for Stop after. A shell made without
a store, as in tests, starts from `Preferences.Seasoned`: no first run and no hints. The pause fade is a straight line
whose length `SceneRenderer.FadeOutSeconds` sets; playing again resets it to the short fade.

**A16 — One beat for every ring.** A scene has a tempo (60 beats a minute by default; 40–120). `SceneRenderer`
counts the scene's beats since it began, paused or not, at that tempo, and before each block hands every sound the beat
and the tempo (`Sound.Beat`, `Sound.Tempo`). A rhythm places its hits on that beat (S18), so every rhythm, one added
midway included, plays in time with the rest, and a new tempo is heard from the next block without moving a pitch.
Seeking and speed move the scene's hand, not the beat. A sound played alone carries its own beat on.

**A17 — Export to a file.** `SceneExport.Render` builds a `SceneRenderer` of its own for the scene as it is now, at
speed 1 and 48 kHz, and pulls blocks of 2048 frames into an `IAudioFileWriter` as fast as the machine allows (about 60
times real time for a journey); the live renderer and its output are never touched. Length: a journey whole (its
morning left out, A14), a loop in laps, a clock scene for hours from an hour of the day. A clock scene reads a
`TimeProvider` of the export's own, in UTC, that moves on with the frames rendered, so it plays the stretch of day
asked for. Every sample is scaled to -1 dBFS (the limiter stays below 1, A4, and an encoder may overshoot), with a
50 ms fade in and a 5 s fade out. A `CancellationToken` is read between blocks; a cancelled file is left for the
caller to delete.

`IAudioFiles` says which formats the system writes, best first, and makes a writer. Core writes WAV (16-bit, dithered;
the header's sizes are written on dispose, and 32-bit sizes cap a file near 6 hours). M4A is AAC at 192 kbps through
the system's own encoder, so no package is needed: `MacOS/M4aWriter` uses ExtAudioFile, which takes the engine's float
stereo; `Windows/M4aWriter` uses Media Foundation's sink writer in an MPEG-4 container, fed 16-bit PCM because the
encoder takes nothing else, with COM methods called by their slot. Linux has no AAC encoder of its own and writes WAV
only. A file carries `AudioTags`: the scene's name as title, Soundmote as artist and Scenes as album
(`SceneExport.TagsFor`). WAV keeps them in a LIST INFO chunk after the audio (INAM, IART, IPRD); the macOS M4A writer
hands them to the audio file under ExtAudioFile as its info dictionary, which becomes MPEG-4 atoms. Windows M4A has no
tags yet (W11). The head hands the app its `IAudioFiles` (A9); `ExportViewModel` runs the render on the thread pool with a
`Progress<double>` back to the UI, and deletes the file when the render is cancelled or fails.
