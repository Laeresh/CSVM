# CLAUDE.md

Read [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) first, it has the project description,
architecture decisions, repo layout, the Godot project + CLI reference, coding conventions, and
the "Current status" pointer. This file holds only what's specific to Claude Code as a tool.

Follow PROJECT_CONTEXT.md's "Development verification loop"; the complete `.\RunTests.ps1` is the
landing gate for any change under `CSVM/`.

## Claude Code specifics

- **Skills** live in `.claude/skills/`, mirrored at `.agents/skills/` for other `.agents/`-aware
  tools; see [`AGENTS.md`](AGENTS.md) for the mirror command. Edit the files directly. Invoke with
  their slash commands, e.g. `/domain-modeling`, `/commit-next`, `/new-plan`.
- When delegating to a subagent, use a cheaper model tier where appropriate, for example for
  exploration.
- **Hooks** (`.claude/settings.json`). Before a shell command: (1) a shell-syntax guard (a
  PowerShell here-string sent to Bash, a heredoc or `/dev/null` sent to PowerShell); (2) the Bash
  guard, [`CheckBashCommand.ps1`](CheckBashCommand.ps1): on Windows, Bash runs only `git` and
  `gh`, optionally piped into `head`, `tail`, `grep`, `wc`, `sort` or `uniq`, and everything else
  goes to PowerShell. Bash is the shell for a `gh --jq` filter with embedded double quotes, which
  PowerShell 5.1 strips; (3) the Godot guard, [`CheckGodotCommand.ps1`](CheckGodotCommand.ps1):
  a command that runs `Godot_v4*.exe` directly is blocked, launch through `RunProbe.ps1`, which
  admits it against the memory ledger (`MemoryLedger.ps1 status`); (4) the format gate,
  [`FormatBeforeTests.ps1`](FormatBeforeTests.ps1): `dotnet format` and a `-t:Rebuild` that blocks
  on StyleCop warnings, before an invocation of `RunTests.ps1`, `dotnet test` or `git commit`;
  (5) the content gate, [`CheckCommitContent.ps1`](CheckCommitContent.ps1), before a commit. After
  an Edit or Write,
  `CheckCommentCaps.ps1 -Hook` reports an over-cap comment in a `.cs` file and
  `CheckEncoding.ps1 -Hook` rejects a `.ps1` holding non-ASCII without a BOM.
- ⚠ **Gates (4) and (5) act on the tree the command names**, not the session's cwd: an absolute
  runner path, a `git -C`/`--work-tree`, or a `Set-Location` earlier in the same command.
  `-ShowRoot`/`-ShowRoots -Command '…'` says which tree a command would hit, and `-SelfTest`
  exercises a gate. How the gates read a command, and why they check one tree rather than every
  worktree, is in their script headers and [`GateCommand.ps1`](GateCommand.ps1).
- **The content gate's checks** are scripts you can run by hand while editing:
  `CheckEncoding.ps1` (double-encoded UTF-8), `CheckItemIds.ps1` (an item or verification-rule ID
  defined twice, a `backlog.md` tag outside its vocabulary), `CheckGoldenProse.ps1` (the golden
  manifest's `exercises` fields), `CheckCommentCaps.ps1` (`-Summary` for one line per file),
  `CheckDocEntries.ps1` (architecture entries and the `docs/cli.md` bullet cap),
  `CheckUidSidecars.ps1` (a Godot `.uid` beside every `.cs`/`.gdshaderinc` under `CSVM/`) and
  `CheckWaiver.ps1` (the form of a `Waiver:` line in the commit message). Each failure says how to
  fix it. A comment over cap has outgrown its subject: move the decode into `docs/` and keep the
  prohibition on the member it binds. `CSVM_SKIP_CONTENT_CHECKS=1` lands something first.
- **Codex and pi call the same gate scripts** (`.codex/hooks/pre-tool-use.ps1`,
  `.pi/extensions/hooks.ts`). Add a check to the repo scripts, never to a harness.
- ⚠ **PowerShell 5.1 reads BOM-less files as ANSI**, which silently double-encodes every
  non-ASCII character a script round-trips. Edit repo text with the Read/Edit/Write tools; when a
  script must write a repo file, pass `-Encoding utf8` on both ends (or `[IO.File]` with an
  explicit `UTF8Encoding`), and keep scripts pure ASCII, building non-ASCII from `[char]` codes.
- **Multi-line commit messages: `Write` the message to a file, then `git commit -F <file>`.** It
  involves no shell quoting, so neither shell can corrupt it.
- Agent worktrees/workspaces created with `isolation: worktree` land in
  `.claude/worktrees/` / `.claude/workspaces/`, both gitignored, swept by
  `CleanScratch.ps1`.
- ⚠ **Never create junctions or symlinks from a worktree (or `.scratch/`) into the main
  checkout.** Git-ignored media (`OriginalScreenshots\`, `playtest\`) is absent from worktrees
  by design, read it via absolute path (`Z:\CSVM\OriginalScreenshots\...`) instead of linking
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
  commit's message, found later with `git log --grep=<ID>`. The dated
  `### ⚠ … — RETIRED (yyyy-mm-dd)` headings in `docs/org/*.md` are the record of superseded
  readings, and are the only place a date belongs; do not extend the pattern elsewhere.
  `backlog.md` entries written before this rule keep their dated clauses until the entry closes or
  an edit reshapes its body, at which point the rule applies; there is no sweep of the old entries.
- Comments explain why. Length caps and what belongs in one are in
  [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md)'s coding conventions; the terms to use are in
  [`CONTEXT.md`](CONTEXT.md).
- Write no documentation unless explicitly asked.
