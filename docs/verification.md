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
   therefore one depth bias. **⚠ That replacement diagnosis was ALSO wrong, and so is the
   "mostly resampling" claim this rule used to end on — see rules 9 and 10.** `g4683` has no
   self-overlapping polygons (the pairs abut, they do not overlap), and the pose's flicker is
   nine *different* World-child nodes stacking coplanar ground. What survives from this rule is
   its method, and it is the part worth keeping: **isolate down to the single node before naming
   a culprit, and prefer a control that changes only the suspected mechanism over one that
   removes the geometry.** Applied honestly, that method is also what caught the second wrong
   answer — hiding a node tells you a node *participates*, never what it participates *with*.
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
9. **A bounding-box overlap is not an overlap.** The C5 ground z-fight has now been diagnosed
   three times and got a different wrong answer each time; the third one — `g4683` "carries 8
   pairs of its own polygons exactly coplanar at y = 5, overlapping by up to 768 × 512 units" —
   rested entirely on an **AABB** intersection test. Clipping the real outlines
   (Sutherland-Hodgman, true polygon ∩ polygon area) gives **zero** overlap for every pair in
   that mesh: they are tiled ground quads that *share an edge exactly* and lie on opposite sides
   of it. The same substitution inflated an install-wide survey from **0.6–1.6% of polygons to
   9–26%**, a 15× error that would have justified a far more invasive fix than the data supports.
   Two triangles tiling one rectangle have identical bounding boxes and no shared area at all.
   **When the hypothesis is "these two surfaces overlap", compute the overlap, not the boxes** —
   and expect game terrain to be mostly *abutting* coplanar tiles, which an AABB test reports as
   a conflict everywhere.

   ⚠ **This substitution has now produced two different wrong answers on the same bug, the second
   one written by people who had already read this rule** (2026-07-22). The claim "nine coplanar
   World-child nodes stack at y = 5 in the C5 repro footprint" — carried into three documents as
   established structural evidence — is the same error one level up: the nine **tile**, with
   0.00 m² of true polygon ∩ polygon area between every cross-node pair, confirmed independently
   by rasterisation (0 of 114,095 cells doubly covered). Only `g4683`'s own AABB spans several
   tiles, which is where the "stack" reading came from. **Knowing this rule is not the same as
   applying it to inherited evidence.** When a premise arrives pre-established from an earlier
   session, check what it was computed *from* before building on it — a claim's age is not
   evidence, and neither is the number of documents repeating it.
10. **"The rest is instrument noise" is a claim that needs its own control.** Rule 7 recorded that
    most of the C5 repro pose's 35.77% flip rate was "grazing-angle mipmap/aniso resampling, not
    depth flips". It was not. A control that changes **only** depth — a per-polygon depth ramp,
    which leaves every projected position identical by construction — took the same pose to
    **0.37%**. So ≤0.4% was resampling and ~35.6% was genuinely depth flipping; the earlier
    "21.92%, so the rest must be noise" reading was a *step-size* artifact of a ramp too small to
    win the depth test, mistaken for a floor. **A residual you have not driven to zero is not
    evidence of a floor.** Drive it with a control that isolates the mechanism; if you cannot, say
    the floor is unmeasured rather than inferring it from wherever your change happened to stop.
    Note also what that successful control *looked like*: 0.37% flicker, achieved by floating the
    coarse sheet in front of the detailed city — rule 4 again. The number and the picture
    disagreed, and the picture was right.
11. **Know your instrument's resolution before you tune a constant to fit under it.** This
    renderer's depth bias is a fraction of view distance, and the fraction below which two
    coplanar surfaces stop separating was never measured until 2026-07-22: it is **≈1e-6** at the
    C5 repro view (bracketed by ramp steps of 2e-7 → 33.20% and 2e-6 → 0.37%). `NodeOrderBias` is
    **5e-8**, i.e. twenty times below its own resolution — so the cross-node tie-break the
    `SceneBuilder` bullet describes is *inoperative* for any pair of nodes closer than ~40 indices,
    and has been since it was written. Three separate z-fight reports are downstream of that one
    unmeasured number. **A tuning constant whose effect has never been bracketed is a guess, and
    a hierarchy of them (priority ≫ surface rank ≫ node order) can be silently inverted or
    silently truncated at any level.**

