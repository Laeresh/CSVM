# Animation/rendering follow-ups (post-billboard-fix backlog)

Four independent, separately-completable items found while chasing the user's C1/IA1
rendering reports on 2026-07-21 (waterfall mist, billboard axes, signal-bridge flares — all
landed the same day; see `docs/HISTORY.md`). Each item below is scoped to fit in its own
session: read its Goal/Evidence/Approach/Verify, then go. Statuses: ☐ open · ◐ in progress ·
☑ done — keep the checklist in sync as items land, per this repo's standing rule.

Ground rules carried over from every prior plan in this repo: original-game data drives
everything (read the reader/compiled JSON before writing a handler, don't guess a value);
`CLAUDE.md` + `docs/architecture.md`/`docs/formats/` are updated in the **same turn** as each
landed item; a landed item gets a dated entry in `docs/HISTORY.md`; verify every change
against a full 8-chapter `--freecam --chapter=<X>` regression (zero errors, same mesh/node
counts unless the change is expected to add coverage) before calling it done, plus a targeted
screenshot at the specific location the report came from.

## Checklist

1. ☐ `If`/`Elseif` condition evaluation + `AnimationLod` quality setting
2. ☐ `LightState` + the remaining unacted-on event kinds
3. ☐ Mission-spawned entity rosters (`hk_zep`, CTF props)
4. ☐ `texture_scroll` rendering

**Dependency note:** items 1 and 2 are the two halves of one visible payoff — `AnimRuntime`
currently *skips* every `If`/`Elseif` branch (item 1), and even once a branch runs, its
payload is very often a `LightState` event the runtime doesn't act on yet (item 2). Landing
item 1 alone will not change what the refinery or lighthouse look like — the branches will
finally execute, but their `LightState` events will still no-op. Either do both in one
session, or land item 1 first and confirm via `--debug-anim`-style logging that the branches
are now taken (not skipped) before moving on, so item 2's session isn't debugging two things
at once. Items 3 and 4 are fully independent of 1/2 and of each other.

---

## 1. `If`/`Elseif` condition evaluation + `AnimationLod` quality setting

**Goal:** `AnimRuntime`'s `SequenceRunner.Advance` currently treats every `If`/`Elseif` as
"skip the branch body" (`docs/formats/anim-definitions.md` records this as the leftover
assumption that conditions are unknowable gameplay state). Replace the skip with a real
evaluation for every condition kind the data uses, so branches that should run, run.
`AnimationLod` specifically becomes a project-level quality setting rather than a fixed
answer, per the user's request (2026-07-21): default it to whatever tier makes every
LOD-gated branch in this install pass (the hardware has no reason to hide detail the original
only hid for performance), with a CLI knob to lower it later if ever wanted.

**Evidence** (surveyed 2026-07-21 across the whole install, see `docs/formats/
anim-definitions.md`'s "`IF`/`ELSEIF` conditions are all evaluable" section for the full
table):

| Count | Condition | How to evaluate |
|---:|---|---|
| 4537 | `RandomWeight` (0..1) | A dice roll — `GD.Randf() < weight`. |
| 4007 | `AnimHealth` | Object health; full/undamaged in a fresh world build. |
| 1052 | `PlayerRange` | Distance from the player/camera; live scene state, recompute per branch check. |
| 717 | `NodeActive` | Whether a named node is currently active; our own scene state (`Visible`/subtree-active). |
| 473 | `NodeUndercover` | Node + distance — needs a line-of-sight/occlusion test; lowest-value, consider stubbing false first. |
| 124 | `AnimHealthRange` | `{min,max}` health window — same as `AnimHealth`, range test. |
| 120 | `AnimationLod` | **Our own setting** — see below. |
| 120 | `PlayerFirstPerson` | Our own camera mode; `false` until a cockpit view exists (backlog). |
| 28 | `NodeBelowAlt` | Node + altitude threshold; live scene state. |
| 17 | `HwRender` | Hardware rendering — always `true`. |

Two concrete payoffs once this lands (found chasing the user's refinery/lighthouse reports):
`refinery_fire_always` and `ref_light_always1..6` wrap their **entire** light sequence in
`If{AnimationLod:2}`; `litehouse_sparking` gates its spark bursts on `If{RandomWeight:0.7}`.

**Approach:**
- `SequenceRunner`'s `"If"`/`"Elseif"` case (currently `rt.Count(...); _pc = SkipBranch(_pc);`
  unconditionally) becomes: evaluate the condition via a new `AnimRuntime.EvaluateCondition
  (AnimData condition, Node3D? anchor)` — a `switch` on the condition's tag (the `Union()`
  helper `AnimData` already exposes elsewhere is the right shape: conditions arrive as a
  one-key object, e.g. `{"RandomWeight": 0.7}`). `true` → fall through to the branch body
  (increment `_pc` past the `If`/`Elseif` event and keep going); `false` → `SkipBranch` as
  today, but then re-check the next `Elseif`/`Else` instead of jumping straight to `Endif`
  (current `SkipBranch` behavior — read it before changing; the control-flow state machine
  may need an explicit "which branch is active" flag rather than pure skip-to-Endif).
