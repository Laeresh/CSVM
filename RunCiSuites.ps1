<#
.SYNOPSIS
    The CI engine stage: the in-engine suites a selector names (default the ci tier), run in one
    headless Godot over the synthetic data root (--synthetic-data, written over an empty data root)
    and judged from the harness's report. Exit 0 only when every selected suite passed and the
    engine log held no error a headless run does not explain.

.DESCRIPTION
    What the engine job in .github/workflows/checks.yml runs, and what a contributor runs on Linux
    or macOS to reproduce it. It does not build or import; the job's steps before it, and a local
    run, do:

        dotnet build CSVM/CSVM.sln
        <godot> --headless --path CSVM --import
        ./RunCiSuites.ps1 -Godot <godot>

    The Godot process gets CSVM_DATA_ROOT at a fresh empty folder, so no suite reads an extraction,
    this tree's own extracted/ included. It also gets --synthetic-data, so the harness writes the
    invented tree of CSVM.Tests/fixtures/ records into .scratch/synthetic-data/<pid>/ and reads that
    in place of the empty folder; -NoSyntheticData runs over the empty folder alone. It gets
    XDG_DATA_HOME, XDG_CONFIG_HOME and XDG_CACHE_HOME at
    fresh folders, so user:// starts empty on Linux (macOS Godot ignores them and keeps the user's
    own). Everything lands in .scratch/ci-suites/, wiped at the start of each run: the process's
    stdout and stderr, and a copy of the engine log the harness screened. The report is the
    harness's own .scratch/test-report.json.

    The verdict is drawn from that report, not from the exit code, because the harness also exits
    1 on an engine error line its allowlist does not cover, and a headless process prints lines a
    windowed one does not. analysis/headless-limits.json lists those patterns, and this script
    applies them exactly as sandbox/LinuxRelease.ps1 does. The run fails on any of:
      - Godot killed by the watchdog, exiting with anything but 0 or 1, or writing no fresh report
      - a report for another selector, data root or port base than this run's, or whose
        syntheticData flag disagrees with the switch this run passed
      - a FAILed suite; under tier:ci a listed suite that SKIPs is already a FAIL in the report
      - an engine log the harness did not screen, or an allowlisted pattern over its cap
      - an unexpected engine error line that no headless pattern matches

.PARAMETER Godot
    The Godot 4.7 .NET executable (the mono build), e.g.
    Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64.

.PARAMETER Selector
    The --run-tests selector. Default tier:ci, the list CI requires to pass.

.PARAMETER NetPortBase
    The first port the suites open sockets from. Default 30000, below Linux's ephemeral range and
    clear of the shipped game's 47500/47501.

.PARAMETER TimeoutSec
    Kill the run after this many seconds and fail it. Default 900.

.PARAMETER NoSyntheticData
    Run over the empty data root alone, without --synthetic-data. The ci tier fails that way, since
    its plane suites then skip; it is for reading what the rest of a selection does with no data.

.EXAMPLE
    ./RunCiSuites.ps1 -Godot ~/godot/Godot_v4.7-stable_mono_linux_x86_64/Godot_v4.7-stable_mono_linux.x86_64

.EXAMPLE
    ./RunCiSuites.ps1 -Godot $godot -Selector suite:menu-zone-layout
#>
#Requires -Version 7.0
param(
    [Parameter(Mandatory = $true)][string]$Godot,
    [string]$Selector = "tier:ci",
    [int]$NetPortBase = 30000,
    [int]$TimeoutSec = 900,
    [switch]$NoSyntheticData
)

$ErrorActionPreference = "Stop"
$Inv = [Globalization.CultureInfo]::InvariantCulture
$Utf8 = New-Object System.Text.UTF8Encoding($false)
$RepoRoot = $PSScriptRoot
$Scratch = Join-Path $RepoRoot ".scratch"
$OutDir = Join-Path $Scratch "ci-suites"
$DataRoot = Join-Path $OutDir "empty-data-root"
$Report = Join-Path $Scratch "test-report.json"

if (-not (Test-Path -LiteralPath $Godot -PathType Leaf)) {
    throw "No Godot executable at $Godot -- pass the Godot 4.7 .NET (mono) binary as -Godot."
}
$Godot = (Resolve-Path -LiteralPath $Godot).Path

