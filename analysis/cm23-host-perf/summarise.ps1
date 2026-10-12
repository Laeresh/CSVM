<#
Reads measure.ps1's host logs (.scratch\cm23-host-perf\<case>-<n>.out) and prints one row per
launch: the median of each [perf] window term after the first -Skip windows (the build settling),
the hitch count, and the lowest physics rate of any window (the sim clock lagging the wall clock
reads under 60 Hz). work_ms is frame_ms less idle_ms, the frame's cost below any present cap.
#>
param(
    [string[]]$Cases = @("1p-enh", "net-enh", "split-enh", "1p-orig", "net-orig", "split-orig"),
    [int]$Skip = 10,
    [string]$Tag = "",
    # Windows past this sim frame are left out, for a run whose seat crashed and went spectating.
    [int]$Until = [int]::MaxValue
)
$scratch = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) ".scratch\cm23-host-perf"
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
$terms = "frame_ms", "work_ms", "p95_ms", "proc_ms", "phys_tick_ms", "phys_hz", "defer_ms", "render_cpu_ms", "gpu_ms", "draws"

function Get-Median($xs) {
    $s = @($xs | Sort-Object)
    if ($s.Count -eq 0) { return [double]::NaN }
    if ($s.Count % 2) { return $s[($s.Count - 1) / 2] }
    return ($s[$s.Count / 2 - 1] + $s[$s.Count / 2]) / 2
}

"{0,-12} {1,-3} {2,4} " -f "case", "n", "win" + (($terms | ForEach-Object { "{0,13}" -f $_ }) -join "") + "{0,8}{1,9}" -f "hitch", "min_hz"
foreach ($case in $Cases) {
    foreach ($log in Get-ChildItem $scratch -Filter "$Tag$case-?.out" | Sort-Object Name) {
        $rows = @()
        foreach ($line in Get-Content $log.FullName) {
            if ($line -notmatch '\[perf\] window (.+)$') { continue }
            $w = @{}
            foreach ($tok in ($matches[1] -split '\s+')) {
                if ($tok -match '^([a-z0-9_]+)=([-\d.]+)$') { $w[$matches[1]] = [double]$matches[2] }
            }
            $w["work_ms"] = $w["frame_ms"] - $w["idle_ms"]
            $rows += $w
        }
        $rows = @($rows | Select-Object -Skip $Skip | Where-Object { $_["sim_frame"] -le $Until })
        $hitches = @(Select-String -Path $log.FullName -Pattern '\[perf\] hitch ').Count
        $minHz = ($rows | ForEach-Object { $_["phys_hz"] } | Measure-Object -Minimum).Minimum
        $n = $log.BaseName.Substring($Tag.Length + $case.Length + 1)
        $cells = $terms | ForEach-Object { $t = $_; "{0,13:N2}" -f (Get-Median ($rows | ForEach-Object { $_[$t] })) }
        ("{0,-12} {1,-3} {2,4} " -f $case, $n, $rows.Count) + ($cells -join "") + ("{0,8}{1,9:N1}" -f $hitches, $minHz)
    }
}
