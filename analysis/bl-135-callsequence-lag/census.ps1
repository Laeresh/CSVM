# Census of CALL_SEQUENCE / STOP_SEQUENCE across the install, to size the bound on a
# same-pass drain (BL-135 / PLAN-m3-polish-6 B12).
#
# The drain has to answer one question with data: how many sequences can one definition
# legitimately start within a single tick, and can the authored call graph cycle? So this
# builds the per-definition sequence call graph (CALL_SEQUENCE edges, plus STOP_SEQUENCE
# edges — a STOP of a sequence that is not running CALLS it, docs/formats/anim-definitions.md)
# and reports the longest acyclic chain and every cycle.
#
# Read-only. Writes a JSON dump + a summary to .scratch/; no game data is committed.
#
#   .\analysis\bl-135-callsequence-lag\census.ps1 [-Extracted Z:\CSVM\extracted] [-Out .scratch\bl-135]

[CmdletBinding()]
param(
    [string]$Extracted = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'extracted'),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-135')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

# ---- compiled front-end (cam_anim / mis_anim) -----------------------------------------

$files = Get-ChildItem $Extracted -Recurse -Filter *.json -File |
    Where-Object { $_.FullName -match '\\(mis_anim|cam_anim)\\' }
Write-Host "scanning $($files.Count) compiled anim files under $Extracted"

$candidates = $files | Where-Object {
    (Get-Content $_.FullName -Raw) -match '"(Call|Stop)Sequence"'
}
Write-Host "  $($candidates.Count) carry a CallSequence/StopSequence event"

$defs = @()
$calls = 0
$stops = 0
foreach ($f in $candidates) {
    $rel = $f.FullName.Substring($Extracted.Length).TrimStart('\')
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json
    $edges = @()
    $seqNames = @()
    $seqs = @()
    if ($j.reset_state) { $seqs += [pscustomobject]@{ name = '<reset_state>'; events = $j.reset_state.events } }
    foreach ($s in $j.sequences) { $seqs += [pscustomobject]@{ name = $s.name; events = $s.events } }
    foreach ($s in $seqs) {
        $seqNames += "$($s.name)"
        foreach ($ev in $s.events) {
            foreach ($kind in @('CallSequence', 'StopSequence')) {
                $body = $ev.data.$kind
                if (-not $body) { continue }
                if ($kind -eq 'CallSequence') { $calls++ } else { $stops++ }
                $edges += [pscustomobject]@{ from = "$($s.name)"; to = "$($body.name)"; kind = $kind }
            }
        }
    }
    if ($edges.Count -eq 0) { continue }
    $defs += [pscustomobject]@{
        file = $rel; anim = $j.name; activation = "$($j.activation)"
        sequences = $seqNames; edges = $edges
    }
}

Write-Host ""
Write-Host "definitions with a call edge : $($defs.Count)"
Write-Host "CallSequence events          : $calls"
Write-Host "StopSequence events          : $stops"

# ---- graph analysis: cycles + longest acyclic chain ------------------------------------

function Analyse($def, $kinds) {
    $adj = @{}
    foreach ($e in $def.edges) {
        if ($kinds -notcontains $e.kind) { continue }
        $k = $e.from.ToLowerInvariant()
        if (-not $adj.ContainsKey($k)) { $adj[$k] = New-Object System.Collections.ArrayList }
        [void]$adj[$k].Add($e.to.ToLowerInvariant())
    }
    $cycles = New-Object System.Collections.ArrayList
    $best = 1
    $bestPath = $null
    foreach ($start in $adj.Keys) {
        $stack = New-Object System.Collections.Stack
        $stack.Push(@($start))
        while ($stack.Count -gt 0) {
            $path = $stack.Pop()
            $node = $path[-1]
            if ($path.Count -gt $best) { $best = $path.Count; $bestPath = $path }
            if (-not $adj.ContainsKey($node)) { continue }
            foreach ($next in $adj[$node]) {
                if ($path -contains $next) {
                    [void]$cycles.Add((($path + $next) -join ' -> '))
                    continue
                }
                $stack.Push(@($path + $next))
            }
        }
    }
    [pscustomobject]@{
        anim = $def.anim; file = $def.file; activation = $def.activation
        depth = $best; path = ($bestPath -join ' -> ')
        cycles = @($cycles | Select-Object -Unique)
    }
}

$analysed = $defs | ForEach-Object { Analyse $_ @('CallSequence', 'StopSequence') }
$analysed | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Out 'call-graphs.json') -Encoding utf8

Write-Host ""
Write-Host "longest acyclic call chain per definition (sequences started from one entry):"
$analysed | Group-Object depth | Sort-Object { [int]$_.Name } | ForEach-Object {
    Write-Host ("  depth {0}: {1} definition(s)" -f $_.Name, $_.Count)
}
Write-Host ""
Write-Host "deepest:"
$analysed | Sort-Object depth -Descending | Select-Object -First 8 | ForEach-Object {
    Write-Host ("  {0,-24} depth {1}  {2}" -f $_.anim, $_.depth, $_.path)
}

$withCycles = @($analysed | Where-Object { $_.cycles.Count -gt 0 })
Write-Host ""
Write-Host "definitions whose call graph CYCLES: $($withCycles.Count)"
$withCycles | Select-Object -First 20 | ForEach-Object {
    Write-Host ("  {0,-24} {1}" -f $_.anim, ($_.cycles -join ' | '))
}

# A STOP_SEQUENCE edge is only a CALL when nothing by that name is running; the dominant
# authored use is the self-halt (`setprop` breaking out of its own IF chain), which can never
# call. So the spin risk a drain has to survive is the CALL-only subgraph — reported apart.
$callOnly = $defs | ForEach-Object { Analyse $_ @('CallSequence') }
$callCycles = @($callOnly | Where-Object { $_.cycles.Count -gt 0 })
Write-Host ""
Write-Host "CALL-only subgraph: deepest chain $(($callOnly | Measure-Object depth -Maximum).Maximum), cycling definitions: $($callCycles.Count)"
$callCycles | Group-Object anim | Sort-Object Name | ForEach-Object {
    Write-Host ("  {0,-24} x{1,-4} {2}" -f $_.Name, $_.Count, ($_.Group[0].cycles -join ' | '))
}

# ---- reader front-end (zrdr) -----------------------------------------------------------
# The reader files are nested token arrays, so this is a token scan, not a parse: count
# CALL_SEQUENCE occurrences and the NAME each carries, per SEQUENCE_DEFINITION block.

$rdr = Get-ChildItem $Extracted -Recurse -Filter *.json -File |
    Where-Object { $_.FullName -match '\\zrdr\\' }
$rdrHits = @($rdr | Where-Object { (Get-Content $_.FullName -Raw) -match 'CALL_SEQUENCE' })
$rdrCalls = 0
foreach ($f in $rdrHits) {
    $rdrCalls += ([regex]::Matches((Get-Content $f.FullName -Raw), '"CALL_SEQUENCE"')).Count
}
Write-Host ""
Write-Host "reader (zrdr): $($rdr.Count) files, $($rdrHits.Count) carry CALL_SEQUENCE, $rdrCalls occurrence(s)"
Write-Host ""
Write-Host "wrote $(Join-Path $Out 'call-graphs.json')"
