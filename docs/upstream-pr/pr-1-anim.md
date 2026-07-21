# Add Crimson Skies `cam_anim.zbd` / `mis_anim.zbd` support

*(PR title above; body below. Branch `pr-cs-anim`, 4 commits on `0.7.0-rc3`.)*

---

> **Disclosure:** this work was done with the help of Claude Code (Anthropic's agentic coding
> tool). The reverse engineering, implementation and the verification described below were
> AI-assisted; the commits carry a `Co-Authored-By: Claude` trailer. I've reviewed it and I'm
> putting my name to it, but you should weigh that when deciding how closely to read it — and
> I'm happy to walk through any part of the format reasoning myself if that helps review.

This fills the CS row of the anim support matrix, which currently reads "not supported yet".
`unzbd cs anim` / `rezbd cs anim` work end-to-end, and **all 61 `cam_anim.zbd` /
`mis_anim.zbd` archives of a retail install round-trip byte-identically** through the real zip
pipeline (`test.py` → `--- ALL OK ---`), which also exercises the JSON layer for every def and
every script.

The CS container turned out to be a fourth variant of the existing `anim` format rather than
anything new, so this is additive: no MW/PM/RC read/write path changes behaviour, and CS
delegates to the PM implementations wherever the layouts match (which is most of the time).

## What's here

Four commits, each round-trip-verified on the full install:

1. **Structural read/write.** The CS container: a 16-byte header
   (`signature`, version 53, `base_count`, `file_count`), a CS-only base-file list (the
   `gamez.zbd`/`planes.zbd` entries, 132-byte records), the anim-file list (common
   `AnimDefFileC` shape, but the counts live in the header), PM's `AnimInfoC` verbatim, every
   anim def with its support arrays and sequences, and the SI-script pool (PM's `SiScriptC`
   verbatim) — with event blobs and script frames kept as raw bytes at this stage.

   CS's `AnimDefC` is 272 bytes: PM's 268-byte layout plus one dword, with three structures PM
   reserves but never uses being live in CS (a 36-byte-record unknowns array, a `u32` index
   list whose count sits in PM's `zero227` and pointer in PM's `zero264`, and one extra
   unnamed sequence when `unknown_seq_ptr` is non-NULL). Static sound refs are 40 bytes
   (PM: 36); every other record matches PM's sizes.

2. **Semantic `AnimDef` + event decode.** Every field, support array and sequence event is
   decoded into the shared API types. A new `EventCs` trait dispatches, delegating to PM for
   all events whose payloads match. CS-specific: e12 `OBJECT_MOTION_SI_SCRIPT` (64 B, indexing
   the def's script-id list), CS-only e46 `SOUND_ADJUST` and e47 `OBJECT_MOTION_SI_SCRIPT`
   `ALL_NAMES` (payloads preserved raw — I have no reader-side source for these), extra
   `If`/`Elseif` conditions (`NODE_BELOW_ALT`, `ANIM_HEALTH` + range, `NODE_ACTIVE`), the
   `MAIN_ROOT_NODE` (−100) sentinel, and `INPUT_NODE` in e01/e42. Duplicate node names are
   disambiguated with a reversible `~N` suffix.

   Cross-validated beyond round-tripping: `hangar3_doors` was checked field-for-field against
   its `zrdr` JSON source.

3. **SI-script frame decode.** All **1090 scripts** across all 61 archives decode through the
   shared `si_script` frame machinery using PM's exact frame scheme. An earlier apparent
   "24 of 48 camera scripts fail to parse" was my own misreading of the record delimiting
   (`flags == 0` frames carry no data blocks), not a format difference. `spline_interp` is a
   real per-script bool in CS (false in 15 scripts).

   Rotate semantics, measured and documented for consumers: per-axis cubics in **half-angle
   radians relative to the frame's base quaternion**, composed in the parent frame as
   `q(t) = exp(v(t)) * base`, with `delta` the average rate — verified by L-exp closure on
   53,515 / 60,411 consecutive rotate pairs to < 1e-5. Splines stay raw bytes, matching the
   existing MW/PM choice; `pfighter11.zan` (C1/M04) carries uninitialized spline memory that
   would break any semantic float validation.

4. **CLI + `test.py` wiring.** `cs::read_anim`/`write_anim` were reshaped onto the same
   `SaveItem`/`LoadItem` callback shape MW/PM/RC use, and the common `ANIMATION_LIST` entry
   read/write was factored out for CS's header-counted list. `test.py` picks CS anim up via a
   `*_anim.zbd` glob; nothing else in the harness needed changing.

## New API surface

All optional and absent for MW/PM/RC:

- `AnimMetadata.base_files` and `AnimMetadata.ptrs`
  (`defs_ptr`/`scripts_ptr`/`world_ptr`/`unk40`/`zero_def_flags`) — CS container data with no
  existing slot. A 61-archive info-block survey argued **against** modelling this as a
  PM-style `Mission` pointer table (56 distinct `defs_ptr` values) and pinned every other
  field constant, so they're asserted the way PM asserts its own.
- `AnimDef.flags_raw` / `node_path` / `si_script_ids` / `unknown_seq`.
- Events `SoundAdjust` and `ObjectMotionSiScriptAllNames`.
- `TranslateData`/`RotateData`/`ScaleData.delta_raw` — see below.

## One wrinkle worth flagging: NaN in the data

`carneypkup_cam.zan;camera1` (C5/M02) contains a degenerate frame whose translate and rotate
`delta` vectors are six `0xFFC00000` NaNs. `serde_json` writes those as `null`, which then
fails to parse back — so the archive round-tripped in memory but not through the CLI.

A field-by-field survey showed these are the **only** non-finite decoded floats in the entire
install (bases, frame times and all other deltas are finite), so rather than change the
representation of `delta`, the three data types gained an optional bits-preserving
`delta_raw`, using the same idiom as the adjacent `garbage` field. The field is absent
whenever the value is finite, so MW/PM/RC output is unchanged.

## Verification

- `test.py <versions> <out> --release` → `--- ALL OK ---`: all 61 CS anim archives
  byte-identical through `unzbd cs anim` → `rezbd cs anim`; every other suite
  (sounds/interp/messages/reader/textures) unchanged; gamez correctly reports `SKIPPING` for
  CS on this branch.
- `cargo build --workspace` and `cargo test --workspace` clean (includes an in-memory 61/61
  round-trip driven by `CS_ANIM_DIR`).
- `cargo run -p mech3ax-metadata-gen` runs clean for both the C# and Python backends.

## Notes

- I don't have MW/PM/RC installs, so those suites are covered only by the unit tests plus the
  fact that no MW/PM/RC code path is modified. If you can run the full harness on your side,
  that would be the check I can't do.
- Happy to squash, reorder, or resplit these commits however suits review.
