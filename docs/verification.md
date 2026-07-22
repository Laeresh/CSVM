# Verifying a change in this project

**Read this before measuring anything.** This project verifies almost everything through
headless screenshots, pixel diffs, log counts and `--perf` numbers, and every one of those
instruments has lied at least once — usually by returning exactly the answer the hypothesis
predicted. The cases below are all real, all from this repo, and each cost time.

`docs/HISTORY.md` holds the narratives; this page holds the transferable rule. Module-specific
gotchas live in that module's `docs/architecture.md` bullet (`AnimRuntime`, `TextureCycler`,
`GaugeCluster`, `MeshLab`, `Pads` all carry one) — read those too before touching those files.

## The rules that have each paid more than once

1. **Check the plan's premise against the data before writing code.** Three scheduled items were
   killed or rewritten this way: `OBJECT_ADD_CHILD` (all three of its stated premises false — the
   fire templates it was supposed to unblock are *never* its children, 0 of 1,152), the
   `hk_zep` "spawn roster" (neither candidate file could gate anything; the real mechanism was the
   interp boot script), and `OBJECT_MOTION` (the event turned out to be two unrelated ops, only
   one reachable). Surveying first is cheaper than implementing a no-op.
2. **Measure the noise floor before believing a difference.** World shots are *not*
   frame-deterministic, so an md5 comparison is meaningless — you must run the same build twice
   first. Floors actually measured here range from 1 px to 13,346 px depending on the view.
3. **Re-measure the baseline before believing a regression.** An 8.6 ms "regression" was partly
   machine drift across a long session.
4. **A metric going to zero is not the outcome being right.** Raising the depth-bias constants
   collapses C5's z-fight flicker from 8.42% to 0.01% *while biasing the wrong surface to the
   front*.
5. **A clean compile, a passing test, or an unchanged number is not evidence** unless you have
   seen it able to fail. See §5.

## 1. Before you trust a screenshot diff

- **The default `--freecam` camera is not deterministic.** The spawn is a random pick per launch,
  so two runs frame different views. This manufactured a "54% of the frame changed" result that
  meant nothing. **Always pass `--spawn=N` or explicit `--campos`/`--lookat`.**
- **A dead-still camera renders bit-identical frames**, so nothing z-fights across a burst.
  Z-fighting is undetectable without deliberate sub-pixel dither (`--jitter`). The 0.15° default
  is far too coarse for a ground-level camera — it changed 92% of pixels and told us nothing;
  0.006° was the useful value.
- **Some real effects are below screenshot resolution.** The water flipbook's frames differ by
  ~2/255 on a dark sea: a with/without burst comes out identical to 0.01%, which reads as "not
  working" and is not. Verify these from the `--debug-anim` frame-index log instead.
- **Something may be occluding or washing out the thing you are testing.** `OBJECT_OPACITY_STATE`
  was wrongly declared to have no visible effect: forcing opacity to 0 changed 8 px and *hiding
  the nodes outright* changed 5 px. Both true, both meaningless — the opaque `cloudlayer`
  `CloudDeck` occludes those sprites from below and fog washes them to exactly `FOG_COLOR`. With
  the deck hidden and fog off, the same change moves 74,129 px.
- **One camera angle is not a test.** The lighthouse-flare bug was invisible from the south,
  where the authored spot already faced the camera.
- **Compare pixel values, not an upscaled crop.** The gauge face textures contain dark *unlit*
  copies of the STALL / LOW ALT windows that read as lit when enlarged (~58,0,0 unlit vs 180+,0,0
  lit). This one bit twice.
- **Encoded-byte comparison of images reports false differences.** All 881 C1 texture PNGs differ
  byte-wise (encoder only) while being pixel-identical.

## 2. Before you trust a number

- **`--perf`'s `script` figure reads ~2.2× the measured frame time** (it reports Godot's
  `TIME_PROCESS` monitor). Trust `frame`/`fps` for absolutes; use `script` only as an A/B ratio.
- **Everything sitting at the 60 fps vsync cap means those numbers are floors, not ceilings** —
  the cap hides both remaining headroom and added cost.
- **Bootstrap op counts vary run to run on an unchanged build**, because `RandomWeight`
  conditions roll dice: C1/M05's `unresolved` count measured 100–107. An apparent +2 regression
  was noise.
- **Do not assume a cost is on the GPU.** Measured viewport GPU time was 0.27 ms while the frame
  was ~133 ms of C#. A bounding-sphere early-out "was solving nothing and was removed" — and the
  identical trap caught the `LIGHT_STATE` cost later.
- **Differences smaller than the instrument are not differences.** A paint-rework load cost of
  5537 vs 5606 ms over 3-run averages is noise, not a win.
- **Watch for cold caches.** An alarming first-run 8.1 s was the OS file cache on 630
  freshly-written JSON files.

## 3. Before you trust "nothing changed"

An unchanged output has at least four innocent explanations, and they look identical:

- **The feature ran perfectly somewhere invisible.** A correctly-animated node inside a
  deactivated subtree moves exactly right and renders nothing — which is why `--debug-anim`
  reports visible-in-tree per node. This is the normal case, not the exception: C4's 25 new
  propeller spins are all on deactivated zeppelins, 36 of C1's 38 sound emitters are built and
  stopped, and C5 plays 0 of its 108. Counts can look like a win with nothing on screen.
