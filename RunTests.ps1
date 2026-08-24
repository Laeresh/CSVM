<#
.SYNOPSIS
    The one verification entry point: build -> unit tests -> in-engine suites -> goldens,
    with a single summary block and a single exit code.

.DESCRIPTION
    Stages, run in order, each reported PASS / FAIL / SKIP / TODO:

      build    dotnet build CSVM/CSVM.sln. A failure stops the run: nothing downstream can
               say anything useful about a tree that does not compile.
      units    dotnet test CSVM/CSVM.sln -- the xUnit project. Built already by the build
               stage, so it runs --no-build; counts are read from a TRX log rather than
               scraped out of console text. A FAILED units stage does NOT stop the run (only
               a failed build does, above): engine, goldens and hitch all still launch, each
               scored from its own report so a red units row can never read as green in the
               summary block (BL-417).
      engine   Godot with --run-tests, the in-engine assertion suites. Windowed (never
               --headless: no shaders compile there, so a clean error screen would prove
               nothing) and with --log-file, which is what lets the harness screen native
               engine ERROR lines. --run-tests implies --det by itself.
      goldens  The golden-image tripwire: one Godot per shot in analysis/goldens/manifest.json,
               each a pinned --det capture, compared as md5 of the RAW pixel buffer (the engine
               prints it; a PNG's encoded bytes are not the picture). Runs as its own scripted
               pass rather than as an in-engine suite because the --run-tests harness completes
               inside one _Ready call and never yields a frame, so it cannot photograph anything.
               A mismatch names the shot and leaves the actual PNG and that shot's engine log in
               .scratch/goldens/. Each shot is launched under a per-shot timeout
               ($EngineTimeoutSec); a shot that exceeds it is killed, reported as FAIL, and has its
               evidence preserved -- it never silently hangs the suite (BL-320).
      hitch    Two scripted Godot launches -- a clean one and one carrying --hitch-inject=50@300
               -- that report whether the frame-hitch detector (HitchMonitor/HitchSidecar) still
               fires on a known stall and stays silent without one. Awareness-only for the same reason
               goldens is one: the detector only trips on a REAL rendered frame over wall time,
               which --run-tests's single, frame-free _Ready call cannot produce. Reads the
               printed "[perf] hitch ..." line and the .hitches.jsonl sidecar it names, and
               checks the record's C8 attribution closes over its own frame cost.
      perf     -Perf only. Every scenario in analysis/perf/scenarios.json, run under
               --det --perf --no-vsync for a fixed number of SIM frames (never wall seconds --
               the fixed clock advances one sim step per rendered frame, so a frame count is an
               exact amount of simulation). Each scenario is launched several times and the first
               launches are thrown away: a cold file cache reshapes a startup profile rather than
               scaling it. Medians of the kept windows land as one JSON line per scenario in the
               git-ignored perf-history.jsonl at the repo root. The stage records; it does not
               judge. A verdict needs -PerfCompare, which pairs this run against an earlier
               labelled one and prints the ratios.

    Exit code: 1 if any stage FAILED, 0 otherwise. A stage that skipped -- no game data, no
    Godot, -SkipUnits / -SkipEngine -- is NOT a failure, but it is printed as a skip and the
    summary names what went unchecked: "the data was not there" must never read as "the
    check held".

    Extracted game data is found through CSVM_DATA_ROOT by the engine and the unit tests
    alike, so this runs from a git worktree -- which has no extracted/, no tools/ and no
    CrimsonSkiesGame/ -- with that one variable pointed at the primary tree. Godot is
    resolved the same way RunGame.ps1 resolves it: this tree first, then CSVM_DATA_ROOT.

.PARAMETER Filter
    Substring filter on the in-engine suite names (-Filter weapons runs weapons-defs and
    weapons-fire). Engine stage only; the unit tests are unaffected -- use -SkipUnits.

.PARAMETER SkipUnits
    Skip the dotnet test stage.

.PARAMETER SkipEngine
    Skip the Godot --run-tests stage.

.PARAMETER SkipGoldens
    Skip the golden-image stage (it is the slow one -- one Godot launch per shot).

.PARAMETER RegenGoldens
    Re-render every shot and rewrite analysis/goldens/manifest.json with the hashes measured.
    Deliberately a separate switch and never automatic: the rewritten file is the review artifact,
    so a moved hash has to be explained in the commit that moves it.

.PARAMETER SkipHitch
    Skip the hitch-detector check (two scripted Godot launches).

.PARAMETER Perf
    Also run the perf stage: every scenario in analysis/perf/scenarios.json under
    --det --perf --no-vsync for a fixed number of SIM frames, one JSON record per scenario
    appended to the git-ignored perf-history.jsonl at the repo root. It measures and records;
    it never judges. A regression verdict comes from a paired A/B (-PerfCompare), never from a
    threshold: machine drift makes a committed number lie (verification METHOD-3, PERF-5).

.PARAMETER PerfLabel
    Tags this run's history records (default "run"). The A/B protocol is: label the baseline,
    flip the one line under test, rebuild, run again with -PerfCompare pointed at that label.

.PARAMETER PerfCompare
    After measuring, pair each scenario against the most recent record carrying this label and
    print the ratios, with the reason each awareness metric is not a verdict. The two builds'
    CSVM.dll hashes are compared too: identical hashes mean the run measured the same binary
    twice, which is a noise floor, not an A/B (METHOD-6).

.PARAMETER PerfFilter
    Substring filter on the perf scenario names.

.PARAMETER PerfIterations
    Launches per scenario, overriding the manifest. The first (manifest "warmups") are discarded:
    a cold OS file cache reshapes a startup profile instead of scaling it (PERF-7).

.PARAMETER PerfFrames
    Sim frames per launch, overriding the manifest.

.EXAMPLE
    .\RunTests.ps1
    Build, the unit tests, the in-engine suites, one summary block, one exit code.

.EXAMPLE
    .\RunTests.ps1 -Filter weapons -SkipUnits
    Build, then only the in-engine suites whose name contains "weapons".

.EXAMPLE
    .\RunTests.ps1 -Perf -PerfLabel base -SkipUnits -SkipEngine -SkipGoldens
    Measure the perf scenario set and file it under the label "base". Flip the one line under
    test, then repeat with -PerfLabel change -PerfCompare base for the paired verdict.

.EXAMPLE
    $env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'; .\RunTests.ps1
    The same run from a git worktree, reading the primary tree's data and Godot.
#>

[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$SkipUnits,
    [switch]$SkipEngine,
    [switch]$SkipGoldens,
    [switch]$RegenGoldens,
    [switch]$SkipHitch,
    [switch]$Perf,
    [string]$PerfLabel = "run",
    [string]$PerfCompare = "",
    [string]$PerfFilter = "",
    [int]$PerfIterations = 0,
    [int]$PerfFrames = 0
)

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$ScratchDir = Join-Path $RepoRoot ".scratch"
$Inv        = [System.Globalization.CultureInfo]::InvariantCulture
$EngineTimeoutSec = 300

# tools/ is git-ignored, so a git worktree checkout has no Godot. Fall back to the primary
# tree named by CSVM_DATA_ROOT -- the same env var the engine and the unit tests read for
# extracted/, so one `$env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'` makes a worktree runnable.
# The NON-console binary on purpose. Its console twin opens a "Godot Engine (Console)" window that
# takes the foreground, and a full run launches Godot ~19 times, so a test run repeatedly steals the
# desktop from whoever is using it. Measured over a 45-frame run: the console build held the
# foreground in 25 of 58 samples (the console window, the game window, and an untitled one between
# them); this build, 0 of 52. Nothing is lost by the swap because every stage already reads its
# results from --log-file and the JSON reports, never from stdout; anything that needs to be seen on
# the console is echoed from the log below. Launching it needs Invoke-Godot -- a GUI-subsystem
# binary neither blocks PowerShell nor keeps its output inside the caller's pipes.
$GodotRel = "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
$GodotExe = Join-Path $RepoRoot $GodotRel
if ((-not (Test-Path $GodotExe)) -and $env:CSVM_DATA_ROOT) {
    $GodotExe = Join-Path $env:CSVM_DATA_ROOT $GodotRel
}

# Every launch goes on a separate, never-displayed Windows desktop, so nothing this script starts can
# appear on screen at all. The engine hides its own window too, but only once _Ready knows the flags
# -- measured 1.0 s after the window appears, which is Godot booting, times ~19 launches. A window
# belongs to the desktop its process was started on, and that is fixed before the process runs.
# Falls back to the visible desktop rather than failing: the tests must still run where it is
# refused, just visibly.
. (Join-Path $PSScriptRoot "HiddenDesktop.ps1")
$HiddenDesktop = Open-HiddenDesktop

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $ScratchDir)) {
    $null = New-Item -ItemType Directory -Path $ScratchDir
}

