# CLAUDE.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to Claude Code as a tool.

Follow PROJECT_CONTEXT.md's **Development verification loop**: run the exact affected suite or unit
while editing (`.\RunTests.ps1 -Suite <suite> -SkipUnits -SkipGoldens`, or `-UnitFilter <expr>
-SkipEngine -SkipGoldens`), use `.\RunTests.ps1 -Quick` for broad development confidence, and run
the complete `.\RunTests.ps1` before landing a change under `CSVM/`.
Quick and targeted runs never satisfy that landing gate; `CSVM.Tests/`-only changes do not require it.
Stage times print against `analysis/verification-budgets.json`; `over budget` is awareness only and
never changes the exit code.

## Claude Code specifics

- **Skills** live in `.claude/skills/` (mirrored at `.agents/skills/` via a local junction for
  other `.agents/`-aware tools — see [`AGENTS.md`](AGENTS.md)). Written by
  `/setup-matt-pocock-skills`; edit the files directly. Invoke with their slash commands, e.g.
  `/domain-modeling`, `/commit-next`, `/new-plan`.
- **Hooks:** `.claude/settings.json` runs seven `PreToolUse` hooks. (1) A shell-syntax guard that
  rejects a PowerShell here-string (`@'…'@`) sent to the **Bash** tool, and a heredoc or
  `/dev/null` sent to the **PowerShell** tool. (2) The **Bash** tool is blocked outright with
  "Use Powershell instead of bash" — the one exception is a command whose every `&&`/`||`/`;`/`|`
  segment starts with `git` or `gh`, since those behave identically in either shell.
  (3) `dotnet format` / `dotnet build` before `RunTests.ps1`, `dotnet test`, and `git commit`,
  blocking on remaining StyleCop warnings. (4) A duplicate item-ID check before `git commit`:
  `backlog.md`/`playtest.md` defining the same `BL-`/`PT-`/`CAP-` ID twice fails the commit.
  (5) An encoding tripwire before `git commit`: any changed text file containing double-encoded
  UTF-8 (mojibake) fails the commit, naming the file and line.
  (6) A golden-manifest prose check before `git commit`: an `exercises` field in
  `analysis/goldens/manifest.json` over 250 chars, or carrying an item id, a date or an
  "also exercises" clause, fails the commit. That field says what a shot covers *today* and is
  REWRITTEN on a re-pin, never appended to — the history is `git log -p` on the file.
  (7) A comment-cap check before `git commit`, running [`CheckCommentCaps.ps1`](CheckCommentCaps.ps1)
  over `CSVM/src` and `CSVM.Tests`: a comment block over its cap fails the commit, naming the file,
  the line and which cap applies. Run the script yourself (`-Summary` for one line per file) while
  editing. A block over cap has outgrown its subject, so reflowing it is the wrong fix: move the
  decode into `docs/` and leave the prohibition on the member it binds.
- ⚠ **PowerShell 5.1 corrupts UTF-8 silently.** It reads BOM-less files as ANSI, so a
  `Get-Content`/`Set-Content` round-trip without `-Encoding utf8` on **both** ends turns every
  em dash, arrow and warning sign into double-encoded garbage — and a BOM-less `.ps1` containing
  non-ASCII is mangled by the *interpreter itself* before it runs. Edit repo text with the
  Read/Edit/Write tools; when a script must write a repo file, pass `-Encoding utf8` (or use
  `[IO.File]` with an explicit `UTF8Encoding`) and keep the script itself pure ASCII, building
  any non-ASCII characters from `[char]` codes. Hook (5) above is the backstop, not the plan.
- ⚠ **Leave `videodata/` alone entirely, and never let PowerShell near it.** These per-clip decode
  sidecars are inert: the gauge-decode pipeline that wrote and read them went with footage-derived
  flight analysis, so nothing consumes them and no result may be quoted from them. They are
  git-ignored, so hook (5) *cannot* see them and a PS round-trip would mojibake them with no
  backstop at all. Clip filenames under `OriginalScreenshots/` carry non-ASCII too
  (`CAP-10 90° Banked Pith Up Down.mp4`), so the same care applies to those paths.
- ⚠ **Multi-line commit messages: `Write` the message to a file, then `git commit -F <file>`.**
  Shell quoting is where this goes wrong: PowerShell's `@'…'@` needs its closing delimiter at
  column 0, and the same text handed to Bash (still allowed for `git`) is silently accepted as
  literal `@` lines, committing a corrupted message. The `-F` form has no shell quoting at all,
  so it cannot go wrong in either tool. Hook (1) above is the backstop, not the plan.
- Agent worktrees/workspaces created with `isolation: worktree` land in
  `.claude/worktrees/` / `.claude/workspaces/` — both gitignored, swept by
  `CleanScratch.ps1`.
- ⚠ **Never create junctions or symlinks from a worktree (or `.scratch/`) into the main
  checkout.** Git-ignored media (`OriginalScreenshots\`, `playtest\`) is absent from worktrees
  by design — read it via absolute path (`Z:\CSVM\OriginalScreenshots\...`) instead of linking
  it in. PowerShell 5.1's recursive delete follows junctions into their *target*, so a link
  left behind turns any later cleanup into a deletion of irreplaceable original-game footage.
  `CleanScratch.ps1` unlinks reparse points before sweeping as a backstop, but other tools'
  recursive deletes have no such guard.


## Writing style

- No em dashes. Use commas, parentheses, or a new sentence.
- Banned phrases: "load-bearing", "worth stating plainly", "full stop",
  "carry the argument", "the trap", "isn't just X — it's Y".
- No punchy fragments for drama. Write complete sentences.
- Do not build to a turn of phrase. State the claim directly.
- Technical documentation, not marketing copy.
- **Docs state what is, not what was.** No dates and no event narration in live prose
  (`docs/`, `backlog.md`, `playtest.md`, code comments): write "the unscaled arc reads like the
  original at the controls", never "judged at the controls on 2026-08-16" or "retired 2026-08-16".
  A date in live prose is a claim that ages and makes the reader rebuild a timeline instead of
  reading the current state. When closing an item, the evidence and its date go in the closing
  commit's message, found later with `git log --grep=<ID>`. `docs/HISTORY.md` (frozen) and the
  dated `### ⚠ … — RETIRED (yyyy-mm-dd)` headings in `docs/org/*.md` are the record of superseded
  readings, and are the only place a date belongs; do not extend the pattern elsewhere.
- Comments explain why. Length caps and what belongs in one are in
  [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md)'s coding conventions; the terms to use are in
  [`CONTEXT.md`](CONTEXT.md).
- Write no documentation unless explicitly asked.
