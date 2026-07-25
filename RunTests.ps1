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
               scraped out of console text.
      engine   Godot with --run-tests, the in-engine assertion suites. Windowed (never
               --headless: no shaders compile there, so a clean error screen would prove
               nothing) and with --log-file, which is what lets the harness screen native
               engine ERROR lines. --run-tests implies --det by itself.
      goldens  Not implemented yet -- the seam is here so the pipeline has a place for the
               golden-image tripwire, and so its absence is visible rather than assumed.
      perf     -Perf only. Not implemented yet, same reason.

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

.PARAMETER Perf
    Also report the perf stage (not implemented yet).

.EXAMPLE
    .\RunTests.ps1
    Build, the unit tests, the in-engine suites, one summary block, one exit code.

.EXAMPLE
    .\RunTests.ps1 -Filter weapons -SkipUnits
    Build, then only the in-engine suites whose name contains "weapons".

.EXAMPLE
    $env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'; .\RunTests.ps1
    The same run from a git worktree, reading the primary tree's data and Godot.
#>

[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$SkipUnits,
    [switch]$SkipEngine,
    [switch]$Perf
)

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$ScratchDir = Join-Path $RepoRoot ".scratch"
$Inv        = [System.Globalization.CultureInfo]::InvariantCulture

# tools/ is git-ignored, so a git worktree checkout has no Godot. Fall back to the primary
# tree named by CSVM_DATA_ROOT -- the same env var the engine and the unit tests read for
# extracted/, so one `$env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'` makes a worktree runnable.
$GodotRel = "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"
$GodotExe = Join-Path $RepoRoot $GodotRel
if ((-not (Test-Path $GodotExe)) -and $env:CSVM_DATA_ROOT) {
    $GodotExe = Join-Path $env:CSVM_DATA_ROOT $GodotRel
}

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $ScratchDir)) {
    $null = New-Item -ItemType Directory -Path $ScratchDir
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
    if ($Status -eq "TODO") { return "DarkYellow" }
    return "Yellow"
}

# Rule 66: a stray Godot from an earlier run poisons the next one's error census and its
# window. Two filters, deliberately: a leftover of THIS script -- this tree's project dir
# AND --run-tests, which always quits by itself, so one still alive is stuck -- gets killed;
# any other Godot on this tree is only reported. A live playtest or another agent's session
# is not ours to kill, and "everything launched against this tree" catches both.
function Stop-StrayGodots {
    $killed = 0
    $mine = @()
    $others = @()
    $godots = @(Get-CimInstance Win32_Process -Filter "Name LIKE 'Godot%'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.IndexOf($ProjectDir, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    foreach ($godot in $godots) {
        if ($godot.CommandLine.IndexOf("--run-tests", [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
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
    Stop-StrayGodots

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
    & $GodotExe --path $ProjectDir --log-file $engineLog res://scenes/Main.tscn -- $testArg
    $engineCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    $watch.Stop()

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
    if ($ePassed -ge 0) {
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
    if ($engineCode -eq 0) {
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

# The seam for the golden-image tripwire. It reports TODO rather than PASS on purpose: a
# stage that reports green while checking nothing is worse than one that is missing.
Add-Stage -Name "goldens" -Status "TODO" -Seconds 0 -Detail "not implemented yet -- no golden hashes are captured or compared"
Add-Unchecked "no golden-image compare exists yet: pixel regressions are not caught by this script"

# ---- perf --------------------------------------------------------------------------------

if ($Perf) {
    Add-Stage -Name "perf" -Status "TODO" -Seconds 0 -Detail "not implemented yet -- no scenario set, no A/B, no history store"
    Add-Unchecked "-Perf measured nothing: the perf harness does not exist yet"
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
