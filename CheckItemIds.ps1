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

$dupes = @($defs | Group-Object | Where-Object { $_.Count -gt 1 })
if ($dupes.Count -eq 0) {
    if (-not $Quiet) { Write-Output 'no duplicate item IDs' }
    exit 0
}

if (-not $Quiet) {
    foreach ($d in $dupes) {
        Write-Output ('Duplicate item ID defined more than once: {0} ({1} times)' -f $d.Name, $d.Count)
    }
    Write-Output ''
    Write-Output 'backlog.md/playtest.md define the same ID twice - mint a fresh ID with'
    Write-Output './New-ItemId.ps1 and renumber the later mint before committing.'
}
exit 1
