# Verifying a change in this project

**Read this before measuring anything.** This project verifies through headless screenshots,
pixel diffs, log counts and `--perf` numbers, and every one of those instruments has lied at
least once — usually by returning exactly the answer the hypothesis predicted. Narratives:
`docs/HISTORY.md` and git history. **Per-module constraints live as ⚠ lines in that module's
`docs/architecture.md` bullet — read the bullet before touching the module.**

## The rules

1. **Check a plan's premise against the data before writing code.** Three scheduled items died
   this way — the fire templates one was meant to unblock are never its children, 0 of 1,152.
2. **A premise check needs its own able-to-fail control, and an animation census must cover BOTH
   sources — compiled (`cam_anim`/`mis_anim`) and reader (`zrdr`).** C1's `cloudparent#` is
   reader-only; a compiled-only sweep "disproved" a true premise.
3. **Before scheduling work from a bug report, `git log` the interval since it was filed.** One
   z-fight was fixed the day it was reported (4.87% → 0.09%); two sessions chased the stale entry.
4. **Confirm a symptom is a DEFECT before diagnosing it: does the original do the same?** Three
   diagnoses failed on a pose the original also z-fights — sunk effort makes nobody re-ask this.
5. **Check the case a plan points at can discriminate before measuring it.** A showcase leg held
   0°, equal to the authored rest pose — the one case its real bug cannot touch.
6. **When two coplanar layers look like variants (day/night, LOD), test the geometry
   relationship first.** The 4×-lower-res, 3×-brighter "pair" was base ground under an authored
   subface — 100.000% containment.
7. **Measure the same-build noise floor before believing a difference.** World shots are not
   frame-deterministic; floors measured span 1 px to 13,346 px.
8. **Re-measure the baseline before believing a regression.** An 8.6 ms "regression" was partly
   machine drift.
9. **Measure a pose's sensitivity to ±1 frame on ONE build before believing a screenshot A/B.**
   An 18.32% "regression" reproduced as `--frames=120` vs `121`; `TIME`-driven surfaces need a
   provably equal frame budget, which any shader change moves.
