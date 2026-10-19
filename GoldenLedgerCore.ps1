# The goldens ledger: the history of every golden move (an Enhanced FLAKE, a move that stayed
# moved, a re-render that wrote no hash or that a deferral stopped, a Faithful red), appended by
# RunTests.ps1's goldens stage so the moves outlive the run tree. GoldenLedger.ps1 is the command
# line over it; see docs/tooling.md.
#
# Two kinds of JSON line: one "run" line per goldens stage that judged shots outside a regeneration,
# so clean runs are counted too, and one "shot" line per move, naming its run. The nvidia-smi table
# is the bulk of a census (several KB), so only a run with a move carries it, and a run that moves
# many shots on purpose writes it once.
#
# The file is goldens-ledger.jsonl at the root of the main checkout, found through the git common
# directory as New-ItemId.ps1 finds its counter, so every worktree appends to the one file. The root
# is where perf-history.jsonl already keeps a history: CleanScratch.ps1 never sweeps it, a worktree
# removal never reaches it, and .gitignore keeps it untracked. %TEMP%, where the memory ledger keeps
# its live state, is not used: a history must not depend on the temp folder's cleanup.
#
# Takes no parameters, so dot-sourcing it binds nothing in the caller. Pure ASCII on purpose
# (PROJECT_CONTEXT.md); the file is read and written as UTF-8 without a BOM.

# CSVM_GOLDEN_LEDGER is test-only: GoldenLedger.ps1 -SelfTest points itself and its children at a
# private file with it. A tree git cannot resolve falls back to its own root, which goes with a
# worktree, so the fallback warns.
function Get-GoldenLedgerPath {
    param([string]$Root = $PSScriptRoot)
    if ($env:CSVM_GOLDEN_LEDGER) { return $env:CSVM_GOLDEN_LEDGER }
    $common = $null
    $saved = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $common = git -C $Root rev-parse --path-format=absolute --git-common-dir 2>$null
        if ($LASTEXITCODE -ne 0) { $common = $null }
    } catch {
        $common = $null
    } finally {
        $ErrorActionPreference = $saved
    }
    if ($common) { return Join-Path (Split-Path ([string]$common).Trim() -Parent) "goldens-ledger.jsonl" }
    $fallback = Join-Path $Root "goldens-ledger.jsonl"
    Write-Host "  WARN goldens ledger: git could not resolve the main checkout from $Root, writing to $fallback" -ForegroundColor Yellow
    return $fallback
}

# One complete line per write: the line is serialised first, then appended under a machine-wide
# mutex, so two batteries finishing at once neither interleave nor lose a line (File.AppendAllText
# opens the file without write sharing, and a second writer's open would throw). A mutex not won
# within the timeout throws rather than hold a battery; the caller warns.
function Add-GoldenLedgerEntry {
    param($Entry, [string]$Path = (Get-GoldenLedgerPath), [int]$TimeoutSec = 30)
    $line = (ConvertTo-Json -InputObject $Entry -Compress -Depth 4) + "`n"
    $m = New-Object System.Threading.Mutex($false, "Global\csvm-golden-ledger")
    $held = $false
    try {
        try { $held = $m.WaitOne($TimeoutSec * 1000) } catch [System.Threading.AbandonedMutexException] { $held = $true }
        if (-not $held) { throw "the ledger's mutex was not free within ${TimeoutSec}s" }
        [System.IO.File]::AppendAllText($Path, $line, (New-Object System.Text.UTF8Encoding($false)))
    } finally {
        if ($held) { $m.ReleaseMutex() }
        $m.Dispose()
    }
}