12. **A quantity read out of an 8-bit capture is quantised, and the error scales with how
    little of the range you used.** Diagnosing the hairline seams (2026-07-22) needed the
    screen size of one texel. The obvious probe — render `fract(UV)` and take a finite
    difference — gave **46 px/texel**, which made the 3-px hairline sub-texel and therefore
    "impossible for any texture filter to produce", killing the correct hypothesis. The true
    value is **~4 px/texel**: the finite difference had spanned about *1.4 quantisation steps*
    of an 8-bit channel, so it was ~all rounding. The instrument that worked encodes the
    quantity as a **period instead of a level** — a ramp repeating once per texel, where the
    answer is the stripe spacing and 8-bit precision is irrelevant. **Before believing a
    number derived from pixel values, check how many of the 256 levels the measurement
    actually moved across; if it is single digits, change the encoding rather than the
    threshold.** Note this is the mirror of rule 6: there, a hypothesis survived because it
    predicted the right magnitude; here, a correct hypothesis was nearly discarded because a
    bad instrument gave the wrong one.
13. **"Not on a polygon boundary" and "nothing is behind it" each need their own control, and
    the obvious probe has a hole.** The same diagnosis replaced textured surfaces with flat
    colours to prove the seam was in the sampled texture, and the seam count went to zero —
    but a crack showing *another surface that happens to use the same texture* would also have
    scored zero, because both sides render the same flat colour. Closing that took a second
    probe: unique colour **per surface**, with the material cache bypassed so two meshes
    sharing one texture cannot collapse into one ID. **When an ID map is your evidence that
    nothing else is there, check what the map is keyed on — anything it cannot distinguish is
    exactly what can hide in it.**

14. **Before scheduling work from a bug report, check whether a fix already landed — compare the
    report's date against the commit log, not against your memory of the file.** The C3 coast
    z-fight was reported and *fixed the same day*: `6c592c2` (per-mission entity setup) took it
    from 4.87% to 0.09% flicker, a 54× reduction, as a side effect of running the mission's own
    `NodeSetActive`/`DeleteTree` and thereby not drawing duplicate coplanar entities. But the
    backlog entry still carried "user-confirmed as real z-fighting", so **two** later agent
    sessions (polish-3 items 3 and 11) spent their budget chasing an already-fixed symptom — and
    item 11's honest "this pose barely flickers, 0.41%" reading was *the fix showing through*, mis-
    read as a mis-aimed camera. A backlog entry is a claim with a timestamp, not a live query.
    **When a report and a fix share a date, `git log` the interval before writing any code**; and
    when you fix something incidentally, go back and close the entry it fixed (this project's rule
    that a closed entry leaves `backlog.md` exists precisely to keep the list queryable).
15. **A symptom at a hand-picked camera pose can be exact geometry rather than a defect.** "The
    world renders into only the upper-left quadrant" — two hard edges meeting at the exact centre
    of the viewport, which reads unmistakably as clipping or a stray `SubViewport` — was the
    correct picture: C1's world `area` is x,z ∈ [-12288, 0], so the world **origin is the map's
    corner**, and a camera at `--campos=0,30,420 --lookat=0,0,0` derives yaw 0, putting the plane
    x=0 on the vertical centre line and the line (t,0,0) on the horizontal one. Nothing was
    clipped. **Before debugging a suspiciously axis-aligned artifact, check whether the camera
    pose is special** — an artifact that lands on exact half-viewport boundaries is usually
    projection, not rendering.
16. **A metric built while looking at the broken state may not survive the state being fixed.**
    Verifying the bowl-sign scheduler fix (2026-07-22) started with the obvious classifier: the
    sign is red, so score each frame "is this crop reddish?". Against the *broken* build that is a
    perfectly good instrument — the sign was either lit-red or completely absent, and it measured
    38.0% absent exactly. Then the fix landed and it reported **72% blank: twice as bad**. The
    number was precise, reproducible, and meaningless. The fix had restored the `des_off` variant,
    which had never rendered in a single frame before, and an **unlit grey panel is not reddish** —
    so the classifier scored "sign correctly showing its off state" identically to "no sign at
    all". What caught it was *looking at four frames*; what fixed it was classifying on luminance
    **standard deviation** (empty sky sd≈18 vs any panel sd≈48–53) before asking about brightness.
    **Two rules fall out.** First: a classifier that only ever saw two of the three possible states
    cannot be trusted to name the third — enumerate the states the *fixed* code can produce, not
    the ones the bug produces. Second, and more general: **when a fix makes a metric worse, look at
    the artifact before believing the metric.** Compare rule 4 (a metric going to zero is not the
    outcome being right) — this is its mirror image, and the same discipline answers both.

