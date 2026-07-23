# Milestone 2 / 2.5 polish run 4

**🟢 LIVE — written 2026-07-22.** Ten items selected from `backlog.md` against three criteria the
user set: **feasibility, little or no user input required, and a preference for long-running work.**
Items needing a playtest, two controllers, or a fidelity judgement the data cannot settle were
deliberately excluded — they stay in `backlog.md` under "Owed playtests", "TUNE constants pending
playtest" and "Open fidelity questions".

**Scope: Milestone 2 / 2.5 only.** `docs/PLAN-M3-weapons.md` remains written but unstarted; every
M3-deferred and M4 item in the backlog (gun heat/jam, ammo pickups, the loadout configurator,
turrets, AI armour) is out of scope here, as is anything blocked on weapons or a cutscene player.
**When both plans sit in `docs/`, this one is the active plan** and M3 is queued behind it.

**Every item below was verified still open on 2026-07-22** — against `docs/HISTORY.md` *and* against
the code itself, because a backlog entry is not evidence that the work is undone. Two backlog claims
did not survive that check and are corrected at the bottom of this file.

## Selection: what was chosen, and what was consciously left

Chosen for a mix of **long-running depth** (items 1, 9, 10) and **diagnosed-and-ready** work
(items 2–8). Deliberately *not* scheduled, and why:

| Left in backlog | Why not this run |
|---|---|
| Every "TUNE constants pending playtest" entry | Needs the user in the cockpit — the explicit exclusion criterion |
| Every "Open fidelity question" | Answerable only by testing the original |
| Owed playtests (M2.5 join flow, splitscreen race) | Needs two controllers this machine does not have |
| `zone_id`, partition visibility | Blocked on "which zone does a mission activate", which is in no file |
| Fire templates / `fire1`/`fire2` | Postponed by user decision 2026-07-21; trigger not findable |
| `C3` `cloud1`/`cloud2` magenta | Explicitly left to the user — it trades away a diagnostic signal |
| `SpinMotion` rest-pose re-seed | Needs the original game to adjudicate two equally plausible readings |
| `poleflare` billboard axis | Changes 139,388 C5 sprites with no reference shot to check against |
| SDL `DirectInput` workaround | Blocked on an upstream Godot/SDL release |
| Static collider probe gap (C4 −6 / C5 −11) | Considered and dropped: `.scratch/probe_exempt.py` no longer exists (swept by `CleanScratch`, and `.scratch/` is gitignored), so this now means rewriting the probe — and its own conclusion is that the candidate list is a superset and the safety verdict is unaffected. Low value for the cost. **Stays in backlog with the note that the script must be rewritten.** |

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done — keep this in sync as items land.

