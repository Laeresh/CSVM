# The machine-wide memory ledger every scripted Godot launch is admitted against, so concurrent
# sessions queue instead of running the machine out of RAM. Dot-sourced by RunTests.ps1,
# RunProbe.ps1, RunGame.ps1 and RunDev.ps1; it takes no parameters, so dot-sourcing binds nothing in
# the caller. The command line and the self-test are MemoryLedger.ps1's; see docs/tooling.md.
#
# Wait-QuietMachine (RunTests.ps1 -WaitQuiet) waits for every CSVM Godot on the machine to end.
#
# One reservation file per live launch in %TEMP%\csvm-mem, held open (delete-on-close) for the
# launch's lifetime. The open handle is the claim, as with the net-port slots: a dead holder frees
# its reservation with its process. A named semaphore would not; its count survives a crash.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

if (-not ([System.Management.Automation.PSTypeName]'CSVMMemLedger').Type) {
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class CSVMMemLedger
{
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
    }
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("psapi.dll", SetLastError=true)] static extern bool GetProcessMemoryInfo(IntPtr h, out PROCESS_MEMORY_COUNTERS c, uint cb);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint GetProcessId(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr h, out uint code);

    /// <summary>Physical memory available now, in bytes.</summary>
    public static ulong AvailableBytes()
    {
        MEMORYSTATUSEX m = new MEMORYSTATUSEX();
        m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        return GlobalMemoryStatusEx(ref m) ? m.ullAvailPhys : 0;
    }

    /// <summary>Peak private bytes (peak commit) of a process, readable after it exited while its
    /// handle is still open; 0 when unreadable.</summary>
    public static ulong PeakPrivateBytes(IntPtr process)
    {
        PROCESS_MEMORY_COUNTERS c;
        if (!GetProcessMemoryInfo(process, out c, (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS)))) { return 0; }
        return (ulong)c.PeakPagefileUsage;
    }

    public static bool HasExited(IntPtr process) { return WaitForSingleObject(process, 0) == 0; }
    public static int ExitCode(IntPtr process) { uint c; return GetExitCodeProcess(process, out c) ? (int)c : -1; }
    public static int ProcessId(IntPtr process) { return (int)GetProcessId(process); }
}
'@
}

$MemGB = 1073741824.0
$MemInv = [System.Globalization.CultureInfo]::InvariantCulture
# CSVM_MEM_DIR is test-only: -SelfTest points itself and its child at a private ledger with it.
$MemLedgerDir = if ($env:CSVM_MEM_DIR) { $env:CSVM_MEM_DIR } else { Join-Path ([System.IO.Path]::GetTempPath()) "csvm-mem" }
$MemHistoryFile = Join-Path $MemLedgerDir "history.json"
# Written beside the history for the engine's self-registration (Tooling/MemoryAdmission.cs).
$MemEstimatesFile = Join-Path $MemLedgerDir "estimates.json"
$MemMutexName = "Global\csvm-mem-admission"
# The held-file claim, and gaming mode, whose floor is the higher one while it is on.
. (Join-Path $PSScriptRoot "HeldFile.ps1")
. (Join-Path $PSScriptRoot "GamingModeCore.ps1")
# How long a launch may wait for memory before its run ends DEFERRED. Gaming mode's lock wait
# shares the outcome and has its own cap.
$MemMaxWaitSec = if ($env:CSVM_MEM_MAX_WAIT_SEC) { [int]$env:CSVM_MEM_MAX_WAIT_SEC } else { 1800 }
# A run that ended DEFERRED exits with this code: neither PASS (0) nor FAIL (1).
$MemDeferredExitCode = 3
# The engine's own refusal below the floor; must equal MemoryAdmission.TripwireExitCode.
$MemTripwireExitCode = 75
$MemHistoryDepth = 20
$MemEstimateMargin = 1.25
$MemRetrySec = 2
$MemUpdateSec = 30

# Every kind's estimate before it has any history, in GB. The one seed table: a kind with history
# is estimated from it, never below its seed. The seeds are measured peaks with headroom
# (analysis/shard-memory/FINDINGS.md); engine-shard is the heaviest full shard's, engine-headless the
# heaviest --headless shard's, which compiles no shader and uploads nothing.
$MemSeedGB = [ordered]@{
    "engine-shard"     = 7.0
    "engine-headless"  = 3.0
    "golden-shot"      = 5.5
    "perf"             = 5.5
    "hitch"            = 5.5
    "probe"            = 3.5
    "capture-enhanced" = 8.0
    "capture-xr"       = 8.5
}