- `AnimationLod`: add a settable `AnimRuntime.QualityLod` (int, default = the max value seen
  in a survey like the one above — confirm the actual max across all 8 chapters before
  hardcoding it, don't assume 2). `EvaluateCondition`'s `AnimationLod` case is
  `QualityLod >= requested`. Wire a `--anim-lod=N` CLI arg in `PlaneViewer.cs` (optional,
  defaults to max) purely so a screenshot can force a lower tier for A/B comparison — not a
  real settings-menu feature this pass.
- `RandomWeight` needs a stable RNG the runtime already owns (check if one exists; if not, a
  single `System.Random` field, not `GD.Randf()`, so `--debug-anim` runs are reproducible
  with a seed if that's ever wanted).
- `NodeActive`/`NodeBelowAlt`/`PlayerRange` need a "resolve this name in the current scene"
  path — `ResolveOne`/`ResolvePath` already exist for event targets; reuse them rather than
  inventing a second resolver.

**Verify:** `--debug-anim`-style log line when a branch's condition is evaluated (kind + value
+ result) for a session or two while landing this, then remove/gate it behind an env var like
the puffer diagnostics used this session (temporary, reverted before done). Confirm via that
log that `refinery_fire_always`'s `AnimationLod` branch is now taken. Full 8-chapter
regression (zero errors; op-count increases are expected and fine here, unlike the mission-
roster fix — document the new counts). `--debug-anim` pose log unaffected for the train/cars/
doors (this item touches only control flow, not the transform/roster fixes already landed).

## 2. `LightState` + the remaining unacted-on event kinds

**Goal:** Act on the event kinds `AnimRuntime.Dispatch` currently only counts
(`Count(ev.Kind)` in the `default:` case) and drops. `LightState` is the highest-value one —
it's the payload behind the refinery/lighthouse branches item 1 unlocks, and 518 events in C1
alone. The rest (`Sound`/`SoundNode`, `ObjectOpacityState`/`ObjectOpacityFromTo`,
`ObjectCycleTexture`, `ObjectAddChild`, `FbfxColorFromTo`, `CameraState`, `ObjectMotion`) are
independent, much smaller additions — pick off however many fit a session.

**Evidence:** the dispatch table's `default:` comment already states the intent ("Adding a
handler is a case above and nothing else") — this item is exactly that, event kind by event
kind. Payload shapes are in the compiled JSON dumps already taken this session (e.g.
`refinery-refinery_fire_always.json`'s `LightState` events carry `name`, `type_`
(`PointSource`), `translate` (`AtNode` + local offset), `range: {min,max}`, `directional`,
`saturated`, etc. — dump a few more `LightState` events across chapters before committing to
a field list, the refinery sample alone may not cover every variant). `ObjectCycleTexture`'s
one known use is the cockpit damage-indicator hilite cycle (`docs/formats/gamez.md`'s
`damageindicator` bullet) — already implemented as a *build-time* material swap in
`GaugeCluster.cs`; check whether the anim-driven case is the same mechanism or a distinct one
before assuming it's redundant.

**Approach:** one `case` per kind in `AnimRuntime.Dispatch`, same pattern as the existing
`ObjectActiveState`/`ObjectRotateState` handlers — resolve the target node(s) via
`Targets(ev, def, anchor)`, apply the state. `LightState` likely wants an actual
`OmniLight3D`/`SpotLight3D` node (or, if performance matters at the density these fire — 518
in C1 — a cheaper approach reusing `SceneBuilder`'s existing point-sprite glow shader
(`GetLightPoints`/`LightShaderCode`) rather than real dynamic lights, since the world already
renders fullbright and doesn't consume dynamic lighting anywhere else). Decide which BEFORE
writing code — a real light per event at this count could be a measurable perf regression on
C4/C5; the sprite-glow route reuses proven, cheap infrastructure. `Sound`/`SoundNode` should
reuse `FlightAudio`/`SoundArchive`'s existing one-shot/loop machinery, not a new audio path.

**Verify:** per kind landed, a targeted screenshot/listen at a known location (refinery
lights, lighthouse sparking once item 1 + this land together; a `Sound` event's location for
audio — note audio can't be screenshot-verified, describe what should be audible and let the
user confirm). Full 8-chapter regression after each kind. Update the "Playback ops seen and
deferred to part 2+" list in `docs/formats/anim-definitions.md` — remove each kind as it
lands, don't leave the doc claiming something is deferred once it isn't.

## 3. Mission-spawned entity rosters (`hk_zep`, CTF props)

**Goal:** `hk_zep` (the Hollywood Knights zeppelin) sits on the C1/IA1 field when it should be
absent — confirmed against the user's M04 reference screenshot, where it correctly *is*
present. C1's CTF props (`ctf_1`/`ctf_2` gate posts, `cs_flag_1`/`cs_flag_2` flags) need the
same treatment: visible only in Capture the Flag, invisible everywhere else (user-confirmed
against the original), and flag sprites carry no collider in the original.

**Evidence** (this session, `docs/formats/anim-definitions.md`'s "Mission-spawned entities
(open)" section): scenery visibility is governed by compiled `zepstate` (landed — see the
`AnimProgram.cs` mission-scope-library fix). *Entities* are the opposite: absent unless a
roster spawns them.
- `aiv.zrd.json` (AI vehicles) — C1/IA1 lists only the player; C1/M02 lists `hk_zep`; C1/M04
  lists `hk_zep` + `piratezep`.
- `zeppelins.zrd.json` (flyable zeppelins, with position/yaw/engines/cannons/gasbags) —
  C1/IA1: `multiplayer1zep`. C1/M04: `piratezep`. MP1/MP2: none. MP3: two.
- CTF: `ctf_1`/`ctf_2`/`cs_flag_1`/`cs_flag_2` are referenced only by `C1/MP2/zrdr/
  targets.zrd.json` — the CTF mission's own objective list, which is presumably the roster
  signal for this family (worth confirming there isn't a more specific CTF roster file before
  committing to `targets.zrd.json` as the mechanism — it may just be the *coincidental* only
  reference, not the actual spawn gate; the mission-type check might belong on `net.zrd.json`
  or a scenario-name check instead).

**Approach:** do NOT hide-by-name-pattern (`dliner1` is also zeppelin-shaped and must stay
visible — it's already correctly handled by the zepstate fix, a different mechanism from this
one). Build the entity visibility set from the rosters themselves: parse `aiv.zrd.json`/
`zeppelins.zrd.json` (new small readers, or extend `AnimDefs.cs`/a sibling file — check
whether `Zrdr.LoadFile`'s existing alternating-list walker already covers this shape before
writing a new parser) into a name set, then in `WorldBuilder`/`AnimRuntime`'s bootstrap, any
node matching a *known entity-family name pattern* (start with `hk_zep`, `piratezep`,
`multiplayer1zep`, `multiplayer2zep`, `cargozep1` — the names already surveyed as
zeppelin-roster entries) is active only if it's in that mission's roster set. For CTF: first
determine the actual roster mechanism (see the open question above) before writing the hide
rule — don't guess `targets.zrd.json` is authoritative without checking whether a mission-type
signal exists elsewhere (e.g. does `net.zrd.json` or the scenario name distinguish CTF from
other MP modes cleanly?). Flag collider removal is a one-line addition once the flag entity is
built — mirror `WorldBuilder.IsFlareSpriteNode`'s collision-exemption pattern (a predicate
`NoCollisionNode` already OR's in), not a new mechanism.

**Verify:** C1/IA1 field is empty where `hk_zep` used to sit (screenshot at
`--campos=-5466.595,284.452,-5136.92 --lookat=-5376.862,244.637,-5155.967`, the coordinate
that first surfaced this); C1/M04 still shows `hk_zep` on the field (regression — this is the
mission that SHOULD show it); MP3 shows both `multiplayer1zep` and `multiplayer2zep`; MP1/MP2
show neither. CTF props hidden outside MP2 (or whatever the confirmed real gate turns out to
be), visible inside it, with flags non-collidable. Full 8-chapter regression.

## 4. `texture_scroll` rendering

**Goal:** Wire the parsed-but-unused `GameZMesh.TextureScroll` (u/v units/second) into the
renderer so the 5 models in this install that use it actually scroll: a hangar glass-roof sky
reflection (`h_zone1scroll`, `sky2.tif`), an oil-dock texture (`con_scroll`), and three boat
wake fronts (`wakefront1.tif`, C1B). Low priority — nothing currently reported needs it (the
waterfall, which is what surfaced this field, turned out to scroll at `{0,0}`; its motion is
entirely the splash-puffer mist, already landed).

**Evidence:** `docs/formats/gamez.md`'s `texture_scroll` bullet has the full field survey.
The blocker isn't parsing (done) — it's that `SceneBuilder`'s material cache is keyed by
`(Material, Priority, Rank, DoubleSided)` only. `sky2.tif` (used by `h_zone1scroll`) is almost
certainly also used by ordinary non-scrolling sky geometry elsewhere in the same chapter — two
meshes sharing a `materialIndex` but wanting different scroll rates would silently share
whichever `ShaderMaterial` instance got cached first. Confirm this collision actually occurs
(grep how many *other* nodes reference the same `materialIndex` as each of the 5 scrolling
models) before designing around it — it may turn out none of the 5 collide in practice, which
would simplify the fix considerably.

**Approach (pending the collision check above):** if collisions exist, extend the material
cache key to include a scroll-rate bucket (or, simpler, key scrolling materials by
`(materialIndex, scrollU, scrollV)` and only fall into that keyspace when `TextureScroll !=
Vector2.Zero`, leaving the common non-scrolling path's cache key untouched). Shader change:
`GetBiasShader`'s fragment `UV` becomes `UV + scroll_rate * TIME` (a new `uniform vec2
scroll_rate` per material instance, not a new shader-code variant — `TIME` is a Godot spatial
shader built-in, no extra plumbing needed). Verify `repeat_enable` is already set on the
textured sampler (it is, per the existing anisotropic-filtering uniform line) so the scroll
wraps instead of clamping at the UV edge.

**Verify:** screenshot the hangar glass roof and the C1B oil dock over a `--shots=N`-style
multi-frame burst (like the z-fighting debug burst, repurposed here to show motion across
frames) — confirm the reflection/texture visibly shifts frame to frame at the expected rate.
Confirm NO other scrolling regression: every other textured surface in a full 8-chapter
regression must render motionless across the same multi-frame burst (a UV scroll leaking onto
a shared non-scrolling material would show up exactly this way).