1. ☑ **`NextDue` off-by-one — every sequence in the game is mis-scheduled** (the bowl sign is one symptom) **(done 2026-07-22 — an event's `START_TIME` now gates that event, not its successor; control-flow events are gated too, which restores the sign's discarded `Loop {Event 1.2}` pause. Measured face-on: frames with NO panel at all 38.0% → 0.0%, and `des_off` went from rendering in 0.0% of frames to 72%. 8-chapter regression structurally identical, zero errors, C1 traffic still driving. Found and NOT fixed on the way: `wait_for_completion` is decoded on 56,750 `CallAnimation` events and read by nothing — now in `backlog.md`. `docs/HISTORY.md`)**
2. ☑ **`FromToMotion` resets orientation to rest** — C1's cars drive mis-headed **(done 2026-07-22 — an absent channel now HOLDS the last value written instead of resetting to the authored rest pose; pose held as three components (orthonormal rotation, scale, origin) seeded once per event from the live transform, per `ScriptPlayback`. Median heading error vs direction of travel, `mafia` as the 0.0° unaffected control: `black_car1` 90.0°→0.0°, `truck1` 90.0°→1.9°, `car_loop1` 62.1°→0.0°, `car_go_home` 49.4°→0.0°; `suspect`'s 27 s park 0°→ the authored −110°. 8-chapter regression: all 96 count cells identical, zero errors. **The plan's headline symptom was disproven** — the 17 s straight holds 0°, which *equals* `police_car`'s authored rest, so it is the one leg the bug cannot affect; the real damage is the opening 2 s diagonal and the 27 s park. Found and NOT fixed: all 26 `*_delta` channels are dead (bare `{x,y,z}` vs the `{from,to}` `Channel` expects) → `backlog.md`. `docs/HISTORY.md`)**
3. ⤳ ~~C1 IA1: oil tanks / fuel boxes already destroyed at spawn~~ **MOVED BACK TO `backlog.md` 2026-07-22, under "Open fidelity questions".** The premise inverted twice and then failed to close: our engine is **correct** per the shipped data (intact tanks, every run), the original's only route to a destroyed tank is **weapon damage via the fuel trucks**, and the user has confirmed **those trucks exist only in C1/M02**. So the reported IA1 sighting is most likely M02. **Blocked on user testing**, with the full traced mechanism and the three surviving explanations recorded in the backlog entry. **Nine items remain.**
4. ☑ **C5 IA1: the sunk zeppelin** — `piratezep` at the world origin **(done 2026-07-22 — the mechanism was right and MUCH too narrow. It is not a zeppelin bug and C5 is not the only chapter: `support\<ch>\load.gw` loads every vehicle with a bare `LoadGameGen` + `AddChild` and no placement, so all 122 vehicle roots install-wide start on the world origin — which is the map's *corner*, every chapter's `area` being x,z ∈ [−N, 0]. Missions then place or hide them; retail data misses some, and **C1C's `ia1.gw` misses `piratezep` too**, which the plan's list omits. Fix (b), keyed on the mechanism not on zeppelins: `WorldBuilder.HideUnplacedEntities` after `AnimRuntime.Bind`, plus `RestorePlacedEntities` polled 1 Hz. Switched off: C1C 3, C2 18, C5 2; **nothing correctly placed disappeared** in any chapter, and C3's `zepbridge`/C4+C5's `zepdock` never enter the candidate set. Two traps recorded in `docs/verification.md` as rules 16 and 17. Found and NOT fixed: the gamez node `active` flag is never read → `backlog.md`. `docs/HISTORY.md`)**
5. ☑ **C2 stunt: the Seaplane Hangar objective sits at (0,0,0)** **(done 2026-07-22 — `StuntMission.GeometryAnchor` anchors a geometry-node zone on the aperture between its door leaves, (−5770.4, 23.5, −5623.9), and returns null for anything that draws nothing, so it provably cannot fire for the 53 ordinary `dzN` markers (all measured childless `model_index -1`). No parser change was needed — the plan predicted `child_bbox` parsing; mesh vertices gave the same answer. Corroborated independently by `dzpath1`'s gate polygon, 1.9 m away. Measured: exactly one position line changed across all 6 stunt chapters; flown through and scored; control run with the fallback off scores nothing. One plan detail was wrong and harmless — `sghangar`'s parent is −1, not `world1`. Decoded on the way: `dzpathN` is gate geometry, not a route ribbon → `backlog.md`. `docs/HISTORY.md`)**
6. ☑ **Knife-edge: the nose should drop, not just the path** (+ delete the dead soft-tree branch) **(done 2026-07-23, and independently re-verified after the first agent's own verification was judged untrustworthy and its work backed out. Measured: nose −0°→**−4°**, level cruise byte-identical 25/25 lines, determinism proven first by two identical baseline runs. **Two things the plan did not anticipate, both quantified:** the sag carries into the path **1:1** (settled −6°→−10°, sink +64%), so "leave the path unchanged" is unachievable by construction — the constant was sized so the settled path lands on the documented −10°; and the term fires on **steep pitch at zero bank** too (wings-level zoom apex +81°→+70°), since `knife` grows with pure pitch. User accepted both and will tune. Constants `KnifeNoseSag`/`KnifeNoseRate` → TUNE list. **The rider's commit message was FALSE and was rewritten before landing:** it claimed no collider ever carried the name `clutter_col` and that the user's 7-tree forest plow was flying through never-solid sprites — `git grep` at `a795548~1` shows the name was live 2026-07-17→2026-07-22, so the plow was the branch working as designed. Deletion itself verified byte-identical on two collision paths, with a binary DLL marker closing the stale-build loophole. `docs/HISTORY.md`)**
7. ☑ **Mute on focus loss** (+ the pad-read-on-focus question, settled) **(done 2026-07-22 — `PlaneViewer._Notification`, the project's first, mutes the `AudioServer` master bus on `NOTIFICATION_APPLICATION_FOCUS_OUT` and unmutes on `_IN`. The engine-loop regression the item warned about cannot occur: the bus mute never touches `FlightAudio`, measured `volDb=-13.98 pitch=0.778 ramp=1.000` identically before/during/after two focus cycles, with the same probe shown able to print `-60.00` at startup. The pad half landed as a SEPARATE, user-vetoable commit (`78acbbc`). Found and corrected on the way: the plan's proposed choke point `Pads.Connected()` is the wrong one — it misses the flight path (`For(bound)` never consults it, and every splitscreen/menu player has a binding) and would break the launchscreen (`SyncDevices` reads the roster to drop *disconnected* players; `AssignPads` reads it at session build). Gate went on `Pads.For` instead, `Connected()` left as the ungated roster. Also found: `--headless` + `--screenshot` never terminates → `backlog.md`. `docs/HISTORY.md`)**
8. ◐ **The full `player_plane_destruct` crash choreography** — surface variants, sparks, debris arcs **(FIRST SLICE landed 2026-07-23 — the ground/dirt variant's pure-puffer effects: 2 `small_yellow_sparks` bursts, the `large_black_smokeball`, and the delayed 3-fireball cluster, surface-selected in `FlightController.Crash` and anchored at the plane centre, atop the pre-existing primary fireball + wreck fire. New `CrashChoreography.cs` + two `Puffer.Create` overrides (`blend`, `softParticles`), both no-ops at their defaults. Three bugs found and fixed on the way, all the same "instrument lies" class: the blend-override param was initially dead (smoke stayed additive = invisible); the anchor was the impact point not the `healthy` plane-centre the data uses; and the shader soft-particle fade zeroed the fresh smoke's alpha against the ground — the "smokeball works" call was first made by mistaking a fading fireball for smoke, corrected by isolating the emitter. 8-chapter `--fly` build: choreography builds, zero errors. `docs/HISTORY.md`. **SLICE 2 landed 2026-07-23 — the rest of the dirt variant: the five burning debris arcs (`call_crash_trails`) and the earth-impact boom (`snd_exp_ground_a`).** `FromAnimEvent` now reads the `interval_type: "Distance"` case (⚠ its flag shape is inverted); each `spurtpufferN` is a DISTANCE trail riding a *reconstructed* ballistic `fly_trailN` anchor (the node is never built — invisible carrier; `translation_range` is undocumented + unimplemented in `AnimRuntime`, so `xz`/`y` are read as travel distance over `run_time` in a fanned azimuth under the anim's own gravity — arc shape is TUNE). Ground sound layered over `plane_destroy_sg` via `FlightAudio.OnGroundExplosion`, gated on Ground. Verified: forward-dive crashes into C1 show the arcs streak out (`.scratch/obl_06.png`), 8-chapter build all report `5 debris-arc emitters`, zero hard errors, DISTANCE change proven a no-op for world puffers by construction. DEFERRED to later slices → `backlog.md`: `flydirt_plane` dust + water splash+steam (mesh/scale/`ObjectOpacityFromTo` path, not puffers), per-piece `large_firetrail`+bounces, and the water/air variants (need a surface signal / a mid-air destruct trigger).)**
9. ☑ **~~Conflict-local depth bias~~ → the OpenFlight subface flag** **(done 2026-07-23, and the item's own title is the sixth and last wrong framing: it is not a depth-bias problem and no bias constant was touched. `unk3`/`0x0800` is the OpenFlight SUBFACE mark — coplanar with and contained in the face beneath, drawn on top — and `support\init.gw` applies `GameGenSetSubfacePriorityOffset 1` to it globally. We parsed it nowhere. Fix is the predicted 3 edits: parse in `GameZ.cs`, `Subface` into the surface-group + material-cache key, `SubfaceBias` = half a priority level (1e-4, deliberately not the original's literal 1 level — priority 1 is authored). Measured at the C5 repro pose, jitter burst: ground crop **28.87% → 0.41%**, buildings crop **7.13% → 7.13%** (27,367 vs 27,373 px — untouched, as a ground-only fix must be), `--jitter=0` noise floor exactly **0.00%** on both builds so the residual is not temporal noise. Rule 4 satisfied the right way round: the metric fell AND the picture went to the authored layer — the baseline's washed-out daylit grey ground became near-black night blocks with street lights, matching `OriginalScreenshots/C5 IA1 Terrain.png`. **User confirmed at the controls: "no more z-fighting on the C5 ground."** 8-chapter regression: node/mesh/collider counts **identical everywhere**, zero errors; uv-clamped surfaces moved only in the four chapters with textured subfaces (C2 +3, C3 **+1** — and C3 has exactly **1** `unk3` polygon in the whole chapter, C4 +15, C5 +18), and C1B/C1C/C2B/C1 are unchanged. Aircraft carry **0** `unk3` polygons and the static plane viewer is byte-identical (md5 `F1290254…`). C1B is provably untouched, independently corroborating the user's not-a-bug ruling. `docs/HISTORY.md`)**

   <details><summary>The research trail that led here (kept — it is four retired mechanisms)</summary>

   **(RESEARCH DONE 2026-07-22, no code landed — and the item's inherited premise is now the FOURTH wrong mechanism retired. The "nine coplanar World-child nodes stack at y=5" reading is disproven: they are exactly coplanar and exactly same-priority, and they **tile** — 0.00 m² true polygon∩polygon area between every cross-node pair, plus an independent raster cross-check of 0 / 114,095 cells doubly covered. That is `verification.md` rule 9 (AABB ≠ overlap) costing this same bug a second wrong diagnosis. A software depth probe then found **the two recorded poses are two different bugs**: C5's 78.14%-of-frame conflict is `g4683` fighting ITSELF across its own surfaces, separated only by `SurfaceRankBias` (2e-6) — same node, so `node_bias` cancels and no per-node scheme can ever reach it; C1B's 36.33% is a single cross-node pair (743 `g28169` water over 716 `g28170` surf, index delta 27 → 1.35e-6). **The probe independently predicted the one previously-unexplained measured control** (`NodeOrderBias` 2e-6: C1B → 0.00%, C5 untouched — matching Godot's C1B 30.95%→2.35%, C5 35.96%→41.69% worse), which is the first account explaining both halves with one mechanism. Instrument + full findings preserved in `analysis/item9-depth-bias/`. **Next step is NOT to implement**: bracket the per-pair step first (see the ⚠ below), and get a C1B reference capture, because every scheme tested keeps water in front of surf.)**

   ⛔ **Superseded 2026-07-23 — the per-pair bracket was never needed.** The C5 conflict was not a
   precision problem at all, so neither the dense-rank scheme nor the C1B reference capture was
   required. Kept because these four retired mechanisms are what stop a seventh session
   re-deriving them.

   </details>
10. ☑ **Shader instance-uniform hygiene** — **BOTH PARTS DONE 2026-07-23. Part A landed as four `CSVM/shaders/*.gdshaderinc` files** (user's call: real shader files, not a C# const string). Verified first that Godot resolves `#include` in a `Shader` whose `Code` is assigned at RUNTIME — and that the obvious probe is useless, since an unresolvable include logs no error at all; the uniform list is the signal. `csky_instance_uniforms` is now the single ordered declaration site, so the indices cannot drift; `csky_srgb` / `csky_atmosphere` / `csky_lights` remove three-to-four-way verbatim duplication. The 128 combinatorial variants stay generated in C#. **Opaque sprite variants deliberately do NOT take the preamble** — a shader with no instance uniform keeps its instances off the fixed 16-vec4 per-instance buffer, and C5 stamps ~139k sprites. Caught before landing: `AnimRuntime.HasOpacityPath` string-scanned `sh.Code` for the uniform NAME, which the include would have silently inverted; it now tests the USE (`OpacityTerm`). Verified byte-identical at C5 city / C1 / plane viewer / mesh lab; C2 differs by ≤1 LSB on 0.280% of pixels (fog expression became a function). 8 chapters: counts identical, **zero** `instance_uniforms` warnings incl. C4/C5. ⚠ **C1B’s 18.32% "regression" was the instrument** — same build at `--frames=120` vs `121` gives the identical 18.32%/max-9, i.e. one frame of UV-scroll phase from changed shader-compile cost → `docs/verification.md` rule 23. **The plan’s mis-ordered-preamble control CANNOT fire** and that is the honest outcome: the hazard needs two disagreeing shaders on one `GeometryInstance3D`, which never happens here — the fix is structural (one declaration site), not behavioural. `docs/HISTORY.md`)**

    <details><summary>Part B, and its disproven rationale</summary>

    **Part B done 2026-07-23.** `csky_opacity` now rides in `PlaneViewer.InstanceShaderParams`, referenced through `SceneBuilder.OpacityParam` rather than a string literal. **But Part B's stated symptom is DISPROVEN and it was never a live defect.** The plan claimed cloud decks are opacity-animated, so panes 2–4 would render an opaque deck against pane 1's 0.6. Measured with a temporary probe in a 2-player C1 session: the opacity-animated node is `world1/g27816/l2586/cloudparent` at **0.6** — exactly as C1's `clouds.zrd.json` authors it — and it sits **inside `world1`, which every pane shares**, so it is never duplicated; meanwhile the subtree the duplication loop *does* copy (WorldBuilder's flat `cloudlayer` deck) carries **no `csky_opacity` on any node, in any chapter** (probe: `deck0 <none>`, `deck2 <none>`; 1840 world nodes carry the parameter and every one outside `cloudparent` reads 1.0). The fix is still correct and landed — the list's contract is "every instance uniform SceneBuilder can set" — but it is **latent hygiene, not a bug fix**, and the plan's "a deck posed to 0.6 comes out fully opaque in players 2–4" must not be repeated. ⚠ **Near-miss worth reading:** the first census said *zero* opacity events target any cloud node install-wide, which read as the plan being wrong about the data too. It was the instrument: the census swept `cam_anim`/`mis_anim` only, and C1's `cloudparent#` is **reader-only with no compiled twin** (`docs/HISTORY.md:1696` says so verbatim). New `docs/verification.md` rule 22.

    </details>

## Ground rules (carried from every prior plan here)

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project has fallen into most often.
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
  `backlog.md` (not marked FIXED there).
- **Read `docs/verification.md` before measuring anything.** Several items below hinge on
  instruments that mislead, and the rules are cited per item where they bite.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is expected to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's bullet in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

### ⚠ The standing warning from run 3

Of the five items worked in run 3's first wave, **three had materially wrong evidence in the plan** —
and in both bad cases the supporting evidence agreed while the mechanism did not. **Treat every
Evidence section below as a lead to verify, not a finding to implement. Landing no code with a
correct disproof is a success here.**

That said, the evidence quality in this plan is not uniform, and the difference matters more than
any of the individual findings:

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | 1, 2, 4, 5, 10 | Confirm the trace, then implement. These name a specific line and a specific wrong behaviour. |
| **Direction is sound, magnitude is a judgement call** | 6, 7, 8 | The *what* is settled by data; the *how much* is TUNE and goes on the TUNE list rather than being invented as fact. |
| **Leads only — no mechanism yet** | 9 | Budget the session for investigation. Item 9 has been diagnosed wrong three times. (Item 3 was in this row and has since been moved back to `backlog.md`, which is what this row's risk looks like when it materialises.) |

## Waves and dependencies

**Wave A — the animation runtime (items 1, 2).** Both edit `CSVM/src/Mech3/AnimRuntime.cs` and share
one regression surface, so they are **sequential: 1 before 2.** Item 1 changes *when* events fire;
item 2 changes *what pose* they leave behind. The police chase (`police_car-police_chase.json`) is
the shared repro for both, so doing 2 first means re-establishing its baseline after 1 lands.
*(Item 3 was moved back to `backlog.md` — see the checklist.)*

**Wave B — placement (items 4, 5).** Independent of each other and of Wave A. **These two share a
root cause worth naming once:** a gamez node whose `transform` is the JSON *string* `"Initial"`
leaves `GameZNode.Local` null, and the node is built at its parent's origin — which for a `world1`
child is (0, 0, 0). Two unrelated systems then read that origin as a real position: the stunt
objective loader (item 5) and the world builder (item 4). Fixing either does not fix the other, but
whoever does the second should check whether a shared guard is warranted rather than two local ones.

**Wave C — flight and audio (items 6, 7).** Independent; different files. Item 6 carries a small
dead-code deletion in `FlightController.cs` as a rider.

**Wave D — crash choreography (item 8).** Large, self-contained, touches `CrashBreakup.cs`,
`FlightController.Crash()`, `Puffer.cs` and `PlaneViewer`'s puffer wiring. Best done in one sitting.

**Wave E — rendering (items 9, 10).** Item 9 is the long-running research item and should be started
early if it is going to be attempted at all, because it may well end in a disproof. Items 9 and 10
both edit `SceneBuilder.cs` (9 near `:166`/`:841`, 10 in `GetBiasShader` at `:872-889`), so they
contend on that file — **do not run them in parallel worktrees.**

---

# Wave A — the animation runtime

## 1. ☐ `NextDue` applies each event's `START_TIME` to the *following* event

**Goal.** Fix the sequence scheduler so an event's start offset gates the event that carries it.
The bowl sign stops blacking out and flashes as authored; every other sequence in the install stops
being one slot out of phase.

**Evidence (traced to code — high confidence).** `AnimRuntime.SequenceRunner.Advance`
(`AnimRuntime.cs:1549-1568`) fires event `_pc`, then computes `_due = NextDue(ev, duration)` from
**the event it just fired**, and that `_due` gates event `_pc+1`. `NextDue` (`:1661-1668`):

```csharp
private float NextDue(AnimEvent ev, float duration) => ev.StartOffset switch
{
    "Animation" or "Sequence" => ev.StartTime,
    _                         => _clock + duration + ev.StartTime,
};
```

Per `CompiledAnim.cs:255-294`, `"Event"` means *"since the previous event fired"* — the offset gates
the event that **carries** it. So every timestamped event fires one slot early and its unstamped
partner fires one slot late.

**The bowl sign is the clean demonstration.** Node `bowl` (gamez list position 4868, world
≈ (−6618.7, 131.5, −5974.8), under `static`) has children `des_on` (model 631) and `des_off`
(model 632), **both shipped `active: true`**. `extracted/C1/zrdr/bowlsign.zrd.json` is
`ON_STARTUP`, `EXECUTION_BY_RANGE 300`, reset state `des_off INACTIVE, des_on ACTIVE`. The compiled
twin `extracted/C1/cam_anim/bowl-desert_onoff.json`, sequence `on_off`, is **nine strict pairs plus
an infinite `Loop`**, only the first of each pair timestamped:

```
start=null              des_on  = false
start=null              des_off = true      <- same instant as its partner
start={Animation, 1.0}  des_on  = true
start=null              des_off = false     <- must fire WITH the line above
start={Event, 0.5}      des_on  = false
start=null              des_off = true
…  (0.15, 0.15, 0.65, 0.4, 0.35, 0.2)  …
start={Event, 1.2}      Loop{-1}
```

Every pair is a **swap** — one variant off, the other on, simultaneously — so something is always
lit. Traced under the current code: events 0, 1 **and 2** all fire in frame 1 (event 1's null offset
leaves `_due = _clock`), leaving `des_on=true, des_off=true` — both lit and overlapping — and
`_due = 1.0`. At t=1.0 events 3 and 4 fire together: `des_off=false` then `des_on=false` — **the
sign goes fully dark for 0.5 s**. At t=1.5 both come back. That is exactly the reported
"ours disables and re-enables it instead".

**Approach.** Move the offset to the event it belongs to: gate event `i` on `Events[i].StartTime`
rather than `Events[i-1]`'s. The `"Animation"`/`"Sequence"` absolute cases and the `"Event"`
relative case all need to keep their current *meaning* — only the event they are read from changes.
Keep `duration` accumulating from the fired event (that half is right: an event's run time genuinely
precedes the next one).

**Verify.**
1. **The sign, directly** — `--freecam --chapter=C1 --campos`/`--lookat` onto (−6618.7, 131.5,
   −5974.8) with `--shots` across ≥3 s, and confirm exactly one of `des_on`/`des_off` is visible in
   every frame. Both-lit or neither-lit is a failure.
2. **`--debug-anim`** to confirm the pair events now share a dispatch tick.
3. **The regression this needs is wider than the item.** `NextDue` is the shared scheduler for every
   reader and compiled sequence, so re-check the hangar doors, the C1/C2/C3 traffic routes, the
   train, and the police chase (whose first `FROM_TO` carries `EVENT_OFFSET 1.0` and today starts
   1 s early with a 1 s stall behind it). 8-chapter `--debug-anim` pass, diffing the per-second pose
   report against a captured baseline.

**⚠ Traps.**
- **Take the baseline first.** This changes timing everywhere; without a pre-change `--debug-anim`
  capture you cannot tell an intended shift from a new break (`docs/verification.md` §5 — an
  unchanged number is not evidence unless you have seen it able to fail).
- Do not "fix" the bowl sign locally by toggling one mesh. The two-mesh swap **is** the original's
  flash mechanism; there is no texture-cycle or flash primitive involved.
- This item will change item 2's repro. Land it first, then re-baseline.

## 2. ☐ `FromToMotion` resets orientation to the authored rest pose

**Goal.** A translate-only event holds the last rotation the script wrote, instead of snapping the
node back to its authored rest orientation.

**Evidence (traced to code — high confidence).** `AnimRuntime.cs:1468-1474`:

```csharp
// Rebuild the basis from the absolute euler when a rotate channel is present,
// otherwise keep the rest orientation; deltas then compose on top of that.
var basis = _rest.Basis;
if (_rTo is { } rTo)
    basis = Euler((_rFrom ?? _rest.Basis.GetEuler(EulerOrder.Yxz)).Lerp(rTo, u));
```

`_rest` is `rt.RestOf(target)` captured at `:1430`; `RestOf` (`:1850-1855`) caches the node's
transform the first time anything touches it — before any pose op. The write at `:1481`
(`Target.Transform = new Transform3D(basis, origin)`) therefore clobbers orientation on **every
translate-only event**.

`extracted/C1/cam_anim/police_car-police_chase.json`, sequence `start_walkin`, makes the damage
explicit:

```
ROTSTATE police_car  y=45.0°  basis=Absolute
FROMTO   police_car  run 2.0   rot_from=None  rot_to=None   <- snaps back to authored rest
FROMTO   police_car  run 0.35  45.0° -> 10.0°
FROMTO   police_car  run 0.35  10.0° -> -15.0°
FROMTO   police_car  run 0.5   -15.0° -> 0.0°
FROMTO   police_car  run 17.0  rot_from=None  rot_to=None   <- snaps back again, for 17 s
```

`PoseRotate` (`:1870-1876`) sets the 45° yaw; the next translate-only tween throws it away, and the
**17-second straight — the longest, most visible leg — runs entirely at the authored rest
orientation.**

**The correct rule is already documented in the same file.** `ScriptPlayback`'s docstring
(`:1296-1300`): *"an ABSENT channel means 'hold the last value this script wrote', not 'return to
rest'"* — proven on C1/M04's `piratezep`. `ScriptPlayback` keeps `_rot`/`_scale`/`_origin` as
persistent components (`:1305-1307`, `:1331-1337`); `FromToMotion` does not.

**Ruled out already, do not re-chase:** *units* (all C1 moving-vehicle defs have compiled twins
whose radians convert to exactly the reader's degrees — 45.0, −15.0, 15.0; docstring `:1358-1360`
holds) and *Euler order* (`R = Ry·Rx·Rz` is applied consistently at `GameZ.ParseTransform:243-248`,
`FromToMotion.Euler:1484`, `PoseRotate:1873`).

**Approach.** Port `ScriptPlayback`'s persistent-component model into `FromToMotion`: hold
rotation/scale/translation as components that survive between events, and let an absent channel mean
"hold", not "reset". Update the channel-semantics docstring at `:1341-1364` in the same edit — it
currently documents the wrong rule.

**Verify.** Capture the police car's heading along its 17 s straight before and after (screenshot
sequence plus `--debug-anim` pose lines); the car should point along its direction of travel.
8-chapter regression on the other movers: `mafia`, `black_car1`, `car_go_home`, `car_loop1`,
`firetruck1-6`, `hauler1`, `flatbed1`, `truck1`.

**⚠ Traps.**
- **Do this after item 1**, and re-baseline: item 1 changes when these events fire.
- **Latent, adjacent, and NOT this bug — do not fold it in blind.** Reader-scope rotations are never
  degree-converted: `AnimDefs.cs:166-171` (`ObjectRotateState`) and `:176` (`AddFromTo(…, "rotate",
  "ROTATE_FROM", "ROTATE_TO")`) route through `Vec()` (`:509-518`) raw, while only `ObjectMotion`'s
  `XYZ_ROTATION` gets `Mathf.DegToRad` (`Spin`, `:523-542`). 209 C1 defs are reader-only *and* carry
  rotate ops — mostly plane/zeppelin sub-object scope and so mostly unreachable, but `fuelbox*`/
  `fuelboxconnect*` is one of them and its `hose` rotate of `20` would be applied as 20 **radians**.
  Note the connection to item 3, which is about those same `fuelbox*` nodes. If you fix the units,
  fix them as their own change with their own regression.


# Wave B — placement

## 4. ☐ C5 IA1: the sunk zeppelin is `piratezep` at the world origin

**Goal.** C5 IA1 stops showing a zeppelin buried in the ground.

**Evidence (traced — high confidence).** Three facts line up:

1. **C5's `ia1.gw` is the only IA1 setup script in the install that does not switch off
   `piratezep`.** From `extracted/interp.json`, script `support\c5\ia1.gw` (31 lines, 9 zeppelins
   deactivated): C1, C1B, C2, C2B, C3 and C4 all deactivate `piratezep`; **C5 deactivates
   `multiplayer1zep`, `multiplayer2zep`, `cargozep1/2/3`, `workersvoyagezep`, `dantezep`,
   `blackswanzep`, `beowulfzep` — and not `piratezep`.**
2. **C5's `piratezep` root carries `"transform": "Initial"`** with `parent_indices: [0]` (`world1`).
   `GameZ.ParseTransform` (`GameZ.cs:211-217`) leaves `node.Local` null for that JSON string, and
   `SceneBuilder` only assigns a transform when it is non-null (`SceneBuilder.cs:155-156`) — so the
   node is built at the parent's origin, (0, 0, 0).
3. **Its `child_bbox` is `y ∈ [−160.6, +57.1]`** — the hull hangs 160 m *below* its own root. A root
   pinned at y=0 with ground at y≈0 is literally a zeppelin sunk in the ground, matching the reported
   camera pose. (Note the recorded `--lookat` is exactly 100 units from `--campos`, so it is the F11
   `PoseLookAtDistance` marker, not the object.)

**Why this is genuinely separate from the 2026-07-22 `spline_interp` fix** (as the backlog asserts):
C5 ships no bootstrap-time placement for `piratezep` at all. Its only two anims are
`piratezep-gi_scene2.json` (`OnCall`, `si_script_ids [4,5,6]` — a generic-intro cutscene) and
`piratezep-apzep_engines_start.json` (`OnCall`). Neither is `ON_STARTUP`, and C5 has no
`zepstate.zrd.json`. **There is no degenerate transform to fix — there is no transform at all.**

**Also established:** `zeppelins.zrd.json`'s `position` field (e.g. C5/IA1 `multiplayer1zep` at
(−14279.5, 300.0, −1593.0)) **is read by nothing in `CSVM/src`** — grep finds it only in doc comments
(`MissionSetup.cs:28`, `PlaneViewer.cs:619`). Zeppelin poses come entirely from the anim layer
(`AnimRuntime.ScriptPlayback` `:1301-1338`, or `FromToMotion` `:1415-1485`).

