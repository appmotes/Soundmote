# Dependencies

Every package must be free and permissively licensed. Versions checked against nuget.org
on 2026-10-19.

| Package | Version | Licence | Used by | Why |
|---|---|---|---|---|
| Avalonia | 12.1.4 | MIT | App | UI framework |
| Avalonia.Desktop | 12.1.4 | MIT | Desktop | Windowing on macOS/Windows/Linux |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | App | Base templates, restyled by our tokens (D1) |
| Avalonia.Fonts.Inter | 12.1.4 | MIT (font: OFL) | App | Same font on every OS |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | App | View-models |
| NVorbis | 0.10.5 | MIT | Core | Reads the recordings, Ogg Vorbis (S12) |
| OggVorbisEncoder | 1.2.2 | MIT | Sandbox | Writes them when a recording is prepared (S13) |
| Avalonia.Skia | 12.1.4 | MIT | App.Tests, Desktop.Tests | Real drawing in headless tests |
| Avalonia.Headless.XUnit | 12.1.4 | MIT | App.Tests, Desktop.Tests | Headless platform for xUnit v3 |
| xunit.v3 | 3.2.2 | Apache-2.0 | tests | Test framework |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 | tests | `dotnet test` runner |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | tests | Test host |

The engine has one package, to read Ogg Vorbis; both Vorbis packages bring System.Memory (MIT), which .NET 10 has
built in. Live output on macOS uses the system's AudioToolbox framework (A7).

**Recordings**

Every recording is CC0 (public domain), so it may be shipped, changed and sold without credit. Where each one came
from is kept outside the repo. The files in `src/Soundmote.Core/Recipes/Samples` are cut from them (S13). The
`pad-*` and `hit-*` samples are not recordings: Soundmote makes them (S16, S18), so they are its own and
come under the project's MIT licence.

| Sample | Licence |
|---|---|
| birds-blackbird | CC0 1.0 |
| birds-dawn | CC0 1.0 |
| birds-gulls | CC0 1.0 |
| birds-morning | CC0 1.0 |
| birds-nightsong | CC0 1.0 |
| birds-pond | CC0 1.0 |
| birds-woodpecker | CC0 1.0 |
| bowl-a-1 | CC0 1.0 |
| bowl-b-1 | CC0 1.0 |
| bowl-c-1 | CC0 1.0 |
| bowl-small-a-1 | CC0 1.0 |
| bowl-small-b-1 | CC0 1.0 |
| bowl-small-c-1 | CC0 1.0 |
| bowl-small-d-1 | CC0 1.0 |
| bowl-sung-a | CC0 1.0 |
| chimes-a-1 | CC0 1.0 |
| chimes-b-1 | CC0 1.0 |
| fire-camp | CC0 1.0 |
| fire-crackle | CC0 1.0 |
| fire-hearth | CC0 1.0 |
| fire-heater | CC0 1.0 |
| fire-small | CC0 1.0 |
| fire-stove | CC0 1.0 |
| forest-alps | CC0 1.0 |
| forest-breeze | CC0 1.0 |
| forest-deep | CC0 1.0 |
| forest-morning | CC0 1.0 |
| forest-pines | CC0 1.0 |
| gong-a-1 | CC0 1.0 |
| gong-b-1 | CC0 1.0 |
| night-cicadas | CC0 1.0 |
| night-crickets | CC0 1.0 |
| night-frogs | CC0 1.0 |
| night-treefrogs | CC0 1.0 |
| night-woods | CC0 1.0 |
| noise-clock | CC0 1.0 |
| noise-fan-ceiling | CC0 1.0 |
| noise-fan | CC0 1.0 |
| noise-plane | CC0 1.0 |
| noise-purifier | CC0 1.0 |
| noise-purr | CC0 1.0 |
| noise-washer | CC0 1.0 |
| rain-heavy | CC0 1.0 |
| rain-jungle | CC0 1.0 |
| rain-skylight | CC0 1.0 |
| rain-soft | CC0 1.0 |
| rain-tent | CC0 1.0 |
| rain-window | CC0 1.0 |
| sea-calm | CC0 1.0 |
| sea-far | CC0 1.0 |
| sea-lake | CC0 1.0 |
| sea-lapping | CC0 1.0 |
| sea-pebbles | CC0 1.0 |
| sea-rocks | CC0 1.0 |
| sea-sand | CC0 1.0 |
| stream-fall | CC0 1.0 |
| stream-forest | CC0 1.0 |
| stream-rapids | CC0 1.0 |
| stream-river | CC0 1.0 |
| stream-trickle | CC0 1.0 |
| stream-woodland | CC0 1.0 |
| temple-bell-a-1 | CC0 1.0 |
| temple-bell-b-1 | CC0 1.0 |
| thunder-close-a-1 | CC0 1.0 |
| thunder-close-b-1 | CC0 1.0 |
| thunder-close-c-1 | CC0 1.0 |
| thunder-close-d-1 | CC0 1.0 |
| thunder-close-e-1 | CC0 1.0 |
| thunder-close-f-1 | CC0 1.0 |
| thunder-close-g-1 | CC0 1.0 |
| thunder-far-a-1 | CC0 1.0 |
| thunder-far-a-2 | CC0 1.0 |
| thunder-far-a-3 | CC0 1.0 |
| thunder-far-a-4 | CC0 1.0 |
| thunder-far-b-1 | CC0 1.0 |
| thunder-far-b-2 | CC0 1.0 |
| thunder-far-b-3 | CC0 1.0 |
| thunder-far-b-4 | CC0 1.0 |
| thunder-far-c-1 | CC0 1.0 |
| thunder-mountain-a-1 | CC0 1.0 |
| thunder-mountain-a-2 | CC0 1.0 |
| thunder-mountain-a-4 | CC0 1.0 |
| thunder-mountain-b-1 | CC0 1.0 |
| thunder-mountain-c-1 | CC0 1.0 |
| thunder-roll-a-1 | CC0 1.0 |
| thunder-roll-b-1 | CC0 1.0 |
| thunder-roll-c-1 | CC0 1.0 |
| thunder-rumble-a-1 | CC0 1.0 |
| thunder-rumble-b-1 | CC0 1.0 |
| thunder-rumble-c-1 | CC0 1.0 |
| thunder-rumble-c-2 | CC0 1.0 |
| town-cafe | CC0 1.0 |
| town-night | CC0 1.0 |
| town-sbahn | CC0 1.0 |
| town-street | CC0 1.0 |
| town-train | CC0 1.0 |
| voices-gregorian | CC0 1.0 |
| voices-monks | CC0 1.0 |
| voices-pilgrims | CC0 1.0 |
| voices-temple | CC0 1.0 |
| voices-throat | CC0 1.0 |
| voices-tibetan | CC0 1.0 |
| wind-firs | CC0 1.0 |
| wind-grass | CC0 1.0 |
| wind-howl | CC0 1.0 |
| wind-mountain | CC0 1.0 |
| wind-strong | CC0 1.0 |
| wind-whistle | CC0 1.0 |

**Later**

| Candidate | For | Note |
|---|---|---|
| miniaudio, directly or through a .NET wrapper | Live output on Windows and Linux (N1) | Licence to confirm when added |
| Manrope | The design canvas's typeface (N8) | OFL 1.1; the font files must be added by hand |
