# mech3ax fork — reviving Crimson Skies `gamez`/`planes` + adding `cam_anim`/`mis_anim`

Working plan for `tools/mech3ax` (the user's fork, cloned 2026-07-20 from
`git@github.com:Laeresh/mech3ax.git`, no `upstream` remote yet). Two format tracks, planned
together because they're both "the CS gap in mech3ax" from the outside, but they're
independent pieces of work with very different risk profiles — read the framing before the
checklist. Each item lists goal/evidence/approach/verify. Statuses: ☐ open · ◐ in progress ·
☑ done.

## Framing — what actually broke, and why the two tracks aren't symmetric

**`gamez.zbd`/`planes.zbd` (Track A) didn't get harder to extract — it got deleted.**
Commit `7f592ec` "GameZ/Nodes: remove CS support!" (2025-06-19) is the real removal (the
hash the user gave, `a8b0a18…c1cf`, is `main`'s HEAD-of-the-day "Changelog for v0.7.0-rc1"
commit — it just happens to *record* the removal in `CHANGELOG.md`, it isn't the removal
itself). `7f592ec` deleted every CS-specific file outright: `crates/gamez/src/gamez/cs/**`
(~4,500 lines, of which ~3,400 are the eight per-chapter texture fixup tables + `planes.rs` —
game-data facts, portable near-verbatim; the rest is `mod.rs`/`models.rs`/`nodes.rs`/`fixup.rs`
logic) and `crates/nodes/src/cs/**` + `crates/api-types/src/nodes/cs.rs` (~2,600 lines:
**seven** node kinds — camera/display/light/lod/object3d/window/**world** — plus the 435-line
shared `node.rs` dispatcher; `world/data.rs` at 625 lines is the single biggest deleted file).
It landed as the *first* commit of a ~50-commit run to current `HEAD`, whose first ~20 commits
reshaped the common gamez/nodes infrastructure RC/MW/PM now share ("GameZ: use same GameZ
struct for all games", node-code moves/splits, "new nodes" rewrites, model-array unification)
— CS was cut loose before that reshaping started, not because the format resisted it. This
means a straight revert won't apply: the common code CS's old module plugged into no longer
exists in the same shape. **Reviving it is a port**, from the pre-removal `cs/` module (fully
recoverable at `7f592ec~1`) onto the current common code — closest living relative is `pm/`
(CS's mesh code was historically *merged from* PM's — see `1e3b395`/`300ffde` — and `7f592ec`
itself renamed the previously shared `crates/gamez/src/model/ng/` to `model/pm/`, i.e. the
"ng" model code *was* the PM+CS common code; so the delta to port is smaller than starting
from `mw/` or `rc/`).

**`cam_anim.zbd`/`mis_anim.zbd` (Track B) never had support to lose.** `crates/anim/` fully
implements the compiled-anim-archive format for MW/PM/RC today (container header, def list,
per-def records, SI-script pool) — CS was simply never added as a fourth variant. Our own
byte-level survey (`docs/formats/anim-definitions.md`) found the **same signature**
(`0x08170616`) and adjacent version numbers (RC 28, MW 39, PM 50, our measured CS 53),
confirming this is one format family with CS as an unimplemented sibling, not a different
architecture to reverse-engineer from nothing. `crates/anim/src/{mw,pm,rc}/` is a working,
three-times-validated template for exactly the container/list/def/script shape we need.

**Upstream left unambiguous breadcrumbs that both are wanted back, not abandoned:**
- `crates/gamez/src/gamez/common.rs` still has `pub(crate) const VERSION_CS: u32 = 42;`,
  merely marked `#[expect(dead_code)]` — kept, not deleted.
- The CLI's `GameType`/`Game` enums still carry a full `CS` variant everywhere. The `gamez`
  command bails with *"Crimson Skies support for GameZ isn't implemented **any more**"*
  (removed, expected back); the `anim` command bails with *"isn't implemented **yet**"*
  (never existed, expected to be added). Upstream wrote these two messages differently on
  purpose.
- `test.py`'s round-trip harness still carries full `GAME_CS` detection (`name.endswith("-cs")`,
  sounds/messages/reader filename quirks all still branch on it) and `test_gamez`/`test_anim`
  each have a literal `if game == GAME_CS: print("SKIPPING", name); continue` — a stub built
  to be flipped on, not removed.

**Neither track is blocking today's extraction.** `tools/` pins mech3ax **v0.6.1** (released
2024-11-28, ~7 months before the removal), and that's what `ExtractAssets.ps1` actually runs —
`gamez.zbd`/`planes.zbd` extraction already works for this project right now, unaffected by
what upstream `main` did later. So this plan is about fork health and unlocking `cam_anim`/
`mis_anim` (which v0.6.1 never had either), not an emergency fix. That changes the priority
call: **Track B first** — it delivers new capability this project is actually blocked on
(Run-2 item 9, "animated world vehicles", deferred 2026-07-18 specifically for this reason —
see `backlog.md`), it's additive against a proven template, and it's a smaller, cleaner first
PR to cut our teeth on the fork/upstream workflow before attempting the much bigger gamez
port. Track A matters for fork completeness and the eventual upstream PR (CLAUDE.md's stated
division of labor: Claude does the RE/Rust, the user owns upstream communication), but nothing
in this project is waiting on it.

Ground rules: every new struct field either gets a semantic name or, if genuinely unknown,
round-trips via mech3ax's existing `garbage`/`chk!` preservation pattern (see
`crates/anim/src/common/anim_list.rs` for the idiom) — the project's correctness bar is
**byte-identical round-trip**, not full semantic understanding, so partial decodes are
legitimate landings as long as nothing is silently dropped. New decode knowledge lands in
`docs/formats/anim-definitions.md` (Track B) in the same change; Track A doesn't get a new
format page (`gamez.md` already documents the JSON shape mech3ax produces — the extraction
*output* doesn't change, only whether the fork can produce it).

## Checklist

**Track B — `cam_anim.zbd`/`mis_anim.zbd` (do first):**
1. ☑ Fork housekeeping — add `upstream` remote, confirm baseline `cargo build`/`cargo test`/`test.py` on `main` as-is (done 2026-07-20, see section 1)
2. ☑ Study `crates/anim/src/{mw,pm,rc}` as the porting template; write down what's shared (`common/`) vs per-game (done 2026-07-20, see section 2 — incl. byte-verification against the real C1 `cam_anim.zbd`)
3. ☑ Implement `crates/anim/src/cs/` container + def-list + info block, byte-region-correct (garbage-preserving) round-trip (done 2026-07-20, see section 3 — round-trip verified **byte-identical on all 61 archives**, exceeding the planned C1-only bar)
4. ☑ Decode the `AnimDef` record fields + op dispatch table against `docs/formats/anim-definitions.md`'s known reader-JSON schema (done 2026-07-21, see section 4 — full semantic decode into the shared API types, 61/61 byte-identical, `hangar3_doors` oracle-matched)
5. ☑ Decode the SI-script rotate block; resolve the 24/48-script camera/`cpilot_eject` parse-failure variant (done 2026-07-21, see section 5 — all 1090 scripts of all 61 archives frame-decode byte-exactly; the "failure variant" never existed, and the rotate cubics are half-angle offsets composed `exp(v)⊗base`)
6. ☑ Wire CLI (`unzbd`/`rezbd` `anim` command), README/CHANGELOG, reactivate `test.py`'s CS anim skip, verify byte-identical round-trip on the full install (done 2026-07-21, see section 6 — test.py `--- ALL OK ---`, all 61 archives byte-identical through the real zip pipeline)
7. ☐ Consume in this project — extraction wiring, `OBJECT_MOTION_SI_SCRIPT` playback (train/trucks/`cpilot_eject`), docs + backlog cleanup (**skipped for now by user decision 2026-07-21** — Track A started first; revisit after)

**Track A — `gamez.zbd`/`planes.zbd` (do second):**
8. ☑ Recover the deleted `cs/` module from `7f592ec~1` as porting reference (done 2026-07-21, see section 8 — worktree at `tools/mech3ax-cs-ref`, inventory verified, 13 wiring files outside `cs/` catalogued)
9. ☑ Diff the common gamez/nodes infra between `7f592ec~1` and current `HEAD` to scope the port (done 2026-07-21, see section 9 — the port targets a unified `GameZ`/`Node` API + relocated by-kind node modules; `TextureName` rename machinery obsolete)
10. ☐ Port `cs` gamez/nodes code onto the current `pm`-based architecture (mesh/model/node code, per-chapter texture fixup tables, reactivate `VERSION_CS`)
11. ☐ Wire CLI (`gamez_cs`, `planes` routing), README/CHANGELOG
12. ☐ Verify byte-identical round-trip against the real install (reactivate `test.py`'s CS gamez skip); stretch goal: fix the known 72-byte `planes.zbd` cosmetic diff
13. ☐ Cut this project's extraction pipeline from the pinned v0.6.1 binary to the fork build, once verified byte-identical
14. ☐ Prepare upstream PR(s), split by concern, coordinated with the user (who owns upstream communication)

---

## 1. Fork housekeeping

**Goal:** a known-good, up-to-date baseline to build both tracks on, and a way to pull
upstream's continued iteration (`main` is already past rc1 → rc3 since the CS removal) without
losing fork-local work.

**Approach:** `git remote add upstream https://github.com/TerranMechworks/mech3ax.git` in
`tools/mech3ax`; `cargo build --workspace`, `cargo test --workspace`, and a `test.py` dry run
against whatever non-CS installs are available (even without a CS-labeled directory, the
MW/PM/RC paths should run clean) to confirm the fork starts from a working state before any
CS-specific changes land.

**Verify:** clean build, clean test suite, `test.py` completes with `--- ALL OK ---` for every
non-CS version it's pointed at.

**DONE 2026-07-20.** `upstream` remote added; fork `main` was already exactly at
`upstream/main` (`cbb838f`, v0.7.0-rc3) — no sync needed. Toolchain: rustup present, repo pins
1.91.1 via `rust-toolchain.toml` (auto-installed). `cargo build --workspace` clean in 52 s
(15 pre-existing dead-code warnings in `mech3ax-gamez`, e.g. the unused `NODE_INDEX_*` masks —
more CS-removal leftovers). `cargo test --workspace`: **141 passed, 0 failed**. No MW/PM/RC
installs exist on this machine, so the non-CS dry run was replaced by a **CS-pointed** run:
`test.py` discovers installs as `<versions_dir>/<name>-cs/zbd/`, served by a junction
`tools/test-versions/crimson-cs/zbd` → `CrimsonSkiesGame/ZBD` plus a copy of the install
root's `strings.dll` next to it (the CS messages suite reads `zbd_dir.parent/strings.dll`).
Result: `--- ALL OK ---` — sounds/interp/messages/reader/textures all round-trip
byte-identical on current `main` (52 texture ZBDs across all 8 chapters + `rimage.zbd`,
both `zrdr.zbd` scopes, both sounds archives), motion/mechlib/zmap correctly skip CS, and
gamez/anim print the expected `SKIPPING crimson-cs`. This also pre-answers item 6's open
question: no `test.py` convention change is needed — the `-cs` junction works as-is. Test
output lives in `.scratch/mech3ax-test/` (regeneratable, git-ignored).

## 2. Study the anim crate template

**Goal:** a clear map of what `crates/anim/src/common/` already generalizes (container header
shape, `ANIMATION_LIST`, garbage-preserving name fields, SI-script save/load plumbing) versus
what each of `mw`/`pm`/`rc` reimplements per-game (`AnimInfoC` field layout, the `Mission`
enum + its magic-pointer table, `anim_def/read.rs`'s op dispatch) — this is what tells us
which parts of a `cs` module are "just add a fourth match arm" versus genuine new structs.

**Evidence already gathered (re-verified against the fork):** both `mw/anim/read.rs` and
`pm/anim/read.rs` confirm the read order (header → `read_anim_list` → `AnimInfoC` → per-def
records, first def a reserved `anim_def_zero` placeholder) matches our own byte-level CS
survey. The one apparent discrepancy is **already resolved in PM's favor**: our CS analysis
described the SI-script data as a single pool trailing *after* all `AnimDef` records, running
byte-exactly to EOF — MW differs (its `read_anim_defs` collects scripts inline per def into a
`Vec<SiScript>`), but **PM reads exactly the CS shape**: `read_anim_defs` → a separate
`read_anim_scripts(anim_info.script_count)` pool → `read.assert_end()`, with the script count
carried in its `AnimInfoC`. So `pm/anim/` is the primary template for the whole read path,
not just by mesh-code lineage.

**Verify:** a short internal note (can live as a comment block atop the new `cs` module, no
separate doc needed) listing which structs get a straight port vs a new CS-specific shape.

**DONE 2026-07-20 — the map, byte-verified against the real C1 `cam_anim.zbd`:**

*Reusable as-is (or near) from `common/` + `anim-events`:*
- `common/anim_list.rs` — `AnimDefFileC {Ascii<80>, u32 mtime}` is **exactly** CS's anim-file
  list entry. But CS's count lives in the 16-byte header (before the base list), not directly
  before the entries, so `read_anim_list` can't be called verbatim — reuse the entry
  struct + garbage idiom in a CS loop (or refactor the entry read out).
- `anim-events/src/si_script/` — `FrameC {flags,start,end}` (12 B) +
  `TranslateDataC`/`RotateDataC`/`ScaleDataC` (76 B each) **match CS byte-for-byte**
  (verified on `tr_passengine1`: flags=3 translate|rotate, start=0, end≈3.636 s — our
  survey's ~3.64 s). `RotateDataC` = base quaternion + delta `Vec3` + three `Bytes<16>`
  spline blocks — this **is** the survey's "quaternion + 15 floats presumed cubic", so
  item 5's rotate-block layout already exists upstream (splines kept as preserved bytes).
- `common/seq_def`, `common/activation_prereq`, `common/support`, `common/fixup.rs` — already
  parameterized per-game (`ReadEventsMw/Pm/Rc` hooks); CS adds a variant, not a rewrite.
- `common/si_script.rs` — the `SaveItem`/`LoadItem` plumbing writing each script as
  `si-script-NNN.zan`; game-agnostic.

*Per-game shapes CS needs its own versions of (PM the donor everywhere):*
- **Header**: CS is 16 bytes `{signature, version=53, base_count, anim_file_count}` —
  verified at offset 0: `16 06 17 08 | 35 | 02 | AA` (170 anim files in C1). MW's header is
  8 B, PM's 12 B (with timestamp) — CS's is its own small struct. Then the **CS-only base-file
  list**: `base_count × {Ascii<128>, u32 mtime}` (C1: `zbd\c1\gamez.zbd` + planes) — no
  equivalent in any other game, trivial new struct.
- **`AnimInfoC`**: **PM's 108-byte layout verbatim** — decoded C1's block at 0x38E0
  field-for-field: zeros where PM has zeros, `def_count=476` (u16 @10), `defs_ptr` @12,
  `script_count=48` @16 (exactly the survey's 48 SI scripts), `scripts_ptr` @20,
  `world_ptr` @32, `gravity=−9.8` (f32 @36), `unk40=1`, `one60=1`. Straight port.
- **`Mission` magic-pointer scheme**: PM keys 4 campaign archives to hardcoded
  `defs_ptr`/`scripts_ptr`/`world_ptr` values. CS has ~8 chapter `cam_anim.zbd` + one
  `mis_anim.zbd` per mission — an enum table needs an entry per archive (C1 cam_anim:
  defs 0x048F631C / scripts 0x048BB5DC / world 0x03B8000C), harvested across the install.
  **Open design decision for item 3**: per-archive table vs storing the raw pointers in
  `AnimMetadata` (byte-identical round-trip requires one of the two; the `Unknown→0xDEADBEEF`
  fallback would break it).
- **`AnimDefC`** (268 B in PM, name/root/flags/activation/health + seq/reset ptrs + support
  counts): CS def area starts right after the info block (0x394C in C1) with the shared
  `reserved_anim_0` zero-record convention ✓ — field-by-field CS verification is item 4.
- **Event dispatch**: `anim-events` implements per-game traits (`EventMw`/`EventPm`/`EventRc`)
  with a ~180-line tag→event match per game; many events are `EventAll` (shared). CS = a new
  `EventCs` trait + `cs/` dispatch. The `e01`–`e42` vocabulary already covers **every op our
  survey documented** (`e12` OBJECT_MOTION_SI_SCRIPT, `e20` CAMERA_STATE, `e36`/`e37` FBFX,
  `e42` PUFFER_STATE) — likely zero brand-new event types, only impl/size differences.
- **SI-script pool records**: the one real structural divergence. PM: 28-byte `SiScriptC`
  header (name ptrs/lens, `spline_interp`, `frame_count`, `script_data_len`) then names+data.
  CS (verified at 0x1ad667): `{path\0, object\0, frames…}` back-to-back with **inline
  nul-terminated strings, no header struct** — CS-specific record framing around upstream's
  existing frame structs. How the engine delimits records (count/length declared by the def's
  e12 event vs sentinel) is exactly item 5's remaining question, and likely explains the 24
  camera/`cpilot_eject` parse failures.

## 3. Container + def-list + info block

**Goal:** `crates/anim/src/cs/` parses a real `cam_anim.zbd`/`mis_anim.zbd` far enough to
correctly delimit every `AnimDef` record and the SI-script data, and round-trips those regions
byte-identically — even before individual `AnimDef` fields have semantic names. mech3ax's
`garbage`-preservation idiom (already used for the `AnimDefFileC` name field) means "unknown
but faithfully round-tripped" is a legitimate intermediate state, not a blocker.

**Evidence:** `docs/formats/anim-definitions.md`'s "Compiled anim archives" section has the
header (`signature`/`version 53`/`nBase 2`/`nAnimFiles`), the two fixed-size path/mtime lists
(`nBase` × `{path[128], mtime}` for the gamez.zbd/planes.zbd the archive was built against,
`nAnimFiles` × `{path[80], mtime}` for the `.zrd`/`.zan` sources), and the ~0x6c-byte anim-info
block (gravity, counts, runtime pointers) — cross-check this against **PM's `AnimInfoC`, which
is exactly 108 bytes = 0x6c** (MW's is 68, RC's 60), field-by-field; the size match plus PM's
trailing-script-pool read order (item 2) makes "CS info block = PM layout, possibly verbatim"
the working hypothesis. The counts/pointers are likely the same fields under the
`Mission`-style per-chapter magic-pointer scheme all three games use (`from_defs_ptr` tables),
which would need its own CS pointer table derived from this install's C1–C5 archives.

**Approach:** implement mirroring `pm/anim/{mod,read,write}.rs` (closest sibling by lineage),
substituting the CS header/list shape above; get `read_anim` to consume every byte to EOF with
`read.assert_end()?` succeeding, even with `AnimDef` internals still opaque placeholders.

**Verify:** `read_anim`/`write_anim` round-trip is byte-identical on C1's `cam_anim.zbd` before
moving on to field semantics.

**DONE 2026-07-20.** Two-step landing: first a Python structural model
(`.scratch/cs_anim_defwalk.py` in the main repo) was iterated against the real archives until
it delimited every def/seq/script in **all 61** `cam_anim.zbd`/`mis_anim.zbd` of the install,
landing byte-exactly at EOF — this solved the complete container layout (all deltas recorded
in `docs/formats/anim-definitions.md`: `AnimDefC` 272 B = PM + a u32-list ptr @264 with its
count in PM's `zero227`; static-sound refs 40 B vs PM 36; the live `unknowns` array (36-B
records); the extra unnamed sequence when `unknown_seq_ptr` ≠ 0; per-record sizes
obj 92/node 44/light 44/puffer 44/dsnd 44/animref 72 all PM's). Then the Rust module
`crates/anim/src/cs/mod.rs` (structural stage: typed C-structs for every fixed record,
event blobs + script names/frames preserved as raw bytes) with `read_anim_raw`/
`write_anim_raw` and a `roundtrip_real_archives` test (env `CS_ANIM_DIR` → recursively
round-trips every `*_anim.zbd`; skips when unset so CI without game data stays green):
**61/61 byte-identical**, `cargo clippy` clean on the module. One preservation lesson: the
`reserved_anim_0` zero-def varies per archive (C1/M05 carries flag bit 21), so it is stored
verbatim, not synthesized. Not yet done here by design: semantic field decode + event
dispatch (item 4), script-frame interpretation (item 5), CLI/SaveItem wiring (item 6) — the
structural stage is the byte-region-correct skeleton those fill in. Open question carried to
item 4: whether the `unknowns` array precedes or follows the objects array (the walk only
sums sizes, so both fit; needs a def where name anchors disambiguate, e.g. via the
`mp1zrprop11` family).

## 4. `AnimDef` record fields + op dispatch

**Goal:** every `ANIMATION_DEFINITION` field and state/playback op our own zrdr-JSON survey
already named (`docs/formats/anim-definitions.md`'s field tables: `NAME`, `ACTIVATION`,
`RESET_STATE`, `SEQUENCE_DEFINITION`, `OBJECT_ACTIVE_STATE`, `OBJECT_MOTION_SI_SCRIPT`, etc.)
gets a binary encoding in the compiled `AnimDef` C-struct.

**Approach:** the compiled def is presumably a direct binary encoding of the same AST the zrdr
JSON already exposes in full — use `mw`/`pm`/`rc`'s `anim_def/read.rs` op-tag/payload
dispatch as the Rosetta stone for *how* mech3ax already represents "a tagged op list" in a
binary reader (enum discriminant shape, payload sizing), then match CS's actual op vocabulary
(a superset — CS's docs list ops like `PUFFER_STATE`/`CAMERA_STATE`/`FBFX_COLOR_FROM_TO` that
may not appear in every other game) against whichever existing game's op set is the closest
match, extending rather than reinventing. Cross-validate every decoded def against the
already-fully-understood zrdr JSON for the *same* anim name (e.g. C1's `hangar3_doors` exists
both compiled in `mis_anim.zbd` and as source in `startanims.json`/`hangar3.json`) — this
gives a ground-truth oracle no other mech3ax game format has.

**Verify:** a decoded `AnimDef` for a known def (e.g. `hangar3_doors`) matches the zrdr JSON's
`RESET_STATE`/`SEQUENCE_DEFINITION` semantically field-for-field; full C1 `mis_anim.zbd` +
`cam_anim.zbd` round-trip byte-identical with every def field now named (no remaining
opaque/garbage regions inside `AnimDef` records — the outer garbage-preservation escape hatch
from item 3 should no longer be needed here).

**DONE 2026-07-21** (fork commit `60a0603`, exceeding the planned C1-only bar: **all 61
archives round-trip byte-identically** with the full semantic decode). The def/support/event
model now goes through mech3ax's shared API types exactly like MW/PM/RC: `cs/anim_def.rs`
(the 272-byte struct with Maybe-typed flags/activation, PM-style asserts adjusted to CS
data), `cs/support.rs` (typed support arrays), and a new `EventCs` trait in `anim-events`
whose impls **delegate to `EventPm` wherever the payload layout is PM's** — which the survey
proved is every shared event type (all 32 shared types match PM's payload size exactly).
Survey scripts: `.scratch/cs_anim_survey.py` / `_survey2.py` in the main repo.

Key decode results (full detail in `docs/formats/anim-definitions.md`):
- The item-3 "unknowns array" is the compiled **NAME1 node path** ({multiplayer1zep, reng11}
  for `mp1zrprop11` — matching `multi1_zep_nacelles.json`'s `NAME1` exactly); it precedes
  the objects array. The u32 list is the def's **SI-script pool indices**, and the CS e12
  event (64 B, not PM's 24) references `{node_index, script_slot}` into it — which
  pre-answers item 5's delimiting question.
- Two event types beyond the known MW/PM/RC vocabulary, both named via the reader oracle:
  **e46 = SOUND_ADJUST** (fade series; `hdplayer*`) and **e47 = OBJECT_MOTION_SI_SCRIPT in
  its ROOT/ALL_NAMES form** (skeletal person anims; count × 76-byte records). Both are
  preserved as raw payloads (new API variants) — field decode is item-5-adjacent.
- CS `IF`/`ELSEIF` conditions extend PM's set: NODE_BELOW_ALT (0x100), ANIM_HEALTH (0x800),
  two-value ANIM_HEALTH (0x1000, uses PM's asserted-zero dword), NODE_ACTIVE (0x2000);
  `NODE_NEAR_GROUND` = NODE_UNDERCOVER with negated value. New node sentinel
  **MAIN_ROOT_NODE = −100** (alongside INPUT_NODE −200, which CS also uses in
  SOUND/PUFFER_STATE AT_NODE).
- Flag bits pinned by the oracle: bit 5 = "RESET_TIME key present" (not value ≠ −1), 12/13 =
  SAVE_LOG SET/ON; bit 1 = EXECUTION_BY_RANGE proven iff; 8 CS-only bits remain unnamed, so
  the raw dword is stored (`flags_raw`) and is authoritative on write.
- Corrections to item-3 notes: activation prerequisites ARE used (object `active` ∈ {0,1,2},
  real pointers, `min_to_satisfy` up to the count); static sounds are one 40-byte
  garbage-padded name field (the trailing bytes are not always zero).
- CS data quirks handled: duplicate node names within a def (disambiguated with a reversible
  `~N` suffix — events reference nodes by index, and C1/M02 references the *second*
  `pzep_interior`), stale pointers/`wait_for` values, `reserved_anim_0` with per-archive
  flags, rotations beyond ±180°, negative light ranges, several relaxed PM data bounds.

**Verified:** `cargo test` with `CS_ANIM_DIR` = 61/61 byte-identical; full workspace test
suite green (MW/PM/RC untouched paths); clippy no new warnings vs baseline; decoded
`hangar3_doors` matches `C1/zrdr/hangar3.json` field-for-field (activation, SAVE_LOG, all 5
RESET_STATE ops, all 4 sequences incl. duplicate sequence names and the ±50/±25 door
motions), plus a `dump_anim_def` debug test (env `CS_ANIM_DUMP=<path>;<name>`) for future
oracle checks.

## 5. SI-script rotate block + the camera/`cpilot_eject` gap

**Goal:** close the two known gaps from the 2026-07-18 survey: the rotate block's remaining 15
floats (translate's `value + c1·t + c2·t² + c3·t³` scheme is verified; rotate is presumed the
same but unconfirmed), and the 24-of-48 C1 scripts that fail to parse byte-exactly (confined
to camera/`cpilot_eject` scripts — they stop early on padding or run slightly past, "likely an
extra sub-record").

**Approach:** item 4's def-record decode should make this tractable rather than a byte-guessing
exercise — once a sequence's `OBJECT_MOTION_SI_SCRIPT` op is fully decoded, it will state
*which* script index/length it expects, which turns "why does this script parse wrong" into "does
the declared shape match what's actually there," a much smaller search. All 4 train scripts +
both fueltrucks (every *vehicle* motion, which is what item 7 actually needs) already parse
byte-exactly — this item is about reaching 48/48, not about unblocking vehicle playback, so it
can slip without blocking item 7 if the camera/eject variant turns out to be a genuine rabbit
hole.

**Verify:** all 48 of C1's SI scripts parse byte-exactly to the next record; full-archive
round-trip byte-identical.

**DONE 2026-07-21** (fork commit `916c3d2`, exceeding the planned C1-only bar again:
**all 1090 scripts across all 61 archives frame-decode byte-exactly**, and the semantic
round-trip stays byte-identical on all 61). Both "known gaps" dissolved on measurement:

- **The camera/`cpilot_eject` parse-failure variant never existed.** With the
  `SiScriptC`-declared `frame_count`/`script_data_len` (item 3's delimiting), the plain
  flags-driven frame walk — `{flags, start, end}` + one 76-byte block per set flag —
  consumes every script's data exactly. The 2026-07-18 failures were the sentinel-guessing
  walker tripping over **flags=0 frames** (8,596 of the install's 76,845 frames are a bare
  12-byte header with no data blocks). No extra sub-record, no second format.
- **The rotate block is decoded, beyond the planned bar** (upstream itself never
  interpreted the spline cubics): the three per-axis `{value, c1, c2, c3}` blocks are
  cubics in **half-angle radians relative to the frame's base quaternion** (constant term
  0, vs translate's absolute cubics whose constant = the base component), `delta` = the
  cubic's average rate, and composition is **left-multiplication in the parent frame**:
  `q(t) = exp((fx,fy,fz)(t)) ⊗ base`. Hypothesis race across every consecutive
  rotate-frame pair of the install (60,411 pairs): L-exp closes 53,515 pairs to <1e-5 /
  57,675 to <1e-3; right-multiplication and all six Euler orders are decisively worse.
  Residuals: cubic fit error on fast rotations (ladder-climb scripts, ~1° per 60 ms
  frame) and `pfighter11.zan` (C1/M04), which carries **uninitialized spline memory**
  (coefficients up to 1.7e+27) while its per-frame base quaternions stay smooth — bases
  are authoritative, splines only interpolate within a frame, and this is why the spline
  blocks must remain raw bytes for byte-identical round-trip (also upstream's MW/PM
  choice; the semantics live in the docs + module comment for item 7's playback).

Implementation: `cs/mod.rs` dropped `SiScriptRaw` for the shared API `SiScript` type
(names + `read_si_script_frames`/`write_si_script_frames`, PM's exact machinery;
`SiScriptCsC.spline_interp` became `Bool32` — a real per-script bool in CS, 15 scripts
false). Name fields verified install-wide as exactly `strlen+1`, so the PM-style
write reconstructs them byte-identically. Survey scripts: `.scratch/cs_anim_siprobe*.py`
(1–5) in the main repo. Verified: 61/61 byte-identical with `CS_ANIM_DIR`; full
workspace suite green; clippy clean on the crate (remaining warnings are pre-existing in
`mech3ax-api-types`); docs updated (`docs/formats/anim-definitions.md` SI-script pool +
validation sections).

## 6. CLI wiring + verification

**Goal:** `unzbd cs anim`/`rezbd cs anim` work end-to-end; the project's existing correctness
bar (byte-identical round-trip, per CLAUDE.md's format-support table methodology) is met
across the whole install, not just C1.

**Approach:** replace the `GameType::CS => bail!(...)` arm in `crates/unzbd/src/commands.rs`'s
`anim()` and the equivalent in `rezbd` with real `cs::read_anim`/`cs::write_anim` calls
(mirroring the `MW`/`PM`/`RC` arms exactly); flip `test.py`'s `test_anim` CS skip
(`if game == GAME_CS: … continue`) into a real run; update `README.md`'s support matrix row
(`anim.zbd`/`cam_anim.zbd`/`mis_anim.zbd`: CS ❌→✅) and `CHANGELOG.md`.

**Verify:** `test.py` reports `--- ALL OK ---` for every chapter's `cam_anim.zbd` and every
mission's `mis_anim.zbd` in the real install (answered in item 1: the
`tools/test-versions/crimson-cs/zbd` junction + `strings.dll` copy satisfies `name_to_game`'s
`-cs` convention as-is — no `test.py` change needed).

**DONE 2026-07-21** (fork commit `946b79d`). The CLI arms turned out to require an API
refactor first: `cs::read_anim`/`write_anim` had kept item 3's whole-archive in-memory
struct, while the CLI's `anim()` builds on the `SaveItem`/`LoadItem` callback shape all
three other games share — so the CS module was reshaped to match PM exactly (per-def and
per-script callbacks, `AnimMetadata` in/out; the common `ANIMATION_LIST` entry read/write
factored out for CS's header-counted list). Where the CS container carries data
`AnimMetadata` had no slot for, new **optional CS-only metadata fields** follow item 4's
API precedent: `base_files` (the two gamez/planes.zbd entries) and `ptrs`
(`defs_ptr`/`scripts_ptr`/`world_ptr`/`unk40`/`zero_def_flags`) — a 61-archive info-block
survey (`.scratch/cs_anim_info_probe.py`) settled the item-2 design question **against** a
PM-style `Mission` pointer table (56 distinct `defs_ptr` values) and pinned every other
field constant for PM-style asserts (`unk40` is 0 for all 13 multiplayer missions, 1
otherwise; `script_count` may be 0, which PM's asserts forbid). `test.py` runs CS via a
`*_anim.zbd` glob (its per-folder name mangling needed nothing else); README support
matrix + CHANGELOG updated; the item-4 event types and the new `AnimPtrs` registered in
`metadata-gen` (item 4 had skipped registration).

One real bug surfaced only at this item's JSON layer, which the in-memory round-trip test
can't see: `carneypkup_cam.zan;camera1` (C5/M02) contains a degenerate frame whose
translate+rotate `delta` vectors are six `0xFFC00000` NaNs — serde_json writes NaN as
`null`, which fails to parse back. A field-by-field survey (`.scratch/cs_anim_nanprobe.py`)
showed these are the **only** non-finite decoded floats in the whole install (bases,
frame times, and all other deltas are finite), so `TranslateData`/`RotateData`/`ScaleData`
gained an optional `delta_raw` bits-preserving field (the same idiom as the adjacent
`garbage` field, which stores a file f32 as u32 for exactly this reason); MW/PM/RC output
is unchanged (field absent when finite).

**Verified:** `test.py` `--- ALL OK ---` on the full install — all 61 archives
byte-identical through the real `unzbd cs anim` → `rezbd cs anim` zip pipeline (which
also exercises the JSON serialization layer for every def and script), all other suites
(sounds/interp/messages/reader/textures, 52 texture ZBDs) unchanged; full workspace
`cargo test` green incl. the in-memory 61/61 round-trip; `cargo clippy` clean on every
touched crate (anim, anim-events, api-types, unzbd, rezbd; remaining warnings
pre-existing).

## 7. Consume in this project

**Goal:** land the actual payoff — Run-2 item 9 ("animated world vehicles"), deferred
2026-07-18 for exactly this reason.

**Approach:** extend `ExtractAssets.ps1` with the new `cam_anim`/`mis_anim` mode (mirrors the
existing per-ZBD-type dispatch); a new `src/Mech3/AnimArchive.cs`-equivalent reader (or extend
`AnimDefs.cs` if the compiled and zrdr-source shapes converge enough to share a model, per
item 4's cross-validation); `MissionState.cs` gains playback for `OBJECT_MOTION_SI_SCRIPT`
(the train's `.zan` scripts — 4 cars × 90 frames × ~3.64 s per our survey), `OBJECT_MOTION`
(continuous prop-style spin, if still needed after item 4 — cross-check against what's already
covered), and whatever else `docs/formats/anim-definitions.md`'s "seen and deferred" op list
(`CALL_ANIMATION`/`STOP_ANIMATION`/`PUFFER_STATE`/etc.) turns out to matter for at-rest world
fidelity. Move `docs/formats/anim-definitions.md`'s "surveyed, extraction deferred" section to
a validated decode; update CLAUDE.md's format-support table; delete the backlog.md "Animated
world vehicles" entry.

**Verify:** C1/IA1's train visibly runs its track loop in `--fly`; a scripted flight near the
train's parked/moving position confirms motion matches the ~327 s loop period from the survey;
regression-clean `--fly`/`--stunt` smoke across all 8 chapters (a bad def decode could
mis-position or hide objects that were previously fine via the RESET_STATE-only path).

## 8. Recover the deleted `cs/` module

**Goal:** the pre-removal CS gamez/nodes code on disk as a porting reference, without trying
to make it compile against current `HEAD` (it won't — the common infra it called has changed
underneath it).

**Approach:** `git show 7f592ec~1:crates/gamez/src/gamez/cs/mod.rs` (and siblings) into a
scratch location, or `git worktree add` a checkout of `7f592ec~1` for side-by-side reading in
an editor. Recovers: `cs/mod.rs` (229 lines, top-level read/write), `cs/models.rs` (187),
`cs/nodes.rs` (208), `cs/fixup.rs` (273, the mesh-index hacks — "Hacky fixups for mesh indices
in CS c4 gamez and planes"), `cs/data/mod.rs` (152) + the eight per-chapter
`cs/data/texture/c1.rs`…`c5.rs` tables and `planes.rs` (~3,400 lines of game-data facts,
likely portable near-verbatim), and `nodes/src/cs/**` (~2,500 lines): the **seven** node kinds
`{camera,display,light,lod,object3d,window,world}/*` — `world/data.rs` alone is 625 lines,
the largest deleted file — plus the 435-line `node.rs` dispatcher, and
`api-types/src/nodes/cs.rs` (105).

**Verify:** the recovered tree is readable/diffable side-by-side with current `pm/` in an
editor or via `diff -u`; no attempt made to build it standalone.

**DONE 2026-07-21.** Recovered as a detached `git worktree` at **`tools/mech3ax-cs-ref`**
(commit `0e8707b` = `7f592ec~1`, "GameZ: derive hardware_render flag" — the last commit CS
compiled against). Being a worktree of the fork repo, it costs no second clone, stays
read-only reference (never build/commit there), and `git worktree remove ../mech3ax-cs-ref`
disposes of it when Track A lands. Inventory verified against the plan's framing numbers,
all exact: `crates/gamez/src/gamez/cs/` mod 229 / models 187 / nodes 208 / fixup 273 /
data/mod 152 + the eight per-chapter texture tables + planes.rs (4,040 lines with mods);
`crates/nodes/src/cs/` 2,509 lines — seven node kinds incl. the 625-line `world/data.rs`
and the 435-line `node.rs` dispatcher; `crates/api-types/src/nodes/cs.rs` 105.

Beyond the `cs/` directories, `7f592ec`'s full diffstat (63 files, −7,968/+34) catalogues
**13 wiring files the port must also restore or re-plumb**, each a small targeted diff
(`git show 7f592ec -- <file>` is the reference): `api-types/src/gamez/mod.rs` (−23:
`GameZDataCs`/`GameZMetadataCs` variants), `api-types/src/nodes/mod.rs` (−1: cs module),
`gamez/common.rs` (−29: CS masks/consts, `VERSION_CS` kept dead), `gamez/mod.rs` (−1),
`nodes/src/flags.rs` (−31: CS node flags), `nodes/src/lib.rs` (−1), `nodes/src/math.rs`
(−16), `lib/src/{read,write}.rs` (−8/−9: public API entry points), `metadata-gen` (−14),
`unzbd`/`rezbd` `commands.rs` (−20/−28: the bail arms), `test.py` (−61: the CS gamez run),
README/CHANGELOG. Also in the same commit: `model/ng/` → `model/pm/` and `textures/ng.rs`
→ `pm.rs` renames (confirming "ng" *was* the PM+CS common code) — old CS code imports
`textures::ng`/`model::ng`, which map 1:1 onto today's `pm` modules.

Sanity diff done: `diff -u` old `cs/mod.rs` vs current `pm/mod.rs` reads cleanly, and
current `pm/mod.rs` still uses the same `data::Campaign` shape old CS's `data/mod.rs`
mirrored — the PM-as-donor hypothesis holds at the top level. Scoping the common-infra
delta is item 9.

## 9. Scope the port

**Goal:** a concrete list of exactly what changed in the common gamez/nodes code between
`7f592ec~1` (CS's last living environment) and current `HEAD`, so the port is targeted rather
than a blind reimplementation.

**Approach:** `git log --oneline 7f592ec~1..HEAD -- crates/gamez/src/gamez/common.rs
crates/gamez/src/gamez/pm crates/nodes/src/pm crates/nodes/src/common.rs
crates/nodes/src/types.rs` and read each commit's diff — of the ~50 commits between
`7f592ec` and current `HEAD`, the gamez/nodes-touching subset (`4aa5dd1` metadata unification,
`01353a4` poly flags, `7b41ee4`/`27f4f36` node-code moves/splits, `66c974c`/`9fd2f47` MW/RC
"new nodes", `f850598` camera/display/object3d/window cleanup, `2bca2d6` mechlib new nodes,
`04da5ce` shared `GameZ` struct, `b3cee34` node_count semantics, `599f120` TODO cleanup, etc.
— all verified present) is the working list; each one either (a) changed something CS's
old code also touched, requiring a real adaptation decision, or (b) is RC/MW/PM-only and
doesn't affect the port. Cross-reference against `crates/gamez/src/gamez/pm/mod.rs`'s current
shape specifically, since that's the module the port targets.

**Verify:** a written list (comment block or scratch note, not necessarily a docs page) of
"common-infra changes CS's port must account for," used to drive item 10.

**DONE 2026-07-21.** ~29 commits touch the relevant infra between `7f592ec~1` and `main`.
The list, ordered by how much they change the port (not by date):

1. **One `GameZ` struct + one `Node` type for all games** (`04da5ce`, building on `4aa5dd1`).
   `api-types/gamez/mod.rs` now has a single `GameZ {textures, materials, models, nodes,
   metadata}` + `GameZMetadata {datetime, material/model/node_array_size, node_last_free}`,
   and `api-types/gamez/nodes.rs` a single `Node` struct (per-game-only fields annotated
   `// PM` etc.) with a `NodeData` sum over Camera/Display/Empty/Light/Lod/Object3d/Window/
   World. **Do NOT revive `GameZDataCs`/`GameZMetadataCs`/`NodeCs`/`api-types/nodes/cs.rs`** —
   the port maps CS onto the unified types, adding `// CS` fields where CS genuinely
   diverges (the old CS `node.rs` is 435 lines and its `world/data.rs` 625 — the field-set
   comparison against unified `Node`/`World` is item 10's first real task, and the main
   place "port" ≠ "copy").
2. **Node code moved crates and is now organized by kind, not by game** (`7b41ee4`,
   `27f4f36`, `66c974c`, `9fd2f47`, `f850598`, `2bca2d6`): gamez node read/write lives in
   `crates/gamez/src/nodes/<kind>/`, where camera/display/window/object3d are **game-shared**
   (single read/write) and light/lod/world/node-dispatch have `{mw,pm,rc}` submodules. CS
   adds `cs/` submodules to the per-game kinds + a `gamez/pm→cs`-style `nodes/{read,write}.rs`
   pair under `gamez/cs/`; for the shared kinds, check CS's old
   camera(190)/display/window/object3d code against the shared readers before writing
   anything new. The standalone `crates/nodes` crate still exists but now serves **mechlib
   only** (mw/pm wrappers) — the CS gamez port shouldn't touch it.
3. **Materials reference textures by index, not name** (`4df8963` + the unified API):
   `TexturedMaterial.texture_index: IndexR` replaced name resolution, so CS's whole
   `TextureName {original, renamed}` dedupe/redupe machinery (`dedupe_texture_names`/
   `redupe_texture_names` in old `cs/mod.rs`) is architecturally obsolete — duplicate
   texture names no longer break material resolution. `mech3ax_common::Rename` still
   exists if needed. **Downstream flag for item 13:** v0.6.1's extraction JSON carries the
   renamed (`.-N`-suffixed) texture names and our Godot `TextureArchive` compensates —
   the fork's output will differ here by design, so the cutover diff must treat that as an
   intentional improvement, not a regression.
4. **Index/count newtypes + `chk!`/`len!` idiom** (`c6ae212`, `4df8963`): raw `i32`/`u32`
   counts became `Count`/`Count32`/`IndexO`/`IndexR` with checked conversions;
   `assert_that!`/`assert_len!` largely became `chk!(offset, …)`/`len!(…)` (Track B's item 4
   already worked in this idiom). Mechanical but pervasive across every ported file.
5. **API types are declared via `api!`/`sum!`/`bit!`/`num!` macro_rules** (`89a46e3` …
   `16eb6b5`, replacing the proc-macro derives the old CS code used) and every new type
   registers in `crates/metadata-gen` (learned the hard way in Track B item 6). Old CS
   `#[derive(… Struct)]`/`#[dotnet(…)]` declarations translate 1:1 but must be rewritten.
6. **Header slot 32 semantics** (`b3cee34`): PM's header field at offset 32 is now
   `node_last_free` (was misread as node_count) and lives in `GameZMetadata`. CS's header
   has `light_index` in that slot (old `HeaderCsC`) — a genuine CS divergence to keep,
   but its round-trip (recomputed from the light node on write, hardcoded 2338 for planes)
   should be re-examined against the new metadata idiom.
7. **Model/materials internals moved on** (`01353a4` PM poly flag `unk6`/in-out, `5eefe90`
   PM mechlib material-ref split, `3db51d2` mip validation, `d1551a4`): current `model/pm/`
   IS the old `model/ng/` (renamed by `7f592ec` itself — old CS imports `textures::ng`/
   `model::ng` map 1:1 onto today's `pm` modules), but it has evolved since; `cs/fixup.rs`'s
   mesh-index hacks and the non-sequential mesh read (`4896f52`) must be re-validated
   against the new `models::read_models(read, nodes_offset, material_count)` signature
   rather than assumed portable.
8. **Style bar**: edition 2024 (`130d38e`, `a63d54a` fmt) + clippy (`2c5a61c`) — cosmetic,
   but the ported code should be written in the current idiom from the start (Track B did).

CLI/plumbing (small, known shapes): `lib/src/{read,write}.rs` lost their CS gamez FFI arms
(−8/−9 lines), `unzbd`/`rezbd` `gamez()` has the literal bail arm to replace with a
`gamez_cs` following `gamez_pm` (item 11), `test.py` keeps its flippable CS gamez skip
(item 12). Old-CS read-flow facts worth keeping in view for item 10: `Fixup::read(&header)`
selects per-campaign mesh-index hacks and detects planes.zbd (write side keys off timestamp
`967277477`); `nodes::read_nodes` takes `light_index`, `&models` and an `is_gamez` flag —
none of which the PM template passes.

## 10. Port `cs` onto current `pm`-based architecture

**Goal:** `crates/gamez/src/gamez/cs/` and `crates/nodes/src/cs/` exist again, building
against current `HEAD`'s common infra, producing the same JSON shape (`metadata`/`textures`/
`materials`/`models`/`nodes`) the other three games already do.

**Approach:** start from current `pm/` (the closest living relative) and layer in the CS
deltas recovered in item 8: the per-chapter texture-index fixup tables (near-verbatim data
port), `cs/fixup.rs`'s mesh-index hacks (re-validate against the new struct shapes — item 9's
list says where these are likely to need real changes, not just copy-paste), CS's own node-kind
set (camera/display/light/lod/object3d/window — check against `pm`'s current node-kind list
for overlap before assuming all six need bespoke code), and non-sequential mesh reading
(`4896f52`'s "Read CS gamez meshes non-sequentially" — confirm this quirk still needs special
handling or was subsumed by the general refactor). Reactivate `VERSION_CS = 42` (drop
`#[expect(dead_code)]`) as the live version-dispatch value once the read path exists.

**Verify:** compiles clean against current `HEAD`; `cargo clippy` clean (the codebase clearly
holds this bar — `2c5a61c "Clippy lints"` is in the recent history).

## 11. CLI wiring

**Goal:** `unzbd cs gamez`/`rezbd cs gamez` work, and `planes.zbd` routes through the same code
path (per this project's own note: "it's a GameZ-format file... the boot script loads it via
`GameZReadZBDFile`" — confirm the CLI still special-cases `planes` the way the pre-removal
README did, "For `planes.zbd`, please use the `gamez` mode," or whether that routing was also
stripped and needs restoring).

**Approach:** replace `GameType::CS => bail!("Crimson Skies support for GameZ isn't
implemented any more")` in `crates/unzbd/src/commands.rs::gamez()` (and the `rezbd` mirror)
with a `gamez_cs` function following the `gamez_pm`/`gamez_rc` pattern exactly; restore the
README's dropped `planes.zbd` row/footnote.

**Verify:** `unzbd cs gamez <chapter>/gamez.zbd out.zip` and `unzbd cs gamez planes.zbd
out.zip` both succeed on the real install.

## 12. Verify against the real install

**Goal:** meet or beat the correctness bar this project already documented under the old
v0.6.1 code (gamez.zbd C1+C5 byte-identical; planes.zbd 72-byte cosmetic diff in swapped
`\0`/`.` padding past the null terminator in fixed-width texture-name fields).

**Approach:** flip `test.py`'s `test_gamez` CS skip into a real run across every chapter's
`gamez.zbd` plus `planes.zbd`. As a stretch goal — now that this code is back under active
development anyway — chase the known 72-byte planes.zbd diff (CLAUDE.md already flags it as
an "upstream PR candidate"; it's cheap to fix in the same pass that's already touching this
exact code).

**Verify:** `test.py`'s `--- ALL OK ---` across the full CS corpus (all chapters' `gamez.zbd`
+ `planes.zbd`); ideally zero-byte diff everywhere, including the historical planes.zbd nit.

## 13. Cut the extraction pipeline over

**Goal:** stop depending on the pinned, unmaintained v0.6.1 binary for gamez/planes once the
fork is proven equivalent-or-better.

**Approach:** build the fork's `unzbd`/`rezbd` release binaries, run them side-by-side with the
pinned v0.6.1 ones across the full install, diff every output byte-for-byte before switching
`tools/` or `ExtractAssets.ps1` over. Keep the pinned binary as a fallback until this is fully
confirmed — this is the one step in the whole plan with a real regression risk to *this
project's own working extraction*, so it's the one to be conservative about, not any of the
mech3ax-side work above.

**Verify:** fork-produced `extracted/` tree is byte-identical to the current pinned-binary
tree (or an intentional, documented improvement, e.g. the planes.zbd fix) across every chapter;
`CrimsonSkies` viewer smoke-tests clean against the fork's output before `tools/` is updated.

## 14. Upstream PR(s)

**Goal:** contribute the work back, per CLAUDE.md's stated division of labor (Claude does the
RE/Rust, the user owns upstream/community communication) and stated strategy ("PR'd upstream to
TerranMechworks").

**Approach:** split into separately-reviewable PRs rather than one large diff against an
actively-iterating upstream (already at rc3 since the CS removal): (1) Track B's `cam_anim`/
`mis_anim` support — clean, additive, no conflict with upstream's own direction since it's a
pure "isn't implemented yet" gap; (2) Track A's `gamez`/`planes` CS revival — larger, touches
code upstream deliberately reworked, more likely to need discussion about whether/how upstream
wants CS folded back into the unified architecture; (3) the planes.zbd 72-byte cosmetic fix,
if not already folded into (2), since it's small enough to stand alone. Cheap early step
worth doing before investing heavily in Track A specifically: ask upstream (issue/discussion)
whether the CS removal was "not enough bandwidth to maintain a fourth game through the
refactor" (PR likely welcome) or a deeper architectural call that CS doesn't belong in the
shared abstractions (PR likely to need real back-and-forth, or the fork may end up staying a
permanent fork) — costs nothing, and changes how much polish to invest before opening (2).

**Verify:** N/A — this is a communication/process step, owned by the user per the project's
existing division of labor.
