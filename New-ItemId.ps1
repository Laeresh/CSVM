<#
.SYNOPSIS
Mints the next backlog/playtest item ID (BL-nnn, CAP-nn, PT-nn), safe under concurrent sessions.

.DESCRIPTION
The counter lives in the shared git common dir (.git/item-id-counters.json), so every worktree
sees the same one, and it is read-increment-written under an exclusive file lock — two sessions
can never be handed the same number. This replaced the hand-bumped "next free ID" lines in
backlog.md / playtest.md after BL-253 and BL-262 were each minted twice by concurrent sessions.

The JSON stores the LAST ISSUED number per kind. If the file is ever lost (fresh clone), re-seed
it from the highest ID ever used — scan every *.md,since retired IDs are never reused.

Run this for EVERY id, every time. It is not a once-per-session lookup: an id you derive yourself
by adding 1 to the last one, or reuse from an earlier call, was never recorded in the counter, so
the next call hands the same number out again and the duplicate-id pre-commit hook fails a later
commit (often someone else's). Use -Count to reserve a block when you need several at once.

.EXAMPLE
./New-ItemId.ps1 -Kind BL           # -> BL-283
./New-ItemId.ps1 -Kind CAP -Count 3 # -> CAP-28  CAP-29  CAP-30 (reserve a block up front)
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('BL', 'CAP', 'PT')]
    [string]$Kind,

    [ValidateRange(1, 50)]
    [int]$Count = 1
)

$ErrorActionPreference = 'Stop'

$gitCommon = git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0 -or -not $gitCommon) {
    throw "Could not resolve the git common dir from '$PSScriptRoot'."
}
$counterPath = Join-Path $gitCommon 'item-id-counters.json'
if (-not (Test-Path $counterPath)) {
    throw ("Counter file missing: $counterPath`n" +
        'Re-seed it with the highest ID EVER used per kind (scan all *.md AND git log --all, ' +
        'since retired IDs live only in history and are never reused), e.g.: {"BL":282,"CAP":27,"PT":37}')
}

# Exclusive open is the lock: a concurrent minter gets IOException and retries.
$fs = $null
for ($attempt = 0; -not $fs -and $attempt -lt 100; $attempt++) {
    try {
        $fs = [System.IO.File]::Open($counterPath,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        Start-Sleep -Milliseconds (Get-Random -Minimum 20 -Maximum 120)
    }
}
if (-not $fs) { throw "Could not lock $counterPath after 100 attempts - is another mint stuck?" }

try {
    $buf = New-Object byte[] $fs.Length
    [void]$fs.Read($buf, 0, $buf.Length)
    $data = [System.Text.Encoding]::UTF8.GetString($buf) | ConvertFrom-Json
    $last = [int]$data.$Kind

    $width = if ($Kind -eq 'BL') { 3 } else { 2 }
    for ($i = 1; $i -le $Count; $i++) {
        '{0}-{1}' -f $Kind, ($last + $i).ToString("D$width")
    }

    $data.$Kind = $last + $Count
    $json = $data | ConvertTo-Json -Compress
    $out = [System.Text.Encoding]::UTF8.GetBytes($json)
    $fs.SetLength(0)
    $fs.Write($out, 0, $out.Length)
}
finally {
    $fs.Dispose()
}
