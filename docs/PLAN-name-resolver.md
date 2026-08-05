# NameResolver — the name→node resolution leaves `AnimRuntime`

**DRAFTED PLAN** (written 2026-08-05, from that day's architecture review, candidate 2; decisions
settled in the same day's grilling session). It sits in `docs/`, which by this repo's convention
makes it live; PROJECT_CONTEXT.md's "Current status" still names `PLAN-m3-polish-7` as the active
plan, so treat this as a self-contained handoff for its own session (a parallel worktree is fine —
see the file-ownership note). Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans/plans.md`](plans/plans.md), when every item lands.

"Which world node(s) does this name mean, for this def, at this anchor" is one concept spread over
~10 private methods, 4 caches, 3 policy flags and a census inside the 3,700-line `AnimRuntime.cs`,
with its one invariant — *"every consumer must resolve through THIS"* (`ResolveScoped`'s ⚠,
`AnimRuntime.cs:2869-2872`) — enforced by a doc comment. It has already diverged once (the emitter
host and the motion targets took different routes to the same name, so an authored stop never
reached its emitter). Its rules are deep and all measured: symbol-table authority beats name
matching (`caboose`, `:3678-3685`), twin-instance narrowing (`air_gen`/`eairg31`/`eairg32`,
`:3645-3671`), the three-tier scope order (`fly_trail1`-`5` shared by three templates,
`:2858-2884`), root-lift with `MaxRootLift`, the `.flt` suffix double-match, `#`-matches-a-digit-run.
**None of it is tested in either tier** — `CSVM.Tests` has no resolver coverage at all, and
asserting any rule today needs a chapter world.

This plan extracts `NameResolver<TNode>` to `src/Mech3/Anim/`, beside `MotionSet` and
`EmitterDirector` — the two prior extractions of exactly this shape. **This is not the declined
G18 split** (2026-08-03, design-it-twice, recorded in `architecture.md`'s `AnimRuntime` entry): all
three runtime modes keep using the *same* resolver; a concept moves, not a mode. **No behaviour
change is intended anywhere** — 13/13 goldens hash-identical is primary evidence per item, and an
item that cannot keep them identical stops rather than repins.

## Milestone goal

- The resolver is one module with one interface; `AnimRuntime` cannot compose resolution primitives
  in the wrong order because it no longer holds them — the tier-order invariant is structural.
- Every measured rule above is asserted **off-engine** in `CSVM.Tests`, against a token node type —
  no chapter world, no Godot node.
- The resolution census (`anchored` / `unanchored` / `target-missing`) is resolver-owned data;
  `--debug-anim`'s block projects it instead of re-deriving it.

**`AnimRuntime` keeps the pool and the call semantics.** `SlotOf` / `TemplateRootsFor` /
`NextPooledAnchors` / `PlaceTemplateAt` stay where Decision 16 of PLAN-deepening pinned them, and
`CallTargetSite` / `ConditionNode` stay callers of the resolver, not parts of it.

## Decisions (2026-08-05)

Settled in the grilling session. This table is the authority where prose disagrees.

| # | Question | Decision |
|---|---|---|
| 1 | Whole tier chain, or primitives only? | **Full ownership, pool as a supplied hook.** `NameResolver` owns the index (+ parent snapshot), `Matcher`, `FindAll`, `ResolvePath`, the symbol authority (`_byIndex`, `NarrowToSymbolRoot`), root-lift with `NameResolveFallback`/`SuppressRootLift`/`MaxRootLift` as its documented policy inputs, the census, **and `ResolveScoped`'s three-tier order** — the middle tier's "my own template roots" supplied as a constructor delegate (`ownRootsOf(def, anchor)`) that `AnimRuntime` implements from `TemplateRootsFor`. The ⚠ becomes structural: `ResolvePath` is unreachable from `AnimRuntime`. Decision 16 (PLAN-deepening E12) is respected by construction — the resolver sees only the resolved root list the hook returns. Losing option: primitives-only — smaller diff, but the tier *order* (the thing that already diverged once) stays caller-side, so the seam would protect the parts and not the rule. |
| 2 | Test tier | **Generic `NameResolver<TNode>`, off-engine suite.** Rows enter as `Add(node, srcName, parent, gamezIndex?)`; the three query-time tree touches become module-internal — ancestry/parent from the snapshot those rows build, liveness a supplied predicate (`Node3D` adapter passes Godot's `IsInstanceValid`; tests pass `_ => true`). Two instantiations exist from day one (`Node3D` in engine, a token type in tests) — the two-adapters rule satisfied at birth. The snapshot also *documents* the standing invariant ("the index is built once and never added to", `:3772-3775`; no reparenting) instead of relying on it. Decision 22's contrary call for `MotionSet` does not transfer: that module "dereferences no node, identity only" and had a suite; resolution has real logic and zero coverage, and a new public type widens no `internal`. Losing option: `Node3D`-keyed + engine suites — delivers locality but leaves every rule testable only by building a chapter world. |
| 3 | Migration | **Three commits, each goldens-identical**, in Checklist order below; the between-commits instrument is the resolution census triple on a `--debug-anim` chapter run, A/B'd against HEAD and required identical; placement `src/Mech3/Anim/NameResolver.cs` with its own `architecture.md` entry carrying the tier-order ⚠ and the no-reparent-after-index invariant. |

## ⚠ Read this before implementing anything

| # | The claim to keep honest | What guards it |
|---|---|---|
| 1 | "This re-opens G18." | It must not. G18 declined splitting the *observation* surface into per-mode interfaces; this extracts one concept all modes share. Quote the G18 ⚠ in `architecture.md`'s `AnimRuntime` entry when writing the new entry, and change no mode behaviour. |
| 2 | "`Anchors` can be called wherever resolution is needed." | No — `Anchors` records the anchoring census **once per def**; `ResolveInOwnRoot` (`:3732-3733`) and `TemplateRootsFor` deliberately call `FindAll`, not `Anchors`, to avoid re-entering it. This discipline moves *inside* the module (a private census-free path), where it stops being a caller obligation — but it must be carried over exactly. |
| 3 | "The caches can be merged or dropped." | Each exists for a measured reason: `_findCache` serves C5's ~400 live poll loops re-dispatching every frame (`:3767-3775`); `_matcherCache` the regex builds; `_byIndex` is deliberately NOT populated on the crash runtime (`NameResolveFallback`, `:3659`) and NOT shared across pooled copies (`IndexPooledCopy`, `:909-918` — every copy carries the SAME compiled indices, so a shared map would misresolve). Port them as they are. |
| 4 | "Line citations are current." | They are against `4e4db08` (2026-08-05). `AnimRuntime.cs` is the hottest file in the repo — expect drift; re-grep before editing. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the resolver, in three goldens-identical steps

1. ☑ `NameResolver<TNode>`: index + `Matcher` + `FindAll` + `ResolvePath`, with the off-engine suite
2. ☑ The symbol authority, `Anchors`/root-lift, and the census move in
3. ☐ `ResolveScoped`'s tiers + the own-roots hook; `ResolveOne` folds into `Resolve`

## Dependency and parallelism notes

Linear: A1 → A2 → A3; no intra-plan parallelism. **File ownership of this whole plan:**
`CSVM/src/Mech3/Anim/NameResolver.cs` (new), `CSVM/src/Mech3/AnimRuntime.cs`, a new
`CSVM.Tests/NameResolverTests.cs`. It contends with `PLAN-effect-catalogue` on **nothing** (that
plan owns `Session/` + `Flight`-side tests + `Suites.cs`), so the two may run as parallel worktree
sessions. It DOES own `AnimRuntime.cs` outright — nothing else may edit that file while this plan
is in flight.

**⚠ Contention with the active plan's D9 (`PLAN-m3-polish-7`, `BL-228` `WAIT_FOR_COMPLETION`).**
D9 lands its hold in the sequence scheduler — `AnimRuntime.cs`'s `CallAnimation` path — so it and
this plan edit the same file and must be **serialized, either order, never parallel**. D9 is also
the active plan's one goldens-may-move item: whichever runs second takes its baselines (goldens,
the census-triple A/B) against the then-HEAD, not against `4e4db08`.

---

# Wave A — the resolver, in three goldens-identical steps

## A1 ☑ `NameResolver<TNode>`: index + `Matcher` + `FindAll` + `ResolvePath`, with the off-engine suite

**Goal.** The index rows, the wildcard matcher, the memoized find, and path resolution live in a
public generic module; `AnimRuntime` holds a `NameResolver<Node3D>` and delegates; the first
off-engine resolver suite exists and asserts the matcher/find rules.

**Evidence (confidence: traced).** `_index`/`_matcherCache`/`_findCache` (`AnimRuntime.cs:355`,
`:357`, `:469`), `FindAll` (`:3777-3797`, incl. the `.flt` suffix double-match and the
instance-id cache key), `Matcher` (`:3802-3818`, `*`/`#` semantics), `ResolvePath` (`:3752-3765`),
`IndexWorld`/`AppendSubtree`/`IndexStage`/`IndexPooledCopy` feed sites (`:902`, `:923`, `:1869-1879`).
Query-time tree touches to snapshot: `IsAncestorOf` in `FindAll` scope filtering (`:3792`).

**Approach.** New `src/Mech3/Anim/NameResolver.cs`, public, generic over `TNode` (Decision 2): rows
`Add(node, srcName, parent, gamezIndex?)`, ancestry from the parent snapshot, `FindAll` keyed on a
caller-opaque scope token. `AnimRuntime`'s index-building walks call `Add` with parent; `FindAll`/
`ResolvePath` calls forward. New `CSVM.Tests/NameResolverTests.cs`: `#` matches a digit run
including zero (`air_gen#` covers `air_gen`), `*`/`**`, case-insensitivity, `.flt` double-match,
scope restriction via the snapshot, `_findCache` read-only-list semantics.

**Model recommendation.** medium — mechanical extraction of pure logic; the design is settled.

**Verify.** Full `.\RunTests.ps1` (goldens identical); the census A/B (Decision 3): `--debug-anim`
`--freecam --chapter=C5 --screenshot` run, `ResolutionLines`/anchored-unanchored-missing counts
identical to HEAD (C5 for its ~400 poll loops — the FindAll-heavy chapter). New unit checks each
seen failing once.

**⚠ Traps.** The `_findCache` key uses instance ids because Godot equality inside tuples is
untrustworthy (`:3779-3781`) — the generic version must define identity explicitly (comparer or
constraint), not inherit `Equals`. `IndexPooledCopy` must NOT feed `_byIndex` (⚠ table row 3) —
that lands in A2, but the `Add` signature must already make it expressible.

## A2 ☑ The symbol authority, `Anchors`/root-lift, and the census move in

**Goal.** `_byIndex`, `NarrowToSymbolRoot`, `Anchors` (with root-lift and the three policy inputs),
and the census (`RecordAnchoring`/`RecordMissingTarget`/`ResolutionLines`) are resolver-owned;
`--debug-anim` projects the census; the symbol/narrowing/lift rules gain off-engine assertions.

**Evidence (confidence: traced).** `Anchors` (`:3607-3643`), `NarrowToSymbolRoot` (`:3660-3671` —
the `eairg31`/`eairg32` cross-bind story), `_byIndex` population rules (`:1869-1879`, `:909-918`),
`Targets`' symbol-first + `genx12` anchor-scoped rescue (`:3676-3705`), policy flags (`:207-218`,
`MaxRootLift` `:315`), census fields (`:395-403`) and recorders (`RecordAnchoring` ~`:1799`,
`RecordMissingTarget` ~`:1841` — re-grep, BL-061 shifted this region).

**Approach.** Symbol lookups take the def's `NodeRefs` as an argument (the def type stays outside
the resolver's generic core or is passed whole — implementer's call, but the resolver must not
require Godot to *compile* its test instantiation). Root-lift needs the parent snapshot (already in
from A1). The census-once-per-def discipline becomes a private census-free internal path (⚠ table
row 2). `Targets` itself stays in `AnimRuntime` this item (it reads event payloads) but every
lookup it does goes through the resolver.

