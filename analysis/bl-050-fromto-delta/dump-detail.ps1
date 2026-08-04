# Full payload dump of the delta-bearing FROM_TO events found by census.ps1, deduplicated
# to distinct authored signatures, with the absolute channels printed alongside so the
# question "what does a bare {x,y,z} delta compose ON TOP OF" is answerable from the data.
#
#   .\analysis\bl-050-fromto-delta\dump-detail.ps1 [-In .scratch\bl-050\delta-events.json]

[CmdletBinding()]
param(
    [string]$In = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-050\delta-events.json')
)

$ErrorActionPreference = 'Stop'
$rows = Get-Content $In -Raw | ConvertFrom-Json

function V($v) { if ($null -eq $v) { '-' } else { '({0:0.####},{1:0.####},{2:0.####})' -f $v.x, $v.y, $v.z } }
function Ch($c) { if ($null -eq $c) { 'null' } else { "from=$(V $c.from) to=$(V $c.to)" } }

function Sig($r) {
    "$($r.anim)|$($r.sequence)|$($r.node)|$($r.run_time)|$(Ch $r.translate)|$(Ch $r.rotate)|" +
    "$(Ch $r.scale)|$(V $r.translate_delta)|$(V $r.rotate_delta)|$(V $r.scale_delta)|$($r.event_index)"
}

$groups = $rows | Group-Object { Sig $_ } | Sort-Object { $_.Group[0].anim }
Write-Host "distinct authored signatures: $($groups.Count) (from $($rows.Count) events)"
Write-Host ""

foreach ($g in $groups) {
    $r = $g.Group[0]
    $where = ($g.Group | ForEach-Object { "$($_.chapter)/$($_.mission)" } | Sort-Object -Unique) -join ' '
    Write-Host "=== $($r.anim) / seq '$($r.sequence)' / node '$($r.node)'  (x$($g.Count): $where)"
    Write-Host "    anchor=$($r.anchor)  activation=$($r.activation)  run_time=$($r.run_time)  morph=$($r.morph)"
    Write-Host "    translate       : $(Ch $r.translate)"
    Write-Host "    rotate          : $(Ch $r.rotate)"
    Write-Host "    scale           : $(Ch $r.scale)"
    Write-Host "    translate_delta : $(V $r.translate_delta)"
    Write-Host "    rotate_delta    : $(V $r.rotate_delta)"
    Write-Host "    scale_delta     : $(V $r.scale_delta)"
    Write-Host "    context:"
    foreach ($c in $r.context) {
        $mark = if ($c.self) { '>>' } else { '  ' }
        Write-Host ("      {0} [{1}] start={2,-6} {3} {4}" -f $mark, $c.idx, $c.start, $c.kind, $c.name)
    }
    Write-Host ""
}
