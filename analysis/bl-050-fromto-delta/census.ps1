# Census of every OBJECT_MOTION_FROM_TO event in the install that carries a non-null
# *_delta channel (translate_delta / rotate_delta / scale_delta), plus the surrounding
# sequence so the semantics can be argued from the payload rather than guessed.
#
# Read-only. Writes a JSON dump + a summary to .scratch/; no game data is committed.
#
#   .\analysis\bl-050-fromto-delta\census.ps1 [-Extracted Z:\CSVM\extracted] [-Out .scratch\bl-050]

[CmdletBinding()]
param(
    [string]$Extracted = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) 'extracted'),
    [string]$Out = (Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent) '.scratch\bl-050')
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

$files = Get-ChildItem $Extracted -Recurse -Filter *.json -File |
    Where-Object { $_.FullName -match '\\(mis_anim|cam_anim)\\' }

Write-Host "scanning $($files.Count) compiled anim files under $Extracted"

# Cheap pre-filter: only files whose text carries a delta key with an object value.
$candidates = $files | Where-Object {
    (Get-Content $_.FullName -Raw) -match '"(translate|rotate|scale)_delta":\s*\{'
}
Write-Host "  $($candidates.Count) files carry a non-null delta"

$rows = @()
foreach ($f in $candidates) {
    $rel = $f.FullName.Substring($Extracted.Length).TrimStart('\')
    $j = Get-Content $f.FullName -Raw | ConvertFrom-Json
    $chapter = ($rel -split '\\')[0]
    $mission = ($rel -split '\\')[1]

    $seqs = @()
    if ($j.reset_state) { $seqs += [pscustomobject]@{ name = '<reset_state>'; events = $j.reset_state.events } }
    foreach ($s in $j.sequences) { $seqs += [pscustomobject]@{ name = $s.name; events = $s.events } }

    foreach ($s in $seqs) {
        for ($i = 0; $i -lt $s.events.Count; $i++) {
            $ev = $s.events[$i].data.ObjectMotionFromTo
            if (-not $ev) { continue }
            if (-not ($ev.translate_delta -or $ev.rotate_delta -or $ev.scale_delta)) { continue }

            # Neighbours: the two events either side, by kind + target, so "what the
            # surrounding events do" is on the record.
            $ctx = @()
            for ($k = [Math]::Max(0, $i - 2); $k -le [Math]::Min($s.events.Count - 1, $i + 2); $k++) {
                $d = $s.events[$k].data
                $kind = ($d.PSObject.Properties | Select-Object -First 1).Name
                $body = $d.$kind
                $nm = if ($body -is [string]) { $body } else { $body.name }
                $ctx += [pscustomobject]@{
                    idx = $k; start = $s.events[$k].start; kind = $kind; name = "$nm"
                    self = ($k -eq $i)
                }
            }

            $rows += [pscustomobject]@{
                chapter        = $chapter
                mission        = $mission
                file           = $rel
                anim           = $j.name
                anchor         = $j.anchor_name
                activation     = "$($j.activation)"
                sequence       = $s.name
                event_index    = $i
                node           = $ev.name
                run_time       = $ev.run_time
                morph          = $ev.morph
                translate      = $ev.translate
                rotate         = $ev.rotate
                scale          = $ev.scale
                translate_delta = $ev.translate_delta
                rotate_delta   = $ev.rotate_delta
                scale_delta    = $ev.scale_delta
                context        = $ctx
            }
        }
    }
}

$rows | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $Out 'delta-events.json') -Encoding utf8

Write-Host ""
Write-Host "delta-bearing FROM_TO events: $($rows.Count)"
Write-Host ("  translate_delta: {0}  rotate_delta: {1}  scale_delta: {2}" -f `
    (@($rows | Where-Object translate_delta).Count),
    (@($rows | Where-Object rotate_delta).Count),
    (@($rows | Where-Object scale_delta).Count))
Write-Host ""
$rows | Group-Object chapter | Sort-Object Name | ForEach-Object { Write-Host ("  {0,-5} {1}" -f $_.Name, $_.Count) }
Write-Host ""
$rows | ForEach-Object {
    $ch = @()
    if ($_.translate_delta) { $ch += "T({0:0.###},{1:0.###},{2:0.###})" -f $_.translate_delta.x, $_.translate_delta.y, $_.translate_delta.z }
    if ($_.rotate_delta)    { $ch += "R({0:0.####},{1:0.####},{2:0.####})" -f $_.rotate_delta.x, $_.rotate_delta.y, $_.rotate_delta.z }
    if ($_.scale_delta)     { $ch += "S({0:0.###},{1:0.###},{2:0.###})" -f $_.scale_delta.x, $_.scale_delta.y, $_.scale_delta.z }
    $abs = @()
    if ($_.translate) { $abs += 'translate' }
    if ($_.rotate)    { $abs += 'rotate' }
    if ($_.scale)     { $abs += 'scale' }
    Write-Host ("  {0,-4} {1,-26} {2,-18} {3,-16} rt={4,-6} abs=[{5}] {6}" -f `
        $_.chapter, $_.anim, $_.sequence, $_.node, $_.run_time, ($abs -join ','), ($ch -join ' '))
}
Write-Host ""
Write-Host "wrote $(Join-Path $Out 'delta-events.json')"