10. **Never baseline with `git stash` — flip the one line under test, build, run, flip back.**
    The stash stack is repo-global across worktrees (it reverts other agents' edits) and without
    `-u` new files stay in place.
11. **When an A/B swaps files, prove the new binary runs — a log line only new code can emit.**
    `Copy-Item` keeps the source mtime, so `dotnet build` no-ops and Godot runs the old DLL.
12. **Verify each branch independently, not just combined.** A split exposed codegen
    registrations sitting in the wrong branch's commit.
13. **Confirm "pre-existing" by reproducing it on the unchanged build, not by argument.**
14. **A clean compile, passing test or unchanged number is not evidence until seen able to
    fail.** A reverted guard still passed — a second guard was doing the work.
15. **A screenshot that would pass identically with a no-op is not a test.** The livery lab
    looked right over an already-painted plane.
16. **A metric going to zero is not the outcome being right.** Raising the depth-bias constants
    collapses a z-fight from 8.42% to 0.01% *while biasing the wrong surface to the front*.
17. **When a fix makes a metric worse, look at the artifact — enumerate the states the FIXED
    code can produce, not the bug's.** An "is it reddish?" classifier scored a correctly-off
    grey panel as absent.
18. **An arithmetic coincidence is not a mechanism — demand the exact value; a magnitude-only
    fit proves nothing.** The blown zeppelin transforms — an unread `spline_interp` flag
    exposing uninitialised spline memory — were proved by 4.6109513952913965e27 matching the
    node's world X bit-for-bit.
19. **"Is it a number" is the weakest check on junk-capable data — prefer a format flag saying
    whether the bytes are meaningful.** A NaN/∞ guard sails past finite garbage.
20. **Isolate to the single node before naming a culprit; prefer a control changing only the
    suspected mechanism over removing geometry.** Hiding a sheet group and hiding one mesh gave
    the identical 0.19% flicker — hiding proves participation, never the partner.
21. **A bounding-box overlap is not an overlap — compute true polygon ∩ polygon area.** Terrain
    is mostly abutting tiles; the AABB substitution inflated a survey from 0.6–1.6% of polygons
    to 9–26% where exact clipping gives zero.
22. **Audit inherited evidence: check what a pre-established claim was computed from.** Neither
    a claim's age nor the number of documents repeating it is evidence.
23. **A residual you have not driven to zero is not a floor.** A depth-only ramp control took a
    "mostly resampling noise" pose from 35.77% to 0.37% flicker.
24. **Bracket a tuning constant's effect before trusting it.** Coplanar surfaces separate only
    above a bias of ≈1e-6 of view distance; `NodeOrderBias` is 5e-8, inoperative within ~40 node
    indices.
25. **Locating coplanar geometry does not say which surface is at fault — establish which one
    the original draws on top.** "86 of 102 polygons at exactly Y = 0.0" licensed a fix that
    would have deleted palms the original shows.
26. **A quantity read from an 8-bit capture is quantised — count the levels crossed; single
    digits means re-encode as a period, not a level.** A `fract(UV)` difference spanning ~1.4
    quantisation steps gave 46 px/texel; a stripe-period probe gave ~4.
27. **When an ID map proves "nothing else is there", check its key — what it cannot distinguish
    hides in it.** Per-*texture* colour scores zero when the crack shows another surface with
    the same texture; the fix was per-*surface* colour, cache bypassed.
28. **File-stored bounding boxes are in the node's own frame; a position predicate usually needs
    the world frame.** "Contains the world origin" gave 464 roots from local `child_bbox` but
    122 — all vehicles — in world space.
29. **"After bootstrap" is not "after everything that places things" — prefer a reversible
    action plus a recheck.** Motions and OnCall defs place entities seconds later; a one-shot
    sweep switched off 35 entities merely not yet in place.
30. **Check whether the camera pose is special before debugging an axis-aligned artifact.** The
    "upper-left quadrant only" render was exact projection — the world origin is the map corner
    (C1 `area` x,z ∈ [-12288, 0]).
31. **A dead-still camera renders bit-identical frames — z-fighting needs `--jitter`.** The
    0.15° default changed 92% of pixels at ground level and told nothing; 0.006° was useful.
32. **Some real effects are below screenshot resolution.** The water flipbook differs by ~2/255
    — a burst reads 0.01% and IS working; verify via the `--debug-anim` frame log.
33. **Check nothing occludes, fogs or washes out the thing under test.** Forcing sprite opacity
    to 0 moved 8 px under the cloud deck and fog; with deck hidden and fog off, 74,129 px.
34. **One camera angle — or one scripted pose — is not a test; sweep and require a flip.** A
    collision relabel moved the logged part at exactly one of five spawn altitudes.
35. **Compare pixel values, not an upscaled crop.** The gauge faces carry dark *unlit* STALL /
    LOW ALT copies (~58,0,0 vs 180+,0,0 lit) that read as lit when enlarged.
36. **Never compare images by encoded bytes.** All 881 C1 texture PNGs differ byte-wise (encoder
    only) while pixel-identical.
37. **`--perf`'s `script` reads ~2.2× the real frame time** (Godot's `TIME_PROCESS`); trust
    `frame`/`fps` for absolutes, `script` only as an A/B ratio.
38. **Numbers pinned at the 60 fps vsync cap are floors.** Read `physics`
    (`TIME_PHYSICS_PROCESS`, ratio-only too) for collision changes; with no monitor on the
    subsystem, call the effect unresolved.
39. **Split the timer before choosing what to optimise — engine setters hide cost.** The clutter
    collision build was 271 ms transform vs 3,403 ms `ConcavePolygonShape3D` BVH build.
40. **Do not assume a cost is on the GPU.** Viewport GPU time was 0.27 ms while the frame was
    ~133 ms of C#; this trap fired twice.
41. **Differences smaller than the instrument are not differences.** 5537 vs 5606 ms over 3-run
    averages is noise.
42. **Watch for cold caches.** An alarming first-run 8.1 s was the OS file cache on 630
    freshly-written JSON files.
43. **A feature can run perfectly somewhere invisible.** 36 of C1's 38 sound emitters are built
    and stopped; `--debug-anim` prints visible-in-tree per node for this reason.
