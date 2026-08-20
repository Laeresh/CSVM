---
name: close-worktree
description: Close out the current worktree — commit outstanding work, close its BL-NNN if eligible, merge main in and back out, then remove the worktree. Use when a worktree's work is finished and ready to land on main.
---

Streamlines the repeat closing procedure for a `.claude/worktrees/` branch: commit, gate on backlog
closure, sync with main both ways, land on main, clean up. Runs from **inside the worktree** — it
takes no arguments and refuses to run from the main checkout (`Z:/CSVM` itself has no worktree
branch to close).

Each step below runs in order and any abort stops the whole skill — do not skip ahead.

## 1. Commit outstanding work

`git status --short` in the worktree. If clean, skip to step 2.

Otherwise stage and commit everything as one commit:
- `git add -A`
- Compose a message: subject describing what landed, matching this repo's commit style
  (`git log --oneline -5` for the pattern); body only if there's a verification record or dead-end
  worth keeping. Multi-line message → `Write` it to a file and `git commit -F <file>` (per
  CLAUDE.md) — never a shell heredoc.
- End with a `Co-Authored-By:` trailer naming whichever agent/model is running this session.
- Commit to the worktree's own branch. Do not push.

Note the resulting commit hash for the final report.

## 2. Gate on backlog closure

Extract a `BL-NNN`-shaped token from the worktree's branch name (case-insensitive, digits directly
after `bl`, ignoring surrounding prefixes like `worktree-` or trailing description words — e.g.
`worktree-bl394-ai-identity` → `BL-394`, `bl343-impact-force-gate` → `BL-343`).

**No token found** → this worktree isn't for a backlog item. Skip to step 3.

**Token found** → read that entry's full text in `backlog.md`, in particular its goal and its
*"how you'd know it worked"* line. Diff the worktree branch against `main`
(`git diff main...HEAD`) and judge, on the entry's own terms, whether that bar is actually met —
not just touched.

- **Not met** → state plainly which part of the entry's bar the diff doesn't satisfy (missing
  verification, partial fix, wrong symptom addressed, etc.), then **stop the entire skill here**.
  The step-1 commit stands; nothing merges; the worktree stays open for you to keep working in.
- **Met** → invoke the `close-backlog-item` skill for this BL-NNN now, inline, in this worktree.
  Let it make its doc/playtest edits and draft its closing commit message. Then commit those edits
  as their **own commit** (separate from step 1's), using `close-backlog-item`'s own subject
  convention: `Close BL-NNN: <what is now true>`, via `git commit -F <file>`. Run whatever
  verification `close-backlog-item`'s own step 6 calls for given what it touched.

Note this commit hash too, if made.

## 3. Check whether main has moved

`git -C Z:/CSVM fetch . <worktree-branch> --dry-run` isn't needed — just compare:
`git -C Z:/CSVM merge-base --is-ancestor <main-tip-at-branch-creation> HEAD` is unreliable across
worktrees, so instead: `git -C Z:/CSVM log --oneline <worktree-branch>..main`. Any output means
main has moved since this branch diverged.

**No output (main hasn't moved)** → skip to step 4.

**Main has moved** → in the worktree, merge main in:
- `git merge main`
- **Conflicts** → invoke the `resolving-merge-conflicts` skill to resolve them; do not hand-weave
  conflict markers yourself. If that skill itself cannot resolve a conflict and stops, this skill
  stops too — report where it left off.
- Once merged clean (with or without conflicts to resolve), this combination has never been
  tested together. Run the project's full verification battery on the merged worktree —
  `dotnet build CSVM/CSVM.sln` and `.\RunTests.ps1` (or the narrower check that fits what the
  merge touched, per CLAUDE.md's own hook logic) — and treat this as **mandatory**, not optional.
  - **Failure** → report the failure output and **stop the entire skill here**. Do not proceed to
    step 4 with a broken merge sitting in the worktree.
  - If the merge touched `analysis/goldens/manifest.json`, don't naively take either side —
    regenerate goldens on the merged tree and commit the re-pin naming the shots, per the
    worktree-merge lessons already learned on this project.

## 4. Merge back into main

From the main checkout, not the worktree:

```
git -C Z:/CSVM merge --no-ff <worktree-branch> -m "Merge <worktree-branch>: <one-line of what landed>"
```

This preserves the worktree's individual commits under a merge commit, matching the repo's
existing convention (e.g. `Merge worktree-bl348-balloon-kill-chain: close BL-348`). Never push.

If this merge itself conflicts (possible if step 3 was skipped because main hadn't moved *yet* but
moved during this run), invoke `resolving-merge-conflicts` again, then re-run the mandatory
verification battery from step 3 before continuing.

## 5. Exit and clean up

Try `ExitWorktree` with `action: "remove"` first — it only succeeds for a worktree this session
entered via `EnterWorktree`, and will refuse if there are uncommitted changes (there shouldn't be
any left at this point; if it refuses, something after step 1 wasn't actually committed — go back
and fix that instead of forcing).

If `ExitWorktree` reports no active worktree session (the common case — most worktrees here predate
this session or were made by another tool), fall back to plain git against the main checkout:

```
git -C Z:/CSVM worktree remove <worktree-path>
git -C Z:/CSVM branch -d <worktree-branch>
```

`branch -d` (not `-D`) deliberately refuses if the branch isn't fully merged — treat that refusal
as a signal something upstream of this step went wrong, not something to force past.

## Report

One final message: what got committed in steps 1-2 (hashes + one-line subjects, or "nothing to
commit"), the BL-NNN outcome (closed / not applicable / aborted-with-reason), whether main had
moved and what verification ran, the merge-back commit, and confirmation the worktree and branch
are gone. If the skill stopped early (steps 2 or 3's abort paths), say so clearly and name exactly
what's left in the worktree for you to pick back up.
