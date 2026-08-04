# Tests one hypothesis against every delta-bearing FROM_TO event census.ps1 found:
#
#     *_delta  ==  (channel.to - channel.from) / run_time
#
# i.e. the bare {x,y,z} vector is the sibling ABSOLUTE channel's per-second rate, carrying
# no information the tween does not already have — not an independent relative motion.
#
# Prints the worst per-component residual (absolute and relative to the channel's own span)
# and the count of events that fail a 1e-3 relative tolerance.
#
#   .\analysis\bl-050-fromto-delta\verify-rate.ps1 [-In .scratch\bl-050\delta-events.json]

[CmdletBinding()]
param(
    [string]$In = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-050\delta-events.json')
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture
$rows = Get-Content $In -Raw | ConvertFrom-Json

function F([double]$v) { $v.ToString('0.######', $inv) }
function Vec($v) { if ($null -eq $v) { $null } else { , @([double]$v.x, [double]$v.y, [double]$v.z) } }

$pairs = @(
    @{ abs = 'translate'; delta = 'translate_delta' },
    @{ abs = 'rotate';    delta = 'rotate_delta' },
    @{ abs = 'scale';     delta = 'scale_delta' }
)

$worstAbs = 0.0; $worstRel = 0.0; $fails = 0; $checked = 0; $noSibling = 0
foreach ($r in $rows) {
    foreach ($p in $pairs) {
        $d = Vec $r.($p.delta)
        if ($null -eq $d) { continue }
        $checked++
        $ch = $r.($p.abs)
        if ($null -eq $ch -or $null -eq $ch.from -or $null -eq $ch.to) {
            $noSibling++
            Write-Host "  NO SIBLING $($p.abs) channel: $($r.chapter) $($r.anim)/$($r.sequence) $($r.node)"
            continue
        }
        $from = Vec $ch.from; $to = Vec $ch.to
        $rt = [double]$r.run_time
        $bad = $false
        for ($i = 0; $i -lt 3; $i++) {
            $expect = if ($rt -gt 0) { ($to[$i] - $from[$i]) / $rt } else { 0.0 }
            $err = [Math]::Abs($d[$i] - $expect)
            $span = [Math]::Max([Math]::Abs($expect), 1e-6)
            $rel = $err / $span
            if ($err -gt $worstAbs) { $worstAbs = $err }
            if ($rel -gt $worstRel -and $err -gt 1e-9) { $worstRel = $rel }
            if ($rel -gt 1e-3 -and $err -gt 1e-6) { $bad = $true }
        }
        if ($bad) {
            $fails++
            Write-Host ("  MISMATCH {0,-4} {1}/{2} {3} {4}: delta=({5},{6},{7}) expected=({8},{9},{10}) rt={11}" -f `
                $r.chapter, $r.anim, $r.sequence, $r.node, $p.abs,
                (F $d[0]), (F $d[1]), (F $d[2]),
                (F (($to[0] - $from[0]) / $rt)), (F (($to[1] - $from[1]) / $rt)), (F (($to[2] - $from[2]) / $rt)), $rt)
        }
    }
}

Write-Host ""
Write-Host "delta channels checked : $checked"
Write-Host "without a sibling      : $noSibling"
Write-Host "mismatches (>1e-3 rel) : $fails"
Write-Host "worst absolute residual: $(F $worstAbs)"
Write-Host "worst relative residual: $(F $worstRel)"