44. **A change can be inert by construction in a chapter — say why, or "byte-identical" reads as
    "didn't work".** 7 of 8 chapters were unchanged because only C1 has `OnStartup` light defs.
45. **The one deviating chapter can be the change working — identify what moved before
    "restoring" identity.** A new handler moved exactly one chapter: the install's single
    `ON_STARTUP` opacity fade, the case it exists for.
46. **A metric can legitimately have to not move.** The anim dedupe fix correctly held the op
    count at 3531; expecting movement would have read as failure.
47. **Two different failures look identical from outside — inject input and log what each
    candidate receives.** "Handler doesn't fire" vs "node doesn't exist" reads the same.
48. **A visibly running system is not proof the numbers are right.** A degrees/radians slip made
    every rotation ~57× too small and nothing visibly turned.
49. **`IsVisibleInTree()` is not "it renders" — when every metric says live and the frame is
    blank, diff against the nearest thing that DOES render.** Crash puffers drawing 462
    instances drew nothing until parented at world level — `TopLevel` emitters draw nothing
    under the player subtree.
50. **Ask what window an instrument covers before concluding from an absence.** The ambient-
    sound census prints inside `Bootstrap`; the police siren builds its emitter one frame later.
51. **A sampler paced by the clock under test cannot see a rate error — take at least one
    measurement per wall frame or second.** Pose-per-sim-second matched while the lab ran at 2×;
    20 sim-seconds in a ~10 s wall run caught it.
52. **Triangulate exactly as `SceneBuilder.EmitPolygon` does before computing normals or areas —
    a `tri_strip`'s raw index list is not an outline.** Treating it as one manufactured a false
    7–14% normal-inversion rate and a bogus 6.8 million m² overlap 2 km away.
53. **A sampling window that ignores the structure it samples describes the wrong bytes.** A
    fixed 90,000-byte window ran past a `.BM`'s base plane and "proved" pre-painted shading maps.
54. **A probe that crashes has told you something — read the failure before "fixing" it.** The
    throw site alone can rule out an entire mechanism.
55. **A tool limitation gets recorded as a data variant.** "24 of 48 SI-script parse failures"
    never existed — the walker lacked the header-declared counts.
56. **A diagnostic can perturb what it measures.** MeshLab's override materials must replicate
    SceneBuilder's vertex stage *verbatim*; its old hardcoded light re-aimed the sun.
57. **Never silently skip a case in a diagnostic — recover and log.** A skipped case is a
    conclusion from half an A/B.
58. **A log line can be structurally unable to show the thing.** The motion line printed
    position only, and a spin turns in place.
59. **A truncated diagnostic list can hide the entity under test *because* the fix worked.** The
    12-motion print cap dropped looping cars (restarts re-register at the end); raise the cap
    for the measurement.
60. **Godot node names are not the game files' names** (`.` → `_`, duplicates auto-renamed) —
    use the `cs_name` meta.
61. **Grep the full stderr, not just the line you expect** — that has hidden a whole class of
    shader error.
62. **Pass `--no-pads` on scripted runs** — a drifting stick silently steers the free camera.
63. **The repo path contains a space — a mis-quoted launch aborts every run instantly, reading
    as "the build is broken".** Use the call operator (`& "path\to.exe" args`) and quote
    comma-bearing args (they can arrive as `System.Object[]`); instant identical failure is the
    tell.
64. **A re-implemented render path must clip at the near plane, not cull — and a "no problem
    found" probe needs its own able-to-fail control.** A culling probe dropped the quads
    underfoot and said "nothing fights"; bias off gave 94.80% coincident.
65. **`Assembly.Location` is empty under Godot's Mono loader, and an exception in `_Process`
    silently kills all per-frame debug logging.** Use an explicit literal build tag, not an
    assembly-mtime probe.
