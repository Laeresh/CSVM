# Animation debugger — `--anim-lab`, a permanent def-playback lab

**🟢 LIVE — written 2026-07-23, design decided with the user (grilling session).** Runs
**before** [`PLAN-data-driven-crash.md`](PLAN-data-driven-crash.md): the lab is the development
and verification loop that plan's Layer 1 handlers are built inside, and this plan delivers that
plan's Wave 2a/2b scaffolding (the crash subtree + effect templates + a bound runtime). It is a
**permanent tool**, not crash scaffolding — every M3 `WeaponHit` destruction def will be debugged
in it.

---

## The decisions (what was agreed, so it is not re-litigated)

| Decision | Choice |
|---|---|
| Lifespan | Permanent lab, documented like the H/L/M labs |
| Entry point | **New top-level `--anim-lab` mode** (not a viewer lab key) |
| Code sharing | **No duplication** — extract what the lab needs from `PlaneViewer.cs` into reusable classes; first step of the eventual PlaneViewer split (full split = future work, *not* this plan) |
| Session content | Full chapter world (default C1) + parked plane + crash-effect templates, **one runtime over all of it** |
| Stage default | **Quiet** — reset states run, startanims do NOT; a toggle enables full ambient playback |
| Selection | `--play-anim=<name>` (auto-play on launch, composes with `--screenshot`/`--shots`) + in-lab filterable picker |
| Transport | Start/Restart/Pause/Resume/Stop + **single-frame step** while paused + time-scale (0.1×/0.25×/1×); no backward scrub in v1 |
| Determinism | **Fixed-dt accumulator + pinned RNG seed** (`--seed=N` override). Restart is visually identical; live game stays unseeded/live-dt |
| Timeline | Authored event blocks per sequence lane (child defs indented) + moving playhead + **fired marks at actual dispatch time** — authored-vs-actual divergence visible (the instrument that would have caught the `NextDue` off-by-one) |
| Camera | The viewer's orbit camera, extracted; **auto-frames the played def's anchor** on play |
| Sound | Def `Sound` events play; `--mute` respected |

**Constraint exported to the crash plan (locked in now, before its handlers are written):** the
new Layer-1 handlers' randomness (`rnd_xz`, `translation_range` azimuth) and any puffer jitter
they touch must route through the runtime's seedable RNG (`AnimRuntime._rng`, :113 — the comment
at :111 anticipated exactly this), NOT `GD.Randf()`. Otherwise lab restarts stop being identical
the day the handlers land.

---

## Architecture

### A. Extraction from PlaneViewer (pure refactor, no behavior change)

Only what the lab consumes — the rest of the split stays future work:

1. **`src/UI/OrbitCamera.cs`** (name free) — the orbit controller: `_orbitCenter`/`_orbitDistance`
   state (PlaneViewer :218-219), framing (`FrameCamera` :1898-1912: AABB → distance from FOV),
   pose update (:1960-1961), and the input handling (LMB drag orbit, wheel zoom, :2069-2080 inside
   `_UnhandledInput` :2034). PlaneViewer delegates; `--campos`/`--lookat`/F11/F12 behavior
   unchanged.
2. **`src/SessionPaths.cs`** (name free) — the extracted-data path resolution: `PreferUnzipped` +
   the per-chapter gamez/texture/mission-zrdr path construction (:528-534).
3. **`src/Mech3/WorldSession.cs`** (name free) — the world+anim session build the viewer's
   world-mode `try` does today (~:515-695): gamez+textures load, `WorldBuilder`, clutter, mission
   setup, `AnimProgram.Load` (:631-639), the `AnimRuntime` collaborator wiring (PufferFactory /
   Lights / Sounds / PlayerPosition, :644-670), bind (:678), sound prewarm (:686-693). Parameters
   for what varies by mode (fullbright, debug flags, puffer parent); returns the built root +
   runtime + the collaborators the caller owns.

⚠ The build-scope **disposal lifetimes are semantics, not incidentals** — `textures` dies at
build-scope end, so `PufferFactory` is cleared right after bootstrap (:679) and the sound loader
after prewarm (:693). The extracted class must preserve that contract for the viewer *and* allow
the lab to opt out (see Traps: the lab keeps its archive open).

