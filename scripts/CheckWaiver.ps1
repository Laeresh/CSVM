#!/usr/bin/env pwsh
# Waiver-line form check for a commit message.
#
# A landing over a red battery records the failure it lands over with one line per failing suite:
#
#   Waiver: <suite> (owned by #N): <why the landing does not wait for it>
#
# This checks the FORM of such a line and nothing more. It cannot know whether the battery was red
# (it never reads a battery log, and a missing waiver on a red landing is invisible to it); the
# orchestrator's review of the reported battery result is what enforces that a red landing carries
# one. What it does catch: a waiver that names no suite, no owning issue, or no reason. The #N owner
# is checked for form only: a hook cannot reach the tracker.
#
# A line counts as an attempted waiver when it starts with "waiver" followed by ":", "=" or "-" in
# any case, so a misspelled one blocks instead of passing unread.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md). Files are READ only.
# Exit code 1 on a malformed waiver, so a caller can gate a commit on it.
#
# Usage:
#   ./scripts/CheckWaiver.ps1 -MessageFile <path>
#   ./scripts/CheckWaiver.ps1 -Message <text>
[CmdletBinding()]
param(
    [string]$Message,
    [string]$MessageFile
)

if ($MessageFile) {
    if (-not (Test-Path -LiteralPath $MessageFile -PathType Leaf)) {
        Write-Output ('message file not found: ' + $MessageFile)
        exit 1
    }
    $Message = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $MessageFile).Path)
}
if (-not $Message) { exit 0 }

$attempt = '(?i)^\s*waiver\s*[:=-]'
$form = '^Waiver: (?<suite>[^\s()]+) \(owned by #\d+\): (?<why>\S.*\S)\s*$'
$minWhy = 20

$problems = @()
foreach ($line in ($Message -split "`r?`n")) {
    if ($line -notmatch $attempt) { continue }
    $m = [regex]::Match($line, $form)
    if (-not $m.Success) {
        $problems += ('malformed waiver: "' + $line.Trim() + '"')
        continue
    }
    if ($m.Groups['why'].Value.Length -lt $minWhy) {
        $problems += ('waiver reason under ' + $minWhy + ' characters: "' + $line.Trim() + '"')
    }
}

if ($problems.Count -eq 0) { exit 0 }
foreach ($p in $problems) { Write-Output $p }
Write-Output 'Expected one line per failing suite:'
Write-Output '  Waiver: <suite> (owned by #N): <why the landing does not wait for it>'
exit 1
