# Gaming mode's code: the marker, its expiry and tunables, the machine-wide lock, and the throttle a
# runner applies at each stage boundary. GamingMode.ps1 is the command line over it; see
# docs/tooling.md. Dot-sourced by MemoryLedger.ps1 (so by RunTests.ps1 and RunProbe.ps1), by
# FormatBeforeTests.ps1 and by GamingMode.ps1. It takes no parameters, so dot-sourcing binds nothing
# in the caller's scope.
#
# The switch is a marker file outside every worktree, not an environment variable: a running agent
# session never sees a variable set after it started. The tunables live in the marker.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

. (Join-Path $PSScriptRoot "HeldFile.ps1")

# CSVM_GAMING_MARKER is test-only: it moves the marker, and with it the lock and the waiter files,
# so a test never switches the user's machine into gaming mode. The engine's memory floor reads only
# the fixed path.
$GamingMarker = if ($env:CSVM_GAMING_MARKER) { $env:CSVM_GAMING_MARKER } else { Join-Path ([System.IO.Path]::GetTempPath()) "csvm-gaming" }
$GamingLock = "$GamingMarker.lock"
$GamingDefaultHours = 4
$GamingDefaults = [ordered]@{
    shards = 2; goldenWorkers = 1; dotnetCpus = 2; priority = "BelowNormal"; threads = 4; watchdogFactor = 3; maxWaitSec = 1800
}
# Idle is accepted but starves a run under a CPU-heavy game; BelowNormal is the default for that.
$GamingPriorities = @{ "Idle" = 0x40; "BelowNormal" = 0x4000; "Normal" = 0x20 }
$GamingOnWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
$script:GamingSaidOffWindows = $false

<#
.SYNOPSIS
The settings a marker's text describes, or $null once it has expired. Pure. A field missing or out
of range takes its default; a marker with no readable "until" expires 4 hours after it was written.
#>
function ConvertFrom-GamingMarker {
    param([string]$Text, [datetime]$WrittenAt, [datetime]$Now = (Get-Date), [int]$ProcessorCount = [Environment]::ProcessorCount)
    $doc = $null
    try { $doc = $Text | ConvertFrom-Json } catch { }
    $until = $WrittenAt.AddHours($GamingDefaultHours)
    if ($doc -and [string]$doc.until -eq "forever") {
        $until = $null
    } elseif ($doc -and $doc.until) {
        $parsed = [datetime]::MinValue
        if ([datetime]::TryParse([string]$doc.until, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed)) { $until = $parsed }
    }
    if ($until -and $Now -ge $until) { return $null }
    $s = [ordered]@{ Until = $until }
    foreach ($name in @("shards", "goldenWorkers", "dotnetCpus", "threads", "watchdogFactor", "maxWaitSec")) {
        $v = 0
        $s[$name] = if ($doc -and [int]::TryParse([string]$doc.$name, [ref]$v) -and $v -gt 0) { $v } else { $GamingDefaults[$name] }
    }
    $s.priority = if ($doc -and $GamingPriorities.ContainsKey([string]$doc.priority)) { [string]$doc.priority } else { $GamingDefaults.priority }
    $s.PriorityClass = [uint32]$GamingPriorities[$s.priority]
    # ponytail: one processor group (64 threads) assumed; the development machine has 16.
    $count = [math]::Min($ProcessorCount, 64)
    $s.threads = [math]::Min($s.threads, $count)
    $mask = [uint64]0
    for ($i = $count - $s.threads; $i -lt $count; $i++) { $mask = $mask -bor ([uint64]1 -shl $i) }
    $s.AffinityMask = $mask
    $s.ThreadsText = "$($count - $s.threads)-$($count - 1)"
    return [pscustomobject]$s
}

<#
.SYNOPSIS
The gaming-mode settings now, or $null when it is off. A marker that vanished mid-read (switched off)
reads as off. An expired marker reads as off and is deleted here, unless it was rewritten since it
was read, so the engine's memory floor, which checks only that the file exists, agrees. Off Windows
it is always off, and says so once when a marker is present.
#>
function Get-GamingMode {
    if (-not (Test-Path -LiteralPath $GamingMarker)) { return $null }
    if (-not $GamingOnWindows) {
        if (-not $script:GamingSaidOffWindows) { Write-Host "  gaming mode: a no-op off Windows" -ForegroundColor DarkGray; $script:GamingSaidOffWindows = $true }
        return $null
    }
    try {
        $written = (Get-Item -LiteralPath $GamingMarker -ErrorAction Stop).LastWriteTime
        $text = [System.IO.File]::ReadAllText($GamingMarker)
    } catch {
        if (-not (Test-Path -LiteralPath $GamingMarker)) { return $null }
        $written = Get-Date
        $text = ""
    }
    $s = ConvertFrom-GamingMarker -Text $text -WrittenAt $written
    if (-not $s) {
        $now = Get-Item -LiteralPath $GamingMarker -ErrorAction SilentlyContinue
        if ($now -and $now.LastWriteTime -eq $written) { Remove-Item -LiteralPath $GamingMarker -Force -ErrorAction SilentlyContinue }
    }
    return $s
}

# A launch watchdog in seconds, scaled by the watchdog factor while gaming mode is on. 0 (none) stays 0.
function Get-GamingTimeoutSec {
    param($Settings, [int]$Seconds)
    if ($Settings -and $Seconds -gt 0) { return $Seconds * $Settings.watchdogFactor }
    return $Seconds
}