# Godot's Linux build loads fontconfig for system fonts and logs an engine error on every lookup
# without it, which would read as unexpected errors; sandbox/LinuxRelease.ps1 refuses the same way.
if ($IsLinux) {
    $ldconfig = if (Test-Path /sbin/ldconfig) { "/sbin/ldconfig" } else { "ldconfig" }
    if (-not (@(& $ldconfig -p) | Where-Object { $_ -match 'libfontconfig\.so\.1' })) {
        throw "No libfontconfig.so.1 on this system -- install it (Debian and Ubuntu: 'sudo apt-get install libfontconfig1')."
    }
}

$limits = [IO.File]::ReadAllText((Join-Path $RepoRoot "analysis/headless-limits.json"), $Utf8) | ConvertFrom-Json
$HeadlessEngineErrors = [ordered]@{}
foreach ($p in $limits.headlessEngineErrors.PSObject.Properties) { $HeadlessEngineErrors[$p.Name] = [string]$p.Value }

if (Test-Path -LiteralPath $OutDir) { Remove-Item -LiteralPath $OutDir -Recurse -Force }
$xdg = Join-Path $OutDir "xdg"
foreach ($dir in @($DataRoot, "$xdg/data", "$xdg/config", "$xdg/cache")) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}
# A report left by an earlier run would otherwise be judged as this one's.
if (Test-Path -LiteralPath $Report) { Remove-Item -LiteralPath $Report -Force }

$psi = [Diagnostics.ProcessStartInfo]::new($Godot)
foreach ($a in @("--headless", "--path", (Join-Path $RepoRoot "CSVM"), "res://scenes/Main.tscn", "--",
                 "--run-tests=$Selector", "--net-port-base=$NetPortBase")) {
    $psi.ArgumentList.Add($a)
}
if (-not $NoSyntheticData) { $psi.ArgumentList.Add("--synthetic-data") }
$psi.WorkingDirectory = $RepoRoot
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.Environment["CSVM_DATA_ROOT"] = $DataRoot
$psi.Environment["XDG_DATA_HOME"] = "$xdg/data"
$psi.Environment["XDG_CONFIG_HOME"] = "$xdg/config"
$psi.Environment["XDG_CACHE_HOME"] = "$xdg/cache"

Write-Host "== engine suites, headless: --run-tests=$Selector =="
Write-Host "  godot      $Godot"
Write-Host "  data root  $DataRoot (empty)$(if ($NoSyntheticData) { '' } else { ', replaced by --synthetic-data' })"
Write-Host "  output     $OutDir"
$watch = [Diagnostics.Stopwatch]::StartNew()
$proc = [Diagnostics.Process]::Start($psi)
$stdout = $proc.StandardOutput.ReadToEndAsync()
$stderr = $proc.StandardError.ReadToEndAsync()
if ($proc.WaitForExit($TimeoutSec * 1000)) {
    $proc.WaitForExit()
    $exit = $proc.ExitCode
} else {
    $proc.Kill($true)
    $proc.WaitForExit()
    $exit = 124
}
$seconds = $watch.Elapsed.TotalSeconds
[IO.File]::WriteAllText((Join-Path $OutDir "godot.out"), $stdout.GetAwaiter().GetResult(), $Utf8)
[IO.File]::WriteAllText((Join-Path $OutDir "godot.err"), $stderr.GetAwaiter().GetResult(), $Utf8)

function Get-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
}

