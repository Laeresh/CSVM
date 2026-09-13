# The golden-image tripwire

Pinned `--det` captures, each reduced to one md5; `manifest.json` lists them and is where the
count is read. `RunTests.ps1` re-renders them and compares; a mismatch names the shot and leaves
the actual PNG in `.scratch/goldens/` next to that run's engine log. Nothing here is a picture.
`manifest.json` holds command lines and hashes only, which is what keeps it inside the repo's
no-game-assets rule.

A shot's `frame` is checked before its hash, off the `frame=N clock=<sim|render>` field the capture
prints: photographing a different moment is a clock regression rather than a pixel one. The capture
names its own counter. A run with a session reports its sim clock; a menu screen has none and
reports the rendered frames its countdown waited, which is the coordinate an animated background's
picture is a function of.

## How to run it

```
.\RunTests.ps1                              # goldens are a stage of the normal run
.\RunTests.ps1 -SkipUnits -SkipEngine       # the goldens alone
.\RunTests.ps1 -RegenGoldens                # re-render and rewrite the hashes
.\RunTests.ps1 -SkipGoldens                 # skip the stage (it is the slow one)
```

Reproducing one shot by hand is the manifest entry verbatim plus an absolute `--screenshot=`:

```
& "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe" `
    --path CSVM res://scenes/Main.tscn -- `
    --freecam --chapter=C1 "--pos=-7720,60,-3380" "--lookat=-7868,40,-3449" --det --mute `
    --frames=120 "--screenshot=Z:\Crimson Skies\.scratch\one.png"
```

The `[core] shot pixmd5=… size=… gpu=…` line every capture now prints is the hash — so any
screenshot in this project can be turned into a golden by copying that value into `manifest.json`.

## What the hash is over, and what that costs

The md5 is over the **raw pixel buffer** (`Image.GetData()`), never the encoded PNG: all 881 of this
install's C1 texture PNGs differ byte-wise while being pixel-identical, so a file hash reports
encoder state rather than pixels (`docs/verification.md` SHOT-6).

The price of a raw-pixel hash is that it is a property of **this machine's GPU**. A driver or card
change legitimately moves every hash at once, and the manifest's `gpu` field records what the
current numbers were drawn on. The stage prints a loud line when the running adapter differs from
the recorded one; **that is the case where regenerating is the correct response**, and the commit
message says so. Every other mass flip is a defect.

One shot depends on the machine in a second way: `campaign-4p-grid` names a campaign profile,
`csvm-golden`, that lives in `user://Profiles/` and that no checkout carries, so it is pinned as a
campaign launch flying with no director. A machine whose cabin holds a profile of that name renders
a different frame from the same tree (`docs/verification.md` GOLD-17).

## The rule this file exists to enforce

**A landed visual change updates `manifest.json` in the same commit and names the shots it moved in
the commit message. An unexplained flip is stop-the-line.** A hash regenerated in a separate
"fix the goldens" commit is indistinguishable from a hash regenerated to bury a regression.

**That explanation goes in the commit message, and only there.** `exercises` says what a shot covers
**today** — one sentence, under 250 characters, optionally plus its measured frame-sensitivity.
**Rewrite it on a re-pin; never append.** A commit hook enforces both halves and names the offending
shot: over the cap, or carrying an item id, a date, or an "also exercises …" clause. The re-pin
history is `git log -p analysis/goldens/manifest.json`; a durable finding about the *mechanism*
belongs in `docs/architecture.md` or `docs/formats/`.

## Goldens are a tripwire, not a diagnosis

A failing shot tells you *that* pixels moved, never *why*. Do not diagnose by staring at the diff —
go to the headless instruments: `--tex-census` / `--tex-override` for "is this surface drawing",
`--debug-anim` for pose, condition and emitter state, `--perf` for the frame split, the mesh lab for
shading and normals. The saved `.scratch/goldens/<shot>.png` is for eyeballing *which* thing moved so
you know which instrument to reach for.

## What the set does and does not cover

Each entry's `exercises` field says what that shot covers and carries its measured
frame-sensitivity — frame N against N+1, which is
the check that a pose has any animated surface in it at all (SHOT-12: a pose that renders identically
twice proves nothing, because most poses show nothing that moves). Eight shots move on a one-frame
perturbation (`c1-flight-kill` 78.07 %, `campaign-4p-grid` 73.79 %, `c1-cockpit` 43.35 %,
`empty-stage` 12.21 %, `c4-snow` 3.74 %, `campaign-intro-fill` 3.89 %, `c1c-rain` 2.50 %,
`c1-waterfall` 1.28 %); the rest are geometry-and-shading shots and say so.

**The 2-second window is itself a gap.** Every shot is captured at frame 120 = **2.00 s** of clock
(`viewer-bhawk` 30, `c1-crash` 20, `c1-stunt-marker` 90, `c1-flight-kill` 280, `c1-debris-rest`
360), so anything whose period is seconds long is barely sampled: the
ground-vehicle route animations loop on 1.0 s and up, and two rollovers do not move a car far enough
to change a hash. A change to authored animation *timing* can pass all 13 untouched and still be
wrong — `BL-237` (2026-08-02) was exactly that, and a long-horizon unit test covers it instead.
`campaign-4p-grid` and `campaign-intro-fill` share that 2-second window too, so neither one samples
a campaign mission's later minutes.

**Not covered, deliberately.** The `TextureCycler` flipbooks are below screenshot resolution — the
water frames differ by ~2/255 and no pose in this set moves more than 9 px across a full cycle
(SHOT-3), so `--debug-anim`'s per-flipbook frame log remains the only instrument for them. Sound is
muted in every shot. The labs are unrepresented; add a shot rather than assuming they are watched.
Of the front end, only Original's top level is pinned, for the movie running behind it; C2B, the
least played map, has no shot. `campaign-4p-grid` and `campaign-intro-fill` cover a co-op campaign
launch and its intro collapse alone — a mid-mission cutscene trigger, a guest capture, a downed
pilot's spectator camera and a cutscene skip all need a human at the controls (`playtest.md`
`PT-90`–`PT-93`) rather than a scripted frame. `c1-cockpit` pins the **shipped** cockpit pass alone:
`--no-cockpit-pass` draws the same panel in the main world instead, 5.10 % of the frame apart, and
no shot pins that path. `c1-flight-kill` flies the chase-camera exterior under its own held input
(straight and firing, then a gentle bank), so a moved hash is read across the two: both moving is a
world or flight change, `c1-cockpit` alone is the interior.
