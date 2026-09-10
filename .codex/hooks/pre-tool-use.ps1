#!/usr/bin/env pwsh
# Ordered PreToolUse safeguards for Codex, matching .claude/settings.json.
#
# The content checks themselves are NOT duplicated here. They live in CheckCommitContent.ps1 at
# the repo root, which every harness calls, because three hand-maintained copies of the same
# checks drifted: this file and .pi/extensions/hooks.ts were each missing checks the Claude copy
# had. A check added to the repo script now reaches all three harnesses at once.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }
try { $j = $raw | ConvertFrom-Json } catch { exit 0 }
$command = [string]$j.tool_input.command
if (-not $command) { exit 0 }

# Codex has one canonical shell matcher (Bash), including Windows shell commands.
# The Claude Bash-vs-PowerShell guard is not copied because Codex does not expose that distinction.

# Only to LOCATE the gates; each derives the tree(s) it acts on for itself.
$here = git rev-parse --show-toplevel 2>$null
if (-not $here) { exit 0 }

# Format and StyleCop-check before a test run or a commit. The trigger (an invocation, never a
# mention) and the tree resolution live in FormatBeforeTests.ps1, shared with the other harnesses.
$format = Join-Path $here 'FormatBeforeTests.ps1'
if (Test-Path -LiteralPath $format -PathType Leaf) {
    & $format -Command $command
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# No trigger of its own. Whether a command is a commit is the gate's decision, made once in
# CheckCommitContent.ps1 and covered by its self-test. The three harnesses each used to test for
# 'git\s+commit' here, which required the two words to be adjacent and so skipped the gate
# entirely for 'git -C <tree> commit', the form CLAUDE.md prescribes for naming a tree.
$gate = Join-Path $here 'CheckCommitContent.ps1'
if (-not (Test-Path -LiteralPath $gate -PathType Leaf)) { exit 0 }
& $gate -Command $command
exit $LASTEXITCODE