⚠ In world mode the viewer's `_plane` field **is the world root** (bound at :678, added at :843) —
the content-root naming in the new class should say what it is; PlaneViewer's own field keeps its
name for now (renaming it is the future split's business).

### B. AnimRuntime additions (additive; defaults preserve live behavior)

1. **Manual advance.** Extract the `_Process` body (:252-…) into `public void Advance(float dt)`;
   `_Process` calls `Advance`. The lab calls `SetProcess(false)` and feeds `Advance` itself —
   pause = don't call, step = one fixed step, slow-mo = scaled accumulator. **Same classes, same
   code path as the game** (the user's requirement); the game's behavior is untouched.
2. **Bootstrap autoplay flag** — e.g. `public bool AutoStart = true`. When false, `Bootstrap`
   (:121) runs pass 0 (mission setup :133), pass 1 (reset states :139-148) and pass 4 (safety
   net :188) but **skips pass 2 (ON_STARTUP :154-159) and pass 3 (startanims :164-185)**; a
   `StartAmbient()` method runs the skipped passes on demand (the lab's ambient toggle).
3. **Seedable RNG** — make `_rng` (:113) seed-settable at construction (init property). Default
   unseeded: the game is unchanged.
4. **Dispatch observability** — a callback hook (e.g. `public Action<AnimInstance, string seq,
   string eventKind, string? eventName>? OnEventDispatched`) raised where events dispatch, plus
   instance start/finish notifications. The timeline's fired marks come from this, **not** from
   parsing `--debug-anim` log text. Null by default → zero cost in game.
5. **Verify `Stop` (:320) actually clears the stopped def's live motions/puffers/lights/sounds.**
   Restart = `Stop` → re-apply `ResetState` events → `Start`; if `Stop` leaks any of those, fix it
   *in the runtime* — the crash plan's respawn path (its 2c) needs exactly the same cleanup.

### C. The `--anim-lab` mode

- Arg parsing: a mode flag beside `--viewer`/`--freecam`; `_worldMode` includes it. Defaults:
  `--chapter=C1`, plane optional (`--plane=` parks one at the mission spawn point; `--spawn-at`
  override). No FlightController — the plane is a stage prop.
- Session: `WorldSession` with `AutoStart=false`, seed pinned (default constant, `--seed=N`
  override), the lab's own `TextureArchive` kept open for the whole session so puffers/decals can
  be built interactively at any time.
- Time: the lab owns a fixed-dt accumulator (1/60 steps) driven from its `_Process`; render rate
  never changes simulation results. Playhead time = steps × dt, shown numerically with the seed.
- Transport keys (final bindings in-code; suggestion): Space pause/resume, `.` step, `R` restart,
  `S` stop, `A` ambient toggle, Esc/F11/F12 as everywhere else.
- `--play-anim=<name>`: resolve via `AnimProgram.ByAnimName`, apply the def's `ResetState`,
  `Start(def, anchor)` — anchored defs use their anchors; unanchored use global resolution (null
  anchor), same as bootstrap pass 3 (:176-183). Auto-frame the orbit camera on the def's anchor
  (fallback: first resolved target node's AABB).

### D. Timeline + picker UI (Godot Controls, like the other labs)

- **Picker**: `LineEdit` filter + `ItemList` over `program.Defs` — name, activation
  (`OnCall`/`WeaponHit`/`OnStartup`/…), anchor name. Enter = play.
- **Timeline**: a custom `Control` (`_Draw`) — one lane per sequence of the played def, blocks at
  **authored** start times, a playhead line, and tick-marks stamped at **actual** dispatch (from
  the B4 hook). A `CallAnimation` child def gets its lane group appended, indented, offset at the
  playhead time its instance started; its own events at authored offsets within. Loops re-mark on
  each pass (marks accumulate; Restart clears).

---

## Build order

- **Wave 1 — extraction refactor** (A1-A3). Pure; PlaneViewer delegates. Verify screenshot-identical
  + identical anim boot census before anything else lands on top.
  **✅ A1 landed 2026-07-23** (`src/UI/OrbitCamera.cs`) — orbit camera extracted verbatim, PlaneViewer
  delegates; static plane-viewer `--screenshot` md5s byte-identical before/after (2 planes + a `--yaw=0`
  control), rule-5 able-to-fail control passed. See `docs/HISTORY.md`.
  **✅ A2 landed 2026-07-23** (`src/SessionPaths.cs`) — `PreferUnzipped` + per-chapter/mission path
  construction extracted verbatim (override policy stays in PlaneViewer); plane-viewer md5 unchanged +
  C1 world boot census byte-identical HEAD-vs-after. **A3 (`WorldSession`) next.**
- **Wave 2 — runtime capabilities** (B1-B5). Additive, defaults = live behavior; world regression
  must be unchanged.
- **Wave 3 — lab MVP** (C). Quiet stage, transport, fixed dt + seed, `--play-anim`, auto-frame.
  **No timeline yet.** Verified by playing known-good world defs (see Verification).
- **Wave 4 — picker + timeline** (D), fed by the B4 hooks.
- **Wave 5 — crash stage** = the crash plan's Wave 2a/2b, delivered here: build the plane's
  `destroyed` subtree (`PlaneBuilder.BuildDestroyed` :133) + the effect templates
  (`carnage_trails` → `fly_trail1..5`, `flydirt` → `flydirt`/`dust` — world-gamez roots
  `WorldBuilder` deliberately skips; C1 `nodes.json` :46813/:46899) into the lab's stage, hidden
  by reset states; wire `PufferFactory` on the lab runtime. Acceptance:
  `--anim-lab --play-anim=player_crash_dirt` fires the def's **puffer** events visibly (sparks,
  fireball cluster, smokeball — handlers exist today); motion/opacity events dispatch-but-inert
  until the crash plan's Layer 1. Then amend `PLAN-data-driven-crash.md` (its Wave 2 shrinks to
  the `FlightController` wiring, 2c) — done as part of this wave, not left to memory.
- Docs each wave lands with: `docs/architecture.md` bullets for the new classes, the CLI table row
  (CLAUDE.md + `docs/cli.md`) when `--anim-lab` exists, `docs/HISTORY.md` entries.

---

## Verification

- **Wave 1 (refactor):** fixed-pose screenshots (`--screenshot`, `--jitter=0`) across the plane
  viewer + several chapter worlds must be **byte-identical** before/after; anim bootstrap census
  lines (def/instance/motion counts) identical. `docs/verification.md` rule 5 applies: confirm the
  instrument can fail by diffing against a deliberately broken build once.
- **Wave 2:** same world regression unchanged (flags default off). Then flip each flag once in a
  throwaway run to see it *able* to change behavior (AutoStart=false → "0 start anims running").
- **Wave 3 (the lab proves itself on known-good anims):** play a def whose live behavior is
  established — the C1 train loop, a door def — and check the lab playback matches the `--freecam`
  world (same motion, same period, via `--debug-anim` pose logs). Determinism: two runs with the
  same seed, screenshot burst at the same step counts → identical images; different seed → different
  (rule 5 again).
- **Wave 4:** play a def with known authored times (the crash def's fireball cluster at ~1.1-1.5 s)
  and check fired marks land on the authored blocks — this doubles as a regression test of the
  polish-4 `NextDue` fix.
- **Wave 5:** the acceptance run above, as a `--screenshot`/`--shots` burst — the repeatable
  scripted form is the point; it becomes the crash plan's Layer-1 harness.

## Traps

- **The disposal-lifetime contract** (A: `textures`/`PufferFactory`/sound-loader scoping) is easy
  to silently break in the extraction — the viewer must keep closing its archives; the lab must
  keep its own open. A later puffer request in the viewer should still *report* rather than crash
  (the :640-643 comment is the spec).
- **Quiet stage still needs passes 0/1/4.** Skipping reset states or the safety net leaves
  destroyed-variant subtrees and un-setup mission entities visible — the quiet flag skips
  *autoplay*, not *state*.
- **Pause pauses the runtime, not the renderer.** Shader-time effects (UV scroll TIME, GPU
  precipitation) and already-playing audio streams keep going through a pause; that is accepted
  v1 behavior. Do not chase "the water didn't freeze" as a bug later.
- **Determinism's boundary is the runtime.** Seed + fixed dt make runtime-driven state repeat;
  GPU-side animation and audio phase are outside it. Screenshot comparisons should frame
  runtime-driven subjects.
- **Restart must not leak** (B5). If pieces keep flying or a puffer keeps emitting after Restart,
  fix `Stop` in the runtime — do not paper over it in the lab; the crash respawn needs the fix.
- **The effect templates must stay invisible outside playback** — built into the lab/crash stage
  hidden by reset states, never into normal world rendering (`WorldBuilder` skips them by design).
- **`Bind` is not re-runnable state-free** — it indexes the world and runs bootstrap. Restart
  re-applies reset states + `Start`; it must NOT re-`Bind` (cheaper, and re-binding would re-run
  mission setup).
- **A drifting pad steers nothing here** (no flight controller), but keep `--no-pads` working in
  the mode anyway — scripted runs use it habitually.

## Key locations (as of 2026-07-23)

- `PlaneViewer.cs`: orbit fields :218-219, FrameCamera :1898-1912, pose update :1960-1961, input
  :2034-2080; paths :528-534; world-mode gate :422; world+anim build ~:515-695 (runtime collaborators
  :644-670, bind :678, prewarm :686-693, content root added :843); arg parsing ~:327-460; lab
  add-points (pattern) :813/:836/:851.
- `AnimRuntime.cs`: `Apply`/`Bind` :66/:77, `Bootstrap` :121 (pass 0 :133, pass 1 :139-148, pass 2
  :154-159, pass 3 :164-185, pass 4 :188), `_rng` :113, `_Process` :252, `Start` :277, `Stop` :320,
  `TickMotions` :1276.
- `AnimProgram`: `ArchivePaths`/`Load` call shape at PlaneViewer :631-639.
- Crash-stage data: see `PLAN-data-driven-crash.md` "Key locations".