**Model recommendation.** high — this is the judgement item: the narrowing/fallback/rescue rules
are measured behaviours with named bugs behind them, and a subtle porting error moves world
behaviour silently until the census A/B catches it.

**Verify.** Full gate + census A/B as A1, plus: off-engine assertions for symbol-beats-name
(`caboose` shape), twin narrowing keeps `air_gen#1`→`eairg31`, narrowing returns null on
reader defs / unbuilt index / foreign root, root-lift refuses above `MaxRootLift`,
`NameResolveFallback` leaves symbol lookups empty. Each seen failing once.

**⚠ Traps.** All of ⚠ table rows 2 and 3. `NarrowToSymbolRoot` returning `null` (undecidable)
versus an empty narrowing are different outcomes (`:3670`) — keep the tri-state. The `genx12`
rescue is anchor-scoped *strictly* (`:3694-3700`) — never global.

## A3 ☐ `ResolveScoped`'s tiers + the own-roots hook; `ResolveOne` folds into `Resolve`

**Goal.** The three-tier scope order is resolver-owned with the pool supplied as `ownRootsOf`;
`ResolvePath` goes private to the module; `AnimRuntime` resolves only through the module's `Resolve`
/ `ResolveScoped` / `Anchors` surface, and the tier-order ⚠ moves to the resolver's
`architecture.md` entry as a structural fact.