66. **Confirm zero stray Godots (yours and other agents') before believing a broken capture or
    error burst — kill by command line filtered to your worktree.** `Start-Process -Wait` exit 0
    doesn't mean the run ended; strays manufactured 16,576 errors and a 1/7-size capture.
67. **`dotnet build` cannot fail on comment-encoding corruption; Godot's Mono loader validates
    UTF-8 strictly and refuses the file.** After any bulk text rewrite, run the game — the
    damage surfaces as a `Main.tscn` parse error.
68. **`[IO.File]::ReadAllText($path, $encoding)` ignores your encoding when the file starts with
    a BOM.** For byte-preserving rewrites: read bytes, detect the BOM yourself, decode with
    `UTF8Encoding($false, $true)` so invalid input throws, rewrite the BOM you found.
69. **Window focus IS scriptable — `AttachThreadInput` + `SetForegroundWindow` fires real focus
    notifications.** A background `SetForegroundWindow` alone is no-opped by the foreground
    lock; minimising from another process fires no focus notification at all.
70. **To verify one effect, isolate it and remove whatever clears it early.** A fireball's haze
    was read as the (broken) smokeball, and the 1.5 s auto-respawn kept clearing smoke that
    develops at ~2–3 s.
71. **`--screenshot` needs a real GPU context — never pass `--headless` with it.** `--headless`
    selects Godot's dummy renderer, whose `texture_2d_get` returns null, so
    `GetViewport().GetTexture().GetImage()` throws a `NullReferenceException` and writes no file
    (a loud failure, but the run still exits 0). Screenshots run windowed/offscreen without the
    flag; `--headless` is only for the windowless dump tools (`--dump-markers`/`--dump-weapons`/
    `--dump-loadout`) that never read back pixels. ("Headless screenshots" above means the
    automated `--screenshot` workflow, not the `--headless` flag.)
72. **Colliders exist only in the flight build — a collision census in any non-fly mode reads
    zero and lies.** `WorldSession.Options.Collision` is `_fly`, so `--freecam`, `--viewer` and
    `--anim-lab` build the world with NO `StaticBody3D`/`CollisionShape3D` at all. A C25 collider
    check that ran under `--damage-test` (which is freecam) found "0 world colliders" and nearly
    concluded destructibles were non-collidable — false: force `Collision` on (the harness now does
    `|| _damageTest`) and the same world has 1848 colliders, doors and propane tanks among them.
    Before measuring what is solid, confirm the mode you are in actually built collision.
73. **A net collider delta hides a real removal — split it by direction.** Killing a destructible
    both switches its healthy collider OFF and (via the destroyed swap + any chained animation)
    switches wreck colliders ON. The C2 propane gate nets **+8** enabled, which reads as "death adds
    collision" — but the door's healthy collider *did* turn off; the death just added more wreck than
    it removed. Report `off` and `on` counts separately, never the signed sum.
74. **`--screenshot=` takes an ABSOLUTE path, and the run exits 0 even when the save fails — always
    confirm the file exists afterward.** The CLI path is handed verbatim to `Image.SavePng`, which
    does NOT `GlobalizePath` it (unlike the in-game F12 capture), so a relative `--screenshot=.scratch/x.png`
    resolves against Godot's own dir — not your shell's CWD — and logs only a quiet `ERROR: Can't
    save PNG at path` on stderr while the process still returns 0. A screenshot loop can "succeed"
    and write nothing. Pass a fully-qualified path (the scratch dir from the environment, or
    `$(pwd)/.scratch/x.png`), and `ls`/`Test-Path` the output before trusting it.
75. **A synchronous "do X, then check" reads only the IMMEDIATE (t=0) effects — SCHEDULED effects
    need the clock advanced.** An anim death's debris `OBJECT_MOTION` is scheduled mid-sequence (the
    water tower's at t=2.2 s), so the C24 kill-and-check saw zero debris and wrongly recorded it
    "stubbed" — it fired fine in real gameplay, where `_Process` ticks the clock to 2.2 s. To observe
    a scheduled effect headlessly, call `runtime.Advance(dt)` past the schedule. **But advancing an
    out-of-tree world spams `!is_inside_tree` (global-transform reads return identity):** add the
    subtree to the tree first and set `ManualAdvance` so `_Process` doesn't also drive it. Measure
    immediate state (swap, colliders) BEFORE the tick, scheduled state (debris) after.
76. **A runtime `PUFFER_STATE` renders NOTHING in the flight build — the puffer factory is torn
    down after the world build.** `WorldSession` nulls `AnimRuntime.PufferFactory` (and disposes the
    `TextureArchive`) once the bootstrap finishes unless `KeepArchivesOpen` is set, and that flag is
    **lab-only** (`--anim-lab`). So any effect first reached at runtime in flight — a weapon-impact
    `gunhit` smoke, a destruction fireball puff — hits `Count("PufferState(after build)")` and draws
    nothing, even though the def resolves and its instance starts. The only runtime puffers that
    render in flight are the per-player **crash** runtime's, because `BuildFlightCrashRuntime` builds
    its own live `PufferFactory` over textures it deliberately keeps open. Do not "verify" a
    runtime-played puffer effect by confirming its def started — confirm a `Puffer` was *built*
    (a non-null factory), or you are measuring a no-op. Rendering impact/destruction puffers needs a
    dedicated world-effects runtime on that crash-runtime pattern (D32), not a call into the world runtime.
77. **`CANNON_SPREAD` makes weapon-impact tests non-deterministic — a round hitting a given surface
    is chance, not choice.** Each gun round's direction is jittered by `GD.Randf()` inside the spread
    cone (unseeded), so whether the stream finds a specific patch (a lake, one building) varies
    run-to-run even with a fixed `--hold` dive and `--no-pads`. Don't rely on "I hit water once";
    pick a chapter whose **spawn sits over** the surface you want (measured: C1B/C2B dive → all water,
    C4 → all buildings) so hits are reliable, and assert on the **once-per-name** effect breadcrumb,
    not on a fixed impact count (the impact log itself caps at 8, so a later water hit still logs its
    effect while the surface line is capped out).

## What this project cannot verify itself

These need the user:

- **Audio** — mix levels, falloff, per-pane listeners; ambient world audio has never been
  listened to.
- **Feel** — turn rates, HUD placement, menu repeat, look sensitivity; the TUNE list in
  `backlog.md` exists for this.
- **Anything needing two controllers** — this machine has one pad.
- **Skilled flying** — a full 5-zone stunt run is not blind-scriptable.
- **Live keypresses** — R-restart is verified by construction only.
- **Fidelity against the original** — "nothing looks wrong in our build" is not a side-by-side.

## Known non-deterministic surfaces

If your diff lands here, suspect noise first:

| Surface | Behaviour |
|---|---|
| `--fly` / `--stunt`, any pose | Useless for screenshot diffs — same-build floor 30–84% of pixels; A/B with `--viewer`/`--freecam` + pinned camera |
| `--freecam` default camera | Random spawn per launch — pin with `--spawn=N` or `--campos`/`--lookat` |
| Precipitation (C1C/C2B/C4) | Self-animating from `TIME`; ~5–6% frame difference same-build |
| C3 water flipbook | Baseline flips between two states (~35,250 px, delta ≤3); open water moves 14.4% of pixels with the camera frozen, amplitude ≤12/255 — a real depth flip is delta ~100+ |
| Bootstrap `unresolved` op count | `RandomWeight` dice — 100–107 on one unchanged build |
| Damage-lab fire trails | 413–479 px between runs on a single tree |
| Liveries in flight | Randomised per player per load — pin with `--paint-seed=N` |
| Any world view | Not frame-deterministic — measure the same-build floor first |

## The standing checklist

Before calling a change verified:

- [ ] Build succeeds, and the **baseline** build succeeded too
- [ ] Camera and spawn pinned; noise floor measured same-build-vs-same-build
- [ ] The instrument has been shown capable of reporting failure
- [ ] Nothing is occluding, fogging, deactivating or rounding away the effect
- [ ] "Pre-existing" claims reproduced on the unchanged build
- [ ] 8-chapter regression: zero errors, and counts explained rather than just unchanged
- [ ] Static plane viewer byte-identical (md5) if the change should not touch aircraft
- [ ] Full mode battery: fly / stunt / viewer / damage / 4P race / menu
- [ ] What remains unverifiable is stated plainly, not implied to be done
