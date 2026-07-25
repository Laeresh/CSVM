# The golden-image tripwire

Eleven pinned `--det` captures, each reduced to one md5. `RunTests.ps1` re-renders them and compares;
a mismatch names the shot and leaves the actual PNG in `.scratch/goldens/` next to that run's engine
log. Nothing here is a picture — `manifest.json` holds command lines and hashes only, which is what
keeps it inside the repo's no-game-assets rule.

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
encoder state rather than pixels (`docs/verification.md` rule 36).

The price of a raw-pixel hash is that it is a property of **this machine's GPU**. A driver or card
change legitimately moves every hash at once, and the manifest's `gpu` field records what the
current numbers were drawn on. The stage prints a loud line when the running adapter differs from
the recorded one; **that is the case where regenerating is the correct response**, and the commit
message says so. Every other mass flip is a defect.

## The rule this file exists to enforce

**A landed visual change updates `manifest.json` in the same commit and names the shots it moved in
the commit message. An unexplained flip is stop-the-line.** A hash regenerated in a separate
"fix the goldens" commit is indistinguishable from a hash regenerated to bury a regression.

## Goldens are a tripwire, not a diagnosis

A failing shot tells you *that* pixels moved, never *why*. Do not diagnose by staring at the diff —
go to the headless instruments: `--tex-census` / `--tex-override` for "is this surface drawing",
`--debug-anim` for pose, condition and emitter state, `--perf` for the frame split, the mesh lab for
shading and normals. The saved `.scratch/goldens/<shot>.png` is for eyeballing *which* thing moved so
you know which instrument to reach for.

## What the set does and does not cover

Each entry's `exercises` field carries its measured frame-sensitivity — frame N against N+1, which is
the check that a pose has any animated surface in it at all (rule 80: a pose that renders identically
twice proves nothing, because most poses show nothing that moves). Five shots move on a one-frame
perturbation (`c1-flight` 34.52 %, `empty-stage` 12.21 %, `c4-snow` 3.74 %, `c2b-rain` 3.47 %,
`c1c-rain` 2.50 %, `c1-waterfall` 1.28 %); the rest are geometry-and-shading shots and say so.

**Not covered, deliberately.** The `TextureCycler` flipbooks are below screenshot resolution — the
water frames differ by ~2/255 and no pose in this set moves more than 9 px across a full cycle
(rule 32), so `--debug-anim`'s per-flipbook frame log remains the only instrument for them. Sound is
muted in every shot. Splitscreen, the launchscreen and the labs are unrepresented; add a shot rather
than assuming they are watched.