**Evidence (confidence: traced).** `ResolveScoped` + ⚠ (`:2858-2884`), `ResolveInOwnRoot`'s pool
dependency (`:3738-3748` → `TemplateRootsFor`), `ResolveOne` (`:2850-2856`), consumers:
`Targets`, `CallTargetSite` (`:2902+`), `ConditionNode` (`:3293`), the emitter-host path.

**Approach.** Constructor delegate `ownRootsOf(def, anchor)` implemented by `AnimRuntime` from
`TemplateRootsFor` (Decision 1); `LocalNodesOnly` read off the def argument. Off-engine tier tests:
anchor-subtree wins; own-root tier consulted only on anchor miss (hook returning a canned list);
global tier skipped under `LocalNodesOnly`; dead-anchor path (`IsInstanceValid` predicate false)
falls to the local-only global resolve exactly as `:2876-2877`.

**Model recommendation.** high — the tier order is the invariant the whole plan exists to protect;
the last chance to get it wrong is here.

**Verify.** Full gate + census A/B; the 27 engine suites (effects-census plays 33 defs through
every tier; emitter-host-deactivation and effect-template-mesh cover the diverged-once bug's exact
shape). Docs sweep in the same turn: new `## src/Mech3/Anim/NameResolver.cs` entry (tier-order ⚠,
no-reparent invariant, the G18 non-relation line), `AnimRuntime` entry slimmed, `Anim/` index line,
HISTORY entry per commit.

**⚠ Traps.** `ResolveInOwnRoot` iterates `TemplateRootsFor` per event — the hook must not
introduce the census re-entry (⚠ table row 2) or a new allocation on the poll-loop hot path
(`_findCache` exists because of `:3767-3770`; keep `Resolve` allocation-shape comparable).