# Runs Godot to completion and returns its exit code.
#
# The call operator cannot be used here: PowerShell only blocks on CONSOLE-subsystem executables, so
# `& $gui.exe` returns the instant the process starts and every stage would measure nothing.
#
# The stream redirection is not for capture -- it is what keeps the engine off the terminal. Started
# without std handles, the non-console binary calls AttachConsole(ATTACH_PARENT_PROCESS) and reopens
# stdout on CONOUT$, writing straight to the console screen buffer, past whatever the caller
# redirected; that is how a run can measure 0 captured lines while its text lands on screen anyway
# (SHELL-10). Handing the process real handles at creation leaves its own stdout alone. The files
# are only a post-mortem for a launch that dies before --log-file exists; the log is still the
# record every stage scores itself from.
#
# Start-Process cannot do the redirecting: with -RedirectStandard* it disposes the object it hands
# back, so $p.ExitCode reads as empty and every stage scores a green run as FAIL. Both pipes are
# drained asynchronously BEFORE the wait, or a chatty launch fills the ~4 KB buffer and deadlocks.
#
# When the hidden desktop is open every launch goes there instead, which is the same call with the
# std handles built by hand; the ProcessStartInfo path below is the fallback for a session that was
# refused one, and is what keeps this readable as a plain process launch.
#
# SHELL-1: the argument string is re-split by the callee, and this repo's path contains a space, so
# any argument carrying one is quoted here or Godot receives it split.
function Invoke-Godot {
    param(
        [Parameter(Mandatory=$true)][string[]]$Arguments,
        [int]$TimeoutSec = 0
    )
    $quoted = @()
    foreach ($a in $Arguments) {
        if ($a -match '\s' -and $a -notmatch '^".*"$') {
            # --flag=value with a space keeps the flag outside the quotes, or Godot reads the whole
            # token as one path.
            if ($a -match '^(--[^=]+)=(.*)$') { $quoted += ('{0}="{1}"' -f $Matches[1], $Matches[2]) }
            else { $quoted += ('"{0}"' -f $a) }
        } else {
            $quoted += $a
        }
    }
    # Beside this run's own --log-file, so a crash in shot 3 of 11 leaves its evidence next to
    # shot 3 rather than being overwritten by shot 4.
    $streamBase = Join-Path $ScratchDir "godot"
    for ($i = 0; $i -lt $Arguments.Count - 1; $i++) {
        if ($Arguments[$i] -eq "--log-file") {
            $streamBase = $Arguments[$i + 1]
        }
    }
    if ($HiddenDesktop) {
        # CreateProcess takes ONE command line and it must carry argv[0] itself.
        $cmdLine = ('"{0}" {1}' -f $GodotExe, ($quoted -join " "))
        return Invoke-OnHiddenDesktop -Exe $GodotExe -CommandLine $cmdLine `
                                      -WorkingDirectory $RepoRoot `
                                      -StdOut "$streamBase.out" -StdErr "$streamBase.err" `
                                      -TimeoutSec $TimeoutSec
    }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = $GodotExe
    $psi.Arguments              = ($quoted -join " ")
    $psi.WorkingDirectory       = $RepoRoot
    $psi.UseShellExecute        = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $outRead = $p.StandardOutput.ReadToEndAsync()
    $errRead = $p.StandardError.ReadToEndAsync()
    if ($TimeoutSec -gt 0 -and -not $p.WaitForExit($TimeoutSec * 1000)) {
        $p.Kill()
        $p.WaitForExit()
        $code = 124
    } else {
        $code = $p.ExitCode
    }
    [System.IO.File]::WriteAllText("$streamBase.out", $outRead.Result)
    [System.IO.File]::WriteAllText("$streamBase.err", $errRead.Result)
    return $code
}

$Stages   = New-Object System.Collections.ArrayList
$Unchecked = New-Object System.Collections.ArrayList

function Add-Stage {
    param(
        [string]$Name,
        [string]$Status,
        [double]$Seconds,
        [string]$Detail
    )
    $null = $Stages.Add([pscustomobject]@{
        Name    = $Name
        Status  = $Status
        Seconds = $Seconds
        Detail  = $Detail
    })
}

# Something a stage did NOT check, in the words a reader needs to not mistake it for a pass.
function Add-Unchecked {
    param([string]$What)
    $null = $Unchecked.Add($What)
}

function Format-Seconds {
    param([double]$Seconds)
    return $Seconds.ToString("0.0", $Inv)
}

function Write-Stage-Banner {
    param([string]$Text)
    Write-Host ""
    Write-Host "== $Text ==" -ForegroundColor Cyan
}

function Get-StatusColor {
    param([string]$Status)
    if ($Status -eq "PASS") { return "Green" }
    if ($Status -eq "FAIL") { return "Red" }
    if ($Status -eq "TODO")  { return "DarkYellow" }
    if ($Status -eq "REGEN") { return "DarkYellow" }
    return "Yellow"
}

# SHELL-2: a stray Godot from an earlier run poisons the next one's error census and its
# window. Two filters, deliberately: a leftover of THIS script -- this tree's project dir
# AND $Marker, an argument only this script's own launches carry, so one still alive is stuck --
# gets killed; any other Godot on this tree is only reported. A live playtest or another agent's
# session is not ours to kill, and "everything launched against this tree" catches both.
function Stop-StrayGodots {
    param([string]$Marker)
    $killed = 0
    $mine = @()
    $others = @()
    $godots = @(Get-CimInstance Win32_Process -Filter "Name LIKE 'Godot%'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.IndexOf($ProjectDir, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    foreach ($godot in $godots) {
        if ($godot.CommandLine.IndexOf($Marker, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $mine += $godot
        } else {
            $others += $godot
        }
    }
    foreach ($stray in $mine) {
        try {
            Stop-Process -Id $stray.ProcessId -Force -ErrorAction Stop
            $killed++
        } catch {
            Write-Host "  could not kill stray Godot pid $($stray.ProcessId): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    Write-Host "stray Godots killed: $killed" -ForegroundColor DarkGray
    foreach ($other in $others) {
        Write-Host "  another Godot is live on this tree, left alone: pid $($other.ProcessId) $($other.CommandLine)" -ForegroundColor Yellow
    }
}

# ---- build -------------------------------------------------------------------------------

Write-Stage-Banner "build"
$watch = [System.Diagnostics.Stopwatch]::StartNew()
# PowerShell 5.1 wraps a native command's stderr in ErrorRecords as soon as this script's own
# output is redirected -- a caller piping it into Select-String or a file is enough -- and under
# $ErrorActionPreference = "Stop" the first such line kills the run mid-stage, orphaning whatever
# it had launched. Every native call here is judged by its exit code, so they run non-terminating;
# cmdlets keep Stop, because a silently failed Remove-Item would score a stage from a stale file.
$ErrorActionPreference = "Continue"
dotnet build $Sln
$buildCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
$watch.Stop()
if ($buildCode -eq 0) {
    Add-Stage -Name "build" -Status "PASS" -Seconds $watch.Elapsed.TotalSeconds -Detail "dotnet build CSVM.sln"
} else {
    Add-Stage -Name "build" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail "dotnet build exited $buildCode"
}
$buildOk = ($buildCode -eq 0)

# ---- units -------------------------------------------------------------------------------

if ($SkipUnits) {
    Add-Stage -Name "units" -Status "SKIP" -Seconds 0 -Detail "-SkipUnits"
    Add-Unchecked "the unit tests did not run (-SkipUnits)"
} elseif (-not $buildOk) {
    Add-Stage -Name "units" -Status "SKIP" -Seconds 0 -Detail "build failed"
    Add-Unchecked "the unit tests did not run (the build failed)"
} else {
    Write-Stage-Banner "units (dotnet test)"
    $trxDir = Join-Path $ScratchDir "testresults"
    $trx    = Join-Path $trxDir "units.trx"
    if (Test-Path $trx) {
        Remove-Item -Path $trx -Force
    }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $ErrorActionPreference = "Continue"
    dotnet test $Sln --no-build --nologo --results-directory $trxDir --logger "trx;LogFileName=units.trx"
    $unitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $watch.Stop()

    # Counts come from the TRX, not from the console summary: one is a machine-readable
    # document, the other is localized prose.
    $total = -1; $passed = -1; $failed = -1; $skipped = -1
    if (Test-Path $trx) {
        try {
            [xml]$doc = Get-Content -Path $trx -Raw
            $counters = $doc.TestRun.ResultSummary.Counters
            $total    = [int]$counters.total
            $passed   = [int]$counters.passed
            $failed   = [int]$counters.failed
            $skipped  = $total - [int]$counters.executed
        } catch {
            Write-Host "  could not read $trx : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    if ($total -ge 0) {
        $detail = "$passed passed, $failed failed, $skipped skipped of $total"
    } else {
        $detail = "dotnet test exited $unitCode (no TRX written)"
    }
    if ($unitCode -eq 0 -and $failed -le 0) {
        Add-Stage -Name "units" -Status "PASS" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    } else {
        Add-Stage -Name "units" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    }
    if ($skipped -gt 0) {
        Add-Unchecked "$skipped unit test(s) SKIPPED, normally for missing extracted game data"
    }
}

# ---- engine ------------------------------------------------------------------------------

if ($SkipEngine) {
    Add-Stage -Name "engine" -Status "SKIP" -Seconds 0 -Detail "-SkipEngine"
    Add-Unchecked "the in-engine suites did not run (-SkipEngine)"
} elseif (-not $buildOk) {
    Add-Stage -Name "engine" -Status "SKIP" -Seconds 0 -Detail "build failed"
    Add-Unchecked "the in-engine suites did not run (the build failed)"
} elseif (-not (Test-Path $GodotExe)) {
    Add-Stage -Name "engine" -Status "SKIP" -Seconds 0 -Detail "Godot not found at $GodotExe"
    Add-Unchecked "the in-engine suites did not run: no Godot at $GodotExe (in a worktree, set `$env:CSVM_DATA_ROOT to the primary tree)"
} else {
    Write-Stage-Banner "engine (--run-tests)"
    Stop-StrayGodots -Marker "--run-tests"

    # Freeze workaround, as in RunGame.ps1/RunDev.ps1: the bundled SDL hangs the main thread
    # when a >255-button DirectInput device disconnects. Real pads still work via XInput.
    if (-not $env:SDL_JOYSTICK_DIRECTINPUT) {
        $env:SDL_JOYSTICK_DIRECTINPUT = "0"
    }

    # Stale outputs go first: a run that dies before writing its report must not be scored
    # from the previous run's numbers.
    $report    = Join-Path $ScratchDir "test-report.json"
    $engineLog = Join-Path $ScratchDir "run-tests-engine.log"
    if (Test-Path $report) {
        Remove-Item -Path $report -Force
    }
    if (Test-Path $engineLog) {
        Remove-Item -Path $engineLog -Force
    }

    $testArg = "--run-tests"
    if ($Filter) {
        $testArg = "--run-tests=$Filter"
    }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $ErrorActionPreference = "Continue"
    $engineCode = Invoke-Godot -Arguments @("--path", $ProjectDir, "--log-file", $engineLog,
                                             "res://scenes/Main.tscn", "--", $testArg) `
                               -TimeoutSec $EngineTimeoutSec
    $ErrorActionPreference = "Stop"
    $watch.Stop()

    # The non-console binary's stdout is redirected away from the terminal, so the suite table that
    # used to appear live is replayed from the log. Only the harness's own lines -- the per-suite
    # verdicts and the summary block -- not the whole world-build chatter, which stays in the log
    # for a post-mortem.
    if (Test-Path $engineLog) {
        foreach ($line in (Get-Content -Path $engineLog)) {
            if ($line -match '^\s*\[test\]|^\s*(PASS|FAIL|SKIP)\s') {
                Write-Host "  $line"
            }
        }
    }

    $ePassed = -1; $eFailed = -1; $eSkipped = -1
    $failedNames = @()
    $errorNote = ""
    if (Test-Path $report) {
        try {
            $json     = Get-Content -Path $report -Raw | ConvertFrom-Json
            $ePassed  = [int]$json.passed
            $eFailed  = [int]$json.failed
            $eSkipped = [int]$json.skipped
            $failedNames = @($json.suites | Where-Object { $_.status -eq "fail" } | ForEach-Object { $_.name })
            if (-not $json.engineErrors.screened) {
                $errorNote = "engine errors UNSCREENED"
            } elseif (@($json.engineErrors.unexpected).Count -gt 0 -or @($json.engineErrors.overCap).Count -gt 0) {
                $errorNote = "engine errors UNEXPECTED"
            } else {
                $errorNote = "engine errors clean"
            }
        } catch {
            Write-Host "  could not read $report : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    if ($engineCode -eq 124) {
        $detail = "Godot timed out after ${EngineTimeoutSec}s (exit 124); partial log at $engineLog"
    } elseif ($ePassed -ge 0) {
        $detail = "$ePassed passed, $eFailed failed, $eSkipped skipped; $errorNote"
        if ($failedNames.Count -gt 0) {
            $detail = "$detail [$($failedNames -join ', ')]"
        }
    } else {
        $detail = "Godot exited $engineCode with no report at $report"
    }
    if ($Filter) {
        $detail = "$detail; filter '$Filter'"
    }
    # No report means no suite ran, whatever the exit code says. Treating that as a pass would let a
    # run that never started read as a green one -- the same trap the SKIP rows exist to avoid.
    if ($ePassed -lt 0) {
        Add-Stage -Name "engine" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    } elseif ($engineCode -eq 0) {
        Add-Stage -Name "engine" -Status "PASS" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    } else {
        Add-Stage -Name "engine" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    }
    if ($eSkipped -gt 0) {
        Add-Unchecked "$eSkipped in-engine suite(s) SKIPPED, normally for missing extracted game data"
    }
    if ($errorNote -eq "engine errors UNSCREENED") {
        Add-Unchecked "native engine ERROR lines were not screened (no readable engine log)"
    }
}

# ---- goldens -----------------------------------------------------------------------------

$GoldenManifest = Join-Path $RepoRoot "analysis\goldens\manifest.json"
$GoldenDir      = Join-Path $ScratchDir "goldens"

function ConvertTo-JsonString {
    param([string]$Text)
    $sb = New-Object System.Text.StringBuilder
    $null = $sb.Append('"')
    foreach ($c in $Text.ToCharArray()) {
        switch ($c) {
            '"'  { $null = $sb.Append('\"') }
            '\'  { $null = $sb.Append('\\') }
            "`n" { $null = $sb.Append('\n') }
            "`r" { $null = $sb.Append('\r') }
            "`t" { $null = $sb.Append('\t') }
            default {
                if ([int]$c -lt 0x20) {
                    $null = $sb.Append(('\u{0:x4}' -f [int]$c))
                } else {
                    $null = $sb.Append($c)
                }
            }
        }
    }
    return $sb.Append('"').ToString()
}