<#
.SYNOPSIS
The ledger kind of a launch, from its CSVM user arguments. Mirrors MemoryAdmission.Kind in the engine.
#>
function Get-MemKind {
    param([string[]]$GodotArgs)
    $a = @($GodotArgs)
    if ($a | Where-Object { $_ -like "--run-tests*" }) { return "engine-shard" }
    if ($a | Where-Object { $_ -like "--xr*" }) { return "capture-xr" }
    if ($a -contains "--graphics=enhanced") { return "capture-enhanced" }
    if ($a -contains "--perf") { return "perf" }
    if ($a | Where-Object { $_ -like "--hitch-inject*" }) { return "hitch" }
    return "probe"
}

<#
.SYNOPSIS
Whether a launch drives and ends itself (--det, --run-tests, --frames, --shots, --screenshot), the
launches the ledger admits. Interactive play stays out of it. Mirrors MemoryAdmission.IsNonInteractive.
#>
function Test-MemNonInteractive {
    param([string[]]$GodotArgs)
    return [bool](@($GodotArgs) | Where-Object { $_ -eq "--det" -or $_ -like "--run-tests*" -or $_ -like "--frames=*" -or $_ -like "--shots=*" -or $_ -like "--screenshot=*" })
}

function Get-MemFloorGB {
    if (Get-GamingMode) { return 16.0 }
    return 8.0
}

# The admission rule, pure: a launch fits when what is available, less the growth every live
# reservation still has ahead of it, covers its own estimate plus the floor. With no live
# reservation only the floor is checked, never the estimate, so a grown estimate can never block
# everything; below the floor even an empty ledger waits.
function Test-MemAdmissible {
    param([double]$AvailableGB, [double[]]$OutstandingGB, [double]$EstimateGB, [double]$FloorGB, [int]$LiveCount)
    if ($LiveCount -eq 0) { return ($AvailableGB -ge $FloorGB) }
    $sum = 0.0
    foreach ($o in $OutstandingGB) { $sum += $o }
    return (($AvailableGB - $sum - $FloorGB) -ge $EstimateGB)
}

function Enter-MemMutex {
    $m = New-Object System.Threading.Mutex($false, $MemMutexName)
    try { $null = $m.WaitOne() } catch [System.Threading.AbandonedMutexException] { }
    return $m
}

function Exit-MemMutex {
    param($Mutex)
    $Mutex.ReleaseMutex()
    $Mutex.Dispose()
}

function Read-MemHistory {
    $h = @{}
    if (Test-Path -LiteralPath $MemHistoryFile) {
        try {
            $doc = [System.IO.File]::ReadAllText($MemHistoryFile) | ConvertFrom-Json
            foreach ($p in $doc.PSObject.Properties) { $h[$p.Name] = @($p.Value) }
        } catch { }
    }
    return $h
}

<#
.SYNOPSIS
The estimate per kind in GB: the highest of the kind's last 20 recorded peaks plus 25 percent,
never below the kind's seed.
#>
function Get-MemEstimates {
    param([hashtable]$History = (Read-MemHistory))
    $est = [ordered]@{}
    foreach ($kind in $MemSeedGB.Keys) { $est[$kind] = $MemSeedGB[$kind] }
    foreach ($kind in $History.Keys) {
        $peaks = @($History[$kind] | ForEach-Object { [double]$_.gb })
        if ($peaks.Count -gt 0) {
            $learned = [math]::Round(($peaks | Measure-Object -Maximum).Maximum * $MemEstimateMargin, 2)
            $est[$kind] = if ($MemSeedGB.Contains($kind)) { [math]::Max($learned, [double]$MemSeedGB[$kind]) } else { $learned }
        }
    }
    return $est
}

function Get-MemEstimateGB {
    param([string]$Kind)
    $est = Get-MemEstimates
    if ($est.Contains($Kind)) { return [double]$est[$Kind] }
    return [double]$MemSeedGB["probe"]
}