**Approach — two candidate fixes; the data argues for the second.**
(a) Honour `zeppelins.zrd.json`'s `position`/`yaw` for parked zeppelins. (b) Treat "identity-transform
zeppelin root with no `ON_STARTUP` placement" as **not present**. The data favours (b): the original
almost certainly relies on `gi_scene2` being a cutscene that free flight never triggers, so the object
is not meant to be visible in C5 IA1 at all.

**Verify.** The recorded pose (`--campos=235.618,1471.759,94.103 --lookat=237.62,1371.833,97.39`)
before and after. Then an 8-chapter check that **no other zeppelin disappeared** — this is the risk
of (b), and it is the whole verification (`docs/verification.md`: verify by what disappears).

**⚠ Traps.**
- Do **not** lower or reposition the zeppelin to hide it. That is content invention, and the backlog
  already records the C1/M04 zeppelin-above-the-clouds case as an **accepted artifact** for exactly
  this reason.
- Whichever fix you take, enumerate the zeppelins present per chapter before and after. A rule that
  keys on "identity transform" could plausibly catch parked zeppelins that are *supposed* to be at
  their gamez position elsewhere.

## 5. ☐ C2 stunt: the Seaplane Hangar objective sits at (0, 0, 0)

**Goal.** The C2 Seaplane Hangar danger zone sits on the hangar.