17. **A bounding box stored in a file is in the node's own frame; the one your predicate needs is
    usually in the world's.** Diagnosing the origin-parked entities (polish-4 item 4, 2026-07-22)
    needed "does this node's geometry wrap the world origin". Read off the gamez `child_bbox`, that
    query returned **464** world-build roots across 8 chapters, nearly all terrain — because
    `child_bbox` is *local*, so a placed tile's box is centred on its own origin and contains
    (0,0,0) by construction. Taken from the built subtree in world space the same query returns
    **122**, every one a vehicle. The two differ by a factor of four and by their entire meaning.
    **Before testing a position predicate against stored bounds, check which frame the file stores
    them in** — rule 9's lesson (compute the thing, not a proxy for it) in a different coordinate
    system.

18. **"After bootstrap" is not "after everything that places things".** The same item's first
    implementation swept once, immediately after the animation bootstrap, reasoning that every
    placement mechanism had by then run. Two had not: `OBJECT_MOTION_FROM_TO` registers a motion
    that moves the node over the following *seconds*, and an OnCall definition can start one at any
    time. The sweep therefore switched off 35 C2/IA1 entities — four yachts, three sailboats, ten
    studebakers, the rocket, and C1's train cars — that were merely *not yet* where they were
    going, and **it looked completely correct at the instant it ran**. When a check asks "did
    anything place this", make sure the answer cannot still be "not yet": prefer a reversible
    action plus a recheck over a one-shot verdict, and note that deferring by a fixed delay only
    moves the constant rather than removing it.

19. **A plan that traces the right mechanism can still name the wrong symptom — and the symptom is
    what you will measure.** Polish-4 item 2's mechanism (`FromToMotion` resetting to rest) was
    exactly right, and its demonstration case was not: the plan's "17-second straight, the longest
    and most visible leg" holds 0°, which *equals* `police_car`'s authored rest, so it is the one
    leg in that sequence the bug cannot affect. Measuring where the plan pointed would have
    produced a clean "no change" and read as a **disproof of a real bug**. **Before measuring where
    a plan points, check that the pointed-at case can actually discriminate** — here one line of
    the shipped `nodes.json` (`police_car` rotate `{0,0,0}`) settled it in advance, before any code
    was written.

