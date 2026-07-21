# Upstream PR package (mech3ax CS revival, plan item 14)

Prepared 2026-07-21. These are the **ready-to-open** upstream contributions for the work in
`docs/PLAN-mech3ax-cs-revival.md`. Per the project's division of labor (CLAUDE.md), the code
here is prepared for the user, who owns all upstream/community communication — **nothing has
been pushed or opened**.

Target: [TerranMechworks/mech3ax](https://github.com/TerranMechworks/mech3ax), base
`0.7.0-rc3` (`cbb838f`). Fork remote: `git@github.com:Laeresh/mech3ax.git` (`origin`).

## Branches (local, in `tools/mech3ax/`, unpushed)

| Branch | Contents | Base |
|---|---|---|
| `pr-cs-anim` | Track B — `cam_anim.zbd`/`mis_anim.zbd` support (4 commits) | `cbb838f` (rc3) |
| `pr-cs-gamez` | Track A — CS `gamez.zbd`/`planes.zbd` revival (1 commit) | `cbb838f` (rc3) |
| `cs-anim` | integration branch = both, what this project builds from | `cbb838f` (rc3) |

The two PR branches are **independent** — either can be merged first, in either order, with no
dependency on the other. That cost one real fix (below) and is worth it: upstream can take the
uncontroversial one without waiting on the discussion the other may need.

## Status (2026-07-21)

| Step | State |
|---|---|
| Pre-PR question to upstream | **posted** — [#3](https://github.com/TerranMechworks/mech3ax/issues/3) |
| AI-assistance disclosure on #3 | **posted** (the issue itself predated the rule) |
| Anim PR (`pr-cs-anim`) | **opened** |
| GameZ PR (`pr-cs-gamez`) | **prepared, deliberately held** pending upstream's answer on #3 |

Both branches are pushed to the fork. Holding the gamez PR is the intended sequence, not an
oversight: if upstream answers #3 with "architectural", that PR shouldn't be opened at all.

## AI-assistance disclosure (applies to everything that leaves this repo)

**Standing rule, decided 2026-07-21: all outside communication about this work discloses that
it was done with the help of Claude Code.** That covers PR bodies, issues, discussion posts,
comments, and any community writeup — not just the initial submission.

Where it currently lives:

- `pr-1-anim.md` and `pr-2-gamez.md` — a blockquote disclosure directly under the title, above
  the technical content, so a reviewer sees it before deciding how to read the PR.
- The commits already carry a `Co-Authored-By: Claude` trailer (they did before this rule).
- Issue #3 went out **before** this decision, so its opening post has no disclosure; the
  follow-up comment at the bottom of `pr-0-discussion.md` was **posted to that thread**
  (2026-07-21), ahead of any PR reaching a reviewer.

The wording deliberately doesn't oversell: it names the tool, says the RE/implementation/
verification were AI-assisted, states that the user reviewed it and stands behind it, and
leaves the maintainer free to weigh that — including by declining the contribution.

## Suggested order

1. **`pr-0-discussion.md`** — a short question to upstream *before* investing in review of (3).
   Costs nothing and determines how much polish PR (3) is worth.
2. **`pr-1-anim.md`** — Track B. Clean, additive, fills an "isn't implemented yet" gap; no
   conflict with upstream's direction. Open this one first regardless of the answer to (1).
3. **`pr-2-gamez.md`** — Track A. Larger, and touches code upstream deliberately removed, so
   it is the one that may need real back-and-forth.

## What changed during PR prep (2026-07-21)

Splitting the single working branch into two independently-mergeable PRs surfaced a genuine
defect, not just a bookkeeping change:

- **The anim work did not stand on its own.** `metadata-gen` panicked at type resolution
  (`type mech3ax_api_types::anim::events::NodeBelowAlt required by Condition.NodeBelowAlt not
  found`) because the codegen registrations for four types the *anim* work introduced were
  written later, during the *gamez* work, and so lived in the gamez commit. `docs/PLAN-…`
  already diagnosed this correctly ("Track B item 4 added … but never registered them") but
  fixed it in the wrong place. The registration and its changelog line now sit in the anim
  branch, where the types are introduced; the gamez branch no longer touches `metadata-gen`
  at all.
- Doc conflicts (`README.md` support matrix, `CHANGELOG.md`) were resolved per branch so each
  PR claims only its own support.

The `Ascii::from_str_suffix_first` fix (the 72-byte `planes.zbd` diff) is deliberately **not**
split into its own PR, contrary to the plan's tentative option (3): as implemented it is
purely additive and its only caller is the CS texture writer, so upstream would be taking dead
code. It stays in the gamez PR, called out as its own changelog entry.

## Verification of the split

Each branch was verified **independently**, not just the combination:

| Check | `pr-cs-anim` | `pr-cs-gamez` |
|---|---|---|
| `cargo build --workspace` | clean | clean |
| `cargo test --workspace` | 0 failed | 0 failed |
| `cargo run -p mech3ax-metadata-gen` | runs clean (389 files) | runs clean (375 files) |
| `test.py … --release` | `--- ALL OK ---`, gamez correctly skips CS | `--- ALL OK ---`, anim correctly skips CS |

And on the integration branch `cs-anim`: `test.py --- ALL OK ---` with **no** CS suite skipped
— all 61 anim archives and all 9 gamez archives round-trip byte-identically.

The strongest check that the split changed no behaviour: `git diff df16d8e cs-anim` (the
pre-split tip vs. the re-integrated branch) is **one reordered CHANGELOG line** — the code
trees are identical.

Test harness: `tools/test-versions/crimson-cs/zbd` → junction to `CrimsonSkiesGame/ZBD`, plus
`strings.dll` beside it. Outputs in `.scratch/mech3ax-test-{anim,gamez,both}/` (git-ignored,
regeneratable).

## Opening them

Nothing is pushed. When the user is ready:

```
cd tools/mech3ax
git push origin pr-cs-anim
git push origin pr-cs-gamez
```

then open each PR against `TerranMechworks/mech3ax:main`, using the corresponding markdown
file in this directory as the PR body (the first line is the PR title).
