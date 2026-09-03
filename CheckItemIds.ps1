#!/usr/bin/env pwsh
# Duplicate item-ID check for backlog.md and playtest.md.
#
# IDs are minted once by New-ItemId.ps1 and never reused or renumbered, so the same BL-/PT-/CAP-
# defined twice means two sessions minted against a stale counter. Catching it at commit time is
# what keeps the rule "a missing ID was deleted, not moved" true.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Files are READ only.
# Exit code 1 on a duplicate, so a caller can gate a commit on it.
#
# Usage:
#   ./CheckItemIds.ps1                  this tree
#   ./CheckItemIds.ps1 -Root <path>     another worktree
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$Quiet
)

if (-not $Root) { $Root = $PSScriptRoot }
if (-not $Root) { $Root = (Get-Location).Path }

$sources = @(
    @{ File = 'backlog.md';  Pattern = '^\s*-\s+`(BL-\d+)`' },
    @{ File = 'playtest.md'; Pattern = '^\s*(?:-\s+|\|\s*)`((?:PT|CAP)-\d+)`' }
)

$defs = @()
foreach ($s in $sources) {
    $p = Join-Path $Root $s.File
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { continue }
    $defs += @(Select-String -Path $p -Pattern $s.Pattern -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value })
}

# Tag vocabulary check on backlog.md headers. The vocabularies are the ones the file's own
# header documents and .claude/skills/update-backlog-artifact/build.py parses; keep all three in
# step. Tags are optional (an item minted without them still commits) but a value outside its
# vocabulary is rejected, since the artifact filters on exact values and a typo would hide the
# item from every filter.
$typeVocab = @('Bug', 'Feature', 'Research', 'Tuning', 'Cleanup', 'Fidelity', 'Perf', 'Tooling', 'Testing')
$propVocab = @{
    'Next'     = @('decode', 'data', 'code', 'look', 'decide')
    'Impact'   = @('high', 'low', 'none')
    'Evidence' = @('decoded', 'data', 'footage', 'spec', 'feel', 'trace')
}
$tagProblems = @()
$backlog = Join-Path $Root 'backlog.md'
if (Test-Path -LiteralPath $backlog -PathType Leaf) {
    $headers = Select-String -Path $backlog -Pattern '^- `(BL-\d+)`((?: `\[[^`]*\]`)*)'
    foreach ($h in $headers) {
        $id = $h.Matches[0].Groups[1].Value
        $tags = @([regex]::Matches($h.Matches[0].Groups[2].Value, '`\[([^`]*)\]`') |
            ForEach-Object { $_.Groups[1].Value })
        if ($tags.Count -eq 0) { continue }
        # A missing type is tolerated here and rejected by the artifact build: this gate checks
        # every worktree, and one stale header would block every commit until each tree caught up.
        $rest = @($tags)
        if ($typeVocab -contains $tags[0]) {
            # Select-Object rather than a 1..N slice: on a one-tag item the slice wraps to [1, 0].
            $rest = @($tags | Select-Object -Skip 1)
        } elseif ($tags[0] -notmatch '^(Owed-playtest|Blocked: .+)$') {
            $tagProblems += ('{0}: unknown type [{1}]' -f $id, $tags[0])
            $rest = @($tags | Select-Object -Skip 1)
        }
        foreach ($t in $rest) {
            if ($t -match '^(Owed-playtest|Blocked: .+|Divergence)$') { continue }
            if ($t -match '^[SML]$') { continue }
            if ($t -match '^(Next|Impact|Evidence): (\w+)$') {
                if ($propVocab[$Matches[1]] -notcontains $Matches[2]) {
                    $tagProblems += ('{0}: [{1}] is not one of {2}' -f $id, $t, ($propVocab[$Matches[1]] -join ', '))
                }
                continue
            }
            if ($t -match '^[A-Z]{1,2}\d+[A-Z]?$') { continue }
            $tagProblems += ('{0}: unknown tag [{1}]' -f $id, $t)
        }
    }
}

$dupes = @($defs | Group-Object | Where-Object { $_.Count -gt 1 })
if ($dupes.Count -eq 0 -and $tagProblems.Count -eq 0) {
    if (-not $Quiet) { Write-Output 'no duplicate item IDs, tag vocabulary clean' }
    exit 0
}

if (-not $Quiet) {
    foreach ($d in $dupes) {
        Write-Output ('Duplicate item ID defined more than once: {0} ({1} times)' -f $d.Name, $d.Count)
    }
    if ($dupes.Count -gt 0) {
        Write-Output ''
        Write-Output 'backlog.md/playtest.md define the same ID twice - mint a fresh ID with'
        Write-Output './New-ItemId.ps1 and renumber the later mint before committing.'
    }
    foreach ($p in $tagProblems) { Write-Output ('Tag vocabulary: {0}' -f $p) }
    if ($tagProblems.Count -gt 0) {
        Write-Output ''
        Write-Output 'backlog.md header tags must use the vocabularies its header documents.'
    }
}
exit 1
