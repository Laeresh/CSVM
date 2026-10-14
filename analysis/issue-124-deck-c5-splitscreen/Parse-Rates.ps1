param([string] $Dir)
# One row per run: the [perf] rate windows after the session's first two windows (the load stall and one settling window) are dropped.
$rows = foreach ($f in Get-ChildItem -LiteralPath $Dir -Filter '*.log' | Sort-Object Name) {
    $lines = [IO.File]::ReadAllLines($f.FullName)
    $up = $false; $seen = 0; $fps = @()
    foreach ($l in $lines) {
        if ($l -match 'live respawn') { $up = $true; continue }
        if ($up -and $l -match 'rate fps=([0-9.]+)') {
            $seen++
            if ($seen -gt 2) { $fps += [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture) }
        }
    }
    $mean = if ($fps.Count) { ($fps | Measure-Object -Average).Average } else { 0 }
    $min = if ($fps.Count) { ($fps | Measure-Object -Minimum).Minimum } else { 0 }
    [pscustomobject]@{
        Run = $f.BaseName
        N = $fps.Count
        Mean = [Math]::Round($mean, 1)
        Ms = if ($mean) { [Math]::Round(1000 / $mean, 2) } else { 0 }
        Min = $min
        Windows = ($fps | ForEach-Object { $_.ToString('0.0', [Globalization.CultureInfo]::InvariantCulture) }) -join ' '
    }
}
$rows | Format-Table -AutoSize | Out-String -Width 250