function Write-MemFile {
    param([string]$Path, [string]$Text)
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

<#
.SYNOPSIS
Records one finished launch's peak private bytes for its kind and rewrites the estimates the engine
reads. Keeps the last 20 per kind.
#>
function Add-MemHistory {
    param([string]$Kind, [double]$PeakGB, [string]$Worktree)
    $null = New-Item -ItemType Directory -Path $MemLedgerDir -Force
    $m = Enter-MemMutex
    try {
        $h = Read-MemHistory
        $list = @()
        if ($h.ContainsKey($Kind)) { $list = @($h[$Kind]) }
        $list += [pscustomobject]@{ gb = [math]::Round($PeakGB, 3); worktree = $Worktree; at = (Get-Date).ToString("s") }
        if ($list.Count -gt $MemHistoryDepth) { $list = $list[($list.Count - $MemHistoryDepth)..($list.Count - 1)] }
        $h[$Kind] = $list
        $doc = [ordered]@{}
        foreach ($k in ($h.Keys | Sort-Object)) { $doc[$k] = @($h[$k]) }
        Write-MemFile -Path $MemHistoryFile -Text (ConvertTo-Json -InputObject $doc -Depth 4)
        Write-MemFile -Path $MemEstimatesFile -Text (ConvertTo-Json -InputObject (Get-MemEstimates -History $h))
    } finally {
        Exit-MemMutex $m
    }
}

<#
.SYNOPSIS
Opens a held ledger file (reservation or waiter) under a fresh name (HeldFile.ps1): it lives
exactly as long as the handle, and nobody else can remove it while it is held.
#>
function Open-MemFile {
    param([string]$Prefix, [hashtable]$Fields)
    $null = New-Item -ItemType Directory -Path $MemLedgerDir -Force
    $path = Join-Path $MemLedgerDir ("{0}-{1}.json" -f $Prefix, [guid]::NewGuid().ToString("N"))
    return Open-HeldFile -Path $path -Mode CreateNew -Fields $Fields
}

# Every live ledger file of one prefix with its fields. A file whose holder is gone is deleted here
# (a holder that died before delete-on-close could act); a held one refuses the delete.
function Get-MemFiles {
    param([string]$Prefix)
    $out = @()
    if (-not (Test-Path -LiteralPath $MemLedgerDir)) { return $out }
    foreach ($f in [System.IO.Directory]::GetFiles($MemLedgerDir, "$Prefix-*.json")) {
        try { [System.IO.File]::Delete($f); continue } catch { }
        $fields = Read-HeldFile $f
        if ($fields) { $out += $fields }
    }
    return $out
}

# Live reservations with their current private bytes and the growth each still has ahead of it.
# A reservation not yet given a pid owes its whole estimate; one whose process has exited owes
# nothing, its memory is already back in "available".
function Get-MemReservations {
    $out = @()
    foreach ($r in (Get-MemFiles -Prefix "res")) {
        $private = 0.0
        $alive = $false
        if ([int]$r.pid -gt 0) {
            $p = Get-Process -Id ([int]$r.pid) -ErrorAction SilentlyContinue
            if ($p) { $alive = $true; $private = $p.PrivateMemorySize64 / $MemGB }
        }
        $owed = if ([int]$r.pid -le 0) { [double]$r.estimateGB } elseif ($alive) { [math]::Max(0.0, [double]$r.estimateGB - $private) } else { 0.0 }
        $out += [pscustomobject]@{
            Pid = [int]$r.pid; Kind = [string]$r.kind; EstimateGB = [double]$r.estimateGB
            Worktree = [string]$r.worktree; PrivateGB = $private; OwedGB = $owed
        }
    }
    return $out
}

# Available physical memory in GB. CSVM_MEM_AVAILABLE_GB is test-only: it replaces the machine's
# figure with a simulated one, less the private bytes of every live reservation, so the simulated
# machine fills as the launches grow.
function Get-MemAvailableGB {
    param([object[]]$Reservations = @())
    if ($env:CSVM_MEM_AVAILABLE_GB) {
        $used = 0.0
        foreach ($r in $Reservations) { $used += $r.PrivateGB }
        return [double]$env:CSVM_MEM_AVAILABLE_GB - $used
    }
    return [CSVMMemLedger]::AvailableBytes() / $MemGB
}

<#
.SYNOPSIS
One admission attempt under the machine-wide mutex, held across the check and the reservation
write only. Returns { Admitted; Reservation; EstimateGB; AdmissibleGB; Live }.
#>
function Request-MemAdmission {
    param([string]$Kind, [string]$Worktree)
    $m = Enter-MemMutex
    try {
        # Seeded on creation, so a launch the engine registers itself reads the same estimates.
        if (-not (Test-Path -LiteralPath $MemEstimatesFile)) {
            $null = New-Item -ItemType Directory -Path $MemLedgerDir -Force
            Write-MemFile -Path $MemEstimatesFile -Text (ConvertTo-Json -InputObject (Get-MemEstimates))
        }
        $live = @(Get-MemReservations)
        $est = Get-MemEstimateGB -Kind $Kind
        $floor = Get-MemFloorGB
        $avail = Get-MemAvailableGB -Reservations $live
        $owed = @($live | ForEach-Object { $_.OwedGB })
        $sum = 0.0
        foreach ($o in $owed) { $sum += $o }
        $result = [pscustomobject]@{
            Admitted = $false; Reservation = $null; EstimateGB = $est
            AdmissibleGB = [math]::Round($avail - $sum - $floor, 1); Live = $live
        }
        if (Test-MemAdmissible -AvailableGB $avail -OutstandingGB $owed -EstimateGB $est -FloorGB $floor -LiveCount $live.Count) {
            $result.Admitted = $true
            $result.Reservation = Open-MemFile -Prefix "res" -Fields ([ordered]@{
                pid = 0; kind = $Kind; estimateGB = $est; worktree = $Worktree })
        }
        return $result
    } finally {
        Exit-MemMutex $m
    }
}

<#
.SYNOPSIS
Names the launched process in its reservation, so admissions read its private bytes, and opens a
handle of the ledger's own on it, so its peak is still readable after the launcher closes its own.
A null reservation (an interactive launch) is ignored.
#>
function Set-MemReservationPid {
    param($Reservation, [int]$ProcessId)
    if (-not $Reservation) { return }
    $Reservation.Fields.pid = $ProcessId
    Write-HeldFields $Reservation
    $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($proc) {
        try { $null = $proc.Handle; $Reservation | Add-Member -NotePropertyName Process -NotePropertyValue $proc -Force } catch { }
    }
}

<#
.SYNOPSIS
Records the launch's peak private bytes and releases the reservation; call it once the process has
exited or been killed. A launch the engine refused below the floor records nothing, its peak is a
boot's, and an engine-shard or engine-headless peak is recorded only when the caller marks the
launch a shared shard of a complete-catalog run under the default shard plan (-LearnShardPeak),
since any other shard holds a different share of the catalog.
#>
function Close-MemReservation {
    param($Reservation, [switch]$LearnShardPeak)
    if (-not $Reservation) { return }
    $handle = [IntPtr]::Zero
    if ($Reservation.PSObject.Properties["Process"]) { $handle = $Reservation.Process.Handle }
    $record = $handle -ne [IntPtr]::Zero -and [CSVMMemLedger]::ExitCode($handle) -ne $MemTripwireExitCode -and
        ($LearnShardPeak -or $Reservation.Fields.kind -notin "engine-shard", "engine-headless")
    if ($record) {
        $peak = [CSVMMemLedger]::PeakPrivateBytes($handle) / $MemGB
        if ($peak -gt 0) {
            try { Add-MemHistory -Kind $Reservation.Fields.kind -PeakGB $peak -Worktree $Reservation.Fields.worktree } catch {
                Write-Host "  memory ledger: could not record the peak: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    }
    $Reservation.Stream.Dispose()
    if ($Reservation.PSObject.Properties["Process"]) { $Reservation.Process.Dispose() }
}

<#
.SYNOPSIS
For a script that starts one Godot itself (RunProbe.ps1, RunGame.ps1, RunDev.ps1): admits a
non-interactive launch (any launch with -Always), hands the child its reservation through
CSVM_MEM_RESERVATION and returns it. Returns $null for interactive play, which neither waits nor
registers. Past $MemMaxWaitSec it ends the calling script with $MemDeferredExitCode.
#>
function Request-MemLaunch {
    param([string[]]$GodotArgs, [string]$Label, [string]$Worktree, [switch]$Always)
    if (-not $Always -and -not (Test-MemNonInteractive $GodotArgs)) { return $null }
    $r = Wait-MemAdmission -Kind (Get-MemKind $GodotArgs) -Label $Label -Worktree $Worktree
    if (-not $r) { exit $MemDeferredExitCode }
    $env:CSVM_MEM_RESERVATION = $r.Path
    return $r
}

<#
.SYNOPSIS
The ledger as text lines: live reservations, waiters, and the estimate per kind.
#>
function Format-MemLedger {
    $inv = $MemInv
    $lines = @()
    $live = @(Get-MemReservations)
    $lines += [string]::Format($inv, "memory ledger {0}: {1:0.0} GB available, floor {2:0} GB, {3} live reservation(s)", $MemLedgerDir, (Get-MemAvailableGB -Reservations $live), (Get-MemFloorGB), $live.Count)
    foreach ($r in $live) {
        $lines += [string]::Format($inv, "  pid {0,-6} {1,-16} {2,5:0.0} of {3,4:0.0} GB  {4}", $r.Pid, $r.Kind, $r.PrivateGB, $r.EstimateGB, $r.Worktree)
    }
    foreach ($w in @(Get-MemFiles -Prefix "wait")) {
        $lines += [string]::Format($inv, "  waiting: {0} ({1}, needs {2:0.0} GB) from {3}, since {4}", $w.label, $w.kind, [double]$w.estimateGB, $w.worktree, $w.since)
    }
    $est = Get-MemEstimates
    $lines += "  estimates (GB): " + (($est.Keys | ForEach-Object { [string]::Format($inv, "{0} {1:0.0}", $_, $est[$_]) }) -join ", ")
    return $lines
}

<#
.SYNOPSIS
A launch waiting for admission. Step-MemWaiter tries at most every 2 s; the first refusal prints
the ledger and holds a waiter file, later ones print one line every 30 s, and past $MemMaxWaitSec
it sets .Deferred to the DEFERRED line.
#>
function New-MemWaiter {
    param([string]$Kind, [string]$Label, [string]$Worktree)
    return [pscustomobject]@{
        Kind = $Kind; Label = $Label; Worktree = $Worktree; Since = [System.Diagnostics.Stopwatch]::StartNew()
        LastTry = $null; LastLine = 0.0; Held = $null; Deferred = $null
    }
}

function Step-MemWaiter {
    param($Waiter)
    if ($Waiter.LastTry -and $Waiter.LastTry.Elapsed.TotalSeconds -lt $MemRetrySec) { return $null }
    $Waiter.LastTry = [System.Diagnostics.Stopwatch]::StartNew()
    $try = Request-MemAdmission -Kind $Waiter.Kind -Worktree $Waiter.Worktree
    $waited = $Waiter.Since.Elapsed.TotalSeconds
    if ($try.Admitted) {
        if ($Waiter.Held) {
            $Waiter.Held.Stream.Dispose()
            Write-Host ([string]::Format($MemInv, "  memory: {0} admitted after {1:0}s", $Waiter.Label, $waited)) -ForegroundColor DarkGray
        }
        return $try.Reservation
    }
    $need = [string]::Format($MemInv, "{0} needs {1:0.0} GB, {2:0.0} GB admissible", $Waiter.Label, $try.EstimateGB, $try.AdmissibleGB)
    if ($waited -ge $MemMaxWaitSec) {
        $held = @($try.Live | Group-Object Worktree | ForEach-Object { "$($_.Name) pids $((@($_.Group | ForEach-Object { $_.Pid })) -join ',')" }) -join "; "
        if (-not $held) { $held = "no CSVM launch, available memory is below the floor" }
        $Waiter.Deferred = "DEFERRED: memory, $need, held by $held"
        if ($Waiter.Held) { $Waiter.Held.Stream.Dispose() }
        Write-Host "  $($Waiter.Deferred)" -ForegroundColor Yellow
        return $null
    }
    if (-not $Waiter.Held) {
        $Waiter.Held = Open-MemFile -Prefix "wait" -Fields ([ordered]@{
            label = $Waiter.Label; kind = $Waiter.Kind; estimateGB = $try.EstimateGB; worktree = $Waiter.Worktree
            since = (Get-Date).ToString("s") })
        Write-Host "  memory: waiting, $need" -ForegroundColor Yellow
        foreach ($line in (Format-MemLedger)) { Write-Host "  $line" -ForegroundColor DarkGray }
        $Waiter.LastLine = $waited
    } elseif ($waited - $Waiter.LastLine -ge $MemUpdateSec) {
        Write-Host ([string]::Format($MemInv, "  memory: still waiting {0:0}s, {1}", $waited, $need)) -ForegroundColor Yellow
        $Waiter.LastLine = $waited
    }
    return $null
}

<#
.SYNOPSIS
Blocks until a launch is admitted and returns its reservation, or returns $null with the DEFERRED
line printed once $MemMaxWaitSec has passed. For a runner with one launch in flight.
#>
function Wait-MemAdmission {
    param([string]$Kind, [string]$Label, [string]$Worktree)
    $w = New-MemWaiter -Kind $Kind -Label $Label -Worktree $Worktree
    while ($true) {
        $r = Step-MemWaiter $w
        if ($r) { return $r }
        if ($w.Deferred) { return $null }
        Start-Sleep -Milliseconds 250
    }
}

<#
.SYNOPSIS
The scripted CSVM launches on this machine as { Pid; Kind; Worktree }: every Godot whose --path names
a folder called CSVM and that holds a live ledger reservation or carries a scripted flag
(Test-MemNonInteractive). Interactive play and an editor are left out: a wait on the user's own
game is gaming mode's business, and an editor idles. Kind is the reservation's, or "unledgered" (a
scripted launch nothing admitted). -Processes is test-only.
#>
function Get-CsvmGodots {
    param([object[]]$Processes = @(Get-CimInstance Win32_Process -Filter "Name LIKE 'Godot%'"))
    $kinds = @{}
    foreach ($r in @(Get-MemReservations)) { $kinds[$r.Pid] = $r.Kind }
    $out = @()
    foreach ($p in $Processes) {
        $cmd = [string]$p.CommandLine
        $reserved = $kinds.ContainsKey([int]$p.ProcessId)
        if (-not $reserved -and -not (Test-MemNonInteractive ($cmd -split '\s+'))) { continue }
        if ($cmd -notmatch '--path\s+(?:"([^"]+)"|(\S+))') { continue }
        $project = $(if ($Matches[1]) { $Matches[1] } else { $Matches[2] }).TrimEnd('\', '/')
        if ((Split-Path -Leaf $project) -ne "CSVM") { continue }
        $kind = if ($reserved) { $kinds[[int]$p.ProcessId] } else { "unledgered" }
        $out += [pscustomobject]@{ Pid = [int]$p.ProcessId; Kind = $kind; Worktree = (Split-Path -Parent $project) }
    }
    return $out
}

<#
.SYNOPSIS
Blocks until no scripted CSVM Godot runs on the machine (Get-CsvmGodots; the user's own play is
never waited on), naming each one waited on (pid, kind, worktree)
when the set of worktrees changes and every 60 s. Returns $true once quiet, $false past
$TimeoutSec. Admission already queues launches on memory; this waits out the CPU and GPU contention
of runs that fit in memory side by side. Call it before the caller starts a Godot of its own.
#>
function Wait-QuietMachine {
    param([int]$TimeoutSec)
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $lastTrees = $null
    $lastLine = -60.0
    while ($true) {
        $busy = @(Get-CsvmGodots)
        $waited = $watch.Elapsed.TotalSeconds
        if ($busy.Count -eq 0) {
            Write-Host ([string]::Format($MemInv, "  quiet: no CSVM Godot running, waited {0:0}s", $waited)) -ForegroundColor DarkGray
            return $true
        }
        $who = ($busy | ForEach-Object { "pid $($_.Pid) $($_.Kind) $($_.Worktree)" }) -join "; "
        if ($waited -ge $TimeoutSec) {
            Write-Host ([string]::Format($MemInv, "  DEFERRED: quiet, still {0} CSVM Godot(s) after {1:0}s: {2}", $busy.Count, $waited, $who)) -ForegroundColor Yellow
            return $false
        }
        $trees = (@($busy | ForEach-Object { $_.Worktree } | Sort-Object -Unique)) -join "|"
        if ($trees -ne $lastTrees -or $waited - $lastLine -ge 60) {
            Write-Host ([string]::Format($MemInv, "  quiet: waiting {0:0}s on {1} CSVM Godot(s): {2}", $waited, $busy.Count, $who)) -ForegroundColor Yellow
            $lastTrees = $trees
            $lastLine = $waited
        }
        Start-Sleep -Seconds 5
    }
}