# Re-emits the manifest in its one canonical shape. Every field is written from the parsed
# document, so a regeneration diff shows the hash lines and nothing else -- which is the whole
# reason regeneration is a deliberate switch: the diff IS the review.
function Write-GoldenManifest {
    param($Doc, [string]$Path)
    $lines = New-Object System.Collections.ArrayList
    $null = $lines.Add("{")
    $null = $lines.Add("  ""schema"": $([int]$Doc.schema),")
    $null = $lines.Add("  ""regenerate"": $(ConvertTo-JsonString $Doc.regenerate),")
    $null = $lines.Add("  ""readme"": $(ConvertTo-JsonString $Doc.readme),")
    $null = $lines.Add("  ""size"": $(ConvertTo-JsonString $Doc.size),")
    $null = $lines.Add("  ""gpu"": $(ConvertTo-JsonString $Doc.gpu),")
    $null = $lines.Add("  ""notes"": [")
    $notes = @($Doc.notes)
    for ($i = 0; $i -lt $notes.Count; $i++) {
        $comma = if ($i -eq $notes.Count - 1) { "" } else { "," }
        $null = $lines.Add("    $(ConvertTo-JsonString $notes[$i])$comma")
    }
    $null = $lines.Add("  ],")
    $null = $lines.Add("  ""shots"": [")
    $shots = @($Doc.shots)
    for ($i = 0; $i -lt $shots.Count; $i++) {
        $s = $shots[$i]
        $argJson = @($s.args | ForEach-Object { ConvertTo-JsonString $_ })
        $null = $lines.Add("    {")
        $null = $lines.Add("      ""name"": $(ConvertTo-JsonString $s.name),")
        $null = $lines.Add("      ""frame"": $([int]$s.frame),")
        $null = $lines.Add("      ""hash"": $(ConvertTo-JsonString $s.hash),")
        $null = $lines.Add("      ""exercises"": $(ConvertTo-JsonString $s.exercises),")
        $null = $lines.Add("      ""args"": [$($argJson -join ', ')]")
        $null = $lines.Add($(if ($i -eq $shots.Count - 1) { "    }" } else { "    }," }))
    }
    $null = $lines.Add("  ]")
    $null = $lines.Add("}")
    # UTF-8 without BOM, LF-terminated: this file is reviewed as a diff, so its bytes must not
    # move for reasons nobody asked for.
    $text = ($lines -join "`n") + "`n"
    [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

# The last non-empty line of a log -- often the only clue a silent death leaves (BL-039).
function Get-LastLogLine {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        return ""
    }
    $lines = @(Get-Content -Path $Path | Where-Object { $_.Trim().Length -gt 0 })
    if ($lines.Count -eq 0) {
        return ""
    }
    return $lines[$lines.Count - 1]
}

# Copies one attempt's full evidence (.log/.log.out/.log.err, and the PNG if one exists) into a
# dated failure folder, keyed by shot name and attempt number, so it survives both the next shot
# in this same loop and the next `RunTests.ps1` run -- both of which reuse $GoldenDir and would
# otherwise silently overwrite it. BL-039: an unreproduced silent exit-1 left nothing behind
# specifically because nothing preserved the one run that saw it.
function Save-GoldenFailureEvidence {
    param([string]$FailureDir, [string]$ShotName, [int]$Attempt, [string]$ShotLog, [string]$Png)
    $null = New-Item -ItemType Directory -Path $FailureDir -Force
    $prefix = Join-Path $FailureDir "$ShotName.attempt$Attempt"
    foreach ($pair in @(@($ShotLog, "$prefix.log"), @("$ShotLog.out", "$prefix.log.out"),
                        @("$ShotLog.err", "$prefix.log.err"), @($Png, "$prefix.png"))) {
        if (Test-Path $pair[0]) {
            Copy-Item -Path $pair[0] -Destination $pair[1] -Force
        }
    }
}

if ($SkipGoldens) {
    Add-Stage -Name "goldens" -Status "SKIP" -Seconds 0 -Detail "-SkipGoldens"
    Add-Unchecked "the golden-image shots did not run (-SkipGoldens): pixel regressions are not caught by this run"
} elseif (-not $buildOk) {
    Add-Stage -Name "goldens" -Status "SKIP" -Seconds 0 -Detail "build failed"
    Add-Unchecked "the golden-image shots did not run (the build failed)"
} elseif (-not (Test-Path $GodotExe)) {
    Add-Stage -Name "goldens" -Status "SKIP" -Seconds 0 -Detail "Godot not found at $GodotExe"
    Add-Unchecked "the golden-image shots did not run: no Godot at $GodotExe"
} elseif (-not (Test-Path $GoldenManifest)) {
    Add-Stage -Name "goldens" -Status "SKIP" -Seconds 0 -Detail "no manifest at $GoldenManifest"
    Add-Unchecked "the golden-image shots did not run: no manifest at $GoldenManifest"
} else {
    Write-Stage-Banner $(if ($RegenGoldens) { "goldens (regenerating)" } else { "goldens" })
    # Every launch here carries the .scratch\goldens output path, an argument nothing but this
    # stage passes -- so the kill cannot reach a live playtest or a hand-run capture.
    Stop-StrayGodots -Marker "\.scratch\goldens\"
    if (-not $env:SDL_JOYSTICK_DIRECTINPUT) {
        $env:SDL_JOYSTICK_DIRECTINPUT = "0"
    }
    if (-not (Test-Path $GoldenDir)) {
        $null = New-Item -ItemType Directory -Path $GoldenDir
    }

    # .NET's reader, not Get-Content: PS 5.1 decodes a BOM-less file as the system ANSI codepage,
    # which turns every em-dash in the manifest's prose into mojibake the moment it is written back.
    $manifest = [System.IO.File]::ReadAllText($GoldenManifest) | ConvertFrom-Json
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $moved      = @()      # shots whose hash differs from the manifest
    $broken     = @()      # shots that did not render, or rendered the wrong frame/size
    $retried    = @()      # shots that died silently and were re-run once with --verbose
    $adapters   = @{}
    # Lazily created: most runs never touch it, and BL-039's whole point is that a silent
    # exit-1 must leave something behind instead of being overwritten by the next shot or run.
    $failureRoot  = $null
    $failureStamp = Get-Date -Format "yyyyMMdd-HHmmss"
    foreach ($shot in @($manifest.shots)) {
        $png = Join-Path $GoldenDir "$($shot.name).png"
        $shotLog = Join-Path $GoldenDir "$($shot.name).log"
        foreach ($stale in @($png, $shotLog, "$shotLog.out", "$shotLog.err")) {
            if (Test-Path $stale) {
                Remove-Item -Path $stale -Force
            }
        }

        $attempt = 1
        $verbose = $false
        $hash = ""; $size = ""; $gpu = ""; $simFrame = -1
        while ($true) {
            $shotArgs = @($shot.args) + @("--frames=$([int]$shot.frame)", "--screenshot=$png")
            $godotArgs = @("--path", $ProjectDir, "--log-file", $shotLog)
            if ($verbose) {
                # Godot's own engine flag, so it must sit before the "--" that hands the rest to
                # the game's arg parser -- inside $shotArgs it would just be an unread user arg.
                $godotArgs += "--verbose"
            }
            $ErrorActionPreference = "Continue"
            $shotCode = Invoke-Godot ($godotArgs + @("res://scenes/Main.tscn", "--") + $shotArgs) `
                                      -TimeoutSec $EngineTimeoutSec
            $ErrorActionPreference = "Stop"

            # BL-320: a timeout is deterministic, not the transient silent-exit-1 BL-039 guards
            # against -- retrying a hung shot with --verbose just doubles its wall-clock (every
            # hung shot would then eat $EngineTimeoutSec twice). Short-circuit: preserve the
            # evidence, break out, and let the no-PNG block below report it once.
            if ($shotCode -eq 124) {
                if ($failureRoot -eq $null) {
                    $failureRoot = Join-Path $ScratchDir "goldens-failures\$failureStamp"
                }
                Save-GoldenFailureEvidence -FailureDir $failureRoot -ShotName $shot.name `
                    -Attempt $attempt -ShotLog $shotLog -Png $png
                break
            }

            # SHOT-10: --screenshot exits 0 even when the save fails, so the file's existence is
            # the only proof it wrote anything.
            $hasPng = Test-Path $png
            $hash = ""; $size = ""; $gpu = ""; $simFrame = -1
            if ($hasPng -and (Test-Path $shotLog)) {
                foreach ($line in (Get-Content -Path $shotLog)) {
                    if ($line -match 'shot pixmd5=(\w+) size=(\S+) gpu=(.*)$') {
                        $hash = $Matches[1]; $size = $Matches[2]; $gpu = $Matches[3].Trim()
                    }
                    if ($line -match 'screenshot saved: .* sim_frame=(\d+)') {
                        $simFrame = [int]$Matches[1]
                    }
                }
            }

            # BL-039: a golden shot exiting nonzero with no PNG at all, silently, is the
            # unreproduced symptom this item exists for -- not the instant concurrent-run
            # collision (LOG-13, ~0.9s), which this loop's own Stop-StrayGodots already guards
            # against. One retry with the engine's own --verbose before giving up, and every
            # attempt's evidence is preserved -- it used to be overwritten by the very next shot.
            $silentDeath = ((-not $hasPng) -and $shotCode -ne 0)
            if ($silentDeath) {
                if ($failureRoot -eq $null) {
                    $failureRoot = Join-Path $ScratchDir "goldens-failures\$failureStamp"
                }
                Save-GoldenFailureEvidence -FailureDir $failureRoot -ShotName $shot.name `
                    -Attempt $attempt -ShotLog $shotLog -Png $png
                $lastLine = Get-LastLogLine -Path $shotLog
                Add-Content -Path (Join-Path $failureRoot "report.txt") -Value (
                    "$($shot.name) attempt $attempt : exit=$shotCode png=$hasPng " +
                    "last-log-line: $lastLine")
                if ($attempt -eq 1) {
                    $retried += $shot.name
                    Write-Host "  RETRY $($shot.name): exited $shotCode with no PNG -- re-running with --verbose" -ForegroundColor DarkYellow
                    $attempt++
                    $verbose = $true
                    continue
                }
            }
            break
        }

        if (-not (Test-Path $png)) {
            if ($shotCode -eq 124) {
                $detail = "$($shot.name): timed out after ${EngineTimeoutSec}s (Godot exited 124) -- see $shotLog"
            } else {
                $detail = "$($shot.name): no PNG written (Godot exited $shotCode) -- see $shotLog"
            }
            if ($failureRoot) { $detail = "$detail; evidence preserved in $failureRoot" }
            $broken += $detail
            Write-Host "  FAIL $($shot.name): no PNG at $png" -ForegroundColor Red
            continue
        }
        if ($hash.Length -eq 0) {
            $broken += "$($shot.name): no 'shot pixmd5=' line -- see $shotLog"
            Write-Host "  FAIL $($shot.name): the run printed no pixel hash" -ForegroundColor Red
            continue
        }
        $adapters[$gpu] = 1
        # A shot that photographed a different sim frame is a clock regression, not a pixel one,
        # and reads as neither if it is folded into the hash compare.
        if ($simFrame -ne [int]$shot.frame) {
            $broken += "$($shot.name): captured sim_frame=$simFrame, manifest says $([int]$shot.frame)"
            Write-Host "  FAIL $($shot.name): sim_frame=$simFrame, expected $([int]$shot.frame)" -ForegroundColor Red
            continue
        }
        if ($size -ne $manifest.size) {
            $broken += "$($shot.name): rendered $size, manifest hashes are $($manifest.size)"
            Write-Host "  FAIL $($shot.name): rendered $size, expected $($manifest.size)" -ForegroundColor Red
            continue
        }
        if ($hash -eq $shot.hash) {
            Write-Host "  ok   $($shot.name)  $hash" -ForegroundColor DarkGray
        } else {
            $moved += "$($shot.name) $($shot.hash) -> $hash ($png)"
            $color = if ($RegenGoldens) { "DarkYellow" } else { "Red" }
            Write-Host "  MOVED $($shot.name): $($shot.hash) -> $hash" -ForegroundColor $color
            Write-Host "        actual image: $png" -ForegroundColor $color
        }
        $shot.hash = $hash
    }
    $watch.Stop()
    if ($retried.Count -gt 0) {
        Add-Unchecked "$($retried.Count) golden shot(s) died silently on the first attempt and were retried with --verbose: $($retried -join ', ') -- evidence preserved in $failureRoot"
    }

    # The one legitimate reason for every hash to move at once. Printed whether or not anything
    # failed, because "the adapter also changed" is the difference between regenerate and
    # stop-the-line.
    $liveGpu = @($adapters.Keys) -join " + "
    if ($liveGpu -and $liveGpu -ne $manifest.gpu) {
        Write-Host "  GPU CHANGED: manifest '$($manifest.gpu)', this run '$liveGpu'" -ForegroundColor Yellow
        Write-Host "        a driver or card change legitimately moves every hash -- regenerate and say so in the commit" -ForegroundColor Yellow
        $manifest.gpu = $liveGpu
    }

    $shotCount = @($manifest.shots).Count
    if ($RegenGoldens) {
        Write-GoldenManifest -Doc $manifest -Path $GoldenManifest
        $detail = "regenerated $shotCount shot(s), $($moved.Count) hash(es) changed -> $GoldenManifest"
        if ($broken.Count -gt 0) {
            Add-Stage -Name "goldens" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail "$detail; $($broken.Count) shot(s) did not render"
            foreach ($b in $broken) {
                Write-Host "  !! $b" -ForegroundColor Red
            }
        } else {
            Add-Stage -Name "goldens" -Status "REGEN" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
        }
        Add-Unchecked "goldens were REGENERATED, not checked -- review the manifest diff and name the moved shots in the commit"
    } elseif ($moved.Count -eq 0 -and $broken.Count -eq 0) {
        Add-Stage -Name "goldens" -Status "PASS" -Seconds $watch.Elapsed.TotalSeconds -Detail "$shotCount shot(s) hash-identical; gpu $liveGpu"
    } else {
        $names = @($moved | ForEach-Object { ($_ -split ' ')[0] }) + @($broken | ForEach-Object { ($_ -split ':')[0] })
        Add-Stage -Name "goldens" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds `
            -Detail "$($moved.Count) moved, $($broken.Count) broken of $shotCount [$($names -join ', ')]; actual images in $GoldenDir"
        foreach ($m in $moved) {
            Write-Host "  !! moved: $m" -ForegroundColor Red
        }
        foreach ($b in $broken) {
            Write-Host "  !! $b" -ForegroundColor Red
        }
    }
}

# ---- hitch ---------------------------------------------------------------------------------

# HitchMonitor only trips on a real rendered frame measured over wall time
# (Launcher._Process), so this cannot be a --run-tests suite: that harness runs every suite to
# completion inside one _Ready call and never yields a frame (same reason goldens is a scripted
# pass, above). Two launches instead, mirroring the goldens/perf shape: one clean, one carrying a
# known --hitch-inject= stall, each read back through its own engine --log-file plus the
# .hitches.jsonl sidecar HitchSidecar writes beside the PROJECT's own log (Log.SinkPath) -- not
# Godot's --log-file, which is a different file; the sidecar's path is recovered from the
# "[core] log file=..." line every session prints once at Log.Open.
if ($SkipHitch) {
    Add-Stage -Name "hitch" -Status "SKIP" -Seconds 0 -Detail "-SkipHitch"
    Add-Unchecked "the hitch-detector check did not run (-SkipHitch)"
} elseif (-not $buildOk) {
    Add-Stage -Name "hitch" -Status "SKIP" -Seconds 0 -Detail "build failed"
    Add-Unchecked "the hitch-detector check did not run (the build failed)"
} elseif (-not (Test-Path $GodotExe)) {
    Add-Stage -Name "hitch" -Status "SKIP" -Seconds 0 -Detail "Godot not found at $GodotExe"
    Add-Unchecked "the hitch-detector check did not run: no Godot at $GodotExe"
} else {
    Write-Stage-Banner "hitch (--hitch-inject=)"
    Stop-StrayGodots -Marker "\.scratch\hitchcheck\"
    if (-not $env:SDL_JOYSTICK_DIRECTINPUT) {
        $env:SDL_JOYSTICK_DIRECTINPUT = "0"
    }

    $HitchDir = Join-Path $ScratchDir "hitchcheck"
    if (-not (Test-Path $HitchDir)) {
        $null = New-Item -ItemType Directory -Path $HitchDir
    }

    # Runs one launch, then reads back its engine log for the sidecar path (the "[core] log
    # file=..." line -- Log.Info reaches GD.Print, so it lands in Godot's own --log-file exactly
    # like every "[perf] window ..." line the perf stage below already parses the same way) and
    # every "[perf] hitch ..." summary line HitchSidecar.WriteLogLine emits.
    #
    # --screenshot= is not optional here: --frames=N alone never quits the process -- the only
    # quit is CaptureDirector's, gated on a pending shot (CaptureDirector.cs:126) -- so a launch
    # with --frames= and no --screenshot= just runs forever. This is exactly what goldens/perf
    # already do; it was the one thing dropped when this stage first landed, and it hung the
    # whole RunTests.ps1 run for as long as nobody killed the orphaned Godot by hand.
    function Read-HitchRun {
        param([string]$RunName, [string[]]$RunArgs, [int]$ExpectFrame)
        $log = Join-Path $HitchDir "$RunName.log"
        $png = Join-Path $HitchDir "$RunName.png"
        foreach ($stale in @($log, "$log.out", "$log.err", $png)) {
            if (Test-Path $stale) {
                Remove-Item -Path $stale -Force
            }
        }
        $fullArgs = $RunArgs + @("--frames=$ExpectFrame", "--screenshot=$png")
        $ErrorActionPreference = "Continue"
        $code = Invoke-Godot (@("--path", $ProjectDir, "--log-file", $log,
                                "res://scenes/Main.tscn", "--") + $fullArgs)
        $ErrorActionPreference = "Stop"
        $sinkPath = $null
        $hitchLines = @()
        $simFrame = -1
        if (Test-Path $log) {
            foreach ($line in (Get-Content -Path $log)) {
                if ($line -match '\[core\] log file=(\S+) mode=') {
                    $sinkPath = $Matches[1]
                }
                if ($line -match '\[perf\] hitch frame=(\d+) frame_ms=([\d.]+)') {
                    $hitchLines += [pscustomobject]@{ Frame = [int]$Matches[1]; FrameMs = [double]$Matches[2] }
                }
                if ($line -match 'screenshot saved: .* sim_frame=(\d+)') {
                    $simFrame = [int]$Matches[1]
                }
            }
        }
        return [pscustomobject]@{
            Name = $RunName; ExitCode = $code; Log = $log; SinkPath = $sinkPath
            HitchLines = $hitchLines; SimFrame = $simFrame; ExpectFrame = $ExpectFrame
        }
    }

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $problems = @()

    # Clean run: no injected stall, well clear of the grace window (--frames=180 was measured
    # landing B6 to write nothing but the sidecar's own BOM). Decision 13 says a clean run must
    # stay silent -- a detector that fires on nothing would read as a monitor that fires on
    # everything, and this is the only check here that would catch that.
    $clean = Read-HitchRun -RunName "clean" -RunArgs @("--det", "--no-vsync", "--mute") -ExpectFrame 180
    if ($clean.SimFrame -ne $clean.ExpectFrame) {
        $problems += "clean: ran to sim_frame=$($clean.SimFrame), asked for $($clean.ExpectFrame) -- see $($clean.Log)"
    } elseif ($clean.SinkPath -eq $null) {
        $problems += "clean: no '[core] log file=' line -- see $($clean.Log)"
    } elseif ($clean.HitchLines.Count -ne 0) {
        $problems += "clean: expected 0 hitch lines, saw $($clean.HitchLines.Count) -- see $($clean.Log)"
    } else {
        # ChangeExtension($path, $null) matches HitchSidecar.cs's own derivation in C# -- but
        # PowerShell coerces $null to an empty string on the way into a [string] parameter, which
        # ChangeExtension treats as "replace with a bare dot", leaving a double dot before
        # "hitches" (SinkPath.ext -> SinkPath..hitches.jsonl). Passing the compound extension
        # directly sidesteps the coercion instead of fighting it.
        $cleanSidecar = [System.IO.Path]::ChangeExtension($clean.SinkPath, "hitches.jsonl")
        if (-not (Test-Path $cleanSidecar)) {
            $problems += "clean: no sidecar at $cleanSidecar"
        } else {
            $cleanBytes = (Get-Item $cleanSidecar).Length
            if ($cleanBytes -gt 8) {
                $problems += "clean: sidecar $cleanSidecar is $cleanBytes bytes, expected the UTF-8 BOM alone (0 records)"
            }
        }
    }

    # Injected run: --hitch-inject=50@300 is the verified-safe pair (@120 sits inside the
    # grace window on the dev machine and is not portable).
    # Must trip exactly once, with a full ring and a sidecar record matching the printed line.
    $inject = Read-HitchRun -RunName "inject" -RunArgs @("--det", "--no-vsync", "--mute",
        "--hitch-inject=50@300") -ExpectFrame 310
    if ($inject.SimFrame -ne $inject.ExpectFrame) {
        $problems += "inject: ran to sim_frame=$($inject.SimFrame), asked for $($inject.ExpectFrame) -- see $($inject.Log)"
    } elseif ($inject.SinkPath -eq $null) {
        $problems += "inject: no '[core] log file=' line -- see $($inject.Log)"
    } elseif ($inject.HitchLines.Count -ne 1) {
        $problems += "inject: expected exactly 1 hitch line, saw $($inject.HitchLines.Count) -- see $($inject.Log)"
    } else {
        $line = $inject.HitchLines[0]
        if ($line.Frame -ne 300) {
            $problems += "inject: hitch fired on frame $($line.Frame), expected 300"
        }
        # 50 ms injected plus a normal frame's own cost; the upper bound is a sanity ceiling, not
        # a tight one -- a slower CI machine's baseline frame is not what this check is proving.
        if ($line.FrameMs -lt 45.0 -or $line.FrameMs -gt 150.0) {
            $problems += "inject: frame_ms=$($line.FrameMs) outside the expected ~50 ms+overhead band [45, 150]"
        }
        $injectSidecar = [System.IO.Path]::ChangeExtension($inject.SinkPath, "hitches.jsonl")
        if (-not (Test-Path $injectSidecar)) {
            $problems += "inject: no sidecar at $injectSidecar"
        } else {
            # .NET's reader, not Get-Content: PS 5.1 decodes a BOM-less line as the ANSI codepage.
            # Every field here is numeric so it cannot actually mojibake, but this stays the one
            # reading convention the whole script uses for a file HitchSidecar writes.
            $sidecarLines = @([System.IO.File]::ReadAllLines($injectSidecar) | Where-Object { $_.Trim().Length -gt 0 })
            if ($sidecarLines.Count -ne 1) {
                $problems += "inject: sidecar has $($sidecarLines.Count) record(s), expected 1 -- $injectSidecar"
            } else {
                $record = $null
                try {
                    $record = $sidecarLines[0] | ConvertFrom-Json
                } catch {
                    $problems += "inject: sidecar record did not parse as JSON -- $($_.Exception.Message)"
                }
                if ($record -ne $null) {
                    if ([int]$record.frame -ne $line.Frame) {
                        $problems += "inject: sidecar frame=$($record.frame), log line said $($line.Frame)"
                    }
                    if ([math]::Abs([double]$record.frame_ms - $line.FrameMs) -gt 0.05) {
                        $problems += "inject: sidecar frame_ms=$($record.frame_ms) does not match the log line's $($line.FrameMs)"
                    }
                    # RingFramesDefault (hitchMonitor.ringFrames): populated to depth is B7's own
                    # requirement, not just present -- a ring stuck at partial depth would still
                    # read as "a ring exists" without this count.
                    $ringCount = @($record.ring).Count
                    if ($ringCount -ne 120) {
                        $problems += "inject: sidecar ring has $ringCount entries, expected 120 (hitchMonitor.ringFrames default)"
                    }
                    # C8: what the frame could NAME plus what nothing claimed is the frame's cost,
                    # by construction. Asserted as the identity rather than as "no samples": the
                    # injected stall is deliberately not wrapped in a scope (B5's rule -- an
                    # injected fault must read as unattributed time), and C9 may well seed a site
                    # that does fire during this launch, but neither can break the sum.
                    $attributed = [double]$record.attributed_ms
                    $unattributed = [double]$record.unattributed_ms
                    if ([math]::Abs(($attributed + $unattributed) - [double]$record.frame_ms) -gt 0.005) {
                        $problems += "inject: attributed_ms=$attributed + unattributed_ms=$unattributed does not close over frame_ms=$($record.frame_ms)"
                    }
                    if ([int]$record.sample_violations -ne 0) {
                        $problems += "inject: sample_violations=$($record.sample_violations) -- a nested or unclosed PerfSample scope"
                    }
                }
            }
        }
    }
    $watch.Stop()

    $detail = "clean: $($clean.HitchLines.Count) hitch line(s); inject: $($inject.HitchLines.Count) hitch line(s), " +
        "$(if ($inject.HitchLines.Count -gt 0) { "frame_ms=$($inject.HitchLines[0].FrameMs)" } else { "n/a" })"
    if ($problems.Count -eq 0) {
        Add-Stage -Name "hitch" -Status "TODO" -Seconds $watch.Elapsed.TotalSeconds -Detail "$detail; awareness only"
    } else {
        Add-Stage -Name "hitch" -Status "TODO" -Seconds $watch.Elapsed.TotalSeconds -Detail "$detail; $($problems.Count) awareness item(s)"
        foreach ($p in $problems) {
            Write-Host "  !! hitch awareness: $p" -ForegroundColor Yellow
        }
    }
    Add-Unchecked "the hitch stage reports detector health but does not gate this workstation's verification"
}

# ---- perf --------------------------------------------------------------------------------

$PerfManifest = Join-Path $RepoRoot "analysis\perf\scenarios.json"
$PerfDir      = Join-Path $ScratchDir "perf"
$PerfHistory  = Join-Path $RepoRoot "perf-history.jsonl"
# The assembly Godot actually loads. Hashing it is the only honest answer to "did the new build
# run" (METHOD-6): a dirty tree gives A and B the same commit, and Copy-Item keeps mtimes, so
# nothing else distinguishes two builds of one revision.
$PerfDll      = Join-Path $ProjectDir ".godot\mono\temp\bin\Debug\CSVM.dll"

function Get-Median {
    param([double[]]$Values)
    if ($Values.Count -eq 0) {
        return [double]::NaN
    }
    $sorted = @($Values | Sort-Object)
    $mid = [int][math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return [double]$sorted[$mid]
    }
    return ([double]$sorted[$mid - 1] + [double]$sorted[$mid]) / 2.0
}

# Every key=value on a [perf] line whose value parses as a number, in the order the line wrote
# them. Non-numeric fields (mode=fly, chapter=C1) are identity, not measurement, and are dropped.
function Read-PerfLine {
    param([string]$Line)
    $out = New-Object System.Collections.Specialized.OrderedDictionary
    foreach ($m in [regex]::Matches($Line, '([A-Za-z_][A-Za-z0-9_]*)=([^\s]+)')) {
        $d = 0.0
        if ([double]::TryParse($m.Groups[2].Value, [System.Globalization.NumberStyles]::Float, $Inv, [ref]$d)) {
            $out[$m.Groups[1].Value] = $d
        }
    }
    return $out
}

function Format-PerfNumber {
    param([double]$Value)
    return $Value.ToString("0.####", $Inv)
}

function Get-FileMd5 {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        return ""
    }
    return (Get-FileHash -Path $Path -Algorithm MD5).Hash.ToLowerInvariant()
}

if (-not $Perf) {
    # Nothing to say: the stage is opt-in, and an absent stage is not an unchecked one.
} elseif (-not $buildOk) {
    Add-Stage -Name "perf" -Status "SKIP" -Seconds 0 -Detail "build failed"
    Add-Unchecked "the perf scenarios did not run (the build failed)"
} elseif (-not (Test-Path $GodotExe)) {
    Add-Stage -Name "perf" -Status "SKIP" -Seconds 0 -Detail "Godot not found at $GodotExe"
    Add-Unchecked "the perf scenarios did not run: no Godot at $GodotExe"
} elseif (-not (Test-Path $PerfManifest)) {
    Add-Stage -Name "perf" -Status "SKIP" -Seconds 0 -Detail "no manifest at $PerfManifest"
    Add-Unchecked "the perf scenarios did not run: no manifest at $PerfManifest"
} else {
    Write-Stage-Banner "perf"
    # Every launch here carries the .scratch\perf output path, an argument nothing else passes --
    # so the kill cannot reach a live playtest or a hand-run capture (SHELL-2).
    Stop-StrayGodots -Marker "\.scratch\perf\"
    if (-not $env:SDL_JOYSTICK_DIRECTINPUT) {
        $env:SDL_JOYSTICK_DIRECTINPUT = "0"
    }
    if (-not (Test-Path $PerfDir)) {
        $null = New-Item -ItemType Directory -Path $PerfDir
    }

    # SHELL-7: PS 5.1 decodes a BOM-less UTF-8 file as the ANSI codepage.
    $perfDoc = [System.IO.File]::ReadAllText($PerfManifest) | ConvertFrom-Json
    $perfFrameCount = [int]$perfDoc.frames
    if ($PerfFrames -gt 0) {
        $perfFrameCount = $PerfFrames
    }
    $perfIters = [int]$perfDoc.iterations
    if ($PerfIterations -gt 0) {
        $perfIters = $PerfIterations
    }
    $perfWarmups = [int]$perfDoc.warmups
    if ($perfWarmups -ge $perfIters) {
        $perfWarmups = $perfIters - 1
    }
    $dropFirstWindow = [bool]$perfDoc.dropFirstWindow
    $verdictMetrics = @($perfDoc.verdictMetrics)
    $awareness = @($perfDoc.awarenessMetrics)

    # Build identity, recorded on every line so a history entry can be traced to a tree state.
    $ErrorActionPreference = "Continue"
    $gitCommit = (& git -C $RepoRoot rev-parse --short HEAD 2>$null | Select-Object -First 1)
    $gitDescribe = (& git -C $RepoRoot describe --tags --always --dirty 2>$null | Select-Object -First 1)
    $gitStatus = @(& git -C $RepoRoot status --porcelain 2>$null)
    $ErrorActionPreference = "Stop"
    if (-not $gitCommit) { $gitCommit = "" }
    if (-not $gitDescribe) { $gitDescribe = "" }
    $gitDirty = ($gitStatus.Count -gt 0)
    $dllMd5 = Get-FileMd5 $PerfDll
    $runId = (Get-Date).ToString("yyyyMMdd-HHmmss", $Inv) + "-" + $PID
    $stamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", $Inv)

    Write-Host ("  build: commit $gitCommit dirty=$gitDirty dll_md5=$dllMd5") -ForegroundColor DarkGray
    Write-Host ("  protocol: $perfFrameCount sim frames x $perfIters launch(es) per scenario, first $perfWarmups discarded") -ForegroundColor DarkGray

    $perfScenarios = @($perfDoc.scenarios)
    if ($PerfFilter) {
        $perfScenarios = @($perfScenarios | Where-Object { $_.name -like "*$PerfFilter*" })
    }
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $perfBroken = @()
    $perfRecords = @()
    foreach ($scenario in $perfScenarios) {
        $windowSamples = @{}   # metric -> list of per-window values
        $startupSamples = @{}  # metric -> list of per-launch values
        $startupOrder = @()
        $windowOrder = @()
        $gpuName = ""
        $vsyncMode = ""
        $hitchCounts = New-Object System.Collections.ArrayList  # HitchMonitor trips, one entry per kept launch
        $launchOk = 0
        for ($iter = 1; $iter -le $perfIters; $iter++) {
            $tag = "$($scenario.name)-$iter"
            $log = Join-Path $PerfDir "$tag.log"
            $png = Join-Path $PerfDir "$tag.png"
            foreach ($stale in @($log, $png)) {
                if (Test-Path $stale) {
                    Remove-Item -Path $stale -Force
                }
            }
            # --frames=N --screenshot= is what ends the run at exactly N sim frames, and the
            # saved-shot line prints sim_frame= so the count is proved rather than assumed.
            $runArgs = @($scenario.args) + @("--det", "--mute", "--perf", "--no-vsync",
                                             "--frames=$perfFrameCount", "--screenshot=$png")
            $ErrorActionPreference = "Continue"
            $runCode = Invoke-Godot (@("--path", $ProjectDir, "--log-file", $log,
                                       "res://scenes/Main.tscn", "--") + $runArgs)
            $ErrorActionPreference = "Stop"

            if (-not (Test-Path $log)) {
                $perfBroken += "$tag : no engine log at $log (Godot exited $runCode)"
                continue
            }
            $windows = @()
            $startup = $null
            $simFrame = -1
            $hitchLineCount = 0
            foreach ($line in (Get-Content -Path $log)) {
                if ($line -match '\[perf\] window ') {
                    $windows += ,(Read-PerfLine $line)
                } elseif ($line -match '\[perf\] startup ') {
                    $startup = Read-PerfLine $line
                } elseif ($line -match 'screenshot saved: .* sim_frame=(\d+)') {
                    $simFrame = [int]$Matches[1]
                } elseif ($line -match 'shot pixmd5=\w+ size=\S+ gpu=(.*)$') {
                    $gpuName = $Matches[1].Trim()
                } elseif ($line -match '\[perf\] hitch ') {
                    # HitchMonitor tripped during this launch. Counted, never
                    # judged (E12's own Trap): no threshold here can fail a build.
                    $hitchLineCount++
                } elseif ($line -match '\[perf\] vsync (on|off)') {
                    # Carried per E14's comparability rule: hitch counts (and this stage's own
                    # max_ms/p95_ms) are not comparable across vsync modes.
                    $vsyncMode = $Matches[1]
                }
            }
            if ($simFrame -ne $perfFrameCount) {
                $perfBroken += "$tag : ran to sim_frame=$simFrame, asked for $perfFrameCount -- see $log"
                continue
            }
            if ($startup -eq $null) {
                $perfBroken += "$tag : no '[perf] startup' line -- see $log"
                continue
            }
            if ($dropFirstWindow -and $windows.Count -gt 1) {
                $windows = @($windows[1..($windows.Count - 1)])
            }
            if ($windows.Count -eq 0) {
                $perfBroken += "$tag : no usable '[perf] window' line -- see $log"
                continue
            }
            $launchOk++
            if ($iter -le $perfWarmups) {
                continue   # cache warm-up: it ran, and that is all it was for
            }
            $null = $hitchCounts.Add([double]$hitchLineCount)
            foreach ($w in $windows) {
                foreach ($key in $w.Keys) {
                    if ($key -eq "sim_frame" -or $key -eq "frames" -or $key -eq "wall_ms") {
                        continue   # run identity, not a measurement
                    }
                    if (-not $windowSamples.ContainsKey($key)) {
                        $windowSamples[$key] = New-Object System.Collections.ArrayList
                        $windowOrder += $key
                    }
                    $null = $windowSamples[$key].Add([double]$w[$key])
                }
            }
            foreach ($key in $startup.Keys) {
                if (-not $startupSamples.ContainsKey($key)) {
                    $startupSamples[$key] = New-Object System.Collections.ArrayList
                    $startupOrder += $key
                }
                $null = $startupSamples[$key].Add([double]$startup[$key])
            }
        }

        if ($windowSamples.Count -eq 0) {
            Write-Host "  FAIL $($scenario.name): no measured launch" -ForegroundColor Red
            continue
        }
        $metrics = New-Object System.Collections.Specialized.OrderedDictionary
        foreach ($key in $windowOrder) {
            $metrics[$key] = Get-Median @($windowSamples[$key].ToArray())
        }
        # E12: HitchMonitor trips this launch, median over the same kept-launch population as every
        # other metric here -- not a window metric (a trip is launch-wide, not per-60-frame-window),
        # but riding in $metrics is what makes it flow through -PerfCompare's existing ratio loop and
        # into the JSON "metrics" object for free.
        $metrics["hitch_count"] = Get-Median @($hitchCounts.ToArray())
        $startMetrics = New-Object System.Collections.Specialized.OrderedDictionary
        foreach ($key in $startupOrder) {
            $startMetrics[$key] = Get-Median @($startupSamples[$key].ToArray())
        }
        $windowCount = 0
        if ($windowOrder.Count -gt 0) {
            $windowCount = $windowSamples[$windowOrder[0]].Count
        }
        $perfRecords += [pscustomobject]@{
            scenario = $scenario.name
            windows  = $windowCount
            launches = $launchOk
            metrics  = $metrics
            startup  = $startMetrics
            gpu      = $gpuName
            vsync    = $vsyncMode
        }
        Write-Host ("  ok   {0,-12} windows {1}  render_cpu {2} ms  gpu {3} ms  draws {4}  startup total {5} ms  hitches {6}" -f `
            $scenario.name, $windowCount,
            (Format-PerfNumber $metrics["render_cpu_ms"]), (Format-PerfNumber $metrics["gpu_ms"]),
            (Format-PerfNumber $metrics["draws"]), (Format-PerfNumber $startMetrics["total"]),
            (Format-PerfNumber $metrics["hitch_count"])) -ForegroundColor DarkGray
    }
    $watch.Stop()

    # ---- history: one line per scenario per run, appended, never rewritten -------------------
    $historyLines = @()
    foreach ($rec in $perfRecords) {
        $sb = New-Object System.Text.StringBuilder
        $null = $sb.Append('{')
        $null = $sb.Append('"run":').Append((ConvertTo-JsonString $runId)).Append(',')
        $null = $sb.Append('"ts":').Append((ConvertTo-JsonString $stamp)).Append(',')
        $null = $sb.Append('"label":').Append((ConvertTo-JsonString $PerfLabel)).Append(',')
        $null = $sb.Append('"scenario":').Append((ConvertTo-JsonString $rec.scenario)).Append(',')
        $null = $sb.Append('"commit":').Append((ConvertTo-JsonString $gitCommit)).Append(',')
        $null = $sb.Append('"describe":').Append((ConvertTo-JsonString $gitDescribe)).Append(',')
        $null = $sb.Append('"dirty":').Append($(if ($gitDirty) { "true" } else { "false" })).Append(',')
        $null = $sb.Append('"dll_md5":').Append((ConvertTo-JsonString $dllMd5)).Append(',')
        $null = $sb.Append('"gpu":').Append((ConvertTo-JsonString $rec.gpu)).Append(',')
        # E12: carried explicitly (E14) -- hitch counts, and this stage's own max_ms/p95_ms, are not
        # comparable across vsync modes (a padded frame changes what a hitch even means).
        $null = $sb.Append('"vsync":').Append((ConvertTo-JsonString $rec.vsync)).Append(',')
        $null = $sb.Append('"frames":').Append($perfFrameCount).Append(',')
        $null = $sb.Append('"launches":').Append($rec.launches).Append(',')
        $null = $sb.Append('"warmups":').Append($perfWarmups).Append(',')
        $null = $sb.Append('"windows":').Append($rec.windows).Append(',')
        $null = $sb.Append('"metrics":{')
        $first = $true
        foreach ($key in $rec.metrics.Keys) {
            if (-not $first) { $null = $sb.Append(',') }
            $first = $false
            $null = $sb.Append((ConvertTo-JsonString $key)).Append(':').Append((Format-PerfNumber $rec.metrics[$key]))
        }
        $null = $sb.Append('},"startup":{')
        $first = $true
        foreach ($key in $rec.startup.Keys) {
            if (-not $first) { $null = $sb.Append(',') }
            $first = $false
            $null = $sb.Append((ConvertTo-JsonString $key)).Append(':').Append((Format-PerfNumber $rec.startup[$key]))
        }
        $null = $sb.Append('}}')
        $historyLines += $sb.ToString()
    }
    if ($historyLines.Count -gt 0) {
        # Appended as UTF-8 without BOM through .NET: Add-Content would write the ANSI codepage
        # and a second run would put a BOM in the middle of the file.
        [System.IO.File]::AppendAllText($PerfHistory, (($historyLines -join "`n") + "`n"),
                                        (New-Object System.Text.UTF8Encoding($false)))
    }

    # ---- A/B: pair against an earlier label and print ratios ---------------------------------
    if ($PerfCompare -and (Test-Path $PerfHistory)) {
        $baseByScenario = @{}
        foreach ($line in (Get-Content -Path $PerfHistory)) {
            if (-not $line.Trim()) {
                continue
            }
            $rec = $null
            try {
                $rec = $line | ConvertFrom-Json
            } catch {
                continue
            }
            if ($rec.label -eq $PerfCompare -and $rec.run -ne $runId) {
                $baseByScenario[$rec.scenario] = $rec   # later lines win: the most recent baseline
            }
        }
        Write-Host ""
        Write-Host "--- perf A/B: '$PerfLabel' vs '$PerfCompare' ----------------------------------" -ForegroundColor Cyan
        # A row is marked only when it clears BOTH the relative band and an absolute floor. The
        # floor is verification PERF-5 made mechanical: 0.045 ms of jitter on a 0.26 ms gpu figure
        # is a 17 % ratio and no difference at all.
        $verdictSet = @{}
        foreach ($m in $verdictMetrics) {
            $verdictSet[$m.metric] = $m
        }
        $startupBand = $perfDoc.startupBand
        $sameBinary = $false
        $flagged = @()
        foreach ($rec in $perfRecords) {
            $base = $baseByScenario[$rec.scenario]
            if ($base -eq $null) {
                Write-Host ("  {0}: no '{1}' record to pair against" -f $rec.scenario, $PerfCompare) -ForegroundColor Yellow
                continue
            }
            if ($base.dll_md5 -eq $dllMd5 -and $dllMd5 -ne "") {
                $sameBinary = $true
            }
            Write-Host ("  {0}   base commit {1} dll {2}" -f $rec.scenario, $base.commit, $base.dll_md5.Substring(0, [math]::Min(8, $base.dll_md5.Length))) -ForegroundColor DarkGray
            foreach ($section in @("metrics", "startup")) {
                $mine = $rec.$section
                $theirs = $base.$section
                foreach ($key in $mine.Keys) {
                    $prop = $theirs.PSObject.Properties[$key]
                    if ($prop -eq $null) {
                        continue
                    }
                    $b = [double]$prop.Value
                    $c = [double]$mine[$key]
                    $label = $key
                    $isVerdict = $true
                    $band = $startupBand
                    if ($section -eq "metrics") {
                        $isVerdict = $verdictSet.ContainsKey($key)
                        if ($isVerdict) {
                            $band = $verdictSet[$key]
                        }
                    } else {
                        $label = "startup." + $key
                    }
                    if ($b -eq 0) {
                        $ratioText = "  n/a"
                        $flag = " "
                    } else {
                        $ratio = $c / $b
                        $ratioText = $ratio.ToString("0.000", $Inv)
                        $flag = " "
                        $outsideBand = ([math]::Abs($ratio - 1.0) -gt [double]$band.band)
                        $aboveFloor = ([math]::Abs($c - $b) -ge [double]$band.minAbs)
                        if ($isVerdict -and $outsideBand -and $aboveFloor) {
                            $flag = "*"
                            $flagged += ("{0} {1} {2} -> {3} (x{4})" -f $rec.scenario, $label,
                                         (Format-PerfNumber $b), (Format-PerfNumber $c), $ratioText)
                        }
                    }
                    $color = "DarkGray"
                    if (-not $isVerdict) {
                        $color = "DarkYellow"
                    } elseif ($flag -eq "*") {
                        $color = "Yellow"
                    }
                    $note = ""
                    if (-not $isVerdict) {
                        $note = "  (awareness only)"
                    }
                    Write-Host ("     {0}{1,-20} {2,12} -> {3,12}   x{4}{5}" -f `
                        $flag, $label, (Format-PerfNumber $b), (Format-PerfNumber $c), $ratioText, $note) -ForegroundColor $color
                }
            }
        }
        Write-Host ""
        if ($flagged.Count -eq 0) {
            Write-Host "  nothing outside the recorded same-build bands." -ForegroundColor Green
        } else {
            Write-Host "  outside the recorded same-build bands:" -ForegroundColor Yellow
            foreach ($f in $flagged) {
                Write-Host "    * $f" -ForegroundColor Yellow
            }
        }
        Write-Host "  read the ratios like this:" -ForegroundColor Yellow
        Write-Host "    * marks a verdict metric past BOTH its relative band and its absolute floor -- a candidate, never a verdict. Nothing here fails." -ForegroundColor Yellow
        Write-Host "    the bands are machine- and day-specific: re-measure by running the suite twice unchanged (METHOD-2, METHOD-3, PERF-5)." -ForegroundColor Yellow
        foreach ($a in $awareness) {
            Write-Host ("    $($a.metric): $($a.why)") -ForegroundColor DarkYellow
        }
        if ($sameBinary) {
            Write-Host "  SAME BINARY: base and change share a CSVM.dll hash -- this is a same-build noise floor, not an A/B (METHOD-6)." -ForegroundColor Yellow
        } else {
            Write-Host "  binaries differ (CSVM.dll hash), so the new build did run (METHOD-6)." -ForegroundColor DarkGray
        }
    }

    $scenarioCount = @($perfScenarios).Count
    $detail = "$($perfRecords.Count)/$scenarioCount scenario(s), $perfFrameCount sim frames x $perfIters launch(es), history -> $PerfHistory"
    if ($PerfCompare) {
        $detail = "$detail; paired against '$PerfCompare'"
    }
    if ($perfBroken.Count -gt 0 -or $perfRecords.Count -ne $scenarioCount) {
        Add-Stage -Name "perf" -Status "FAIL" -Seconds $watch.Elapsed.TotalSeconds -Detail "$detail; $($perfBroken.Count) launch(es) unusable"
        foreach ($b in $perfBroken) {
            Write-Host "  !! $b" -ForegroundColor Red
        }
    } else {
        Add-Stage -Name "perf" -Status "PASS" -Seconds $watch.Elapsed.TotalSeconds -Detail $detail
    }
    Add-Unchecked "the perf stage measured and recorded, it did not judge: no threshold here can fail a build, and a history trend is awareness. A regression verdict needs a paired A/B (-PerfCompare) read against a freshly measured same-build noise band"
}

