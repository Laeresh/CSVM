<#
The Enhanced vegetation budget: the frame
cost at one fixed worst-case view per chapter with a vegetation entry, under Faithful, Enhanced
with --vegetation-quality=original and Enhanced with =rich, which on C1 draws the canopy clumps. The
noclumps mode, asked for by name with -Modes, is rich with --canopy-clumps=off, the clumps' A/B, the
nomodels mode rich with --vegetation-models=off, the near models' A/B, the far mode rich with --vegetation-models=far, and
the nocover mode rich with --ground-cover=off, the ground cover's A/B. -ExtraViews takes "name=pos;lookat" entries (a chapter prefix
on the name, C1-crop) in place of the chapter views.

Every launch is --freecam at the chapter's pose below, --det (so no saved option reaches it and the
sway's clock is the sim clock), --perf --no-vsync --mute, at -Resolution, and quits on a
--screenshot at sim frame -Frames. Launches go through RunProbe.ps1, on the hidden desktop, and
interleave the modes inside each repeat so drift lands on every mode alike. The
first -Skip windows of each launch (the first draw's shader compiles and the build settling) are
dropped; each launch reports the median of its remaining windows, and each case the median and
the range of its launches.

The hidden desktop presents at the display's refresh even with max_fps=0, so frame_ms floors at
8.33 ms on a 120 Hz display; read gpu_ms, render_cpu_ms, setup_ms and proc_ms, and the counts.

C5 has five city views, run by name with the city modes: enhanced (View Distance at its default,
Far), vdnormal and vdunlimited (no clutter fade). FINDINGS.md holds their baseline. C5 refuses
every vegetation entry, so there the 3D Vegetation word reaches only the new city (CityModels.On,
Rich and a word other than off): nomodels is the city off, near, far, veryfar and extreme its reaches, and original
(Vegetation Quality Original) a second off. A --det enhanced launch draws the city at near.
Each case also reports the startup line's build times and the city models log line.

    $env:CSVM_DATA_ROOT = "Z:\CSVM"; .\analysis\veg-budget\measure.ps1 -Chapters C1,C3 -Launches 3
    .\analysis\veg-budget\measure.ps1 -Chapters C5-cb7,C5-cb1,C5-alt,C5-cm23 -Modes enhanced,vdnormal
    .\analysis\veg-budget\measure.ps1 -Chapters C5-cb1,C5-street,C5-alt,C5-cm23,C5-cb7 -Modes nomodels,near,far,extreme,original
#>
param(
    [string[]]$Chapters = @("C1", "C1B", "C2", "C3", "C4"),
    [string[]]$Modes = @("faithful", "original", "rich"),
    [int]$Launches = 3,
    [int]$Frames = 600,
    [int]$Skip = 2,
    [string]$Resolution = "5120x1440",
    [string]$Tag = "",
    # Never $Views: PowerShell names ignore case, and $views is the chapter table below.
    [string[]]$ExtraViews = @(),
    # Summarise the logs already in the scratch folder without launching anything.
    [switch]$SummaryOnly
)

$ErrorActionPreference = "Continue"  # native stderr lines must not abort the run (PS 5.1)
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scratch = Join-Path $repoRoot ".scratch\veg-budget"
$probe = Join-Path $repoRoot "scripts\RunProbe.ps1"
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

# Low over each chapter's densest listed vegetation, looking at it from 300 m off on both axes and
# 80 m up: C1's fir forest, C1B's bushes on its rock top, C2's palms and brush beside a suburb,
# C3's palm transition on a beach under the painted jungle, C4's firs along the railway.
$views = @{
    "C1"  = @("--pos=-8975,271,-5980", "--lookat=-9275,191,-6280")
    "C1B" = @("--pos=-7288,285,-5486", "--lookat=-7588,205,-5786")
    "C2"  = @("--pos=59,88,-2955", "--lookat=-241,8,-3255")
    "C3"  = @("--pos=-5439,80,-3077", "--lookat=-5739,0,-3377")
    "C4"  = @("--pos=-8957,827,-4918", "--lookat=-9257,747,-5218")
    # C5's city, asked for by name (the chapter is the text before the first '-'). Ground is y=5.
    # cb7: 120 m up over the unflagged cblock7 cells round (-3328,-3072), looking south. The large
    # western district lies under the fog volumes fvol6 and fvol31-34 (up to y=183), where a low
    # camera sees only the near-black whiteout, so its pose measures the fog. A pose here must not
    # print "camera state 3". cb1: 160 m up (towers reach 108 m) over the largest unflagged
    # cblock1 cells, X[-8704,-6656] Z[-12800,-11264]. alt: 450 m up over the unflagged cblock1 band,
    # looking north-west across the city to the horizon; the distant draw-call rule is read here. cm23:
    # CM23's PLAYER_INIT (C5/M03 objectives), the camera --freecam --mission=M03 starts on.
    "C5-cb7"  = @("--pos=-3328,125,-3840", "--lookat=-3328,5,-3440")
    "C5-cb1"  = @("--pos=-7124,165,-11732", "--lookat=-7424,5,-12032")
    "C5-alt"  = @("--pos=-5000,450,-7000", "--lookat=-9000,250,-11000")
    "C5-cm23" = @("--pos=-11061,400,-8370", "--lookat=-11061,400,-8371")
    # street: 45 m over the cblock1 street grid, looking 900 m down it at the near city.
    "C5-street" = @("--pos=-7306,50,-11700", "--lookat=-7306,40,-12600")
}
$modeArgs = @{
    "faithful" = @("--graphics=original")
    "original" = @("--graphics=enhanced", "--vegetation-quality=original")
    "rich"     = @("--graphics=enhanced", "--vegetation-quality=rich")
    "noclumps" = @("--graphics=enhanced", "--vegetation-quality=rich", "--canopy-clumps=off")
    "nomodels" = @("--graphics=enhanced", "--vegetation-quality=rich", "--vegetation-models=off")
    "nocover"  = @("--graphics=enhanced", "--vegetation-quality=rich", "--ground-cover=off")
    "far"      = @("--graphics=enhanced", "--vegetation-quality=rich", "--vegetation-models=far")
    "near"      = @("--graphics=enhanced", "--vegetation-quality=rich", "--vegetation-models=near")
    "veryfar"   = @("--graphics=enhanced", "--vegetation-quality=rich", "--vegetation-models=veryfar")
    "extreme"   = @("--graphics=enhanced", "--vegetation-quality=rich", "--vegetation-models=extreme")
    # Enhanced at each View Distance; on C5 these draw the city at near.
    "enhanced"    = @("--graphics=enhanced")
    "vdnormal"    = @("--graphics=enhanced", "--view-distance=normal")
    "vdunlimited" = @("--graphics=enhanced", "--view-distance=unlimited")
}
# Extra views replace the chapter ones: the name's text before the first '-' is the chapter.
if ($ExtraViews) {
    $views = @{}
    $Chapters = @()
    foreach ($v in $ExtraViews) {
        $name, $poses = $v -split "=", 2
        $pos, $look = $poses -split ";"
        $views[$name] = @("--pos=$pos", "--lookat=$look")
        $Chapters += $name
    }
}

if (-not $SummaryOnly) {
    for ($i = 0; $i -lt $Launches; $i++) {
        foreach ($ch in $Chapters) {
            foreach ($mode in $Modes) {
                $base = Join-Path $scratch "$Tag$ch-$mode-$i"
                $godotArgs = @("--freecam", "--chapter=$(($ch -split '-')[0])") + $views[$ch] + $modeArgs[$mode] +
                    @("--det", "--perf", "--no-vsync", "--mute", "--frames=$Frames", "--screenshot=$base.png")
                & $probe -TimeoutSec 300 -Resolution $Resolution -EngineArgs @("--log-file", "$base.log") @godotArgs | Out-Null
                Write-Host "$ch $mode $i exit $LASTEXITCODE"
            }
        }
    }
}

function Get-Median($xs) {
    $s = @($xs | Sort-Object)
    if ($s.Count -eq 0) { return [double]::NaN }
    if ($s.Count % 2) { return $s[($s.Count - 1) / 2] }
    return ($s[$s.Count / 2 - 1] + $s[$s.Count / 2]) / 2
}

function Get-Fields($text) {
    $w = @{}
    foreach ($tok in ($text -split '\s+')) {
        if ($tok -match '^([a-z0-9_]+)=([-\d.]+)$') { $w[$matches[1]] = [double]$matches[2] }
    }
    return $w
}

# Window terms, then the startup line's build times (one value per launch, not windowed).
$windowTerms = "gpu_ms", "render_cpu_ms", "setup_ms", "proc_ms", "draws", "prims", "nodes"
$startTerms = "total", "world", "clutter", "bind", "rest"
$terms = $windowTerms + $startTerms
$cityLines = @{}
"{0,-10} {1,-11} {2,3} " -f "ch", "mode", "n" + (($terms | ForEach-Object { "{0,24}" -f $_ }) -join "")
foreach ($ch in $Chapters) {
    foreach ($mode in $Modes) {
        $perLaunch = @{}
        foreach ($t in $terms) { $perLaunch[$t] = @() }
        $n = 0
        foreach ($log in Get-ChildItem $scratch -Filter "$Tag$ch-$mode-*.log" | Sort-Object Name) {
            $rows = @()
            $start = $null
            foreach ($line in Get-Content $log.FullName) {
                if ($line -match '\[world\] (city models: .+)$') { $cityLines[$mode] = $matches[1] }
                if ($line -match '\[perf\] startup (.+)$') { $start = Get-Fields $matches[1] }
                if ($line -notmatch '\[perf\] window (.+)$') { continue }
                $rows += Get-Fields $matches[1]
            }
            $rows = @($rows | Select-Object -Skip $Skip)
            if ($rows.Count -eq 0) { continue }
            $n++
            foreach ($t in $windowTerms) { $perLaunch[$t] += Get-Median ($rows | ForEach-Object { $_[$t] }) }
            if ($start) { foreach ($t in $startTerms) { $perLaunch[$t] += $start[$t] } }
        }
        # Each cell: the median of the launch medians, then their range.
        $cells = $terms | ForEach-Object {
            $v = $perLaunch[$_]
            $lo = ($v | Measure-Object -Minimum).Minimum
            $hi = ($v | Measure-Object -Maximum).Maximum
            "{0,24}" -f ("{0:0.###} ({1:0.###}-{2:0.###})" -f (Get-Median $v), $lo, $hi)
        }
        ("{0,-10} {1,-11} {2,3} " -f $ch, $mode, $n) + ($cells -join "")
    }
}
foreach ($mode in $Modes) { if ($cityLines[$mode]) { "{0,-11} {1}" -f $mode, $cityLines[$mode] } }
