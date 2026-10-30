# Sound design

How each sound is built and why. Ids are append-only (see CLAUDE.md). Every sound is a sample played by a recipe in
`src/Soundmote.Core/Recipes/Library` (S10). Nature, places, voices and bells are CC0 recordings (S13), never
synthesised: made, they do not sound natural. Pads and rhythms are made (S16, S18), as such sounds always are. The recipe
language can still build sounds from noise and oscillators, for a sound described later (N11), but no built-in sound
does.

**S4 — Loudness balance.** Every recipe is trimmed to −23 dBFS RMS at full amount (`RecipeSound.TargetDb`), so
curves mean the same across sounds. A built-in recipe carries its `trim`; the sandbox's `levels` command prints the
trim it should have, averaged over five seeds of 30 s, since one short take swings ±3 dB with the gusts and waves.
A recipe without a trim is measured once when first played (8 s at 24 kHz). `levels` also prints every recipe at
amounts 0.25, 0.5 and 1 with its peak; a quarter of the amount is about 10 dB quieter.

Rule: at full amount every recipe sits within 4 dB of the target with peaks under −1 dBFS. A sparse sound (birds
between phrases, lapping water) reads a few dB under the target in a short window. A sound of sharp ticks has more
than 20 dB between its peaks and its average, so it is trimmed below the target to keep its peaks under the limit:
the wall clock sits at −26.
`Full_amount_lands_in_the_shared_loudness_window` guards it. The demo journey mixes to about −20 dBFS RMS at its
fullest; the limiter rarely engages.

**S10 — Recipes.** A sound is a JSON recipe: data that wires a few building blocks, never code. `Recipe.Parse` checks
every field, holds every number inside its bounds and parses every expression up front, so a recipe that reads will
play; one that does not reads says why ("unknown name 'loud'"). This is what lets a sound be written by hand, by a
tool, or later from a description (N11).

| Field | Meaning |
|---|---|
| `id`, `name`, `family`, `colour`, `character` | Identity; the family is one of S14's, the colour is the ring's (D3) |
| `places`, `moods` | Optional lists of S14's words: where the sound belongs and how it feels |
| `strikes` | For a sound that takes strikes: `every` (the default) or `random`, how it joins a scene (S12) |
| `room` | Reverb send, 0–1 |
| `glide` | Seconds the amount takes to follow the curve |
| `trim` | Gain to the shared loudness (S4); measured when absent |
| `signals` | Named values that move, in order; each may use those before it |
| `parts` | 1–8 sound makers, summed |

Signals: `walk` (seconds: a smooth random wander in −1…1), `wave` ([min, max] seconds per cycle, `rise`, `size`,
`floor`: swells that rise then drain), `lag` (an expression followed late, `seconds`) and `expr`. `perEar: true` gives
a walk or wave its own course in each ear.

Parts, each with a `gain` expression:

| Type | Makes | Fields |
|---|---|---|
| `noise` | Beds, hiss, rumble, wind | `source` (white, pink, brown, or a mix), up to 3 `filters` |
| `events` | Ticks, crackles, a shaker | `rate` per second or `bpm` + `offset` + `jitter`; `level`, `decay`; `burst` [n, m] clicks `spacing` apart; `filters` |
| `pings` | Drops, bubbles, a thump, chirps | `rate` or `bpm`; `pitch`, `glide` (ratio) over `glideTime`, `decay`, `level`; `place` (−1…1) and `pan` (spread around it); `burst` [n, m] repeats of the same tone `spacing` apart; `voices` ≤ 16, `lowpass` |
| `choir` | A six-voice formant choir | — |

Filters: `lowpass`, `highpass`, `bandpass` (peak gain 1), each with `freq` and `q`. Expressions use `amount`, `ear`
(−1 left, +1 right), `waver` (the ring's knob, S17), the signals, numbers, `+ − * / ^` and `sqrt abs min max pow clamp step`. Division by zero, roots
and powers of negatives give 0, never NaN; frequencies are held to 20 Hz–20 kHz, Q to 0.3–30, gains to 0–4.

Expressions, filters and gains update every 32 samples per ear; the parts run per sample in between. Everything is
allocated when the sound is built (A5).

**S12 — Sounds from files.** A recipe's `sample` part plays files from `Samples/` beside the app: `id.ogg` (Ogg
Vorbis, a recording, S13) or else `id.wav` (16- or 24-bit PCM or 32-bit float); mono or stereo, any rate;
decoded whole once, on the caller's thread, and shared. Two ways:
- *A loop* holds one file at a time without a seam: its last `fade` seconds are folded over its first with equal
  power when it is read. Every `change` seconds or so (0.5–1.5 times) a second read head starts somewhere in another
  file and the two cross over `fade`, so a loop of a few takes never repeats in the same order.