# ---- summary -----------------------------------------------------------------------------

$nameWidth = 4
foreach ($stage in $Stages) {
    if ($stage.Name.Length -gt $nameWidth) {
        $nameWidth = $stage.Name.Length
    }
}
$failedStages = @($Stages | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object { $_.Name })
$totalSeconds = 0.0
foreach ($stage in $Stages) {
    $totalSeconds += $stage.Seconds
}

Write-Host ""
Write-Host "--- RunTests ------------------------------------------------------------"
foreach ($stage in $Stages) {
    $line = "  {0}  {1}  {2,6}s  {3}" -f $stage.Status, $stage.Name.PadRight($nameWidth), (Format-Seconds $stage.Seconds), $stage.Detail
    Write-Host $line -ForegroundColor (Get-StatusColor $stage.Status)
}
foreach ($what in $Unchecked) {
    Write-Host "   .  not checked: $what" -ForegroundColor Yellow
}
$dataRootLine = "  data root: "
if ($env:CSVM_DATA_ROOT) {
    $dataRootLine += "$env:CSVM_DATA_ROOT (CSVM_DATA_ROOT)"
} else {
    $dataRootLine += "$RepoRoot (this tree)"
}
Write-Host $dataRootLine
# Say which desktop the launches went to. A silent fallback looks exactly like success until the
# windows start appearing, and by then nobody connects the two.
if ($HiddenDesktop) {
    Write-Host "  windows: hidden desktop '$([CSVMHiddenDesktop]::Name)' -- nothing was drawn on your screen"
} else {
    Write-Host "  windows: THIS desktop -- the hidden one was refused, so launches were visible" -ForegroundColor Yellow
}
Close-HiddenDesktop
if ($failedStages.Count -gt 0) {
    Write-Host ("  result: FAIL in {0} -- {1}s total, exit 1" -f ($failedStages -join ", "), (Format-Seconds $totalSeconds)) -ForegroundColor Red
} else {
    Write-Host ("  result: PASS -- {0}s total, exit 0" -f (Format-Seconds $totalSeconds)) -ForegroundColor Green
}
Write-Host "-------------------------------------------------------------------------"

if ($failedStages.Count -gt 0) {
    exit 1
}
exit 0
