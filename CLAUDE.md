# Soundmote — working notes for Claude

Soundmote is a desktop app (macOS, Windows, Linux) that generates ambient soundscapes to sleep, work, meditate and
relax to. It is built with .NET 10, and the app will use Avalonia 12. Each scene is a dial: one ring per sound,
the ring's width is the amount over time. It is distributed by direct download, possibly with full source, so every
dependency must be free and permissively licensed (see `Docs/dependencies.md`).

## Communication and Coding Do's and Dont's
- Do not overdo abstractions when a simpler and more readable solution suffice
- Do not complicate when a simpler solution suffice
- Do investigate if there is a chance of reuse, modulisation or componisation
- Do be brief and to the point. Use short sentences.
- Do speak plainly and explain with small code samples or examples.
- Don't try to be nice or unpleasant - just state your findings, your opinions or the facts as-is.

## Documents, and what each one carries

| Document | Carries | Never carries |
|---|---|---|
| `Docs/architecture.md` | How the engine and app are put together: projects, scenes, the audio thread, output seams | — |
| `Docs/design-language.md` | Which shape to reach for in the app and why: tokens, the dial, the dock, words, keys | Defects |
| `Docs/sound-design.md` | How each sound is built and why, and the loudness balance between them | Defects |
| `Docs/pitfalls.md` | What fails silently, the fix, and the measurement that proves it | Anything unmeasured stated as fact |
| `Docs/todo.md` | What must be done, need-to-have / nice-to-have, and how to confirm each | Rules |
| `Docs/dependencies.md` | Every package, its version and licence | Anything commercial |

**Each rule file carries one id per entry** — `A` architecture, `D` design language, `S` sound design, `L`/`P`/`U` pitfalls by
likelihood, `N`/`W` todo. **Cite the id, never the heading** (`see A5`). Ids are **append-only**: a new entry takes
the next free number, and a retired one leaves a gap. Keep the docs current and lean at the end of a session:
reasoning and rules, never a changelog.

## Working rules

- Make changes **directly** in this repo — no worktrees. **The user runs the app and handles all git/commits**;
  do not run `git`/`gh`.
- Never edit RSSmote. It is a reference for patterns only, and Soundmote must not look like it.
- Verify with a from-scratch build, aiming for 0 warnings:
  ```
  dotnet clean Soundmote.slnx
  dotnet build Soundmote.slnx --no-incremental -clp:ErrorsOnly -v q
  dotnet test Soundmote.slnx
  ```
- Sounds are recipes (S10), not code. After changing one, run
  `dotnet run --project tools/Soundmote.Sandbox -c Release -- levels` and copy its suggested `trim` into the recipe
  (S4). Tests guard the window; only ears judge the sound — say so when a change is unheard.
- Code that runs inside `Render` must not allocate, lock, throw or do I/O (A5).
- After changing the dial screen, run the tests and look at `tests/Soundmote.Desktop.Tests/bin/Debug/net10.0/snapshots/player.png`.
- Every test that creates an Avalonia object (a brush counts) is an `[AvaloniaFact]`.
- Every new package goes into `Directory.Packages.props` **and** `Docs/dependencies.md` with its licence.
- A `.cs` file's namespace is its project name plus its folder path (`src/Soundmote.Core/Dsp/Rng.cs` →
  `Soundmote.Core.Dsp`).
- The product is written **Soundmote**; lowercase `soundmote` only in technical identifiers.
- Docs, code, comments and recipes never name other products or say where an idea or a sound was found.

## Code comments: minimal, English, and no archaeology

A comment states **what the code does and what a reader must understand to change it safely**, in the present
tense. It never tells how the code got here: no earlier versions, rejected alternatives or measurements. Those
belong in the docs; a comment may point at an id (`(S3)`), not reproduce it.