- *A one-shot* plays a whole file each time it fires, on its own trigger or, without one, at each of the ring's
  strikes. Each shot takes a random file, a pitch within `pitch` (a few tenths of a percent for a struck body, as
  if struck a little differently), a level within `level` and a place within `spread` of the middle. Eight sound at
  once. A recipe whose one-shots take strikes joins a scene as strikes (every 5 minutes, or with `"strikes": "random"`
four times in ten minutes at random, at least 45 s apart), plays at full amount
  while it rings, and is measured by striking it once at full strength and taking its loudest 4 s within 20 s (the
  first seconds of a bowl, the peak of a rumble that builds), as `prepare` sets its takes.

**S13 — Recorded sounds.** Every sound is a CC0 recording, chosen to these rules:
it is one thing only (rain is rain, never rain with traffic, birds or thunder), so it mixes with the others; it is
recorded at 44.1 kHz or more, without clipping; its body lies below about 5 kHz, since many ears hear little above
that (cicadas that sing at 8 kHz are silence to them), and a recipe whose sound lives high starts its dulling there; and each kind comes in variants that set a mood (rain on a roof, a
tent, a window, leaves, a street). The original stays outside the repo; `Soundmote.Sandbox prepare` cuts it into a
sample:
- the steadiest stretch (45 s; 60 s for the first rains; shorter to leave out a flaw): the least swell, the smallest jump above its own loudness, the least rumble;
- a high-pass below what the sound has (100 Hz for rain) takes out wind, traffic and room rumble;
- a look-ahead limiter (1.5 ms) holds near drops at most 16 dB above the stretch's loudness, and lets go within 6 ms,
  so it shaves a loud drop and never ducks the bed under it (a slow release pumps a dense rain or a fire in short
  gulps); it prints how much of the time it held the sound down 3 dB or more, which stays under 3 % (`--crest`;
  17–20 for fire, whose crackles are the sound), so at the −23 dBFS target (S4) a ring alone peaks below −1 dBFS;
- the peak is set to −1 dBFS, encoded as Ogg Vorbis at 48 kHz, quality 0.6 (near 190 kbit/s, about 1.5 MB a
  minute; it softens the very top a little, about a tenth fewer zero crossings).

It prints what it measured: the loudness range, the rumble below 120 Hz, how far the peaks stood above, how many
samples the original clipped, and the hiss: the band above 6 kHz in the quietest tenth, after the lift. For a lone
singer, a bird between its phrases, that is the recorder's own noise, and of two takes of one sound the quieter wins
(a blackbird at −59 dBFS over one at −48). A recording is rejected and replaced, not repaired, when the original clipped more
than 500 samples, when it must be lifted more than 30 dB (the recorder's own hiss comes up with it), or when the rumble
below 120 Hz jumps far above its average (a bump, a footstep, a passing engine). High-passes: 25 Hz for thunder, 40 for
the sea, fans, clocks, 60 for streams, fire, forests, towns and voices, 100 for wind, rain and night creatures, 150 for birds, 400 for a lone songbird. With `--takes n` it cuts one-shots
instead: a strike starts where a quiet stretch is broken 15 dB above the recording's floor (a quarter second before
it), and ends where it has stayed within 4 dB of the floor for 1.5 s, at the next strike, or after 40 s; the n loudest
strikes at least 20 dB above the floor are kept. Each rises over 10 ms and falls over its last second, its loudest 4 s
are set to −22 dBFS, and its crack is held at most 20 dB above them, so the takes of one sound strike alike. A ticking clock loops with a 50 ms fold
and no change of place, so two ticks never sound at once. Rain plays as one loop part that
jumps to a new place every 30 s or so; less amount is quieter and duller (a low-pass from 18 kHz down to 2.5 kHz), as
if further away. The eight rains are tuned by measurement only.