20. **Confirm the symptom is a DEFECT before you spend anything diagnosing it — "is this in the
    original?" is the cheapest question in this project and it is routinely skipped.** The C1B
    z-fighting pose consumed **three failed diagnoses and a full data-analysis session** before
    the user said, in one sentence, that the original z-fights there too (2026-07-22). It was
    never a bug. Every measurement taken against it was sound; the entire enterprise was
    misdirected, and the sharpest evidence was misread in exactly the wrong direction — the
    measured `NodeOrderBias` control taking C1B from 30.95% to 2.35% looked like the most
    promising lead in the file, when it was actually **1.4 percentage points from erasing a
    faithful artifact**. This is a remake: a symptom is only a defect if it *differs from the
    original*, and "it looks wrong to me" is not that comparison. **Before scheduling work from a
    bug report, establish that the original does not do the same thing** — ask the user, or shoot
    the original at the repro pose. Compare rule 4 (a metric going to zero is not the outcome
    being right): here, driving the metric to zero would have been the defect.

    Corollary, and the reason this rule is worth its length: **the more effort already sunk into
    a symptom, the less likely anyone is to re-ask whether it is one.** Item 9's premise was
    re-derived four times; "is this actually wrong?" was asked zero times in four sessions.
    Inherited symptoms need the same audit as inherited evidence (rule 9's note).

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
  the cap hides both remaining headroom and added cost. **`--perf` gained a `physics` term on
  2026-07-22 for exactly this reason**: `frame`/`fps` are pinned at the cap in nearly every run
  here, so they can never show a collision or broadphase change getting cheaper or dearer, while
  `TIME_PHYSICS_PROCESS` moves freely (it reported a steady ~2.4 ms over a C5 rooftop pass and
  ~30–55 ms in the first second after load). When a change lands in a subsystem the frame time
  cannot see, find the monitor that watches that subsystem before concluding anything — and if
  there is none, say the effect is unresolved rather than reading the pinned number as "no
  change". Same caveat as `script`: it is Godot's own monitor, so use it as an A/B ratio.
- **A cost can be entirely in one half of an operation you assumed was uniform.** The clutter
  city-block collision build looked like a bulk-geometry problem — 2.55M triangles transformed
  and merged — and the obvious read was that the vertex arithmetic dominated. Timing the two
  halves separately gave **271 ms transform vs 3,403 ms shape build**: 90% of it was
  `ConcavePolygonShape3D`'s BVH construction, not the loop everyone looks at. Had the fix been
  aimed at the transform loop (SIMD, parallelism, fewer allocations) it would have bought ~7%.
  **Split the timer before choosing what to optimise**, especially when part of the work happens
  inside an engine setter rather than in your own loop.
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

- **A census printed once at startup cannot tell "never requested" from "requested later and
  failed."** The `anim: N ambient sound emitter(s): …` line is emitted inside `Bootstrap`, so it
  is a snapshot, not a running total. C1's police siren *is* dispatched, *is* found, and *does*
  build an emitter — one frame after that line prints. Reading the snapshot as a complete census
  gave a clean, specific, entirely wrong diagnosis ("the emitter is never created, because named
  sequences are never dispatched") that survived a whole investigation because every check
  performed agreed with it. **Ask what window the instrument covers before concluding from an
  absence**, and where a subsystem can fail after its report, make the failure announce itself at
  the point of use rather than inflating a counter nobody prints again.

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
- **A `tri_strip` polygon's index list is not an outline**, and treating it as one manufactures
  geometry that does not exist. Reading each polygon's raw `vertex_indices` as a closed loop gave
  a 16-index strip box a bogus Newell normal and a bogus plane, which "proved" that C5's node 1777
  overlapped buildings **2 km away by 6.8 million m²** (2026-07-22, item 9). Triangulate exactly
  as `SceneBuilder.EmitPolygon` does before computing any planes or areas.
- **A software re-implementation of the render path must clip at the near plane, not cull.** The
  first version of item 9's depth probe *culled* triangles crossing the near plane, which silently
  dropped precisely the large ground quads the camera was standing on and reported "minimum
  relative gap 0.002, nothing is fighting" — a clean, precise, entirely wrong answer of the same
  shape as the three previous diagnoses of that bug. **A probe that reports "no problem found"
  needs the same able-to-fail control as one that reports a fix works** (rule 5): switching the
  bias off took the same probe to 94.80% of frame coincident, which is what proved it could fire.
- **`Assembly.Location` is empty under Godot's Mono loader**, so the obvious build-freshness probe
  (`File.GetLastWriteTime(typeof(X).Assembly.Location)`) throws `ArgumentException: The path is
  empty`. Worse, when it throws from inside `_Process` it silently kills *all* per-frame debug
  logging for the run — which reads exactly like "the change did nothing". Use an explicit literal
  build tag that you flip together with the code under test (2026-07-22, polish-4 item 2).
- **Your own orphaned process is a foreign Godot** (2026-07-22). The rule below warns about
  *another agent's* concurrent Godot; the same thing happens to a single agent working alone. A
  background regression task that outlives its tool call keeps launching Godot and writing to the
  same log paths, and **`Start-Process -Wait` returning exit 0 does not mean the run ended** — the
  console wrapper exits while the real Godot child lives on. This manufactured a "16,576 errors in
  C1B" result for a change that provably cannot touch world building. **Before re-running a
  harness, kill by command line, not by name** (`Get-CimInstance Win32_Process | Where CommandLine
  -like '*<your worktree>*'`) so other agents' instances survive, and confirm zero of yours remain
  before believing the next measurement. See also the `--headless` + `--screenshot` hang in
  `backlog.md`, which is one way to create such an orphan without noticing.
- **Another agent's Godot, running concurrently, corrupts your captures** (2026-07-22). In a
  parallel-worktree session a second Godot was launching continuously from another agent's tree:
  one screenshot in this session's set **never wrote at all** and another came out at **1/7 the
  normal file size**, which read as a code fault in the change under test. `Get-Process
  *Godot*` showed strangers with new PIDs each time. Before believing a broken or oddly-sized
  capture, check for foreign Godot processes and re-run; raise `--frames` so the warm-up survives
  the contention.
- **A restore that preserves mtime makes `dotnet build` a no-op, so you measure the OLD dll.**
  `Copy-Item` keeps the source's timestamp, MSBuild sees nothing newer, and Godot happily runs
  the previous build — producing a frame identical to baseline that reads as "the change does
  nothing" (hit by the item-4 session, 2026-07-22). Whenever you A/B by swapping files, prove the
  new binary is the one running: a log line that only the new code can emit is the cheapest check
  (item 5 used the disappearance of clutter's `", N collision tris"` suffix).
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
- **Restoring a file by copy can make `dotnet build` a silent no-op, so the "fix" build is the
  baseline.** MSBuild decides staleness by timestamp, and `Copy-Item` (like `cp -p`, and unlike
  an editor write) gives the destination the **source's** `LastWriteTime`. Copying a saved-aside
  `WorldBuilder.cs` back over the baseline restored a file *older* than the DLL just built from
  the baseline (13:52:58 vs 13:55:12); `dotnet build` reported success in 0.9 s having compiled
  nothing, and Godot loaded the baseline assembly. The screenshot came out pixel-identical to
  baseline and read exactly as "the change does nothing" — the same signature as a real no-op.
  **Assert the build is fresh from inside the running program, not from the build log:** what
  caught this was a new `GD.Print` line being absent from stderr. Every A/B that flips a file
  needs one such marker, or a `touch` on the restored file. `git checkout HEAD -- <path>` is
  safe for the other direction (it writes a current mtime); `git stash` is banned here for the
  separate reason in the bullet above.
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
- **Window focus is NOT on this list — it is scriptable after all** (corrected 2026-07-22).
  Alt-tabbing in and out was assumed to need a human; it does not. `AttachThreadInput` +
  `SetForegroundWindow` from PowerShell drives real `APPLICATION_FOCUS_IN`/`OUT` notifications at a
  live game, which is how the focus mute was verified. Two catches: a plain `SetForegroundWindow`
  from a background script is **silently no-opped by the Windows foreground lock** (it reports
  success while the foreground never moves, reading as "the notification never fires"), and
  **minimising the window from another process delivers no focus notification at all** — only the
  mouse enter/exit pair. What stays unverifiable is the usual: whether the result is *audible*, and
  whether a held stick feels dead.
- **Fidelity against the original.** "Nothing looks wrong in our build" is not a side-by-side.
  Grade the evidence honestly: the user not knowing C2 *had* a Spruce Goose is mild positive
  evidence, not mere absence of complaint.

## 7. Known non-deterministic surfaces

Quick reference — if your diff lands here, suspect noise first:

| Surface | Behaviour |
|---|---|
| **`--fly` / `--stunt`, any pose** | **Useless for screenshot diffs — same-build floor measured 30–84% of pixels** (2026-07-22). The plane flies, the chase camera follows and the animations advance, so frame N is a different moment every run. Use `--viewer`/`--freecam` with `--campos`/`--lookat` for any A/B; use `--fly` only for counts and log lines |
| `--freecam` default camera | Random spawn pick per launch — pin with `--spawn=N` or `--campos`/`--lookat` |
| Precipitation (C1C/C2B/C4) | Self-animating from `TIME`; ~5–6% frame difference, same magnitude same-build-vs-same-build |
| C3 water flipbook | Baseline flips between two states run to run (~35,250 px at max delta 3). **Over open water it is far larger than that**: measured 2026-07-22 at a waterline view of the C3 east beach, **14.4% of pixels** move with the camera frozen (`--jitter=0`), amplitude ≤12/255. So a jitter burst there reads ~15% and almost none of it is depth. Separate the two with a `--jitter=0` control, and note the amplitude — a real sand↔water depth flip is a delta of ~100+ (sand ≈ 215,190,150 vs water ≈ 45,95,105), not 12 |
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