- **The change is inert by construction in that chapter.** 7 of 8 chapters were byte-identical
  after the point-light work *because C1 is the only chapter with `OnStartup` light defs* — worth
  stating, because "byte-identical" otherwise reads as "didn't work".
- **The metric legitimately must not move.** The anim dedupe fix correctly left the op count at
  3531 throughout; expecting movement would have read as failure.
- **Two different failures look the same from outside.** "The key handler doesn't fire" and "the
  node doesn't exist" are indistinguishable until you inject input and log what each candidate
  receives. Reading the two handlers side by side would never have separated them.

Conversely, a *visible* change is not proof the feature works: `DegToRad` made every animation
rotation ~57× too small and nothing visibly turned, while the system ran the whole time.

## 4. Before you trust your instrument

The measuring tool has been the bug more often than is comfortable:

- **The wrong triangulation manufactured exactly the evidence the hypothesis predicted** — a
  Newell normal over a `triangle_strip`'s raw index list is meaningless and reported a false
  7–14% inversion rate on aircraft normals.
- **A sampling window that ignores the structure it samples will confidently describe the wrong
  bytes** — a fixed 90,000-byte window ran past a `.BM`'s base plane into the mask planes and
  "proved" that shading maps ship pre-painted.
- **A tool limitation gets recorded as a data variant.** "24 of 48 SI-script parse failures"
  never existed; the walker lacked the header-declared counts.
- **A diagnostic can perturb what it measures.** MeshLab's hardcoded default light direction
  re-aimed the sun and broke the byte-identical viewer screenshot; its override materials must
  replicate SceneBuilder's vertex stage *verbatim* or the coplanar decals z-fight the moment you
  toggle anything and every comparison is worthless.
- **Never silently skip a case in a diagnostic** — that is drawing a conclusion from half an A/B.
  Recover and log.
- **A log line can be structurally unable to show the thing.** `--debug-anim`'s motion line
  printed position only, and a spin turns in place, so it read identically every second whether
  or not it was running.
- **Node names in the tool are not the names in the game files.** Godot sanitises `.`→`_` and
  auto-renames duplicate siblings; use the `cs_name` meta.
- **Grep the *full* stderr.** A byte-identical run that grepped only for the screenshot line
  would have hidden a whole class of shader error.
- **A pad with stick drift silently steers the free camera** and turns a "deterministic" scripted
  screenshot into one that isn't. SDL's hints do not stop Godot enumerating it — pass `--no-pads`.

## 5. Before you trust the baseline

- **`git stash` without `-u` leaves new files in place**, so the "baseline" build can fail and
  Godot happily runs the new DLL. Check the build actually succeeded.
- **A regression test that has never been seen to fail proves nothing.** Reverting one guard
  still passed, because a second guard was silently doing the work.
- **A passing screenshot that would pass identically with a no-op is not a test.** The livery lab
  applied its scheme on top of an already-painted plane; it looked right, and would have looked
  exactly as right if `Repaint` did nothing. The viewer now builds bare so the lab owns the
  livery end to end.
- **Verify each branch independently, not just combined.** Splitting the mech3ax work into two
  PR branches exposed a panicking `metadata-gen`: the anim branch's codegen registrations were
  sitting in the gamez commit.
- **Confirm "pre-existing" by actually reproducing it on the unchanged build**, not by argument.
  Done for the C3 water-flipbook flip (35,250 px), the degenerate zeppelin transforms, the
  font-cache warnings, and the origin-camera quadrant clipping.
- **A clean compile is not evidence.** An `instance_index(N)` hint on a shader uniform compiles
  fine and then silently no-ops the uniform entirely.

## 6. What this project cannot verify itself

These need the user, and saying so is the correct outcome — not a gap to paper over:

- **Audio.** Nothing about mix level, falloff curve or whether each splitscreen pane acts as a
  listener is screenshot-verifiable. Ambient world audio has still never been *listened* to.
- **Feel.** Flight-model turn rates, HUD placement, menu repeat timing, look sensitivity — the
  `TUNE` list in CLAUDE.md exists for exactly this.
- **Anything needing two controllers.** The menu → multi-player launch path rests on construction
  plus the CLI-equivalent path; this machine has one pad.
- **Skilled flying.** A full 5-zone stunt run is not blind-scriptable; the user closed it.
- **Live keypresses.** R-restart is verified by construction.
- **Fidelity against the original.** "Nothing looks wrong in our build" is not a side-by-side.
  Grade the evidence honestly: the user not knowing C2 *had* a Spruce Goose is mild positive
  evidence, not mere absence of complaint.

## 7. Known non-deterministic surfaces

Quick reference — if your diff lands here, suspect noise first:

| Surface | Behaviour |
|---|---|
| `--freecam` default camera | Random spawn pick per launch — pin with `--spawn=N` or `--campos`/`--lookat` |
| Precipitation (C1C/C2B/C4) | Self-animating from `TIME`; ~5–6% frame difference, same magnitude same-build-vs-same-build |
| C3 water flipbook | Baseline flips between two states run to run (~35,250 px at max delta 3) |
| Bootstrap `unresolved` op count | Varies with `RandomWeight` dice (C1/M05: 100–107) |
| Damage-lab fire trails | 413–479 px between runs on a single tree |
| Liveries in flight | `--fly`/`--stunt` randomise per player per load — pin with `--paint-seed=N` |
| Any world view | Not frame-deterministic; measure the floor before comparing |

## 8. The standing checklist

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