**Evidence (traced — high confidence; the smallest, best-understood item in the plan).**

A stunt objective's position is **looked up from a world node by name, never taken from the ia.json
record** — the ia.json entry carries only two strings:
`StuntMission.cs:119` reads `dzones` → `:129-131` destructures `[pathName, dzName]` → `:132`
`worldGamez.FindByName(dzName)` → **`:143` `Position = worldGamez.WorldTransformOf(node).Origin`**.
Display text is a wholly independent lookup (`MissionTargets.Load`, `StuntMission.cs:123`, resolved
through `Messages.Get`) — which is why this bug shows a **correct label on a wrong point**.

`extracted/C2/IA1/zrdr/ia.zrd.json:343-348` — C2's first dzone is not a `dzN` marker:

```
"dzones", [ ["dzpath1", "sghangar"], ["dzpath2","dz2"], … ["dzpath9","dz9"] ]
```

`extracted/C2/gamez/nodes.json` array index **1171**, `sghangar`: `transform` is the JSON *string*
`"Initial"`, `model_index -1`, `parent_indices [0]` (`world1`, also identity), children
`[1172, 1173, 1174]` = `sgh_door1`, `sgh_door2`, `g35925`, `child_bbox` a=(−5892.0, 8.0, −5625.8)
b=(−5648.8, 38.9, −5367.4). `Local` stays null → **`Position` resolves to exactly (0, 0, 0)**, about
8 km from the building.

Correct anchors from the geometry: `sghangar` child_bbox centre **(−5770.4, 23.5, −5496.6)**; the
hangar mouth between the doors ≈ **(−5770.4, 23.5, −5623.9)** (`sgh_door1` (−5733.5, 23.5, −5623.9)
and `sgh_door2` (−5807.3, 23.5, −5623.9)); body `g35925` at (−5770.4, 23.5, −5494.7). For contrast a
normal marker, `dz2` (index 1485), has a real `RotateTranslateScale` `translate =
(−6033.0054, 26.00221, −3844.231)`.

**Scope: C2 is the only affected chapter.** Every `ia.zrd.json` was scanned — C1 (5), C1B (5), C3 (4),
C4 (14), C5 (17) are pure `dzN`; C2 has 9 zones of which exactly one (`sghangar`) is a geometry node.

