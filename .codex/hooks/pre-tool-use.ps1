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

# The tree a command writes to is not always the directory this hook runs in: git -C names another
# one. Formatting the wrong tree is worse than checking it, because dotnet format WRITES.
function Get-CommandRoot {
    param([string]$CommandLine)
    $m = [regex]::Match($CommandLine, '(?:^|\s)-C\s+("[^"]*"|\S+)')
    if ($m.Success) {
        $named = $m.Groups[1].Value.Trim('"')
        if (Test-Path -LiteralPath $named) {
            $top = git -C $named rev-parse --show-toplevel 2>$null
            if ($top) { return [string]$top }
        }
    }
    $top = git rev-parse --show-toplevel 2>$null
    if ($top) { return [string]$top }
    return ''
}

if ($command -match 'RunTests\.ps1|dotnet\s+test|git\s+commit') {
    $repo = Get-CommandRoot -CommandLine $command
    if ($repo) {
        $proj = Join-Path $repo 'CSVM/CSVM.csproj'
        if (Test-Path -LiteralPath $proj) {
            dotnet format $proj --verbosity quiet
            $buildOutput = dotnet build $proj --verbosity quiet --nologo -t:Rebuild 2>&1
            $remaining = $buildOutput | Select-String -Pattern 'SA\d{4}' | Where-Object { $_.Line -notmatch 'SA0001' }
            if ($remaining) {
                $remaining | ForEach-Object { [Console]::Error.WriteLine($_.Line) }
                [Console]::Error.WriteLine('StyleCop warnings dotnet format could not auto-fix remain above (SA0001 excluded) - fix them by hand, then retry.')
                exit 2
            }
        }
    }
}

if ($command -notmatch 'git\s+commit') { exit 0 }

# Only to LOCATE the gate; the gate derives the trees to check for itself.
$here = git rev-parse --show-toplevel 2>$null
if (-not $here) { exit 0 }
$gate = Join-Path $here 'CheckCommitContent.ps1'
if (-not (Test-Path -LiteralPath $gate -PathType Leaf)) { exit 0 }
& $gate -Command $command
exit $LASTEXITCODE
