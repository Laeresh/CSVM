# Can Godot render a correct frame on a Windows desktop nobody is looking at?

`RunTests.ps1` launches the engine about nineteen times. Those windows are created without focus and
the engine hides its own as soon as `_Ready` knows the flags, but the window still **exists** for the
second Godot takes to boot, so a test run flashes game windows over whatever the user is doing.

A window belongs to the desktop the creating process was started on, and only one desktop is ever
displayed. Starting the process on a second desktop therefore decides the question before the process
runs — which rule 107 says is the only kind of placement that works. The open question was whether
the GPU still produces the right pixels there. It does.

Kept here because `HiddenDesktop.ps1`, `docs/tooling.md` and `docs/HISTORY.md` cite these numbers and
`.scratch/` is swept.

## The script

- `probe.ps1` — creates a desktop with `CreateDesktop`, launches one golden shot on it via
  `CreateProcess` with `STARTUPINFO.lpDesktop`, and answers three questions separately.

## What it measured

Running C1's `c1-waterfall` golden (`--freecam --chapter=C1 --pos=-7720,60,-3380
--lookat=-7868,40,-3449 --det --mute --frames=120`) on a desktop named `csvm-tests`:

| question | instrument | result |
|---|---|---|
| does it render correctly | the engine's own `shot pixmd5=` vs `analysis/goldens/manifest.json` | `0bb2532d29261339fb11e3eda79403d0` — **exact match** |
| is it invisible to the user | `EnumWindows` on our own desktop | **0** of 52 samples |
| did it actually go elsewhere | `EnumDesktopWindows` on the new desktop | **50** of 52 samples |

**All three are needed.** "No window appeared on my desktop" is equally consistent with success and
with the process dying on startup, so the third row is what separates them (rule 122) and the first
is what proves the GPU work still happened.

Confirmed at full scale afterwards: a complete `RunTests.ps1` on the hidden desktop passed 152 units,
9/9 engine suites and 11/11 goldens hash-identical, while a probe sampling our own desktop every
50 ms saw a Godot window in **0** of 700 samples with Godot alive in 697 of them. Perf is unaffected —
draw counts identical to the visible-desktop run (198.2 / 900.5 / 907 / 2181 / 1192.1) and frame
costs within noise.

## What was rejected on the way

- **`display/window/size/mode=1` (create minimized)** — a minimized window does not render. 6 of the
  11 goldens collapsed onto one identical blank hash *and the other 5 passed*, so it reads as a
  partial regression rather than a broken instrument (rule 121).
- **`--position` far off-screen** — Godot clamps it to keep about a third of the window on the
  desktop: 5184 and 10000 both land at 4686 on a 5120-wide desktop. A goldens run passed green and
  "proved" off-screen rendering works, while a rect probe showed the window had never left the screen
  (rule 122).
- **An always-on-bottom window flag** — does not exist; the `WindowFlags` enum was read out of
  `GodotSharp.dll` to be sure, and `WindowMoveToForeground` has no opposite.
