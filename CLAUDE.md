# CLAUDE.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first — it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to Claude Code as a tool.

## Claude Code specifics

- **Skills** live in `.claude/skills/` (mirrored at `.agents/skills/` via a local junction for
  other `.agents/`-aware tools — see [`AGENTS.md`](AGENTS.md)). Written by
  `/setup-matt-pocock-skills`; edit the files directly. Invoke with their slash commands, e.g.
  `/domain-modeling`, `/commit-next`, `/new-plan`.
- **Hooks:** `.claude/settings.json` runs five `PreToolUse` hooks. (1) A shell-syntax guard that
  rejects a PowerShell here-string (`@'…'@`) sent to the **Bash** tool, and a heredoc or
  `/dev/null` sent to the **PowerShell** tool. (2) The **Bash** tool is blocked outright with
  "Use Powershell instead of bash" — the one exception is a command whose every `&&`/`||`/`;`/`|`
  segment starts with `git` or `gh`, since those behave identically in either shell.
  (3) `dotnet format` / `dotnet build` before `RunTests.ps1`, `dotnet test`, and `git commit`,
  blocking on remaining StyleCop warnings. (4) A duplicate item-ID check before `git commit`:
  `backlog.md`/`playtest.md` defining the same `BL-`/`PT-`/`CAP-` ID twice fails the commit.
  (5) An encoding tripwire before `git commit`: any changed text file containing double-encoded
  UTF-8 (mojibake) fails the commit, naming the file and line.
- ⚠ **PowerShell 5.1 corrupts UTF-8 silently.** It reads BOM-less files as ANSI, so a
  `Get-Content`/`Set-Content` round-trip without `-Encoding utf8` on **both** ends turns every
  em dash, arrow and warning sign into double-encoded garbage — and a BOM-less `.ps1` containing
  non-ASCII is mangled by the *interpreter itself* before it runs. Edit repo text with the
  Read/Edit/Write tools; when a script must write a repo file, pass `-Encoding utf8` (or use
  `[IO.File]` with an explicit `UTF8Encoding`) and keep the script itself pure ASCII, building
  any non-ASCII characters from `[char]` codes. Hook (5) above is the backstop, not the plan.
- ⚠ **PowerShell must never touch `videodata/`** — the per-clip decode sidecars written by
  `analysis/video-flight-calibration/clipdata.py`. They are git-ignored, so hook (5) *cannot* see
  them: a PS round-trip would mojibake the prose shot-index with no backstop at all. Read and write
  them only through `clipdata.py` (`show` / `slice` / `where` / `note` / `build`), which uses
  explicit UTF-8 and refuses a file that already looks double-encoded. Clip filenames carry
  non-ASCII too (`CAP-10 90° Banked Pith Up Down.mp4`), so this covers the paths as well as the
  contents.
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