$problems = New-Object System.Collections.ArrayList
$rows = @()
$headlessSeen = [ordered]@{}
if ($exit -eq 124) {
    $null = $problems.Add("Godot ran past the ${TimeoutSec}s watchdog and was killed; see godot.out")
}
if (-not (Test-Path -LiteralPath $Report)) {
    $missed = @([IO.File]::ReadAllLines((Join-Path $OutDir "godot.out")) | Where-Object { $_ -match 'selector matched nothing|matched no suite' })
    $why = if ($missed.Count -gt 0) { ": $($missed[0].Trim())" } else { "; see godot.out and godot.err" }
    $null = $problems.Add("Godot exited $exit and wrote no report$why")
} else {
    $json = [IO.File]::ReadAllText($Report, $Utf8) | ConvertFrom-Json
    if ($exit -ne 0 -and $exit -ne 1 -and $exit -ne 124) {
        $null = $problems.Add("Godot exited $exit after writing its report, a crash in its teardown; see godot.err")
    }
    if ($json.selector -ne $Selector) {
        $null = $problems.Add("the report is for selector '$($json.selector)', not '$Selector'")
    }
    # With the switch the harness reads the tree it wrote for its own process id, never the empty
    # folder, so that tree is the one root a fresh, synthetic report may name.
    $wantRoot = if ($NoSyntheticData) { $DataRoot } else { Join-Path $Scratch "synthetic-data/$($proc.Id)" }
    $wantWhat = if ($NoSyntheticData) { "the empty" } else { "this run's synthetic" }
    if (-not $json.dataRoot -or (Get-FullPath $json.dataRoot) -ne (Get-FullPath $wantRoot)) {
        $null = $problems.Add("the report read data root '$($json.dataRoot)', not $wantWhat $wantRoot")
    }
    if ([bool]$json.syntheticData -ne (-not $NoSyntheticData)) {
        $null = $problems.Add("the report's syntheticData is '$($json.syntheticData)', but this run $(if ($NoSyntheticData) { 'did not pass' } else { 'passed' }) --synthetic-data")
    }
    if ($null -eq $json.shard.netPortBase -or [int]$json.shard.netPortBase -ne $NetPortBase) {
        $null = $problems.Add("the report opened its sockets from net port base '$($json.shard.netPortBase)', not $NetPortBase")
    }
    $rows = @($json.suites)
    if ($rows.Count -eq 0) {
        $null = $problems.Add("the report holds no suite")
    }
    $logFile = $json.engineErrors.logFile
    if ($logFile -and (Test-Path -LiteralPath $logFile)) {
        Copy-Item -LiteralPath $logFile -Destination (Join-Path $OutDir "godot.log")
    }
    if (-not $json.engineErrors.screened) {
        $null = $problems.Add("the harness did not screen the engine log ($logFile), so its error lines went unchecked")
    }
    foreach ($a in @($json.engineErrors.allowlist)) {
        if ([int]$a.seen -gt [int]$a.max) {
            $null = $problems.Add("allowlisted engine error over its cap: $($a.pattern) seen $($a.seen)x, allowed $($a.max)x, $($a.why)")
        }
    }
    $unexpected = [ordered]@{}
    foreach ($u in @($json.engineErrors.unexpected)) {
        $line = $u -replace '\s+', ' '
        $headless = $HeadlessEngineErrors.Keys | Where-Object { $line -match $_ } | Select-Object -First 1
        if ($headless) { $headlessSeen[$headless] = [int]$headlessSeen[$headless] + 1 }
        else { $unexpected[$line] = [int]$unexpected[$line] + 1 }
    }
    foreach ($line in $unexpected.Keys) {
        $null = $problems.Add("unexpected engine error ($($unexpected[$line])x): $line")
    }
}

$pass = 0; $fail = 0; $skip = 0
foreach ($suite in ($rows | Sort-Object { [int]$_.index })) {
    if ($suite.status -eq "pass") { $pass++; continue }
    if ($suite.status -eq "skip") {
        $skip++
        Write-Host ("  SKIP  {0}  {1}" -f $suite.name, $suite.detail) -ForegroundColor Yellow
        continue
    }
    $fail++
    Write-Host ("  FAIL  {0}" -f $suite.name) -ForegroundColor Red
    foreach ($f in @($suite.failures)) { Write-Host "          !! $f" -ForegroundColor Red }
}
foreach ($p in $problems) { Write-Host "  !! $p" -ForegroundColor Red }
foreach ($pattern in $HeadlessEngineErrors.Keys) {
    Write-Host ("  allowed headless engine error /{0}/ seen {1}x: {2}" -f $pattern, [int]$headlessSeen[$pattern], $HeadlessEngineErrors[$pattern]) -ForegroundColor DarkGray
}
Write-Host ("  {0} passed, {1} failed, {2} skipped, exit {3}, {4}s" -f $pass, $fail, $skip, $exit, $seconds.ToString("0", $Inv))
if ($fail -gt 0 -or $problems.Count -gt 0) {
    Write-Host "CI SUITES FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "CI SUITES PASSED" -ForegroundColor Green
exit 0