**Approach.** In `StuntMission.cs:132-147`, fall back when the node has no transform / `MeshIndex -1`
with children: anchor on the bbox centre instead of `WorldTransformOf(...).Origin`. **Note the bbox
fields are not currently parsed into `GameZNode` at all** (`child_bbox`/`node_bbox`/`active_bbox`
exist in nodes.json with no C# field), so either the parser gains them or the centre is computed from
the children's built meshes. **Precedent for exactly this choice already exists in the repo:**
`NodeLabels.cs:217` (`sum += mi.Transform * mi.GetAabb().GetCenter()`), landed with the note
*"Anchor at the mesh AABB centre, not the node origin. Gamez origins are frequently nowhere near the
geometry they draw."*

A fly-through zone on a hangar plausibly wants the **door midpoint**, not the body centre — the two
differ by ~127 m in Z. Prefer the mouth; it is what "fly through" means.

**Verify.** `--stunt --chapter=C2`, confirm the marker sits on the hangar and the zone can be flown
through. Then confirm the other 8 C2 zones and all other chapters are **unmoved** — the fallback must
not fire for a normal `dzN`.

**⚠ Traps.**
- Consumers follow automatically (`MarkerHud.cs:144,145,158,212`, `StuntMission.cs:190`), but
  `DzRadius` (`StuntMission.cs:59`) is a point-marker radius and a hangar mouth may want more.
  **`DzRadius` is 15 m and that value is correct** — user-tuned by hand 2026-07-22 (an old
  `backlog.md` note claiming 30 m was stale and has been fixed). **Do not retune it as part of this
  item.**
- **Worth a look while you are in here, but keep it a separate change:** one global radius does not
  fit — 15 m is too tight at some zones, and 30 m was loose enough that you could fly *around* the
  danger and still score it. A per-zone extent would have to come off the marker node, since the
  `dzones` record is two strings with no size: check whether `dzN`'s `RotateTranslateScale` **scale**
  is meaningful rather than identity, and whether `node_bbox`/`child_bbox` (**not parsed into
  `GameZNode` today** — the same gap this item already has to close for `sghangar`) gives a usable
  extent. `dzpathN` is named in the data and read by nothing; check it too. If none of that pans
  out, 15 m stands as a tuned constant and this stays on the TUNE list.

---

# Wave C — flight and audio

## 6. ☐ Knife-edge: the nose should drop, not just the flight path

**Goal.** In a sustained knife-edge the nose drops slightly as the aircraft sinks, instead of the
plane descending wings-level-nosed.

**Evidence (confirmed line-for-line).** Both knife-edge terms act on `VelocityDir` (the flight path),
never on `Attitude`:
- `FlightModel.cs:165-166` — `wingVert = Abs(Attitude.Y.Dot(Vector3.Up))`; `liftFrac = speedLift *
  wingVert` → **0 at 90° bank**.
- `:186` — `+ gAcross * (1f - liftFrac)`, the un-cancelled cross-path gravity (the sink); feeds `vel`
  at `:187` → `VelocityDir` at `:190`.
- `:198-199` — `align = AlignRate * Clamp(Speed/liftSpeed,0,1) * (KnifeAlignFloor + (1-KnifeAlignFloor)*wingVert)`,
  applied at `:206-213` by slerping **`VelocityDir` toward `nose`**. `Attitude` is untouched.

The only code rotating the nose toward world-down is the stall block, `:113-127`, gated at `:113-114`
on `isStalled()` (`:218-222`, `Speed < StallSpeedFrac * Stats.FdSpeed`); the great-circle rotation is
`:118-125`. Constants: `StallSpeedFrac` `:45` = 0.30, `StallNoseRate` `:47` = 1.0, `KnifeAlignFloor`
`:50` = 0.35. The only other `Attitude` writes are `:80` (`Reset`), `:132` (integrating `BodyRates`),
`:149`. **The backlog's diagnosis holds exactly.**

Note `docs/HISTORY.md:39` (M2-polish item 6) describes today's behaviour as *designed* —
"knife-edge loses lift … 90° bank sinks at path ≈ −10°". This item adds an attitude term on top; it
does not revisit that decision.

**Approach.** Add a new attitude term next to `:198-199`, where `wingVert` is already in scope,
reusing the great-circle rotation pattern from `:118-126` — a `KnifeNoseRate` scaled by
`(1 - wingVert)`. **Do not touch the existing path terms**, exactly as the backlog prescribes.

**Verify.** Scripted `--hold` run (`--hold=0,1,0,0.5` is the established knife-edge input) capturing
nose pitch over time: the nose angle below horizon should grow and settle, and the plane should still
sink. Compare against a pre-change capture to show the path behaviour is unchanged.

**⚠ Traps.**
- **`KnifeNoseRate`'s magnitude is TUNE, not fact.** The data does not give it. Add it to the
  backlog's TUNE list with the rest of the flight constants and let the user judge the feel; the
  scripted test proves *presence and direction*, not correctness of magnitude.
- Do not let the new term fight the stall block at low speed — check the interaction where both are
  active.

**Rider — delete the dead soft-tree collision branch.** Confirmed still dead at both sites:
`FlightController.cs:565` (`bool tree = hitName.EndsWith("clutter_col");`) and `:649`. Nothing can
produce that name: `Clutter.cs:782-785` names bodies `clutter_bld_{cx}_{cz}` and
`MapEdgeExtender.cs:260,272` names them `clutter_bld_ext`, **both with a comment saying they are
named so as NOT to end in `clutter_col`**. Remove the branch (`:567-568`, `:581-582`, `:591`, `:597`,
`:602-606`, `:647-653`) and with it `TreeDamage` (`:189`, used only at `:581`) and `TreeSpeedFactor`
(`:190`, used only at `:604`), plus the stale doc comments at `:292`, `:559`, `:647`.

## 7. ☐ Mute on focus loss — and settle the pad question with it

**Goal.** Alt-tabbing away silences the game; alt-tabbing back restores it. Decide in the same change
whether pad reads should be gated on focus.

**Evidence (confirmed — nothing exists).** Grep over all of `CSVM/src`: `NOTIFICATION_` /
`_Notification` **zero hits** (no `_Notification` override anywhere); `AudioServer` **zero hits**;
audio `Bus` **zero hits** (nothing sets `AudioStreamPlayer.Bus`; everything is on Master by default).
`FOCUS` hits are all unrelated (Control `FocusModeEnum.None`, camera "focus point" variables, menu
cursor focus).

**Every place volume is controlled today:**
1. `FlightAudio.cs:38` `MixGain` — set once at construction from `PlaneViewer.cs:1048` (the
   splitscreen 1/√N equal-power split), **never modified at runtime**.
2. `FlightAudio.cs:125-126` engine loop `VolumeDb`; `:128` whine (× `WhineMixGain` `:33` = 0.12);
   `:130` rattle; `:144` crash one-shot — **crash and prop one-shots deliberately skip `MixGain`**
   (`docs/HISTORY.md:105`, "one-shots stay global"); `:165`/`:182` the shared `PlayOneShot`/
   `UpdateLoop` helpers.
3. **`WorldSounds.cs:130-136`** — the ambient 3D emitters, `AudioStreamPlayer3D` with `VolumeDb` from
   the sound def: **a second, entirely separate audio path with no `MixGain` and no shared gate.**
4. `PlaneViewer.cs:77,288,377,524,542-545,644` — `--mute` is a **load-time** switch that simply never
   constructs `FlightAudio`/`WorldSounds`. It cannot be toggled after startup.

Every gain is written per-frame from a curve, so a focus mute must be either a factor all paths route
through, or an `AudioServer` bus mute — and the bus wiring does not exist.

**Pads are not gated anywhere.** `Pads.cs` (39 lines) is the single source of truth —
`Connected()` `:30-31`, `For(bound)` `:36-37` — and its only gate is `Pads.Disabled` (`:25`, the
`--no-pads` flag). No consumer checks focus. `Pads.cs:15-19` documents that SDL hints do not help, so
the switch has to live in project code, making `Pads.Connected()` the natural choke point.
`docs/HISTORY.md:795-803` records the open question verbatim: *"whether pad reads should be gated on
window focus project-wide, which would also stop a flight taking stick input while alt-tabbed."*

**Approach.** A `_Notification` override on `PlaneViewer` handling
`NOTIFICATION_APPLICATION_FOCUS_OUT`/`_IN`. Prefer the **`AudioServer` master-bus mute** over
threading a factor through two independent audio paths — it is one call, it covers `WorldSounds`
(which has no gain plumbing at all), and it does not perturb the per-frame curves. Then make the pad
call: gating `Pads.Connected()` on focus is the same override and closes the recorded open question.

**Verify.** Launch, alt-tab out and back, confirm silence and restoration; confirm the engine loop
resumes at the right level rather than re-ramping from `-60f` (`FlightAudio.cs:79`). With a pad
connected, confirm a held stick does not fly the plane while unfocused.

**⚠ Traps.**
- **Do not implement the mute by setting `MixGain = 0`** — it would miss `WorldSounds` entirely and
  the one-shots deliberately bypass it.
- The pad half is a **behaviour change the user may not want** (some players run windowed with a pad).
  Implement it behind the same notification but call it out explicitly when the item lands, rather
  than folding it in silently.

---

# Wave D — the crash

## 8. ◐ The full `player_plane_destruct` crash choreography — FIRST SLICE landed 2026-07-23

**Goal.** A crash plays the original's authored choreography — the right variant for the surface,
sparks, dust or splash, the smokeball, the debris arcs — instead of one generic fireball.

**Evidence.** Current state, stated by the code itself at `CrashBreakup.cs:18-19`: *"The full
player_crash_default/_dirt/_water choreography (sparks, surface variants, plane_destroy_sg) stays on
the backlog."* `FlightController.Crash()` (`:309-324`) is **one path with no surface discrimination**:
hide model, `Audio?.OnCrash()` (`:315`), `CrashEffect?.Burst(impact)` (`:316`),
`Breakup?.Begin(...)` (`:319`). Wiring at `PlaneViewer.cs:996-1037`.

**The data — three variants, all present, all decoded.** Reader:
`extracted/zrdr/player_plane_destruct.zrd.json`. Compiled twins in every chapter:
`extracted/C1/cam_anim/player-player_crash_default.json`, `…_dirt.json`, `…_water.json` (plus
`kestrel-ai_crash_*` and `player-player_destruction_reset.json`). Def shape: `anim_root_name
"player"`, `activation OnCall`, nodes `player, healthy, destroyed, dontmove, markers, piece1..piece4,
shadow, cockpit1` — **the same subtree `PlaneBuilder.BuildDestroyed` already builds.**

- **`_default` (air), sequence `destroy_it`, 15 events:** `cpejectstop`, `rem_pas`, then
  `healthy/destroyed/dontmove/markers` all false (**note `destroyed` is FALSE in the air variant**),
  `small_yellow_sparks` ×5 at `healthy` (three at origin, one +(0,0,1), one +(1,0,0)),
  `large_fireball` at `healthy`, `call_crash_trails` at `destroyed` +(0,0,3), **`Sound
  plane_destroy_sg`**, and a second `large_fireball` at **Event+0.35 s** +(0,−4,0).
- **`_dirt` (ground):** `destroyed` = **TRUE** (the wreck stays), 2× sparks, **`flydirt_plane`** (the
  brown dust burst), **`large_black_smokeball`**, **`large_10sec_fire`** at `destroyed` (this is the
  wreck fire already implemented), `call_crash_trails` +(0,−3,3), `Sound snd_exp_ground_a`, and
  **three more fireballs** at Event+0.25 s at +(−3,0,5), +(3,2,5), +(−3,2,−5).
- **`_water`:** sequence is named **`destroy_crash`**, not `destroy_it`. `call_crash_trails` ×2,
  `large_10sec_fire`, `plane_big_splash` at `healthy` +(0,10,0), `large_steam_spray`; a second
  sequence `fade` deactivates `piece1..piece4`.
- **Per-piece:** `piece1seq`/`3`/`4` activate the piece → `large_firetrail WithNode pieceN` → an
  `ObjectMotion` with `gravity {-9.8, complex, do_intersections}` → deactivate (`piece2seq` has no
  trail). Bounce handlers `p1hit`/`p2hit` (`OnCall`) stop the sequence, play `air_mixed_exp_sg`, and
  run a second `ObjectMotion` with `translation_range xz 120–200`. Wired via `BOUNCE_SEQUENCE
  ["p1hit"], RUN_TIME [6]` and `p1grndhit..p3grndhit`, `RUN_TIME [10]`.

**What the effects resolve to** (compiled defs in `extracted/C1/cam_anim/`):

| Effect | Puffer | Key params |
|---|---|---|
| `small_yellow_sparks` | `trailpuffer2` | number 15, interval 0.1, world_vel (0,10,0), rand_vel ±(10,40,10), **world_accel (0,−9.8,0)**, size 0.2–0.4, life 2–3, friction 2, textures `exp_yel01`,`poleflare`, colors null → additive; stopped at Event+0.2 s |
| `large_fireball` | `fierypuffer` | **already implemented** as `CrashEffect`; burst stopped +0.3 s |
| `large_black_smokeball` | `trailpuffer2` | number 5, interval 1.0, world_vel (0,3,0), rand_vel ±65, size **4–7**, life 4–5, friction 8, growth→4.0, textures `thickblksmoke01/02/03`, **colors null**; stops at Sequence+2.1 s |
| `large_10sec_fire` | `fire_n_smoke` | **already implemented** — and this is where `CrashBreakup.BurnTime = 10f` comes from (`stop_fire_n_smoke` at Animation+10.0 s) |
| `call_crash_trails` | `spurtpuffer1..5` | five sequences, each `CallSequence partN_trail` → `ObjectMotion` on `fly_trail1..5`, gravity −3/−2/−2/−1/−2, `translation_range xz` 35–55/135–165/205–255/75–105/155–205. Each puffer is a DISTANCE_INTERVAL trail with a 6-frame `fire_f01..06` flipbook → **five burning debris arcs.** This is exactly the "burning debris arcs" video follow-up. |
| `large_firetrail` | `lgpuffer` | DISTANCE_INTERVAL trail, interval 2.0, size 1.2–2.6, `fire_f01..06` at 0.05 s steps |
| `flydirt_plane` | **none** (`puffers: null`) | Pure mesh animation: `ObjectMotion` on `flydirt`, an `ObjectMotion` **scale** on `dust`, then **`ObjectOpacityFromTo`** 0.3→0.0 over 1.7 s. **Not a Puffer — needs the anim/mesh path** (`AnimRuntime.SetSubtreeOpacity` exists at `:1906`). |

**`Puffer.cs` can already express 4 of the 5 puffer effects with no new parser.**
`PufferState.FromAnimEvent` (`:120`) parses the compiled payload directly, including the
`interval_garbage.interval_value` quirk (`:113-114`); static `Textures` pool (`:50-52`),
`TextureSequence` flipbook (`:47-48`), `DistanceInterval` + `TrailAdvance`/`TrailEnd`/`TrailBurnAt`
(`:42-45`, `:442`, `:474`, `:482`), `WorldAcceleration` stored **and integrated** (`:26`, `:611`),
`Friction`/`Size`/`Lifetime`/`Growth`/`Deviation`/`Number`/`Colors` all present.

**Four gaps to plan around:**
1. **Blend mode is inferred, not authored** — `Puffer.cs:136`:
   `ShaderCode.Replace("BLEND_MODE", state.Colors.Count > 0 ? "blend_mix" : "blend_add")`.
   `large_black_smokeball` has **`colors: null`**, so it would build **additive — and additive black
   smoke is invisible.** (`docs/HISTORY.md:71` already flags this class of problem.) It needs an
   explicit blend override or a texture-name heuristic.
2. `fade_range` (700–900 camera-distance fade) is parsed by nobody (`Puffer.cs:16-18` says so).
3. `AtNodeOffset` exists (`:34-40`) but the crash events carry their offset in
   `parameters.AtNode.position` on the **CallAnimation**, not the PufferState — separate plumbing.
4. No `ObjectOpacityFromTo` / scale-`ObjectMotion` support in the Puffer path → `flydirt_plane` must
   go through the anim/mesh path.

**Surface selection is engine-side, not data-side.** The three variants are never
`CallAnimation`-referenced by any other def (they appear only in `cam_anim` metadata and their own
files) — the original picks natively. The decision point here is `FlightController.Crash()` (`:309`),
which already receives `impact` and `hitName`.

**Approach.** Sequence it: (1) surface discrimination in `Crash()` picking one of three variants;
(2) sparks + the extra delayed fireballs (pure Puffer, already expressible); (3) `call_crash_trails`
debris arcs; (4) the smokeball, with the blend fix; (5) per-piece `large_firetrail` and the bounce
sub-sequences, attaching at `CrashBreakup.Begin()` (`:88`) / `Advance()` (`:114`) — the `Piece` struct
(`:23-31`) already tracks `Resting`, i.e. the bounce moment; (6) `flydirt_plane` last, since it needs
the mesh path. `PlaneViewer.cs:1634` `MakePuffer(...)` is the existing construction helper
(call shape at `:1034-1036`).

**Verify.** Scripted crashes into ground, water and air (mid-air breakup) with `--shots` bursts,
checking each variant's distinctive marker: dust + smokeball on dirt, splash + steam on water, no
persistent wreck in air. `--debug-anim` to confirm the right def is driving.

**⚠ Traps.**
- **`plane_destroy_sg` is ALREADY DONE — the backlog is wrong on this point.**
  `extracted/zrdr/sounds.zrd.json` `SOUND_GROUPS` defines it as `DYNAMIC_WEIGHTS 0.5` over
  `snd_exp_plane1..4`, and `FlightAudio.cs:53-58` already loads exactly those four and `:142` picks
  one at random per crash. The group's membership and behaviour are reproduced, just hardcoded rather
  than resolved through a `SOUND_GROUPS` table. **Do not re-implement it.** What *is* missing on the
  audio side: `air_mixed_exp_sg` (piece bounces: `snd_exp_hit1/2/3/3a/5`), `ground_mixed_exp_sg`,
  `snd_exp_ground_a`, and a real `SOUND_GROUPS` resolver.
- This item is large. **It is fine to land it in slices** — each of the six steps above is
  independently verifiable and independently valuable.

---

# Wave E — rendering

## 9. ☐ Conflict-local depth bias — the surviving direction on C1B/C5 z-fighting

**Goal.** Separate coplanar sibling nodes without scrambling the authored layering — or produce a
clean, recorded disproof.

**⚠ Read this before anything else.** This bug has been diagnosed **three times and got a different
wrong answer each time**: coarse-sheet-vs-partition-ground (implemented, changed nothing, 35.77% →
35.79%); `g4683` fighting its own polygons (rested on an **AABB** overlap — the real outlines share
edges and overlap by zero area); and the per-polygon within-surface tie-break (implemented properly,
moved nothing at all three poses). `docs/verification.md` rules 4, 7, 9 and 11 are all written from
this bug. **Landing no code with a correct disproof is a success.**

**Evidence — what actually survives.** Constants in `SceneBuilder.cs`: `DepthBiasPerLevel` `:687` =
2e-4, `SurfaceRankBias` `:696` = 2e-6, `SurfaceRankCap` `:697` = 5, `NodeOrderBias` `:698` = **5e-8**
(`public const`). Applied per-material at `:841-842` (`bias = Clamp(priority * DepthBiasPerLevel,
±0.05) + rank * SurfaceRankBias` → `depth_bias`) and per-instance at `:166`
(`mi.SetInstanceShaderParameter("node_bias", node.Index * NodeOrderBias)`, uniform declared `:873`).
Shader: `:972` `VERTEX *= 1.0 - (depth_bias + node_bias);` (mirrored in `MeshLab.cs:668`). Other
consumers any redesign must follow: `Clutter.cs:220`, `:698`, `MapEdgeExtender.cs:297`,
`PlaneViewer.cs:1252`.

**The measurement that survives all three disproofs:** this renderer's depth-resolution floor is
**≈1e-6 of view distance** (bracketed directly — a per-polygon depth ramp of 2e-7 moves C5's flip
rate 35.96% → 33.20%; one of 2e-6 moves it to 0.37%). `NodeOrderBias` at 5e-8 is **20× below that
floor**, so sibling nodes 36–60 indices apart straddle it and nodes 1 index apart (1822 vs 1823) can
never separate. **The bias is `node.Index * constant` — strictly uniform. No coplanar-group ranking,
no conflict detection, no scene-graph analysis exists anywhere in `CSVM/src`.**

**Inherited structural evidence (moved here from `backlog.md` 2026-07-22 — this is now its only home
besides `docs/HISTORY.md`).** Established and still standing:

- **The two surfaces at the C5 pose.** The coarse sheet is node 1777 `g4683` (34 polys,
  2048 × 11264, `brick1`/`cblock1`), a **direct child of `world1`**; the fine ground is
  partition-referenced. `world1`'s 105 children and the 471 partition roots are **exactly disjoint**
  (intersection 0, all 8 chapters), and the `terrain` node flag is set on every partition root and no
  World child — so "partition-referenced" is readable straight from the data. Neither side sits under
  an `Lod` node, so `SceneBuilder.cs:132-134`'s nearest-LOD rule could never have dropped either.
- **Nine coplanar World-child nodes stack at y = 5** inside the repro footprint — 1777, 1799, 1800,
  1801, 1813, 1814, 1822, 1823, 1837, at priorities 0 and −10. The −10 ones separate cleanly
  (10 × 2e-4); the priority-0 ones are separated only by `node_bias`, and 1822 vs 1823 (index delta 1
  → 5e-8) can never separate at all. **That is why the flicker is partial rather than total.**
- **The coarse sheets must not be culled** — no sheet is fully covered by fine tiles, so culling
  leaves holes somewhere. ⚠ **The per-sheet coverage percentages once quoted here are RETRACTED and
  must not be re-quoted** (the original method tested fine-tile *bounding-box* containment; an
  independent rasterisation disagrees, 18.2–81.4% vs the claimed 0.0–97.8%).
- **The rule "demote World children below partition ground" does not generalise.** Across all 8
  chapters only C1, C4 and C5 have any World-child mesh (1 / 4 / 9); **C1C has none**, `a6` is the
  detailed airfield tile, and C4's `g1612` is a 477 m cliff — demoting them would regress.
- **Cautions for anyone re-measuring.** A naive "large flat quad" filter also catches the `fvol*`
  **fog volumes** (10 in C1, 14 in C5, at altitude) — exclude them by name. And `zone_id` does not
  explain the pair: **both surfaces are `zone_id = 1`**.
- **Reference captures** (user-confirmed as being about this issue): `OriginalScreenshots/C5 IA1
  Terrain.png`, `…Terrain2.png`, `…Terrain3.png` — C5 IA1 at night, in all three the ground reads as
  the **fine-detail city-block surface** with no coarse quad over it. **Not conclusive on its own:**
  a still cannot show z-fighting, and none of the three poses is matched to our repro camera. To make
  it conclusive, re-shoot the original at the repro pose.

**Related but NOT this item:** partition visibility (`WorldPartitionSetActive`, 25 uses in interp) is
the real runtime system the original uses to pick between coarse and fine ground, and we draw both
unconditionally (`WorldBuilder.cs:155`, `:157-159`). It stays in `backlog.md` under
"Blocked / deferred" — implementing it is cell-resident tracking with pop risk and an 8-chapter
regression, i.e. Milestone 3 work, not polish.

> ## 🛑 C1B IS NOT A BUG — user ruling, 2026-07-22: **"C1B z-fighting is not a bug. It's in the
> ## original."**
>
> The C1B repro pose reproduces the **original game's own** z-fighting. It is a fidelity target,
> not a defect, and our replication was already correct there.
>
> **This closes the whole conflict-local-bias direction, not just one pose.** Per the analysis
> below, C1B was the *only* half a dense per-node rank could fix; C5's conflict is one node
> fighting its own surfaces, where `node_bias` is identical on both sides and cancels. With C1B
> ruled authentic, the dense-rank scheme has nothing left to fix and **must not be implemented** —
> it would drive C1B away from the original.
>
> Everything the earlier measurements implied is now inverted: `NodeOrderBias` 5e-8 → 2e-6 taking
> C1B from 30.95% to 2.35% was never an improvement, it was **1.4 percentage points from erasing
> a faithful artifact**. The instinct to "fix" the number was the error; the constant staying at
> 5e-8 is why C1B still looks like the original.
>
> **What survives: C5 only — and it is NOT a depth-bias bug either. FIFTH mechanism, user
> observation 2026-07-22:**
>
> > "C5 seems to be a real defect. What I can see in the original is that there is some kind of LOD
> > mechanism where the ground texture changes from completely dark with some points to a brighter
> > illuminated street. It corresponds to going from `cblock1_2.png` (bright lights but lower
> > resolution) to `cblock1_1.png` to `cblock1.png`. **I could not find `cblock[4-6].png` in C5 IA1
> > in the original.**"
>
> Line that against the measurement: our 78.14% conflict is `cblock4` over `cblock2` over
> `cblock1`, **all coplanar inside `g4683`**. If the original draws only the `cblock1` family
> there, then we are **rendering ground variants the original never draws**, and the flicker is a
> *symptom* of drawing 2–3 layers where the original draws 1 — selected at runtime by a mechanism
> we do not implement.
>
> **⛔ Therefore do NOT raise `SurfaceRankBias`, even though it measures perfectly.** Scheme 1
> takes C5 to 0.00% — by picking a winner among surfaces that should not be co-rendered at all.
> The metric would be flawless and the picture still wrong: `docs/verification.md` rule 4 exactly,
> and rule 8 (a measurement that locates the geometry has not told you which surface is at fault).
> **Every "fix" this item has ever proposed is now known to be papering over a missing system.**
>
> ### ✅ MECHANISM FOUND 2026-07-23 — the fifth framing was right, and this is the answer
>
> **The polygon flag mech3ax calls `unk3` (raw `0x0800`) is the original's OpenFlight *subface*
> mark** — "this face is coplanar with and contained in the face beneath it; draw it on top" — and
> **`support\init.gw` line 22 applies `GameGenSetSubfacePriorityOffset 1` to it globally, for every
> mission in the game**, beside `SetCoplanarTolerance`/`SetBFETolerance`/`SetInverseZTolerance`.
> **`grep unk3 CSVM/src` returns zero hits: we parse it nowhere.**
>
> So `cblock4/5/6` are not a day set and not a distance LOD — they are the **base** ground, and
> `cblock1/2/3` are subfaces laid on it. **Measured:** 25.85M m² of C5 ground is subface-over-base;
> we render **66.5% of it inverted and 5.6% exactly tied**. At the repro pose the base `cblock4`
> wins **144,817 px (62.8% of frame)** where the original shows the subface. Applying the flag
> takes the pose **78.14% → 0.00%** and `cblock4` to **16 px**, with the **C1B and C3 poses
> byte-identical** (no `unk3` polygons participate at either).
>
> **The falsifiable prediction held exactly:** a subface is *contained*, never partial — coverage
> of each flagged polygon is **bimodal**, 196 at exactly 100%, 251 at ~0%, exactly one in between.
> `cblock6` is **100.000%** covered by `cblock3`. Corroborating: every `unk3` texture across the
> install is overlay-shaped (`terpat01/03/04` = **ter**rain **pat**ch, `cliff*_trans*`, `river1/3`,
> `wtr00000`, `pier`), and the original screenshots show only the near-black night blocks — the
> daylit base (3× the mean luminance) appears nowhere.
>
> **C1B has ZERO `unk3` polygons in the entire chapter** — an independent, data-side corroboration
> of the user's "C1B is not a bug" ruling: the original authored no resolution there either, so it
> z-fought too. The fix provably cannot perturb it.
>
> **Ruled out with data, all of it:** `WorldPartitionSetActive` is **C3-only** (all 25 uses) and
> coordinate-based — **this plan's own "the real runtime system…" claim was wrong for C5**; `Lod`
> nodes cover 3250 C5 nodes but none of the cblock ground; `cycle` is null on all seven materials;
> `zone_set` is 1 on both sides.
>
> **Fix: 3 edits, ~10 lines, no new parser and NO bias-constant changes.** Parse `flags.unk3` in
> `GameZ.cs:278` (absent-when-false — use `TryGetProperty` with a `false` default), add `Subface`
> to the surface group key at `SceneBuilder.cs:386` (and the material cache chain), add
> `bias += subface ? SubfaceBias : 0f` at `:841`. **Use 0.5 of a priority level (1e-4), not the
> original's literal 1 level** — priority 1 is genuinely authored (955 C5 polygons) and a full
> level would make a subface tie with a real overlay; 0.5 measures identically. ⚠ **The regression
> risk is rank shifting, not the fix itself**: splitting a group changes every later group's rank
> and therefore its bias, so the 8-chapter `--freecam` surface/mesh-count regression is the check
> that matters. Full evidence: `analysis/item9-depth-bias/CBLOCK-LOD.md`.
>
> ## ⚠⚠ Everything from here to the end of item 9 was written BEFORE the 2026-07-22
> ## data analysis, and its central evidence is now DISPROVEN.
>
> **Read `analysis/item9-depth-bias/FINDINGS.md` first.** The "nine coplanar World-child nodes
> stack at y = 5" claim below is another AABB reading: the nine **tile**, with 0.00 m² of true
> polygon overlap between every cross-node pair (two independent methods). The text below is
> retained only because the *constants*, the *poses* and the *disproven-list* remain accurate.
>
> **What replaced it, all measured:**
> - **The two recorded poses are two different bugs.** No single fix can move both, which is
>   exactly why all three previous attempts moved one and not the other.
> - **C5 (78.14% of frame at risk): `g4683` fights ITSELF** — `cblock4` rank 1 over `cblock2`
>   rank 0, and `cblock1` rank 2 over `cblock4` rank 1. Same node, same priority, different
>   materials → different Godot surfaces. `node_bias` is identical on both sides and **cancels**.
>   `SurfaceRankBias` (2e-6) is the entire separation. **A dense per-node rank cannot touch C5.**
> - **C1B (36.33%): one cross-node pair** — node 743 `g28169`/`wtr00000` in front of node 716
>   `g28170`/`srf0001`, index delta 27 → 1.35e-6, i.e. 1.35× the claimed floor. This *is* the
>   half a dense rank fixes.
> - **The probe reproduced the previously-unexplained control from data alone**: `NodeOrderBias`
>   → 2e-6 gives C1B 0.00% and leaves C5 untouched, matching Godot's measured C1B 30.95%→2.35%
>   and C5 35.96%→41.69%. First account that explains both halves with one mechanism.
> - **The 1e-6 "floor" is not a safe denominator.** `SurfaceRankBias` = 2e-6 is *twice* it and is
>   exactly the separation on 78% of a frame that still measures 35.96% flicker. The polish-3
>   bracket measured a *cumulative* per-polygon ramp (~10× its step), not the per-pair step a
>   tie-break needs. **The required per-pair step is > 2e-6 and has never been bracketed.**
> - **The slot arithmetic, conditional on that:** longest conflict chain is 27/11/16/16/10/13/20/28
>   (8/3/0/5/0/5/5/3 excluding the origin-parked pile). Budget = 2e-4 / step. At 5e-6 it fits
>   comfortably; **at 1e-5 it does not** for C5 and C1.
> - **Order matters if both land:** today 2–16% of cross-node conflicting pairs already resolve
>   the wrong way round; raising `SurfaceRankBias` makes that worse and the dense rank makes it
>   markedly better, so the dense rank goes first or they are sized together.
>
> **Do this before writing any code: (1) bracket the per-pair step — one constant, two poses;
> (2) get a C1B reference capture at the repro pose.** On (2): water currently renders in front
> of surf and **every** scheme tested keeps it there (all preserve node index order, 743 > 716),
> so a bigger separation only makes the current winner win harder. If the original draws surf
> over water, the flicker metric improves while the picture gets worse — rule 4's failure mode,
> and the same shape as the C3 beach case. The data cannot settle it: "later node wins" is the
> documented rule and it says water.

**Approach — the recorded direction: make the bias dense over the nodes that actually conflict.**
Instead of `node.Index * 5e-8` spanning thousands of indices, detect groups of coplanar same-priority
nodes within a spatial region and assign **small dense ranks within the group**, so the separation
clears the 1e-6 floor without spanning tens of priority levels. This is a **scene-graph analysis, not
a constant change.**

**Verify.** `--shots=5 --jitter=0.006` (the project standard; flip threshold 8/255) at both recorded
poses, using `.scratch/flickerdiff.py` / `flickermap.py`:
- **C5:** `--viewer --chapter=C5 --sky-zone=zone2 --campos=-9533.178,76.319,-3367.413
  --lookat=-9451.281,28.148,-3398.597` — baseline 35.96%
- **C1B:** `--campos=-7698.844,48.763,-5797.924 --lookat=-7749.957,-20.093,-5849.367` — baseline 30.95%

**⚠ Traps.**
- **Do NOT "fix" this by raising `NodeOrderBias`.** Measured: 5e-8 → 2e-6 takes C1B from 30.95% to
  **2.35%** and C5 from 35.96% to **41.69% (worse)**. That control is a **diagnosis, not a landable
  fix**.
- **A metric going to zero is not the outcome being right** (rule 4). The 2e-6 per-polygon ramp drove
  C5 to 0.37% *by floating the coarse sheet in front of the detailed city* — the flicker vanished
  because the wrong surface won. **Check what the fix looks like, not just what it measures.** The
  reference captures (`OriginalScreenshots/C5 IA1 Terrain*.png`) show the **fine-detail city ground**
  winning, with no coarse quad over it.
- **Always pass an explicit `--campos`/`--lookat`** (`verification.md:151-153`), and **always run a
  `--jitter=0` control** — a waterline C3 view moves 14.4% of pixels with the camera frozen
  (`verification.md:381`), so a jitter burst there reads ~15% of pure noise.
- The C3 beach pose measures 0.41% and **does not reproduce a flicker** — it needs a fresh capture
  before it can be used as evidence for anything.

## 10. ☐ Shader instance-uniform hygiene

**Goal.** Remove the latent index-mismatch hazard structurally, and fix the live splitscreen opacity
defect.

**Part A — the `csky_fog_on` index disagreement (latent, confirmed).**
`Clutter.cs:605` declares `instance uniform float csky_fog_on` as that shader's **only** instance
uniform → **index 0**. `SceneBuilder.GetBiasShader` declares `node_bias` at `:873` → index 0, so
`csky_fog_on` at `:887` lands at **index 1** (and `csky_opacity` at `:888-889` at index 2). Godot
merges instance-uniform mappings across the materials on one `GeometryInstance3D`, and a disagreement
silently drops fog on the losing surfaces — the 2026-07-17 unfogged-hilltops bug.

**Verified latent, not live:** **nothing writes `csky_fog_on` anywhere** in `CSVM/src` (the former
writer was deleted — `WorldBuilder.cs:394` records it; `PlaneViewer.cs:1685` notes `--no-fog` pushes
the range out of reach instead of touching the uniform). Clutter also renders through
`MultiMeshInstance3D` + `MaterialOverride` and never shares an instance with a `SceneBuilder`
material. **Adding the first writer would make it live.**

The fix is **a shared ordered-preamble constant emitted by both shaders**. Padding `Clutter`'s shader
with an unused `node_bias` was already tried and reverted — it enforces nothing, since the next
shared uniform still has to be added to both by hand. Keep the third consumer in sync:
`MeshLab.cs:655` also declares `node_bias` (and `:23` says it copies the stage verbatim). The in-file
invariant is currently documented at both declaration sites (`Clutter.cs:591-604`,
`SceneBuilder.cs:874-883`) — that documentation is replaced by the preamble, not kept alongside it.

**Part B — `csky_opacity` missing from splitscreen duplication (live defect).**
`PlaneViewer.cs:1252`:

```csharp
private static readonly string[] InstanceShaderParams = { "node_bias", "csky_fog_on", "csky_light_fade" };
```

Three uniforms; **`csky_opacity` is absent.** `CopyInstanceShaderParams` (`:1254-1266`) is called from
`:1242` inside the splitscreen cloud-deck duplication loop (`:1240-1246`), whose comment (`:1249-1251`)
states the rationale — `Duplicate()` does not carry per-instance shader params — but the list was
never extended when `csky_opacity` was added. The only writer is
`AnimRuntime.cs:1921` (`g.SetInstanceShaderParameter(SceneBuilder.OpacityParam, alpha)`, via
`SetSubtreeOpacity` `:1906`, the `OBJECT_OPACITY_STATE` handler at `:418`).

**Why it bites:** `OBJECT_OPACITY_STATE` genuinely animates cloud decks (`docs/HISTORY.md:1688` —
C1's `cloudparent#` is the single largest use in the game), and cloud parents are precisely the nodes
the duplication loop copies. **A deck posed to 0.6 opacity by `AnimRuntime` comes out fully opaque in
players 2–4.**

**Approach.** Part B first — it is one token, and it should reference `SceneBuilder.OpacityParam`
rather than a string literal, since `AnimRuntime.cs:1921` already goes through that constant. Then
Part A, which changes every world material's shader text and therefore needs the full regression.

**Verify.** Part B: a 2-player splitscreen session in a chapter where the deck is opacity-animated,
confirming both panes match. Part A: 8-chapter regression — the shader text changes everywhere, so
this is a "prove nothing moved" run, and per `docs/verification.md` §5 an unchanged number is only
evidence if you have seen the instrument able to fail (deliberately mis-order the preamble once and
confirm the regression catches it).

**⚠ Traps.**
- Part A is a **structural** fix. If it degenerates into "add a padding uniform", stop — that is the
  approach already tried and reverted.
- Do not add a writer for `csky_fog_on` as part of this item; that is what makes the latent hazard
  live, and it belongs to whatever feature needs it.

---

## Corrections to `backlog.md` found while verifying this plan

**✅ All three below were applied to `backlog.md` on 2026-07-22 — re-verified against the file, no
action left.** Correction 1 needed no edit in the end: the crash-choreography entry had already been
consumed into item 8 when it was scheduled, so the wrong claim no longer exists in `backlog.md`
(the record of it survives in item 8's traps, which is where it is useful). Corrections 2 and 3 are
live in the file at the "Static collider probe" and "Stunt mode" entries respectively. Kept below as
the reasoning, not as an open to-do.

Both were discovered by checking the backlog's claims against the code, and **should be applied to
`backlog.md` whether or not the items are worked:**

1. **`plane_destroy_sg` is already implemented in effect** (item 8). The backlog lists it as missing
   from the crash choreography; `FlightAudio.cs:53-58` + `:142` already reproduce the sound group's
   exact membership and random-pick behaviour. Only the *generic `SOUND_GROUPS` resolver* and the
   other groups (`air_mixed_exp_sg`, `ground_mixed_exp_sg`, `snd_exp_ground_a`) are genuinely absent.
2. **`.scratch/probe_exempt.py` no longer exists.** The "static collider probe is off by 6 (C4) and
   11 (C5)" entry reads as though the script is on hand; `.scratch/` was swept by `CleanScratch.ps1`
   and is gitignored, so **there is no copy anywhere** and picking that item up means rewriting the
   probe first. The entry should say so.

Additionally, one small discrepancy worth resolving whenever item 5 is worked: `StuntMission.cs:59`
reads `DzRadius = 15f`, while `backlog.md` describes it as "30 m (tightened from 60 on user
feedback)". One of the two is stale.
