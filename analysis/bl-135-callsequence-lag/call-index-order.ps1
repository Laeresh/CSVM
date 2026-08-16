# How many CALL_SEQUENCE edges point FORWARD in the definition's sequence array (BL-135).
#
# The decode settles that crimson.exe walks a definition's sequences as one ascending pass over a
# fixed array and that CALL_SEQUENCE writes state into the callee's own slot, so a call runs in the
# same tick exactly when the callee's index is higher than the caller's (docs/org/sequences.md,
# "The tick walk is ascending"). CSVM appends a runner past a descending cursor, so it defers EVERY
# call by a tick. The share of edges we get wrong is therefore the FORWARD share, and that is the
# number that decides whether matching the walk is worth a golden rebaseline.
#
# Index is the position in the definition's `sequences` array. `reset_state` is a separate pointer
# on the instance (anim+0xd0), not a member of the walked array, so calls originating there are
# bucketed apart rather than given index -1.
#
# Read-only. Writes a JSON dump + a summary to .scratch/; no game data is committed.
#
#   .\analysis\bl-135-callsequence-lag\call-index-order.ps1 -Extracted Z:\CSVM\extracted

[CmdletBinding()]
param(
    [string]$Extracted = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'extracted'),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-135')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

$files = Get-ChildItem $Extracted -Recurse -Filter *.json -File |
    Where-Object { $_.FullName -match '\\(mis_anim|cam_anim)\\' }
Write-Host "scanning $($files.Count) compiled anim files under $Extracted"

$candidates = $files | Where-Object { (Get-Content $_.FullName -Raw) -match '"CallSequence"' }
Write-Host "  $($candidates.Count) carry a CallSequence event"

$edges = New-Object System.Collections.ArrayList
foreach ($f in $candidates) {
    $rel = $f.FullName.Substring($Extracted.Length).TrimStart('\')
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json

    # Index map over the walked array only: position in `sequences`.
    $index = @{}
    $i = 0
    foreach ($s in $j.sequences) {
        $k = "$($s.name)".ToLowerInvariant()
        if (-not $index.ContainsKey($k)) { $index[$k] = $i }   # first wins; the empty name is not unique
        $i++
    }

    $lanes = New-Object System.Collections.ArrayList
    if ($j.reset_state) {
        [void]$lanes.Add([pscustomobject]@{ name = '<reset_state>'; idx = $null; events = $j.reset_state.events })
    }
    $i = 0
    foreach ($s in $j.sequences) {
        [void]$lanes.Add([pscustomobject]@{ name = "$($s.name)"; idx = $i; events = $s.events })
        $i++
    }

    foreach ($lane in $lanes) {
        foreach ($ev in $lane.events) {
            $body = $ev.data.CallSequence
            if (-not $body) { continue }
            $target = "$($body.name)".ToLowerInvariant()
            $to = $null
            if ($index.ContainsKey($target)) { $to = $index[$target] }

            if ($null -eq $to)             { $bucket = 'unresolved' }
            elseif ($null -eq $lane.idx)   { $bucket = 'from-reset-state' }
            elseif ($to -gt $lane.idx)     { $bucket = 'forward' }
            elseif ($to -lt $lane.idx)     { $bucket = 'backward' }
            else                           { $bucket = 'self' }

            [void]$edges.Add([pscustomobject]@{
                file = $rel; anim = "$($j.name)"
                from = $lane.name; fromIdx = $lane.idx
                to = "$($body.name)"; toIdx = $to
                bucket = $bucket
            })
        }
    }
}

$total = $edges.Count
Write-Host ""
Write-Host "CallSequence events: $total"
Write-Host ""
Write-Host "bucket                 events    share   dispatch in the ORIGINAL"
$order = @('forward', 'backward', 'self', 'from-reset-state', 'unresolved')
$meaning = @{
    'forward'          = 'SAME tick   <- CSVM defers: WRONG'
    'backward'         = 'next tick       CSVM defers: matches'
    'self'             = 'next tick       CSVM defers: matches'
    'from-reset-state' = 'build time      outside the tick walk'
    'unresolved'       = 'no-op           name not in this def'
}
foreach ($b in $order) {
    $n = @($edges | Where-Object { $_.bucket -eq $b }).Count
    $pct = if ($total) { 100.0 * $n / $total } else { 0 }
    Write-Host ("  {0,-18} {1,8}  {2,6:N2}%   {3}" -f $b, $n, $pct, $meaning[$b])
}

$walked = @($edges | Where-Object { $_.bucket -in @('forward', 'backward', 'self') })
$fwd = @($walked | Where-Object { $_.bucket -eq 'forward' }).Count
Write-Host ""
if ($walked.Count) {
    Write-Host ("of the {0} edges the tick walk actually sees, {1} ({2:N2}%) are FORWARD, i.e. same-tick in the original and a tick late in CSVM." -f $walked.Count, $fwd, (100.0 * $fwd / $walked.Count))
}

Write-Host ""
Write-Host "definitions contributing the most forward edges:"
$edges | Where-Object { $_.bucket -eq 'forward' } | Group-Object anim |
    Sort-Object Count -Descending | Select-Object -First 12 | ForEach-Object {
        Write-Host ("  {0,-28} {1,5} forward edge(s)" -f $_.Name, $_.Count)
    }

$dst = Join-Path $Out 'call-index-order.json'
$edges | ConvertTo-Json -Depth 4 | Set-Content $dst -Encoding utf8
Write-Host ""
Write-Host "wrote $dst"
