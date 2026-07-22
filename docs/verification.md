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
6. **An arithmetic coincidence is not a mechanism — find the value, not just the magnitude.**
   Item 9 of the polish-3 plan traced C1/M04's ~1e29 zeppelin transforms to `ScriptPlayback`
   compounding scale, and the numbers fit beautifully: `lkgasbag02` carries
   `scale.base.x = 1.52`, 1.52^150 ≈ 1e27, ~2.5 s at 60 fps, and the gasbags sit on exactly
   the reported node chain. Every step was true and the conclusion was still wrong. The real
   cause was an unread `spline_interp` flag letting uninitialised spline memory be evaluated,
   and what proved it was not a magnitude but an **exact value**: the offending script's scale
   constant term is `4.6109513952913965e27` and the blown-up node's world X was
   `4610951000000000000000000000` — bit-identical, so no compounding was involved at all.
   **A hypothesis that predicts the right order of magnitude has barely been tested; one that
   predicts the exact bits has.** Note also that the plan's static analysis named the right
   *files* (all 12 of its scripts are among the 15 the real bug affects) for the wrong reason —
   agreement on where is not agreement on why. See rule 7 below for the same trap in space
   rather than in arithmetic.
7. **"Hide one side and the artifact goes away" does not prove which two surfaces were
   fighting — and it does not prove they were two surfaces at all.** Hiding C5's coarse
   ground sheets collapsed the repro pose's flicker from 35.77% to 0.19%, which reads as
   "confirmed: the sheets fight the partition ground". Both halves of that reading were
   wrong. Hiding *only* `g4683` gave the identical 0.19%, and the flicker was that single
   mesh's **own** polygons — five same-material coplanar pairs sharing one surface and
   therefore one depth bias. The control was also confounded: removing the sheet removed the
   only textured surface in the near field, so it deleted the grazing-angle mipmap/aniso
   resampling noise along with the depth flips, and *most of that 35.77% was never a depth
   fight at all* (per-polygon ordering, which changes nothing but depth, moved it only to
   21.92%). **Isolate down to the single node before naming a culprit, and prefer a control
   that changes only the suspected mechanism over one that removes the geometry.**
8. **A measurement that locates the geometry still does not tell you which surface is at fault.**
   C3's "trees standing in the water" was measured precisely — 86 of the 102 `cliff1_sandtrans`
   polygons sit at exactly Y = 0.0, coplanar with the sea plane — and that correct number
   licensed a wrong fix: drop the submerged palms with a `y > waterLevel` guard. But the palms
   are in the original and are *supposed* to be there; the water was winning the depth fight
   against the beach, and the fix would have deleted correct content while leaving the real bug
   untouched (caught by the user, 2026-07-22). The measurement answered "where", and was then
   read as if it had answered "what is wrong". **Before fixing a coplanar-surface bug, establish
   which surface the original draws on top — that is a separate question from where they
   overlap.** Compare rule 4, which is the same trap reached from the other direction.

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
- **One scripted collision pose is not a test either — sweep the parameter.** The airframe
  collision boxes deliberately *overlap*, so only impact points inside the disputed region
  discriminate between two classifications; everywhere else both labels give the same answer.
  A/B'ing the item-10 tail/wing relabel over five spawn altitudes changed the logged part at
  **exactly one** of them (`graze (tail→tail)` → `graze (wing→rightwing)`, at identical vn,
  damage and HP — which is also what proves the physics did not move). Any of the other four
  poses on its own would have read as "the fix does nothing", or, run before the fix, as "there
  is no bug". Sweep a range and require *some* pose to flip.
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
- **A guard against non-finite values does not guard against garbage.** `SiCubic.Eval` degrades a
  NaN/∞ result to the constant term — and sailed straight past a *finite* 4.6e27 read out of
  uninitialised spline memory, which then destroyed a whole subtree's world poses. When data can
  be junk, "is it a number" is the weakest possible check; prefer a flag in the format that says
  whether the bytes are meaningful at all (here `spline_interp`, parsed and then read by nothing).
- **A probe that crashes has told you something.** The first attempt at instrumenting this bug
  threw `ArithmeticException` out of `Basis.get_Scale()` — and that stack trace was worth more
  than the log line it failed to print: it proved the corruption was already present *inside
  `ScriptPlayback`'s constructor during bootstrap*, which ruled out the per-frame accumulation the
  whole hypothesis rested on. Read the failure before "fixing" the probe.
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
- **A truncated diagnostic list can hide exactly the entity under test — and hide it *because*
  the fix worked.** `--debug-anim` prints the first 12 live motions. When the `Loop{0}` fix made
  C1's traffic loop, each restart re-registered that car's motion at the *end* of `_motions`, so
  the looping cars fell off the printed list and the fixed build looked identical to the broken
  one ("last seen at t=14" in both). **Before trusting a per-entity log, check whether the entity
  is inside the print window, and whether your change moves it out of it.** Raise the cap for the
  measurement and revert it afterwards.
- **Node names in the tool are not the names in the game files.** Godot sanitises `.`→`_` and
  auto-renames duplicate siblings; use the `cs_name` meta.
- **Grep the *full* stderr.** A byte-identical run that grepped only for the screenshot line
  would have hidden a whole class of shader error.
- **A pad with stick drift silently steers the free camera** and turns a "deterministic" scripted
  screenshot into one that isn't. SDL's hints do not stop Godot enumerating it — pass `--no-pads`.
- **`dotnet build` cannot fail on script encoding corruption, so it is not evidence about it.**
  A bulk rewrite mangled every UTF-8 multi-byte sequence in 3 source files; `dotnet build` returned
  0 warnings / 0 errors because the damage sat in comments, which the C# compiler happily compiles
  as mojibake. Godot's Mono loader validates UTF-8 *strictly* and refused the same files
  (`Script contains invalid unicode (UTF-8), so it was not loaded`), surfacing only as
  `Main.tscn: Parse Error: [ext_resource] referenced non-existent resource`. **After any bulk text
  rewrite, run the game — a clean build is a green light that means nothing here.**
- **`[IO.File]::ReadAllText($path, $encoding)` silently ignores the encoding you passed** when the
  file starts with a BOM: it auto-detects and decodes as UTF-8 instead. The "read as Latin-1,
  replace ASCII, write as Latin-1" trick is therefore **not** byte-preserving — for BOM-prefixed
  files it decodes real Unicode and then collapses each multi-byte char to one byte (`°` C2 B0 → B0,
  `×` C3 97 → D7) and drops the BOM. For a bulk rewrite, read the bytes, detect the BOM yourself,
  decode with `UTF8Encoding($false, $true)` so invalid input *throws* instead of being replaced with
  U+FFFD, and write back with the BOM state you found.

## 5. Before you trust the baseline

- **`git stash` without `-u` leaves new files in place**, so the "baseline" build can fail and
  Godot happily runs the new DLL. Check the build actually succeeded.
- **Do not use `git stash` to produce a baseline when anything else may be touching the tree.**
  The stash stack is **repo-global, shared across every worktree**, and `git stash push <path>`
  reverts a file another agent may be editing at the same moment. Doing this in a supposedly
  isolated agent worktree (2026-07-22) silently lost the change under test *and* built the
  "baseline" from a concurrent agent's in-flight edits to the same two files — which manufactured
  a confident C4 result in a chapter the change provably cannot touch. **Flip the one line under
  test, build, run, flip back**; and before comparing, confirm what the baseline build actually
  contained (`git diff --stat` immediately before the build, not after the run).
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