function Format-GamingHolder {
    param($Held)
    if (-not $Held) { return "another run" }
    return "$($Held.worktree) (pid $($Held.pid)) since $($Held.since)"
}

function Get-GamingWaiters {
    $dir = Split-Path -Parent $GamingMarker
    $leaf = Split-Path -Leaf $GamingMarker
    if (-not (Test-Path -LiteralPath $dir)) { return @() }
    return @([System.IO.Directory]::GetFiles($dir, "$leaf.wait-*") | ForEach-Object { Read-HeldFile $_ } | Where-Object { $_ })
}

<#
.SYNOPSIS
One runner's gaming-mode state across its stages. Settings is the marker read at the last stage
boundary ($null when off), Used the last settings the run ran under, Deferred the DEFERRED line.
#>
function New-GamingRun {
    param([string]$Worktree)
    return [pscustomobject]@{ Worktree = $Worktree; Settings = $null; Used = $null; Lock = $null; Throttled = $false; WaitedSec = 0.0; Deferred = $null }
}

<#
.SYNOPSIS
A stage boundary: reads the marker. Off, it releases the lock and lifts the throttle. On, it takes
the machine-wide lock (waiting for it unless -NoWait) and throttles the run's job. Past the marker's
maxWaitSec it sets .Deferred; switched off while waiting, it proceeds unthrottled. The caller
releases the lock with Exit-GamingRun in a finally block.
#>
function Sync-GamingStage {
    param($Run, [switch]$NoWait)
    $Run.Settings = Get-GamingMode
    if ($Run.Settings -and -not $Run.Lock -and -not $NoWait) {
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $waiter = $null
        $lastWho = $null
        try {
            while ($true) {
                $since = (Get-Date).ToString("HH:mm")
                try {
                    $Run.Lock = Open-HeldFile -Path $GamingLock -Mode OpenOrCreate -Fields @{ worktree = $Run.Worktree; pid = $PID; since = $since }
                    break
                } catch { }
                $held = Read-HeldFile $GamingLock
                $who = Format-GamingHolder $held
                if ($watch.Elapsed.TotalSeconds -ge $Run.Settings.maxWaitSec) {
                    $Run.Deferred = "DEFERRED: gaming mode, lock held by $who"
                    Write-Host "  $($Run.Deferred)" -ForegroundColor Yellow
                    break
                }
                if (-not $waiter) {
                    $waiter = Open-HeldFile -Path "$GamingMarker.wait-$([guid]::NewGuid().ToString('N'))" -Mode CreateNew -Fields @{ worktree = $Run.Worktree; pid = $PID; since = $since }
                }
                # A holder that has not written its fields yet is not named until it has.
                if ($held -and $who -ne $lastWho) {
                    Write-Host "  waiting for gaming-mode lock, held by $who" -ForegroundColor Yellow
                    $lastWho = $who
                }
                # Jittered so waiters do not poll in step; no strict FIFO.
                Start-Sleep -Milliseconds (Get-Random -Minimum 1000 -Maximum 3000)
                $Run.Settings = Get-GamingMode
                if (-not $Run.Settings) {
                    Write-Host "  gaming mode switched off while waiting; proceeding unthrottled" -ForegroundColor DarkGray
                    break
                }
            }
        } finally {
            if ($waiter) { $waiter.Stream.Dispose() }
            $Run.WaitedSec += $watch.Elapsed.TotalSeconds
        }
    }
    if (-not $Run.Settings) { Exit-GamingRun $Run }
    if ($Run.Settings) { $Run.Used = $Run.Settings }
    $throttle = $Run.Settings -and -not $Run.Deferred
    # Set-RunJobThrottle is JobObject.ps1's; a caller without a job has nothing to throttle.
    if (($throttle -or $Run.Throttled) -and (Get-Command Set-RunJobThrottle -ErrorAction SilentlyContinue)) {
        if ($throttle) { Set-RunJobThrottle -PriorityClass $Run.Settings.PriorityClass -Affinity $Run.Settings.AffinityMask }
        else { Set-RunJobThrottle -PriorityClass 0 -Affinity 0 }
    }
    $Run.Throttled = [bool]$throttle
}

# Releases the lock, if held. Safe to call more than once.
function Exit-GamingRun {
    param($Run)
    if ($Run -and $Run.Lock) { $Run.Lock.Stream.Dispose(); $Run.Lock = $null }
}

# The summary line: what the run last ran under, or off.
function Format-GamingSummary {
    param($Run)
    $s = $Run.Used
    if (-not $s) { return "gaming mode: off" }
    $until = if ($s.Until) { "until $($s.Until.ToString('HH:mm'))" } else { "until switched off" }
    $waited = [TimeSpan]::FromSeconds([math]::Round($Run.WaitedSec))
    return ("gaming mode: ON ({0}), waited {1}:{2:00}, shards {3}, workers {4}, threads {5}" -f $until, [int][math]::Floor($waited.TotalMinutes), $waited.Seconds, $s.shards, $s.goldenWorkers, $s.ThreadsText)
}

<#
.SYNOPSIS
The dotnet arguments a throttled build or test takes: its MSBuild parallelism and, for a build,
private nodes and an in-process compiler, so every compile runs as a child at the throttled priority
and affinity rather than in a shared server. Empty when $Settings is $null.
#>
function Get-GamingDotnetArgs {
    param($Settings, [switch]$Build)
    if (-not $Settings) { return @() }
    $a = @("-m:$($Settings.dotnetCpus)")
    if ($Build) { $a += @("-nodeReuse:false", "-p:UseSharedCompilation=false") }
    return $a
}
