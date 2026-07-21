# Revive Crimson Skies `gamez.zbd` / `planes.zbd` support, ported onto the unified API

*(PR title above; body below. Branch `pr-cs-gamez`, 1 commit on `0.7.0-rc3`. Independent of
the `cam_anim`/`mis_anim` PR — either can merge first.)*

---

> **Disclosure:** this work was done with the help of Claude Code (Anthropic's agentic coding
> tool). The reverse engineering, implementation and the verification described below were
> AI-assisted; the commits carry a `Co-Authored-By: Claude` trailer. I've reviewed it and I'm
> putting my name to it, but you should weigh that when deciding how closely to read it — and
> I'm happy to walk through any part of the format reasoning myself if that helps review.

`7f592ec` removed CS `gamez` support ahead of the RC/MW/PM common-infra refactor. This brings
it back, **ported onto the current unified `GameZ`/`Node` API rather than reverted** — there
is no `GameZDataCs`/`NodeCs` revival, and CS's lod, window, display and object3d node data
reuse the shared readers unchanged, which is a real saving over the old module's bespoke
copies of all of them.

**All nine archives of a retail install round-trip byte-identically** — the eight chapter
`gamez.zbd` *and* `planes.zbd` — both in memory (`cargo test` with `CS_GAMEZ_DIR`) and through
the real `unzbd cs gamez` → `rezbd cs gamez` zip pipeline (`test.py` → `--- ALL OK ---`, which
also exercises the JSON layer).

Before you spend review time on this, see #3, where I asked whether the removal was a
bandwidth call or an architectural one. If CS genuinely doesn't belong in the shared
abstractions, say so and I'll close this and keep the fork — no hard feelings, and the
`cam_anim` PR stands on its own either way.

## Layout

New code lives in `crates/gamez/src/gamez/cs/` (header + top-level read/write, `models.rs`,
`fixup.rs`, `nodes/{read,write}.rs`, and `data/` with the nine per-chapter texture tables
ported verbatim), plus `cs` submodules under the by-kind node directories
(`nodes/node/cs/`, `nodes/world/cs/`, `nodes/light/cs/`, `nodes/camera/cs.rs`) — mirroring
how `mw`/`pm`/`rc` are organised.

## New API surface

Everything here is optional and absent for MW/PM/RC, with one exception called out below.

- **`GameZMetadata.model_slots`** — the CS model array interleaves live models with free slots.
  `models` stays a dense `Vec<Model>` (so every existing consumer is unaffected) and this
  records each model's original slot; node model indices are remapped dense↔slot on read and
  write. The alternative, which the old CS module used, was `Vec<Option<Model>>` — that would
  have forced the sparse array on all four games.
- **Optional CS fields:** `Node.field040` (live in CS, asserted zero elsewhere),
  `World.flags`, `World.virt_partition_min_x`/`_min_z` (PM hardcodes 1; CS varies),
  `World.child_value`, `WorldPtrs.children_ptr`, `NodeFlags::UNK12`, `ModelFlags::UNK8`.
- **`ModelFlags::HARDWARE_RENDER`** — MW/PM/RC synthesise this on write from the model type;
  CS sets it independently, so it has to be stored. `write_model_info_cs` skips the synthesis.
- **`Partition.x`/`z` widened `u8` → `i16`** — this is the one change **visible to the other
  games' JSON**, though their values are unaffected. CS carries partial/bogus partition
  values (e.g. `x == -1` alongside a real `z` index) that `u8` cannot represent. Happy to hide
  this behind a CS-only representation instead if you'd rather not touch the shared type; it
  just seemed worse to have two partition types.
- `GameZMetadata.node_last_free` carries CS's header slot 32, which is the light node index
  (2338 for `planes.zbd`).

## Bonus fix: the 72-byte `planes.zbd` diff, and it isn't CS-specific

`planes.zbd` had a long-standing 72-byte round-trip diff. The cause is an asymmetry in
`Ascii`: `to_str_suffix` decodes `prefix\0suffix\0` by restoring a period at the **first**
zero, but `from_str_suffix` re-encodes by converting the **last** period. Those agree unless
the stored suffix itself contains a period — `planes.zbd` has 18 such names, e.g.
`bldhwk_cowling\0.tif\0`, which decodes to `bldhwk_cowling..tif` and was re-encoded as
`bldhwk_cowling.\0tif\0`.

I added `Ascii::from_str_suffix_first` as the true inverse and used it **from the CS texture
writer only**, leaving MW/PM/RC on the existing function. That's the conservative choice — if
you'd prefer to fix `from_str_suffix` itself, I think that's arguably more correct, but it
would change MW/PM/RC write behaviour for any name of this shape and I have no installs to
verify that against.

## Verification

- In-memory round-trip 9/9 byte-identical (`cargo test` with `CS_GAMEZ_DIR`).
- `test.py <versions> <out> --release` → `--- ALL OK ---` on the full install: all 8 chapter
  `gamez.zbd` plus `planes.zbd`, with every other suite unchanged and anim correctly reporting
  `SKIPPING` for CS on this branch.
- Manual `unzbd`/`rezbd` CLI round-trip on C1, md5-identical.
- `cargo build --workspace` / `cargo test --workspace` clean;
  `cargo run -p mech3ax-metadata-gen` runs clean.
- `cargo clippy` **below** baseline on the touched crates (49 vs 52 warnings; no warnings at
  all in the new CS files — the `Partition` widening also let 8 now-useless `.into()`
  conversions go).

## Notes

- No MW/PM/RC installs here, so those games are covered by the unit tests plus the fact that
  their read/write paths are untouched apart from the `Partition` type widening. A run of the
  full harness on your side is the check I can't do.
- This is one large commit. Happy to split it (e.g. API types / node kinds / gamez module /
  texture tables / the `Ascii` fix) if that would make review tractable — just say how you'd
  like it cut.