**S14 — Families, places and moods.** Every sound belongs to one of fifteen families (`Family.All`): rain, thunder,
sea & shore, streams, wind, fire, birds, forest, night creatures, voices & chant, bowls & bells, synth pads, rhythms,
town & traffic, noise & pulse. A family says what the sound is and gives a sound without its own glyph its glyph. The
list is fixed so the add panel stays fifteen rows (D20); a new kind of sound joins a family, and a family waits, shown as coming, until it has one.
A sound also names where it belongs (forest, jungle, shore, town, indoors, outdoors, temple) and how it feels (calm,
cosy, warm, mysterious, wild, bright), from those words only, so a typo fails when the recipe is read. Places and
moods only narrow; a whole place, a harbour or a street at night, is a scene made of sounds from several families.
Forest is the one family that is a place's whole sound, trees, wind, birds and insects together, recorded as one;
every other family is one thing only.

**S15 — Thunder.** Five sounds, each a set of takes cut from CC0 recordings of thunder alone (no rain, no wind), so
rain and thunder are rings of their own and mix. *Distant thunder* (nine takes, up to 40 s, long rolls from far off),
*Rolling thunder* (three: a strike at a distance and its roll), *Thunder crack* (seven: close, sharp, then the
rumble), *Mountain thunder* (five: a storm in a valley below, echoing) and *Thunder rumble* (four: deep and slow, no
crack). They take strikes at random (S12); each strike plays a random take, a few percent lower or higher (more for
the distant, as if further), a little to one side, the distant one with more room. A crack is startling: its mood is
wild, never calm. Tuned by measurement only.

**S16 — Pads.** Made, not recorded: the sandbox's `make` renders each to a 32-bit WAV (kept outside the repository,
for listening) and then to its Ogg sample, its peaks held 12 dB above its loudness and its peak at −1 dBFS. Every pad
uses only D E F G A C (D minor with a ninth, no B), so any two pads, a pad and a tuned rhythm (S18), sound well
together. A pad is a held harmony, never a chord change, and moves inside itself: notes that swell on their own slow
waves, a filter that opens and closes, a breath of four seconds in and six out, and a pitch that wavers (S17). They are
full: up to nine saws a few cents apart across the ears, a sine an octave or two below, a chorus, a long room and a
soft warmth on the loudest moments. A minute long, folded over four seconds, jumping every 40 s or so like a
recording. Vorbis drops the first 1 024 frames it decodes, so `make` puts the pad's last 1 024 in front, or silence
before a hit, and checks the sample plays exactly as long as the WAV. Ten pads: warm, glass, a deep drone, breathing,
a synth choir, shimmer, strings, a tide of harmony, a reed organ, space. Tuned by measurement only.

**S17 — Waver.** A ring has a Waver knob, 0–1 (0.3 by default), that a recipe reads as `waver`; only a sound that
reads it shows the knob (D21). A loop's `tune` is its speed and pitch as an expression, 1 as recorded, held to
0.5–2 and worked out every 32 samples; it is for a loop that does not keep the beat, since a rhythm's speed is its
tempo (A16). Every pad tunes itself by two random walks, a slow one of nine seconds and a flutter of under one:
`pow(2, waver * (40 * slow + 10 * flutter) / 1200)`, so at full Waver it drifts up to half a semitone and at the
default about a sixth of that, and it stays in key with the rest. Both ears read one head, so the whole pad moves
together, like tape that is not quite steady.

**S18 — Rhythms.** A rhythm is a pulse that carries the pads in a calm, ambient or meditative scene, never a beat that
pulls the ear: low, soft hits and tuned notes, nothing bright on top. It is a recipe of one-shot parts, each with a
`pattern`: `every` beats (a cycle), the beats `at` which it hits within it and their `levels`. The hits are single made
sounds (`hit-*`, rendered by `make` without a room; the scene's room is added as they play), so the scene's tempo
(A16) changes their spacing and never their pitch. A part with several files hits a random one each time: the kalimba
plays a new tune on D minor's notes every cycle, the bass a root or a fifth. A hit fires at the start of the update
block its beat falls in, at most 32 samples early. Nine rhythms: a heartbeat (lub and dub on every beat), a soft pulse,
downtempo, frame drums (dum, tek, tek, dum, tek), hand drums on D and A, two low shakers, a kalimba, a bass pulse and
great drums. Less amount is quieter and duller, as if in the next room. Tuned by measurement only.