# The goldens stage's append: a failure warns and never decides the stage.
function Save-GoldenLedgerEntry {
    param($Entry, [string]$What)
    try {
        Add-GoldenLedgerEntry $Entry
    } catch {
        Write-Host "  WARN goldens ledger: $What not recorded in $(Get-GoldenLedgerPath): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# One shot line, naming its run line. A move with no re-render (a Faithful red, or a re-render a
# deferral stopped) passes no -Rerender, so its rerender is null, not an empty hash.
function New-GoldenLedgerShot {
    param($Run, [string]$Shot, [string]$Verdict, [string]$Pin, [string]$FirstHash, $Rerender, [string]$Diff, [string]$Failures)
    $entry = [ordered]@{
        kind = "shot"; run = $Run.run; time = (Get-Date -Format s); tree = $Run.tree; commit = $Run.commit
        shot = $Shot; verdict = $Verdict; pin = $Pin; firstHash = $FirstHash; rerender = $Rerender; diff = $Diff
    }
    $fields = ConvertTo-GoldenDiffFields $Diff
    foreach ($k in $fields.Keys) { $entry[$k] = $fields[$k] }
    $entry.failures = $Failures
    return $entry
}

# Get-GoldenFrameDiff's sentence as fields, so flips can be compared by region: pixel count, largest
# level difference and bounding box [x0, y0, x1, y1]. A diff that measured nothing has null fields.
function ConvertTo-GoldenDiffFields {
    param([string]$Diff)
    $fields = [ordered]@{ pixels = $null; maxLevel = $null; box = $null }
    if ($Diff -eq "pixel-identical") {
        $fields.pixels = 0; $fields.maxLevel = 0
    } elseif ($Diff -match '^(\d+) pixel\(s\) differ, at most (\d+) level\(s\), inside \((\d+),(\d+)\)-\((\d+),(\d+)\)$') {
        $fields.pixels = [int]$Matches[1]; $fields.maxLevel = [int]$Matches[2]
        $fields.box = @([int]$Matches[3], [int]$Matches[4], [int]$Matches[5], [int]$Matches[6])
    }
    return $fields
}

# Every parseable line; a line that does not parse (a hand edit) is skipped, never fatal.
function Read-GoldenLedger {
    param([string]$Path = (Get-GoldenLedgerPath))
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    return @(foreach ($line in [System.IO.File]::ReadAllLines($Path, (New-Object System.Text.UTF8Encoding($false)))) {
        if (-not $line.Trim()) { continue }
        try {
            $entry = $line | ConvertFrom-Json
            # PowerShell 7 reads an ISO time as a DateTime, which sorts and prints by culture.
            if ($entry.time -is [datetime]) {
                $entry.time = [regex]::Match($line, '"time"\s*:\s*"([^"]*)"').Groups[1].Value
            }
            $entry
        } catch { }
    })
}

# A run's census in one line: gaming mode, live memory reservations, other scripted CSVM Godots and
# the processes in nvidia-smi's table.
function Format-GoldenLedgerLoad {
    param($Run)
    if (-not $Run) { return "no census recorded" }
    $reservations = if ("$(@($Run.memory)[0])" -match '(\d+) live reservation') { $Matches[1] } else { "?" }
    $gpu = if (@($Run.gpu).Count -gt 0) {
        "$(@(@($Run.gpu) | Where-Object { $_ -match '^\|\s+\d+\s+\S+\s+\S+\s+\d+\s+(C|G|C\+G)\s' }).Count) GPU process(es)"
    } else { "no nvidia-smi" }
    return "$($Run.gaming); $reservations reservation(s), $(@($Run.godots).Count) CSVM Godot(s), $gpu"
}

# The ledger grouped by shot, most recent move first: how many flakes, moves that stayed,
# re-renders that wrote no hash, Faithful reds and re-renders a deferral stopped, then the shot's
# last $Recent moves, each with its diff and its run's load. A clean run judged shots, recorded no
# move and had no shot that failed to render.
function Format-GoldenLedger {
    param([string]$Path = (Get-GoldenLedgerPath), [int]$Recent = 5)
    $all = @(Read-GoldenLedger -Path $Path)
    $runs = @{}
    foreach ($r in @($all | Where-Object { $_.kind -eq "run" })) { $runs[[string]$r.run] = $r }
    $flips = @($all | Where-Object { $_.kind -eq "shot" })
    $moving = @{}
    foreach ($f in $flips) { $moving[[string]$f.run] = 1 }
    $clean = @($runs.Values | Where-Object { [int]$_.judged -gt 0 -and [int]$_.broken -eq 0 -and -not $moving.ContainsKey([string]$_.run) }).Count
    $lines = @("goldens ledger ${Path}: $($flips.Count) move(s) in $($runs.Count) run(s), $clean clean run(s)")
    if ($flips.Count -eq 0) { return $lines }
    $groups = @($flips | Group-Object -Property shot | ForEach-Object {
        [pscustomobject]@{ Shot = $_.Name; Flips = @($_.Group | Sort-Object -Property time -Descending) }
    } | Sort-Object -Property { $_.Flips[0].time } -Descending)
    $width = [math]::Max(4, ($groups | ForEach-Object { $_.Shot.Length } | Measure-Object -Maximum).Maximum)
    $lines += ("  {0}  flake  moved  unrendered  red  deferred  last" -f "shot".PadRight($width))
    foreach ($g in $groups) {
        $count = { param($v) @($g.Flips | Where-Object { $_.verdict -eq $v }).Count }
        $lines += ("  {0}  {1,5}  {2,5}  {3,10}  {4,3}  {5,8}  {6} {7}" -f $g.Shot.PadRight($width), (& $count "flake"), (& $count "moved"),
            (& $count "unrendered"), (& $count "red"), (& $count "deferred"), $g.Flips[0].time, $g.Flips[0].verdict)
        foreach ($f in @($g.Flips | Select-Object -First $Recent)) {
            $region = if ($null -ne $f.pixels -and $f.box) { "$($f.pixels) px, $($f.maxLevel) level(s), ($($f.box -join ','))" } else { "$($f.diff)" }
            $lines += "      $($f.time) $($f.verdict): $region; $(Format-GoldenLedgerLoad $runs[[string]$f.run])"
        }
        if ($g.Flips.Count -gt $Recent) { $lines += "      ... $($g.Flips.Count - $Recent) older" }
    }
    return $lines
}
